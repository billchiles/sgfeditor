### sgfpy.py is the __main__ kicker file and implements the GUI for an SGF
### editor written with IronPython and WPF.
###
### NOTE: all top-level functions are only used internally, but not all are
### named with the leading underscore.  If this module were ever to be consumed
### by another should more carefully name top-level functions and think about
### the module's contract.
###

import wpf
import clr

### Don't need this now due to new wpf module, but left as documentation of
### usage.
###
### Needed for System.Windows Application, Markup, Controls, Shapes, etc.
clr.AddReference('PresentationFramework')
### Needed for System.Windows.Media ...
clr.AddReference("PresentationCore")
### Needed for Point
clr.AddReference("WindowsBase")


#from System.Windows.Markup import XamlReader
from System.Windows import Application, Window, HorizontalAlignment, VerticalAlignment, Thickness
from System.Windows import GridLength, GridUnitType, MessageBox, Point, RoutedEventHandler
from System.Windows import MessageBoxButton, MessageBoxResult, Rect, Size
from System.Windows import FontWeights
from System.Windows.Controls import Grid, ColumnDefinition, RowDefinition, Label, Viewbox
from System.Windows.Controls import ComboBox, ComboBoxItem, TreeView, TreeViewItem
from System.Windows.Media import SolidColorBrush, Colors, Color
from System.Windows.Data import Binding, RelativeSource
from System.Windows.Shapes import Rectangle, Ellipse, Polygon
from System.Windows.Input import MouseButtonEventHandler, Key, Keyboard, ModifierKeys
from System.Windows.FrameworkElement import WidthProperty
from System.IO import FileFormatException #FileStream, FileMode
from Microsoft.Win32 import OpenFileDialog, SaveFileDialog

#import sys
#print sys.path
#print __file__


import game
from goboard import Adornments
import sgfparser
import newdialog


__all__ = ["SgfEdWindow"]

help_string = """SGFEd can read and write .sgf files, edit game trees, etc.

PLACING STONES AND ANNOTATIONS:
Click on board location to place alternating colored stones.
Shift click to place square annotations, ctrl click for triangles, and
alt click to place letter annotations.

KEEPING FOCUS ON BOARD FOR KEY BINDINGS
Escape will always return focus to the board so that the arrow keys work
and are not swallowed by the comment editing pane.  Sometimes opening and
saving files leaves WPF in a weird state such that you must click somewhere
to fix focus.

NAVIGATING MOVES IN GAME TREE
Right arrow moves to the next move, left moves to the previous, up arrow selects
another branch or the main branch, down arrow selects another branch, home moves
to the game start, and end moves to the end of the game following the currently
selected branches.

CREATING NEW FILES
The new button (or ctrl-n) prompts for game info (player names, board size,
handicap, komi) and creates a new game.  If the current game is dirty, this prompts
to save.

OPENING EXISTING FILES
The open button (or ctrl-o) prompts for a .sgf file name to open.  If the current
game is dirty, this prompts to save.

SAVING FILES, SAVE AS
The save button (or ctrl-s) saves to the associated file name if there is one;
otherwise it prompts for a filename.  If there is a filename, but the game state
is not dirty, then it prompts to save to a different filename (and tracks to the
new name).  To explicitly get save-as behaivor, use ctrl-alt-s.

SAVING REVERSE VIEW
To save the game so that your opponent can review it from his point of view, use
ctrl-alt-f.  (Would have preferred ctrl-shift-s or alt-s, but WPF was difficult.)

CUTTING MOVES/SUB-TREES AND PASTING
Delete or c-x cuts the current move (and sub tree), making the previous move the
current move.  C-v will paste a cut sub tree to be a next move after the current
move.  If the the sub tree has a move that occupies a board location that already
has a stone, you will not be able to advance past this position.

MOVING BRANCHES
You can move branches up and down in the order in which they show up in the branch
combo, including changing what is the main line branch of the game.  To move a
a branch up, use ctrl-uparrow, and to move a branch down, use ctrl-downarrow.

F1 produces this help.
"""



class SgfEdWindow(Window):
    def __init__(self):
        ## LoadComponent reads xaml to fill in Window contents, creates
        ## SgfEdWindow members for named elements in the xaml, and hooks up
        ## event handling methods.
        wpf.LoadComponent(self, "sgfpy.xaml")
        self.Width = 1050
        self.Height = 700
        self._prev_setup_size = None
        self.game = game.create_default_game(self)

    ### setup_board_display sets up the lines and stones hit grids.  Game uses
    ### this to call back on the main UI when constructing a Game.  This also
    ### handles when the game gets reset to a new game from a file or starting
    ### fresh new game.
    ###
    def setup_board_display (self, new_game):
        if self._prev_setup_size is None:
            ## First time setup.
            self._setup_lines_grid(new_game.board.size)
            self._setup_stones_grid(new_game.board.size)
            self._prev_setup_size = new_game.board.size
            self.add_handicap_stones(new_game)
        elif self._prev_setup_size == new_game.board.size:
            ## Clean up and re-use same sized model objects.
            cur_move = self.game.current_move
            g = self.stonesGrid
            ## Must remove current adornment before other adornments.
            if cur_move is not None:
                remove_current_stone_adornment(g, cur_move)
            ## Remove stones and adornments, but not labels.
            for elt in [o for o in g.Children if type(o) is not Label]:
                ## Only labels in stones grid should be row/col labels
                ## because letter adornments are inside ViewBoxes.
                g.Children.Remove(elt)
            ## Clear board just to make sure we drop all model refs.
            self.game.board.goto_start();
            self.prevButton.IsEnabled = False;
            self.homeButton.IsEnabled = False;
            ## If opening game file, next/end set to true by game code.
            self.nextButton.IsEnabled = False;
            self.endButton.IsEnabled = False;
            self.gameTreeView.Items.Clear()
            global stones
            stones = [[None for y in xrange(game.MAX_BOARD_SIZE)] for
                      x in xrange(game.MAX_BOARD_SIZE)]
            self.add_handicap_stones(new_game)
        else:
            raise Exception("Haven't implemented changing board size for new games.")
        ##
        ## Initialize tree view.
        root = TreeViewItem()
        l = Label()
        l.Content = "Start"
        l.Background = SolidColorBrush(Colors.LightSkyBlue)
        root.Header = l
        root.IsExpanded = True
        self.gameTreeView.Items.Add(root)
        root.IsSelected = True


    ### create_lines_grid takes an int for the board size (as int) and returns a WPF Grid object for
    ### adding to the MainWindow's Grid.  The returned Grid contains lines for the Go board.
    ###
    def _setup_lines_grid (self, size):
        #g = self.FindName("boardGrid") #Grid();
        g = self.boardGrid
        #g.ShowGridLines = True
        _define_lines_columns(g, size)
        _define_lines_rows(g, size)
        _place_lines(g, size)
        if size == game.MAX_BOARD_SIZE:
            add_handicap_point(g, 4, 4)
            add_handicap_point(g, 4, 10)
            add_handicap_point(g, 4, 16)
            add_handicap_point(g, 10, 4)
            add_handicap_point(g, 10, 10)
            add_handicap_point(g, 10, 16)
            add_handicap_point(g, 16, 4)
            add_handicap_point(g, 16, 10)
            add_handicap_point(g, 16, 16)
        return g

    ### _setup_stones_grid takes an int for the size of the go board and returns
    ### a Grid to which we can add stones and use for hit testing mouse clicks.
    ###
    def _setup_stones_grid (self, size):
        #g = self.FindName("stonesGrid")
        g = self.stonesGrid
        ## Define rows and columns
        for i in xrange(size + 2):
            col_def = ColumnDefinition()
            col_def.Width = GridLength(1, GridUnitType.Star)
            g.ColumnDefinitions.Add(col_def)
            row_def = RowDefinition()
            row_def.Height = GridLength(1, GridUnitType.Star)
            g.RowDefinitions.Add(row_def)
        _setup_index_labels(g, size)
        #g.MouseLeftButtonDown += MouseButtonEventHandler(stones_mouse_left_down)
        return g
    
    ###
    ### Input Handling
    ###

    ### helpButton_left_down prints help.
    ###
    def helpButton_left_down (self, home_button,  e):
        MessageBox.Show(help_string, "SGFEd Help")
        self._focus_on_stones()

    ### stones_mouse_left_down handles stonesGrid clicks to add moves as if a
    ### game is in session, add adornments, and handle the current move
    ### adornment.
    ###
    def stones_mouse_left_down (self, stones_grid,  e):
        col, row = grid_pixels_to_cell(stones_grid, e.GetPosition(stones_grid).X,
                                       e.GetPosition(stones_grid).Y)
        ## cell x,y is col, row from top left, and board is row, col.
        if Keyboard.Modifiers == ModifierKeys.Shift:
            add_or_remove_adornment(stones_grid, row, col, Adornments.square, self.game)
        elif Keyboard.Modifiers == ModifierKeys.Control:
            add_or_remove_adornment(stones_grid, row, col, Adornments.triangle, self.game)
        elif Keyboard.Modifiers == ModifierKeys.Alt:
            add_or_remove_adornment(stones_grid, row, col, Adornments.letter, self.game)
        else:
            move = self.game.make_move(row, col)
            if move is not None:
                self._advance_to_stone(move)
        self._focus_on_stones()
    
    ### prevButton_left_down handles the rewind one move button.  Also,
    ### mainwin_keydown calls this to handle left arrow.  This also handles
    ### removing and restoring adornments, and handling the current move
    ### adornment.  This function assume the game is started, and there's a
    ### move to rewind.
    ###
    def prevButton_left_down (self, prev_button,  e):
        move = self.game.unwind_move()
        #remove_stone(main_win.FindName("stonesGrid"), move)
        if not move.is_pass:
            remove_stone(self.stonesGrid, move)
        if move.previous is not None:
            self.add_current_adornments(move.previous)
        else:
            restore_adornments(self.stonesGrid, self.game.setup_adornments)
        self._update_tree_view(move.previous)
        if self.game.current_move is not None:
            m = self.game.current_move
            self.update_title(m.number, m.is_pass)
        else:
            self.update_title(0)
        self._focus_on_stones()
    
    ### homeButton_left_down rewinds all moves to the game start.  This
    ### function signals an error if the game has not started, or no move has
    ### been played.
    ###
    def homeButton_left_down (self, home_button,  e):
        self.game.goto_start()
        self.update_title(0)
        self._focus_on_stones()

    ### nextButton_left_down handles the replay one move button.  Also,
    ### mainwin_keydown calls this to handle left arrow.  This also handles
    ### removing and restoring adornments, and handling the current move
    ### adornment.  This function assumes the game has started, and there's
    ### a next move to replay.
    ###
    def nextButton_left_down (self, next_button,  e):
        move = self.game.replay_move()
        if move is None:
            MessageBox.Show("Can't play branch further due to conflicting stones on the board.")
            return
        self._advance_to_stone(move)
        self._update_tree_view(move)
        self._focus_on_stones()

    ### _advance_to_stone displays move, which as already been added to the
    ### board and readied for rendering.  We add the stone with no current
    ### adornment because that does the basic work and then immediately add the
    ### adornment.
    ###
    def _advance_to_stone (self, move):
        self.add_next_stone_no_current(move)
        self.add_current_adornments(move)
        self._update_tree_view(move)
        self.update_title(move.number, move.is_pass)
    
    
    ### _tree_moves maps move objects to TreeViewItems to support finding
    ### whether moves have been added to the tree view yet and to help
    ### scrolling to them if they have been added.  We do not put a tree view
    ### cookie in Move to keep them pure of UI modeling.  Yeah, adornments have
    ### display cookies in them, oh well.
    ###
    _tree_moves = {}
    _tree_sub_root = None
    
    def _update_tree_view (self, move):
        if move is None:
            item = self.gameTreeView.Items[0]
        elif move in self._tree_moves:
            item = self._tree_moves[move]
        else:
            item = self._new_tree_view_item(move)
        ## Update current move shading and bring into view.
        sitem = self.gameTreeView.SelectedItem
        sitem.Header.Background = SolidColorBrush(Colors.Transparent)
        item.IsSelected = True
        item.Header.Background = SolidColorBrush(Colors.LightSkyBlue)
        ## Rect keeps node from being pegged to end of pane.
        item.BringIntoView(Rect(Size(100, 100)))
    
     
    def _new_tree_view_item (self, move):
        parent = None
        if move.previous is not None and move.previous.branches is not None:
            #if len(move.previous.branches) == 2:
            #    parent = self._tree_moves[move.previous]
            #elif len(move.previous.branches) > 2:
            parent = self._tree_moves[move.previous]
            while len(parent.Items) != 0:
                parent = parent.Items[0]
                
        else:
            parent = self.gameTreeView.SelectedItem.Parent
        item = TreeViewItem()
        item.Header = _new_tree_view_item_grid(move)
        parent.Items.Add(item)
        self._tree_moves[move] = item
        return item



    ### add_next_stone_no_current adds a stone to the stones grid for move,
    ### removes previous moves current move marker, and removes previous moves
    ### adornments.  This is used for advancing to a stone for replay move or
    ### click to place stone (which sometimes replays a move and needs to place
    ### adornments for pre-existing moves).  The Game class also uses this.
    ###
    def add_next_stone_no_current (self, move):
        if not move.is_pass:
            #add_stone(main_win.FindName("stonesGrid"), m.row, m.column, m.color)
            add_stone(self.stonesGrid, move.row, move.column, move.color)
        ## Must remove current adornment before adding it elsewhere.
        if move.previous is not None:
            if Adornments.current_move_adornment in move.previous.adornments:
                remove_current_stone_adornment(self.stonesGrid, move.previous)
            remove_adornments(self.stonesGrid, move.previous.adornments)
        else:
            remove_adornments(self.stonesGrid, self.game.setup_adornments)

    ### add_current_adornments adds to the stones grid a current move marker
    ### for move as well as move's adornments.  This is used for replay move UI
    ### in this module as well as by code in the Game class.
    ###
    def add_current_adornments (self, move):
        ## Must restore adornemnts before adding current, else error adding
        ## current twice.
        restore_adornments(self.stonesGrid, move.adornments)
        if not move.is_pass:
            add_current_stone_adornment(self.stonesGrid, move)
    
    ### endButton_left_down replays all moves to the game end, using currently
    ### selected branches in each move.  This function signals an error if the
    ### game has not started, or no move has been played.
    ###
    def endButton_left_down (self, end_button, e):
        self.game.goto_last_move()
        self._focus_on_stones()
    
    ### brachCombo_SelectionChanged changes the active branch for the next move
    ### of the current move.  Updating_branch_combo is set in update_branch_combo
    ### so that we only update when the user has taken an action as opposed to
    ### programmatically changing the selected item due to arrow keys, deleting moves, etc.
    ###
    def branchCombo_SelectionChanged(self, branch_dropdown, e):
        if updating_branch_combo != True:
            self.game.set_current_branch(branch_dropdown.SelectedIndex)
            self._focus_on_stones()

    
    ### update_title sets the main window title to display the open
    ### file and current move number.  "Move " is always in the title, set
    ### there by default originally.
    ###
    def update_title (self, num, is_pass = False, filebase = None):
        title = self.Title
        pass_str = (is_pass and " Pass") or ""
        if filebase is not None:
            self.Title = ("SGFEd -- " + filebase + ";  Move " + 
                          str(num) + pass_str)
        else:
            tail = title.find("Move ")
            if tail != -1:
                self.Title = (title[:tail + 5] + str(num) + pass_str)
            else:
                raise Exception("Title doesn't have move in it?!")
    

    ### newButton_left_down starts a new game after checking to save the
    ### current game if it is dirty.
    ###
    def newButton_left_down (self, new_button,  e):
        self._check_dirty_save()
        ##
        dlg = newdialog.NewGameDialog()
        dlg.Owner = self
        dlg.ShowDialog()
        if dlg.DialogResult:
            g = game.Game(self, int(dlg.sizeText.Text), int(dlg.handicapText.Text), dlg.komiText.Text)
            if dlg.blackText.Text != "":
                g.player_black = dlg.blackText.Text
            ## Just to make sure we drop all refs ...
            self.game.board.goto_start()
            if dlg.whiteText.Text != "":
                g.player_white = dlg.whiteText.Text
            self.game = g
            self.update_title(0, False, "unsaved")
            

    ### openButton_left_down prompts to save current game if dirty and then
    ### prompts for a .sgf file to open.
    ###
    def openButton_left_down (self, open_button, e):
        self._check_dirty_save()
        dlg = OpenFileDialog();
        dlg.FileName = "game01" # Default file name
        dlg.DefaultExt = ".sgf" # Default file extension
        dlg.Filter = "SGF files (.sgf)|*.sgf" # Filter files by extension
        result = dlg.ShowDialog(); # None, True, or False
        if result:
            try:
                self.game = game.create_parsed_game(sgfparser.parse_file(dlg.FileName), self)
                self.game.filename = dlg.FileName
                self.game.filebase = dlg.FileName[dlg.FileName.rfind("\\") + 1:]
                self.update_title(0, False, self.game.filebase)
            except FileFormatException, err: 
                ## Essentially handles unexpected EOF or malformed property values.
                MessageBox.Show(err.Message + err.StackTrace);
        self._focus_on_stones()

    ### _check_dirty_save prompts whether to save the game if it is dirty.  If
    ### saving, then it uses the game filename, or prompts for one if it is None.
    ###
    def _check_dirty_save (self):
        self.game.save_current_comment()
        if (self.game.dirty and
             MessageBox.Show("Game is unsaved, save it?",
                             "Confirm saving file", MessageBoxButton.YesNo) == 
                MessageBoxResult.Yes):
            if self.game.filename is not None:
                f = self.game.filename
            else:
                f = _get_save_filename()
            if f is not None:
                self.game.write_game(f)

    ### saveButton_left_down saves if game has a file name and is dirty.  If
    ### there's a filename, but the file is up to date, then ask to save-as to
    ### a new name.  Kind of lame to not have explicit save-as button, but work
    ### for now.
    ###
    def saveButton_left_down (self, save_button, e):
        if self.game.filename is not None:
            ## See if UI has comment edits and persist to model.
            self.game.save_current_comment()
            if self.game.dirty:
                self.game.write_game()
            elif (MessageBox.Show("Game is already saved.  " +
                                  "Do you want to save it to a new name?",
                                  "Confirm save-as", MessageBoxButton.YesNo) == 
                    MessageBoxResult.Yes):
                self._save_as()
        else:
            self._save_as()

    def _save_as (self):
        f = _get_save_filename()
        if f is not None:
            self.game.save_current_comment() # Persist UI edits to model.
            self.game.write_game(f)
    
    ### mainWin_keydown dispatches arrow keys for rewinding, replaying, and
    ### choosing branches.  These events always come, catching arrows,
    ### modifiers, etc.  However, when a TextBox has focus, it gets arrow keys
    ### first, so we need to support <escape> to allow user to put focus back
    ### on the stones grid.  We also pick off up and down arrow for branch
    ### selection, working with update_branch_combo to ensure stones grid keeps
    ### focus.
    ###
    def mainWin_keydown (self, win, e):
        if e.Key == Key.Escape:
            win._focus_on_stones()
            e.Handled = True
            return
        ## Previous move
        if (e.Key == Key.Left and
                not self.commentBox.IsKeyboardFocused and
                win.game.can_unwind_move()):
            self.prevButton_left_down(None, None)
            e.Handled = True
        ## Next move
        elif e.Key == Key.Right:
            if self.commentBox.IsKeyboardFocused:
                return
            if win.game.can_replay_move():
                self.nextButton_left_down(None, None)
            e.Handled = True
        ## Initial board state
        elif (e.Key == Key.Home and
                  not self.commentBox.IsKeyboardFocused and
                  win.game.can_unwind_move()):
            self.homeButton_left_down(None, None)
            e.Handled = True
        ## Last move
        elif (e.Key == Key.End and
                  not self.commentBox.IsKeyboardFocused and
                  win.game.can_replay_move()):
            self.game.goto_last_move()
            e.Handled = True
        ## Move branch down
        elif (e.Key == Key.Down and
                  Keyboard.Modifiers == ModifierKeys.Control and
                  not self.commentBox.IsKeyboardFocused):
            self.game.move_branch_down()
            e.Handled = True
        ## Move branch up
        elif (e.Key == Key.Up and
                  Keyboard.Modifiers == ModifierKeys.Control and
                  not self.commentBox.IsKeyboardFocused):
            self.game.move_branch_up()
            e.Handled = True
        ## Display next branch
        elif (e.Key == Key.Down and
                  self.branchCombo.Items.Count > 0 and
                  not self.commentBox.IsKeyboardFocused):
            set_current_branch_down(self.branchCombo, self.game)
            e.Handled = True
        ## Display previous branch
        elif (e.Key == Key.Up and
                  self.branchCombo.Items.Count > 0 and
                  not self.commentBox.IsKeyboardFocused):
            set_current_branch_up(self.branchCombo, self.game)
            e.Handled = True
        ## Opening a file
        elif e.Key == Key.O and Keyboard.Modifiers == ModifierKeys.Control:
            self.openButton_left_down(self.openButton, None)
            e.Handled = True
        ## Testing Game Tree Layout
        elif e.Key == Key.T and Keyboard.Modifiers == ModifierKeys.Control:
            game.show_tree(self.game)
            e.Handled = True
        ## Explicit Save As
        elif (e.Key == Key.S and
              Keyboard.Modifiers == ModifierKeys.Control | ModifierKeys.Alt):
            self._save_as()
            self._focus_on_stones()            
            e.Handled = True
        ## Saving
        elif e.Key == Key.S and Keyboard.Modifiers == ModifierKeys.Control:
            self.saveButton_left_down(self.saveButton, None)
            self._focus_on_stones()            
            e.Handled = True
        ## Save flipped game for opponent's view
        elif (e.Key == Key.F and
               Keyboard.Modifiers == ModifierKeys.Control | ModifierKeys.Alt and
               not self.commentBox.IsKeyboardFocused):
            f = _get_save_filename("Save Flipped File")
            if f is not None:
                self.game.write_flipped_game(f)
            self._focus_on_stones()            
            e.Handled = True
        ## New file
        elif e.Key == Key.N and Keyboard.Modifiers == ModifierKeys.Control:
            self.newButton_left_down(self.newButton, None)
            e.Handled = True
        ## Cutting a sub tree
        elif ((e.Key == Key.Delete or
                (e.Key == Key.X and Keyboard.Modifiers == ModifierKeys.Control)) and
                not self.commentBox.IsKeyboardFocused and
                win.game.can_unwind_move() and
                MessageBox.Show("Cut current move from game tree?",
                                "Confirm cutting move", MessageBoxButton.YesNo) == 
                MessageBoxResult.Yes):
            win.game.cut_move()
            e.Handled = True
        ## Pasting a sub tree
        elif (e.Key == Key.V and Keyboard.Modifiers == ModifierKeys.Control and
                not self.commentBox.IsKeyboardFocused):
            if win.game.can_paste():
                win.game.paste_move()
            else:
                MessageBox.Show("No cut move to paste at this time.")
            e.Handled = True
        ## Help
        elif e.Key == Key.F1:
            MessageBox.Show(help_string, "SGFEd Help")
            e.Handled = True
        elif e.Key == Key.X and Keyboard.Modifiers == ModifierKeys.Control:
            if self.commentBox.IsKeyboardFocused:
                return
            if self.game.current_move is None:
                MessageBox.Show("Must cut sub tree when there is a current move displayed.")
            elif (MessageBox.Show("Do you want to cut the current move?",
                                 "Confirm cut operation", MessageBoxButton.YesNo) == 
                     MessageBoxResult.Yes):
                self.game.cut_move()
            e.Handled = True

    ###
    ### Utilities
    ###

    ### add_stones adds Ellispses to the stones grid for each move.  Moves must
    ### be non-null.  Game uses this for replacing captures.
    ###
    def add_stones (self, moves):
        #g = self.FindName("stonesGrid")
        g = self.stonesGrid
        for m in moves:
            add_stone(g, m.row, m.column, m.color)

    ### remove_stones removes the Ellipses from the stones grid.  Moves must be
    ### non-null.  Game uses this to remove captures.
    ###
    def remove_stones (self, moves):
        #g = self.FindName("stonesGrid") #not needed with wpf.LoadComponent
        g = self.stonesGrid
        for m in moves:
            remove_stone(g, m)

    ### reset_to_start resets the board UI back to the start of the game before
    ### any moves have been played.  Handicap stones will be displayed.  Game
    ### uses this after resetting the model.
    ###
    def reset_to_start (self, cur_move):
        g = self.stonesGrid
        ## Must remove current adornment before other adornments.
        if not cur_move.is_pass:
            remove_current_stone_adornment(g, cur_move)
        remove_adornments(g, cur_move.adornments)
        size = self.game.board.size
        for row in xrange(size):
            for col in xrange(size):
                stone = stones[row][col]
                if stone is not None:
                    g.Children.Remove(stone)
                stones[row][col] = None
        self.add_handicap_stones(self.game)
        self.commentBox.Text = self.game.comments

    ### add_handicap_stones takes a game and adds its handicap moves to the
    ### display.  This takes a game because it is used on new games when
    ### setting up an initial display and when resetting to the start of self.game.
    ###
    def add_handicap_stones (self, game):
        if game.handicap_moves is not None:
            for elt in game.handicap_moves:
                add_stone(self.stonesGrid, elt.row, elt.column, elt.color)


    ### update_branch_combo takes the current branches and the next move, then
    ### sets the branches combo to the right state with the branch IDs and
    ### selecting the one for next_move.  This does not take the move because
    ### the inital board state is not represented by a bogus move object.  Game
    ### uses this from several tree manipulation functions.
    ###
    branch_combo_background = None
    updating_branch_combo = False
    def update_branch_combo (self, branches, next_move):
        combo = self.branchCombo
        if self.branch_combo_background is None:
            self.branch_combo_background = combo.Background
        global updating_branch_combo
        try:
            updating_branch_combo = True
            combo.Items.Clear()
            if branches is not None:
                self.branchLabel.Content = str(len(branches)) + " branches:"
                combo.IsEnabled = True
                combo.Background = SolidColorBrush(Colors.Yellow)
                combo.Items.Add("main")
                for i in xrange(2, len(branches) + 1):
                    combo.Items.Add(str(i))
                combo.SelectedIndex = branches.index(next_move)
            else:
                self.branchLabel.Content = "No branches:"
                combo.IsEnabled = False
                combo.Background = self.branch_combo_background
            self._focus_on_stones()
        finally:
            updating_branch_combo = False

    ### _focus_on_stones ensures the stones grid has focus so that
    ### mainwin_keydown works as expected.  Not sure why event handler is on
    ### the main window, and the main window is focusable, but we have to set
    ### focus to the stones grid to yank it away from the branches combo and
    ### textbox.
    ###
    def _focus_on_stones (self):
        self.stonesGrid.Focusable = True
        Keyboard.Focus(self.stonesGrid)

    ### add_unrendered_adornment setups all the UI objects to render the
    ### adornment a, but it pass False to stop actually putting the adornment
    ### in the stones grid for display.  These need to be added in the right
    ### order, so this just sets up the adornment so that when the move that
    ### triggers it gets displayed, the adornment is ready to replay.
    ###
    def add_unrendered_adornment (self, a):
        add_new_adornment(self.stonesGrid, a, self.game, False)
        
### end SgfEdWindow class


###
### Setup Lines Grid Utilities
###

### _define_lines_columns defines the columns needed to draw lines for the go
### board.  It takes the Grid to modify and the size of the go board.  The
### columns would be all uniform except that the first non-border column must
### be split so that lines can be placed to make them meet cleanly in the
### corners rather than making little spurs outside the board from the lines
### spanning the full width of the first non-border column (if it were uniform width).
###
def _define_lines_columns (g, size):
    g.ColumnDefinitions.Add(_def_col(2)) # border space
    g.ColumnDefinitions.Add(_def_col(1)) # split first row so line ends in middle of cell
    g.ColumnDefinitions.Add(_def_col(1)) # split first row so line ends in middle of cell
    for i in xrange(size - 2):
        g.ColumnDefinitions.Add(_def_col(2))
    g.ColumnDefinitions.Add(_def_col(1)) # split last row so line ends in middle of cell
    g.ColumnDefinitions.Add(_def_col(1)) # split last row so line ends in middle of cell
    g.ColumnDefinitions.Add(_def_col(2)) # border space

### _def_col defines a proportional column.  It uses the relative
### size/proportion spec passed in.  The lines grid construction and adornments
### grids use this.
###
def _def_col (proportion): 
    col_def = ColumnDefinition()
    col_def.Width = GridLength(proportion, GridUnitType.Star)
    return col_def


### _define_lines_rows defines the rows needed to draw lines for the go board.
### It takes the Grid to modify and the size of the go board.  The rows would
### be all uniform except that the first non-border row must be split so that
### lines can be placed to make them meet cleanly in the corners rather than
### making little spurs outside the board from the lines spanning the full
### height of the first non-border row (if it were uniform height).
###
###
def _define_lines_rows (g, size):
    g.RowDefinitions.Add(_def_row(2)) # border space
    g.RowDefinitions.Add(_def_row(1)) # split first column so line ends in middle of cell
    g.RowDefinitions.Add(_def_row(1)) # split first column so line ends in middle of cell
    for i in xrange(size - 2):
        g.RowDefinitions.Add(_def_row(2))
    g.RowDefinitions.Add(_def_row(1)) # split last column so line ends in middle of cell
    g.RowDefinitions.Add(_def_row(1)) # split last column so line ends in middle of cell
    g.RowDefinitions.Add(_def_row(2)) # border space
    
### _def_row defines a proportional row.  It uses the relative
### size/proportion spec passed in.  The lines grid construction and adornments
### grids use this.
###
def _def_row (proportion):
    row_def = RowDefinition()
    row_def.Height = GridLength(proportion, GridUnitType.Star)
    return row_def

    
### _place_lines adds Line elements to the supplied grid to form the go board.
### Size is the number of lines as well as the number of row/columns to span.
### The outside lines are placed specially in the inner of two rows and columns
### that are half the space of the other rows and columns to get the lines to
### meet cleanly in the corners.  Without the special split rows and columns,
### the lines cross and leave little spurs in the corners of the board.
###
def _place_lines (g, size):
    g.Children.Add(_def_hline(2, size, VerticalAlignment.Top))
    g.Children.Add(_def_vline(2, size, HorizontalAlignment.Left))
    for i in xrange(3, size + 1):
        g.Children.Add(_def_hline(i, size, VerticalAlignment.Center))
        g.Children.Add(_def_vline(i, size, HorizontalAlignment.Center))
    g.Children.Add(_def_hline(size + 1, size, VerticalAlignment.Bottom))
    g.Children.Add(_def_vline(size + 1, size, HorizontalAlignment.Right))


LINE_WIDTH = 2

### _def_hline defines a horizontal line for _place_lines.  Loc is the grid row,
### size the go board size, and alignment pins the line within the grid row.
###
def _def_hline (loc, size, alignment):
    hrect = Rectangle()
    Grid.SetRow(hrect, loc)
    Grid.SetColumn(hrect, 2) ## 0th and 1st cols are border and half of split col.
    Grid.SetColumnSpan(hrect, size)
    hrect.Height = LINE_WIDTH
    hrect.Fill = SolidColorBrush(Colors.Black)
    hrect.VerticalAlignment = alignment
    return hrect

### _def_vline defines a vertical line for _place_lines.  Loc is the grid column,
### size the go board size, and alignment pins the line within the grid column.
###
def _def_vline (loc, size, alignment):
    vrect = Rectangle()
    Grid.SetRow(vrect, 2) ## 0th and 1st rows are border and half of split row.
    Grid.SetColumn(vrect, loc)
    Grid.SetRowSpan(vrect, size)
    vrect.Width = LINE_WIDTH
    vrect.Fill = SolidColorBrush(Colors.Black)
    vrect.HorizontalAlignment = alignment
    return vrect


### add_handicap_point takes a Grid and location in terms of go board indexing
### (one based).  It adds a handicap location dot to the board.  We do not have
### to subtract one from x or y since there is a border row and column that is
### at the zero location.
###
def add_handicap_point (g, x, y):
    ## Account for border rows/cols.
    x += 1
    y += 1
    dot = Ellipse()
    dot.Width = 8
    dot.Height = 8
    Grid.SetRow(dot, y)
    Grid.SetColumn(dot, x)
    dot.Fill = SolidColorBrush(Colors.Black)
    g.Children.Add(dot)


###
### Setup Stones Grid Utilities
###

### _setup_index_labels takes a grid and go board size, then emits Label
### objects to create alphanumeric labels.  The labels index the board with
### letters for the columns, starting at the left, and numerals for the rows,
### starting at the bottom.  The letters skip "i" to avoid fontface confusion
### with the numeral one.  This was chosen to match KGS and many standard
### indexing schemes commonly found in pro commentaries.
###
def _setup_index_labels (g, size):
    for i in xrange(1, size + 1):
        ## chr_offset skips the letter I to avoid looking like a one
        chr_offset = (i < 9 and i) or i + 1
        chr_txt = chr(chr_offset + ord('A') - 1)
        num_label_y = 19 - (i - 1)
        # Place labels
        _setup_index_label(g, str(i), 0, num_label_y,
                           HorizontalAlignment.Left, VerticalAlignment.Center)
        _setup_index_label(g, str(i), 20, num_label_y,
                           HorizontalAlignment.Right, VerticalAlignment.Center)
        _setup_index_label(g, chr_txt, i, 0,
                           HorizontalAlignment.Center, VerticalAlignment.Top)
        _setup_index_label(g, chr_txt, i, 20,
                           HorizontalAlignment.Right, VerticalAlignment.Bottom)
    return None

def _setup_index_label (g, content, x, y, h_alignment, v_alignment):
    label = Label()
    label.Content = content
    Grid.SetRow(label, y)
    Grid.SetColumn(label, x)
    label.FontWeight = FontWeights.Bold
    label.HorizontalAlignment = h_alignment
    label.VerticalAlignment = v_alignment
    g.Children.Add(label)
    

###
### Adding and Remvoing Stones
###

### stones holds Ellipse objects to avoid linear searching when removing
### stones.  We could linear search for the Ellipse and store in a move.cookie,
### but we'd have to search since grids don't have any operations to remove
### children by identifying the cell that holds them.  Grids don't do much to
### support cell-based operations, like mapping input event pixel indexes to
### cells.
###
stones = [[None for col in xrange(game.MAX_BOARD_SIZE)] for
           row in xrange(game.MAX_BOARD_SIZE)]

### add_stone takes a Grid and row, column that index the go board one based
### from the top left corner.  It adds a WPF Ellipse object to the stones grid.
###
def add_stone (g, row, col, color):
    stone = Ellipse()
    Grid.SetRow(stone, row)
    Grid.SetColumn(stone, col)
    stone.StrokeThickness = 1
    stone.Stroke = SolidColorBrush(Colors.Black)
    stone.Fill = SolidColorBrush(color)
    stone.HorizontalAlignment = HorizontalAlignment.Stretch
    stone.VerticalAlignment = VerticalAlignment.Stretch
    b = Binding("ActualHeight")
    b.RelativeSource = RelativeSource.Self
    stone.SetBinding(WidthProperty, b)
    g.Children.Add(stone)
    stones[int(row) - 1][int(col) - 1] = stone

_current_stone_adornment_grid = None
_current_stone_adornment_ellipse = None


### remove_stone takes a stones grid and a move.  It removes the Ellipse for
### the move and notes in stones global that there's no stone there in the
### display.  This function also handles the current move adornment and other
### adornments since this is used to rewind the current move.
###
def remove_stone (g, move):
    stone = stones[move.row - 1][move.column - 1]
    if stone is None:
        raise Exception("Shouldn't be removing stone if there isn't one.")
    g.Children.Remove(stone)
    stones[move.row - 1][move.column - 1] = None
    ## Must remove current adornment before other adornments (or just call
    ## Adornments.release_current_move after loop).
    if Adornments.current_move_adornment in move.adornments:
        remove_current_stone_adornment(g, move)
    remove_adornments(g, move.adornments)


### grid_pixels_to_cell returns the go board indexes (one based), as a tuple,
### on which intersection the click occurred (or nearest intersection).
###
def grid_pixels_to_cell (g, x, y):
    col_defs = g.ColumnDefinitions
    row_defs = g.RowDefinitions
    cell_x = int(x / col_defs[0].ActualWidth)
    cell_y = int(y / row_defs[0].ActualHeight)
    ## Cell index must be 1..<board_size>, and there's two border rows/cols.
    return (max(min(cell_x, col_defs.Count - 2), 1),
            max(min(cell_y, col_defs.Count - 2), 1))


###
### Adding and Remving Adornments
###

### add_current_stone_adornment takes the stones grid and a move, then adds the
### concentric circle to mark the last move on the board.  This uses two
### globals to cache an inner grid (placed in one of the cells of the stones
### grid) and the cirlce since they are re-used over and over throughout move
### placemnent.  The inner grid is used to create a 3x3 so that there is a
### center cell in which to place the current move adornment so that it can
### stretch as the whole UI resizes.
###
def add_current_stone_adornment (stones_grid, move):
    global _current_stone_adornment_grid
    global _current_stone_adornment_ellipse
    if _current_stone_adornment_grid is not None:
        _current_stone_adornment_ellipse.Stroke = \
           SolidColorBrush(game.opposite_move_color(move.color))        
        Grid.SetRow(_current_stone_adornment_grid, move.row)
        Grid.SetColumn(_current_stone_adornment_grid, move.column)
        Adornments.get_current_move(move, _current_stone_adornment_grid)
        stones_grid.Children.Add(_current_stone_adornment_grid)        
    else:
        inner_grid = add_adornment_grid(stones_grid, move.row, move.column)
        _current_stone_adornment_grid = inner_grid
        ##
        ## Create mark
        mark = Ellipse()
        _current_stone_adornment_ellipse = mark
        Grid.SetRow(mark, 1)
        Grid.SetColumn(mark, 1)
        mark.StrokeThickness = 2
        mark.Stroke = SolidColorBrush(game.opposite_move_color(move.color))
        mark.Fill = SolidColorBrush(Colors.Transparent)
        mark.HorizontalAlignment = HorizontalAlignment.Stretch
        mark.VerticalAlignment = VerticalAlignment.Stretch
        inner_grid.Children.Add(mark)
        ##
        ## Update model.
        Adornments.get_current_move(move, inner_grid)

def remove_current_stone_adornment (stones_grid, move):
    if move is not None and not move.is_pass:
        stones_grid.Children.Remove(Adornments.current_move_adornment.cookie)
        Adornments.release_current_move()


### add_or_remove_adornment adds or removes an adornment to the current board
### state.  It removes an adornment of kind kind at row,col if one already
### exisits there.  Otherwise, it adds the new adornment.  If the kind is
### letter, and all letters A..Z have been used, then this informs the user.
###
def add_or_remove_adornment (stones_grid, row, col, kind, game):
    a = game.get_adornment(row, col, kind)
    if a is not None:
        game.remove_adornment(a)
        stones_grid.Children.Remove(a.cookie)
    else:
        a = game.add_adornment(game.current_move, row, col, kind)
        if a is None and kind is Adornments.letter:
            MessageBox.Show("Cannot add another letter adornment.  " +
                            "You have used A through Z already.")
            return
        add_new_adornment(stones_grid, a, game)
    game.dirty = True


### add_new_adornment takes the stones grid, an adornment, and the game
### instance to update the UI with the specified adornment (square, triangle,
### letter).  The last parameter actually controls whether the adornment is
### added to the stones grid for rendering or simply prepared for rendering.
### The adornment is a unique instance and holds in its cookie the grid holding
### the WPF markup object.  This grid is a 3x3 grid that sits in a single cell
### of the stones grid.  We do not re-uses these grids for multiple adornments
### or free list them at this time.
###
def add_new_adornment (stones_grid, adornment, game_inst, render=True):
    if adornment.kind is Adornments.square:
        grid = make_square_adornment(stones_grid, adornment.row, adornment.column,
                                     game_inst, render)
    elif adornment.kind is Adornments.triangle:
        grid = make_triangle_adornment(stones_grid, adornment.row, adornment.column,
                                       game_inst, render)
    elif adornment.kind is Adornments.letter:
        ## grid in this case is really a viewbow.
        grid = make_letter_adornment(stones_grid, adornment.row, adornment.column,
                                     adornment.letter, game_inst, render)
    adornment.cookie = grid

### make_square_adornment returns the inner grid with a Rectangle adornment in
### it.  The inner grid is already placed in the stones grid at the row, col if
### render is true.  game_inst is needed to determine if there is a move at
### this location or an empty board location to set the adornment color.
###
def make_square_adornment (stones_grid, row, col, game_inst, render):
    grid = add_adornment_grid(stones_grid, row, col, render)
    sq = Rectangle()
    Grid.SetRow(sq, 1)
    Grid.SetColumn(sq, 1)
    sq.StrokeThickness = 2
    move = game_inst.board.move_at(row, col)
    if move is not None:
        color = game.opposite_move_color(move.color)
    else:
        color = Colors.Black
    sq.Stroke = SolidColorBrush(color)
    sq.Fill = SolidColorBrush(Colors.Transparent)
    sq.HorizontalAlignment = HorizontalAlignment.Stretch
    sq.VerticalAlignment = VerticalAlignment.Stretch
    grid.Children.Add(sq)
    return grid

### make_triangle_adornment returns the inner grid with a ViewBox in the center
### cell that holds the adornment in it.  Since Polygons don't stretch
### automatically, like Rectangles, the ViewBox provides the stretching.  The
### inner grid is already placed in the stones grid at the row, col if render
### is true.  game_inst is needed to determine if there is a move at this
### location or an empty board location to set the adornment color.
###
def make_triangle_adornment (stones_grid, row, col, game_inst, render):
    grid = add_adornment_grid(stones_grid, row, col, render)
    #grid.ShowGridLines = True
    vwbox = Viewbox()
    Grid.SetRow(vwbox, 1)
    Grid.SetColumn(vwbox, 1)
    vwbox.HorizontalAlignment = HorizontalAlignment.Stretch
    vwbox.VerticalAlignment = VerticalAlignment.Stretch
    grid.Children.Add(vwbox)
    tri = Polygon()
    tri.Points.Add(Point(3, 0))
    tri.Points.Add(Point(0, 6))
    tri.Points.Add(Point(6, 6))
    tri.StrokeThickness = 1
    move = game_inst.board.move_at(row, col)
    if move is not None:
        color = game.opposite_move_color(move.color)
    else:
        color = Colors.Black
    tri.Stroke = SolidColorBrush(color)
    tri.Fill = SolidColorBrush(Colors.Transparent)
    #tri.HorizontalAlignment = HorizontalAlignment.Stretch
    #tri.VerticalAlignment = VerticalAlignment.Stretch
    vwbox.Child = tri
    return grid

### make_letter_adornment returns a ViewBox in the stones_grid cell.  This does
### not do the inner grid like make_square_adornment and make_triagle_adornment
### since for some reason, that makes the letter show up very very small.  The
### ViewBox provides the stretching for the Label object.  If render is false,
### we do not put the viewbox into the grid.  game_inst is needed to determine
### if there is a move at this location or an empty board location to set the
### adornment color.
###
def make_letter_adornment (stones_grid, row, col, letter, game_inst, render):
    vwbox = Viewbox()
    Grid.SetRow(vwbox, row)
    Grid.SetColumn(vwbox, col)
    vwbox.HorizontalAlignment = HorizontalAlignment.Stretch
    vwbox.VerticalAlignment = VerticalAlignment.Stretch
    label = Label()
    label.Content = letter
    label.FontSize = 50.0
    Grid.SetRow(label, 1)
    Grid.SetColumn(label, 1)
    #label.FontWeight = FontWeights.Bold
    label.HorizontalAlignment = HorizontalAlignment.Stretch
    label.VerticalAlignment = VerticalAlignment.Stretch
    move = game_inst.board.move_at(row, col)
    if move is not None:
        color = game.opposite_move_color(move.color)
        label.Background = SolidColorBrush(Colors.Transparent)
    else:
        color = Colors.Black
        ## See sgfpy.xaml for lines grid (board) tan background.
        label.Background = SolidColorBrush(Color.FromArgb(0xff, 0xd7, 0xb2, 0x64))
    label.Foreground = SolidColorBrush(color)
    vwbox.Child = label
    if render:
        stones_grid.Children.Add(vwbox)
    return vwbox

### add_adornment_grid sets up 3x3 grid in current stone's grid cell to hold
### adornment.  It returns the inner grid.  This adds the inner grid to the
### stones grid only if render is true.  This inner grid is needed to both
### center the adornment in the stones grid cell and to provide a stretchy
### container that grows with the stones grid cell.
###
def add_adornment_grid (stones_grid, row, col, render=True):
    inner_grid = Grid()
    inner_grid.ShowGridLines = False
    inner_grid.Background = SolidColorBrush(Colors.Transparent)
    Grid.SetRow(inner_grid, row)
    Grid.SetColumn(inner_grid, col)
    inner_grid.HorizontalAlignment = HorizontalAlignment.Stretch
    inner_grid.VerticalAlignment = VerticalAlignment.Stretch
    #inner_grid.Name = "adornmentGrid"
    inner_grid.ColumnDefinitions.Add(_def_col(1))
    inner_grid.ColumnDefinitions.Add(_def_col(2))
    inner_grid.ColumnDefinitions.Add(_def_col(1))
    inner_grid.RowDefinitions.Add(_def_row(1))
    inner_grid.RowDefinitions.Add(_def_row(2))
    inner_grid.RowDefinitions.Add(_def_row(1))
    if render:
        stones_grid.Children.Add(inner_grid)
    return inner_grid

### remove_adornments removes the list of adornments from stones grid.
### Adornments must be non-null.  Also, be careful to call
### remove_current_stone_adornment before calling this so that it can be
### managed correctly.  Note, this does not set the cookie to None so that
### restore_adornments can re-use them.  We don't think there's so much mark up
### that holding onto the cookies would burden most or all serious game review
### files.
###
def remove_adornments (stones_grid, adornments):
    for a in adornments:
        stones_grid.Children.Remove(a.cookie)

def restore_adornments (stones_grid, adornments):
    for a in adornments:
        stones_grid.Children.Add(a.cookie)


###
### Misc Utils
###

### set_current_branch{_up|_down} takes the branch combo to operate on and the
### game instance.  It updates the combo box and then updates the model by
### calling on the game object.
###
def set_current_branch_up (combo, game):
    cur = combo.SelectedIndex
    if cur > 0:
        combo.SelectedIndex = cur - 1
    game.set_current_branch(combo.SelectedIndex)

def set_current_branch_down (combo, game):
    cur = combo.SelectedIndex
    if cur < combo.Items.Count:
        combo.SelectedIndex = cur + 1
    game.set_current_branch(combo.SelectedIndex)


def _get_save_filename (title = None):
    dlg = SaveFileDialog()
    dlg.FileName = "game01" # Default file name
    dlg.Title = (title is not None and title or dlg.Title)
    dlg.DefaultExt = ".sgf" # Default file extension
    dlg.Filter = "Text documents (.sgf)|*.sgf" # Filter files by extension
    result = dlg.ShowDialog() # None, True, or False
    if result:
        return dlg.FileName
    else:
        return None

def _new_tree_view_item_grid (move):
    ## Get Grid to hold stone image and move number label
    g = Grid()
    g.ShowGridLines = False
    g.Background = SolidColorBrush(Colors.Transparent)
    g.Height = 25
    g.Width = 25
    g.Margin = Thickness(0,2,0,2)
    ## Get stone image
    stone = Ellipse()
    stone.StrokeThickness = 1
    stone.Stroke = SolidColorBrush(Colors.Black)
    stone.Fill = SolidColorBrush(move.color)
    stone.HorizontalAlignment = HorizontalAlignment.Stretch
    stone.VerticalAlignment = VerticalAlignment.Stretch
    g.Children.Add(stone)
    ## Get move number label
    label = Label()
    label.Content = str(move.number)
    label.FontWeight = FontWeights.Bold
    label.FontSize = 12
    label.Foreground = SolidColorBrush(game.opposite_move_color(move.color))
    label.HorizontalAlignment = HorizontalAlignment.Center
    label.VerticalAlignment = VerticalAlignment.Center
    g.Children.Add(label)
    return g


###
### __main__ kicker
###

if __name__ == '__main__':
	#Application().Run(SgfEdWindow())
    app = Application()
    clr.SetCommandDispatcher(lambda x: app.Dispatcher.Invoke(x))
    app.Run(SgfEdWindow())



