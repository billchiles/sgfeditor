### game.py is sort of the controller of the IronPython SGF Editor.  The main
### class is Game, which provides calls for GUI event handling and makes calls
### to update the board and moves model.

import wpf

### Don't need this now due to new wpf module, but left as documentation of usage.
###
### Needed for System.Windows.Media ...
#clr.AddReference("PresentationCore")
## Needed for System.Windows
#clr.AddReference('PresentationFramework')

from System.Windows.Media import Colors
from System.Windows import MessageBox

import goboard
import sgfparser

__all__ = ["Game", "create_default_game", "MAX_BOARD_SIZE", "opposite_move_color",
           "create_parsed_game"]


MAX_BOARD_SIZE = 19
MIN_BOARD_SIZE = 9
DEFAULT_KOMI = "6.5"

### The Game class is a controller of sorts for the app.  It provides helper functions
### for the UI that event handlers can call.  These functions call on the model,
### GoBoard, and they call on the UI APIs to update, such as enabling/disabling buttons.
###
class Game (object):

    def __init__ (self, main_win, size, handicap, komi, handicap_stones = None):
        ## _main_win is the WPF application object.
        self._main_win = main_win
        ## board holds the GoBoard model.
        self.board = goboard.GoBoard(size)
        self._init_handicap_next_color(handicap, handicap_stones)
        ## komi is either 0.5, 6.5, or <int>.5
        self.komi = komi
        ## _state helps with enabling and disabling buttons.
        self._state = GameState.NOT_STARTED
        ## first_move holds the first move which links to subsequent moves.
        ## when displaying the intial board state of a started game, this is
        ## the current move.
        self.first_move = None
        ## Branches holds all the first moves, while first_move points to one
        ## of these.  This is None until there's more than one first move.
        self.branches = None
        ## The following hold any markup for the initial board state
        self.setup_adornments = []
        ## current_move keeps point to the move in the tree we're focused on It
        ## is None when the board is in the initial state, and first_move then
        ## points to the current move.
        self.current_move = None
        ## Comments holds any initial board state comments for the game.  This
        ## is guaranteed to be set when opening a file, which write_game
        ## depends on.
        self.comments = ""
        ## Move count is just a counter in case we add a feature to number
        ## moves or show the move # in the title bar or something.
        self.move_count = 0
        ## parsed_game is not None when we opened a file to edit.
        self.parsed_game = None
        ## filename holds the full pathname if we read from a file or ever
        ## saved this game.  Filebase is just <name>.<ext>.
        self.filename = None
        self.filebase = None
        ## dirty means we've modified this game in some way
        self.dirty = False
        ## player members hold strings if there are player names in record.
        self.player_black = None
        self.player_white = None
        ## _cut_move holds the head of a sub tree that was last cut.
        ## Note, the public cut_move is a method.
        self._cut_move = None
        main_win.setup_board_display(self)

    ### _init_handicap_next_color sets the next color to play and sets up any
    ### handicap state.  If there is a handicap,the moves may be specified in a
    ### parsed game; otherwise, this fills in traditional locations.  If there
    ### is a handicap and stones are supplied, then their number must agree.
    ###
    def _init_handicap_next_color (self, handicap, handicap_stones):
        self.handicap = handicap
        if handicap == 0 or handicap == "0":
            self.handicap_moves = None
            self.next_color = Colors.Black
        else:
            self.next_color = Colors.White
            self.handicap_moves = handicap_stones
            if handicap_stones is None:
                self.handicap_moves = []
                def make_move (row, col):
                    m = goboard.Move(row, col, Colors.Black)
                    self.handicap_moves.append(m)
                    self.board.add_stone(m)
                if handicap >= 2:
                    make_move(4, 16)
                    make_move(16, 4)
                if handicap >= 3:
                    make_move(16, 16)
                if handicap >= 4:
                    make_move(4, 4)
                if handicap == 5:
                    make_move(10, 10)
                if handicap >= 6:
                    make_move(10, 4)
                    make_move(10, 16)
                if handicap == 7:
                    make_move(10, 10)
                if handicap >= 8:
                    make_move(4, 10)
                    make_move(16, 10)
                if handicap == 9:
                    make_move(10, 10)
            elif len(handicap_stones) != handicap:
                raise Exception("Handicap number is not equal to all " +
                                "black stones in parsed root node.")
            else:
                for m in handicap_stones:
                    self.board.add_stone(m)
        
    #def _message_board (self):    
    #    res = ""
    #    for row in self.board.moves:
    #        col_str = ""
    #        for col in row:
    #            if col is None:
    #                col_str = col_str + ". "
    #            elif col.color == Colors.Black:
    #                col_str = col_str + "X "
    #            elif col.color == Colors.White:
    #                col_str = col_str + "O "
    #            else:
    #                col_str = col_str + "?!"
    #        res = res + col_str + "\n"
    #    MessageBox.Show(res)


    ###
    ### Making Moves while Playing Game
    ###
    
    ### make_move adds a move in sequence to the game and board at row, col.
    ### Row, col index from the top left corner.  Other than marking the
    ### current move with a UI adornments, this handles clicking and adding
    ### moves to a game.  It handles branching if the current move already has
    ### next moves and displays message if the row, col already has a move at
    ### that location.  If this is the first move, this function sets the game
    ### state to started.  It sets next move color and so on.  This returns the
    ### new move (or an existing move if the user clicked on a location where
    ### there is a move on another branch following the current move).
    ###
    def make_move (self, row, col):
        cur_move = self.current_move
        maybe_branching = ((cur_move is not None and cur_move.next is not None) or
                           (cur_move is None and self.first_move is not None))
        if self.board.has_stone(row, col):
            MessageBox.Show("Can't play where there already is a stone.")
            return None
        ## move may be set below to pre-existing move, tossing this new object.
        move = goboard.Move(row, col, self.next_color)
        if self._check_self_capture_no_kill(move):
            MessageBox.Show("You cannot make a move that removes a group's last liberty")
            return None
        if maybe_branching:
            tmp = self._make_branching_move(cur_move, move)
            if tmp is move:
                ## Just because we're branching, doesn't mean the game is dirty.
                ## If added new move, mark dirty since user could have saved game.
                self.dirty = True
            else:
                ## Found existing move at location in branches, just reply it for
                ## capture effects, etc.  Don't need to check ReplayMove for conflicting
                ## board move since user clicked and space is empty.
                return self.replay_move()
        else:
            if self._state is GameState.NOT_STARTED:
                self.first_move = move
                self._state = GameState.STARTED
            else:
                cur_move.next = move
                move.previous = cur_move
            self.dirty = True
        self._save_and_update_comments(cur_move, move)
        self.board.add_stone(move)
        self.current_move = move
        move.number = self.move_count + 1
        self.move_count += 1
        self.next_color = opposite_move_color(self.next_color)
        self._main_win.prevButton.IsEnabled = True
        self._main_win.homeButton.IsEnabled = True
        if move.next is None:
            ## Made a move or branch that is at end of line of play.
            self._main_win.nextButton.IsEnabled = False
            self._main_win.endButton.IsEnabled = False
        else:
            ## Made a move that is already the next move in some branch,
            ## and it has a next move.
            self._main_win.nextButton.IsEnabled = True
            self._main_win.endButton.IsEnabled = True
        if len(move.dead_stones) != 0:
            self.remove_stones(move.dead_stones)
        return move

    ### CheckSelfCaptureNoKill returns true if move removes the last liberty of
    ### its group without killing an opponent group.  It needs to temporarily add
    ### the move to the board, then remove it.  Try catch may be over kill here, but ....
    ###
    def _check_self_capture_no_kill (self, move):
        try:
            self.board.add_stone(move)
            noKill = len(self.check_for_kill(move)) == 0
            return (not self.find_liberty(move.row, move.column, move.color)) and noKill
        finally:
            self.board.remove_stone(move)

    ### _make_branching_move sets up cur_move to have more than one next move,
    ### that is, branches.  If the new move, move, is at the same location as
    ### a next move of cur_move, then this function loses move in lieu of the
    ### existing next move.  This also sets up any next and prev pointers as
    ### appropriate and updates the branches combo.
    ###
    def _make_branching_move (self, cur_move, move):
        if cur_move is None:
            move = self._make_branching_move_branches(self, self.first_move, move)
            self.first_move = move
        else:
            move = self._make_branching_move_branches(cur_move, cur_move.next, move)
            cur_move.next = move
            move.previous = cur_move
        ## move may be pre-existing move with branches, or may need to clear combo ...
        self._main_win.update_branch_combo(move.branches, move.next)
        return move

    ### _make_branching_move_branches takes a game or move object (the current
    ### move), the current next move, and a move representing where the user
    ### clicked.  If there are no branches yet, then see if new_move is at the
    ### same location as next and toss new_move in this case, which also means
    ### there are still no branches yet.
    ###
    def _make_branching_move_branches (self, game_or_move, next, new_move):
        if game_or_move.branches is None:
            game_or_move.branches = [next] # Must pass non-None branches.
            move = self._maybe_update_branches(game_or_move, new_move)
            if move is next:
                ## new_move and next are same, keep next and clean up branches.
                game_or_move.branches = None
                return next
            ## Keep branches and return move, which may be new or pre-existing.
            return move
        else:
            return self._maybe_update_branches(game_or_move, new_move)
        
    ### _maybe_update_branches takes a game or move object (has a .branches member)
    ### and a next move.  Branches must not be None.  It returns a pre-existing
    ### move if the second argument represents a move at a location for which there
    ### already is a move; otherwise, it returns the second argument as a new next
    ### move.  If this is a new next move, we add it to .branches.
    ###
    def _maybe_update_branches (self, game_or_move, move):
        already_move = _list_find(move, game_or_move.branches,
                                    lambda x,y: x.row == y.row and x.column == y.column)
        if already_move != -1:
            m = game_or_move.branches[already_move]
            if not m.rendered:
                self._ready_for_rendering(m)
            return m
        else:
            game_or_move.branches.append(move)
            return move

    ### check_for_kill determines if move kills any stones on the board and
    ### returns a list of move objects that were killed after storing them in
    ### the Move object.  We use find_liberty and collect_stones rather than
    ### try to build list as we go to simplify code.  Worse case we recurse all
    ### the stones twice, but it doesn't impact observed performance.
    ###
    ### We do not need to clear visited between each outer 'if' check.  If we
    ### start descending on a different stone in a different outer 'if' and
    ### encounter a stone, S, that we've visited before, then searching that
    ### stone previously must have returned false.  That is, searching all the
    ### stones connected to S found no liberties before, and the current outer
    ### 'if' must be searching the same group.  The new stone is going to
    ### return false again, so no reason to clear visited.
    ###
    def check_for_kill (self, move):
        row = move.row
        col = move.column
        ## Consider later if this is too much consing per move.
        visited = [[False for j in xrange(self.board.size)] for i in xrange(self.board.size)]
        opp_color = opposite_move_color(move.color)
        dead_stones = []
        if (self.board.has_stone_color_left(row, col, opp_color) and 
            not self.find_liberty(row, col - 1, opp_color, visited)):
            self.collect_stones(row, col - 1, opp_color, dead_stones)
        if (self.board.has_stone_color_up(row, col, opp_color) and 
            not self.find_liberty(row - 1, col, opp_color, visited)):
            self.collect_stones(row - 1, col, opp_color, dead_stones)
        if (self.board.has_stone_color_right(row, col, opp_color) and 
            not self.find_liberty(row, col + 1, opp_color, visited)):
            self.collect_stones(row, col + 1, opp_color, dead_stones)
        if (self.board.has_stone_color_down(row, col, opp_color) and 
            not self.find_liberty(row + 1, col, opp_color, visited)):
            self.collect_stones(row + 1, col, opp_color, dead_stones)
        move.dead_stones = dead_stones
        return dead_stones

    ### find_Liberty starts at row, col traversing all stones with the supplied
    ### color to see if any stone has a liberty.  It returns true if it finds a
    ### liberty.  If we've already been here, then its search is still pending
    ### (and other stones it connects with should be searched).  See comment
    ### for check_for_kill.  Visited can be null if you just want to check if a
    ### single stone/group has any liberties, say, to see if a move was a self capture.
    ###
    def find_liberty (self, row, col, color, visited = None):
        if visited is None:
            ## Consider later if this is too much consing per move.
            ## We cons this for self kill check, cons another for CheckForKill of opponent stones.
            visited = [[None for i in xrange(self.board.size)] 
                       for j in xrange(self.board.size)]
        if visited[row - 1][col - 1]:
            return False
        ## Check for immediate liberty (breadth first).
        if col != 1 and not self.board.has_stone_left(row, col):
            return True
        if row != 1 and not self.board.has_stone_up(row, col):
            return True
        if col != self.board.size and not self.board.has_stone_right(row, col):
            return True
        if row != self.board.size and not self.board.has_stone_down(row, col):
            return True
        ## No immediate liberties, so keep looking ...
        visited[row - 1][col - 1] = True
        if (self.board.has_stone_color_left(row, col, color) and
            self.find_liberty(row, col - 1, color, visited)):
            return True;
        if (self.board.has_stone_color_up(row, col, color) and
            self.find_liberty(row - 1, col, color, visited)):
            return True;
        if (self.board.has_stone_color_right(row, col, color) and
            self.find_liberty(row, col + 1, color, visited)):
            return True;
        if (self.board.has_stone_color_down(row, col, color) and
            self.find_liberty(row + 1, col, color, visited)):
            return True;
        ## No liberties ...
        return False;
    
    ### CollectStones gathers all the stones at row, col of color color, adding them
    ### to the list dead_stones.  This does not update the board model by removing
    ### the stones.  CheckForKill uses this to collect stones, ReadyForRendering calls
    ### CheckForKill to prepare moves for rendering, but it shouldn't remove stones
    ### from the board.
    ###
    def collect_stones (self, row, col, color, dead_stones, visited = None):
        if visited is None:
            ## Consider later if this is too much consing per move.
            ## We cons this for self kill check, cons another for CheckForKill of opponent stones.
            visited = [[None for i in xrange(self.board.size)] 
                       for j in xrange(self.board.size)]
        dead_stones.append(self.board.move_at(row, col))
        visited[row - 1][col - 1] = True
        ##self.board.remove_stone_at(row, col)
        if self.board.has_stone_color_left(row, col, color) and not visited[row - 1][col - 2]:
            self.collect_stones(row, col - 1, color, dead_stones, visited)
        if self.board.has_stone_color_up(row, col, color) and not visited[row - 2][col - 1]:
            self.collect_stones(row - 1, col, color, dead_stones, visited)
        if self.board.has_stone_color_right(row, col, color) and not visited[row - 1][col]:
            self.collect_stones(row, col + 1, color, dead_stones, visited)
        if self.board.has_stone_color_down(row, col, color) and not visited[row][col - 1]:
            self.collect_stones(row + 1, col, color, dead_stones, visited)

    
    ###
    ### Unwinding Moves and Goign to Start
    ###
    
    ### unwind_move removes the last move made (see make_move).  Other than
    ### marking the previous move as the current move with a UI adornments,
    ### this handles rewinding game moves.  If the game has not started, or
    ### there's no current move, this signals an error.  This returns the move
    ### that was current before rewinding.
    ###
    def unwind_move (self):
        if self._state is GameState.NOT_STARTED:
            raise Exception("Previous button should be disabled if game not started.")
        current = self.current_move
        if current is None:
            raise Exception("Previous button should be disabled if no current move.")
        if not current.is_pass:
            self.board.remove_stone(current)
        self.add_stones(current.dead_stones)
        self.next_color = current.color
        self.move_count -= 1
        previous = current.previous
        self._save_and_update_comments(current, previous)        
        if previous is None:
            self._main_win.prevButton.IsEnabled = False
            self._main_win.homeButton.IsEnabled = False
        #    self._main_ui.MainWindow.FindName("prevButton").IsEnabled = False
        #    self._main_ui.MainWindow.FindName("homeButton").IsEnabled = False
        #self._main_ui.MainWindow.FindName("nextButton").IsEnabled = True
        #self._main_ui.MainWindow.FindName("endButton").IsEnabled = True
        self._main_win.nextButton.IsEnabled = True
        self._main_win.endButton.IsEnabled = True
        if previous is None:
            self._main_win.update_branch_combo(self.branches, current)
        else:
            self._main_win.update_branch_combo(previous.branches, current)
        self.current_move = previous
        return current
    
    def can_unwind_move (self):
        return (not self._state is GameState.NOT_STARTED and
                self.current_move is not None)

    def add_stones (self, stones):
        self._main_win.add_stones(stones)
        for m in stones:
            self.board.add_stone(m)

    ### goto_start resets the model to the initial board state before any moves
    ### have been played, and then resets the UI.  This assumes the game has
    ### started.
    ###
    def goto_start (self):
        if self._state is GameState.NOT_STARTED:
            raise Exception("Home button should be disabled if game not started.")
        current = self.current_move
        if current is None:
            raise Exception("Home button should be disabled if no current move.")
        self._save_and_update_comments(current, None)
        self.board.goto_start()
        self._main_win.reset_to_start(current)
        ## Updating self.current_move, so after here, lexical 'current' is different
        self.next_color = (self.handicap_moves is None and Colors.Black) or Colors.White
        self.current_move = None
        self.move_count = 0
        self._main_win.update_branch_combo(self.branches, self.first_move)
        self._main_win.prevButton.IsEnabled = False
        self._main_win.homeButton.IsEnabled = False
        self._main_win.nextButton.IsEnabled = True
        self._main_win.endButton.IsEnabled = True


    ###
    ### Replaying Moves and Goign to End
    ###
    
    ### replay_move add the next that follows the current move.  move made (see
    ### make_move).  Other than marking the next move as the current move with
    ### a UI adornments, this handles replaying game moves.  The next move is
    ### always move.next which points to the selected branch if there is more
    ### than one next move.  If the game hasn't started, or there's no next
    ### move, this signals an error.  This returns the move that was current
    ### before rewinding.
    ###
    def replay_move (self):
        if self._state is GameState.NOT_STARTED:
            raise Exception("Next button should be disabled if game not started.")
        ## advance self.current_move to the next move.
        fixup_move = self.current_move
        if self.current_move is None:
            self.current_move = self.first_move
        elif self.current_move.next is None:
            raise Exception("Next button should be disabled if no next move.")
        else:
            self.current_move = self.current_move.next
        if self._replay_move_update_model(self.current_move) is None:
            self.current_move = fixup_move
            return None
        self._save_and_update_comments(self.current_move.previous, self.current_move)
        if self.current_move.next is None:
            self._main_win.nextButton.IsEnabled = False
            self._main_win.endButton.IsEnabled = False
        #    self._main_ui.MainWindow.FindName("nextButton").IsEnabled = False
        #    self._main_ui.MainWindow.FindName("endButton").IsEnabled = False
        #self._main_ui.MainWindow.FindName("prevButton").IsEnabled = True
        self._main_win.prevButton.IsEnabled = True
        self._main_win.homeButton.IsEnabled = True
        self._main_win.update_branch_combo(self.current_move.branches, self.current_move.next)
        self._main_win.commentBox.Text = self.current_move.comments
        return self.current_move

    def can_replay_move (self):        
        return (self._state is GameState.STARTED and
                (self.current_move is None or self.current_move.next is not None))


    ### goto_last_move handles jumping to the end of the game record following
    ### all the currently selected branches.  This handles all game/board model
    ### and UI updates, including current move adornments.  If the game hasn't
    ### started, this throws an error.
    ###
    def goto_last_move (self):
        if self._state is GameState.NOT_STARTED:
            raise Exception("End button should be disabled if game not started.")
        current = self.current_move
        save_orig_current = current
        ## Setup for loop ...
        if current is None:
            current = self.first_move
            if self._replay_move_update_model(current) is None:
                ## No partial actions/state to cleanup or revert
                return
            self._main_win.add_next_stone_no_current(current)
            next = current.next
        else:
            next = current.next
        ## Walk to last move
        while next is not None:
            if self._replay_move_update_model(next) is None:
                MessageBox.Show("Next move conincides with a move on the board. " +
                                "You are replying moves from a pasted branch that's inconsistent.")
                break
            self._main_win.add_next_stone_no_current(next)
            current = next
            next = current.next
        ## Update last move UI
        self._save_and_update_comments(save_orig_current, current)
        self._main_win.add_current_adornments(current)
        self.current_move = current
        self.move_count = current.number
        self.next_color = opposite_move_color(current.color)
        self._main_win.prevButton.IsEnabled = True
        self._main_win.homeButton.IsEnabled = True
        self._main_win.nextButton.IsEnabled = next is not None
        self._main_win.endButton.IsEnabled = next is not None
        ## There can't be any branches, but this ensures UI is cleared.
        if next is not None:
            self._main_win.update_branch_combo(current.branches, next)
        else:
            self._main_win.update_branch_combo(None, None)
    
    ### _replay_move_update_model updates the board model, next move color,
    ### etc., when replaying a move in the game record.  This also handles
    ### rendering a move that has only been read from a file and never
    ### displayed in the UI.  Rendering here just means its state will be as if
    ### it had been rendedered before.  We must setup branches to Move objects,
    ### and make sure the next Move object is created and marked unrendered so
    ### that code elsewhere that checks move.next will know there's a next
    ### move.
    ###
    def _replay_move_update_model (self, move):
        if not move.is_pass:
            ## Check if board has stone already since might be replaying branch
            ## that was pasted into tree (and moves could conflict).
            if not self.board.has_stone(move.row, move.column):
                self.board.add_stone(move)
            else:
                return None;
        self.next_color = opposite_move_color(move.color)
        if not move.rendered:
            ## Move points to a ParsedNode and has never been displayed.
            self._ready_for_rendering(move)
        self.move_count += 1
        self.remove_stones(move.dead_stones)
        return move

    def remove_stones (self, stones):
        self._main_win.remove_stones(stones)
        for m in stones:
            self.board.remove_stone(m)


    ### _ready_for_rendering puts move in a state as if it had been displayed
    ### on the screen before.  Moves from parsed nodes need to be created when
    ### their previous move is actually displayed on the board so that there is
    ### a next Move object in the game three for consistency with the rest of
    ### model.  However, until the moves are actually ready to be displayed
    ### they do not have captured lists hanging off them, their next branches
    ### and moves set up, etc.  This function makes the moves completely ready
    ### for display.
    ###
    def _ready_for_rendering (self, move):
        if not move.is_pass:
            self.check_for_kill(move)
        pn = move.parsed_node
        mnext = None
        if pn.branches is not None:
            moves = []
            for n in pn.branches:
                m = _parsed_node_to_move(n)
                m.number = self.move_count + 2
                m.previous = move
                moves.append(m)
            move.branches = moves
            mnext = moves[0]
        elif pn.next is not None:
            mnext = _parsed_node_to_move(pn.next)
            mnext.number = self.move_count + 2
            mnext.previous = move
        move.next = mnext
        self._replay_unrendered_adornments(move)
        move.rendered = True
        return move

    ### _replay_unrendered_adornments is just a helper for
    ### _replay_move_update_model.  This does not need to check add_adornment
    ### for a None result since we're trusting the file was written correctly,
    ### or it doesn't matter if there are dup'ed letters.
    ###
    def _replay_unrendered_adornments (self, move):
        props = move.parsed_node.properties
        if "TR" in props:
            coords = [goboard.parsed_to_model_coordinates(x) for x in props["TR"]]
            adorns = [self.add_adornment(move, x[0], x[1], goboard.Adornments.triangle)
                      for x in coords]
            for x in adorns:
                self._main_win.add_unrendered_adornment(x)
        if "SQ" in props:
            coords = [goboard.parsed_to_model_coordinates(x) for x in props["SQ"]]
            adorns = [self.add_adornment(move, x[0], x[1], goboard.Adornments.square)
                      for x in coords]
            for x in adorns:
                self._main_win.add_unrendered_adornment(x)
        if "LB" in props:
            coords = [goboard.parsed_label_model_coordinates(x) for x in props["LB"]]
            adorns = [self.add_adornment(move, x[0], x[1], goboard.Adornments.letter, x[2])
                      for x in coords]
            for x in adorns:
                self._main_win.add_unrendered_adornment(x)


    ### _save_and_update_comments ensures the model captures any comment
    ### changes for the origin and displays dest's comments.  Dest may be a new
    ### move, and its empty string comment clears the textbox.  Dest may also
    ### be the previous move of origin if we're unwinding a move right now.
    ### Dest and origin may not be contiguous when jumping to the end or start
    ### of the game.  If either origin or dest is None, then it represents the
    ### intial board state.  If the captured comment has changed, mark game as
    ### dirty.
    ###
    def _save_and_update_comments (self, origin, dest):
        self.save_comment(origin)
        if dest is not None:
            self._main_win.commentBox.Text = dest.comments
        else:
            self._main_win.commentBox.Text = self.comments

    ### save_current_comment makes sure the current comment is persisted from the UI to
    ### the model.  This is used from the UI, such as when saving a file.
    ###
    def save_current_comment (self):
        self.save_comment(self.current_move)

    ### save_comment takes a move to update with the current comment from the UI.
    ### If move is null, the comment belongs to the game start or empty board.
    ###
    def save_comment (self, move):
        cur_comment = self._main_win.commentBox.Text
        if move is not None:
            if move.comments != cur_comment:
                move.comments = cur_comment
                self.dirty = True
        else:
            if self.comments != cur_comment:
                self.comments = cur_comment
                self.dirty = True



    ###
    ### Cutting and Pasting Sub Trees
    ###

    ### cut_move must be invoked on a current move.  It leaves the game state
    ### with the previous move or initial board as the current state, and it
    ### updates UI.
    ###
    def cut_move (self):
        cut_move = self.current_move
        if cut_move is None:
            raise Exception("Must cut current move, so cannot be initial board state.")
        ## unwind move with all UI updates and game model updates (and saves comments)
        self._main_win.prevButton_left_down(None, None)
        prev_move = self.current_move
        cut_move.previous = None
        if prev_move is None:
            ## Handle initial board state.  Can't use _cut_next_move here due
            ## to special handling of initial board and self._state.
            branches = self.branches
            if branches is None:
                self.first_move = None
                self._state = GameState.NOT_STARTED
            else:
                cut_index = _list_find(cut_move, branches)
                new_branches = branches[:cut_index] + branches[cut_index + 1:]
                self.first_move = new_branches[0]
                if len(new_branches) == 1:
                    self.branches = None
                else:
                    self.branches = new_branches
            if (self.parsed_game is not None and
                    self.parsed_game.nodes.next is not None):
                ## May not be parsed node to cut since the cut move
                ## could be new (not from parsed file)
                self._cut_next_move(self.parsed_game.nodes, self.parsed_game.nodes.next)
        else:
            ## Handle regular move.
            self._cut_next_move(prev_move, cut_move)
        self._cut_move = cut_move
        self.dirty = True
        ## Update UI now that current move's next/branches have changed.
        if prev_move is None:
            self._main_win.nextButton.IsEnabled = self.first_move is not None
            self._main_win.endButton.IsEnabled = self.first_move is not None
            self._main_win.update_branch_combo(self.branches, self.first_move)
        else:
            self._main_win.nextButton.IsEnabled = prev_move.next is not None
            self._main_win.endButton.IsEnabled = prev_move.next is not None
            self._main_win.update_branch_combo(prev_move.branches, prev_move.next)

    ### _cut_next_move takes a Move or ParsedNode that is the previous move of
    ### the second argument, which is the move being cut.  This cleans up next
    ### pointers and branches list appropriately for the move_or_parsednode.
    ###
    def _cut_next_move (self, move_or_parsednode, cut_move):
        branches = move_or_parsednode.branches
        if branches is None:
            move_or_parsednode.next = None
            if (type(move_or_parsednode) is goboard.Move and
                  move_or_parsednode.parsed_node is not None and
                  move_or_parsednode.parsed_node.next is not None):
                self._cut_next_move(move_or_parsednode.parsed_node, 
                                    move_or_parsednode.parsed_node.next)
        else:
            cut_index = _list_find(cut_move, branches)
            new_branches = branches[:cut_index] + branches[cut_index + 1:]
            move_or_parsednode.next = new_branches[0]
            if len(new_branches) == 1:
                move_or_parsednode.branches = None
            else:
                move_or_parsednode.branches = new_branches

    ### can_paste returns whether there is a cut sub tree, but it does not
    ### check whether the cut tree actually can be pasted at the current move.
    ### It ignores whether the right move color will follow the current move,
    ### which paste_move allows, but this does not check whether all the moves
    ### will occupy open board locations, which paste_move requires.
    ###
    def can_paste (self):
        return self._cut_move is not None

    ### paste_move makes self._cut_move be the next move of the current move
    ### displayed.  It does not worry about duplicate next moves; it just
    ### pastes the sub tree.  If there is a next move at the same loc, we do
    ### not merge the trees matching moves since this would lose node
    ### information (marked up and comments).
    ###
    def paste_move (self):
        if self._cut_move is None:
            raise Exception("No cut sub tree to paste.")
        if self._cut_move.color != self.next_color:
            MessageBox.Show("Cannot paste cut move that is same color as current move.");
            return;
        cur_move = self.current_move
        if cur_move is not None:
            _paste_next_move(cur_move, self._cut_move)
        else:
            if self.first_move is not None:
                # branching initial board state
                if self.branches is None:
                    self.branches = [self.first_move, self._cut_move]
                else:
                    self.branches.append(self._cut_move)
                self.first_move = self._cut_move
                self.first_move.number = 1
                if (self.parsed_game is not None and
                      self._cut_move.parsed_node is not None):
                    _paste_next_move(self.parsed_game.nodes,
                                     self._cut_move.parsed_node)
            else:
                if self._state is not GameState.NOT_STARTED:
                    raise Exception("Internal error: " + 
                                    "no first move and game not started?!")
                # not branching initial board state
                self.first_move = self._cut_move
                self.first_move.number = 1
                self._state = GameState.STARTED
        self._cut_move.previous = cur_move  # stores None appropriately when no current
        self.dirty = True
        _renumber_moves(self._cut_move)
        self._cut_move = None
        self._main_win.nextButton_left_down(None, None)


    ###
    ### Adornments
    ###

    ### add_adornment creates the Adornments object in the model and adds it to
    ### move.  If move is None (or game not started), then this affects the
    ### initial game state.  The returns the new adornment.  If all the letter
    ### adornments have been used at this point in the game tree, then this
    ### adds nothing and returns None.
    ###
    def add_adornment (self, move, row, col, kind, data = None):
        def make_adornment (adornments, data):
            ## Pass in adornments because access is different for initial board
            ## state vs. a move.  Pass in data because Python has broken
            ## closure semantics or poor lexical model, take your pick :-).
            if kind is goboard.Adornments.letter and data is None:
                letters = [a for a in adornments if 
                           a.kind is goboard.Adornments.letter]
                if len(letters) == 26:
                    return None, None
                for elt in ['A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K',
                            'L', 'M', 'N', 'O', 'P', 'Q', 'R', 'S', 'T', 'U', 'V',
                            'W', 'X', 'Y', 'Z']:
                    if _list_find(elt, letters,
                                  lambda x,y: x == y.cookie.Child.Content) == -1:
                        data = elt #chr(ord('A') +  len(letters))
                        break
            return goboard.Adornments(kind, row, col, None, data), data
        if self._state is GameState.NOT_STARTED or move is None:
            adornment, data = make_adornment(self.setup_adornments, data)
            if adornment is None: return None
            self.setup_adornments.append(adornment)
        elif move is not None:
            adornment, data = make_adornment(move.adornments, data)
            if adornment is None: return None
            move.add_adornment(adornment)
        else:
            raise Exception("Should never get here.")
        return adornment

    ### get_adornment return the adornment of kind kind at the row, col
    ### location if there is one, otherwise it returns None.
    ###
    def get_adornment (self, row, col, kind):
        move = self.current_move
        if self._state is GameState.NOT_STARTED or move is None:
            adornments = self.setup_adornments
        elif move is not None:
            adornments = move.adornments
        else:
            raise Exception("Should never get here.")
        for a in adornments:
            if a.kind is kind and a.row == row and a.column == col:
                return a
        return None

    ### remove_adornment assumes a is in the current adornments list, and
    ### signals an error if it is not.  You can always call this immediately
    ### after get_adornment if no move state has changed.
    ###
    def remove_adornment (self, a):
        move = self.current_move
        if self._state is GameState.NOT_STARTED or move is None:
            adornments = self.setup_adornments
        elif move is not None:
            adornments = move.adornments
        else:
            raise Exception("Should never get here.")
        adornments.remove(a)

    ###
    ### Misc Branches UI helpers
    ###

    ### set_current_branch is a helper for UI that changes which branch to take
    ### following the current move.  Cur is the index of the selected item in
    ### the branches combo box, which maps to the branches list for the current
    ### move.
    ###
    def set_current_branch (self, cur):
        if self.current_move is None:
            move = self.branches[cur]
            self.first_move = move
        else:
            move = self.current_move.branches[cur]
            self.current_move.next = move

    ### move_branch_up and move_branch_down move the current move (if it
    ### follows a move or initial board state with branching) to be higher or
    ### lower in the previous branches list.  If the game hasn't started, or
    ### the conditions aren't met, this informs the user.
    ###
    def move_branch_up (self):
        (branches, cur_index) = self._branches_for_moving()
        if branches is not None:
            self._move_branch(branches, cur_index, -1)

    def move_branch_down (self):
        (branches, cur_index) = self._branches_for_moving()
        if branches is not None:
            self._move_branch(branches, cur_index, 1)

    ### _branches_for_moving returns the branches list (from previous move or
    ### intial board state) and the index in that list of the current move.
    ### This does user interation for move_branch_up and move_branch_down.
    ###
    def _branches_for_moving (self):
        ## Check if have move
        if self._state is GameState.NOT_STARTED:
            MessageBox.Show("Game not started, now branches to modify.")
            return (None, None)
        current = self.current_move
        if current is None:
            MessageBox.Show("Must be on the first move of a branch to move it.")
            return (None, None)
        ## Get appropriate branches
        prev = current.previous
        if prev is None:
            branches = self.branches
        else:
            branches = prev.branches
        ## Get index of current move in branches
        if branches is None:
            MessageBox.Show("Must be on the first move of a branch to move it.")
            return (None, None)
        elif prev is None:
            cur_index = branches.index(self.first_move)
        else:
            cur_index = branches.index(prev.next)
        ## Successful result ...
        return (branches, cur_index)

    ### _move_branch takes a list of brances and the index of a branch to move
    ### up or down, depending on delta.  This provides feedback to the user of
    ### the result.
    ###
    def _move_branch (self, branches, cur_index, delta):
        if delta not in [1, -1]:
            raise Exception("Branch moving delta must be 1 or -1.")
        def swap ():
            tmp = branches[cur_index]
            branches[cur_index] = branches[cur_index + delta]
            branches[cur_index + delta] = tmp
        if delta < 0:
            if cur_index > 0:
                swap()
                MessageBox.Show("Branch moved up.")
            else:
                MessageBox.Show("This branch is the main branch.")
        elif delta > 0:
            if cur_index < (len(branches) - 1):
                swap()
                MessageBox.Show("Branch moved down.")
            else:
                MessageBox.Show("This branch is the last branch.")
        else:
            raise Exception("Must call _move_branch with non-zero delta.")

###
### File Writing
###

    ### write_game takes a filename to write an .sgf file.  This maps the game
    ### to an sgfparser.ParsedGame and uses its __str__ method to produce the
    ### output.
    ###
    def write_game (self, filename = None):
        if filename is None:
            if self.filename is None:
                raise Exception ("Need filename to write file.")
            filename = self.filename
        pg = parsed_game_from_game(self)
        f = open(filename, "w")
        f.write(str(pg))
        f.close()
        self.dirty = False
        self.filename = filename
        self.filebase = filename[filename.rfind("\\") + 1:]
        if self.current_move is None:
            number = 0
            is_pass = False
        else:
            number = self.current_move.number
            is_pass = self.current_move.is_pass
        self._main_win.update_title(number, is_pass, self.filebase)
        #self.Title = "SGFEd -- " + self.filebase + ";  Move " + str(number)

    ### write_flipped_game saves all the game moves as a diagonal mirror image.
    ### You can share a game you recorded with your opponents, and they can see
    ### it from their points of view.  Properties to modify: AB, AW, B, W, LB,
    ### SQ, TR, MA.
    ###
    def write_flipped_game (self, filename):
        pg = parsed_game_from_game(self, True) # True = flipped
        f = open(filename, "w")
        f.write(str(pg))
        f.close()
        self.dirty = False
        
### end Game class


###
### Mapping Games to ParsedGames (for printing)
###

### parsed_game_from_game returns a ParsedGame representing game, re-using
### existing parsed node properties where appropriate to avoid losing any we
### ignore from parsed files.  If flipped is true, then moves and adornment
### indexes are diagonally mirrored; see write_flipped_game.
###
def parsed_game_from_game (game, flipped = False):
    pgame = sgfparser.ParsedGame()
    pgame.nodes = _gen_parsed_game_root(game, flipped)
    if game.branches is None:
        if game.first_move is not None:
            pgame.nodes.next = _gen_parsed_nodes(game.first_move, flipped)
            pgame.nodes.next.previous = pgame.nodes
    else:
        branches = []
        for m in game.branches:
            tmp = _gen_parsed_nodes(m, flipped)
            branches.append(tmp)
            tmp.previous = pgame.nodes
        pgame.nodes.next = branches[0]
    return pgame

### _gen_parsed_game_root returns a ParsedNode that is based on the Game object
### and that represents the first node in a ParsedGame.  It grabs any existing
### root node properties if there's an existing ParsedGame root node.  If
### flipped is true, then moves and adornment indexes are diagonally mirrored;
### see write_flipped_game.
###
### NOTE, this function needs to overwrite any node properties that the UI
### supports editing.  For example, if the end user can change the players
### names or rank, then this function needs to overwrite the node properties
### value with the game object's value.  It also needs to write properties from
### new games.
###
def _gen_parsed_game_root (game, flipped):
    n = sgfparser.ParsedNode()
    if game.parsed_game is not None:
        n.properties = _copy_properties(game.parsed_game.nodes.properties)
    n.properties["AP"] = ["SGFPy"]
    n.properties["SZ"] = [str(game.board.size)]
    ## Comments
    if "GC" in n.properties:
        ## game.comments has merged GC and C comments.
        del n.properties["GC"]
    if game.comments != "":
        n.properties["C"] = [game.comments]
    elif "C" in n.properties:
        del n.properties["C"]
    ## Handicap/Komi
    if game.handicap != 0 and game.handicap != "0":
        n.properties["HA"] = [str(game.handicap)]
    elif "HA" in n.properties:
        del n.properties["HA"]
    n.properties["KM"] = [game.komi]
    if "AB" in n.properties:
        if flipped:
            n.properties["AB"] = flip_coordinates(n.properties["AB"])
        ## else leave them as-is
    else:
        if game.handicap != 0 and game.handicap != "0":
            n.properties["AB"] = [goboard.get_parsed_coordinates(m, flipped) for
                                  m in game.handicap_moves]
    ## Player names
    n.properties["PB"] = ((game.player_black is not None and [game.player_black]) or 
                          ["Black"])
    n.properties["PW"] = ((game.player_white is not None and [game.player_white]) or 
                          ["White"])
    return n

def _copy_properties (props):
    res = {}
    for k,v in props.iteritems():
        res[k] = v[:]
    return res

### _gen_parsed_nodes returns a ParsedNode with all the moves following move
### represented in the linked list.  If move has never been rendered, then the
### rest of the list is the parsed nodes hanging from it since the user could
### not have modified the game at this point.  This recurses on move objects
### with branches.  If flipped is true, then moves and adornment indexes are
### diagonally mirrored; see write_flipped_game.
###
def _gen_parsed_nodes (move, flipped):
    if not move.rendered:
        ## If move exists and not rendered, then must be ParsedNode.
        if flipped:
            return _clone_and_flip_nodes(move.parsed_node)
        else:
            return move.parsed_node
    cur_node = _gen_parsed_node(move, flipped)
    first = cur_node
    if move.branches is None:
        move = move.next
        while move is not None:
            cur_node.next = _gen_parsed_node(move, flipped)
            cur_node.next.previous = cur_node
            if move.branches is None:
                cur_node = cur_node.next
                move = move.next
            else:
                cur_node = cur_node.next                
                break
    ## Only get here when move is None, or we're recursing on branches.
    if move is not None:
        cur_node.branches = []
        for m in move.branches:
            tmp = _gen_parsed_nodes(m, flipped)
            cur_node.branches.append(tmp)
            tmp.previous = cur_node
        cur_node.next = cur_node.branches[0]
    return first

### _gen_parsed_node returns a ParsedNode that is based on the Move object.  It
### grabs any existing parsed node properties from move to preserve any move
### properties that we ignore from a file we read.  This does not just take the
### whole parsed node from move to avoid keeping branches or whatnot that we've
### deleted.  If flipped is true, then moves and adornment indexes are
### diagonally mirrored; see write_flipped_game.
###
### NOTE, this function needs to overwrite any node properties that the UI
### supports editing.  For example, if the end user modified adornments.
###
def _gen_parsed_node (move, flipped):
    if not move.rendered:
        ## If move exists and not rendered, then must be ParsedNode.
        if flipped:
            return _clone_and_flip_nodes(move.parsed_node)
        else:
            return move.parsed_node
    node = sgfparser.ParsedNode()
    node.properties = ((move.parsed_node is not None and
                         _copy_properties(move.parsed_node.properties)) or
                       node.properties)
    props = node.properties
    ## Color
    if move.color == Colors.Black:
        props["B"] = [goboard.get_parsed_coordinates(move, flipped)]
    elif move.color == Colors.White:
        props["W"] = [goboard.get_parsed_coordinates(move, flipped)]
    else:
        raise Exception ("Should have only B or W moves.")
    ## Comments
    if move.comments != "":
        props["C"] = [move.comments]
    elif "C" in props:
        del props["C"]
    ## Adornments
    if "TR" in props:
        del props["TR"]
    if "SQ" in props:
        del props["SQ"]
    if "LB" in props:
        del props["LB"]
    for a in move.adornments:
        coords = goboard.get_parsed_coordinates(a, flipped)
        if a.kind is goboard.Adornments.triangle:
            if "TR" in props:
                props["TR"].append(coords)
            else:
                props["TR"] = [coords]
        if a.kind is goboard.Adornments.square:
            if "SQ" in props:
                props["SQ"].append(coords)
            else:
                props["SQ"] = [coords]
        if a.kind is goboard.Adornments.letter:
            data = coords + ":" + a.cookie.Child.Content
            if "LB" in props:
                props["LB"].append(data)
            else:
                props["LB"] = [data]
    return node

### _clone_and_flip_nodes is similar to _gen_parsed_nodes.  This returns a
### ParsedNode with all the nodes following the argument represented in the
### resulting linked list, but their coordinates have been transposed to the
### diagonal mirror image, see write_flipped_game.  This recurses on nodes with
### branches.
###
def _clone_and_flip_nodes (nodes):
    first = _clone_and_flip_node(nodes)
    cur_node = first
    if nodes.branches is None:
        nodes = nodes.next
        while nodes is not None:
            cur_node.next = _clone_and_flip_node(nodes)
            cur_node.next.previous = cur_node
            if nodes.branches is None:
                cur_node = cur_node.next
                nodes = nodes.next
            else:
                cur_node = cur_node.next                
                break
    ## Only get here when nodes is None, or we're recursing on branches.
    if nodes is not None:
        cur_node.branches = []
        for m in nodes.branches:
            tmp = _clone_and_flip_nodes(m)
            cur_node.branches.append(tmp)
            tmp.previous = cur_node
        cur_node.next = cur_node.branches[0]
    return first

### _clone_and_flip_node is similar to _gen_parsed_node.  This returns a
### ParsedNode that is a clone of node, but any indexes are diagonally mirror
### transposed, see write_flipped_game.
###
def _clone_and_flip_node (node):
    new_node = sgfparser.ParsedNode()
    new_node.properties = _copy_properties(node.properties)
    props = new_node.properties
    ## Color
    if "B" in props:
        props["B"] = flip_coordinates(props["B"])
    elif "W" in props:
        props["W"] = flip_coordinates(props["W"])
    else:
        raise Exception ("Should have only B or W moves.")
    ## Adornments
    if "TR" in props:
        props["TR"] = flip_coordinates(props["TR"])
    if "SQ" in props:
        props["SQ"] = flip_coordinates(props["SQ"])
    if "LB" in props:
        props["LB"] = flip_coordinates(props["LB"], True)
    return new_node

### flip_coordinates takes a list of parsed coordinate strings and returns the
### same kind of list with the coorindates diagonally flipped (see
### write_flipped_game).
###
def flip_coordinates (coords, labels = False):
    if labels:
        ## coords elts are "<col><row>:<letter>"
        return [x + y for x in flip_coordinates([l[:2] for l in coords])
                      for y in [lb[2:] for lb in coords]]
    else:
        return [goboard.flip_parsed_coordinates(yx) for yx in coords]
    

###
### Internal utilities for Game methods.
###

### _paste_next_move takes a Move or ParsedNode that is the current move to
### which _paste_next_move adds cut_move as the next move.  This sets up next
### pointers and the branches list appropriately for the move_or_parsednode.
###
def _paste_next_move (move_or_parsednode, cut_move):
    if move_or_parsednode.next is not None:
        ## need branches
        if move_or_parsednode.branches is None:
            move_or_parsednode.branches = [move_or_parsednode.next, cut_move]
        else:
            move_or_parsednode.branches.append(cut_move)
        move_or_parsednode.next = cut_move
    else:
        move_or_parsednode.next = cut_move
    cut_move.previous = move_or_parsednode
    if (type(move_or_parsednode) is goboard.Move):
        move_or_parsednode.next.number = move_or_parsednode.number + 1
        if (move_or_parsednode.parsed_node is not None and
                cut_move.parsed_node is not None):
            _paste_next_move(move_or_parsednode.parsed_node, cut_move.parsed_node)

### _renumber_moves takes a move with the correct number assignment and walks
### the sub tree of moves to reassign new numbers to the nodes.  This is used
### by game._paste_move.
###
def _renumber_moves (move):
    count = move.number
    if move.branches is None:
        move = move.next
        while move is not None:
            move.number = count + 1
            count += 1
            if move.branches is None:
                move = move.next
            else:
                break
    ## Only get here when move is None, or we're recursing on branches.
    if move is not None:
        for m in move.branches:
            m.number = count
            _renumber_moves(m)

### _check_for_coincident_moves checks if every move in a cut tree can play
### where pasted to ensure no conflicts.
###
### Turns out this test isn't so good.  Need to do "abstract interpretation"
### to see if moves are played where moves would be cut.  KGS just lets you paste
### and just plays stones over other stones on the board, not sound but works.
###
#def _check_for_coincident_moves (board, move):
#    while move is not None:
#        if board.move_at(move.row, move.column) is not None:
#            return True
#        if move.branches is None:
#            move = move.next
#        else:
#            break
#    ## Only get here when move is None from while loop, or we're recursing on branches.
#    if move is not None:
#        for m in move.branches:
#            if _check_for_coincident_moves(board, m):
#                return True
#    return False

### _list_find returns the index of elt in the first argument using the compare
### test.  The test defaults to identity.  Need to define this since python
### doesn't have general sequence utilities.  Str has find, but list doesn't.
###
def _list_find (elt, l, compare = lambda x,y: x is y):
   for k, v in enumerate(l):
       if compare(elt, v): return k
   else:
       return -1


###
### External Helper Functions
###

def opposite_move_color (color):
    return (color == Colors.Black and Colors.White) or Colors.Black

    
def create_default_game (main_win):
    return Game(main_win, MAX_BOARD_SIZE, 0, DEFAULT_KOMI)


### create_parsed_game takes a ParsedGame and main UI window.  It creates a new
### Game (which cleans up the current game) and sets up the first moves so that
### the user can start advancing through the moves.
###
def create_parsed_game (pgame, main_win):
    ## Check some root properties
    props = pgame.nodes.properties
    ## Handicap stones
    if "HA" in props:
        ## KGS saves HA[6] and then AB[]...
        handicap = int(props["HA"][0])
        if "AB" not in props:
            raise Exception("If parsed game has handicap, then need handicap stones.")
        def make_handicap_move (coords):
            row, col = goboard.parsed_to_model_coordinates(coords)
            m = goboard.Move(row, col, Colors.Black)
            m.parsed_node = pgame.nodes
            m.rendered = False
            return m
        all_black = [make_handicap_move(x) for x in props["AB"]]
    else:
        handicap = 0
        all_black = None
    if "AW" in props:
        raise Exception("Don't support multiple white stones at root.")
    ## Board size
    if "SZ" not in props:
        raise Exception("No board size property?!")
    size = int(props["SZ"][0])
    if size != 19:
        raise Exception("Only work with size 19 currently, got %s" % (size))
    ## Komi
    if "KM" in props:
        komi = props["KM"][0]
    else:
        komi = ((handicap == 0) and DEFAULT_KOMI) or "0.5"
    ## Creating new game cleans up current game
    g = Game(main_win, size, handicap, komi, all_black)
    ## Player names
    if "PB" in props:
        g.player_black = props["PB"][0]
    if "PW" in props:
        g.player_white = props["PW"][0]
    ## Initial board state comments
    if "C" in props:
        g.comments = props["C"][0]
    if "GC" in props:
        g.comments = props["GC"][0] + g.comments
    ## Setup remaining model for first moves and UI
    g.parsed_game = pgame
    _setup_first_parsed_move(g, pgame.nodes)
    ## Setup navigation UI so that user can advance through game.
    if g.first_move is not None:
        ## No first move if file just has handicap stones.
        g._state = GameState.STARTED
        main_win.nextButton.IsEnabled = True
        main_win.endButton.IsEnabled = True
        main_win.update_branch_combo(g.branches, g.first_move)
    else:
        main_win.nextButton.IsEnabled = False
        main_win.endButton.IsEnabled = False
        main_win.update_branch_combo(g.branches, None)
    main_win.commentBox.Text = g.comments
    main_win.game = g
    return g


### _setup_first_parsed_move takes a game and the head of ParsedNodes.  It sets
### up the intial move models, handling initial node branching and so on.  The
### basic invariant here is that we always have the next move models created,
### but they are in an unrendered state.  This means their branches have not
### been processed, adornments have never been created, captured stones never
### processed, etc.  When we advance to a move, we render it and set up its
### next move(s) as unrendered.  Keeping the next move pointer of a Move object
### set up makes several other invariants in helper functions and game
### processing fall out.  This function returns a None g.first_move if the .sgf
### file only had a root node.
###
def _setup_first_parsed_move (g, nodes):
    props = nodes.properties
    if "B" in props or "W" in props:
        raise Exception("Unexpected move in root parsed node.")
    if "PL" in props:
        raise Exception("Do not support player-to-play for changing start color.")
    if "AW" in props:
        raise Exception("Do not support AW in root node.")
    if "TR" in props or "SQ" in props or "LB" in props:
        raise Exception("Don't handle adornments on initial board from parsed game yet.")
    if nodes.branches is not None:
        ## Game starts with branches
        moves = []
        for n in nodes.branches:
            m = _parsed_node_to_move(n)
            m.number = g.move_count + 1
            ## Don't set m.previous since they are fist moves.
            moves.append(m)
        g.branches = moves
        m = moves[0]
    else:
        nodes = nodes.next
        if nodes is None:
            m = None
        else:
            m = _parsed_node_to_move(nodes)
            ## Note, do not incr g.move_count since first move has not been rendered,
            ## so if user clicks, that should be number 1 too.
            m.number = g.move_count + 1
    g.first_move = m
    return m

### _parsed_node_to_move takes a ParsedNode and returns a Move model for it.
### For now, this is fairly constrained to expected next move colors and no
### random setup nodes that place several moves or just place adornments.
###
def _parsed_node_to_move (n):
    if "B" in n.properties:
        color = Colors.Black
        row, col = goboard.parsed_to_model_coordinates(n.properties["B"][0])
    elif "W" in n.properties:
        color = Colors.White
        row, col = goboard.parsed_to_model_coordinates(n.properties["W"][0])        
    else:
        raise Exception("Next nodes must be moves, don't handle arbitrary nodes yet -- %s" %
                        (n.node_str(False)))
    m = goboard.Move(row, col, color)
    m.parsed_node = n
    m.rendered = False
    if "C" in n.properties:
        m.comments = n.properties["C"][0]
    return m



### GameState simply represents whether a game has started (that is, there's a
### first move).  DONE is gratuitous right now and isn't used.  If we support
### pass move, and there's two, perhaps DONE becomes relevant :-).
###
class GameState (object):
    NOT_STARTED = object()
    STARTED = object()
    DONE = object()







###############################################################################

## The display is a grid of columns and rows, with the main game tree spine
## drawn across the first row, with branches descending from it. So, columns
## map to tree depth, and a column N, should have a move with number N,
## due to fake node added for board start in column zero.
##

## show_tree displays a grid of node objects that represent moves in the game tree,
## where lines between moves need to bend, or where lines need to descend straight
## downward before angling to draw next move in a branch.
##
test_columns = 50
test_rows = 10
def show_tree (game):
    pn = game.parsed_game.nodes
    tree_grid = [[None for col in xrange(test_columns)] for row in xrange(test_rows)]
    max_rows = [0 for col in range(test_columns)]
    layout(pn, tree_grid, max_rows, 0, 0, 0, 0)
    display = [[" " for col in xrange(test_columns)] for row in xrange(test_rows)]
    for i in xrange(len(tree_grid)):
        for j in xrange(len(tree_grid[i])):
            if tree_grid[i][j] is None:
                display[i][j] = "+"
            elif tree_grid[i][j].color == Colors.Black:
                display[i][j] = "X"
            elif tree_grid[i][j].color == Colors.White:
                display[i][j] = "O"
            elif tree_grid[i][j].kind is TreeViewNode.line_bend_kind:
                display[i][j] = "L"
            else: #if tree_grid[i][j].color == Colors.BurlyWood:
                display[i][j] = "S"
    MessageBox.Show("\n".join(["".join(r) for r in display]))

## layout recurses through the moves assigning them to a location in the display grid.
## max_rows is an array mapping the column number to the next free row that
## can hold a node.  cum_max_row is the max row used while descending a branch
## of the game tree, which we use to create branch lines that draw straight across,
## rather than zigging and zagging along the contour of previously placed nodes.
## tree_depth is just that, and branch_depth is the heigh to the closest root node of a
## branch, where its immediate siblings branch too.
##
def layout (pn, tree_grid, max_rows, cum_max_row, tree_depth, branch_depth, branch_root_row):
    model = setup_layout_model(pn, max_rows, cum_max_row, tree_depth)
    if branch_depth == 0:
        ## If we're not doing a branch, keep the zero.
        new_branch_depth = 0
    else: # Increment the depth for the children
        new_branch_depth = branch_depth + 1
    ## Layout main child branch
    if pn.next is None:
        ## If no next, then no branches to check below
        maybe_add_bend_node(tree_grid, max_rows, model.row, tree_depth, branch_depth, branch_root_row)
        tree_grid[model.row][tree_depth] = model
        return model
    else:
        next_model = layout(pn.next, tree_grid, max_rows, model.row, tree_depth + 1, 
                            new_branch_depth, branch_root_row)
    adjust_layout_row(model, tree_grid, max_rows, next_model.row, tree_depth, branch_depth, branch_root_row)
    maybe_add_bend_node(tree_grid, max_rows, model.row, tree_depth, branch_depth, branch_root_row)
    tree_grid[model.row][tree_depth] = model
    ## Layout branches if any
    if pn.branches is not None:
        for i in xrange(1, len(pn.branches)):
            layout(pn.branches[i], tree_grid, max_rows, model.row, tree_depth + 1, 1, model.row)
    return model

## setup_layout_model initializes the current node model for the display, with row, column,
## color, etc.  This returns the new model element.
##
def setup_layout_model (pn, max_rows, cum_max_row, tree_depth):
    model = TreeViewNode((tree_depth == 0 and TreeViewNode.start_board_kind) 
                         or TreeViewNode.move_kind, pn)
    ## Get column's free row or use row from parent
    row = max(cum_max_row, max_rows[tree_depth])
    model.row = row
    max_rows[tree_depth] = row + 1
    model.col = tree_depth
    ## Set color
    if "B" in pn.properties:
        model.color = Colors.Black
    elif "W" in pn.properties:
        model.color = Colors.White
    elif tree_depth == 0:
        ## This is the empty board start node
        model.color = Colors.BurlyWood # sentinel color
    else:
        raise Exception("eh?!  Node is not move, nor are we at the start of the parsed tree -- %s" %
                        (pn.node_str(False)))
    return model

## adjust_layout_row adjusts moves downward if moves farther out on the branch
## had to occupy lower rows.  This keeps branches drawn straighter, rather than
## zig-zagging with node contours.  Then this function checks to see if we're
## within the square defined by the current model and the branch root, and if we
## this is the case, then start subtracting one row at at time to get a diagonal
## line of moves up to the branch root.
##
def adjust_layout_row (model, tree_grid, max_rows, next_row_used, tree_depth, 
                       branch_depth, branch_root_row):
    ## If we're on a branch, and it had to be moved down farther out to the right
    ## in the layout, then move this node down to keep a straight line.
    if next_row_used > model.row:
        model.row = next_row_used
        max_rows[tree_depth] = next_row_used + 1
    ## If we're unwinding back toward this node's branch root, and we're within a direct
    ## diagonal line from the root, start decreasing the row by one.
    if (branch_depth < model.row - branch_root_row) and (tree_grid[model.row - 1][tree_depth] is None):
        ## row - 1 does not index out of bounds since model.row would have to be zero,
        ## and zero minus anything will not be greater than branch depth (which would be zero)
        ## if row - 1 were less than zero.
        model.row = model.row - 1
        max_rows[tree_depth] = model.row

## maybe_add_bend_node checks if the diagonal line of rows for a branch hit the column
## for the branch's root at a row great than the root's row.  If this happens, then we
## need a model node to represent where to draw the line bend to start the diagonal line.
##
def maybe_add_bend_node (tree_grid, max_rows, row, tree_depth, branch_depth, branch_root_row):
    if (branch_depth == 1) and (row - branch_root_row > 1) and (tree_grid[row - 1][tree_depth - 1] is None):
        ## last test should always be true
        bend = TreeViewNode(TreeViewNode.line_bend_kind)
        bend.row = row - 1
        bend.col = tree_depth - 1
        max_rows[tree_depth - 1] = row
        tree_grid[bend.row][bend.col] = bend


class TreeViewNode (object):
    move_kind = object()
    line_bend_kind = object()
    start_board_kind = object()

    def __init__ (self, kind = move_kind, node = None):
        self.kind = kind
        self.cookie = None
        self.node = node
        self.row = 0
        self.col = 0
        self.color = None

    #def __str__ (self):
    #    if self.color == Colors.Black:
    #        return "X"
    #    elif self.color == Colors.White:
    #        return "O"
    #    elif self.kind is TreeViewNode.line_bend_kind:
    #        return "\""
    #    else: # Colors.BurlyWood for start of boardi
    #        return "S"

