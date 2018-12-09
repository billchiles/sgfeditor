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
using Windows.ApplicationModel.DataTransfer; // DataPackage and Clipboard


namespace SgfEdwin8 {

    ////
    //// MainWindow class
    ////

    public sealed partial class MainWindow : Page {

        private const string HelpString = @"
SGFEditor can read and write .sgf files, edit game trees, etc.

The following describes command and key bindings, but you can use commands
from buttons in the upper right of the display and from the app bar (right
click on the game board or drag up from the bottom of the display).

PLACING STONES AND ANNOTATIONS:
Click on board location to place alternating colored stones.  If the last move
is a misclick (at the end of a branch), click the last move again to undo it.
Shift click to place square annotations, ctrl click for triangles, and
alt click to place letter annotations.  If you click on an adornment location
twice (for example shift-click twice in one spot), the second click removes
the adornment.

KEEPING FOCUS ON BOARD FOR KEY BINDINGS
Escape will always return focus to the board so that the arrow keys work
and are not swallowed by the comment editing pane.  Sometimes opening and
saving files leaves WPF in a weird state such that you must click somewhere
to fix focus.

NAVIGATING MOVES IN GAME TREE
Right arrow moves to the next move, left moves to the previous, up arrow selects
another branch or the main branch, down arrow selects another branch, home moves
to the game start, and end moves to the end of the game following the currently
selected branches.  You can always click a node in the game tree graph.  Ctrl-left
arrow moves to the closest previous move that has branches.  If the current move
has branches following it, the selected branch's first node has a light grey
square outlining it. Nodes that have comments have a light green highlight, and
the current node has a fuchsia highlight.

CREATING NEW FILES
The new button (or ctrl-n) prompts for game info (player names, board size,
handicap, komi) and creates a new game.  If the current game is dirty, this prompts
to save.

OPENING EXISTING FILES
The open button (or ctrl-o) prompts for a .sgf file name to open.  If the current
game is dirty, this prompts to save.

MULTIPLE OPEN FILES
You can have up to 10 games open.  Limit to 10 seems reasonable until understand
store app VM profile.  Ctrl-w rotates through games.  Ctrl-g brings up dialog for
switching to games and closing games.  When creating or opening games, SgfEditor
closes the default game if it is unused. If you open a game that is already open,
SgfEditor just displays the game where you were last viewing it.

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
a branch up, you must be on the first move of a branch, and then you can use
ctrl-uparrow, and to move a branch down, use ctrl-downarrow.

PASSING
The Pass button or c-p will make a pass move.

MISCELLANEOUS
   F1 produces this help.
   Ctrl-k clears the current node's comment and puts text on system clipboard.
   Ctrl-1, ..., ctrl-5 deletes the first, ..., fifth line of node's comment and
      puts entire comment's text on clipboard.
   Ctrl-t changes the first occurence of the move's board coordinates in the comment
      to 'this'; for example, 'd6 is strong cut' changes to 'this is strong cut'.
";

///
/// Variables in dialog to play with display:
///    tree font size -- bound it relative to cell, subtract for smaller size
///    tree cell size -- how to compute stone size, 65%?
///    colors -- curret, comment, which branch, and how to translate to code and how to enter
///       https://docs.microsoft.com/en-us/uwp/api/windows.ui.colors
///       maybe (SolidColorBrush)new BrushConverter().ConvertFromString("Red");
///       Namespace: Windows.UI.Xaml.Markup
///       Assemblies: Windows.UI.Xaml.Markup.dll, Windows.dll
///       XamlBindingHelper Class
///       var color = (Color)XamlBindingHelper.ConvertValue(typeof(Color), "ORANGE");
///       var brush = new SolidColorBrush(color);
///       in WPF ... Color col=(Color)ColorConverter.ConvertFromString("Red"); 
///       in ??? ... System.Drawing.Color.FromName(colorName);
///    comment font size -- how to bound?
///    file name and game state font -- how to bound?
///    board indexes font
///    non-board width -- current is min, what about changing grid from asterisk to fixed ?
///

        private int prevSetupSize = 0;

        public Game Game { get; set; }
        // Public because helpers in view controller GameAux add games here.  Could put
        // helpers in MainwindowAux, but it is meant for use in this file only.  No great design choice.
        public List<Game> Games = new List<Game>();
        // Helps cleanup when there are lexing, parsing, or game construction errors when opening files.
        public Game LastCreatedGame = null;
        // Helps cleanup unused default game to avoid accumulating them in open games.
        public Game DefaultGame = null;
        // Limit open games to avoid VM bloat and app issues from opening games over weeks.
        const int MAX_GAMES = 10;



        public MainWindow () {
            this.InitializeComponent();
            this.prevSetupSize = 0;
            this.CreateDefaultGame();
        }

        private void CreateDefaultGame () {
            this.Game = GameAux.CreateDefaultGame(this);
            this.DefaultGame = this.Game;
            this.UpdateTitle();
            this.DrawGameTree();
            this.FocusOnStones(); // Doesn't seem to work for some reason when called on launch.
        }


        //// 
        //// Ensuring board is square and checking for auto saved file
        //// 

        //// OnNavigatedTo adds SizeChanged handler to ensure board is square.  It also checks for the
        //// unnamed auto saved file.
        //// 
        protected override void OnNavigatedTo (NavigationEventArgs e) {
            // Examples use e.Parameter here, but this e.Parameter has a string or other weird types.
            // MainWindow mainWin = e.Parameter as MainWindow;
            Window.Current.SizeChanged += this.MainView_SizeChanged;
            base.OnNavigatedTo(e);
            // Set up autosave timer.
            var timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(30);
            timer.Tick += new EventHandler<object>(async (sender, args) => { await this.MaybeAutoSave(); });
            timer.Start();
            // Set up one time timer to check for autosaved unnamed file, which is on timer so that
            // it does not sometimes cause app to fail store WACK for launch time.
            var timer2 = new DispatcherTimer();
            timer2.Interval = TimeSpan.FromSeconds(1);
            timer2.Tick += 
                new EventHandler<object>(async (sender, args) => {
                    timer2.Stop();
                    await this.onNavigatedToCheckAutoSave();
                });
            timer2.Start();
            this.FocusOnStones(); // Doesn't seem to work.
        }

        //// FileActivatedNoUnnamedAutoSave is set by App.OnFileActivated so that when the user launches
        //// on a file, we do not check for an unnamed auto saved file.
        public static bool FileActivatedNoUnnamedAutoSave = false;
        //// onNavigatedToCheckAutoSave checks for unnamed auto saved file from user doodling on default board.
        //// If we file launched, then don't check since the only options from the unnamed auto save dialog are
        //// to open auto saved file or create default board.  Don't check if in open file dialog, see comment
        //// below.
        //// 
        private async Task onNavigatedToCheckAutoSave () {
            if (MainWindow.FileActivatedNoUnnamedAutoSave) {
                // Reset var for cleanliness.  Shouldn't matter if reset it since it is only set when launching
                // cold from file, and when launching warm from file, then we do frame.nav to MainWindow.
                MainWindow.FileActivatedNoUnnamedAutoSave = false;
                await this.DeleteUnnamedAutoSave();
            }
            else if (! this.inOpenFileDialog) {
                // Users can launch app, type c-o, and be in open file dialog when we're checking for an
                // auto-saved, unnamed file.  If user launched and hit c-o that fast, assume we can forgo
                // auto-save check.
                //
                // User started editing within one sec startup timer before checking autosave, so just punt.
                if (this.Game.Dirty) 
                    return;
                // Need to cover UI to block user from mutating state while checking autosave.
                Popup popup = null;
                try {
                    popup = new Popup();
                    popup.Child = this.GetUIDike();
                    popup.IsOpen = true;
                    await CheckUnnamedAutoSave();
                }
                finally {
                    popup.IsOpen = false;
                }
            }
            else
                await this.DeleteUnnamedAutoSave();

        }

        //// OnNavigatedFrom removes the resize handler, which was recommended in the sample I used
        //// to figure out how to create a snapped view.
        //// 
        protected override void OnNavigatedFrom (NavigationEventArgs e) {
            Window.Current.SizeChanged -= this.MainView_SizeChanged;
            base.OnNavigatedFrom(e);
        }


        //// MaybeAutoSave saves a temp file if the game state is dirty, but this leaves the dirty flag true.
        //// This is public since OnSuspended in app.xaml.cs calls it too.  OnNavigatedTo above creates a
        //// DispatchTimer to invoke this every 30s.
        ////
        public async Task MaybeAutoSave () {
            var g = this.Game; // Capture game in case user switches games while this is running.
            if (g.Dirty) {
                var file = this.GetAutoSaveName(g.Filebase);
                var tempFolder = ApplicationData.Current.TemporaryFolder;
                var storage = await tempFolder.CreateFileAsync(file, CreationCollisionOption.ReplaceExisting);
                await g.WriteGame(storage, true);
            }
        }

        //// CheckUnnamedAutoSave looks for an auto save of a game the user never saved to disk.
        //// OnNavigatedTo above sets a 2s timer to invoke this so that the file probes and parsing do
        //// no affect launch time, as measured by the WACK.  If the file is less than 12 hours old,
        //// prompt to open it, but assume user doesn't care otherwise.
        ////
        private async Task CheckUnnamedAutoSave () {
            var autoSf = await this.GetAutoSaveFile(MainWindow.UnnamedAutoSaveName);
            if (autoSf != null) {
                if ((DateTimeOffset.Now - autoSf.DateCreated).Hours < 12 &&
                        await GameAux.Message("Found unnamed auto saved file.", "Confirm opening auto saved file",
                                               new List<string>() {"Open auto saved file", 
                                                                   "Create default new board"})
                            == "Open auto saved file") {
                    var defaultGame = this.Game;
                    await this.ParseAndCreateGame(autoSf);
                    // Since we only check this on launch, and user chose to open saved file, clean up default game.
                    this.Games.Remove(defaultGame);
                    this.Game.Dirty = true; // actual file is not saved (there is no file assoc :-))
                    // Erase auto save file info from Game so that save prompts for name.
                    this.Game.Storage = null;  // No Close operation.
                    this.Game.Filename = null;
                    this.Game.Filebase = null;
                    // Fix up title and draw tree.
                    this.UpdateTitle();
                    this.DrawGameTree();
                }
                await autoSf.DeleteAsync();
            }
        }

        //// DeleteUnnamedAutoSave is used for just cleaning up the unnamed auto save file
        //// when OnNavigatedTo forgoes checking the file.
        ////
        private async Task DeleteUnnamedAutoSave () {
            var autoSf = await this.GetAutoSaveFile(MainWindow.UnnamedAutoSaveName);
            if (autoSf != null)
                await autoSf.DeleteAsync();
        }

        //// 
        //// Handling Snap View (required by store compliance)
        //// 


        //// Page_SizeChanged comes from win10 port for smooth resizing, along with commensurate changes in xaml
        //// outer grid columns and FocusableInputControl resizing.  On win8 and win8.1 need to deal with
        //// snapped view interactions.  When making this change, could only test win8 build on win8.1 machine and
        //// a 8.1 build on a win10 machine, leaving out testing win8 build on win8 :-(.  The 8.1 build on win10
        //// had some quirks that the win10 version did not have for the exact same xaml and code (because the
        //// snapped view code never executes).  The win8 build had similar slightly off quirks, mostly requiring
        //// more width for right hand panel of controls (more width than 8.1 build required on win10).
        //// 
        private void Page_SizeChanged (object sender, SizeChangedEventArgs e) {
            //var narrow = e.NewSize.Width < 600;   
            var w = e.NewSize.Width - 600; //110 if cleverly relaying out narrow controls and wide controls   
            var h = e.NewSize.Height;
            if (w < 50) return;
            if (w > h) { this.inputFocus.Width = h; this.inputFocus.Height = h; }
            else { this.inputFocus.Width = w; this.inputFocus.Height = w; }
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
            this.AddInitialStones(this.Game, this.collapsedStonesGrid);
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
                this.AddInitialStones(new_game);
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
                // TODO Investigate this, probably was no-op for releasing memory before, and now that hold
                // games in this.Games, just need to make sure swapping games is fine if we delete this.
                //this.Game.Board.GotoStart();
                this.UpdateBranchCombo(null, null);
                this.CurrentComment = "";
                this.DisableBackwardButtons();
                // If opening game file, next/end set to true by game code.
                this.DisableForwardButtons();
                MainWindowAux.stones = new Ellipse[Game.MaxBoardSize, Game.MaxBoardSize];
                this.AddInitialStones(new_game);
            }
            else
                throw new Exception("Haven't implemented changing board size for new games.");
            this.InitializeTreeView();  // Do not call DrawGameTree here since main win does not point to game yet.
            this.MoveButtonClick(null, null);
        }

        //// SetupLinesGrid takes an int for the board size (as int) and returns a WPF Grid object for
        //// adding to the MainWindow's Grid.  The returned Grid contains lines for the Go board.
        ////
        private Grid SetupLinesGrid (int size, Grid g = null) {
            MyDbg.Assert(size >= Game.MinBoardSize && size <= Game.MaxBoardSize,
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
            //if (! hackDone) {
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
        private void ShowHelp () {
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


        //// whatBoardClickCreates is set in App Bar button handlers and used by
        //// StonesPointerPressed.
        ////
        private AdornmentKind whatBoardClickCreates = AdornmentKind.CurrentMove;

        //// StonesPointerPressed handles creating a move or adding adornments
        //// to the current move.
        ////
        private async void StonesPointerPressed (object sender, PointerRoutedEventArgs e) {
            var g = (Grid)sender;
            if (e.GetCurrentPoint(g).Properties.IsLeftButtonPressed) {
                var cell = MainWindowAux.GridPixelsToCell(g, e.GetCurrentPoint(g).Position.X, 
                                                          e.GetCurrentPoint(g).Position.Y);
                //MessageDialog.Show(cell.X.ToString() + ", " + cell.Y.ToString());
                // cell x,y is col, row from top left, and board is row, col.
                if (this.IsKeyPressed(VirtualKey.Shift) || this.whatBoardClickCreates == AdornmentKind.Square)
                    await MainWindowAux.AddOrRemoveAdornment(this.stonesGrid, (int)cell.Y, (int)cell.X, 
                                                             AdornmentKind.Square, this.Game);
                else if (this.IsKeyPressed(VirtualKey.Control) || this.whatBoardClickCreates == AdornmentKind.Triangle)
                    await MainWindowAux.AddOrRemoveAdornment(this.stonesGrid, (int)cell.Y, (int)cell.X, 
                                                             AdornmentKind.Triangle, this.Game);
                else if (this.IsKeyPressed(VirtualKey.Menu) || this.whatBoardClickCreates == AdornmentKind.Letter)
                    await MainWindowAux.AddOrRemoveAdornment(this.stonesGrid, (int)cell.Y, (int)cell.X, 
                                                             AdornmentKind.Letter, this.Game);
                else {
                    MyDbg.Assert(this.whatBoardClickCreates == AdornmentKind.CurrentMove);
                    var curmove = this.Game.CurrentMove;
                    var row = (int)cell.Y;
                    var col = (int)cell.X;
                    if (curmove != null && curmove.Row == row && curmove.Column == col) {
                        // Light UI affordance to undo misclicks
                        // Note, this does not undo persisting previous move comment.
                        if (curmove.Next == null)
                            this.Game.CutMove();
                        else
                            await GameAux.Message("Tapping last move to undo only works if there is no sub tree " +
                                                  "hanging from it.\nPlease use delete/Cut Move.");
                    }
                    else {
                        // Normal situation, just make move.
                        var move = await this.Game.MakeMove(row, col);
                        if (move != null) {
                            this.AdvanceToStone(move);
                            this.UpdateTreeView(move);
                        }
                    }
                }
                this.FocusOnStones();
            }
        }

        //// passButton_left_down handles creating a passing move.  Also,
        //// mainwin_keydown calls this to handle c-p.
        ////
        private async void passButton_left_down (object sender, RoutedEventArgs e) {
            this.theAppBar.IsOpen = false;
            var move = await this.Game.MakeMove(GoBoardAux.NoIndex, GoBoardAux.NoIndex);
            MyDbg.Assert(move != null);
            this.AdvanceToStone(move);
            this.UpdateTreeView(move);
            this.FocusOnStones();
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
            if (! move.IsPass)
                MainWindowAux.RemoveStone(this.stonesGrid, move);
            if (move.Previous != null)
                this.AddCurrentAdornments(move.Previous);
            else
                MainWindowAux.RestoreAdornments(this.stonesGrid, this.Game.SetupAdornments);
            if (this.Game.CurrentMove != null) {
                this.UpdateTreeView(this.Game.CurrentMove);
                this.UpdateTitle();
            }
            else {
                this.UpdateTreeView(null);
                this.UpdateTitle();
            }
            this.FocusOnStones();
        }

        //// PreviousBranchMove moves back to the closest previous move that has branches.
        //// This function assumes the game is started, and there's a move to rewind.
        //// This handles removing and restoring adornments, and handling the current move
        //// adornment.  
        ////
        private void PreviousBranchMove () {
            // Setup for loop so do not stop on current move if it has branches.
            var move = this.Game.UnwindMove();
            if (! move.IsPass)
                MainWindowAux.RemoveStone(this.stonesGrid, move);
            var curmove = this.Game.CurrentMove;
            // Find previous with branches.
            while (curmove != null && curmove.Branches == null) {
                move = this.Game.UnwindMove();
                if (!move.IsPass)
                    MainWindowAux.RemoveStone(this.stonesGrid, move);
                curmove = move.Previous;
            }
            if (curmove != null) {
                this.AddCurrentAdornments(curmove);
                this.UpdateTreeView(curmove);
                this.UpdateTitle();
            }
            else {
                MainWindowAux.RestoreAdornments(this.stonesGrid, this.Game.SetupAdornments);
                this.UpdateTreeView(null);
                this.UpdateTitle();
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
            var move = await this.Game.ReplayMove();
            if (move == null) {
                // If got null, then next move conflicts with move on the board (likely from pasting moves), or
                // there was an error rendering a parse node.
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
            MainWindowAux.RestoreAdornments(this.stonesGrid, this.Game.SetupAdornments);
            this.UpdateTitle();
            this.UpdateTreeView(null);
            this.FocusOnStones();
        }

        //// endButton_left_down replays all moves to the game end, using currently
        //// selected branches in each move.  This function signals an error if the
        //// game has not started, or no move has been played.
        ////
        private async void endButtonLeftDown (object end_button, RoutedEventArgs e) {
            await this.Game.GotoLastMove(); // This may not succeed due to replaying and rendering nodes.
            this.UpdateTitle();
            this.UpdateTreeView(this.Game.CurrentMove);
            this.FocusOnStones();
        }

        //// brachCombo_SelectionChanged changes the active branch for the next move
        //// of the current move.  Updating_branch_combo is set in update_branch_combo
        //// so that we only update when the user has taken an action as opposed to
        //// programmatically changing the selected item due to arrow keys, deleting moves, etc.
        ////
        private void branchComboSelectionChanged (object branch_dropdown, SelectionChangedEventArgs e) {
            if (updating_branch_combo != true) {
                this.Game.SetCurrentBranch(((ComboBox)branch_dropdown).SelectedIndex);
                this.FocusOnStones();
            }
        }

        //private AdornmentKind[] boardClickChoices = 
        //    {AdornmentKind.CurrentMove, AdornmentKind.Letter, AdornmentKind.Triangle, AdornmentKind.Square};
        //private void boardClickComboSelectionChanged (object sender, SelectionChangedEventArgs e) {
        //    if (this.boardClickCombo == null)
        //        return; // Ignore call during initialization, clicking by default is "Move"
        //    this.whatBoardClickCreates = this.boardClickChoices[this.boardClickCombo.SelectedIndex];
        //}


        //// _advance_to_stone displays move, which as already been added to the
        //// board and readied for rendering.  We add the stone with no current
        //// adornment and then immediately add the adornment because some places in
        //// the code need to just add the stone to the display with no adornments.
        ////
        private void AdvanceToStone (Move move) {
            this.AddNextStoneNoCurrent(move);
            this.AddCurrentAdornments(move);
            this.UpdateTitle();
        }

        //// add_next_stone_no_current adds a stone to the stones grid for move,
        //// removes previous moves current move marker, and removes previous moves
        //// adornments.  This is used for advancing to a stone for replay move or
        //// click to place stone (which sometimes replays a move and needs to place
        //// adornments for pre-existing moves).  The Game class also uses this.
        ////
        public void AddNextStoneNoCurrent (Move move) {
            if (! move.IsPass)
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
            if (! move.IsPass)
                MainWindowAux.AddCurrentStoneAdornment(this.stonesGrid, move);
        }

        //// update_title sets the main window title to display the open
        //// file and current move number.  "Move " is always in the title, set
        //// there by default originally.  Game also uses this.
        ////
        //// Note, originally this used the window title in WPF, but without title bars in
        //// modern/winrt apps, we use part of the main UI to display this info, using two
        //// lines for better display of title/file/dirty vs. move and capture info.  Old
        //// code was "SGF Editor -- [<dirty>] Move #   B captures: #   W captures: #  <file>".
        ////
        public void UpdateTitle () {
            var curMove = this.Game.CurrentMove;
            var num = curMove == null ? 0 : curMove.Number;
            var filebase = this.Game.Filebase;
            var pass_str = (curMove != null && curMove.IsPass) ? " **PASS**" : "";
            var title = "SGF Editor -- " + (this.Game.Dirty ? "[*] " : "") + (filebase != null ? filebase : "");
            var title2 = "Move " + num.ToString() + pass_str +
                         "   Black captures: " + this.Game.BlackPrisoners.ToString() +
                         "   White captures: " + this.Game.WhitePrisoners.ToString();
            this.Title.Text = title;
            this.TitleLine2.Text = title2;
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
            var sf = await this.DoOpenGetFile();
            if (sf == null) return;
            // If file is already open, just show it.
            var gindex = GameAux.ListFind(sf.Name, this.Games, (sfname, game) => ((string)sfname) == ((Game)game).Filename);
            if (gindex != -1) {
                await this.GotoOpenGame(gindex);
                return;
            }
            // Get new open file.
            await DoOpenGetFileGame(sf);
            this.DrawGameTree();
            this.FocusOnStones();
        }


        //// inOpenFileDialog helps handle the situation when the user launches the app and types c-o fast enough
        //// that then the check for an auto-save of an unnamed file puts up a "recursive" dialog, which throws
        //// a System.UnauthorizedAccessException. The OnNavigatedTo lambda that calls CheckUnnamedAutoSave checks
        //// this flag and forgoes the auto-save check on launch if we're already in this function.
        ////
        private bool inOpenFileDialog = false;
        //// DoOpenGetFile prompts user and returns file to open, or null.
        ////
        private async Task<StorageFile> DoOpenGetFile () {
            var fp = new FileOpenPicker();
            fp.FileTypeFilter.Add(".sgf");
            fp.FileTypeFilter.Add(".txt");
            // This randomly throws HRESULT: 0x80070005 (E_ACCESSDENIED)) UnauthorizedAccessException,
            // which I believe is a race condition with CheckUnnamedAutoSave.  See comment for inOpenFileDialog and
            // note if the error occurs here, the check for an auto saved file must have put up a dialog first, or
            // some other dialog showed.  May be able to harden this later if it ever occurs again so that there's
            // a repro case.
            StorageFile sf;
            try {
                this.inOpenFileDialog = true;
                sf = await fp.PickSingleFileAsync();
            }
            catch (UnauthorizedAccessException einfo) {
                var ignoreTask = // Squelch warning that we're not awaiting Message, which we can't in catch blocks.
                GameAux.Message("\"recursive\" dialogs up, probably checking for unnamed auto save on launch"
                                + " and you typed c-o or used Open command quickly after launching app.\n\n"
                                + einfo.Message);
                return null;
            }
            finally { this.inOpenFileDialog = false; }
            return sf;
        }

        //// DoOpenGetFileGame covers the UI so that the user cannot mutate state, and then it
        //// checks for a more recent auto saved file for sf, parsing and creating a game from the
        //// appropriate file.
        ////
        private async Task DoOpenGetFileGame (StorageFile sf) {
            if (sf != null) {
                var curgame = this.Game; // Stash in case we have to undo due to file error.
                Popup popup = null;
                try {
                    // Need to cover UI to block user due to all the awaits from parsing and so on.
                    // Don't want user mutating state after saving the last file and processing the new one.
                    popup = new Popup();
                    popup.Child = this.GetOpenfileUIDike();
                    popup.IsOpen = true;
                    // Process file ...
                    this.LastCreatedGame = null;
                    await this.GetFileGameCheckingAutoSave(sf);
                }
                catch (IOException err) {
                    // Essentially handles unexpected EOF or malformed property values.
                    // Should be nothing to do since curgame should still be intact if never got past reading file.
                    var ignoreTask = // Squelch warning that we're not awaiting Message, which we can't in catch blocks.
                    GameAux.Message("IO Error with opening or reading file.\n\n" + err.Message + "\n\n" + err.StackTrace);
                }
                catch (Exception err) {
                    // No code paths should throw from incomplete, unrecoverable state, so should be fine to continue.
                    // For example, game state should be intact (other than IsDirty) for continuing.
                    if (this.LastCreatedGame != null) {
                        // Error after creating game, so remove it and reset board to last game or default game.
                        // The games list may not contain g because creating new games may delete the initial default game.
                        // Must do this before Message call since cannot await it.
                        this.UndoLastGameCreation(this.Games.Contains(curgame) ? curgame : null);
                    }
                    var ignoreTask = // Squelch warning that we're not awaiting Message, which we can't in catch blocks.
                    GameAux.Message("IO Error or SGF formatting issue.\n\n" + err.Message + "\n\n" + err.StackTrace);
                    // NOTE: because not awaiting Message, the following code executes immediately.
                }
                finally {
                    popup.IsOpen = false;
                    popup.Child = null;  // Need to explicitly unparent UI tree, or get weird race condition UI bug.
                }
            }
        }

        //// GetFileGameCheckingAutoSave checks the auto save file and prompts user whether to
        //// use it.  If we use the auto save file, then we need to mark game dirty since the file
        //// is not up to date.  We also delete the auto save file at this point.  This is public
        //// for code in app.xaml.cs to call.
        ////
        public async Task GetFileGameCheckingAutoSave (StorageFile sf) {
            StorageFile autoSf = await this.GetAutoSaveFile(this.GetAutoSaveName(sf.Name));
            if (autoSf != null) {
                if (autoSf.DateCreated > sf.DateCreated &&
                        await GameAux.Message("Found more recent auto saved file for " + sf.Name + ".",
                                              "Confirm opening auto saved file",
                                              new List<string>() {"Open newer auto saved version", 
                                                                  "Open older original file"})
                            == "Open newer auto saved version") {
                    await this.ParseAndCreateGame(autoSf);
                    this.Game.Dirty = true; // actual file is not saved up to date
                    // Persist actual file name and storage for future save operations.
                    this.Game.SaveGameFileInfo(sf);
                    // Though ParseAndCreateGame updates title, do it again with correct file name.
                    this.UpdateTitle();
                }
                else {
                    await this.ParseAndCreateGame(sf);
                }
                await autoSf.DeleteAsync();
            }
            else {
                await this.ParseAndCreateGame(sf);
            }
        }

        //// UnnamedAutoSaveName and GetAutoSaveName determine how auto saving names the temporary file.
        //// These are public for code in app.xaml.cs to call.
        ////
        public const string UnnamedAutoSaveName = "unnamed-new-game.sgf";
        public string GetAutoSaveName (string file) {
            if (file == null)
                return MainWindow.UnnamedAutoSaveName;
            else
                return file.Substring(0, file.Length - 4) + "-autosave.sgf";
        }

        //// GetAutoSaveFile checks if the file exists, returning the StorageFile if it does.
        //// This is public so that game.writegame can use it to clean up autosave file.
        ////
        public async Task<StorageFile> GetAutoSaveFile (string autosaveName) {
            var tempFolder = ApplicationData.Current.TemporaryFolder;
            StorageFile autoSf = null;
            try {
                autoSf = await tempFolder.GetFileAsync(autosaveName);
            }
            catch (FileNotFoundException) { }
            return autoSf;
        }


        //// ParseAndCreateGame does just that.  DoOpenButton above and app.xaml.cs's OnFileActivated use this.
        ////
        public async Task ParseAndCreateGame (StorageFile sf) {
            var pg = await ParserAux.ParseFile(sf);
            //this.Game = await GameAux.CreateParsedGame(pg, this);
            await GameAux.CreateParsedGame(pg, this);
            //await Task.Delay(5000);  // Testing input blocker.
            this.Game.SaveGameFileInfo(sf);
            this.UpdateTitle();
        }

        //// GetOpenfileUIDike returns a re-used grid to eat input after commiting to open a file.
        //// DoOpenGetFileGame uses this.
        ////
        private Grid openFileUIDike = null;
        private Grid GetOpenfileUIDike () {
            if (this.openFileUIDike == null)
                this.openFileUIDike = GetUIDike();
            return this.openFileUIDike;
        }

        //// GetUIDike returns a grid with a control to eat input to be used in operations where user
        //// shouldn't mutate the model after committing to save or delete or whatnot.
        ////
        //// For some weird reason, re-using openFileUiDike grid in onNavigatedToCheckAutoSave and
        //// GetOpenfileUIDike for DoOpenGetFileGame threw an error that something changed size, but
        //// the window, grid, textbox should all be the same, WTF?! :-)
        ////
        private Grid GetUIDike () {
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
            return g;
        }

        //// CheckDirtySave prompts whether to save the game if it is dirty.  If
        //// saving, then it uses the game filename, or prompts for one if it is null.
        //// This is public for use in app.xaml.cs OnfileActivated, and it takes a game
        //// optionally for checking a game that is not this.game when deleting games.
        ////
        public async Task CheckDirtySave (Game g = null) {
            if (g == null)
                g = this.Game;
            g.SaveCurrentComment();
            if (g.Dirty &&
                    await GameAux.Message("Game is unsaved, save it?", "Confirm saving file",
                                    new List<string>() {"Yes", "No"}) == GameAux.YesMessage) {
                StorageFile sf;
                if (g.Storage != null) {
                    sf = g.Storage;
                }
                else {
                    sf = await MainWindowAux.GetSaveFilename();
                }
                if (sf != null)
                    await g.WriteGame(sf);
            }
        }


        //// newButton_left_down starts a new game after checking to save the
        //// current game if it is dirty.
        ////
        private async void newButton_left_down(object new_button, RoutedEventArgs e) {
            await DoNewButton();
        }

        //// DoNewButton can be re-used where we need to use 'await'.  Can't 'await'
        //// newButtonLeftDown since it must be 'async void' due to being an event handler.
        ////
        private async Task DoNewButton () {
            await this.CheckDirtySave();
            // Setup new dialog and show it
            var newDialog = new NewGameDialog();
            var popup = new Popup();
            newDialog.NewGameDialogClose += (s, e) => { 
                popup.IsOpen = false;
                this.NewGameDialogDone(newDialog); 
            };
            popup.Child = newDialog;
            popup.IsOpen = true;
            // Put focus into dialog, good for user, but also stops mainwindow from handling kbd events
            ((NewGameDialog)popup.Child).WhiteTextBox.IsEnabled = true;
            ((NewGameDialog)popup.Child).WhiteTextBox.IsTabStop = true;
            ((NewGameDialog)popup.Child).WhiteTextBox.IsHitTestVisible = true;
            ((NewGameDialog)popup.Child).WhiteTextBox.Focus(FocusState.Keyboard);
        }

        //// NewGameDialogDone handles when the new game dialog popup is done.
        //// It checks whether the dialog was confirmed or cancelled, and takes
        //// appropriate action.
        ////
        private async void NewGameDialogDone (NewGameDialog dlg) {
            if (this.theAppBar.IsOpen)
                // If launched from appbar, and it remained open, close it.
                this.theAppBar.IsOpen = false;
            if (dlg.NewGameConfirmed) {
                var white = dlg.WhiteText;
                var black = dlg.BlackText;
                var size = dlg.SizeText;
                var handicap = dlg.HandicapText;
                var komi = dlg.KomiText;
                var sizeInt = int.Parse(size);
                var handicapInt = int.Parse(handicap);
                if (sizeInt != 19) {
                    await GameAux.Message("Size must be 19 for now.");
                    return;
                }
                if (handicapInt < 0 || handicapInt > 9) {
                    await GameAux.Message("Handicap must be 0 to 9.");
                    return;
                }
                var g = GameAux.CreateGame(this, sizeInt, handicapInt, komi);
                g.PlayerBlack = black;
                g.PlayerWhite = white;
                this.Game = g;
                this.UpdateTitle();
                this.DrawGameTree();
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

        //// SaveAs gets a file, opens it, saves, and keeps file info in game.
        //// This is public because Game.cs (controller) needs to call this if previously opened file is no longer good.
        ////
        public async Task SaveAs () {
            var sf = await MainWindowAux.GetSaveFilename();
            if (sf != null) {
                this.Game.SaveCurrentComment(); // Persist UI edits to model.
                await this.Game.WriteGame(sf);
            }
        }

        private async Task SaveFlippedFile() {
            var sf = await MainWindowAux.GetSaveFilename("Save Flipped File");
            if (sf != null)
                await this.Game.WriteFlippedGame(sf);
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
            // Ensure focus on board where general commands are dispatched.
            if (e.Key == VirtualKey.Escape) {
                this.Game.SaveCurrentComment();
                this.UpdateTitle();
                win.FocusOnStones();
                e.Handled = true;
                return;
            }
            // Previous move
            if (e.Key == VirtualKey.Left && this.commentBox.FocusState != FocusState.Keyboard &&
                // Kbd focus covers tabbing to comment box, and pointer covers clicking on it
                this.commentBox.FocusState != FocusState.Pointer && win.Game.CanUnwindMove()) {
                if (this.IsKeyPressed(VirtualKey.Control))
                    this.PreviousBranchMove();
                else
                    this.prevButtonLeftDown(null, null);
                e.Handled = true;
            }
            // Next move
            else if (e.Key == VirtualKey.Right && this.commentBox.FocusState != FocusState.Keyboard &&
                     // Kbd focus covers tabbing to comment box, and pointer covers clicking on it
                     this.commentBox.FocusState != FocusState.Pointer && win.Game.CanReplayMove()) {
                await this.DoNextButton();
                e.Handled = true;
            }
            // Initial board state
            else if (e.Key == VirtualKey.Home && this.commentBox.FocusState != FocusState.Keyboard &&
                     // Kbd focus covers tabbing to comment box, and pointer covers clicking on it
                     this.commentBox.FocusState != FocusState.Pointer && win.Game.CanUnwindMove()) {
                this.homeButtonLeftDown(null, null);
                e.Handled = true;
            }
            // Last move
            else if (e.Key == VirtualKey.End && this.commentBox.FocusState != FocusState.Keyboard &&
                     // Kbd focus covers tabbing to comment box, and pointer covers clicking on it
                     this.commentBox.FocusState != FocusState.Pointer && win.Game.CanReplayMove()) {
                this.endButtonLeftDown(null, null);
                e.Handled = true;
            }
            // Move branch down
            else if (e.Key == VirtualKey.Down && this.IsKeyPressed(VirtualKey.Control) &&
                     this.commentBox.FocusState != FocusState.Keyboard && // Covers tabbing to it
                     this.commentBox.FocusState != FocusState.Pointer) {  // Covers clicking on it
                await this.Game.MoveBranchDown();
                e.Handled = true;
            }
            // Move branch up
            else if (e.Key == VirtualKey.Up && this.IsKeyPressed(VirtualKey.Control) &&
                     this.commentBox.FocusState != FocusState.Keyboard && // Covers tabbing to it
                     this.commentBox.FocusState != FocusState.Pointer) {  // Covers clicking on it
                await this.Game.MoveBranchUp();
                e.Handled = true;
            }
            // Display next branch
            else if (e.Key == VirtualKey.Down && this.branchCombo.Items.Count > 0 &&
                     this.commentBox.FocusState != FocusState.Keyboard && // Covers tabbing to it
                     this.commentBox.FocusState != FocusState.Pointer) {  // Covers clicking on it
                MainWindowAux.SetCurrentBranchDown(this.branchCombo, this.Game);
                e.Handled = true;
            }
            // Display previous branch
            else if (e.Key == VirtualKey.Up && this.branchCombo.Items.Count > 0 &&
                     this.commentBox.FocusState != FocusState.Keyboard && // Covers tabbing to it
                     this.commentBox.FocusState != FocusState.Pointer) {  // Covers clicking on it
                MainWindowAux.SetCurrentBranchUp(this.branchCombo, this.Game);
                e.Handled = true;
            }
            // Opening a file
            else if (e.Key == VirtualKey.O && //this.CtrlKeyPressed) 
                     this.IsKeyPressed(VirtualKey.Control)) {
                await this.DoOpenButton();
                e.Handled = true;
            }
            // Game info
            else if (e.Key == VirtualKey.I && this.IsKeyPressed(VirtualKey.Control)) {
                this.AppBarGameInfoClick(null, null);
                e.Handled = true;
            }
            // Explicit Save As
            else if (e.Key == VirtualKey.S &&
                     this.IsKeyPressed(VirtualKey.Control) && this.IsKeyPressed(VirtualKey.Menu)) {
                await this.SaveAs();
                // There is some bug in win8 that prevents this call to FocusOnstones from working,
                // or it seems that way.  It actually appears the app is displayed but has lost focus.
                // If you alt-tab, the app flashes and keeps focus (does not switch to another app),
                // and focus is in deed on the board.
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
                     this.commentBox.FocusState != FocusState.Keyboard && // Covers tabbing to it
                     this.commentBox.FocusState != FocusState.Pointer) {  // Covers clicking on it
                await SaveFlippedFile();
                e.Handled = true;
            }
            // New game
            else if (e.Key == VirtualKey.N && this.IsKeyPressed(VirtualKey.Control)) {
                await this.DoNewButton();
                e.Handled = true;
            }
            // Cutting a sub tree
            else if ((e.Key == VirtualKey.Delete || (e.Key == VirtualKey.X && this.IsKeyPressed(VirtualKey.Control))) &&
                     this.commentBox.FocusState != FocusState.Keyboard && // Covers tabbing to it
                     this.commentBox.FocusState != FocusState.Pointer &&  // Covers clicking on it
                     win.Game.CanUnwindMove() &&
                     await GameAux.Message("Cut current move from game tree?", "Confirm cutting move",
                                           new List<string>() {"Yes", "No"}, 1, 1) ==
                         GameAux.YesMessage) {
                win.Game.CutMove();
                this.appBarPasteButton.IsEnabled = true;
                e.Handled = true;
            }
            // Pasting a sub tree
            else if (e.Key == VirtualKey.V && this.IsKeyPressed(VirtualKey.Control) &&
                     this.commentBox.FocusState != FocusState.Keyboard && // Covers tabbing to txt box
                     this.commentBox.FocusState != FocusState.Pointer) {  // Covers clicking in txt box
                if (win.Game.CanPaste())
                    await win.Game.PasteMove();
                else
                    await GameAux.Message("No cut move to paste at this time.");
                this.appBarPasteButton.IsEnabled = true;
                e.Handled = true;
            }
            // Pass move
            else if (e.Key == VirtualKey.P && this.IsKeyPressed(VirtualKey.Control) &&
                     this.commentBox.FocusState != FocusState.Keyboard && // Covers tabbing to txt box
                     this.commentBox.FocusState != FocusState.Pointer) {  // Covers clicking in txt box
                this.passButton_left_down(null, null);
                e.Handled = true;
            }
            // Switch Windows/Games
            else if (e.Key == VirtualKey.W && this.IsKeyPressed(VirtualKey.Control) &&
                     this.commentBox.FocusState != FocusState.Keyboard && // Covers tabbing to txt box
                     this.commentBox.FocusState != FocusState.Pointer) {  // Covers clicking in txt box
                if (this.IsKeyPressed(VirtualKey.Shift))
                    await this.GotoNextGame(-1);
                else
                    await this.GotoNextGame();
                e.Handled = true;
            }
            else if (e.Key == VirtualKey.G && this.IsKeyPressed(VirtualKey.Control) &&
                     this.commentBox.FocusState != FocusState.Keyboard && // Covers tabbing to txt box
                     this.commentBox.FocusState != FocusState.Pointer) {  // Covers clicking in txt box
                await this.ShowGamesGoto();
                e.Handled = true;
            }
            // Close Window/Game
            else if (e.Key == VirtualKey.F4 && this.IsKeyPressed(VirtualKey.Control) &&
                     this.commentBox.FocusState != FocusState.Keyboard && // Covers tabbing to txt box
                     this.commentBox.FocusState != FocusState.Pointer) {  // Covers clicking in txt box
                await this.CloseGame(this.Game);
                e.Handled = true;
            }
            // Special Bill command because I do this ALL THE TIME
            // Delete entire comment.
            else if (e.Key == VirtualKey.K && this.IsKeyPressed(VirtualKey.Control)) {
                this.UpdateCurrentComment("");
                win.FocusOnStones();
                e.Handled = true;
                return;
            }
            // Special Bill command because I do this ALL THE TIME
            // Delete question mark at end of first line -- c-?.
            else if (((int)e.Key) == 191 && this.IsKeyPressed(VirtualKey.Shift) &&
                     this.IsKeyPressed(VirtualKey.Control)) {
                await this.FirstRhetoricalLineToStatement();
                win.FocusOnStones();
                e.Handled = true;
                return;
            }
            // Special Bill command because I do this ALL THE TIME
            // Delete line indicated by c-N keypress.
            else if (e.Key > VirtualKey.Number0 && e.Key < VirtualKey.Number6 &&
                     this.IsKeyPressed(VirtualKey.Control)) {
                await this.DeleteCommentLine(((int)e.Key) - ((int)VirtualKey.Number0));
                win.FocusOnStones();
                e.Handled = true;
                return;
            }
            // Special Bill command because I do this ALL THE TIME
            // Replace indexed move reference for current move with "this".
            else if (e.Key == VirtualKey.T && this.IsKeyPressed(VirtualKey.Control)) {
                this.ReplaceIndexedMoveRef();
                win.FocusOnStones();
                e.Handled = true;
                return;
            }
            // Special Bill command because I do this ALL THE TIME
            // Replace indexed move reference with "marked", "square marked", "marked group", etc.
            else if (e.Key == VirtualKey.M && this.IsKeyPressed(VirtualKey.Control)) {
                this.ReplaceIndexedMarkedRef();
                win.FocusOnStones();
                e.Handled = true;
                return;
            }
            // Help
            else if (e.Key == VirtualKey.F1) {
                this.ShowHelp();
                e.Handled = true;
            }
            else {
                // Tell me what key I pressed.
                //await GameAux.Message(e.Key.ToString() + (this.IsKeyPressed(VirtualKey.Shift)).ToString());
                }
        }  // mainWin_keydown



        //// gameTree_mousedown handles clicks on the game tree graph canvas,
        //// navigating to the move clicked on.
        ////
        private async void gameTree_mousedown (object sender, PointerRoutedEventArgs e) {
            // Integrity checking code for debugging and testing, not for release.
            //this.CheckTreeParsedNodes();
            //
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
            if (this.Game.CurrentMove != null) { // Same as checking game state is not NotStarted.
                this.Game.GotoStart();
                MainWindowAux.RestoreAdornments(this.stonesGrid, this.Game.SetupAdornments);
            }
            else
                this.Game.SaveCurrentComment();
            if (move != null) {
                if (move.Row != -1 && move.Column != -1) {
                    // move is NOT dummy move for start node of game tree view, so advance to it.
                    if (! this.GotoGameTreeMove(move)) {
                        // Hit conflicting move location due to pasted node or rendering bad parsed node
                        // Do not go back to start, then user sees where the issue is.
                        await GameAux.Message("You are replaying moves from a pasted branch that has conflicts " +
                                              "with stones on the board, or replaying moves with bad properties " +
                                              "from an SGF file.");
                    }
                }
            }
            else {
                // Move is ParsedNode, not a move that's been rendered.
                if (! this.GotoGameTreeMove((ParsedNode)n.Node))
                    // Hit conflicting move location due to pasted node or rendering bad parsed node
                    await GameAux.Message("You are replaying moves from a pasted branch that has conflicts " +
                                          "with stones on the board, or replaying moves with bad properties " + 
                                          "from an SGF file.");
            }
            this.UpdateTitle();
            this.UpdateTreeView(this.Game.CurrentMove);
            this.FocusOnStones();
        }

        private bool GotoGameTreeMove (Move move) {
            var res = true;
            var path = this.Game.GetPathToMove(move);
            if (path != this.Game.TheEmptyMovePath) {
                if (! this.Game.AdvanceToMovePath(path)) res = false; // conflicting stone loc or bad parse node
                // Do not update UI using move, use CurrentMove because we didn't make it to move's position.
                GotoGameTreeMoveUpdateUI(this.Game.CurrentMove);
            }
            return res;
        }

        private bool GotoGameTreeMove (ParsedNode move) {
            var res = true;
            var path = this.Game.GetPathToMove(move);
            if (path != this.Game.TheEmptyMovePath) {
                if (! this.Game.AdvanceToMovePath(path)) res = false; // conflicting stone loc or bad parse node
                this.GotoGameTreeMoveUpdateUI(this.Game.CurrentMove);
            }
            return res;
        }

        //// GotoGameTreeMoveUpdateUI sets the forw/back buttons appropriately.  It also
        //// stores move's comment in the comment box.  We do not need to use the save
        //// and update function because we reset to the board start, saving any current
        //// comment at that time, and now just need to put the current comment in the UI.
        ////
        private void GotoGameTreeMoveUpdateUI(Move move) {
            // If move is null, there was an error advancing to the first move.  this.currentmove is still 
            // null from going to start.  All other UI should be correct from gametree_mousedown, nothing
            // changed by AdvanceToMovePath.
            if (move != null) {
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
                this.CurrentComment = move.Comments;
            }
        }


        ////
        //// Handling button panel for what clicking on the board does
        ////

        //// All the handlers and helpers for the App Bar buttons that let you set what clicking
        //// on the board does have been commented out in the xaml and here in lieu of the combo
        //// box in the main UI (upper right command panel).
        ////
        private void MoveButtonClick (object sender, RoutedEventArgs e) {
            // If null args, then called while setting up new game and just ensuring board clicks make moves is default.
            this.MoveButton.IsEnabled = false;
            this.MoveButton.BorderThickness = new Thickness(1);
            this.TriangleButton.IsEnabled = true;
            this.LetterButton.IsEnabled = true;
            this.SquareButton.IsEnabled = true;
            if (! (sender == null && e == null && this.whatBoardClickCreates == AdornmentKind.CurrentMove))
                // Ensure we clear the border when some other button was selected and NOT when we're just
                // calling this explicitly to ensure new games have the move button selected.
                this.ClearBoardClickButtonBorder();
            this.whatBoardClickCreates = AdornmentKind.CurrentMove;
        }

        private void TriangleButtonClick (object sender, RoutedEventArgs e) {
            this.TriangleButton.IsEnabled = false;
            this.TriangleButton.BorderThickness = new Thickness(1);
            this.MoveButton.IsEnabled = true;
            this.LetterButton.IsEnabled = true;
            this.SquareButton.IsEnabled = true;
            this.ClearBoardClickButtonBorder();
            this.whatBoardClickCreates = AdornmentKind.Triangle;
        }

        private void LetterButtonClick (object sender, RoutedEventArgs e) {
            // Do not disable this button because winRT insists on changing foreground after I set it to white.
            //this.LetterButton.IsEnabled = false;
            this.LetterButton.BorderThickness = new Thickness(1);
            //this.LetterButton.Foreground = new SolidColorBrush(Colors.White);
            this.TriangleButton.IsEnabled = true;
            this.MoveButton.IsEnabled = true;
            this.SquareButton.IsEnabled = true;
            // Hack test if we're already the selected button since we never disable this one.
            // Since we do not disable, we need to make sure tapping it twice does not remove border.
            if (this.whatBoardClickCreates != AdornmentKind.Letter) {
                this.ClearBoardClickButtonBorder();
                this.whatBoardClickCreates = AdornmentKind.Letter;
            }
        }

        private void SquareButtonClick (object sender, RoutedEventArgs e) {
            this.SquareButton.IsEnabled = false;
            this.SquareButton.BorderThickness = new Thickness(1);
            this.LetterButton.IsEnabled = true;
            this.TriangleButton.IsEnabled = true;
            this.MoveButton.IsEnabled = true;
            this.ClearBoardClickButtonBorder();
            this.whatBoardClickCreates = AdornmentKind.Square;
        }

        private void ClearBoardClickButtonBorder () {
            switch (this.whatBoardClickCreates) {
                case AdornmentKind.CurrentMove:
                    this.MoveButton.BorderThickness = new Thickness(0);
                    break;
                case AdornmentKind.Triangle:
                    this.TriangleButton.BorderThickness = new Thickness(0);
                    break;
                case AdornmentKind.Square:
                    this.SquareButton.BorderThickness = new Thickness(0);
                    break;
                case AdornmentKind.Letter:
                    this.LetterButton.BorderThickness = new Thickness(0);
                    break;
            }
        }


        ////
        //// App Bar Input Handling
        ////

        //// All the handlers and helpers for the App Bar buttons that let you set what clicking
        //// on the board does have been commented out in the xaml and here in lieu of the combo
        //// box in the main UI (upper right command panel).
        ////
        //private void AppBarMoveButtonClick(object sender, RoutedEventArgs e) {
        //    SetAppBarText("Moves");
        //    this.appBarMoveButton.IsEnabled = false;
        //    this.appBarTriangleButton.IsEnabled = true;
        //    this.appBarLetterButton.IsEnabled = true;
        //    this.appBarSquareButton.IsEnabled = true;
        //    this.whatBoardClickCreates = AdornmentKind.CurrentMove;
        //}

        //private void AppBarTriangleButtonClick(object sender, RoutedEventArgs e) {
        //    SetAppBarText("Triangles");
        //    this.appBarTriangleButton.IsEnabled = false;
        //    this.appBarMoveButton.IsEnabled = true;
        //    this.appBarLetterButton.IsEnabled = true;
        //    this.appBarSquareButton.IsEnabled = true;
        //    this.whatBoardClickCreates = AdornmentKind.Triangle;
        //}

        //private void AppBarLetterButtonClick(object sender, RoutedEventArgs e) {
        //    SetAppBarText("Letters");
        //    this.appBarLetterButton.IsEnabled = false;
        //    this.appBarTriangleButton.IsEnabled = true;
        //    this.appBarMoveButton.IsEnabled = true;
        //    this.appBarSquareButton.IsEnabled = true;
        //    this.whatBoardClickCreates = AdornmentKind.Letter;
        //}

        //private void AppBarSquareButtonClick(object sender, RoutedEventArgs e) {
        //    SetAppBarText("Squares");
        //    this.appBarSquareButton.IsEnabled = false;
        //    this.appBarLetterButton.IsEnabled = true;
        //    this.appBarTriangleButton.IsEnabled = true;
        //    this.appBarMoveButton.IsEnabled = true;
        //    this.whatBoardClickCreates = AdornmentKind.Square;
        //}

        //private void SetAppBarText(string kind) {
        //    var txt = this.appBarClickText.Text;
        //    var loc = txt.IndexOf(" ... ");
        //    MyDbg.Assert(loc != -1);
        //    this.appBarClickText.Text = txt.Substring(0, loc + 5) + kind;
        //}

        //// AppBarGameInfoClick sets up the misc game info users mostly never edit, displays
        //// a big dialog to edit that stuff, and sets up event handler for when dialog is done.
        ////
        private void AppBarGameInfoClick (object sender, RoutedEventArgs e) {
            if (this.Game.ParsedGame != null) {
                if (this.Game.MiscGameInfo == null) {
                    // If misc properties still in parsed structure, capture them all to pass them through
                    // if user saves file.  After editing them, MiscGameInfo supercedes parsed structure.
                    this.Game.MiscGameInfo = GameAux.CopyProperties(this.Game.ParsedGame.Nodes.Properties);
                }
            }
            else if (this.Game.MiscGameInfo == null)
                this.Game.MiscGameInfo = new Dictionary<string, List<string>>();
            // Just in case we're on the empty board state and comment is modified.
            this.Game.SaveCurrentComment();
            var newDialog = new GameInfo(this.Game);
            var popup = new Popup();
            newDialog.GameInfoDialogClose += (s, args) => {
                popup.IsOpen = false;
                this.GameInfoDialogDone(newDialog);
            };
            popup.Child = newDialog;
            popup.IsOpen = true;
            // Put focus into dialog, good for user, but also stops mainwindow from handling kbd events
            ((GameInfo)popup.Child).PlayerBlackTextBox.IsEnabled = true;
            ((GameInfo)popup.Child).PlayerBlackTextBox.IsTabStop = true;
            ((GameInfo)popup.Child).PlayerBlackTextBox.IsHitTestVisible = true;
            ((GameInfo)popup.Child).PlayerBlackTextBox.Focus(FocusState.Keyboard);
        }

        //// GameInfoDialogDone handles when the game info dialog popup is done.
        //// It checks whether the dialog was confirmed or cancelled, and takes
        //// appropriate action.
        ////
        private void GameInfoDialogDone (GameInfo dlg) {
            if (this.theAppBar.IsOpen)
                // If launched from appbar, and it remained open, close it.
                this.theAppBar.IsOpen = false;
            if (dlg.GameInfoConfirmed) {
                if (this.Game.CurrentMove == null && dlg.CommentChanged)
                    this.commentBox.Text = this.Game.Comments;
                // Update title in case dirty flag changed.
                this.UpdateTitle();
            }
            this.theAppBar.IsOpen = false;
            this.FocusOnStones();
        }


        private async void AppBarGotoGame (object sender, RoutedEventArgs e) {
            await this.ShowGamesGoto();
        }

        private async void AppBarSaveAsClick (object sender, RoutedEventArgs e) {
            await this.SaveAs();
            this.FocusOnStones();
        }

        private async void AppBarSaveFlippedClick(object sender, RoutedEventArgs e) {
            await this.SaveFlippedFile();
            this.FocusOnStones();
        }

        private async void AppBarMoveUpClick(object sender, RoutedEventArgs e) {
            await this.Game.MoveBranchUp();
        }

        private async void AppBarMoveDownClick(object sender, RoutedEventArgs e) {
            await this.Game.MoveBranchDown();
        }

        private async void AppBarCutClick(object sender, RoutedEventArgs e) {
            if (this.Game.CanUnwindMove() &&
                    await GameAux.Message("Cut current move from game tree?",
                                          "Confirm cutting move", new List<string>() { "Yes", "No" }) ==
                              GameAux.YesMessage) {
                this.Game.CutMove();
                this.appBarPasteButton.IsEnabled = true;
            }
        }

        private async void AppBarPasteClick(object sender, RoutedEventArgs e) {
            MyDbg.Assert(this.Game.CanPaste());
            //if (this.Game.CanPaste())
                await this.Game.PasteMove();
            //else
            //    await GameAux.Message("No cut move to paste at this time.");
            this.appBarPasteButton.IsEnabled = false;
            this.FocusOnStones();
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
        private TreeViewNode treeViewSelectedItem;

        //// InitializeTreeView is used by SetupBoardDisplay.  It clears the canvas and sets its size.
        ////
        private void InitializeTreeView () {
            var canvas = this.gameTreeView;
            canvas.Children.Clear(); //.RemoveRange(0, canvas.Children.Count);
            this.treeViewMoveMap.Clear();
            this.SetTreeViewSize();
            this.UpdateTreeClearBranchHighlight();
        }

        private void SetTreeViewSize () {
            var canvas = this.gameTreeView;
            canvas.Width = GameAux.TreeViewGridColumns * MainWindowAux.treeViewGridCellSize;
            canvas.Height = GameAux.TreeViewGridRows * MainWindowAux.treeViewGridCellSize;
        }


        //// CheckTreeParsedNodes is some integrity checking code that found some violations in the
        //// parsednodes and tree drawing maps.  The calls to this are commented out as well as the defs.
        ////
        //public void CheckTreeParsedNodes () {
        //    foreach (var kv in this.treeViewMoveMap) {
        //        var n = kv.Value;
        //        if (kv.Key != n.Node) {
        //            if (n.Kind == TreeViewNodeKind.StartBoard) {
        //                continue;
        //            }
        //            else {
        //                Debugger.Break();
        //            }
        //        }
        //        var pn = n.Node as ParsedNode;
        //        if (! ((pn != null) ? NodeInModel(pn) : NodeInModel((Move)n.Node)))
        //            Debugger.Break();
        //    }
        //}
        //private bool NodeInModel (ParsedNode n) {
        //    var pg = this.Game.ParsedGame;
        //    if (pg.Nodes == n) return true;
        //    var cur = n;
        //    var parent = cur.Previous;
        //    while (parent != pg.Nodes) {
        //        if (parent == null)
        //            return false;
        //        cur = parent;
        //        parent = parent.Previous;
        //    }
        //    return true;
        //}

        //private bool NodeInModel (Move n) {
        //    var first = this.Game.FirstMove;
        //    if (n == first) return true;
        //    var cur = n;
        //    var parent = cur.Previous;
        //    while (parent != first) {
        //        if (parent == null) {
        //            if (this.Game.Branches != null && this.Game.Branches.Contains(cur))
        //                return true;
        //            else
        //                return false;
        //        }
        //        cur = parent;
        //        parent = parent.Previous;
        //    }
        //    return true;
        //}

        //// DrawGameTree gets a view model of the game tree, creates objects to put on the canvas,
        //// and sets up the mappings for updating the view as we move around the game tree.  This
        //// also creates a special "start" mapping to get to the first view model node.  This is
        //// public for app.xaml.cs's OnFileAcivated to call it.
        ////
        public void DrawGameTree (bool force = false) {
            if (this.TreeViewDisplayed() || force)
                // considered premature optimization of re-using objects from this.treeViewMoveMap, but no need
                this.InitializeTreeView();
            var treeModel = GameAux.GetGameTreeModel(this.Game);
            // Set canvas size in case computing tree model had to grow model structures.
            this.SetTreeViewSize();
            var canvas = this.gameTreeView;
            for (var i = 0; i < GameAux.TreeViewGridRows; i++) {
                for (var j = 0; j < GameAux.TreeViewGridColumns; j++) {
                    var curModel = treeModel[i, j];
                    if (curModel != null) {
                        if (curModel.Kind != TreeViewNodeKind.LineBend) {
                            var eltGrid = MainWindowAux.NewTreeViewItemGrid(curModel);
                            curModel.Cookie = eltGrid;
                            var node = curModel.Node;
                            var pnode = node as ParsedNode;
                            var mnode = node as Move;
                            if ((pnode != null && pnode.Properties.ContainsKey("C")) ||
                                (mnode != null && mnode.Comments != "")) {
                                eltGrid.Background = new SolidColorBrush(Colors.LightGreen);
                            }
                            this.treeViewMoveMap[node] = curModel;
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
            //Grid cookie = (Grid)treeModel[0, 0].Cookie;
            // Set this to something so that UpdateTreeView doesn't deref null.
            this.treeViewSelectedItem = treeModel[0, 0]; // cookie;
            this.UpdateTreeView(this.Game.CurrentMove);
        }


        //// UpdateTreeView moves the current move highlighting as the user moves around in the
        //// tree.  Various command handlers call this after they update the model.  Eventually,
        //// this will look at some indication to redraw whole tree (cut, paste, maybe add move).
        ////
        public void UpdateTreeView (Move move, bool wipeit = false) {
            MyDbg.Assert(this.TreeViewDisplayed());
            if (wipeit) {
                this.InitializeTreeView();
                this.DrawGameTree();
            }
            else {
                UpdateTreeHighlightMove(move);
            }
            UpdateTreeViewBranch(move);

        }

        //// UpdateTreeHighlightMove handles the primary case of UpdateTreeView, moving the
        //// highlight from the last current move the new current move, and managing if the old
        //// current move gets a green highlight for having a comment.
        ////
        private Rectangle currentNodeRect = null;
        private Grid currentNodeGrid = null;
        private void UpdateTreeHighlightMove (Move move) {
            // Ensure we have the box that helps the current node be highlighted.
            if (this.currentNodeRect == null) {
                this.currentNodeRect = new Rectangle();
                this.currentNodeRect.Stroke = new SolidColorBrush(Colors.Gray);
                //this.currentNodeRect.StrokeThickness = 0.7;
            }
            TreeViewNode curMoveItem = this.TreeViewNodeForMove(move);
            if (curMoveItem == null) {
                // move is not in tree view node map, so it is new.  For simplicity, just redraw entire tree.
                // This is fast enough to 300 moves at least, but could add optimization to notice adding move
                // to end of branch with no node in the way for adding new node.
                this.InitializeTreeView();
                this.DrawGameTree();
            }
            else {
                UpdateTreeHighlightMoveAux(curMoveItem);
            }
        }

        private void UpdateTreeHighlightMoveAux (TreeViewNode curItem) {
            Grid curItemCookie = ((Grid)curItem.Cookie);
            // Get previous current move node (snode) and save new current move node (item)
            var prevNode = this.treeViewSelectedItem;
            var prevItem = (Grid)prevNode.Cookie;
            this.treeViewSelectedItem = curItem; 
            // Clear current node box that helps you see it highlighted, but don't use sitem since the grid
            // holding the box might have been thrown away if we wiped out the entire tree.
            if (this.currentNodeGrid != null)
                this.currentNodeGrid.Children.Remove(this.currentNodeRect);
            // Get model object and see if previous current move has comments
            var pnode = prevNode.Node as ParsedNode;
            var mnode = prevNode.Node as Move;
            if ((pnode != null && pnode.Properties.ContainsKey("C")) ||
                (mnode != null && (mnode.Comments != "" ||
                                   // or mnode is dummy node representing empty board
                                   (mnode.Row == -1 && mnode.Column == -1 && this.Game.Comments != "")))) {
                // Nodes with comments are green
                prevItem.Background = new SolidColorBrush(Colors.LightGreen);
            }
            else
                // Those without comments are transparent
                prevItem.Background = new SolidColorBrush(Colors.Transparent);
            // Update current move shading and bring into view.
            curItemCookie.Background = new SolidColorBrush(Colors.Fuchsia);
            curItemCookie.Children.Add(this.currentNodeRect);
            this.currentNodeGrid = curItemCookie; // Save grid so that we can remove the Rect correctly.
            this.BringTreeElementIntoView(curItemCookie);
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
                    var gheight = g.ActualHeight;
                    if (pos.Y < 0 || (pos.Y + gheight) > sv.ViewportHeight)
                        sv.ScrollToVerticalOffset(sv.VerticalOffset + pos.Y - MainWindow.BringIntoViewPadding);
                    var gwidth = g.ActualWidth;
                    if (pos.X < 0 || (pos.X + gwidth) > sv.ViewportWidth)
                        sv.ScrollToHorizontalOffset(sv.HorizontalOffset + pos.X - MainWindow.BringIntoViewPadding);
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

        //// UpdateTreeViewBranch clears the current branch move highlight if any, then
        //// checks to see if it needs to add branching highlighting to a move.  This is
        //// public so that Game.SetCurrentBranch can call it.
        ////
        public void UpdateTreeViewBranch (Move move) {
            // Highlight branch if there are any.
            UpdateTreeClearBranchHighlight();
            if (move == null) {
                if (this.Game.Branches != null) {
                    this.UpdateTreeHighlightBranch(this.Game.FirstMove);
                }
            }
            else {
                if (move.Branches != null) {
                    this.UpdateTreeHighlightBranch(move.Next);
                }
            }
        }

        //// UpdateTreeHighlightBranch takes a move that is the next move after a move with
        //// branches.  This highlights that move with a rectangle.
        ////
        private Grid nextBranchGrid = null;
        private Rectangle nextBranchRect = null;
        private void UpdateTreeHighlightBranch (Move move) {
            TreeViewNode item = this.TreeViewNodeForMove(move);
            // Should always be item here since not wiping tree, and if node were new, then no branches.
            MyDbg.Assert(item != null);
            Grid itemCookie = ((Grid)item.Cookie);
            this.nextBranchGrid = itemCookie;
            if (this.nextBranchRect == null) {
                this.nextBranchRect = new Rectangle();
                this.nextBranchRect.Stroke = new SolidColorBrush(Colors.Fuchsia);
                this.nextBranchRect.StrokeThickness = 0.7;
            }
            itemCookie.Children.Add(this.nextBranchRect);
        }

        private void UpdateTreeClearBranchHighlight () {
            // Clear current branch highlight if there is one.
            if (this.nextBranchGrid != null) {
                this.nextBranchGrid.Children.Remove(this.nextBranchRect);
                this.nextBranchGrid = null;
            }
        }

        //// TreeViewNodeForMove returns the TreeViewNode representing the view model for move.
        //// This is public because Game needs to call it when zipping through moves for gotolast,
        //// gotomovepath, etc., to ensure treeViewMoveMap maps Move objects when they get rendered,
        //// instead of apping the old ParseNode.
        ////
        public TreeViewNode TreeViewNodeForMove (Move move) {
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
                node.Node = move;
                return node;
            }
            else
                return null;
                // TODO: figure out what really to do here.
                //return this.treeViewMoveMap[move] = this.NewTreeViewNode(move);
        }


        ////
        //// Handling Multiple Open Games
        ////

        //// AddGame updates game management app globals in MainWindow.  It must be called after SetupBoardDisplay
        //// when creating new games.  If the current game is a non-dirty default game, then toss it.
        ////
        public void AddGame (Game g) {
            // this.Game is null when creating very first default game while launching app to display board.
            if (this.Game != null && object.ReferenceEquals(this.Game, this.DefaultGame) && 
                (this.Game.State != GameState.Started || ! this.Game.Dirty)) {
                this.Games.Remove(this.Game);
                this.DefaultGame = null;
            }
            if (this.Games.Count >= MainWindow.MAX_GAMES) {
                var now = DateTime.Now;
                var max = now - now;
                Game target = null;
                foreach (var gg in this.Games)
                    if ((now - gg.LastVisited) > max && ! gg.Dirty) {
                        target = gg;
                        max = now - gg.LastVisited;
                    }
                // If no such game, then don't worry about it.
                // Should add new game, but don't want to prompt here for one to close.
                if (target != null)
                    this.Games.Remove(target);
            }
            this.Game = g;
            this.Games.Add(g);
            this.LastCreatedGame = g;
        }


        //// UndoLastGameCreation should only be called after an open game operation fails, and a game
        //// was created that you know you need to clean up.  If the game passed in is not null, then
        //// it is the game to display; otherwise, create a default game.
        ////
        public void UndoLastGameCreation (Game newgame) {
            if (this.Games.Contains(this.LastCreatedGame))
                this.Games.Remove(this.LastCreatedGame);
            if (newgame == null) {
                //GameAux.CreateDefaultGame(this);
                this.CreateDefaultGame();
                return;
            }
            this.SetupBoardDisplay(newgame); // Clear current game UI, initialize board with new game.
            this.Game = newgame; // Must set this after calling SetupBoardDisplay.
            this.DrawGameTree();
            var move = newgame.CurrentMove;
            this.Game.GotoStartForGameSwap();
            if (move != null) {
                this.GotoGameTreeMove(move);
            } // else leave board at initial board state
            // Setup UI for target game's current move.
            this.UpdateTitle();
            this.UpdateTreeView(move);
            this.FocusOnStones();
        }

        //// CloseGame handles swapping out current game if necessary, checking dirty save, 
        //// creating default game if need be, etc.
        //// 
        private async Task CloseGame (Game g) {
            if (Object.ReferenceEquals(g, this.Game)) {
                if (this.Games.Count > 1) {
                    await this.GotoNextGame();
                }
                else {
                    await this.CheckDirtySave();
                    //GameAux.CreateDefaultGame(this);
                    //this.UpdateTitle();
                    this.CreateDefaultGame();
                }
            }
            else
                await this.CheckDirtySave();
            this.Games.Remove(g);
        }


        //// GotoNextGame updates the view to show the next game in the game list.  If the argument is
        //// is supplied and negative, this rotates backwards in the list.
        //// 
        //// The argument is an integer for gratuitous generality, but for now, this only goes one game.
        ////
        private async Task GotoNextGame (int howmany = 1) {
            Debug.Assert(howmany == 1 || howmany == -1, "Only support changing game display by one game in list.");
            var len = this.Games.Count;
            if (len == 1) {
                await GameAux.Message("There is only one game open currently.  Use Open or New to change games.",
                                      "Change Game Windows");
                return;
            }
            // Find target game as valid index.
            var curindex = GameAux.ListFind(this.Game, this.Games);
            Debug.Assert(curindex != -1, "Uh, how can the current game not be in the games list?!");
            int target = curindex + howmany;
            if (target == len)
                target = 0;
            else if (target < 0)
                target = len - 1;
            // Swap the board view.
            await GotoOpenGame(target);
        }

        //// GotoOpenGame updates the view to show the specified game.  Target is an index into this.Games.
        //// It must be a valid index.  This is used by app.xaml.cs for file activation and DoOpenFile.
        ////
        public async Task GotoOpenGame (int target) {
            await this.CheckDirtySave();
            var g = this.Games[target];
            this.SetupBoardDisplay(g); // Clear current game UI, initialize board with new game.
            this.Game = g; // Must set this after calling SetupBoardDisplay.
            this.DrawGameTree();
            var move = g.CurrentMove;
            this.Game.GotoStartForGameSwap();
            if (move != null) {
                this.GotoGameTreeMove(move);
            } // else leave board at initial board state
            // Setup UI for target game's current move.
            this.UpdateTitle();
            this.UpdateTreeView(move);
            this.FocusOnStones();
        }


        //// ShowGamesGoto displays a dialog listing the open games, from which users can select
        //// a game to display or delete/close.
        ////
        async Task ShowGamesGoto () {
            await this.CheckDirtySave();
            // Setup new dialog and show it
            var gameDialog = new WindowSwitchingDialog();
            var popup = new Popup();
            gameDialog.WindowSwitchingDialogClose += (s, e) => {
                popup.IsOpen = false;
                this.WindowSwitchingDialogDone(gameDialog);
            };
            popup.Child = gameDialog;
            popup.IsOpen = true;
            var gamesList = ((WindowSwitchingDialog)popup.Child).GamesList;
            foreach (var g in this.Games) {
                gamesList.Items.Add(g.Filename ?? "No File Association");
            }
            // Put focus into dialog, good for user, but also stops mainwindow from handling kbd events
            gamesList.IsEnabled = true;
            ((WindowSwitchingDialog)popup.Child).GamesList.IsTabStop = true;
            ((WindowSwitchingDialog)popup.Child).GamesList.IsHitTestVisible = true;
            ((WindowSwitchingDialog)popup.Child).GamesList.Focus(FocusState.Keyboard);
        }

        //// WindowSwitchingDialogDone handles when the goto/close game dialog popup is done.
        //// It checks the result of the dialog and takes appropriate action.
        ////
        private async void WindowSwitchingDialogDone (WindowSwitchingDialog dlg) {
            if (this.theAppBar.IsOpen)
                // If launched from appbar, and it remained open, close it.
                this.theAppBar.IsOpen = false;
            if (dlg.SelectionConfirmed == WindowSwitchingDialog.WindowSwitchResult.Switch) {
                var gfile = dlg.SelectedGame;
                // Do not search for filename because 1) "No File Name" and 2) always finds first if two have same name
                //var gindex = GameAux.ListFind(gfile, this.Games, (fname, game) => ((string)fname) == ((Game)game).Filename);
                var gindex = dlg.GamesList.SelectedIndex;
                var g = this.Games[gindex];
                if (Object.ReferenceEquals(g, this.Game))
                    return;
                await this.GotoOpenGame(gindex);
            }
            else if (dlg.SelectionConfirmed == WindowSwitchingDialog.WindowSwitchResult.Delete) {
                // Do not search for filename because 1) "No File Name" and 2) always finds first if two have same name
                //var gindex = GameAux.ListFind(gfile, this.Games, (fname, game) => ((string)fname) == ((Game)game).Filename);
                await CloseGame(this.Games[dlg.GamesList.SelectedIndex]);
            }
            this.FocusOnStones();
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
            this.AddInitialStones(this.Game);
            this.commentBox.Text = this.Game.Comments;
        }


        //// AddInitialStones takes a game and adds its handicap moves or all black (AB) stones and 
        //// all white (AW) to the display.  This takes a game because it is used on new games when
        //// setting up an initial display and when resetting to the start of this.Game.
        //// This optionally takes a grid to use instead of the main this.stonesGrid,
        //// and when we're adding stones to a one-off grid, then we do not map the
        //// added stones and screw up the main displays mapping of stone Ellispes.
        //// An example of a one-off grid is the static snapped view required by win8.
        ////
        private void AddInitialStones (Game game, Grid g = null) {
            if (game.HandicapMoves != null) {
                foreach (var elt in game.HandicapMoves)
                    if (g == null)
                        MainWindowAux.AddStone(this.stonesGrid, elt.Row, elt.Column, elt.Color);
                    else
                        MainWindowAux.AddStoneNoMapping(g, elt.Row, elt.Column, elt.Color);
            }
            if (game.AllWhiteMoves != null) {
                foreach (var elt in game.AllWhiteMoves)
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
                    // Win8 randomly sets background to transparent when disabled, and sometimes
                    // the background declared in the xaml (white) shows through, so just always
                    // set transparent on win8.  In WPF, it is always white, as declared.
                    combo.Background = new SolidColorBrush(Colors.Transparent);
                    //combo.Background = this.branch_combo_background;
                    combo.IsEnabled = false;
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

        //// FirstRhetoricalLineToStatement is a pet feature that converts the first line of a comment from
        //// a question to a statement by deleting the "?" at the EOL.
        //// 
        private async Task FirstRhetoricalLineToStatement () {
            var s = this.CurrentComment;
            var i = s.IndexOf("\r\n");  //Comments canonicalized because winRT TextBox forces that with no options
            if (i > 0 && s[i - 1] == ' ') {
                await GameAux.Message("Got whitespace at EOL.  Generalize your code, dude :-).");
            }
            if (i == -1) {
                if (s[s.Length - 1] == '?') i = s.Length;
            }
            if (i > 0 && s[i - 1] == '?') {
                this.UpdateCurrentComment(s.Substring(0, i - 1) + s.Substring(i));
            }
        }

        //// DeleteCommentLine is a pet feature that deletes the one-based numbered line in the comment.
        ////
        private async Task DeleteCommentLine (int num) {
            var s = this.CurrentComment;
            var loc = s.IndexOf("\r\n");  //Comments canonical newline because winRT TextBox forces that with no options
            // If no newlines, delete single first line or punt
            if (loc < 0) {
                if (s.Length != 0 && num == 1) {
                    this.UpdateCurrentComment("");
                    return;
                }
                await GameAux.Message("There is no line numbered " + num.ToString());
                return;
            }
            // Find start and end of desired line
            int prevloc = 0;
            for (int i = 1; i < num; i++) {
                var tmp = s.IndexOf("\r\n", loc + 2); // loc must point to \r\n, and starting at string.len returns -1
                if (tmp == -1) {
                    if (i == num - 1) { // Found num lines, last one does not end in newline sequence
                        tmp = s.Length;
                    }
                    else {
                        await GameAux.Message("There is no line numbered " + num.ToString());
                        return;
                    }
                }
                prevloc = loc;
                loc = tmp;
            }
            // Delete line from comment
            if (s[prevloc] == '\r') prevloc += 2; // Might be beginning of comment, but if not leave this newline along
            if (loc < s.Length) loc += 2; // Might be at end, but if not delete this newline
            if (prevloc == loc) {
                await GameAux.Message("There is no line numbered " + num.ToString());
                return;
            }
            this.UpdateCurrentComment(s.Substring(0, prevloc) + s.Substring(loc, s.Length - loc));
        }

        //// ReplaceIndexedMoveRef is a pet feature that replaces an indexed reference to a move (d4) with
        //// a word ("this").  This confirms index is to current move.
        ////
        //// For some reason the convention on computer go boards is to count rows from the bottom to the top,
        //// and SGF format for some reason records moves as col,row pairs.  Hence, we build moveRef backwards
        //// with column first and flipping row from bottom of board.
        ////
        private void ReplaceIndexedMoveRef () {
            var m = this.Game.CurrentMove;
            if (m == null) // empty board has no current move :-)
                return;
            var moveRef = GoBoardAux.ModelCoordinateToDisplayLetter(m.Column).ToString() + 
                          (this.Game.Board.Size + 1 - m.Row).ToString();
            var reflen = moveRef.Length;
            var comment = this.CurrentComment;
            var found = comment.IndexOf(moveRef);
            var res = "";
            if (found == -1) {
                var MoveRefLower = moveRef.ToLower();
                found = comment.IndexOf(MoveRefLower);
            }
            if (found != -1) {
                res = comment.Substring(0, found) + "this" +
                        comment.Substring(found + reflen, comment.Length - found - reflen);
            }
            if (res != "")
                this.UpdateCurrentComment(res);
        }

        //// ReplaceIndexedMarkedRef is a pet feature that replaces an indexed reference to a location (d4) with
        //// a word ("marked", "marked group", etc.).  This confirms index has an adornment marking it.
        ////
        //// For some reason the convention on computer go boards is to count rows from the bottom to the top,
        //// and SGF format for some reason records moves as col,row pairs.  Hence, we build moveRef backwards
        //// with column first and flipping row from bottom of board.
        ////
        private void ReplaceIndexedMarkedRef () {
            var comment = this.CurrentComment;
            var len = comment.Length;
            int modelrow = 0;
            int modelcol = 0;
            for (var i = 0; i < len - 1; i++) {
                var c = comment[i];
                if (c >= 'a' && c <= 'z' || c >= 'A' && c <= 'Z') {
                    var c2 = comment[i + 1];
                    if (c2 >= '0' && c2 <= '9') {
                        var num = 0;
                        int sublen = 2;
                        var c3 = i + 2 < len ? comment[i + 2] : ' ';
                        if (c3 >= '0' && c3 <= '9') {
                            num = ((c2 - '0')) * 10 + (c3 - '0');
                            sublen = 3;
                        }
                        else
                            num = c2 - '0';
                        // figure out row/col, get adornment
                        modelrow = (this.Game.Board.Size + 1 - num);
                        modelcol = GoBoardAux.DisplayLetterToModelCoordinate(c);
                        var adornments = this.Game.GetAdornments(modelrow, modelcol);
                        //var a = this.Game.GetAdornment(modelrow, modelcol, AdornmentKind.Triangle);
                        string res = "";
                        if (adornments.Count() == 0)
                            // Count is small, even in degenerate case, so ok to call Count
                            continue;
                        if (adornments.Count() == 1) {
                            var a = adornments[0];
                            if (a.Kind == AdornmentKind.Triangle)
                                res = "marked stone";
                            else if (a.Kind == AdornmentKind.Square)
                                res = "square marked stone";
                            else if (a.Kind == AdornmentKind.Letter)
                                res = a.Letter;
                            // No else for current move and setting res to "this" because current is not in list.
                        }
                        else
                            // Very rare and not useful to put mulitiple adornments on same location, so don't bother if multiple.
                            break;
                        this.UpdateCurrentComment(comment.Substring(0, i) + res +
                                                  comment.Substring(i + sublen, comment.Length - i - sublen));
                        break;
                    }
                }
            }
            
        }
        
        
        //// UpdateCurrentComment sets the current comment to the string, pushes the change to the model, and
        //// updates the title since the dirty bit at least may have changed.
        //// 
        private void UpdateCurrentComment (string s) {
            // Stash previous text just in case.
            var old = this.CurrentComment;
            if (old != "") { // Clipboard throws bogus null deref exception if the string is empty.
                var d = new DataPackage();
                d.SetText(this.CurrentComment);
                Clipboard.SetContent(d);
            }
            // Set UI, model, and title.
            this.CurrentComment = s;
            this.Game.SaveCurrentComment();
            this.UpdateTitle();
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
            MyDbg.Assert(size > 1);
            for (var i = 1; i < size + 1; i++) {
                //for i in xrange(1, size + 1):
                // chr_offset skips the letter I to avoid looking like the numeral one in the display.
                var chr_txt = GoBoardAux.ModelCoordinateToDisplayLetter(i);
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
            label.FontSize = 18;
            label.Foreground = new SolidColorBrush(Colors.Black);
            label.HorizontalAlignment = h_alignment;
            if (h_alignment == HorizontalAlignment.Right)
                // Add extra margin on right so that row numbers do not slam against edge
                // of board (tan of board).  This used to be 2 on win8/8.1 because
                // FocusableInputControl would set the parent grid cell 2 pixels bigger than
                // the square board size, but due to win10 back compat bug, winRT infinitely
                // resizes the focusable control and crashes.
                label.Margin = new Thickness(0, 0, 4, 0);
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
            MyDbg.Assert(stone != null, "Shouldn't be removing stone if there isn't one.");
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
                var inner_grid = MainWindowAux.AddAdornmentGrid(stones_grid, move.Row, move.Column, true, true);
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
            MyDbg.Assert(adornment.Kind == AdornmentKind.Square || adornment.Kind == AdornmentKind.Triangle ||
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
            tri.StrokeThickness = 0.5; // Makes it look more like square's line weight.
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
        internal static Grid AddAdornmentGrid (Grid stones_grid, int row, int col, bool render = true,
                                               bool smaller = false) {
            var inner_grid = new Grid();
            //inner_grid.ShowGridLines = false;  // not needed on win8
            inner_grid.Background = new SolidColorBrush(Colors.Transparent);
            Grid.SetRow(inner_grid, row);
            Grid.SetColumn(inner_grid, col);
            inner_grid.HorizontalAlignment = HorizontalAlignment.Stretch;
            inner_grid.VerticalAlignment = VerticalAlignment.Stretch;
            //inner_grid.Name = "adornmentGrid"
            var middleSize = smaller ? 2 : 3;
            inner_grid.ColumnDefinitions.Add(MainWindowAux.def_col(1));
            inner_grid.ColumnDefinitions.Add(MainWindowAux.def_col(middleSize));
            inner_grid.ColumnDefinitions.Add(MainWindowAux.def_col(1));
            inner_grid.RowDefinitions.Add(MainWindowAux.def_row(1));
            inner_grid.RowDefinitions.Add(MainWindowAux.def_row(middleSize));
            inner_grid.RowDefinitions.Add(MainWindowAux.def_row(1));
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
        public const int treeViewGridCellSize = 50; // 45;
        private const int treeViewNodeSize = 35; // 30;
        private const int treeViewFontSize = 16; // 14;
        private const int treeViewFontSize2 = 12; //10;


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
            g.Height = MainWindowAux.treeViewNodeSize;
            g.Width = MainWindowAux.treeViewNodeSize;
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
            MyDbg.Assert(model.Kind != TreeViewNodeKind.LineBend,
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
                label.FontSize = MainWindowAux.treeViewFontSize2;
                label.FontWeight = FontWeights.Normal;
            }
            else
                label.FontSize = MainWindowAux.treeViewFontSize;
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
        }


    } // class MainWindowAux

} // namespace sgfed

