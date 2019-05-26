//// game.cs is sort of the controller of the SGF Editor.  The main
//// class is Game, which provides calls for GUI event handling and makes calls
//// to update the board and moves model.

using System;
using System.Collections.Generic;
using System.Linq;
//using System.Text;
using Windows.UI; // Colors
//using System.Windows.Media; // Colors
using Windows.UI.Xaml.Controls; // ViewBox
using System.Windows; // Label (need to coerce type to fetch label cookie)
//using System.Windows.Controls; // MessageBox
using Windows.UI.Popups; // MessageDialog
using System.IO; // StreamWriter
using System.Threading.Tasks; // Task<string> for GameAux.Message
using Windows.Storage; // StorageFile
using System.Diagnostics; // Debug.Assert
using Windows.UI.Xaml; // UIElement



namespace SgfEdwin10 {


    public class Game {
        public const int MaxBoardSize = 19;
        public const int MinBoardSize = 9;
        public const string DefaultKomi = "6.5";

        private Color nextColor = Colors.Black;

        // _main_win is the WPF application object.
        private MainWindow mainWin = null;
        // board holds the GoBoard model.
        public GoBoard Board { get; set; }
        // komi is either 0.5, 6.5, or <int>.5
        public string Komi { get; set; }
        // _state helps with enabling and disabling buttons.
        public GameState State { get; set; }
        // first_move holds the first move which links to subsequent moves.
        // When displaying the intial board state of a started game, this is
        // the next move.
        public Move FirstMove { get; set; }
        // Branches is null until there's more than one first move.
        public List<Move> Branches { get; set; }
        // The following hold any markup for the initial board state
        public List<Adornments> SetupAdornments { get; set; }
        // current_move keeps point to the move in the tree we're focused on It
        // is None when the board is in the initial state, and first_move then
        // points to the current move.
        public Move CurrentMove { get; set; }
        // Comments holds any initial board state comments for the game.  This
        // is guaranteed to be set when opening a file, which write_game
        // depends on.
        public string Comments { get; set; }
        // handicap is the number of stones, and handicap_moves is the explicit placement moves.
        // HandicapMoves also models any All Black (AB) property on the root parsed node, even if no handicap (HA) prop.
        public int Handicap { get; set; }
        public List<Move> HandicapMoves { get; set; }
        public List<Move> AllWhiteMoves { get; set; }
        // Move count is just a counter to show the move # in the title bar.
        public int MoveCount { get; set; }
        // parsed_game is not None when we opened a file to edit.
        public ParsedGame ParsedGame { get; set; }
        // ParsedProperties is not null when we opened a file to edit.
        public Dictionary<string, List<string>> ParsedProperties { get; set; }
        // MiscGameInfo is like ParsedGame.Nodes.Properties, but this is null unless the user edits these.
        // If there is no user edit, then the ParsedGames holds the misc properties, and we pass them through
        // when we save the file.
        public Dictionary<string, List<string>> MiscGameInfo { get; set; }
        // filename holds the full pathname if we read from a file or ever
        // saved this game.  Filebase is just <name>.<ext>.
        public string Filename { get; set; }
        public string Filebase { get; set; }
        public StorageFile Storage { get; set; }
        // dirty means we've modified this game in some way
        public bool Dirty { get; set; }
        // player members hold strings if there are player names in record.
        public string PlayerBlack { get; set; }
        public string PlayerWhite { get; set; }
        public int BlackPrisoners { get; set; }
        public int WhitePrisoners { get; set; }
        // _cut_move holds the head of a sub tree that was last cut.
        // Note, the public cut_move is a method.
        private Move cutMove;

        // Used to retire games when they are too many open.
        public DateTime LastVisited;


        public Game (MainWindow main_win, int size, int handicap, string komi, List<Move> handicap_stones = null,
                     List<Move> all_white = null) {
            if (size != Game.MaxBoardSize)
                // Change this eventually to check min size and max size.
                throw new Exception("Only support 19x19 games for now.");
            this.CurrentMove = null;
            this.mainWin = main_win;
            this.Board = new GoBoard(size);
            this.InitHandicapNextColor(handicap, handicap_stones);
            if (all_white != null) {
                this.AllWhiteMoves = all_white;
                foreach (var m in all_white)
                    this.Board.AddStone(m);
            }
            this.Komi = komi;
            this.State = GameState.NotStarted;
            this.FirstMove = null;
            this.Branches = null;
            this.SetupAdornments = new List<Adornments>();
            this.CurrentMove = null;
            this.Comments = "";
            this.MoveCount = 0;
            this.ParsedGame = null;
            this.MiscGameInfo = null;
            this.Filename = null;
            this.Filebase = null;
            this.Storage = null;
            this.Dirty = false;
            this.PlayerBlack = "";
            this.PlayerWhite = "";
            this.BlackPrisoners = 0;
            this.WhitePrisoners = 0;
            this.cutMove = null;
            this.LastVisited = DateTime.Now;
        } // Game Constructor


        //// _init_handicap_next_color sets the next color to play and sets up any
        //// handicap state.  If there is a handicap, the moves may be specified in a
        //// parsed game; otherwise, this fills in traditional locations.  If there
        //// is a handicap and stones are supplied, then their number must agree.
        //// This sets nextColor based on handicap since sgfeditor ignores the PL property in root node.
        ////
        private void InitHandicapNextColor (int handicap, List<Move> handicap_stones) {
            this.Handicap = handicap;
            if (handicap == 0) {
                // Even if no handicap, could have All Black (AB) property in game root, which we model as handicap.
                if (handicap_stones != null) {
                    foreach (var m in handicap_stones)
                        this.Board.AddStone(m);
                    this.nextColor = Colors.White;
                }
                else {
                    this.nextColor = Colors.Black;
                }
                this.HandicapMoves = handicap_stones;
            }
            else {
                this.nextColor = Colors.White;
                this.HandicapMoves = handicap_stones;
                if (handicap_stones == null) {
                    this.HandicapMoves = new List<Move>();
                    Action<int, int> make_move = (row, col) => {
                        var m = new Move(row, col, Colors.Black);
                        this.HandicapMoves.Add(m);
                        this.Board.AddStone(m);
                    };
                    // Handicap stones accumulate from two in opposing corners, to a third
                    // one in a third corner, to four corners, then a fifth in the center.
                    // Six handicap stones is three along two sides, and seven has one in
                    // the center.  Eight handicaps is one in each corner and one in the
                    // middle of each side.  Nine has adds one in the center.
                    if (handicap >= 2) {
                        make_move(4, 16);
                        make_move(16, 4);
                    }
                    if (handicap >= 3)
                        make_move(16, 16);
                    if (handicap >= 4)
                        make_move(4, 4);
                    // There is only a center stone for 5, 7, and 9 handicaps.
                    if (handicap == 5)
                        make_move(10, 10);
                    if (handicap >= 6) {
                        make_move(10, 4);
                        make_move(10, 16);
                    }
                    // There is only a center stone for 5, 7, and 9 handicaps.
                    if (handicap == 7)
                        make_move(10, 10);
                    if (handicap >= 8) {
                        make_move(4, 10);
                        make_move(16, 10);
                    }
                    // There is only a center stone for 5, 7, and 9 handicaps.
                    if (handicap == 9)
                        make_move(10, 10);
                } /// handicap stones == null
                else {
                    MyDbg.Assert(handicap_stones.Count == handicap,
                                 "Handicap number is not equal to all " +
                                 "black stones in parsed root node.");
                    foreach (var m in handicap_stones)
                        this.Board.AddStone(m);
                }
            } // handicap != 0
        } // InitHandicapNextColor


        ////
        //// Making Moves while Playing Game
        ////

        //// MakeMove adds a move in sequence to the game and board at row, col.
        //// Row, col index from the top left corner.  Other than marking the
        //// current move with a UI adornments, this handles clicking and adding
        //// moves to a game.  It handles branching if the current move already has
        //// next moves and displays message if the row, col already has a move at
        //// that location.  If this is the first move, this function sets the game
        //// state to started.  It sets next move color and so on.  This returns the
        //// new move (or an existing move if the user clicked on a location where
        //// there is a move on another branch following the current move).  This returns
        //// null if there are any problems playing at this location or rendering a
        //// pre-existing found move here.  This assumes it was called because the user clicked.
        ////
        public async Task<Move> MakeMove (int row, int col) {
            var cur_move = this.CurrentMove;
            var maybe_branching = ((cur_move != null && cur_move.Next != null) ||
                                   (cur_move == null && this.FirstMove != null));
            // move may be set below to pre-existing move, tossing this new object.
            var move = new Move(row, col, this.nextColor);
            if (! move.IsPass && this.Board.HasStone(row, col)) {
                await GameAux.Message("Can't play where there already is a stone.");
                return null;
            }
            if (! move.IsPass && this.CheckSelfCaptureNoKill(move)) {
                await GameAux.Message("You cannot make a move that removes a group's last liberty");
                return null;
            }
            if (maybe_branching) {
                var tmp = this.MakeBranchingMove(cur_move, move);
                if (tmp == null) {
                    // do not call GameAux.NextMoveDisplayError because new move object has no error msg.
                    // NOTE, if we do not return here, ReplayMove below will report on the SGF issue, but
                    // it puts up two dialogs which feels ugly (can get error msg by using arrows to next move.
                    await GameAux.Message("You clicked where a next move exists in the game tree, but \n" +
                                          "the next move at that location had bad properties in the SGF file.\n" +
                                          "You cannot play further down that branch.");
                    return null;
                }
                // Just because we're branching, doesn't mean the game is dirty.
                if (object.ReferenceEquals(tmp, move))
                    // If added new move, mark dirty since user could have saved game.
                    this.Dirty = true;
                else
                    // Found existing move at location in branches, just replay it for capture effects, etc.
                    // Don't need to check ReplayMove for conflicting board move since user clicked and space is empty.
                    return await this.ReplayMove();
            }
            else {
                if (this.State == GameState.NotStarted) {
                    this.FirstMove = move;
                    this.State = GameState.Started;
                }
                else {
                    cur_move.Next = move;
                    move.Previous = cur_move;
                }
                this.Dirty = true;
            }
            this.SaveAndUpdateComments(cur_move, move);
            if (! move.IsPass)
                this.Board.AddStone(move);
            this.CurrentMove = move;
            move.Number = this.MoveCount + 1;
            this.MoveCount += 1;
            this.nextColor = GameAux.OppositeMoveColor(this.nextColor);
            this.mainWin.EnableBackwardButtons();
            if (move.Next == null) {
                // Made a move or branch that is at end of line of play.
                this.mainWin.DisableForwardButtons();
            }
            else {
                // Made a move that is already the next move in some branch,
                // and it has a next move.
                this.mainWin.EnableForwardButtons();
            }
            if (! move.IsPass && move.DeadStones.Any()) { // CheckSelfCaptureNoKill above set captured stones for new move
                this.RemoveStones(move.DeadStones);
                this.UpdatePrisoners(move.Color, move.DeadStones.Count());
            }
            return move;
        }

        //// CheckSelfCaptureNoKill returns true if move removes the last liberty of
        //// its group without killing an opponent group.  This function needs to 
        //// temporarily add the move to the board, then remove it, but we don't need
        //// a try..finally since any error is unexpected and unrecoverable.
        ////
        private bool CheckSelfCaptureNoKill (Move move) {
            this.Board.AddStone(move);
            var noKill = ! this.CheckForKill(move).Any(); // .Any ensures it executes.
            var noLibertyAndNoKill = ! this.FindLiberty(move.Row, move.Column, move.Color) && noKill;
            this.Board.RemoveStone(move);
            return noLibertyAndNoKill;
        }

        //// _make_branching_move sets up cur_move to have more than one next move,
        //// that is, branches.  If the new move, move, is at the same location as
        //// a next move of cur_move, then this function dumps move in lieu of the
        //// existing next move.  This also sets up any next and prev pointers as
        //// appropriate and updates the branches combo.  This return null if it can't return a move.
        ////
        private Move MakeBranchingMove (Move cur_move, Move move) {
            if (cur_move == null) {
                var stuff = this.MakeBranchingMoveBranches(this.Branches, this.FirstMove, move);
                move = stuff.Item1;
                this.Branches = stuff.Item2;
                //move = this.MakeBranchingMoveBranches(this, this.FirstMove, move);
                if (move == null) return null;  // Conflicting next stone location or bad parse node.
                this.FirstMove = move;
            }
            else {
                var stuff = this.MakeBranchingMoveBranches(cur_move.Branches, cur_move.Next, move);
                move = stuff.Item1;
                cur_move.Branches = stuff.Item2;
                //move = this.MakeBranchingMoveBranches(cur_move, cur_move.Next, move);
                if (move == null) return null;  // Conflicting next stone location or bad parse node.
                cur_move.Next = move;
                move.Previous = cur_move;
            }
            // move may be pre-existing move with branches, or may need to clear combo ...
            this.mainWin.UpdateBranchCombo(move.Branches, move.Next);
            return move;
        }

        //// _make_branching_move_branches takes a game or move object (the current
        //// move), the current next move, and a move representing where the user
        //// clicked.  If there are no branches yet, then see if new_move is at the
        //// same location as next and toss new_move in this case, which also means
        //// there are still no branches yet.  This return null if it can't return a move,
        //// which happens if it finds an existing move in the tree, but that move has bad parse info.
        ////
        private Tuple<Move, List<Move>> MakeBranchingMoveBranches (List<Move> branches, Move next, Move new_move) {
            if (branches == null) {
                branches = new List<Move>() { next }; // Must pass non-null branches.
                // Can't use'var', must decl with 'Move'.  C# desn't realize all MaybeUpdateBanches
                // definitions return a Move.
                Move move = this.MaybeUpdateBranches(branches, new_move);
                if (move == null) return null;  // Conflicting next stone location or bad parse node.
                if (object.ReferenceEquals(move, next)) {
                    // new_move and next are same, keep next and clean up branches.
                    //game_or_move.Branches = null;
                    return new Tuple<Move, List<Move>>(next, null);
                }
                else {
                    // new_move and next are not the same, so keep branches since there are two next moves now.
                    //game_or_move.Branches = branches;
                    // Keep branches and return move
                    return new Tuple<Move, List<Move>>(move, branches);
                }
            }
            else
                return new Tuple<Move, List<Move>>(this.MaybeUpdateBranches(branches, new_move), branches);
        }

        //// _maybe_update_branches takes a branches list and a next move.  Branches must not be null.
        //// It returns a pre-existing move if the second argument represents a move at a location for which there
        //// already is a move; otherwise, this function returns the second argument as a new next
        //// move.  If this is a new next move, we add it to branches.  This return null if it can't return a move.
        ////
        private Move MaybeUpdateBranches (List<Move> branches, Move move) {
            MyDbg.Assert(branches != null);
            var already_move = GameAux.ListFind(
                                   move, branches,
                                   (x, y) => move.Row == ((Move)y).Row && move.Column == ((Move)y).Column);
            if (already_move != -1) {
                var m = branches[already_move];
                if (! m.Rendered)
                    if (this.ReadyForRendering(m) == null)  // Issue with parsed node, cannot go forward.
                        return null;
                return m;
            }
            else {
                branches.Add(move);
                return move;
            }
        }

        //// check_for_kill determines if move kills any stones on the board and
        //// returns a list of move objects that were killed after storing them in
        //// the Move object.  We use find_liberty and collect_stones rather than
        //// try to build list as we go to simplify code.  Worse case we recurse all
        //// the stones twice, but it doesn't impact observed performance.
        ////
        //// We do not create a visited matrix to pass to each FindLiberty call since
        //// we do not know whether a previously visited location resulted in finding
        //// a liberty or collecting dead stones.
        ////
        private List<Move> CheckForKill (Move move) {
            var row = move.Row;
            var col = move.Column;
            var opp_color = GameAux.OppositeMoveColor(move.Color);
            var visited = new bool[this.Board.Size, this.Board.Size];
            var dead_stones = new List<Move>();
            if (this.Board.HasStoneColorLeft(row, col, opp_color) &&
                !this.FindLiberty(row, col - 1, opp_color))
                this.CollectStones(row, col - 1, opp_color, dead_stones, visited);
            if (this.Board.HasStoneColorUp(row, col, opp_color) &&
                !this.FindLiberty(row - 1, col, opp_color))
                this.CollectStones(row - 1, col, opp_color, dead_stones, visited);
            if (this.Board.HasStoneColorRight(row, col, opp_color) &&
                !this.FindLiberty(row, col + 1, opp_color))
                this.CollectStones(row, col + 1, opp_color, dead_stones, visited);
            if (this.Board.HasStoneColorDown(row, col, opp_color) &&
                !this.FindLiberty(row + 1, col, opp_color))
                this.CollectStones(row + 1, col, opp_color, dead_stones, visited);
            move.DeadStones = dead_stones;
            return dead_stones;
        }

        //// find_Liberty starts at row, col traversing all stones with the supplied
        //// color to see if any stone has a liberty.  It returns true if it finds a
        //// liberty.  If we've already been here, then its search is still pending
        //// (and other stones it connects with should be searched).  See comment
        //// for check_for_kill.  Visited can be null if you just want to check if a
        //// single stone/group has any liberties, say, to see if a move was a self capture.
        ////
        private bool FindLiberty (int row, int col, Color color, bool[,] visited = null) {
            if (visited == null)
                // Consider later if this is too much consing per move.
                // We cons this for self kill check, cons another for CheckForKill of opponent stones.
                visited = new bool[this.Board.Size, this.Board.Size];
            if (visited[row - 1, col - 1])
                return false;
            // Check for immediate liberty (breadth first).
            if (col != 1 && !this.Board.HasStoneLeft(row, col))
                return true;
            if (row != 1 && !this.Board.HasStoneUp(row, col))
                return true;
            if (col != this.Board.Size && !this.Board.HasStoneRight(row, col))
                return true;
            if (row != this.Board.Size && !this.Board.HasStoneDown(row, col))
                return true;
            // No immediate liberties, so keep looking ...
            visited[row - 1, col - 1] = true;
            if (this.Board.HasStoneColorLeft(row, col, color) &&
                this.FindLiberty(row, col - 1, color, visited))
                return true;
            if (this.Board.HasStoneColorUp(row, col, color) &&
                this.FindLiberty(row - 1, col, color, visited))
                return true;
            if (this.Board.HasStoneColorRight(row, col, color) &&
                this.FindLiberty(row, col + 1, color, visited))
                return true;
            if (this.Board.HasStoneColorDown(row, col, color) &&
                this.FindLiberty(row + 1, col, color, visited))
                return true;
            // No liberties ...
            return false;
        }

        //// CollectStones gathers all the stones at row, col of color color, adding them
        //// to the list dead_stones.  This does not update the board model by removing
        //// the stones.  CheckForKill uses this to collect stones.  ReadyForRendering calls
        //// CheckForKill to prepare moves for rendering, but it shouldn't remove stones
        //// from the board.
        ////
        private void CollectStones (int row, int col, Color color, List<Move> dead_stones,
                                    bool[,] visited) {
            MyDbg.Assert(visited != null, "Must call CollectStones with initial matrix of null values.");
            if (! visited[row - 1, col - 1])
                dead_stones.Add(this.Board.MoveAt(row, col));
            else
                return;
            visited[row - 1, col - 1] = true;
            if (this.Board.HasStoneColorLeft(row, col, color) && !visited[row - 1, col - 2])
                this.CollectStones(row, col - 1, color, dead_stones, visited);
            if (this.Board.HasStoneColorUp(row, col, color) && !visited[row - 2, col - 1])
                this.CollectStones(row - 1, col, color, dead_stones, visited);
            if (this.Board.HasStoneColorRight(row, col, color) && !visited[row - 1, col])
                this.CollectStones(row, col + 1, color, dead_stones, visited);
            if (this.Board.HasStoneColorDown(row, col, color) && !visited[row, col - 1])
                this.CollectStones(row + 1, col, color, dead_stones, visited);
        }

        ////
        //// Unwinding Moves and Goign to Start
        ////

        //// unwind_move removes the last move made (see make_move).  Other than
        //// marking the previous move as the current move with a UI adornments,
        //// this handles rewinding game moves.  If the game has not started, or
        //// there's no current move, this signals an error.  This returns the move
        //// that was current before rewinding.
        ////
        public Move UnwindMove () {
            // These debug.asserts could arguably be throw's if we think of this function
            // as platform/library.
            MyDbg.Assert(this.State != GameState.NotStarted,
                         "Previous button should be disabled if game not started.");
            var current = this.CurrentMove;
            MyDbg.Assert(current != null, "Previous button should be disabled if no current move.");
            if (! current.IsPass)
                this.Board.RemoveStone(current);
            this.AddStones(current.DeadStones);
            this.UpdatePrisoners(current.Color, - current.DeadStones.Count());
            this.nextColor = current.Color;
            this.MoveCount -= 1;
            var previous = current.Previous;
            this.SaveAndUpdateComments(current, previous);
            if (previous == null) {
                this.mainWin.DisableBackwardButtons();
            }
            this.mainWin.EnableForwardButtons();
            if (previous == null)
                this.mainWin.UpdateBranchCombo(this.Branches, current);
            else
                this.mainWin.UpdateBranchCombo(previous.Branches, current);
            this.CurrentMove = previous;
            return current;
        }

        //// UpdatePrisoners takes a positive (just captured) or negative (unwinding)
        //// count of prisoners and updates the appropriate counter for the color.
        ////
        private void UpdatePrisoners (Color color, int count) {
            if (color == Colors.Black)
                this.BlackPrisoners += count;
            else
                this.WhitePrisoners += count;
        }

        public bool CanUnwindMove () {
            return (this.State != GameState.NotStarted) && this.CurrentMove != null;
        }

        private void AddStones (List<Move> stones) {
            this.mainWin.AddStones(stones);
            foreach (var m in stones)
                this.Board.AddStone(m);
        }


        //// goto_start resets the model to the initial board state before any moves
        //// have been played, and then resets the UI.  This assumes the game has
        //// started, but throws an exception to ensure code is consistent on that.
        ////
        public void GotoStart () {
            // These debug.asserts could arguably be throw's if we think of this function
            // as platform/library.
            MyDbg.Assert(this.State != GameState.NotStarted,
                         "Home button should be disabled if game not started.");
            var current = this.CurrentMove;
            MyDbg.Assert(current != null, "Home button should be disabled if no current move.");
            this.SaveAndUpdateComments(current, null);
            this.Board.GotoStart();
            this.mainWin.ResetToStart(current);
            this.nextColor = this.Handicap == 0 ? Colors.Black : Colors.White;
            this.CurrentMove = null;
            this.MoveCount = 0;
            this.BlackPrisoners = 0;
            this.WhitePrisoners = 0;
            this.mainWin.UpdateBranchCombo(this.Branches, this.FirstMove);
            this.mainWin.DisableBackwardButtons();
            this.mainWin.EnableForwardButtons();
        }

        //// GotoStartForGameSwap resets the model to the initial board state before any moves
        //// have been played so that the new current game can replay moves to its last current state.
        //// This assumes the UI, board, etc., has been cleared with SetupBoardDisplay.  This does
        //// not need the state guarantees of GotoStart, such as a started game or current move, and
        //// it does not have to rewind all state, like the board view, since we setup the board
        //// display before calling this to replay moves.
        ////
        public void GotoStartForGameSwap () {
            // Comments for cur move have already been saved and cleared.  Put initial board comments in
            // place in case this.Game is sitting at the intial board state.
            this.mainWin.CurrentComment = this.Comments; 
            this.Board.GotoStart();
            this.nextColor = this.Handicap == 0 ? Colors.Black : Colors.White;
            this.CurrentMove = null;
            this.MoveCount = 0;
            this.LastVisited = DateTime.Now;
            this.BlackPrisoners = 0;
            this.WhitePrisoners = 0;
            this.mainWin.UpdateBranchCombo(this.Branches, this.FirstMove);
            this.mainWin.DisableBackwardButtons();
            this.mainWin.EnableForwardButtons();
        }


        ////
        //// Replaying Moves and Goign to End
        ////

        //// replay_move add the next that follows the current move.  move made (see
        //// make_move).  Other than marking the next move as the current move with
        //// a UI adornments, this handles replaying game moves.  The next move is
        //// always move.next which points to the selected branch if there is more
        //// than one next move.  If the game hasn't started, or there's no next
        //// move, this signals an error.  This returns the move that was current
        //// before rewinding.  This return null if it can't replay a move, which means it
        //// encountered a conflicting move or bad parse node.
        ////
        public async Task<Move> ReplayMove () {
            // These debug.asserts could arguably be throw's if we think of this function
            // as platform/library.
            MyDbg.Assert(this.State != GameState.NotStarted,
                         "Next button should be disabled if game not started.");
            // advance this.current_move to the next move.
            var fixupMove = this.CurrentMove; // save for catch block
            if (this.CurrentMove == null)
                this.CurrentMove = this.FirstMove;
            else {
                MyDbg.Assert(this.CurrentMove.Next != null,
                             "Next button should be disabled if no next move.");
                this.CurrentMove = this.CurrentMove.Next;
            }
            if (this.ReplayMoveUpdateModel(this.CurrentMove) == null) {
                await GameAux.NextMoveDisplayError(this.CurrentMove);
                this.CurrentMove = fixupMove;
                return null;
            }
            this.SaveAndUpdateComments(this.CurrentMove.Previous, this.CurrentMove);
            if (this.CurrentMove.Next == null) {
                this.mainWin.DisableForwardButtons();
            }
            this.mainWin.EnableBackwardButtons();
            this.mainWin.UpdateBranchCombo(this.CurrentMove.Branches, this.CurrentMove.Next);
            this.mainWin.CurrentComment = this.CurrentMove.Comments;
            return this.CurrentMove;
        }

        public bool CanReplayMove () {
            return (this.State == GameState.Started &&
                    (this.CurrentMove == null || this.CurrentMove.Next != null));
        }

        //// goto_last_move handles jumping to the end of the game record following
        //// all the currently selected branches.  This handles all game/board model
        //// and UI updates, including current move adornments.  This assumes the game
        //// has started.
        ////
        public async Task GotoLastMove () {
            // This debug.assert could arguably be a throw if we think of this function
            // as platform/library.
            MyDbg.Assert(this.State != GameState.NotStarted,
                         "End button should be disabled if game not started.");
            var current = this.CurrentMove;
            var save_orig_current = current;
            Move next;
            // Setup for loop ...
            if (current == null) {
                current = this.FirstMove;
                if (this.ReplayMoveUpdateModel(current) == null) {
                    await GameAux.NextMoveDisplayError(current);
                    // No partial actions/state to clean up or revert.
                    return;
                }
                this.mainWin.AddNextStoneNoCurrent(current);
                next = current.Next;
            }
            else
                next = current.Next;
            // Walk to last move
            while (next != null) {
                if (this.ReplayMoveUpdateModel(next) == null) {
                    await GameAux.NextMoveDisplayError(next);
                    break;
                }
                this.mainWin.AddNextStoneNoCurrent(next);
                current = next;
                next = current.Next;
            }
            // Update last move UI
            this.SaveAndUpdateComments(save_orig_current, current);
            this.mainWin.AddCurrentAdornments(current);
            this.CurrentMove = current;
            this.MoveCount = current.Number;
            this.nextColor = GameAux.OppositeMoveColor(current.Color);
            this.mainWin.EnableBackwardButtons();
            if (next == null)
                this.mainWin.DisableForwardButtons();
            else
                this.mainWin.EnableForwardButtons();
            // There can't be any branches, but this ensures UI is cleared.
            if (next != null)
                this.mainWin.UpdateBranchCombo(current.Branches, next);
            else
                this.mainWin.UpdateBranchCombo(null, null);
        }


        //// _replay_move_update_model updates the board model, next move color,
        //// etc., when replaying a move in the game record.  This also handles
        //// rendering a move that has only been read from a file and never
        //// displayed in the UI.  Rendering here just means its state will be as if
        //// it had been rendedered before.  We must setup branches to Move objects,
        //// and make sure the next Move object is created and marked unrendered so
        //// that code elsewhere that checks move.next will know there's a next
        //// move.  This return null if there is an issue replaying the move.
        ////
        private Move ReplayMoveUpdateModel (Move move) {
            var cleanup = false;
            if (! move.IsPass) {
                // Check if board has stone already since might be replaying branch
                // that was pasted into tree (and moves could conflict).
                if (!this.Board.HasStone(move.Row, move.Column)) {
                    this.Board.AddStone(move);
                    cleanup = true;
                }
                else
                    return null;
            }
            this.nextColor = GameAux.OppositeMoveColor(move.Color);
            if (! move.Rendered) {
                // Move points to a ParsedNode and has never been displayed.
                if (this.ReadyForRendering(move) == null) { // Issue with parsed node, cannot go forward.
                    if (cleanup) this.Board.RemoveStone(move);
                    return null;
                }
                // Don't need view model object, but need to ensure there is one mapped by move.
                this.mainWin.TreeViewNodeForMove(move);
            }
            this.MoveCount += 1;
            this.RemoveStones(move.DeadStones);
            this.UpdatePrisoners(move.Color, move.DeadStones.Count());
            return move;
        }

        private void RemoveStones (List<Move> stones) {
            this.mainWin.RemoveStones(stones);
            foreach (var m in stones)
                this.Board.RemoveStone(m);
        }


        //// _ready_for_rendering puts move in a state as if it had been displayed
        //// on the screen before.  Moves from parsed nodes need to be created when
        //// their previous move is actually displayed on the board so that there is
        //// a next Move object in the game three for consistency with the rest of
        //// model.  However, until the moves are actually ready to be displayed
        //// they do not have captured lists hanging off them, their next branches
        //// and moves set up, etc.  This function makes the moves completely ready
        //// for display, or returns null if there was a problem.
        ////
        private Move ReadyForRendering (Move move) {
            if (! move.IsPass)
                this.CheckForKill(move);
            var pn = move.ParsedNode;
            Move mnext = null;
            if (pn.Branches != null) {
                var moves = new List<Move>();
                foreach (var n in pn.Branches) {
                    var m = GameAux.ParsedNodeToMove(n);
                    if (m == null) return null; // Unhandled parse node features
                    m.Number = this.MoveCount + 2;
                    m.Previous = move;
                    moves.Add(m);
                }
                move.Branches = moves;
                mnext = moves[0];
            }
            else if (pn.Next != null) {
                mnext = GameAux.ParsedNodeToMove(pn.Next);
                if (mnext == null) return null; // Unhandled parse node features
                mnext.Number = this.MoveCount + 2;
                mnext.Previous = move;
            }
            move.Next = mnext;
            this.ReplayUnrenderedAdornments(move);
            move.Rendered = true;
            return move;
        }

        //// _replay_unrendered_adornments is just a helper for
        //// _replay_move_update_model.  This does not need to check add_adornment
        //// for a None result since we're trusting the file was written correctly,
        //// or it doesn't matter if there are dup'ed letters.
        ////
        private void ReplayUnrenderedAdornments (Move move) {
            var props = move.ParsedNode.Properties;
            if (props.ContainsKey("TR")) {
                var coords = props["TR"].Select((c) => GoBoardAux.ParsedToModelCoordinates(c))
                                        .ToList<Tuple<int, int>>();
                //var coords = [goboard.parsed_to_model_coordinates(x) for x in props["TR"]];
                var adorns = coords.Select((c) => this.AddAdornment(move, c.Item1, c.Item2, AdornmentKind.Triangle))
                                   .ToList<Adornments>();
                //var adorns = [this.add_adornment(move, x[0], x[1], goboard.Adornments.triangle)
                //          for x in coords];
                foreach (var a in adorns)
                    this.mainWin.AddUnrenderedAdornments(a);
            }
            if (props.ContainsKey("SQ")) {
                var coords = props["SQ"].Select((c) => GoBoardAux.ParsedToModelCoordinates(c))
                                        .ToList<Tuple<int, int>>();
                //coords = [goboard.parsed_to_model_coordinates(x) for x in props["SQ"]]
                var adorns = coords.Select((c) => this.AddAdornment(move, c.Item1, c.Item2, AdornmentKind.Square))
                                   .ToList<Adornments>();
                //adorns = [this.add_adornment(move, x[0], x[1], goboard.Adornments.square)
                //          for x in coords]
                foreach (var a in adorns)
                    this.mainWin.AddUnrenderedAdornments(a);
            }
            if (props.ContainsKey("LB")) {
                var coords = props["LB"].Select((c) => GoBoardAux.ParsedLabelModelCoordinates(c))
                                        .ToList<Tuple<int, int, char>>();
                //coords = [goboard.parsed_label_model_coordinates(x) for x in props["LB"]];
                var adorns = coords.Select((c) => this.AddAdornment(move, c.Item1, c.Item2,
                                                                    AdornmentKind.Letter, new string(c.Item3, 1)))
                                   .ToList<Adornments>();
                //adorns = [this.add_adornment(move, x[0], x[1], goboard.Adornments.letter, x[2])
                //          for x in coords];
                foreach (var a in adorns)
                    this.mainWin.AddUnrenderedAdornments(a);
            }
        }


        //// _save_and_update_comments ensures the model captures any comment
        //// changes for the origin and displays dest's comments.  Dest may be a new
        //// move, and its empty string comment clears the textbox.  Dest may also
        //// be the previous move of origin if we're unwinding a move right now.
        //// Dest and origin may not be contiguous when jumping to the end or start
        //// of the game.  If either origin or dest is None, then it represents the
        //// intial board state.  If the captured comment has changed, mark game as
        //// dirty.
        ////
        private void SaveAndUpdateComments (Move origin, Move dest) {
            this.SaveComment(origin);
            if (dest != null)
                this.mainWin.CurrentComment = dest.Comments;
            else
                this.mainWin.CurrentComment = this.Comments;
        }

        //// save_current_comment makes sure the current comment is persisted from the UI to
        //// the model.  This is used from the UI, such as when saving a file.
        ////
        public void SaveCurrentComment () {
            this.SaveComment(this.CurrentMove);
        }

        //// save_comment takes a move to update with the current comment from the UI.
        //// If move is null, the comment belongs to the game start or empty board.
        ////
        private void SaveComment (Move move = null) {
            var cur_comment = this.mainWin.CurrentComment;
            // Need to bind this shit here due to C#'s lax lexical semantics for locals.
            Tuple<bool, string> stuff;
            bool same;
            string newstr;
            // If move is null, then the "current move" is the initial board state.
            if (move != null) {
                stuff = GameAux.CompareComments(cur_comment, move.Comments);
                same = stuff.Item1;
                newstr = stuff.Item2;
                if (!same) { //if (move.Comments != cur_comment) {
                    move.Comments = newstr;
                    this.Dirty = true;
                }
            }
            else {
                stuff = GameAux.CompareComments(cur_comment, this.Comments);
                same = stuff.Item1;
                newstr = stuff.Item2;
                if (!same) { //this.Comments != cur_comment) {
                    this.Comments = newstr;
                    this.Dirty = true;
                }
            }
        }


        ////
        //// Cutting and Pasting Sub Trees
        ////

        //// cut_move must be invoked on a current move.  It leaves the game state
        //// with the previous move or initial board as the current state, and it
        //// updates UI.
        ////
        public void CutMove () {
            var cut_move = this.CurrentMove;
            // This debug.assert could arguably be a throw if we think of this function
            // as platform/library.
            MyDbg.Assert(cut_move != null,
                         "Must cut current move, so cannot be initial board state.");
            // unwind move with all UI updates and game model updates (and saves comments)
            this.mainWin.prevButtonLeftDown(null, null);
            var prev_move = this.CurrentMove;
            cut_move.Previous = null;
            cut_move.DeadStones.Clear();
            if (prev_move == null) {
                CutFirstMove(cut_move);
            }
            else
                // Handle regular move.
                this.CutNextMove(prev_move, cut_move);
            this.cutMove = cut_move;
            this.Dirty = true;
            // Update UI now that current move's next/branches have changed.
            if (prev_move == null) {
                if (this.FirstMove == null)
                    this.mainWin.DisableForwardButtons();
                else
                    this.mainWin.EnableForwardButtons();
                this.mainWin.UpdateBranchCombo(this.Branches, this.FirstMove);
                this.mainWin.UpdateTitle();
            }
            else {
                if (prev_move.Next == null)
                    this.mainWin.DisableForwardButtons();
                else
                    this.mainWin.EnableForwardButtons();
                this.mainWin.UpdateBranchCombo(prev_move.Branches, prev_move.Next);
                this.mainWin.UpdateTitle();
            }
            this.mainWin.UpdateTreeView(prev_move, true);
        }

        //// CutFirstMove takes a Move that is a firstmove of the game.  This function cleans up next pointers
        //// and branches lists appropriately for the move.  This is VERY similar to CutNextMove for general
        //// nodes, but has a few tweaks due to initial board modeling ... if only C# had procedural macros :-).
        ////
        private void CutFirstMove (Move cut_move) {
            // Handle initial board state.  Can't use _cut_next_move here due
            // to special handling of initial board and this._state.
            var branches = this.Branches;
            if (branches == null) {
                this.FirstMove = null;
                this.State = GameState.NotStarted;
            }
            else {
                var cut_index = GameAux.ListFind(cut_move, branches);
                branches.RemoveAt(cut_index);
                this.FirstMove = branches[0];
                if (branches.Count == 1)
                    this.Branches = null;
            }
            if (this.ParsedGame != null && this.ParsedGame.Nodes.Next != null && cut_move.ParsedNode != null)
                // If we have a game with a parsed node, then we need to cut the first parsed node tree
                // too.  Need to check if cut_move.ParsedNode is null because it could have been added
                // after parsing file and have no ParsedNode.  It also could be an auto-save stashed a
                // parsed node tree in the game, in which case it would have a new parsed node we could not
                // find anyway.  It does no harm if an extra branch hangs off the parsed node tree, and
                // it goes away on the next auto save.
                this.CutNextParsedNode(this.ParsedGame.Nodes, cut_move.ParsedNode);
        }

        //// CutNextMove takes a Move that is the previous move of the second argument,
        //// and the move being cut.  This function cleans up next pointers and branches
        //// lists appropriately for the move.
        ////
        private void CutNextMove (Move move, Move cut_move) {
            var branches = move.Branches;
            if (branches == null)
                move.Next = null;
            else {
                var cut_index = GameAux.ListFind(cut_move, branches);
                branches.RemoveAt(cut_index);
                move.Next = branches[0];
                if (branches.Count == 1)
                    move.Branches = null;
            }
            if (move.ParsedNode != null && move.ParsedNode.Next != null && cut_move.ParsedNode != null)
                // If we have a Move with a parsed node, then we need to cut the parsed node tree
                // too.  If have Move for parsed node, and move does not have branches, then parsed
                // node does not either since we create Moves for parsed nodes ahead of fully rendering
                // and consistently delete parsed nodes if delete moves.  Need to check if cut_move.ParsedNode
                // is null because it could have been added after parsing file and have no ParsedNode.
                // It also could be an auto-save stashed a parsed node tree in the game, in which case it
                // would have a new parsed node we could not find anyway.  It does no harm if an extra branch
                // hangs off the parsed node tree, and it goes away on the next auto save.
                this.CutNextParsedNode(move.ParsedNode, cut_move.ParsedNode);
        }

        //// CutNextParsedNode is the same as CutNextMove, except for ParsedNode.  I could have used
        //// dynamic, but it required an 'is' type test (a red flag for using dynamic) to determine if
        //// I had to check parsed nodes.  This function assumes pn and cut-move are not null.
        ////
        private void CutNextParsedNode (ParsedNode pn, ParsedNode cut_move) {
            var branches = pn.Branches;
            if (branches == null)
                pn.Next = null;
            else {
                var cut_index = GameAux.ListFind(cut_move, branches);
                branches.RemoveAt(cut_index);
                pn.Next = branches[0];
                if (branches.Count == 1)
                    pn.Branches = null;
            }
        }

        //// can_paste returns whether there is a cut sub tree, but it does not
        //// check whether the cut tree actually can be pasted at the current move.
        //// It ignores whether the right move color will follow the current move,
        //// which paste_move allows, but this does not check whether all the moves
        //// will occupy open board locations, which paste_move requires.
        ////
        public bool CanPaste () {
            return this.cutMove != null;
        }

        //// paste_move makes this._cut_move be the next move of the current move
        //// displayed.  This does not check consistency of all moves in sub tree since that
        //// would involve replaying them all.  It does check a few things with the first
        //// cut move.
        ////
        public async Task PasteMove () {
            // These debug.asserts could arguably be throw's if we think of this function
            // as platform/library.
            MyDbg.Assert(this.cutMove != null, "There is no cut sub tree to paste.");
            if (this.cutMove.Color != this.nextColor) {
                await GameAux.Message("Cannot paste cut move that is same color as current move.");
                return;
            }
            // Need to ensure first cut move doesn't conflict, else checking self capture throws.
            if (! this.cutMove.IsPass && this.Board.HasStone(this.cutMove.Row, this.cutMove.Column)) {
                await GameAux.Message("Cannot paste cut move that is at same location as another stone.");
                return;
            }
            // If CheckSelfCaptureNoKill returns false, the it updates cutMove to have dead
            // stones hanging from it so that calling DoNextButton removes them.
            if (! this.cutMove.IsPass && this.CheckSelfCaptureNoKill(this.cutMove)) {
                await GameAux.Message("You cannot make a move that removes a group's last liberty");
                return;
            }
            var cur_move = this.CurrentMove;
            if (cur_move != null)
                GameAux.PasteNextMove(cur_move, this.cutMove);
            else {
                if (this.FirstMove != null) {
                    // branching initial board state
                    if (this.Branches == null)
                        this.Branches = new List<Move>() { this.FirstMove, this.cutMove };
                    else
                        this.Branches.Add(this.cutMove);
                    this.FirstMove = this.cutMove;
                    this.FirstMove.Number = 1;
                }
                else {
                    MyDbg.Assert(this.State == GameState.NotStarted,
                                 "Internal error: no first move and game not started?!");
                    // not branching initial board state
                    this.FirstMove = this.cutMove;
                    this.FirstMove.Number = 1;
                    this.State = GameState.Started;
                }
                if (this.ParsedGame != null && this.cutMove.ParsedNode != null)
                    GameAux.PasteNextParsedNode(this.ParsedGame.Nodes, this.cutMove.ParsedNode);
            }
            this.cutMove.Previous = cur_move;  // stores null appropriately when no current
            this.Dirty = true;
            GameAux.RenumberMoves(this.cutMove);
            this.cutMove = null;
            await this.mainWin.DoNextButton();
            this.mainWin.UpdateTreeView(cur_move, true);
        }


        ////
        //// Adornments
        ////


        //// add_adornment creates the Adornments object in the model and adds it to
        //// move.  If move is None (or game not started), then this affects the
        //// initial game state.  This returns the new adornment.  If all the letter
        //// adornments have been used at this point in the game tree, then this
        //// adds nothing and returns None.
        ////
        public Adornments AddAdornment (Move move, int row, int col, AdornmentKind kind, string data = null) {
            Adornments adornment;
            if (this.State == GameState.NotStarted || move == null) {
                adornment = this.AddAdornmentMakeAdornment(this.SetupAdornments, row, col, kind, data);
                if (adornment == null) return null;
                this.SetupAdornments.Add(adornment);
            }
            else { //if (move != null) {
                adornment = this.AddAdornmentMakeAdornment(move.Adornments, row, col, kind, data);
                if (adornment == null) return null;
                move.AddAdornment(adornment);
            }
            return adornment;
        }

        private string[] capLetters = new string[] {"A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K",
                                                    "L", "M", "N", "O", "P", "Q", "R", "S", "T", "U", "V",
                                                    "W", "X", "Y", "Z"};
        private Adornments AddAdornmentMakeAdornment(List<Adornments> adornments, int row, int col, 
                                                     AdornmentKind kind, string data) {
            // Pass in adornments because access is different for initial board
            // state vs. a move.  Pass in data because Python has broken
            // closure semantics or poor lexical model, take your pick :-).
            if (kind == AdornmentKind.Letter && data == null) {
                // Collect the letter adornments for the move (or empty board)
                var letters = adornments.Where((a) => a.Kind == AdornmentKind.Letter).ToList();
                if (letters.Count == 26) // All letters in use
                    return null;
                foreach (var elt in capLetters)
                    // if the letter, elt, taken in order from A..Z, is not used in letters adornments, then
                    // return -1 means we can use that letter.
                    if (GameAux.ListFind<Adornments>(elt, letters, (x, y) => {
                        var cookie = ((Adornments)y).Cookie;
                        var lbl = ((Viewbox)cookie).Child;
                        // Check for win8 hack: Grid inside Viewbox because ViewBoxes and labels have no background.
                        var txtgrid = lbl as Grid;
                        if (txtgrid != null)
                            return (string)x == (string)((TextBlock)(txtgrid.Children[0])).Text;
                        else {
                            var txt = ((TextBlock)lbl).Text;
                            return (string)x == (string)txt;
                        }
                    }) == -1) {
                        data = elt; //chr(ord('A') +  len(letters))
                        break;
                    }
            }
            return new Adornments(kind, row, col, null, data);
        }

        //// GetAdornment returns the first (and should be only) adornment of kind kind
        //// at row, col (one-based model coordinates).  If there is no such adornment, this returns null.
        ////
        public Adornments GetAdornment (int row, int col, AdornmentKind kind) {
            var move = this.CurrentMove;
            List<Adornments> adornments =
                (this.State == GameState.NotStarted || move == null) ? this.SetupAdornments : move.Adornments;
            foreach (var a in adornments)
                if (a.Kind == kind && a.Row == row && a.Column == col)
                    return a;
            return null;
        }

        //// GetAdornments returns the current move's adornments at this location.
        //// Row, col are one-based model coordinates.  If there are no adornments, return empty collection.
        ////
        public List<Adornments> GetAdornments (int row, int col) {
            var move = this.CurrentMove;
            List<Adornments> adornments =
                (this.State == GameState.NotStarted || move == null) ? this.SetupAdornments : move.Adornments;
            return adornments.Where((a) => a.Row == row && a.Column == col).ToList();
        }

        
        //// remove_adornment assumes a is in the current adornments list, and
        //// signals an error if it is not.  You can always call this immediately
        //// after get_adornment if no move state has changed.
        ////
        public void RemoveAdornment (Adornments a) {
            var move = this.CurrentMove;
            List<Adornments> adornments =
                (this.State == GameState.NotStarted || move == null) ? this.SetupAdornments : move.Adornments;
            adornments.Remove(a);
        }


        ////
        //// Misc Branches UI helpers
        ////

        //// set_current_branch is a helper for UI that changes which branch to take
        //// following the current move.  Cur is the index of the selected item in
        //// the branches combo box, which maps to the branches list for the current
        //// move.
        ////
        public void SetCurrentBranch (int cur) {
            if (this.CurrentMove == null)
                this.FirstMove = this.Branches[cur];
            else
                this.CurrentMove.Next = this.CurrentMove.Branches[cur];
            this.mainWin.UpdateTreeViewBranch(this.CurrentMove);
        }

        //// move_branch_up and move_branch_down move the current move (if it
        //// follows a move or initial board state with branching) to be higher or
        //// lower in the previous branches list.  If the game hasn't started, or
        //// the conditions aren't met, this informs the user.
        ////
        public async Task MoveBranchUp () {
            var res = await this.BranchesForMoving();
            var branches = res.Item1;
            var cur_index = res.Item2;
            if (branches != null) {
                await this.MoveBranch(branches, cur_index, -1);
                this.Dirty = true;
                this.mainWin.UpdateTitle();
                this.mainWin.UpdateTreeView(this.CurrentMove, true);
            }
        }

        public async Task MoveBranchDown () {
            var res = await this.BranchesForMoving();
            var branches = res.Item1;
            var cur_index = res.Item2;
            if (branches != null) {
                await this.MoveBranch(branches, cur_index, 1);
                this.Dirty = true;
                this.mainWin.UpdateTitle();
                this.mainWin.UpdateTreeView(this.CurrentMove, true);
            }
        }

        //// _branches_for_moving returns the branches list (from previous move or
        //// intial board state) and the index in that list of the current move.
        //// This does user interaction for move_branch_up and move_branch_down.
        ////
        private async Task<Tuple<List<Move>, int>> BranchesForMoving () {
            // Check if have move
            if (this.State == GameState.NotStarted) {
                await GameAux.Message("Game not started, no branches to modify.");
                return new Tuple<List<Move>, int>(null, -1);
            }
            var current = this.CurrentMove;
            if (current == null) {
                await GameAux.Message("Must be on the first move of a branch to move it.");
                return new Tuple<List<Move>, int>(null, -1);
            }
            // Get appropriate branches
            var prev = current.Previous;
            List<Move> branches;
            if (prev == null)
                branches = this.Branches;
            else
                branches = prev.Branches;
            // Get index of current move in branches
            var cur_index = -1;
            if (branches == null) {
                await GameAux.Message("Must be on the first move of a branch to move it.");
                return new Tuple<List<Move>, int>(null, -1);
            }
            else if (prev == null)
                cur_index = branches.IndexOf(this.FirstMove);
            else
                cur_index = branches.IndexOf(prev.Next);
            // Successful result ...
            return new Tuple<List<Move>, int>(branches, cur_index);
        }

        //// _move_branch takes a list of brances and the index of a branch to move
        //// up or down, depending on delta.  This provides feedback to the user of
        //// the result.
        ////
        private async Task MoveBranch (List<Move> branches, int cur_index, int delta) {
            MyDbg.Assert(delta == 1 || delta == -1,
                         "Branch moving delta must be 1 or -1 for now.");
            Action swap = () => {
                var tmp = branches[cur_index];
                branches[cur_index] = branches[cur_index + delta];
                branches[cur_index + delta] = tmp;
            };
            if (delta < 0)
                // if moving up and current is not top ...
                if (cur_index > 0) {
                    swap();
                    await GameAux.Message("Branch moved up.");
                }
                else
                    await GameAux.Message("This branch is the main branch.");
            else if (delta > 0)
                // if moving down and current is not last ...
                if (cur_index < branches.Count - 1) {
                    swap();
                    await GameAux.Message("Branch moved down.");
                }
                else
                    await GameAux.Message("This branch is the last branch.");
        }


        ////
        //// File Writing
        ////

        //// write_game takes a storage to write an .sgf file.  This maps the game
        //// to a ParsedGame and uses its ToString() method to produce the output.
        //// This also handles if the write fails, which can happen if the user deletes the
        //// file after opening the file or since the last save.
        ////
        public async Task WriteGame (StorageFile sf = null, bool autosave = false) {
            string filename = null;
            if (sf == null) {
                // This debug.assert could arguably be a throw if we think of this function
                // as platform/library.
                MyDbg.Assert(this.Storage != null, "Need storage/filename to write file.");
                sf = this.Storage;
                filename = this.Filename;
            }
            else
                filename = sf.Name;
            var pg = this.UpdateParsedGameFromGame();
            var caughtException = false; // Needed since C# cannot await in catch blocks.
            try {
                await FileIO.WriteTextAsync(sf, pg.ToString());
            }
            // Catches case if user moved or deleted file while SGFEditor was holding a StorageFile on it.
            // For soem reason Windows does not just write the file again, which is good since user clearly screwed up.
            catch (FileNotFoundException) {
                caughtException = true;
            }
            // Win10 UWP build only has pseudo rare race condition where it cannot replace the existing file on write.
            // This never occurred for years of daily usage on win8 or win8 build on win10.
            // This catch block ignores it because always the next autosave succeeds.
            catch (Exception e) when (e.HResult == -2147023721) {
                //Debug.WriteLine("\nCaught bogus can't replace bug.\n");  // Comment out after verifying fix.
                //MyDbg.Assert(autosave, "What?! Bogus can't replace happened on c-s?!");
                return; // Nothing else to do, no need to update title for auto save
            }
            // Only exception we care about here is the user moved or deleted the file.
            if (caughtException) { // Probably should check for autosave file mishap and handle differently
                var res =  await GameAux.Message("You've moved or deleted the file (" + sf.Name +
                                                 ") since opening it or your last save.\n" +
                                                 "Save as?",
                                "Previous File Missing", new List<string>() {"Save As", "Cancel"});
                // There is a race condition here when auto saving calls WriteGame, but all outcomes of writing
                // should be fine, resulting in explicit save and possibly a remnant unnecessary auto-save file.
                if (res == "Save As") {
                    await this.mainWin.SaveAs();
                }
                return;
            }
            if (! autosave) {
                this.Dirty = false;
                SaveGameFileInfo(sf);  // In case save-as
                StorageFile autoSf = await this.mainWin.GetAutoSaveFile(this.mainWin.GetAutoSaveName(sf.Name));
                if (autoSf != null) {
                    await autoSf.DeleteAsync();
                }
            }
            this.mainWin.UpdateTitle();
        }

        //// SaveGameFileInfo updates the games storage object and filename properties.
        //// This is public since it is called from MainWindow.xaml.cs and App.xaml.cs.
        ////
        public void SaveGameFileInfo (StorageFile sf) {
            this.Storage = sf;
            var filename = sf.Name;
            this.Filename = filename;
            this.Filebase = filename.Substring(filename.LastIndexOf('\\') + 1);
        }

        //// write_flipped_game saves all the game moves as a diagonal mirror image.
        //// You can share a game you recorded with your opponents, and they can see
        //// it from their points of view.  (Properties to modify: AB, AW, B, W, LB,
        //// SQ, TR, MA.)  This does NOT update the view or the game to track the
        //// flipped file.  This does NOT set this.Dirty to false since the tracked
        //// file may be out of date with the state of the game.
        ////
        public async Task WriteFlippedGame (StorageFile sf) {
            MyDbg.Assert(sf != null, "Must call WriteFlippedGame with non-null file.");
            var savepg = this.ParsedGame; // Save unflipped, unrendered parsed nodes if any.
            var pg = this.UpdateParsedGameFromGame(true); // True = flipped
            this.ParsedGame = savepg;
            // Considered but don't seem to need to re-gen entire tree view model to ensure view model soundness.
            //this.mainWin.UpdateTreeView(this.CurrentMove, true);
            await FileIO.WriteTextAsync(sf, pg.ToString());
        }


        //// parsed_game_from_game returns a ParsedGame representing game, re-using
        //// existing parsed node properties where appropriate to avoid losing any we
        //// ignore from parsed files.  This stores the new ParsedGame into Game
        //// because re-using ParsedNode objects changes some previous pointners.
        //// We just keep the new one for consistency.  If flipped is true, then move
        //// and adornment indexes are diagonally mirrored; see write_flipped_game.
        ////
        internal ParsedGame UpdateParsedGameFromGame(bool flipped = false) {
            var pgame = new ParsedGame();
            //pgame.Nodes = this.GenParsedGameRoot(flipped);
            pgame.GameNode = this.GenParsedGameRoot(flipped);
            if (this.Branches == null) {
                if (this.FirstMove != null) {
                    //pgame.Nodes.Next = GameAux.GenParsedNodes(this.FirstMove, flipped);
                    pgame.Nodes = GameAux.GenParsedNodes(this.FirstMove, flipped);
                    // Only set Next to Game.FirstMove, but do not make first move Previous point at bogus GameNode.
                    pgame.GameNode.Next = pgame.Nodes;
                    //pgame.Nodes.Next.Previous = pgame.Nodes;
                }
            }
            else {
                var branches = new List<Move>();
                foreach (var m in this.Branches) {
                    var tmp = GameAux.GenParsedNodes(m, flipped);
                    branches.Add(tmp);
                    // Don't set Prev pointers to bogus GameNode because 1) Prev pointers don't matter for writing
                    // files and 2) if flipped == false, then setting Previous clobbers Move objects in Game.
                    //tmp.Previous = pgame.GameNode;
                }
                pgame.GameNode.Branches = branches; 
                pgame.GameNode.Next = branches[0];
            }
            // Need to store new game since creating the parsed game re-uses original nodes.
            //this.ParsedGame = pgame;
            // Integrity checking code for debugging and testing, not for release.
            //this.mainWin.CheckTreeParsedNodes();
            return pgame;
        }

        //// _gen_parsed_game_root returns a ParsedNode that is based on the Game object
        //// and that represents the first node in a ParsedGame.  It grabs any existing
        //// root node properties if there's an existing ParsedGame root node.  If
        //// flipped is true, then moves and adornment indexes are diagonally mirrored;
        //// see write_flipped_game.
        ////
        //// NOTE, this function needs to overwrite any node properties that the UI
        //// supports editing.  For example, if the end user can change the players
        //// names or rank, then this function needs to overwrite the node properties
        //// value with the game object's value.  It also needs to write properties from
        //// new games.
        ////
        private Move GenParsedGameRoot(bool flipped) {
            var n = new Move();
            if (this.ParsedProperties != null) {
                if (this.MiscGameInfo != null)
                    // If this is not null, then user edited properties in GameInfo dialog.
                    n.ParsedProperties = GameAux.CopyProperties(this.MiscGameInfo);
                else
                    // Misc properties still in parsed structure, so pass them through for saving.
                    //n.ParsedProperties = GameAux.CopyProperties(this.ParsedGame.Nodes.ParsedProperties);
                    n.ParsedProperties = GameAux.CopyProperties(this.ParsedProperties);

            }
            // From here on out, n is bogus node representing empty board and first SGF node, and since all
            // properties are copied from Game or parsed properties, it is ok if flipped clobbers indexes.
            var version = Windows.ApplicationModel.Package.Current.Id.Version;
            n.ParsedProperties["AP"] = new List<string>() {"SGFEditor:" + version.Major.ToString() + "." + 
                                                     version.Minor.ToString() + "." + version.Build.ToString()};
            n.ParsedProperties["SZ"] = new List<string>() { this.Board.Size.ToString() };
            // Comments
            if (n.ParsedProperties.ContainsKey("C"))
                // game.comments has merged GC and C comments.
                n.ParsedProperties.Remove("C");
            if (this.Comments != "")
                n.ParsedProperties["GC"] = new List<string>() { this.Comments };
            else if (n.ParsedProperties.ContainsKey("GC"))
                n.ParsedProperties.Remove("GC");
            // Komi
            n.ParsedProperties["KM"] = new List<string>() { this.Komi };
            // Handicap, all black, and all white properties
            this.GenParsedGameRootInitialStones(flipped, n);
            // Player names
            n.ParsedProperties["PB"] = new List<string>() { this.PlayerBlack != "" ? this.PlayerBlack : "Black" };
            n.ParsedProperties["PW"] = new List<string>() { this.PlayerWhite != "" ? this.PlayerWhite : "White" };
            return n;
        }

        //// GenParsedGameRootInitialStones sets handicap (HA), all black (AB), and all white (AW)
        //// properties.  You can have all black with a zero handicap, that is, if a program supported
        //// just laying down stones in a example pattern.
        ////
        ////
        private void GenParsedGameRootInitialStones (bool flipped, Move n) {
            // HA
            if (this.Handicap != 0)
                n.ParsedProperties["HA"] = new List<string>() { this.Handicap.ToString() };
            else if (n.ParsedProperties.ContainsKey("HA"))
                n.ParsedProperties.Remove("HA");
            // AB
            if (n.ParsedProperties.ContainsKey("AB")) {
                // Prefer to keep what we parsed.
                if (flipped)
                    n.ParsedProperties["AB"] = GameAux.FlipCoordinates(n.ParsedProperties["AB"]);
                // else leave them as-is
            }
            else
                if (this.HandicapMoves != null)
                    n.ParsedProperties["AB"] =
                        this.HandicapMoves.Select((m) => GoBoardAux.GetParsedCoordinates(m, flipped))
                                           .ToList();
            // AW
            if (n.ParsedProperties.ContainsKey("AW")) {
                // Prefer to keep what we parsed.
                if (flipped)
                    n.ParsedProperties["AW"] = GameAux.FlipCoordinates(n.ParsedProperties["AW"]);
                // else leave them as-is
            }
            // Should have AW in properties or null AllWhiteMoves since don't support adding all white setup.
            else if (this.AllWhiteMoves != null) { 
                n.ParsedProperties["AW"] =
                    this.AllWhiteMoves.Select((m) => GoBoardAux.GetParsedCoordinates(m, flipped))
                                        .ToList();
            }
        }



        ////
        //// Utils for Jumping in Game Tree and Resuming State
        ////

        public List<Tuple<int, int>> TheEmptyMovePath = new List<Tuple<int, int>>() { Tuple.Create(0, -1) };

        //// GetPathToMove returns a list of tuples, the first int of which is a move
        //// number to move to paired with what branch to take at that move.  The last
        //// move (the argument) has a sentinel -1 branch int.  Only moves that take
        //// an alternative branch (not branch zero) are in the result.  This assumes
        //// move is in the game and on the board, asserting if not.  The 0,-1 tuple
        //// indicates the empty initial board state, or the empty path.
        ////
        public List<Tuple<int, int>> GetPathToMove (Move move) {
            MyDbg.Assert(move != null);
            //if (move == null)
            //    return this.TheEmptyMovePath;
            var parent = move.Previous;
            var res = new List<Tuple<int, int>>() { Tuple.Create(move.Number, -1) };
            while (parent != null) {
                if (parent.Branches != null && parent.Branches[0] != move) {
                    var loc = GameAux.ListFind(move, parent.Branches);
                    MyDbg.Assert(loc != -1, "Move must be in game.");
                    res.Add(Tuple.Create(parent.Number, loc));
                }
                move = parent;
                parent = move.Previous;
            }
            if (this.Branches != null && this.Branches[0] != move) {
                var loc = GameAux.ListFind(move, this.Branches);
                MyDbg.Assert(loc != -1, "Move must be in game.");
                res.Add(Tuple.Create(0, loc));
            }
            res.Reverse();
            return res;
        }

        public List<Tuple<int, int>> GetPathToMove (ParsedNode move) {
            MyDbg.Assert(move != null);
            //if (move == null)
            //    return this.TheEmptyMovePath;
            var parent = move.Previous;
            if (parent == null)
                return this.TheEmptyMovePath;
            // No move nums in parsed nodes, so count down, then fix numbers at end.
            var moveNum = 1000000;
            var res = new List<Tuple<int, int>>() { Tuple.Create(moveNum, -1) };
            while (parent != this.ParsedGame.Nodes) {
                moveNum -= 1;
                if (parent.Branches != null && parent.Branches[0] != move) {
                    var loc = GameAux.ListFind(move, parent.Branches);
                    MyDbg.Assert(loc != -1, "Move must be in game.");
                    res.Add(Tuple.Create(moveNum, loc));
                }
                move = parent;
                parent = move.Previous;
            }
            moveNum -= 1;
            // Add tuple for move zero if we need to select a branch from the empty oard state.
            if (this.ParsedGame.Nodes.Branches != null) {
                var loc = GameAux.ListFind(move, this.ParsedGame.Nodes.Branches);
                MyDbg.Assert(loc != -1, "Move must be in game.");
                res.Add(Tuple.Create(moveNum, loc)); // moveNum becomes a zero when we fix numbers below.
            }
            // Fix up numbers to match move numbers.
            var final = res.Select((pair) => Tuple.Create(pair.Item1 - moveNum, pair.Item2)).ToList();
            //var final = new List<Tuple<int, int>>();
            //foreach (var pair in res) {
            //    final.Add(Tuple.Create(pair.Item1 - moveNum, pair.Item2));
            //}
            final.Reverse();
            return final;
        }

        //// AdvanceToMovePath takes a path, where each tuple is a move number and the
        //// branch index to take at that move.  The branch is -1 for the last move.
        //// This returns true if successful, null if the path was bogus, or we encounter
        //// a move conflict in the game tree (can happen from pastes).
        ////
        public bool AdvanceToMovePath (List<Tuple<int, int>> path) {
            MyDbg.Assert(this.CurrentMove == null, "Must be at beginning of empty game board.");
            MyDbg.Assert(this.FirstMove != null || path == this.TheEmptyMovePath,
                         "If first move is null, then path must be the empty path.");
            if (this.FirstMove == null) return true;
            // Setup for loop ...
            if (path[0].Item1 == 0) {
                this.SetCurrentBranch(path[0].Item2);
                path.RemoveAt(0);
            }
            else if (this.Branches != null && this.FirstMove != this.Branches[0]) {
                this.SetCurrentBranch(0);
            }
            var curMove = this.FirstMove; // Set after possible call to SetCurrentBranch.
            if (this.ReplayMoveUpdateModel(curMove) == null) // may have issue with rendering parsed node
                return false;
            this.mainWin.AddNextStoneNoCurrent(curMove);
            var next = curMove.Next;
            // Walk to last move on path ...
            foreach (var n in path) {
                var target = n.Item1;
                // Play moves with no branches or all taking default zero branch ...
                while (curMove.Number != target) {
                    if (curMove.Branches != null && curMove.Branches[0] != next) {
                        this.CurrentMove = curMove; // Must set this before calling SetCurrentBranch.
                        this.SetCurrentBranch(0);
                        next = curMove.Next; // Update next, now that curMove is updated.
                    }
                    if (this.ReplayMoveUpdateModel(next) == null) {
                        // had issue with rendering parsed node or conflicting location for pasted move
                        this.CurrentMove = curMove;  // Restore state to clean up.
                        return false;
                    }
                    this.mainWin.AddNextStoneNoCurrent(next);
                    curMove = next;
                    next = curMove.Next;
                }
                // Select next moves branch correctly ...
                var branch = n.Item2;
                if (branch == -1) break;
                MyDbg.Assert(curMove.Branches != null && branch > 0 && branch < curMove.Branches.Count,
                             "Move path does not match game's tree.");
                this.CurrentMove = curMove; // Needs to be right for SetCurrentBranch.
                this.SetCurrentBranch(branch);
                next = curMove.Next; // Update next, now that curMove is updated.
                if (this.ReplayMoveUpdateModel(next) == null)
                    return false;
                this.mainWin.AddNextStoneNoCurrent(next);
                curMove = next;
                next = curMove.Next;
            }
            this.CurrentMove = curMove;
            return true;
        }

    } // Game



    //// GameState helps check for consistency with whether there should be a first move, to set first move
    //// appropriately when making a move, and to check whether to affect initial board state or a move.  NOTE,
    //// a game could be NotStarted and dirty if you deleted all moves without saving.
    ////
    //// CLEANUP: It should be that Started is true if and only if FirstMove is non-null.  In fact, that's
    //// asserted in a couple of places.  Obviously, the state here is dual, so could use a bool if keep this.
    //// While it may make some code cleaner to read, possibly should kill this in lieu of just using FirstMove,
    //// or IsStarted property implemented by looking at FirstMove.
    ////
    public enum GameState {
        NotStarted, Started
    }



    //// GameAux provides stateless helpers for Game and MainWindow.  Members
    //// marked internal should only be used by Game.
    ////
    public static class GameAux {

        //// User notification and prompting utility.
        ////
        //// Since Game/GameAux is the controller (and view-model) in MVVC, this is the
        //// location for this since it is used by Game and MainWindow.
        ////

        public static string OkMessage = "OK";
        public static string YesMessage = "Yes";


        //// Message is a lot like WPF MessageBox.Show.  It returns the string name of the command selected.
        //// If cmds is null, there is a simple msgbox with an OK button, which is the default (enter) and the
        //// cancel default (escape) commands.  If cmds is not null, the dialog shows buttons for the listed
        //// commands.  The default command is the first element unless defaultIndex is specified.
        ////
        public static async Task<string> Message (string msg, string title = null, List<string> cmds = null,
                                                  uint defaultIndex = 0, uint cancelIndex = 9999) {
            // Create the message dialog and set its content 
            var msgdlg = new MessageDialog(msg, title ?? "");
            string response = "";
            if (cmds == null) {
                msgdlg.Commands.Add(new UICommand(GameAux.OkMessage));
                msgdlg.DefaultCommandIndex = 0;
                msgdlg.CancelCommandIndex = 0;
                response = GameAux.OkMessage;
            }
            else {
                foreach (var c in cmds)
                    msgdlg.Commands.Add(new UICommand(c, new UICommandInvokedHandler((cmd) => response = cmd.Label)));
                //msgdlg.Commands.Add(new UICommand("Close", new UICommandInvokedHandler(
                //                                                   GameAux.StonesPtrPressedMsgDlgHandler)));
                // Set the command that will be invoked by default (enter)
                msgdlg.DefaultCommandIndex = defaultIndex;
                // Set the command to be invoked when escape is pressed 
                msgdlg.CancelCommandIndex = cancelIndex == 9999 ? (uint)(cmds.Count() - 1) : cancelIndex;
            }
            // Show the message dialog 
            await msgdlg.ShowAsync();
            return response;
        }


        ////
        //// Mapping Games to ParsedGames (for printing)
        ////

        //// CopyProperties returns a mostly shallow copy of props.  The returned dictionary is new, and
        //// the list of string values is new, but the strings are shared.
        ////
        public static Dictionary<string, List<string>> CopyProperties (Dictionary<string, List<string>> props) {
            var res = new Dictionary<string, List<string>>();
            foreach (var kv in props)
                res[kv.Key] = kv.Value.GetRange(0, kv.Value.Count);
            return res;
        }

        //// _gen_parsed_nodes returns a ParsedNode with all the moves following move
        //// represented in the linked list.  If move has never been rendered, then the
        //// rest of the list is the parsed nodes hanging from it since the user could
        //// not have modified the game at this point.  This recurses on children of move objects
        //// with branches.  If flipped is true, then moves and adornment indexes are
        //// diagonally mirrored; see write_flipped_game.
        ////
        public static Move GenParsedNodes (Move move, bool flipped) {
            if (! move.Rendered) {
                // If move exists and not rendered, then must be ParsedNode.
                if (flipped)
                    return CloneAndFlipNodes(move);
                else
                    // Note: result re-uses original parsed nodes, and callers change this node
                    // to point to new parents.
                    return move;
            }
            var cur_node = GenParsedNode(move, flipped);
            var first = cur_node;
            if (move.Branches == null) {
                move = move.Next;
                while (move != null) {
                    if (flipped) { // then new objects, so need to link them. Prev ptrs don't actually matter.
                        cur_node.Next = GenParsedNode(move, flipped);
                        //cur_node.Next.Previous = cur_node;
                    }
                    if (move.Branches == null) {
                        cur_node = cur_node.Next;
                        move = move.Next;
                    }
                    else {
                        cur_node = cur_node.Next;
                        break;
                    }
                } // while
            } // if
            // Only get here when move is None, or we're recursing on branches.
            if (move != null) {
                if (flipped) // Generating new nodes, so need new branches list.
                    cur_node.Branches = new List<Move>();
                foreach (var m in move.Branches) {
                    var tmp = GenParsedNodes(m, flipped);
                    if (flipped) {
                        cur_node.Branches.Add(tmp);
                        tmp.Previous = cur_node;
                    }
                }
                if (flipped)
                    cur_node.Next = cur_node.Branches[0];
            }
            return first;
        } // _gen_parsed_nodes

        //// _gen_parsed_node returns move or a clone of move, depending on flipped.  This
        //// grabs any existing parsed node properties from move to preserve any move
        //// properties that we ignore from a file we read.  If flipped is true, then moves 
        //// and adornment indexes are diagonally mirrored; see write_flipped_game.
        ////
        //// NOTE, this function needs to overwrite any node ParsedProperties that the UI
        //// supports editing.  For example, if the end user modified adornments.  If flipped,
        //// then we clone move, so ok to clobber ParsedProperties.  If not flipped, we always
        //// overwrite the ParsedProperties based on Game move state, and it is fine that they
        //// no longer represent what we read from a file.
        ////
        private static Move GenParsedNode (Move move, bool flipped) {
            if (! move.Rendered) {
                // If move exists and not rendered, then must be ParsedNode.
                if (flipped)
                    return CloneAndFlipNodes(move);
                else
                    // Note: result re-uses original parsed nodes, and callers change this node
                    // to point to new parents.
                    //return move.ParsedNode;
                    return move;
            }
            //var node = new ParsedNode();
            var node = flipped ? new Move() : move;
            //node.ParsedProperties = (move.ParsedNode != null) ? CopyProperties(move.ParsedNode.ParsedProperties)
            //                                             : node.ParsedProperties;
            if (flipped) node.ParsedProperties = GameAux.CopyProperties(move.ParsedProperties);
            // Even if not flipped and clobbering move object from Game, it is ok to overwrite ParsedProperty values
            // with current Game/move state.
            //
            var props = node.ParsedProperties;
            // Color
            MyDbg.Assert(move.Color == Colors.Black || move.Color == Colors.White,
                         "Move color must be B or W?!");
            if (move.Color == Colors.Black)
                props["B"] = new List<string>() { GoBoardAux.GetParsedCoordinates(move, flipped) };
            else if (move.Color == Colors.White)
                props["W"] = new List<string>() { GoBoardAux.GetParsedCoordinates(move, flipped) };
            // Comments
            if (move.Comments != "")
                props["C"] = new List<string>() { move.Comments };
            else if (props.ContainsKey("C"))
                props.Remove("C");
            // Adornments
            if (props.ContainsKey("TR"))
                props.Remove("TR");
            if (props.ContainsKey("SQ"))
                props.Remove("SQ");
            if (props.ContainsKey("LB"))
                props.Remove("LB");
            foreach (var a in move.Adornments) {
                var coords = GoBoardAux.GetParsedCoordinatesA(a, flipped);
                if (a.Kind == AdornmentKind.Triangle) {
                    if (props.ContainsKey("TR"))
                        props["TR"].Add(coords);
                    else
                        props["TR"] = new List<string>() { coords };
                }
                if (a.Kind == AdornmentKind.Square) {
                    if (props.ContainsKey("SQ"))
                        props["SQ"].Add(coords);
                    else
                        props["SQ"] = new List<string>() { coords };
                }
                if (a.Kind == AdornmentKind.Letter) {
                    var cookie = ((Adornments)a).Cookie;
                    var lbl = ((Viewbox)cookie).Child;
                    string txt;
                    // Check for win8 hack: Grid inside Viewbox because ViewBoxes and labels have no background.
                    var txtgrid = lbl as Grid;
                    if (txtgrid != null)
                        txt = ((TextBlock)(txtgrid.Children[0])).Text;
                    else
                        txt = ((TextBlock)lbl).Text;
                    var data = coords + ":" + txt;
                    if (props.ContainsKey("LB"))
                        props["LB"].Add(data);
                    else
                        props["LB"] = new List<string>() { data };
                }
            } // foreach
            return node;
        } // _gen_parsed_node

        //// _clone_and_flip_nodes is similar to _gen_parsed_nodes.  This returns a
        //// ParsedNode with all the nodes following the argument represented in the
        //// resulting linked list, but their coordinates have been transposed to the
        //// diagonal mirror image, see write_flipped_game.  This recurses on nodes with
        //// branches.
        ////
        private static Move CloneAndFlipNodes (Move nodes) {
            var first = CloneAndFlipNode(nodes);
            var cur_node = first;
            if (nodes.Branches == null) {
                nodes = nodes.Next;
                while (nodes != null) {
                    cur_node.Next = CloneAndFlipNode(nodes);
                    // Prev not used when writing files, but it doesn't hurt to set it on cloned nodes.
                    cur_node.Next.Previous = cur_node;
                    if (nodes.Branches == null) {
                        cur_node = cur_node.Next;
                        nodes = nodes.Next;
                    }
                    else {
                        cur_node = cur_node.Next;
                        break;
                    }
                }
            }
            // Only get here when nodes is None from while loop, or we're recursing on branches.
            if (nodes != null) {
                cur_node.Branches = new List<Move>();
                foreach (var m in nodes.Branches) {
                    var tmp = CloneAndFlipNodes(m);
                    cur_node.Branches.Add(tmp);
                    // Prev not used when writing files, but it doesn't hurt to set it on cloned nodes.
                    tmp.Previous = cur_node;
                }
                cur_node.Next = cur_node.Branches[0];
            }
            return first;
        }

        //// _clone_and_flip_node is similar to _gen_parsed_node.  This returns a
        //// ParsedNode that is a clone of node, but any indexes are diagonally mirror
        //// transposed, see write_flipped_game.
        ////
        private static Move CloneAndFlipNode (Move node) {
            var new_node = new Move();
            new_node.ParsedProperties = CopyProperties(node.ParsedProperties);
            var props = new_node.ParsedProperties;
            // Color
            MyDbg.Assert(props.ContainsKey("B") || props.ContainsKey("W"),
                         "Move color must be B or W?!");
            if (props.ContainsKey("B"))
                props["B"] = FlipCoordinates(props["B"]);
            else if (props.ContainsKey("W"))
                props["W"] = FlipCoordinates(props["W"]);
            // Adornments
            if (props.ContainsKey("TR"))
                props["TR"] = FlipCoordinates(props["TR"]);
            if (props.ContainsKey("SQ"))
                props["SQ"] = FlipCoordinates(props["SQ"]);
            if (props.ContainsKey("LB"))
                props["LB"] = FlipCoordinates(props["LB"], true);
            return new_node;
        }

        //// flip_coordinates takes a list of parsed coordinate strings and returns the
        //// same kind of list with the coorindates diagonally flipped (see
        //// write_flipped_game).
        ////
        public static List<string> FlipCoordinates (List<string> coords, bool labels = false) {
            if (labels)
                // coords elts are "<col><row>:<letter>"
                return GameAux.FlipCoordinates(coords.Select((c) => c.Substring(0, c.Length - 2)).ToList())
                              .Zip(coords.Select((c) => c.Substring(2, 2)),
                                   (x, y) => x + y)
                              .ToList();
            //[x + y for x in flip_coordinates([l[:2] for l in coords])
            //       for y in [lb[2:] for lb in coords]]
            else
                return coords.Select((c) => GoBoardAux.FlipParsedCoordinates(c)).ToList();
            //[goboard.flip_parsed_coordinates(yx) for yx in coords]
        }


        ////
        //// Misc Helper Functions for Game Consumers
        ////

        public static Color OppositeMoveColor (Color color) {
            return color == Colors.Black ? Colors.White : Colors.Black;
        }

        //// CompareComments exists because UWP randomly between releases changed the TextBox behavior
        //// to always return newline sequences as \r, regardless of what you put into it.  WinRT TextBox
        //// kept \r\n, so reading files, storing comments in moves, and writing files was all consistent.
        //// Now we have to explicitly compare and fix strings on the way out of TextBoxes.  This returns
        //// if the strings match, and if they do not, then it also returns the fixed up string for storing
        //// and writing to files.
        ////
        public static Tuple<bool, string> CompareComments (string uitext, string movetext) {
            // Don't shortcut and check for len becuase this is all about disparity in line endings.
            int i = 0;
            int j = 0;
            bool different = false;
            int newlinecount = 0;
            while (i < uitext.Length && j < movetext.Length) {
                // Common case until newline
                if (uitext[i] == movetext[j]) {
                    i += 1;
                    j += 1;
                    continue;
                }
                // Common case on mismatch
                if (i > 0 && uitext[i - 1] == '\r' && movetext[j] == '\n') {
                    j += 1;
                    newlinecount += 1;
                } // Or the strings are just different at this i and j
                else {
                    different = true;
                    break;
                }
            }
            if (different) { // Fix up new comment text
                return new Tuple<bool, string>(false, uitext.Replace("\r", Environment.NewLine));
            }
            // Common case of same text: uitext at end, and newline is last char in movetext
            if (i == uitext.Length && j == movetext.Length - 1 && movetext[j] == '\n') {
                return new Tuple<bool, string>(true, null);
            }
            // If-then-else handles when last line does not end with newline sequence.
            // If no differences seen, then some length terminated loop, and same lengths means same strings
            if (newlinecount == 0) {
                if (uitext.Length == movetext.Length) {
                    // No newlines to separate indexes, no differences, and same lengths terminated loop
                    return new Tuple<bool, string>(true, null);
                }
            }
            else if (movetext.Length == uitext.Length + newlinecount) {
                // Saw newlines separate indexes, but no differences, and same lengths except newlines
                // This happens when last line is not newline terminated.
                return new Tuple<bool, string>(true, null);
            }
            // Fix up new comment text
            return new Tuple<bool, string>(false, uitext.Replace("\r", Environment.NewLine));
        }


        //// CreateDefaultGame stashed the new game in DefaultGame so that we can throw it away if
        //// the user does not use it and opens a file or creates a new game.
        ////
        public static Game CreateDefaultGame (MainWindow mainwin) {
            mainwin.DefaultGame = GameAux.CreateGame(mainwin, Game.MaxBoardSize, 0, Game.DefaultKomi);
            return mainwin.DefaultGame;
        }
        
        public static Game CreateGame (MainWindow mainwin, int size, int handicap, string komi,
                                       List<Move> handicap_stones = null, List<Move> all_white = null) {
            var g = new Game(mainwin, size, handicap, komi, handicap_stones, all_white);
            mainwin.SetupBoardDisplay(g);
            // Must set Game after calling SetupBoardDisplay.
            mainwin.AddGame(g);
            return g;
        }


        //// _list_find returns the index of elt in the list argument using the compare function.
        //// Elt is the first argument to the compare function, elements from the list are second.
        //// The test defaults to identity.
        ////
        public static int ListFind<T> (object elt, List<T> l, Func<object, object, bool> compare = null) {
            if (compare == null)
                compare = object.ReferenceEquals;
            for (int i = 0; i < l.Count; i++)
                if (compare(elt, l[i]))
                    return i;
            return -1;
        }


        //// create_parsed_game takes a ParsedGame and main UI window.  It creates a new
        //// Game (which cleans up the current game) and sets up the first moves so that
        //// the user can start advancing through the moves.
        ////
        public static async Task<Game> CreateParsedGame (ParsedGame pgame, MainWindow main_win) {
            // Check some root properties
            var props = pgame.Nodes.Properties;
            // Handicap stones
            int handicap;
            List<Move> all_black;
            if (props.ContainsKey("HA")) {
                var stuff = CreateParsedGameHandicap(pgame, props);
                handicap = stuff.Item1;
                all_black = stuff.Item2;
            }
            else {
                handicap = 0;
                // There may be all black stone placements even if there is no handicap property since some programs
                // allow explicit stone placements of black stones that get written to the initial board properties.
                if (props.ContainsKey("AB"))
                    all_black = CreateParsedGameAllBlack(pgame, props);
                else
                    all_black = null;
            }
            List<Move> all_white = null;
            if (props.ContainsKey("AW")) {
                all_white = CreateParsedGameAllWhite(pgame, props);
                //throw new Exception("Don't support multiple white stones at root.");
            }
            // Board size
            var size = Game.MaxBoardSize;
            if (props.ContainsKey("SZ"))
                size = int.Parse(props["SZ"][0]);
            else
                await GameAux.Message("No SZ, size, property in .sgf.  Default is 19x19");
            if (size != Game.MaxBoardSize)
                //MessageBox.Show("Only work with size 19 currently, got " + size.ToString());
                throw new Exception("Only work with size 19 currently, got " + size.ToString());
            // Komi
            string komi;
            if (props.ContainsKey("KM"))
                komi = props["KM"][0];
            else
                komi = handicap == 0 ? Game.DefaultKomi : "0.5";
            // Create new game and clean up current game
            var g = GameAux.CreateGame(main_win, size, handicap, komi, all_black, all_white);
            // Player names
            if (props.ContainsKey("PB"))
                g.PlayerBlack = props["PB"][0];
            if (props.ContainsKey("PW"))
                g.PlayerWhite = props["PW"][0];
            // Initial board state comments
            if (props.ContainsKey("C"))
                g.Comments = props["C"][0];
            if (props.ContainsKey("GC"))
                g.Comments = props["GC"][0] + g.Comments;
            // Setup remaining model for first moves and UI
            //g.ParsedGame = pgame;
            g.ParsedProperties = props; // Save these for pass through properties when writing files.
            GameAux.SetupFirstParsedMove(g, pgame.Nodes);
            // Setup navigation UI so that user can advance through game.
            if (g.FirstMove != null) {
                // No first move if file just has handicap stones.
                g.State = GameState.Started;
                main_win.EnableForwardButtons();
                main_win.UpdateBranchCombo(g.Branches, g.FirstMove);
            }
            else {
                main_win.DisableForwardButtons();
                main_win.UpdateBranchCombo(g.Branches, null);
            }
            main_win.CurrentComment = g.Comments;
            main_win.Game = g;
            return g;
        }

        //// CreateParsedGameHandicap helps create a Game from a ParsedGame by processing the handicap (HA)
        //// and all black (AB) properties.  It returns the handicap number and the Moves for the stones.
        //// This assumes there is an HA property, so check before calling.
        ////
        private static Tuple<int, List<Move>> CreateParsedGameHandicap (ParsedGame pgame,
                                                                        Dictionary<string, List<string>> props) {
            // Handicap stones
            int handicap;
            List<Move> all_black;
            handicap = int.Parse(props["HA"][0]);
            if (handicap == 0)
                all_black = null;
            // KGS saves HA[6] and then AB[]...
            else if (! props.ContainsKey("AB"))
                throw new Exception("If parsed game has handicap, then need handicap stones.");
            else if (props["AB"].Count != handicap)
                throw new Exception("Parsed game's handicap count (HA) does not match stones (AB).");
            else
                all_black = CreateParsedGameAllBlack(pgame, props);
            return Tuple.Create(handicap, all_black);
        }

        //// CreateParsedGameAllBlack helps create a Game from a ParsedGame by processing the all black (AB)
        //// properties.
        ////
        private static List<Move> CreateParsedGameAllBlack (ParsedGame pgame, Dictionary<string, List<string>> props) {
            return props["AB"].Select((coords) => {
                var tmp = GoBoardAux.ParsedToModelCoordinates(coords);
                var row = tmp.Item1;
                var col = tmp.Item2;
                var m = new Move(row, col, Colors.Black);
                m.ParsedNode = pgame.Nodes;
                m.Rendered = false;
                return m;
            }).ToList();
        }

        private static List<Move> CreateParsedGameAllWhite (ParsedGame pgame, Dictionary<string, List<string>> props) {
            return props["AW"].Select((coords) => {
                var tmp = GoBoardAux.ParsedToModelCoordinates(coords);
                var row = tmp.Item1;
                var col = tmp.Item2;
                var m = new Move(row, col, Colors.White);
                m.ParsedNode = pgame.Nodes;
                m.Rendered = false;
                return m;
            }).ToList();
        }


        //// _setup_first_parsed_move takes a game and the head of ParsedNodes.  It sets
        //// up the intial move models, handling initial node branching and so on.  The
        //// basic invariant here is that we always have the next move models created,
        //// but they are in an unrendered state.  This means their branches have not
        //// been processed, adornments have never been created, captured stones never
        //// processed, etc.  When we advance to a move, we render it and set up its
        //// next move(s) as unrendered.  Keeping the next move pointer of a Move object
        //// set up makes several other invariants in helper functions and game
        //// processing fall out.  This function returns a None g.first_move if the .sgf
        //// file only had a root node.
        ////
        private static Move SetupFirstParsedMove (Game g, ParsedNode nodes) {
            var props = nodes.Properties;
            if (props.ContainsKey("B") || props.ContainsKey("W"))
                throw new Exception("Unexpected move in root parsed node.");
            if (props.ContainsKey("PL"))
                throw new Exception("Do not support player-to-play for changing start color.");
            if (props.ContainsKey("TR") || props.ContainsKey("SQ") || props.ContainsKey("LB"))
                throw new Exception("Don't handle adornments on initial board from parsed game yet.");
            Move m;
            if (nodes.Branches != null) {
                // Game starts with branches
                var moves = new List<Move>();
                foreach (var n in nodes.Branches) {
                    m = ParsedNodeToMove(n);
                    if (m == null) {
                        MyDbg.Assert(n.BadNodeMessage != null);
                        throw new Exception(n.BadNodeMessage);
                    }
                    m.Number = g.MoveCount + 1;
                    // Don't set m.Previous since they are fist moves.
                    moves.Add(m);
                }
                g.Branches = moves;
                m = moves[0];
            }
            else {
                nodes = nodes.Next;
                if (nodes == null)
                    m = null;
                else {
                    m = ParsedNodeToMove(nodes);
                    if (m == null) {
                        MyDbg.Assert(nodes.BadNodeMessage != null);
                        throw new Exception(nodes.BadNodeMessage);
                    }

                    // Note, do not incr g.move_count since first move has not been rendered,
                    // so if user clicks, that should be number 1 too.
                    m.Number = g.MoveCount + 1;
                }
            }
            g.FirstMove = m;
            return m;
        }

        //// _parsed_node_to_move takes a ParsedNode and returns a Move model for it.
        //// For now, this is fairly constrained to expected next move colors and no
        //// random setup nodes that place several moves or just place adornments.
        //// This returns null for failure cases, setting the parse nodes error msg.
        ////
        internal static Move ParsedNodeToMove (ParsedNode n) {
            if (n.BadNodeMessage != null) return null;
            Color color;
            int row;
            int col;
            if (n.Properties.ContainsKey("B")) {
                color = Colors.Black;
                var tmp = GoBoardAux.ParsedToModelCoordinates(n.Properties["B"][0]);
                row = tmp.Item1;
                col = tmp.Item2;
            }
            else if (n.Properties.ContainsKey("W")) {
                color = Colors.White;
                var tmp = GoBoardAux.ParsedToModelCoordinates(n.Properties["W"][0]);
                row = tmp.Item1;
                col = tmp.Item2;
            }
            else {
                n.BadNodeMessage = "Next nodes must be moves, don't handle arbitrary nodes yet -- " +
                                   n.NodeString(false);
                return null;
            }
            var m = new Move(row, col, color);
            m.ParsedNode = n;
            m.Rendered = false;
            if (n.Properties.ContainsKey("C"))
                m.Comments = n.Properties["C"][0];
            return m;
        }


        //// NextMoveDisplayError figures out the error msg for the user when trying to replay
        //// or render next moves.  It gives a general message if there is nothing specific.
        ////
        internal static async Task NextMoveDisplayError (Move move) {
            var msg = GameAux.NextMoveGetMessage(move);
            if (msg != null)
                await GameAux.Message(msg);
            else
                await GameAux.Message("You are likely replaying moves from a pasted branch that have conflicts " +
                                      "with stones on the board, or you encounted a node with bad properties " +
                                      "from and SGF file.");
        }

        //// NextMoveGetMessage digs into move and next moves to see if rendering parse nodes put
        //// an error msg into one of these nodes, then returns the string or null.
        ////
        internal static string NextMoveGetMessage (Move move) {
            var pn = move.ParsedNode;
            if (pn == null) return null;
            var msg = pn.BadNodeMessage;
            if (msg != null) return msg;
            // Check branches because one of them could have been the culprit.
            if (pn.Branches != null) {
                foreach (var n in pn.Branches) {
                    if (n.BadNodeMessage != null)
                        return "Rendering next move's branch nodes ...\n" + n.BadNodeMessage;
                }
            }
            // It is hard to know if it is move, next move, or branches that have the bad msg,
            // so search heuristically, assuming first found is the one since it is likely the culprit.
            if (pn.Next != null) {
                var nextpn = pn.Next;
                if (nextpn.BadNodeMessage != null) 
                    return "Rendering the next move's next move ...\n" + nextpn.BadNodeMessage;
                if (nextpn.Branches != null)
                    foreach (var n in nextpn.Branches) {
                        if (n.BadNodeMessage != null)
                            return "Rendering the next move's next move's branches ...\n" + n.BadNodeMessage;
                    }
            }
            return null;
        }


        ////
        //// Internal utilities for Game methods.
        ////

        //// _paste_next_move takes a Move that is the current move to
        //// which _paste_next_move adds cut_move as the next move.  This sets up next
        //// pointers and the branches list appropriately for the move.
        ////
        internal static void PasteNextMove (Move move, Move cut_move) {
            if (move.Next != null) {
                if (move.Branches == null)
                    // need branches
                    move.Branches = new List<Move>() { move.Next, cut_move };
                else
                    move.Branches.Add(cut_move);
                move.Next = cut_move;
            }
            else
                move.Next = cut_move;
            cut_move.Previous = move;
            move.Next.Number = move.Number + 1;
            if (move.ParsedNode != null && cut_move.ParsedNode != null)
                PasteNextParsedNode(move.ParsedNode, cut_move.ParsedNode);
        }

        //// _paste_next_parsed_node is very much like _paste_next_move but for ParsedNodes.
        //// I could have used dynamic for the parameters, but the function required an 'is'
        //// type test which is of course a red flag with dynamic.
        ////
        internal static void PasteNextParsedNode (ParsedNode pn, ParsedNode cut_move) {
            if (pn.Next != null) {
                if (pn.Branches == null)
                    // need branches
                    pn.Branches = new List<ParsedNode>() { pn.Next, cut_move };
                else
                    pn.Branches.Add(cut_move);
                pn.Next = cut_move;
            }
            else
                pn.Next = cut_move;
            cut_move.Previous = pn;
        }


        //// _renumber_moves takes a move with the correct number assignment and walks
        //// the sub tree of moves to reassign new numbers to the nodes.  This is used
        //// by game._paste_move.
        ////
        internal static void RenumberMoves (Move move) {
            var count = move.Number;
            if (move.Branches == null) {
                move = move.Next;
                while (move != null) {
                    move.Number = count + 1;
                    count += 1;
                    if (move.Branches == null)
                        move = move.Next;
                    else
                        break;
                }
            }
            // Only get here when move is None, or we're recursing on branches.
            if (move != null)
                foreach (var m in move.Branches) {
                    m.Number = count;
                    RenumberMoves(m);
                }
        }

        //// _check_for_coincident_moves checks if every move in a cut tree can play
        //// where pasted to ensure no conflicts.
        ////
        //// Turns out this test isn't so good.  Need to do "abstract interpretation"
        //// to see if moves are played where moves would be cut.  KGS just lets you paste
        //// and just plays stones over other stones on the board, not sound but works.
        ////
        //// Also, if go back to this to account for moves that capture stones, then also
        //// need to account for moves not ready for rendering, which have row, col of -100, -100.
        //// Need to then start searching ParsedNodes.
        ////
        //internal static bool CheckForCoincidentMoves (GoBoard board, Move move) {
        //    while (move != null) {
        //        if (board.MoveAt(move.row, move.column) != null)
        //            return true;
        //        if (move.branches == null)
        //            move = move.next;
        //        else
        //            break;
        //    }
        //    // Only get here when move is null from whlie loop, or we're recursing on branches.
        //    if (move != null)
        //        foreach (var m in move.branches)
        //            if (CheckForCoincidentMoves(board, m))
        //                return true;
        //    return false;
        //}

        ////
        //// Computing Game Tree Layout
        ////

        //// The display is a grid of columns and rows, with the main game tree spine
        //// drawn across the first row, with branches descending from it. So, columns
        //// map to tree depth, and a column N, should have a move with number N,
        //// due to fake node added for board start in column zero.
        ////


        //// These properties represent/control the size of the canvas on which we draw
        //// move nodes and lines.  
        ////
        private static int _treeGridCols = 200;
        public static int TreeViewGridColumns {
            get { return _treeGridCols; }
            set { _treeGridCols = value; }
        }
        private static int _treeGridRows = 50;
        public static int TreeViewGridRows {
            get { return _treeGridRows; }
            set { _treeGridRows = value; }
        }

        //// GetGameTreeModel returns a matrix of node objects that represent moves in the game
        //// tree view, as well as where lines between moves need to bend, or where lines need
        //// to descend straight downward before angling to draw next move in a branch.
        ////
        public static TreeViewNode[,] GetGameTreeModel (Game game) {
            IMoveNext start = null;
            // Get start node
            if (game.FirstMove != null) {
                if (game.FirstMove.Rendered) {
                    // Mock a move for empty board state.
                    var m = new Move(-1, -1, GoBoardAux.NoColor);
                    m.Next = game.FirstMove;
                    m.Branches = game.Branches;
                    m.Rendered = false;
                    start = m;
                }
                else if (game.ParsedGame != null) {// This test should be same as g.pg.nodes != null.
                    start = game.ParsedGame.Nodes;
                }
            }
            // Get layout
            var layoutData = new TreeViewLayoutData();
            if (start != null)
                GameAux.LayoutGameTreeFromRoot(start, layoutData);
            else {
                // Or just have empty board ...
                layoutData.TreeGrid[0, 0] = GameAux.NewTreeModelStart(new Move(-1, -1, GoBoardAux.NoColor),
                                                                      layoutData);
            }
            return layoutData.TreeGrid;
        }

        //// LayoutGameTreeFromRoot takes a Move or ParsedNode and layout data (tree grid and max rows).
        //// It returns the model for the start (empty board) node, after laying out the rest of the
        //// tree.
        ////
        public static TreeViewNode LayoutGameTreeFromRoot (IMoveNext pn, TreeViewLayoutData layoutData) {
            // Vars to make arguments to calls below more readable.
            int tree_depth = 0;
            int new_branch_depth = 0;
            int branch_root_row = 0;
            // Setup initial node model.
            // Can't use 'var', must decl with 'TreeViewNode'.  C# doesn't realize all NewTreeModelStart
            // definitions return a TreeViewNode.
            TreeViewNode model = GameAux.NewTreeModelStart(pn, layoutData);
            // Return model, or get next model.
            TreeViewNode next_model;
            if (GetLayoutGameTreeNext(pn) == null) {
                // If no next, then no branches to check below
                layoutData.TreeGrid[model.Row, tree_depth] = model;
                return model;
            }
            else {
                next_model = GameAux.LayoutGameTree(GetLayoutGameTreeNext(pn), layoutData, model.Row,
                                                    tree_depth + 1, new_branch_depth, branch_root_row);
                model.Next = next_model;
            }
            // Store start model and layout any branches for first move.
            // Don't need to call StoreTreeViewNode since definitely do not need to grow model matrix.
            layoutData.TreeGrid[model.Row, tree_depth] = model;
            LayoutGameTreeBranches(pn, layoutData, tree_depth, model, next_model);
            return model;
        }

        private static TreeViewNode NewTreeModelStart (IMoveNext pn, TreeViewLayoutData layoutData) {
            var model = new TreeViewNode(TreeViewNodeKind.StartBoard, pn);
            model.Row = 0;
            layoutData.MaxRows[0] = 1;
            model.Column = 0;
            model.Color = GoBoardAux.NoColor; // sentinel color;
            return model;
        }

        //// LayoutGameTreeNext returns the next node in the top branch following the argument
        //// node.  If there are no branches, then return the Next property.  This function is
        //// necessary since Move nodes chain Next to the branch that is selected, but ParsedNodes
        //// always chain Next to Branches[0] if there are branches.  We always want the top branch.
        ////
        private static IMoveNext GetLayoutGameTreeNext (IMoveNext pn) {
            // Can't dynamically invoke Assert for some reason, and C# doesn't bind only matching
            // method at compile time.
            //Debug.Assert(pn.GetType() != typeof(Game));
            if (pn.IMNBranches != null)
                return pn.IMNBranches[0];
            else
                return pn.IMNNext;
        }

        //// layout recurses through the moves assigning them to a location in the display grid.
        //// max_rows is an array mapping the column number to the next free row that
        //// can hold a node.  cum_max_row is the max row used while descending a branch
        //// of the game tree, which we use to create branch lines that draw straight across,
        //// rather than zigging and zagging along the contour of previously placed nodes.
        //// tree_depth is just that, and branch_depth is the heigh to the closest root node of a
        //// branch, where its immediate siblings branch too.
        ////
        public static TreeViewNode LayoutGameTree (IMoveNext pn, TreeViewLayoutData layoutData,
                                                   int cum_max_row, int tree_depth, int branch_depth,
                                                   int branch_root_row) {
            // Check if done with rendered nodes and switch to parsed nodes.
            if (pn is Move && !((Move)pn).Rendered && ((Move)pn).ParsedNode != null)
                pn = ((Move)pn).ParsedNode;
            // Create and init model, set
            TreeViewNode model = GameAux.SetupTreeLayoutModel(pn, layoutData, cum_max_row, tree_depth);
            // Adjust last node and return, or get next model node.
            TreeViewNode next_model;
            if (GetLayoutGameTreeNext(pn) == null) {
                // If no next, then no branches to check below
                StoreTreeViewNode(layoutData, tree_depth, model);
                return GameAux.MaybeAddBendNode(layoutData, model.Row, tree_depth,
                                                branch_depth, branch_root_row, model);
            }
            else {
                next_model = GameAux.LayoutGameTree(GetLayoutGameTreeNext(pn), layoutData, model.Row, tree_depth + 1,
                                                    branch_depth == 0 ? 0 : branch_depth + 1, branch_root_row);
                //new_branch_depth, branch_root_row);
                model.Next = next_model;
            }
            // Adjust current model down if tail is lower, or up if can angle toward root now
            GameAux.AdjustTreeLayoutRow(model, layoutData, next_model.Row, tree_depth,
                                        branch_depth, branch_root_row);
            StoreTreeViewNode(layoutData, tree_depth, model);
            // bend is eq to model if there is no bend
            var bend = GameAux.MaybeAddBendNode(layoutData, model.Row, tree_depth,
                                                branch_depth, branch_root_row, model);
            // Layout branches if any
            LayoutGameTreeBranches(pn, layoutData, tree_depth, model, next_model);
            return bend;
        }

        private static void LayoutGameTreeBranches (IMoveNext pn, TreeViewLayoutData layoutData, int tree_depth,
                                                    TreeViewNode model, TreeViewNode next_model) {
            if (pn.IMNBranches != null) {
                model.Branches = new List<TreeViewNode>() { next_model };
                // Skip branches[0] since caller already did branch zero as pn's next move, but note, when
                // pn is a Move (that is, not a ParsedNode), then branches[0] may not equal pn.Next.
                for (var i = 1; i < pn.IMNBranches.Count; i++) {
                    // Can't use 'var', must decl with 'TreeViewNode'.  C# doesn't realize all LayoutGameTree
                    // definitions return a TreeViewNode.
                    TreeViewNode branch_model = GameAux.LayoutGameTree(pn.IMNBranches[i], layoutData, model.Row,
                                                                       tree_depth + 1, 1, model.Row);
                    model.Branches.Add(branch_model);
                }
            }
        }


        //// setup_layout_model initializes the current node model for the display, with row, column,
        //// color, etc.  This returns the new model element.
        ////
        private static TreeViewNode SetupTreeLayoutModel (IMoveNext pn, TreeViewLayoutData layoutData,
                                                          int cum_max_row, int tree_depth) {
            var model = new TreeViewNode(TreeViewNodeKind.Move, pn);
            // Get column's free row or use row from parent
            if (tree_depth >= GameAux.TreeViewGridColumns)
                GameAux.GrowTreeView(layoutData);
            var row = Math.Max(cum_max_row, layoutData.MaxRows[tree_depth]);
            model.Row = row;
            layoutData.MaxRows[tree_depth] = row + 1;
            model.Column = tree_depth;
            // Set color
            model.Color = pn.IMNColor;
            return model;
        }

        //// adjust_layout_row adjusts moves downward if moves farther out on the branch
        //// had to occupy lower rows.  This keeps branches drawn straighter, rather than
        //// zig-zagging with node contours.  Then this function checks to see if we're
        //// within the square defined by the current model and the branch root, and if we
        //// this is the case, then start subtracting one row at at time to get a diagonal
        //// line of moves up to the branch root.
        ////
        private static void AdjustTreeLayoutRow (TreeViewNode model, TreeViewLayoutData layoutData,
                                                 int next_row_used, int tree_depth, int branch_depth, int branch_root_row) {
            if (tree_depth >= GameAux.TreeViewGridColumns)
                GameAux.GrowTreeView(layoutData);
            // If we're on a branch, and it had to be moved down farther out to the right
            // in the layout, then move this node down to keep a straight line.
            if (next_row_used > model.Row) {
                model.Row = next_row_used;
                layoutData.MaxRows[tree_depth] = next_row_used + 1;
            }
            //// If we're unwinding back toward this node's branch root, and we're within a direct
            //// diagonal line from the root, start decreasing the row by one.
            if ((branch_depth < model.Row - branch_root_row) && (layoutData.TreeGrid[model.Row - 1, tree_depth] == null)) {
                // row - 1 does not index out of bounds since model.row would have to be zero,
                // and zero minus anything will not be greater than branch depth (which would be zero)
                // if row - 1 were less than zero.
                layoutData.MaxRows[tree_depth] = model.Row;
                model.Row = model.Row - 1;
            }
        }

        //// maybe_add_bend_node checks if the diagonal line of nodes for a branch intersect the column
        //// for the branch's root at a row great than the root's row.  If this happens, then we
        //// need a model node to represent where to draw the line bend to start the diagonal line.
        ////
        private static TreeViewNode MaybeAddBendNode (TreeViewLayoutData layoutData, int row,
                                                      int tree_depth, int branch_depth, int branch_root_row,
                                                      TreeViewNode curNode) {
            if ((branch_depth == 1) && (row - branch_root_row > 1) &&
                (layoutData.TreeGrid[row - 1, tree_depth - 1] == null)) {
                var bend = new TreeViewNode(TreeViewNodeKind.LineBend);
                bend.Row = row - 1;
                bend.Column = tree_depth - 1;
                bend.Next = curNode;
                layoutData.MaxRows[tree_depth - 1] = row;
                layoutData.TreeGrid[bend.Row, bend.Column] = bend;
                return bend;
            }
            return curNode;
        }

        private static void StoreTreeViewNode (TreeViewLayoutData layoutData, int tree_depth, TreeViewNode model) {
            if (model.Row >= GameAux.TreeViewGridRows || tree_depth >= GameAux.TreeViewGridColumns)
                GameAux.GrowTreeView(layoutData);
            MyDbg.Assert(layoutData.TreeGrid[model.Row, tree_depth] == null,
                         "Eh?!  This tree view location should be empty.");
            layoutData.TreeGrid[model.Row, tree_depth] = model;
        }

        private static void GrowTreeView (TreeViewLayoutData layoutData) {
            // Update globals for sizes
            GameAux.TreeViewGridColumns = GameAux.TreeViewGridColumns + (GameAux.TreeViewGridColumns / 2);
            GameAux.TreeViewGridRows = GameAux.TreeViewGridRows + (GameAux.TreeViewGridRows / 2);
            // Grow tree grid
            var oldGrid = layoutData.TreeGrid;
            var oldGridRows = oldGrid.GetLength(0);
            var oldGridCols = oldGrid.GetLength(1);
            layoutData.TreeGrid = new TreeViewNode[GameAux.TreeViewGridRows, GameAux.TreeViewGridColumns];
            for (var i = 0; i < oldGridRows; i++) {
                for (var j = 0; j < oldGridCols; j++) {
                    layoutData.TreeGrid[i, j] = oldGrid[i, j];
                }
            }
            // Grow Maxes
            var oldMaxes = layoutData.MaxRows;
            layoutData.MaxRows = new int[GameAux.TreeViewGridColumns];
            for (var i = 0; i < oldMaxes.Length; i++)
                layoutData.MaxRows[i] = oldMaxes[i];
        }

    } // GameAux


    public class TreeViewNode {
        public TreeViewNodeKind Kind { get; set; }
        public UIElement Cookie { get; set; }
        public Color Color { get; set; }
        public IMoveNext Node { get; set; } // public ParsedNode Node {get; set;}
        // Row has nothing to do with node's coordinates. It is about where this node appears
        // in the grid displaying the entire game tree.
        public int Row { get; set; }
        public int Column { get; set; }
        public TreeViewNode Next { get; set; }
        public List<TreeViewNode> Branches { get; set; }

        public TreeViewNode (TreeViewNodeKind kind = TreeViewNodeKind.Move, IMoveNext node = null) {
            this.Kind = kind;
            this.Cookie = null;
            this.Node = node;
            this.Row = 0;
            this.Column = 0;
            this.Color = GoBoardAux.NoColor;
        }
    } // TreeViewNode

    public enum TreeViewNodeKind {
        Move, LineBend, StartBoard
    }


    //// TreeViewLayoutData holds info we need to indirect to and change the size of sometimes while
    //// laying out game tree views.  This would be private to the compilation unit, but that's not possible.
    //// Also, since LayoutGameTree is partially thought of as platform that might be used by extensions,
    //// and it is public, this needs to be.
    ////
    public class TreeViewLayoutData {
        public TreeViewNode[,] TreeGrid { get; set; }
        public int[] MaxRows { get; set; }

        public TreeViewLayoutData () {
            this.TreeGrid = new TreeViewNode[GameAux.TreeViewGridRows, GameAux.TreeViewGridColumns];
            this.MaxRows = new int[GameAux.TreeViewGridColumns];
        }
    }

} // namespace
