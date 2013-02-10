using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Xaml.Shapes; // Rectangle
using Windows.UI; // Color
using Windows.UI.Popups; // MessageDialog
using Windows.UI.Core; // CoreWindow, PointerEventArgs
using Windows.System; // VirtualKeyModifiers
using Windows.Storage.Pickers;  // FileOpenPicker
using Windows.Storage; // FileIO
using System.Threading.Tasks; // Task<T>
using System.Diagnostics; // Debug.Assert
using Windows.UI.Text; // FontWeight
using Windows.UI.ViewManagement; // ApplicationView (for snap view)


namespace SgfEdwin8 {

    ////
    //// What VS generated for me ...
    ////

    // The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238
    //
    //public sealed partial class MainPage : Page
    //{
    //    public MainPage()
    //    {
    //        this.InitializeComponent();
    //    }

    //    /// <summary>
    //    /// Invoked when this page is about to be displayed in a Frame.
    //    /// </summary>
    //    /// <param name="e">Event data that describes how this page was reached.  The Parameter
    //    /// property is typically used to configure the page.</param>
    //    protected override void OnNavigatedTo(NavigationEventArgs e)
    //    {
    //    }
    //}


    ////
    //// MainWindow class
    ////

    public sealed partial class MainWindow : Page {

        private const string HelpString = @"
SGFEd can read and write .sgf files, edit game trees, etc.

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
";

        private int prevSetupSize = 0;

        public Game Game { get; set; }


        public MainWindow () {
            this.InitializeComponent();
            this.prevSetupSize = 0;
            this.Game = GameAux.CreateDefaultGame(this);
            // fixing win8 -- tried this to always get it invoked, but the event is simply not raising
            //this.AddHandler(UIElement.KeyDownEvent, (KeyEventHandler)this.mainWin_keydown, true);
        } // Constructor


        //// 
        //// Handling Snap View (required by store compliance)
        //// 

        protected override void OnNavigatedTo (NavigationEventArgs e) {
            // Can't use e.Parameter here as in examples because it is appears to have type CSharp.Runtime.Binder?
            //MainWindow mainWin = e.Parameter as MainWindow;
            Window.Current.SizeChanged += this.MainView_SizeChanged;
            base.OnNavigatedTo(e);
        }

        //// OnNavigatedFrom removes the resize handler, which was recommended in the sample I used
        //// to figure out how to create a snapped view.
        //// 
        protected override void OnNavigatedFrom (NavigationEventArgs e) {
            Window.Current.SizeChanged -= this.MainView_SizeChanged;
            base.OnNavigatedFrom(e);
        }

        //// MainView_SizeChanged does the work to switch from full view to snapped view.  For now
        //// this assumes the main view is good enough for filled view (complement of snapped)
        //// and portrait, which it might be if the xaml includes the commented out scrollviewer
        //// shown in sample used to figure out snapped view.  Also, if the xaml includes that
        //// scrollviewer, then the code in OnFocusLost will have to test for the scrollviewer
        //// used in SGFed and keep searching for the hidden root scrollviewer that steals focus.
        //// 
        void MainView_SizeChanged (object sender, WindowSizeChangedEventArgs e) {
            switch (ApplicationView.Value) {
                case ApplicationViewState.Snapped:
                    this.mainLandscapeView.Visibility = Visibility.Collapsed;
                    this.snapViewContent.Visibility = Visibility.Visible;
                    this.SetupSnappedViewDisplay();
                    break;
                case ApplicationViewState.FullScreenLandscape:
                case ApplicationViewState.FullScreenPortrait:
                case ApplicationViewState.Filled:
                    this.snapViewContent.Visibility = Visibility.Collapsed;
                    this.mainLandscapeView.Visibility = Visibility.Visible;
                    break;
            }
        }

        //// SetupSnappedViewDisplay creates a small static board view so that the user can see
        //// the current state of the game.  Need to refactor some code and state maintenance in
        //// MainWindowAux that assumes there's only one live view of the game: stones matrix for
        //// mapping where stone UIElements are in the view and current move adorment UIelement.
        //// 
        private bool DisplayedSnappedViewBefore = false;
        public void SetupSnappedViewDisplay () {
            if (! this.DisplayedSnappedViewBefore) {
                this.snapViewContent.Height = Window.Current.Bounds.Width;
                this.SetupLinesGrid(this.Game.Board.Size, this.collapsedBoardGrid);
                this.SetupStonesGrid(this.Game.Board.Size, this.collapsedStonesGrid);
                this.DisplayedSnappedViewBefore = true;
            }
            else
                this.collapsedStonesGrid.Children.Clear();
            this.AddHandicapStones(this.Game, this.collapsedStonesGrid);
            var size = this.Game.Board.Size;
            for (var row = 0; row < size; row++)
                for (var col = 0; col < size; col++) {
                    var stone = MainWindowAux.stones[row, col];
                    if (stone != null)
                        MainWindowAux.AddStoneNoMapping(this.collapsedStonesGrid, row + 1, col + 1, 
                                                        this.Game.Board.ColorAt(row + 1, col + 1));
                }
        }


        //// 
        //// Setting up Board and Stones Grids
        //// 

        //// setup_board_display sets up the lines and stones hit grids.  Game uses
        //// this to call back on the main UI when constructing a Game.  This also
        //// handles when the game gets reset to a new game from a file or starting
        //// fresh new game.
        ////
        public void SetupBoardDisplay (Game new_game) {
            if (this.prevSetupSize == 0) {
                // First time setup.
                this.SetupLinesGrid(new_game.Board.Size);
                this.SetupStonesGrid(new_game.Board.Size);
                MainWindowAux.SetupIndexLabels(this.stonesGrid, new_game.Board.Size);
                this.prevSetupSize = new_game.Board.Size;
                this.AddHandicapStones(new_game);
            }
            else if (this.prevSetupSize == new_game.Board.Size) {
                // Clean up and re-use same sized model objects.
                var cur_move = this.Game.CurrentMove;
                var g = this.stonesGrid;
                // Must remove current adornment before other adornments.
                if (cur_move != null)
                    MainWindowAux.RemoveCurrentStoneAdornment(g, cur_move);
                // Remove stones and adornments, so don't just loop over stones.
                foreach (var elt in g.Children.OfType<UIElement>().Where((o) => !(o is TextBlock)).ToList()) {
                    // Only labels in stones grid should be row/col labels
                    // because adornments are inside ViewBoxes.
                    g.Children.Remove(elt);
                }
                // Clear board just to make sure we drop all model refs.
                this.Game.Board.GotoStart();
                this.prevButton.IsEnabled = false;
                this.homeButton.IsEnabled = false;
                // If opening game file, next/end set to true by game code.
                this.nextButton.IsEnabled = false;
                this.endButton.IsEnabled = false;
                MainWindowAux.stones = new Ellipse[Game.MaxBoardSize, Game.MaxBoardSize];
                this.AddHandicapStones(new_game);
            }
            else
                throw new Exception("Haven't implemented changing board size for new games.");
            this.InitializeTreeView();
        }

        //// SetupLinesGrid takes an int for the board size (as int) and returns a WPF Grid object for
        //// adding to the MainWindow's Grid.  The returned Grid contains lines for the Go board.
        ////
        private Grid SetupLinesGrid (int size, Grid g = null) {
            Debug.Assert(size >= Game.MinBoardSize && size <= Game.MaxBoardSize,
                         "Board size must be between " + Game.MinBoardSize +
                         " and " + Game.MaxBoardSize + " inclusively.");
            // <Grid ShowGridLines="False" Background="#FFD7B264" Grid.RowSpan="2" HorizontalAlignment="Stretch"
            //       Margin="2" Name="boardGrid" VerticalAlignment="Stretch" 
            //       Width="{Binding ActualHeight, RelativeSource={RelativeSource Self}}" >
            //
            if (g == null) {
                //var g = this.boardGrid;
                // On win8, need user control for setting stones focus, and win8 tooling doesn't set this.boardGrid.
                g = (Grid)this.inputFocus.FindName("boardGrid");
                this.boardGrid = g;
            }
            //Grid g = ((Grid)this.inputFocus.Content).Children[0] as Grid;
            MainWindowAux.DefineLinesColumns(g, size);
            MainWindowAux.DefineLinesRows(g, size);
            MainWindowAux.PlaceLines(g, size);
            if (size == Game.MaxBoardSize) {
                MainWindowAux.AddHandicapPoint(g, 4, 4);
                MainWindowAux.AddHandicapPoint(g, 4, 10);
                MainWindowAux.AddHandicapPoint(g, 4, 16);
                MainWindowAux.AddHandicapPoint(g, 10, 4);
                MainWindowAux.AddHandicapPoint(g, 10, 10);
                MainWindowAux.AddHandicapPoint(g, 10, 16);
                MainWindowAux.AddHandicapPoint(g, 16, 4);
                MainWindowAux.AddHandicapPoint(g, 16, 10);
                MainWindowAux.AddHandicapPoint(g, 16, 16);
            }
            return g;
        } // SetupLinesGrid

        /// SetupStonesGrid takes an int for the size of the go board and sets up
        /// this.stonesGrid to which we add stones and hit test mouse clicks.
        ///
        private void SetupStonesGrid (int size, Grid g = null) {
            if (g == null) {
                //var g = this.stonesGrid;
                // On win8, need user control for setting stones focus, and win8 tooling doesn't set this.stonesGrid.
                g = (Grid)this.inputFocus.FindName("stonesGrid");
                this.stonesGrid = g;
            }
            //Grid g = ((Grid)this.inputFocus.Content).Children[1] as Grid;
            // Define rows and columns
            for (int i = 0; i < size + 2; i++) {
                var col_def = new ColumnDefinition();
                col_def.Width = new GridLength(1, GridUnitType.Star);
                g.ColumnDefinitions.Add(col_def);
                var row_def = new RowDefinition();
                row_def.Height = new GridLength(1, GridUnitType.Star);
                g.RowDefinitions.Add(row_def);
            }
        }



        //// 
        //// Input Handling
        //// 

        // Attempted hack around winRT's internals stealing focus to hidden root ScrollViewer.
        //
        //protected override void OnNavigatedTo (NavigationEventArgs e) {
        //    MainWindow mainWin = e.Parameter as MainWindow;
        //    // These don't work because mainwin's frame is always null (here and in constructor)
        //    //mainWin.Frame.KeyDown += this.mainWin_keydown;
        //    //mainWin.Frame.AddHandler(UIElement.KeyDownEvent, (KeyEventHandler)this.mainWin_keydown, true);
        //}

        // Another attempted hack around for winRT stealing focus to hidden root ScrollViewer ...
        // OnLostFocus installed this handler on the ScrollViewer at the top of the UI tree to try to
        // keep focus down on the user control so that mainwin_keydown gets called.
        //
        // This doesn't work because an infinite loop occurs with the scrollviewer getting focus,
        // then the sf_GotFocus handler putting it back on stones, then scrollviewer getting focus again.
        //
        //void sv_GotFocus (object sender, RoutedEventArgs e) {
        //    this.FocusOnStones();
        //}
        //private bool hackDone = false;

        // This is what OnLostFocus printed when focus was appropriately down in my UI elements,
        // specifically when hitting esc with focus in the comment box.
        //
        //Lost focus. source: Windows.UI.Xaml.Controls.TextBox, new focus: SgfEdWin8.FocusableInputControl
        //SgfEdWin8.FocusableInputControl
        // => Windows.UI.Xaml.Controls.Grid
        // => SgfEdWin8.MainWindow
        // => Windows.UI.Xaml.Controls.ContentPresenter
        // => Windows.UI.Xaml.Controls.Border
        // => Windows.UI.Xaml.Controls.Frame
        // => Windows.UI.Xaml.Controls.Border
        // => Windows.UI.Xaml.Controls.ScrollContentPresenter
        // => Windows.UI.Xaml.Controls.Grid
        // => Windows.UI.Xaml.Controls.Border
        // => Windows.UI.Xaml.Controls.ScrollViewer
        //
        // This is what OnLostFocus printed when focus was snatched by internal winRT code.
        // Focus is all the way on hidden root UI element, so winmain_keydown never gets called,
        // even if installed manually with AddHandler to always get invoked, even for handled input.
        //
        //Lost focus. source: SgfEdWin8.FocusableInputControl, new focus: Windows.UI.Xaml.Controls.ScrollViewer
        //Windows.UI.Xaml.Controls.ScrollViewer

        //// OnLostFocus works around winRT bug that steals focus to a hidden root ScrollViewer,
        //// disabling MainWindow's mainwin_keydown handler.  Can't just call FocusOnStones because
        //// if commentBox gets focus, want it to be there, and it turns out all the command buttons
        //// quit working too since this somehow runs first, puts focus on stones, then button never
        //// gets click event.
        ////
        private ScrollViewer hiddenRootScroller = null;
        protected override void OnLostFocus (RoutedEventArgs e) {
            if (hiddenRootScroller == null) {
                var d = FocusManager.GetFocusedElement() as DependencyObject;
                // When new game dialog gets shown, d is null;
                if (d == null) return;
                while (d.GetType() != typeof(ScrollViewer)) {
                    d = VisualTreeHelper.GetParent(d);
                    if (d == null) return;
                }
                hiddenRootScroller = d as ScrollViewer;
            }
            if (FocusManager.GetFocusedElement() == hiddenRootScroller)
                this.FocusOnStones();

            // Diagnostic output that helped show FocusOnStones wasn't working because something
            // deep in winRT was stealing focus to a hidden root ScrollViewer, which mean mainwin_keydown
            // was never called.
            //
            //System.Diagnostics.Debug.WriteLine(string.Format("\n\nLost focus. source: {0}, new focus: {1}", e.OriginalSource,
            //                                                 FocusManager.GetFocusedElement()));
            //base.OnLostFocus(e);
            //DependencyObject d = FocusManager.GetFocusedElement() as DependencyObject;
            //string output = string.Empty;
            //while (d != null) {
            //    if (output.Length > 0) {
            //        output = string.Concat(output, "\n => ");
            //    }
            //    output += d.ToString();
            //    d = VisualTreeHelper.GetParent(d);
            //}
            //System.Diagnostics.Debug.WriteLine(output);

            // This was a hack around attempt to put focus handler on the root ScrollViewer that put focus on stones.
            // This doesn't work because an infinite loop occurs with the scrollviewer getting focus,
            // then the sf_GotFocus handler putting it back on stones, then scrollviewer getting focus again.
            //
            //if (!hackDone) {
            //    d = FocusManager.GetFocusedElement() as DependencyObject;
            //    while (d.GetType() != typeof(Windows.UI.Xaml.Controls.ScrollViewer)) {
            //        d = VisualTreeHelper.GetParent(d);
            //    }
            //    var sv = d as Windows.UI.Xaml.Controls.ScrollViewer;
            //    sv.GotFocus += sv_GotFocus;
            //    hackDone = true;
            //}
        }


        private void helpButtonLeftDown (object sender, RoutedEventArgs e) {
            this.ShowHelp();
        }

        //// This is a hack because win8 msgbox does not scroll, so will need to design a popup with a
        //// with a scrolling text box.
        ////
        private async void ShowHelp () {
            var helpDialog = new HelpDialog();
            var helpText = helpDialog.FindName("helpText") as TextBox;
            helpText.Text = "Note: text has scroll bar to view all help.\n\n" + MainWindow.HelpString;
            var popup = new Popup();
            helpDialog.HelpDialogClose += (s, e) => { 
                popup.IsOpen = false;
            };
            popup.Child = helpDialog;
            popup.IsOpen = true;
        }
        // This is previous hack due to win8 msgbox not scrolling.
        //
        //private bool showFirstHalfHelp = true;
        //private async void ShowHelp () {
        //    var howMuch = (MainWindow.HelpString.Length / 2) + 15;
        //    if (this.showFirstHalfHelp)
        //        await GameAux.Message("HACK: Hit OK and press F1 again to see alternating halves of help string.\n" +
        //                                  MainWindow.HelpString.Substring(0, howMuch),
        //                              "SGFEd Help");
        //    else
        //        await GameAux.Message(MainWindow.HelpString.Substring(howMuch), "SGFEd Help");
        //    this.showFirstHalfHelp = ! this.showFirstHalfHelp;
        //}


        //// StonesMouseLeftDown handles creating a move or adding adornments
        //// to the current move.
        ////
        private async void StonesPointerPressed (object sender, PointerRoutedEventArgs e) {
            var g = (Grid)sender;
            if (e.GetCurrentPoint(g).Properties.IsLeftButtonPressed) {
                var cell = MainWindowAux.GridPixelsToCell(g, e.GetCurrentPoint(g).Position.X, 
                                                          e.GetCurrentPoint(g).Position.Y);
                //MessageDialog.Show(cell.X.ToString() + ", " + cell.Y.ToString());
                // cell x,y is col, row from top left, and board is row, col.
                if (this.IsKeyPressed(VirtualKey.Shift))
                    await MainWindowAux.AddOrRemoveAdornment(this.stonesGrid, (int)cell.Y, (int)cell.X, 
                                                             AdornmentKind.Square, this.Game);
                else if (this.IsKeyPressed(VirtualKey.Control))
                    await MainWindowAux.AddOrRemoveAdornment(this.stonesGrid, (int)cell.Y, (int)cell.X, 
                                                             AdornmentKind.Triangle, this.Game);
                else if (this.IsKeyPressed(VirtualKey.Menu))
                    await MainWindowAux.AddOrRemoveAdornment(this.stonesGrid, (int)cell.Y, (int)cell.X, 
                                                             AdornmentKind.Letter, this.Game);
                else {
                    var move = await this.Game.MakeMove((int)cell.Y, (int)cell.X);
                    if (move != null)
                        this.AdvanceToStone(move);
                }
                this.FocusOnStones();
            }
        }

        //// passButton_left_down handles creating a passing move.  Also,
        //// mainwin_keydown calls this to handle c-p.
        ////
        private async void passButton_left_down (object sender, RoutedEventArgs e) {
            await GameAux.Message("Need to implement passing move.");
        }

        //// prevButton_left_down handles the rewind one move button.  Also,
        //// mainwin_keydown calls this to handle left arrow.  This also handles
        //// removing and restoring adornments, and handling the current move
        //// adornment.  This function assumes the game is started, and there's a
        //// move to rewind.
        ////
        public void prevButtonLeftDown (object self, RoutedEventArgs e) {
            var move = this.Game.UnwindMove();
            //remove_stone(main_win.FindName("stonesGrid"), move)
            if (!move.IsPass)
                MainWindowAux.RemoveStone(this.stonesGrid, move);
            if (move.Previous != null)
                this.AddCurrentAdornments(move.Previous);
            else
                MainWindowAux.RestoreAdornments(this.stonesGrid, this.Game.SetupAdornments);
            //self._update_tree_view(move.Previous);
            if (this.Game.CurrentMove != null) {
                var m = this.Game.CurrentMove;
                this.UpdateTreeView(m);
                this.UpdateTitle(m.Number, m.IsPass);
            }
            else {
                this.UpdateTreeView(null);
                this.UpdateTitle(0);
            }
            this.FocusOnStones();
        }

        //// nextButton_left_down handles the replay one move button.  Also,
        //// mainwin_keydown calls this to handle left arrow.  This also handles
        //// removing and restoring adornments, and handling the current move
        //// adornment.  This function assumes the game has started, and there's
        //// a next move to replay.
        ////
        public async void nextButtonLeftDown(object next_button, RoutedEventArgs e) {
            await DoNextButton();
        }
        // Need this to re-use this functionality where we need to use 'await'.  Can't
        // 'await' nextButtonLeftDown since it must be 'async void'.
        public async Task DoNextButton () {
            var move = this.Game.ReplayMove();
            if (move == null) {
                await GameAux.Message("Can't play branch further due to conflicting stones on the board.");
                return;
            }
            this.AdvanceToStone(move);
            this.UpdateTreeView(move);
            this.FocusOnStones();
        }

        //// homeButton_left_down rewinds all moves to the game start.  This
        //// function signals an error if the game has not started, or no move has
        //// been played.
        ////
        private void homeButtonLeftDown (object home_button, RoutedEventArgs e) {
            this.Game.GotoStart();
            this.UpdateTitle(0);
            this.UpdateTreeView(null);
            this.FocusOnStones();
        }

        //// endButton_left_down replays all moves to the game end, using currently
        //// selected branches in each move.  This function signals an error if the
        //// game has not started, or no move has been played.
        ////
        private async void endButtonLeftDown (object end_button, RoutedEventArgs e) {
            await this.Game.GotoLastMove();
            var cur = this.Game.CurrentMove;
            this.UpdateTitle(cur == null ? 0 : cur.Number);
            this.UpdateTreeView(cur);
            this.FocusOnStones();
        }

        //// brachCombo_SelectionChanged changes the active branch for the next move
        //// of the current move.  Updating_branch_combo is set in update_branch_combo
        //// so that we only update when the user has taken an action as opposed to
        //// programmatically changing the selected item due to arrow keys, deleting moves, etc.
        ////
        private void branchComboSelectionChanged(object branch_dropdown, SelectionChangedEventArgs e) {
            if (updating_branch_combo != true) {
                this.Game.SetCurrentBranch(((ComboBox)branch_dropdown).SelectedIndex);
                this.FocusOnStones();
            }
        }


        //// _advance_to_stone displays move, which as already been added to the
        //// board and readied for rendering.  We add the stone with no current
        //// adornment and then immediately add the adornment because some places in
        //// the code need to just add the stone to the display with no adornments.
        ////
        private void AdvanceToStone (Move move) {
            this.AddNextStoneNoCurrent(move);
            this.AddCurrentAdornments(move);
            this.UpdateTitle(move.Number, move.IsPass);
        }

        //// add_next_stone_no_current adds a stone to the stones grid for move,
        //// removes previous moves current move marker, and removes previous moves
        //// adornments.  This is used for advancing to a stone for replay move or
        //// click to place stone (which sometimes replays a move and needs to place
        //// adornments for pre-existing moves).  The Game class also uses this.
        ////
        public void AddNextStoneNoCurrent (Move move) {
            if (!move.IsPass)
                //add_stone(main_win.FindName("stonesGrid"), m.row, m.column, m.color)
                MainWindowAux.AddStone(this.stonesGrid, move.Row, move.Column, move.Color);
            // Must remove current adornment before adding it elsewhere.
            if (move.Previous != null) {
                if (move.Previous.Adornments.Contains(Adornments.CurrentMoveAdornment))
                    MainWindowAux.RemoveCurrentStoneAdornment(this.stonesGrid, move.Previous);
                MainWindowAux.RemoveAdornments(this.stonesGrid, move.Previous.Adornments);
            }
            else
                MainWindowAux.RemoveAdornments(this.stonesGrid, this.Game.SetupAdornments);
        }

        //// add_current_adornments adds to the stones grid a current move marker
        //// for move as well as move's adornments.  This is used for replay move UI
        //// in this module as well as by code in the Game class.
        ////
        public void AddCurrentAdornments (Move move) {
            // Must restore adornemnts before adding current, else error adding
            // current twice.
            MainWindowAux.RestoreAdornments(this.stonesGrid, move.Adornments);
            if (!move.IsPass)
                MainWindowAux.AddCurrentStoneAdornment(this.stonesGrid, move);
        }

        //// update_title sets the main window title to display the open
        //// file and current move number.  "Move " is always in the title, set
        //// there by default originally.  Game also uses this.
        ////
        public void UpdateTitle (int num, bool is_pass = false, string filebase = null) {
            var title = this.Title.Text;
            var pass_str = is_pass ? " Pass" : "";
            if (filebase != null)
                this.Title.Text = "SGFEd -- " + filebase + ";  Move " + num.ToString() + pass_str;
            else {
                var tail = title.IndexOf("Move ");
                Debug.Assert(tail != -1, "Title doesn't have move in it?!");
                if (tail != -1)
                    this.Title.Text = title.Substring(0, tail + 5) + num.ToString() + pass_str;
            }
        }


        //// openButton_left_down prompts to save current game if dirty and then
        //// prompts for a .sgf file to open.
        ////
        private async void openButton_left_down(object open_button, RoutedEventArgs e) {
            await DoOpenButton();
        }
        // Need this to re-use this functionality where we need to use 'await'.  Can't
        // 'await' openButtonLeftDown since it must be 'async void'.
        private async Task DoOpenButton () {
            await this.CheckDirtySave();
            var fp = new FileOpenPicker();
            fp.FileTypeFilter.Add(".sgf");
            fp.FileTypeFilter.Add(".txt");
            // This randomly throws HRESULT: 0x80070005 (E_ACCESSDENIED))
            // UnauthorizedAccessException, seems to be when comment box has focus.
            StorageFile sf;
            try {
                sf = await fp.PickSingleFileAsync();
            }
            catch (UnauthorizedAccessException einfo) {
                GameAux.Message(einfo.Message);
                return;
            }
            if (sf != null) {
                Popup popup = null;
                try {
                    // fixing win8 -- set flag to disable UI due to await on parsing
                    popup = new Popup();
                    popup.Child = this.GetOpenfileUIDike();
                    popup.IsOpen = true;
                    // Process file ...
                    var pg = await ParserAux.ParseFile(sf);
                    this.Game = await GameAux.CreateParsedGame(pg, this);
                    //await Task.Delay(5000);  // Testing input blocker.
                    this.Game.Storage = sf;
                    var name = sf.Name;
                    this.Game.Filename = name;
                    this.Game.Filebase = name.Substring(name.LastIndexOf('\\') + 1);
                    this.UpdateTitle(0, false, this.Game.Filebase);
                }
                catch (IOException err) {
                    // Essentially handles unexpected EOF or malformed property values.
                    GameAux.Message(err.Message + err.StackTrace);
                }
                catch (Exception err) {
                    // No code paths should throw from incomplete, unrecoverable state, so should be fine to continue.
                    // For example, game state should be intact (other than IsDirty) for continuing.
                    GameAux.Message(err.Message + err.StackTrace);
                }
                finally {
                    popup.IsOpen = false;
                }
            }
            this.FocusOnStones();
        }

        private Grid openFileUIDike = null;
        private Grid GetOpenfileUIDike () {
            if (this.openFileUIDike != null)
                return this.openFileUIDike;
            else {
                // Need outer UIElement to stretch over whole screen.
                var g = new Grid();
                // Need inner UIElement that can have focus, take input, etc.
                var tb = new TextBox();
                // For some reason NewGameDialog can have huge margins and dike input over entire screen,
                // but this must span entire screen.
                tb.Margin = new Thickness(1, 1, 1, 1);
                tb.Background = new SolidColorBrush(Colors.Black);
                tb.Opacity = 0.25;
                var bounds = Window.Current.Bounds;
                g.Width = bounds.Width;
                g.Height = bounds.Height;
                g.Children.Add(tb);
                this.openFileUIDike = g;
                return g;
            }
        }

        //// _check_dirty_save prompts whether to save the game if it is dirty.  If
        //// saving, then it uses the game filename, or prompts for one if it is None.
        ////
        private async Task CheckDirtySave () {
            this.Game.SaveCurrentComment();
            if (this.Game.Dirty &&
                    await GameAux.Message("Game is unsaved, save it?", "Confirm saving file",
                                    new List<string>() {"Yes", "No"}) == GameAux.YesMessage) {
                StorageFile sf;
                if (this.Game.Storage != null) {
                    sf = this.Game.Storage;
                }
                else {
                    sf = await MainWindowAux.GetSaveFilename();
                }
                if (sf != null)
                    await this.Game.WriteGame(sf);
            }
        }


        //// newButton_left_down starts a new game after checking to save the
        //// current game if it is dirty.
        ////
        private async void newButton_left_down(object new_button, RoutedEventArgs e) {
            await DoNewButton();
        }

        ////// Needed this when trying to navigate to the new dialog as a page.
        ////// That didn't work since win8 tears down the main page, and it
        ////// required crufty hacks to coordinate and synchronize with MianWindow.
        //////
        //public bool NewDiaogDone = false;
        //// white name, black name, size, handicap, komi ...
        //public Tuple<string, string, string, string, string> NewDialogInfo = null;

        //// DoNewButton can be re-used where we need to use 'await'.  Can't 'await'
        //// newButtonLeftDown since it must be 'async void' due to being an event handler.
        ////
        private async Task DoNewButton () {
            // Hack while seplunking how to build a popup dialog in win8.
            //await GameAux.Message("For now, create an .sgf file with this line and use the Open command:\n" +
            //                      "    ;GM[1]FF[4]SZ[19]KM[6.5]PW[WhiteName]PB[BlackName])\n" +
            //                      "For handicaps, change KM to 0.0 and add stones before the close paren:\n" +
            //                      "   2: AB[pd][dp]\n" +
            //                      "   3: AB[pd][dp][pp]\n" +
            //                      "   4: AB[pd][dp][pp][dd]\n" +
            //                      "   5: AB[pd][dp][pp][dd][jj]" +
            //                      "   6: AB[pd][dp][pp][dd][dj][pj]");
            await this.CheckDirtySave();
            var newDialog = new NewGameDialog();
            var popup = new Popup();
            newDialog.NewGameDialogClose += (s, e) => { 
                popup.IsOpen = false;
                this.NewGameDialogDone(newDialog); 
            };
            popup.Child = newDialog;
            popup.IsOpen = true;
            // This was used when tried to navigate to new dialog as a page, but win8 tears
            // down MainWindow when we navigated to the dialog, requiring jumping through multiple hoops.
            //this.Frame.Navigate(typeof(NewGameDialog), this);
            //while (! this.NewDiaogDone)
            //    await Task.Delay(500);
            //if (this.NewDialogInfo != null) {
            //    NewGameDialogDone();
            //}
        }

        //// NewGameDialogDone handles when the new game dialog popup is done.
        //// It checks whether the dialog was confirmed or cancelled, and takes
        //// appropriate action.
        ////
        private void NewGameDialogDone (NewGameDialog dlg) {
            if (dlg.NewGameConfirmed) {
                var white = dlg.WhiteText;
                var black = dlg.BlackText;
                var size = dlg.SizeText;
                var handicap = dlg.HandicapText;
                var komi = dlg.KomiText;
                //var white = this.NewDialogInfo.Item1;
                //var black = this.NewDialogInfo.Item2;
                //var size = this.NewDialogInfo.Item3;
                //var handicap = this.NewDialogInfo.Item4;
                //var komi = this.NewDialogInfo.Item5;
                var g = new Game(this, int.Parse(size), int.Parse(handicap), komi);
                if (black != "")
                    g.PlayerBlack = black;
                if (white != "")
                    g.PlayerWhite = white;
                this.Game = g;
                this.UpdateTitle(0, false, "unsaved");
            }
        }


        //// saveButton_left_down saves if game has a file name and is dirty.  If
        //// there's a filename, but the file is up to date, then ask to save-as to
        //// a new name.  Kind of lame to not have explicit save-as button, but work
        //// for now.
        ////
        private async void saveButton_left_down(object save_button, RoutedEventArgs e) {
            await DoSaveButton();
        }
        // Need this to re-use this functionality where we need to use 'await'.  Can't
        // 'await' saveButtonLeftDown since it must be 'async void'.
        private async Task DoSaveButton () {
            if (this.Game.Filename != null) {
                // See if UI has comment edits and persist to model.
                this.Game.SaveCurrentComment();
                if (this.Game.Dirty)
                    await this.Game.WriteGame();
                else if (await GameAux.Message("Game is already saved.  " +
                                               "Do you want to save it to a new name?",
                                               "Confirm save-as", new List<string>() { "Yes", "No" }) ==
                         GameAux.YesMessage)
                    await this.SaveAs();
            }
            else
                await this.SaveAs();
        }

        private async Task SaveAs () {
            var sf = await MainWindowAux.GetSaveFilename();
            if (sf != null) {
                this.Game.SaveCurrentComment(); // Persist UI edits to model.
                await this.Game.WriteGame(sf);
            }
        }


        //// Looks like win8 is old school and have to manage state of key presses.
        //// These fields and mainWin_keyup manage whether modifier keys are down
        //// when processing keyboard or mouse input.
        ////
        //// ACTUALLY, SEE mainWin_keyup, figured out how to check withut managing state.
        ////
        //private bool CtrlKeyPressed = false;
        //private bool ShiftKeyPressed = false;
        //private bool AltKeyPressed = false;

        //private void mainWin_keyup (object sender, KeyRoutedEventArgs e) {
        //    if (e.Key == VirtualKey.Control) this.CtrlKeyPressed = false;
        //    if (e.Key == VirtualKey.Shift) this.ShiftKeyPressed = false;
        //    if (e.Key == VirtualKey.Menu) this.AltKeyPressed = false;
        //}

        //// IsKeyPressed takes a modifier key and determins if it is down during input handling.
        ////
        private bool IsKeyPressed (VirtualKey key) {
            var x = (CoreWindow.GetForCurrentThread().GetKeyState(key) & CoreVirtualKeyStates.Down) != 0;
            return x;
        }

        //// mainWin_keydown dispatches arrow keys for rewinding, replaying, and
        //// choosing branches.  These events always come, catching arrows,
        //// modifiers, etc.  However, when a TextBox has focus, it gets arrow keys
        //// first, so we need to support <escape> to allow user to put focus back
        //// on the stones grid.  We also pick off up and down arrow for branch
        //// selection, working with update_branch_combo to ensure stones grid keeps
        //// focus.
        ////
        private async void mainWin_keydown (object sender, KeyRoutedEventArgs e) {
            // Diagnostics used to figure out winRT's internals were stealing focus to root ScrollViewer.
            //System.Diagnostics.Debug.WriteLine("Keyboard from " + sender + ", source: " + e.OriginalSource);

            // Turns out don't need to manage state of modifier keys.  See IsKeyPressed.
            //if (e.Key == VirtualKey.Control) {
            //    this.CtrlKeyPressed = true;
            //    return;
            //} else if (e.Key == VirtualKey.Shift) {
            //    this.ShiftKeyPressed = true;
            //    return;
            //} else if (e.Key == VirtualKey.Menu) {
            //    this.AltKeyPressed = true;
            //    return;
            //}
            MainWindow win;
            if (sender.GetType() == typeof(MainWindow))
                win = (MainWindow)sender;
            else
                win = this;
            //var win = (MainWindow)sender;
            if (e.Key == VirtualKey.Escape) {
                win.FocusOnStones();
                e.Handled = true;
                return;
            }
            // Previous move
            if (e.Key == VirtualKey.Left && (this.commentBox.FocusState != FocusState.Keyboard && win.Game.CanUnwindMove())) {
                this.prevButtonLeftDown(null, null);
                e.Handled = true;
            }
            // Next move
            else if (e.Key == VirtualKey.Right) {
                if (this.commentBox.FocusState == FocusState.Keyboard)
                    return;
                if (win.Game.CanReplayMove())
                    await this.DoNextButton();
                e.Handled = true;
            }
            // Initial board state
            else if (e.Key == VirtualKey.Home && (this.commentBox.FocusState != FocusState.Keyboard) && win.Game.CanUnwindMove()) {
                this.homeButtonLeftDown(null, null);
                e.Handled = true;
            }
            // Last move
            else if (e.Key == VirtualKey.End && (this.commentBox.FocusState != FocusState.Keyboard) && win.Game.CanReplayMove()) {
                this.endButtonLeftDown(null, null);
                e.Handled = true;
            }
            // Move branch down
            else if (e.Key == VirtualKey.Down && this.IsKeyPressed(VirtualKey.Control) &&
                     (this.commentBox.FocusState != FocusState.Keyboard)) {
                await this.Game.MoveBranchDown();
                e.Handled = true;
            }
            // Move branch up
            else if (e.Key == VirtualKey.Up && this.IsKeyPressed(VirtualKey.Control) &&
                     (this.commentBox.FocusState != FocusState.Keyboard)) {
                await this.Game.MoveBranchUp();
                e.Handled = true;
            }
            // Display next branch
            else if (e.Key == VirtualKey.Down && this.branchCombo.Items.Count > 0 &&
                     (this.commentBox.FocusState != FocusState.Keyboard)) {
                MainWindowAux.SetCurrentBranchDown(this.branchCombo, this.Game);
                e.Handled = true;
            }
            // Display previous branch
            else if (e.Key == VirtualKey.Up && this.branchCombo.Items.Count > 0 &&
                     (this.commentBox.FocusState != FocusState.Keyboard)) {
                MainWindowAux.SetCurrentBranchUp(this.branchCombo, this.Game);
                e.Handled = true;
            }
            // Opening a file
            else if (e.Key == VirtualKey.O && //this.CtrlKeyPressed) 
                     this.IsKeyPressed(VirtualKey.Control)) {
                await this.DoOpenButton();
                e.Handled = true;
            }
            // Testing Game Tree Layout
            else if (e.Key == VirtualKey.T && this.IsKeyPressed(VirtualKey.Control)) {
                this.DrawGameTree();
                e.Handled = true;
            }
            // Explicit Save As
            else if (e.Key == VirtualKey.S && 
                     this.IsKeyPressed(VirtualKey.Control) && this.IsKeyPressed(VirtualKey.Menu)) {
                await this.SaveAs();
                this.FocusOnStones();
                e.Handled = true;
            }
            // Saving
            else if (e.Key == VirtualKey.S && this.IsKeyPressed(VirtualKey.Control)) {
                await this.DoSaveButton();
                this.FocusOnStones();
                e.Handled = true;
            }
            // Save flipped game for opponent's view
            else if (e.Key == VirtualKey.F &&
                     this.IsKeyPressed(VirtualKey.Control) && this.IsKeyPressed(VirtualKey.Menu) &&
                     this.commentBox.FocusState != FocusState.Keyboard) {
                var sf = await MainWindowAux.GetSaveFilename("Save Flipped File");
                if (sf != null)
                    await this.Game.WriteFlippedGame(sf);
                this.FocusOnStones();
                e.Handled = true;
            }
            // New file
            else if (e.Key == VirtualKey.N && this.IsKeyPressed(VirtualKey.Control)) {
                await this.DoNewButton();
                e.Handled = true;
            }
            // Cutting a sub tree
            else if ((e.Key == VirtualKey.Delete || (e.Key == VirtualKey.X && this.IsKeyPressed(VirtualKey.Control))) &&
                     (this.commentBox.FocusState != FocusState.Keyboard) &&
                     win.Game.CanUnwindMove() &&
                     await GameAux.Message("Cut current move from game tree?",
                                           "Confirm cutting move", new List<string>() {"Yes", "No"}) ==
                         GameAux.YesMessage) {
                win.Game.CutMove();
                e.Handled = true;
            }
            // Pasting a sub tree
            else if (e.Key == VirtualKey.V && this.IsKeyPressed(VirtualKey.Control) &&
                     this.commentBox.FocusState != FocusState.Keyboard) {
                if (win.Game.CanPaste())
                    await win.Game.PasteMove();
                else
                    await GameAux.Message("No cut move to paste at this time.");
                e.Handled = true;
            }
            // Help
            else if (e.Key == VirtualKey.F1) {
                this.ShowHelp();
                e.Handled = true;
            }
        } // mainWin_keydown


        //// gameTree_mousedown handles clicks on the game tree graph canvas,
        //// navigating to the move clicked on.
        ////
        private void gameTree_mousedown (object sender, PointerRoutedEventArgs e) {
            // find TreeViewNode model for click locatio on canvas.
            var x = e.GetCurrentPoint(this.gameTreeView).Position.X;
            var y = e.GetCurrentPoint(this.gameTreeView).Position.Y;
            var elt_x = (int)(x / MainWindowAux.treeViewGridCellSize);
            var elt_y = (int)(y / MainWindowAux.treeViewGridCellSize);
            TreeViewNode n = null;
            var found = false;
            foreach (var moveNode in this.treeViewMoveMap) {
                n = moveNode.Value;
                if (n.Row == elt_y && n.Column == elt_x) {
                    found = true;
                    break;
                }
            }
            if (! found) return; // User did not click on node.
            // Reset board before advancing to move.
            var move = n.Node as Move;
            if (this.Game.CurrentMove != null)
                this.Game.GotoStart();
            if (move != null) {
                if (move.Row != -1 && move.Column != -1)
                    // move is NOT dummy move for start node of game tree view, so advance to it.
                    this.GotoGameTreeMove(move);
            }
            else
                // Move is ParsedNode, not a move that's been rendered.
                this.GotoGameTreeMove((ParsedNode)n.Node);
            this.UpdateTitle(this.Game.CurrentMove == null ? 0 : this.Game.CurrentMove.Number);
            this.UpdateTreeView(this.Game.CurrentMove);
            this.FocusOnStones();
        }

        private void GotoGameTreeMove (Move move) {
            var path = this.Game.GetPathToMove(move);
            if (path != this.Game.TheEmptyMovePath) {
                this.Game.AdvanceToMovePath(path);
                GotoGameTreeMoveUpdateButtons(move);
            }
        }

        private void GotoGameTreeMove (ParsedNode move) {
            var path = this.Game.GetPathToMove(move);
            if (path != this.Game.TheEmptyMovePath) {
                this.Game.AdvanceToMovePath(path);
                this.GotoGameTreeMoveUpdateButtons(this.Game.CurrentMove);
            }
        }

        private void GotoGameTreeMoveUpdateButtons (Move move) {
            // All other game and UI state has been updated by Game.AdvanceToMovePath.
            this.AddCurrentAdornments(move);
            if (move.Previous != null)
                this.EnableBackwardButtons();
            else
                this.DisableBackwardButtons();
            if (move.Next != null) {
                this.EnableForwardButtons();
                this.UpdateBranchCombo(move.Branches, move.Next);
            }
            else {
                this.DisableForwardButtons();
                this.UpdateBranchCombo(null, null);
            }
        }


        ////
        //// Tree View of Game Tree
        ////

        //// treeViewMoveMap maps Moves and ParsedNodes to TreeViewNodes.  This aids in moving tree
        //// view to show certain moves, moving the current move highlight, etc.  We could put a cookie
        //// on Move and ParsedNode, but that feels too much like the model knowing about the view ...
        //// yeah, Adornments have a cookie, but oh well, and they are arguably viewmodel :-).
        ////
        private Dictionary<object, TreeViewNode> treeViewMoveMap = new Dictionary<object, TreeViewNode>();
        private Grid treeViewSelectedItem;

        //// InitializeTreeView is used by SetupBoardDisplay.  It clears the canvas and sets its size.
        ////
        private void InitializeTreeView () {
            var canvas = this.gameTreeView;
            canvas.Children.Clear(); //.RemoveRange(0, canvas.Children.Count);
            this.treeViewMoveMap.Clear();
            this.SetTreeViewSize();
        }

        private void SetTreeViewSize () {
            var canvas = this.gameTreeView;
            canvas.Width = GameAux.TreeViewGridColumns * MainWindowAux.treeViewGridCellSize;
            canvas.Height = GameAux.TreeViewGridRows * MainWindowAux.treeViewGridCellSize;
        }


        //// DrawGameTree gets a view model of the game tree, creates objects to put on the canvas,
        //// and sets up the mappings for updating the view as we move around the game tree.  This
        //// also creates a special "start" mapping to get to the first view model node.
        ////
        private void DrawGameTree () {
            if (this.TreeViewDisplayed())
                //TODO: this is temporary, should re-use objects from this.treeViewMoveMap
                this.InitializeTreeView();
            var treeModel = GameAux.GetGameTreeModel(this.Game);
            // Set canvas size in case computing tree model had to grow model structures.
            this.SetTreeViewSize();
            var canvas = this.gameTreeView;
            for (var i = 0; i < GameAux.TreeViewGridRows; i++) {
                for (var j = 0; j < GameAux.TreeViewGridColumns; j++) {
                    var curModel = treeModel[i, j];
                    if (curModel != null) {
                        //TODO: get previous grid from uncleared tree view map copy and update it
                        // appropriately.  Also, probably want some flag or control that if did cut/paste
                        // operation, then remove objects, handle renumbering of labels in grids, etc.
                        // probably just toss whole tree on paste and re-gen for simplicity.
                        //if (this.treeViewMoveMap.ContainsKey(curModel.Node)
                        //    eltGrid = ((TreeViewNode)this.treeViewMoveMap[curModel.Node]).Cookie;
                        if (curModel.Kind != TreeViewNodeKind.LineBend) {
                            var eltGrid = MainWindowAux.NewTreeViewItemGrid(curModel);
                            curModel.Cookie = eltGrid;
                            var node = curModel.Node;
                            //if (node != null)
                            //    // If node is null, then it is a line bend node.
                            this.treeViewMoveMap[curModel.Node] = curModel;
                            Canvas.SetLeft(eltGrid, curModel.Column * MainWindowAux.treeViewGridCellSize);
                            Canvas.SetTop(eltGrid, curModel.Row * MainWindowAux.treeViewGridCellSize);
                            Canvas.SetZIndex(eltGrid, 1);
                            canvas.Children.Add(eltGrid);
                        }
                    }
                }
            }
            this.treeViewMoveMap.Remove(treeModel[0, 0].Node);
            this.treeViewMoveMap["start"] = treeModel[0, 0];
            MainWindowAux.DrawGameTreeLines(canvas, treeModel[0, 0]);
            Grid cookie = (Grid)treeModel[0, 0].Cookie;
            //cookie.Background = new SolidColorBrush(Colors.LightSkyBlue);
            // Set this to something so that UpdateTreeView doesn't deref null.
            this.treeViewSelectedItem = cookie;
            this.UpdateTreeView(this.Game.CurrentMove);
        }


        //// UpdateTreeView moves the current move highlighting as the user moves around in the
        //// tree.  Various command handlers call this after they update the model.  Eventually,
        //// this will look at some indication to redraw whole tree (cut, paste, maybe add move).
        ////
        private void UpdateTreeView (Move move) {
            // TODO: remove this check when fully integrated and assume tree view is always there.
            if (! this.TreeViewDisplayed())
                return;
            TreeViewNode item = this.TreeViewNodeForMove(move);
            Grid itemCookie = ((Grid)item.Cookie);
            // Update current move shading and bring into view.
            var sitem = this.treeViewSelectedItem;
            this.treeViewSelectedItem = itemCookie;
            sitem.Background = new SolidColorBrush(Colors.Transparent);
            itemCookie.Background = new SolidColorBrush(Colors.LightSkyBlue);
            this.BringTreeElementIntoView(itemCookie);
            //itemCookie.BringIntoView(new Rect((new Size(MainWindowAux.treeViewGridCellSize * 2,
            //                                            MainWindowAux.treeViewGridCellSize * 2))));
        }

        private const int BringIntoViewPadding = 10;
        private void BringTreeElementIntoView (Grid g) {
            var parent = VisualTreeHelper.GetParent(g);
            while (parent != null) {
                parent = VisualTreeHelper.GetParent(parent);
                var sv = parent as ScrollViewer;
                if (sv != null) {
                    var transform = g.TransformToVisual(sv);
                    var pos = transform.TransformPoint(new Point(0, 0));
                    if (pos.Y < 0 || pos.Y > sv.ViewportHeight)
                        sv.ScrollToVerticalOffset(sv.VerticalOffset + pos.Y - MainWindow.BringIntoViewPadding);
                    if (pos.X < 0 || pos.X > sv.ViewportWidth)
                        sv.ScrollToHorizontalOffset(sv.VerticalOffset + pos.X - MainWindow.BringIntoViewPadding);
                    break;
                }
            }
        }

        //// TreeViewDipslayed returns whether there is a tree view displayed, abstracting the
        //// somewhat informal way we determine this state.  Eventually, the tree view will always
        //// be there and initialized or up to date.
        ////
        private bool TreeViewDisplayed () {
            return this.treeViewMoveMap.ContainsKey("start");
        }

        //// TreeViewNodeForMove returns the TreeViewNode representing the view model for move.
        //// Eventually this will handle having to add moves or redraw whole tree due to big
        //// operations.
        ////
        private TreeViewNode TreeViewNodeForMove (Move move) {
            if (move == null)
                return this.treeViewMoveMap["start"];
            else if (this.treeViewMoveMap.ContainsKey(move))
                return this.treeViewMoveMap[move];
            else if (move.ParsedNode != null && this.treeViewMoveMap.ContainsKey(move.ParsedNode)) {
                // Replace parsed node mapping with move mapping.  Once rendered, the move is what
                // we track, where new moves get hung, etc.
                var node = this.treeViewMoveMap[move.ParsedNode];
                this.treeViewMoveMap.Remove(move.ParsedNode);
                this.treeViewMoveMap[move] = node;
                return node;
            }
            else
                // TODO: figure out what really to do here.
                return this.treeViewMoveMap[move] = this.NewTreeViewNode(move);
        }

        private TreeViewNode NewTreeViewNode (Move move) {
            return this.treeViewMoveMap["start"];
            //throw new NotImplementedException();
        }

        
        ////
        //// Utilities
        ////

        //// add_stones adds Ellispses to the stones grid for each move.  Moves must
        //// be non-null.  Game uses this for replacing captures.
        ////
        public void AddStones (List<Move> moves) {
            //g = self.FindName("stonesGrid")
            var g = this.stonesGrid;
            foreach (var m in moves)
                MainWindowAux.AddStone(g, m.Row, m.Column, m.Color);
        }

        //// remove_stones removes the Ellipses from the stones grid.  Moves must be
        //// non-null.  Game uses this to remove captures.
        ////
        public void RemoveStones (List<Move> moves) {
            //g = self.FindName("stonesGrid") #not needed with wpf.LoadComponent
            var g = this.stonesGrid;
            foreach (var m in moves)
                MainWindowAux.RemoveStone(g, m);
        }

        //// reset_to_start resets the board UI back to the start of the game before
        //// any moves have been played.  Handicap stones will be displayed.  Game
        //// uses this after resetting the model.
        ////
        public void ResetToStart (Move cur_move) {
            var g = this.stonesGrid;
            // Must remove current adornment before other adornments.
            if (! cur_move.IsPass)
                MainWindowAux.RemoveCurrentStoneAdornment(g, cur_move);
            MainWindowAux.RemoveAdornments(g, cur_move.Adornments);
            var size = this.Game.Board.Size;
            for (var row = 0; row < size; row++)
                for (var col = 0; col < size; col++) {
                    var stone = MainWindowAux.stones[row, col];
                    if (stone != null)
                        g.Children.Remove(stone);
                    MainWindowAux.stones[row, col] = null;
                }
            this.AddHandicapStones(this.Game);
            this.commentBox.Text = this.Game.Comments;
        }


        //// AddHandicapStones takes a game and adds its handicap moves to the
        //// display.  This takes a game because it is used on new games when
        //// setting up an initial display and when resetting to the start of this.Game.
        //// This optionally takes a grid to use instead of the main this.stonesGrid,
        //// and when we're adding stones to a one-off grid, then we do not map the
        //// added stones and screw up the main displays mapping of stone Ellispes.
        ////
        private void AddHandicapStones (Game game, Grid g = null) {
            if (game.HandicapMoves != null) {
                foreach (var elt in game.HandicapMoves)
                    if (g == null)
                        MainWindowAux.AddStone(this.stonesGrid, elt.Row, elt.Column, elt.Color);
                    else
                        MainWindowAux.AddStoneNoMapping(g, elt.Row, elt.Column, elt.Color);
            }
        }

        //// update_branch_combo takes the current branches and the next move,
        //// then sets the branches combo to the right state with the branch
        //// IDs and selecting the one for next_move.  This does not take the
        //// move instead of the branches because the inital board state is not
        //// represented by a bogus move object.  Game uses this function from
        //// several tree manipulation functions.  We need
        //// updating_branch_combo so that branchCombo_SelectionChanged only
        //// takes action when the user changes the dropdown, as opposed to
        //// using arrow keys, deleting moves, etc.
        ////
        private Brush branch_combo_background = null;
        private bool updating_branch_combo = false;
        public void UpdateBranchCombo (List<Move> branches, Move next_move) {
            var combo = this.branchCombo;
            if (this.branch_combo_background == null)
                this.branch_combo_background = combo.Background;
            try {
                updating_branch_combo = true;
                combo.Items.Clear();
                if (branches != null) {
                    this.branchLabel.Text = branches.Count.ToString() + " branches:";
                    combo.IsEnabled = true;
                    combo.Background = new SolidColorBrush(Colors.Yellow);
                    combo.Items.Add("main");
                    for (var i = 2; i < branches.Count + 1; i++)
                        combo.Items.Add(i.ToString());
                    combo.SelectedIndex = branches.IndexOf(next_move);
                }
                else {
                    this.branchLabel.Text = "No branches:";
                    combo.IsEnabled = false;
                    combo.Background = this.branch_combo_background;
                }
                this.FocusOnStones();
            }
            finally {
                updating_branch_combo = false;
            }
        }

        //// _focus_on_stones ensures the stones grid has focus so that
        //// mainwin_keydown works as expected.  Not sure why event handler is on
        //// the main window, and the main window is focusable, but we have to set
        //// focus to the stones grid to yank it away from the branches combo and
        //// textbox.
        ////
        private void FocusOnStones () {
            this.inputFocus.IsEnabled = true;
            this.inputFocus.IsTabStop = true;
            this.inputFocus.IsHitTestVisible = true;
            this.inputFocus.Focus(FocusState.Pointer);
            this.inputFocus.Focus(FocusState.Keyboard);
        }

        //// add_unrendered_adornment setups all the UI objects to render the
        //// adornment a, but it passes False to stop actually putting the adornment
        //// in the stones grid for display.  These need to be added in the right
        //// order, so this just sets up the adornment so that when the move that
        //// triggers it gets displayed, the adornment is ready to replay.  Game uses this.
        ////
        public void AddUnrenderedAdornments (Adornments a) {
            MainWindowAux.AddNewAdornment(this.stonesGrid, a, this.Game, false);
        }


        public void EnableBackwardButtons () {
            this.prevButton.IsEnabled = true;
            this.homeButton.IsEnabled = true;
        }

        public void EnableForwardButtons () {
            this.nextButton.IsEnabled = true;
            this.endButton.IsEnabled = true;
        }

        public void DisableBackwardButtons () {
            this.prevButton.IsEnabled = false;
            this.homeButton.IsEnabled = false;
        }

        public void DisableForwardButtons () {
            this.nextButton.IsEnabled = false;
            this.endButton.IsEnabled = false;
        }

        public string CurrentComment {
            get { return this.commentBox.Text; }
            set { this.commentBox.Text = value; }
        }

    } // class MainWindow



    //// MainWindowAux provides "stateless" helpers for MainWindow.  This class is internal, but
    //// only MainWindow uses it.
    ////
    internal static class MainWindowAux {

        ////
        //// Setup Lines Grid Utilities
        ////

        /// _define_lines_columns defines the columns needed to draw lines for the go
        /// board.  It takes the Grid to modify and the size of the go board.  The
        /// columns would be all uniform except that the first non-border column must
        /// be split so that lines can be placed to make them meet cleanly in the
        /// corners rather than making little spurs outside the board from the lines
        /// spanning the full width of the first non-border column (if it were uniform width).
        ///
        internal static void DefineLinesColumns (Grid g, int size) {
            g.ColumnDefinitions.Add(def_col(2)); // border space
            g.ColumnDefinitions.Add(def_col(1)); // split first row so line ends in middle of cell
            g.ColumnDefinitions.Add(def_col(1)); // split first row so line ends in middle of cell
            for (int i = 0; i < size - 2; i++) {
                g.ColumnDefinitions.Add(def_col(2));
            }
            g.ColumnDefinitions.Add(def_col(1)); // split last row so line ends in middle of cell
            g.ColumnDefinitions.Add(def_col(1)); // split last row so line ends in middle of cell
            g.ColumnDefinitions.Add(def_col(2)); // border space
        }

        //// _def_col defines a proportional column.  It uses the relative
        //// size/proportion spec passed in.  The lines grid construction and adornments
        //// grids use this.
        ////
        private static ColumnDefinition def_col (int proportion) {
            var col_def = new ColumnDefinition();
            col_def.Width = new GridLength(proportion, GridUnitType.Star);
            return col_def;
        }


        /// _define_lines_rows defines the rows needed to draw lines for the go board.
        /// It takes the Grid to modify and the size of the go board.  The rows would
        /// be all uniform except that the first non-border row must be split so that
        /// lines can be placed to make them meet cleanly in the corners rather than
        /// making little spurs outside the board from the lines spanning the full
        /// height of the first non-border row (if it were uniform height).
        ///
        internal static void DefineLinesRows (Grid g, int size) {
            g.RowDefinitions.Add(def_row(2)); // border space
            g.RowDefinitions.Add(def_row(1)); // split first column so line ends in middle of cell
            g.RowDefinitions.Add(def_row(1)); // split first column so line ends in middle of cell
            for (int i = 0; i < size - 2; i++) {
                g.RowDefinitions.Add(def_row(2));
            }
            g.RowDefinitions.Add(def_row(1)); // split last column so line ends in middle of cell
            g.RowDefinitions.Add(def_row(1)); // split last column so line ends in middle of cell
            g.RowDefinitions.Add(def_row(2)); // border space
        }

        //// _def_row defines a proportional row.  It uses the relative
        //// size/proportion spec passed in.  The lines grid construction and adornments
        //// grids use this.
        ////
        private static RowDefinition def_row (int proportion) {
            var row_def = new RowDefinition();
            row_def.Height = new GridLength(proportion, GridUnitType.Star);
            return row_def;
        }

        /// _place_lines adds Line elements to the supplied grid to form the go board.
        /// Size is the number of lines as well as the number of row/columns to span.
        /// The outside lines are placed specially in the inner of two rows and columns
        /// that are half the space of the other rows and columns to get the lines to
        /// meet cleanly in the corners.  Without the special split rows and columns,
        /// the lines cross and leave little spurs in the corners of the board.
        ///
        internal static void PlaceLines (Grid g, int size) {
            g.Children.Add(def_hline(2, size, VerticalAlignment.Top));
            g.Children.Add(def_vline(2, size, HorizontalAlignment.Left));
            for (int i = 3; i < size + 1; i++) {
                g.Children.Add(def_hline(i, size, VerticalAlignment.Center));
                g.Children.Add(def_vline(i, size, HorizontalAlignment.Center));
            }
            g.Children.Add(def_hline(size + 1, size, VerticalAlignment.Bottom));
            g.Children.Add(def_vline(size + 1, size, HorizontalAlignment.Right));
        }

        private const int LINE_WIDTH = 2;

        /// _def_hline defines a horizontal line for _place_lines.  Loc is the grid row,
        /// size the go board size, and alignment pins the line within the grid row.
        ///
        private static Rectangle def_hline (int loc, int size, VerticalAlignment alignment) {
            var hrect = new Rectangle();
            Grid.SetRow(hrect, loc);
            // 0th and 1st cols are border and half of split col.
            Grid.SetColumn(hrect, 2);
            Grid.SetColumnSpan(hrect, size);
            hrect.Height = LINE_WIDTH;
            hrect.Fill = new SolidColorBrush(Colors.Black);
            hrect.VerticalAlignment = alignment;
            return hrect;
        }

        /// _def_vline defines a vertical line for _place_lines.  Loc is the grid column,
        /// size the go board size, and alignment pins the line within the grid column.
        ///
        private static Rectangle def_vline (int loc, int size, HorizontalAlignment alignment) {
            var vrect = new Rectangle();
            // 0th and 1st rows are border and half of split col.
            Grid.SetRow(vrect, 2);
            Grid.SetColumn(vrect, loc);
            Grid.SetRowSpan(vrect, size);
            vrect.Width = LINE_WIDTH;
            vrect.Fill = new SolidColorBrush(Colors.Black);
            vrect.HorizontalAlignment = alignment;
            return vrect;
        }

        /// add_handicap_point takes a Grid and location in terms of go board indexing
        /// (one based).  It adds a handicap location dot to the board.  We do not have
        /// to subtract one from x or y since there is a border row and column that is
        /// at the zero location.
        ///
        internal static void AddHandicapPoint (Grid g, int x, int y) {
            // Account for border rows/cols.
            x += 1;
            y += 1;
            var dot = new Ellipse();
            dot.Width = 8;
            dot.Height = 8;
            Grid.SetRow(dot, y);
            Grid.SetColumn(dot, x);
            dot.Fill = new SolidColorBrush(Colors.Black);
            g.Children.Add(dot);
        }


        ////
        //// Setup Stones Grid Utilities
        ////

        //// _setup_index_labels takes a grid and go board size, then emits Label
        //// objects to create alphanumeric labels.  The labels index the board with
        //// letters for the columns, starting at the left, and numerals for the rows,
        //// starting at the bottom.  The letters skip "i" to avoid fontface confusion
        //// with the numeral one.  This was chosen to match KGS and many standard
        //// indexing schemes commonly found in pro commentaries.
        ////
        internal static void SetupIndexLabels (Grid g, int size) {
            Debug.Assert(size > 1);
            for (var i = 1; i < size + 1; i++) {
                //for i in xrange(1, size + 1):
                // chr_offset skips the letter I to avoid looking like the numeral one in the display.
                var chr_offset = (i < 9) ? i : (i + 1);
                var chr_txt = (char)(chr_offset + (int)('A') - 1);
                var num_label_y = size - (i - 1);
                // Place labels
                MainWindowAux.SetupIndexLabel(g, i.ToString(), 0, num_label_y,
                                              HorizontalAlignment.Left, VerticalAlignment.Center);
                MainWindowAux.SetupIndexLabel(g, i.ToString(), 20, num_label_y,
                                              HorizontalAlignment.Right, VerticalAlignment.Center);
                MainWindowAux.SetupIndexLabel(g, chr_txt.ToString(), i, 0,
                                   HorizontalAlignment.Center, VerticalAlignment.Top);
                MainWindowAux.SetupIndexLabel(g, chr_txt.ToString(), i, 20,
                                              HorizontalAlignment.Center, VerticalAlignment.Bottom);
            }
        }

        internal static void SetupIndexLabel (Grid g, string content, int x, int y,
                                              HorizontalAlignment h_alignment, VerticalAlignment v_alignment) {
            var label = new TextBlock();
            label.Text = content;
            Grid.SetRow(label, y);
            Grid.SetColumn(label, x);
            label.FontWeight = FontWeights.Bold;
            label.FontSize = 14;
            label.Foreground = new SolidColorBrush(Colors.Black);
            label.HorizontalAlignment = h_alignment;
            label.VerticalAlignment = v_alignment;
            g.Children.Add(label);
        }

        
        ////
        //// Adding and Remvoing Stones
        ////

        /// stones holds Ellipse objects to avoid linear searching when removing
        /// stones.  We could linear search for the Ellipse and store in a move.cookie,
        /// but we'd have to search since grids don't have any operations to remove
        /// children by identifying the cell that holds them.  Grids don't do much to
        /// support cell-based operations, like mapping input event pixel indexes to
        /// cells.
        ///
        internal static Ellipse[,] stones = new Ellipse[Game.MaxBoardSize, Game.MaxBoardSize];

        //// add_stone takes a Grid and row, column that index the go board one based
        //// from the top left corner.  It adds a WPF Ellipse object to the stones grid.
        ////
        internal static void AddStone (Grid g, int row, int col, Color color) {
            var stone = AddStoneNoMapping(g, row, col, color);
            stones[row - 1, col - 1] = stone;
        }

        //// AddStoneNoMapping does most of the work of AddStone, but it does NOT record the
        //// Ellipse in stones[,] for quick look up elsewhere (ResetToStart, SetupBoardDisplay).
        //// This method helps SetupSnappedViewDisplay create one-off static board images.
        ////
        internal static Ellipse AddStoneNoMapping (Grid g, int row, int col, Color color) {
            var stone = new Ellipse();
            Grid.SetRow(stone, row);
            Grid.SetColumn(stone, col);
            stone.StrokeThickness = 1;
            stone.Stroke = new SolidColorBrush(Colors.Black);
            stone.Fill = new SolidColorBrush(color);
            stone.HorizontalAlignment = HorizontalAlignment.Stretch;
            stone.VerticalAlignment = VerticalAlignment.Stretch;
            var b = new Binding(); // ("ActualHeight");
            b.ElementName = "ActualHeight";
            stone.SetBinding(FrameworkElement.WidthProperty, b);
            g.Children.Add(stone);
            return stone;
        }

        //// remove_stone takes a stones grid and a move.  It removes the Ellipse for
        //// the move and notes in stones global that there's no stone there in the
        //// display.  This function also handles the current move adornment and other
        //// adornments since this is used to rewind the current move.
        ////
        internal static void RemoveStone (Grid g, Move move) {
            var stone = stones[move.Row - 1, move.Column - 1];
            Debug.Assert(stone != null, "Shouldn't be removing stone if there isn't one.");
            g.Children.Remove(stone);
            stones[move.Row - 1, move.Column - 1] = null;
            // Must remove current adornment before other adornments (or just call
            // Adornments.release_current_move after loop).
            if (move.Adornments.Contains(Adornments.CurrentMoveAdornment))
                RemoveCurrentStoneAdornment(g, move);
            RemoveAdornments(g, move.Adornments);
        }

        /// grid_pixels_to_cell returns the go board indexes (one based)
        /// for which intersection the click occurred on (or nearest to).
        /// 
        internal static Point GridPixelsToCell (Grid g, double x, double y) {
            var col_defs = g.ColumnDefinitions;
            var row_defs = g.RowDefinitions;
            // No need to add one to map zero based grid elements to one based go board.
            // The extra border col/row accounts for that in the division.
            int cell_x = (int)(x / col_defs[0].ActualWidth);
            int cell_y = (int)(y / row_defs[0].ActualHeight);
            // Cell index must be 1..<board_size>, and there's two border rows/cols.
            return new Point(Math.Max(Math.Min(cell_x, col_defs.Count - 2), 1),
                             Math.Max(Math.Min(cell_y, col_defs.Count - 2), 1));
        }


        ////
        //// Adding and Remvoing Adornments
        ////

        //// add_current_stone_adornment takes the stones grid and a move, then adds the
        //// concentric circle to mark the last move on the board.  This uses two
        //// globals to cache an inner grid (placed in one of the cells of the stones
        //// grid) and the cirlce since they are re-used over and over throughout move
        //// placemnent.  The inner grid is used to create a 3x3 so that there is a
        //// center cell in which to place the current move adornment so that it can
        //// stretch as the whole UI resizes.
        ////
        private static Grid current_stone_adornment_grid = null;
        private static Ellipse current_stone_adornment_ellipse = null;
        internal static void AddCurrentStoneAdornment (Grid stones_grid, Move move) {
            if (current_stone_adornment_grid != null) {
                current_stone_adornment_ellipse.Stroke =
                   new SolidColorBrush(GameAux.OppositeMoveColor(move.Color));
                Grid.SetRow(current_stone_adornment_grid, move.Row);
                Grid.SetColumn(current_stone_adornment_grid, move.Column);
                Adornments.GetCurrentMove(move, current_stone_adornment_grid);
                stones_grid.Children.Add(current_stone_adornment_grid);
            }
            else {
                var inner_grid = MainWindowAux.AddAdornmentGrid(stones_grid, move.Row, move.Column);
                current_stone_adornment_grid = inner_grid;
                //
                // Create mark
                var mark = new Ellipse();
                current_stone_adornment_ellipse = mark;
                Grid.SetRow(mark, 1);
                Grid.SetColumn(mark, 1);
                mark.StrokeThickness = 2;
                mark.Stroke = new SolidColorBrush(GameAux.OppositeMoveColor(move.Color));
                mark.Fill = new SolidColorBrush(Colors.Transparent);
                mark.HorizontalAlignment = HorizontalAlignment.Stretch;
                mark.VerticalAlignment = VerticalAlignment.Stretch;
                inner_grid.Children.Add(mark);
                //
                // Update model.
                Adornments.GetCurrentMove(move, inner_grid);
            }
        }

        internal static void RemoveCurrentStoneAdornment (Grid stones_grid, Move move) {
            if (move != null && !move.IsPass) {
                stones_grid.Children.Remove(Adornments.CurrentMoveAdornment.Cookie);
                Adornments.ReleaseCurrentMove();
            }
        }


        //// add_or_remove_adornment adds or removes an adornment to the current board
        //// state.  It removes an adornment of kind kind at row,col if one already
        //// exisits there.  Otherwise, it adds the new adornment.  If the kind is
        //// letter, and all letters A..Z have been used, then this informs the user.
        ////
        internal static async Task AddOrRemoveAdornment (Grid stones_grid, int row, int col, AdornmentKind kind,
                                                   Game game) {
            var a = game.GetAdornment(row, col, kind);
            if (a != null) {
                game.RemoveAdornment(a);
                stones_grid.Children.Remove(a.Cookie);
            }
            else {
                a = game.AddAdornment(game.CurrentMove, row, col, kind);
                if (a == null && kind == AdornmentKind.Letter) {
                    await GameAux.Message("Cannot add another letter adornment.  " +
                                          "You have used A through Z already.");
                    return;
                }
                AddNewAdornment(stones_grid, a, game);
            }
            game.Dirty = true;
        }


        //// add_new_adornment takes the stones grid, an adornment, and the game
        //// instance to update the UI with the specified adornment (square, triangle,
        //// letter).  The last parameter actually controls whether the adornment is
        //// added to the stones grid for rendering or simply prepared for rendering.
        //// The adornment is a unique instance and holds in its cookie the grid holding
        //// the WPF markup object.  This grid is a 3x3 grid that sits in a single cell
        //// of the stones grid.  We do not re-uses these grids for multiple adornments
        //// or free list them at this time.
        ////
        internal static void AddNewAdornment(Grid stones_grid, Adornments adornment, Game game_inst,
                                               bool render = true) {
            UIElement gridOrViewbox;
            Debug.Assert(adornment.Kind == AdornmentKind.Square || adornment.Kind == AdornmentKind.Triangle ||
                         adornment.Kind == AdornmentKind.Letter,
                         "Eh?! Unsupported AdornmentKind value?");
            if (adornment.Kind == AdornmentKind.Square)
                gridOrViewbox = MakeSquareAdornment(stones_grid, adornment.Row, adornment.Column,
                                             game_inst, render);
            else if (adornment.Kind == AdornmentKind.Triangle)
                gridOrViewbox = MakeTriangleAdornment(stones_grid, adornment.Row, adornment.Column,
                                               game_inst, render);
            else // if (adornment.Kind == AdornmentKind.Letter)
                // grid in this case is really a viewbow.
                gridOrViewbox = MakeLetterAdornment(stones_grid, adornment.Row, adornment.Column,
                                             adornment.Letter, game_inst, render);
            adornment.Cookie = gridOrViewbox;
        }

        //// make_square_adornment returns the inner grid with a Rectangle adornment in
        //// it.  The inner grid is already placed in the stones grid at the row, col if
        //// render is true.  game_inst is needed to determine if there is a move at
        //// this location or an empty board location to set the adornment color.
        ////
        private static Grid MakeSquareAdornment (Grid stones_grid, int row, int col, Game game_inst, bool render) {
            var grid = MainWindowAux.AddAdornmentGrid(stones_grid, row, col, render);
            var sq = new Rectangle();
            Grid.SetRow(sq, 1);
            Grid.SetColumn(sq, 1);
            sq.StrokeThickness = 2;
            var move = game_inst.Board.MoveAt(row, col);
            Color color;
            if (move != null)
                color = GameAux.OppositeMoveColor(move.Color);
            else
                color = Colors.Black;
            sq.Stroke = new SolidColorBrush(color);
            sq.Fill = new SolidColorBrush(Colors.Transparent);
            sq.HorizontalAlignment = HorizontalAlignment.Stretch;
            sq.VerticalAlignment = VerticalAlignment.Stretch;
            grid.Children.Add(sq);
            return grid;
        }

        //// make_triangle_adornment returns the inner grid with a ViewBox in the center
        //// cell that holds the adornment in it.  Since Polygons don't stretch
        //// automatically, like Rectangles, the ViewBox provides the stretching.  The
        //// inner grid is already placed in the stones grid at the row, col if render
        //// is true.  game_inst is needed to determine if there is a move at this
        //// location or an empty board location to set the adornment color.
        ////
        private static Grid MakeTriangleAdornment (Grid stones_grid, int row, int col, Game game_inst, bool render) {
            var grid = MainWindowAux.AddAdornmentGrid(stones_grid, row, col, render);
            //grid.ShowGridLines = True
            var vwbox = new Viewbox();
            Grid.SetRow(vwbox, 1);
            Grid.SetColumn(vwbox, 1);
            vwbox.HorizontalAlignment = HorizontalAlignment.Stretch;
            vwbox.VerticalAlignment = VerticalAlignment.Stretch;
            grid.Children.Add(vwbox);
            var tri = new Polygon();
            tri.Points.Add(new Point(3, 0));
            tri.Points.Add(new Point(0, 6));
            tri.Points.Add(new Point(6, 6));
            tri.StrokeThickness = 1;
            var move = game_inst.Board.MoveAt(row, col);
            Color color;
            if (move != null)
                color = GameAux.OppositeMoveColor(move.Color);
            else
                color = Colors.Black;
            tri.Stroke = new SolidColorBrush(color);
            tri.Fill = new SolidColorBrush(Colors.Transparent);
            //tri.HorizontalAlignment = HorizontalAlignment.Stretch;
            //tri.VerticalAlignment = VerticalAlignment.Stretch;
            vwbox.Child = tri;
            return grid;
        }

        //// make_letter_adornment returns a ViewBox in the stones_grid cell.  This does
        //// not do the inner grid like make_square_adornment and make_triagle_adornment
        //// since for some reason, that makes the letter show up very very small.  The
        //// ViewBox provides the stretching for the Label object.  If render is false,
        //// we do not put the viewbox into the grid.  game_inst is needed to determine
        //// if there is a move at this location or an empty board location to set the
        //// adornment color.
        ////
        private static Viewbox MakeLetterAdornment (Grid stones_grid, int row, int col, string letter,
                                                    Game game_inst, bool render) {
            var vwbox = new Viewbox();
            Grid.SetRow(vwbox, row);
            Grid.SetColumn(vwbox, col);
            vwbox.HorizontalAlignment = HorizontalAlignment.Stretch;
            vwbox.VerticalAlignment = VerticalAlignment.Stretch;
            var label = new TextBlock();
            label.Text = letter;
            // Win8: Fontsize has no effect on TextBlock, but setting a margin will reduce the size.
            label.FontSize = 10.0;
            label.Margin = new Thickness(2, 2, 2, 2);
            Grid.SetRow(label, 1);
            Grid.SetColumn(label, 1);
            //label.FontWeight = FontWeights.Bold
            //label.HorizontalAlignment = HorizontalAlignment.Stretch;
            //label.VerticalAlignment = VerticalAlignment.Stretch;
            var move = game_inst.Board.MoveAt(row, col);
            Color color;
            if (move != null) {
                color = GameAux.OppositeMoveColor(move.Color);
                // No way to set anything here, but the label is transparent by default in win8.
                //label.Background = new SolidColorBrush(Colors.Transparent);
                vwbox.Child = label;
            }
            else {
                color = Colors.Black;
                // Win8 hack: Need extra grid inside Viewbox because ViewBoxes and TextBlocks have no background.
                var inner_grid = new Grid();
                // See MainPage.xaml for lines grid (board) tan background ...
                inner_grid.Background = new SolidColorBrush(Color.FromArgb(0xff, 0xd7, 0xb2, 0x64));
                inner_grid.VerticalAlignment = VerticalAlignment.Stretch;
                inner_grid.HorizontalAlignment = HorizontalAlignment.Stretch;
                inner_grid.Children.Add(label);
                vwbox.Child = inner_grid;
            }
            label.Foreground = new SolidColorBrush(color);
            //vwbox.Child = label;
            if (render)
                stones_grid.Children.Add(vwbox);
            return vwbox;
        }


        //// add_adornment_grid sets up 3x3 grid in current stone's grid cell to hold
        //// adornment.  It returns the inner grid.  This adds the inner grid to the
        //// stones grid only if render is true.  This inner grid is needed to both
        //// center the adornment in the stones grid cell and to provide a stretchy
        //// container that grows with the stones grid cell.
        ////
        internal static Grid AddAdornmentGrid (Grid stones_grid, int row, int col, bool render = true) {
            var inner_grid = new Grid();
            //inner_grid.ShowGridLines = false;  // not needed on win8
            inner_grid.Background = new SolidColorBrush(Colors.Transparent);
            Grid.SetRow(inner_grid, row);
            Grid.SetColumn(inner_grid, col);
            inner_grid.HorizontalAlignment = HorizontalAlignment.Stretch;
            inner_grid.VerticalAlignment = VerticalAlignment.Stretch;
            //inner_grid.Name = "adornmentGrid"
            inner_grid.ColumnDefinitions.Add(MainWindowAux.def_col(1));
            inner_grid.ColumnDefinitions.Add(def_col(2));
            inner_grid.ColumnDefinitions.Add(def_col(1));
            inner_grid.RowDefinitions.Add(def_row(1));
            inner_grid.RowDefinitions.Add(def_row(2));
            inner_grid.RowDefinitions.Add(def_row(1));
            if (render)
                stones_grid.Children.Add(inner_grid);
            return inner_grid;
        }

        //// remove_adornments removes the list of adornments from stones grid.
        //// Adornments must be non-null.  Also, be careful to call
        //// remove_current_stone_adornment before calling this so that it can be
        //// managed correctly.  Note, this does not set the cookie to None so that
        //// restore_adornments can re-use them.  We don't think there's so much mark up
        //// that holding onto the cookies would burden most or all serious game review
        //// files.
        ////
        internal static void RemoveAdornments (Grid stones_grid, List<Adornments> adornments) {
            foreach (var a in adornments)
                stones_grid.Children.Remove(a.Cookie);
        }

        internal static void RestoreAdornments (Grid stones_grid, List<Adornments> adornments) {
            foreach (var a in adornments)
                stones_grid.Children.Add(a.Cookie);
        }


        ////
        //// Tree View Utils
        ////

        //// treeViewGridCellSize is the number of pixels along one side of a "grid cell"
        //// on the canvas.
        ////
        internal const int treeViewGridCellSize = 40;

        //// DrawGameTreeLines draws all the lines from this node to its next nodes.
        //// Note, these are TreeViewNodes, not Moves, so some of the nodes simply denote
        //// line bends for drawing the tree.
        ////
        internal static void DrawGameTreeLines (Canvas canvas, TreeViewNode node) {
            if (node.Next == null)
                return;
            if (node.Branches != null)
                foreach (var n in node.Branches) {
                    MainWindowAux.DrawGameTreeLine(canvas, node, n);
                    MainWindowAux.DrawGameTreeLines(canvas, n);
                }
            else {
                MainWindowAux.DrawGameTreeLine(canvas, node, node.Next);
                MainWindowAux.DrawGameTreeLines(canvas, node.Next);
            }
        }

        internal static void DrawGameTreeLine (Canvas canvas, TreeViewNode origin, TreeViewNode dest) {
            var ln = new Line();
            // You'd think you'd divide by 2 to get a line in the middle of a cell area to the middle
            // of another cell area, but the lines all appear too low for some reason, so use 3 instead.
            ln.X1 = (origin.Column * MainWindowAux.treeViewGridCellSize) + (MainWindowAux.treeViewGridCellSize / 3);
            ln.Y1 = (origin.Row * MainWindowAux.treeViewGridCellSize) + (MainWindowAux.treeViewGridCellSize / 3);
            ln.X2 = (dest.Column * MainWindowAux.treeViewGridCellSize) + (MainWindowAux.treeViewGridCellSize / 3);
            ln.Y2 = (dest.Row * MainWindowAux.treeViewGridCellSize) + (MainWindowAux.treeViewGridCellSize / 3);
            // Lines appear behind Move circles/nodes.
            Canvas.SetZIndex(ln, 0);
            ln.Stroke = new SolidColorBrush(Colors.Black);
            ln.StrokeThickness = 2;
            canvas.Children.Add(ln);
        }


        //// NewTreeViewItemGrid returns a grid to place on the canvas for drawing game tree views.
        //// The grid has a stone image with a number label if it is a move, or just an "S" for the start.
        //// Do not call this on line bend view nodes.
        ////
        internal static Grid NewTreeViewItemGrid (TreeViewNode model) {
            // Get Grid to hold stone image and move number label
            var g = new Grid();
            //g.ShowGridLines = false;
            g.HorizontalAlignment = HorizontalAlignment.Stretch;
            g.VerticalAlignment = VerticalAlignment.Stretch;
            g.Background = new SolidColorBrush(Colors.Transparent);
            g.Height = 25;
            g.Width = 25;
            g.Margin = new Thickness(0, 2, 0, 2);
            // Get stone image
            if (model.Kind == TreeViewNodeKind.Move) {
                var stone = new Ellipse();
                stone.StrokeThickness = 1;
                stone.Stroke = new SolidColorBrush(Colors.Black);
                stone.Fill = new SolidColorBrush(model.Color);
                stone.HorizontalAlignment = HorizontalAlignment.Stretch;
                stone.VerticalAlignment = VerticalAlignment.Stretch;
                g.Children.Add(stone);
            }
            // Get move number label
            Debug.Assert(model.Kind != TreeViewNodeKind.LineBend,
                         "Eh?!  Shouldn't be making tree view item grid for line bends.");
            var label = new TextBlock();
            if (model.Kind == TreeViewNodeKind.StartBoard)
                label.Text = "S";
            else
                // Should use move number, but ParsedNodes are not numbered.  Column = move number anyway.
                label.Text = model.Column.ToString();
            // Set font size based on length of integer print representation
            label.FontWeight = FontWeights.Bold;
            if (model.Column.ToString().Length > 2) {
                label.FontSize = 10;
                label.FontWeight = FontWeights.Normal;
            }
            else
                label.FontSize = 12;
            label.Foreground = new SolidColorBrush(model.Kind == TreeViewNodeKind.Move ?
                                                    GameAux.OppositeMoveColor(model.Color) :
                                                    Colors.Black);
            label.HorizontalAlignment = HorizontalAlignment.Center;
            label.VerticalAlignment = VerticalAlignment.Center;
            g.Children.Add(label);
            return g;
        }


        ////
        //// Misc Utils
        ////

        //// set_current_branch{_up|_down} takes the branch combo to operate on and the
        //// game instance.  It updates the combo box and then updates the model by
        //// calling on the game object.
        ////
        internal static void SetCurrentBranchUp (ComboBox combo, Game game) {
            var cur = combo.SelectedIndex;
            if (cur > 0)
                combo.SelectedIndex = cur - 1;
            game.SetCurrentBranch(combo.SelectedIndex);
        }

        internal static void SetCurrentBranchDown (ComboBox combo, Game game) {
            var cur = combo.SelectedIndex;
            if (cur < combo.Items.Count - 1)
                combo.SelectedIndex = cur + 1;
            game.SetCurrentBranch(combo.SelectedIndex);
        }


        internal static async Task<StorageFile> GetSaveFilename (string title = null) {
            var fp = new FileSavePicker();
            fp.FileTypeChoices.Add("Text documents (.sgf)", new[] { ".sgf" });
            fp.DefaultFileExtension = ".sgf";
            fp.SuggestedFileName = "game01";
            var sf = await fp.PickSaveFileAsync();
            return sf;

            //dlg.Title = (title != null ? title : dlg.Title);
            //var result = dlg.ShowDialog(); // None, True, or False
            //if (result.HasValue && result.Value)
            //    return dlg.FileName;
            //else
            //    return null;
        }


    } // class MainWindowAux

} // namespace sgfed

