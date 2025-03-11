//// goboard.cs provides the board and move models.  The main classes are
//// GoBoard, Move, and Adornments.

using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml; // UIElement
// System.Windows: UIElement, MessageBox
using System.Windows;
//using System.Linq;
//using System.Text;
using Microsoft.UI; // Color, Colors
using Color = Windows.UI.Color;
//using System.Windows.Media; // Color, Colors
using System.Diagnostics; // Debug.Assert


namespace SgfEdwin10 {

    public class GoBoard {
        public int Size { get; set; }
        private Move[,] moves = null;

        public GoBoard (int size) {
            this.Size = size;
            this.moves = new Move[size, size];
        }


        //// add_stone adds move to the model, assuming it has valid indexes.
        //// row, col are one-based, as we talk about go boards.
        ////
        public Move AddStone (Move move) {
            // This debug.assert could arguably be a throw if we think of this function
            // as platform/library.
            MyDbg.Assert(this.moves[move.Row - 1, move.Column - 1] == null,
                         "Ensure board has no stone at location.");
            this.moves[move.Row - 1, move.Column - 1] = move;
            return move;
        }

        //// remove_stone removes the move indicated by move's row and column.
        //// There is no identity check that the argument is in the model.  Row, col
        //// are one-based, and this assumes they are valid indexes.
        ////
        public void RemoveStone (Move move) {
            this.moves[move.Row - 1, move.Column - 1] = null;
        }

        public void RemoveStoneAt (int row, int col) {
            if (this.MoveAt(row, col) != null)
                this.RemoveStone(this.MoveAt(row, col));
        }

        //// goto_start removes all stones from the model.
        ////
        public void GotoStart () {
            for (var row = 0; row < this.Size; row++)
                for (var col = 0; col < this.Size; col++)
                    this.moves[row, col] = null;
        }

        //// move_at returns the move at row, col (one-based indexes), or None if
        //// there is no move here.
        ////
        public Move MoveAt (int row, int col) {
            return this.moves[row - 1, col - 1];
        }

        //// BoardModelAsSTring returns a string to lightly display board state for debugging purposes.
        ////
        public string BoardModelAsString () {
            var res = "";
            for (var row = 0; row < this.Size; row++) {
                for (var col = 0; col < this.Size; col++)
                    if (this.moves[row, col] != null) {
                        if (this.moves[row, col].Color == Colors.White) {
                            res = res + "O";
                        }
                        else if (this.moves[row, col].Color == Colors.Black) {
                            res = res + "X";
                        }
                        else
                            res = res + "?";
                    }
                    else
                        res = res + "+";
                res = res + "\n";
            }
            return res;
        }

        //// color_at returns the color of the stone at row, col (one-based), or None if there is not stone there.
        //// This assumes row, col are valid indexes.
        ////
        public Color ColorAt (int row, int col) {
            var m = this.moves[row - 1, col - 1];
            if (m != null)
                return m.Color;
            else
                return GoBoardAux.NoColor;
        }


        //// has_stone returns true if the go board location row,col (one-based) has
        //// a stone.  This function assumes row and col are valid locations.
        ////
        public bool HasStone (int row, int col) {
            return this.MoveAt(row, col) != null;
        }

        //// The following functions return whether the specified location has a
        //// stone or a stone of a particular color at the specified location.  The
        //// row, col are one-based, and if an index is invalid, the functions
        //// return false so that the edges of the board never have a stone
        //// essentially.

        public bool HasStoneLeft (int row, int col) {
            return ((col - 1) >= 1) && (this.MoveAt(row, col - 1) != null);
        }

        public bool HasStoneColorLeft (int row, int col, Color color) {
            return this.HasStoneLeft(row, col) && (this.MoveAt(row, col - 1).Color == color);
        }


        public bool HasStoneRight (int row, int col) {
            return ((col + 1) <= this.Size) && (this.MoveAt(row, col + 1) != null);
        }

        public bool HasStoneColorRight (int row, int col, Color color) {
            return this.HasStoneRight(row, col) && (this.MoveAt(row, col + 1).Color == color);
        }


        public bool HasStoneUp (int row, int col) {
            return ((row - 1) >= 1) && (this.MoveAt(row - 1, col) != null);
        }

        public bool HasStoneColorUp (int row, int col, Color color) {
            return this.HasStoneUp(row, col) && (this.MoveAt(row - 1, col).Color == color);
        }


        public bool HasStoneDown (int row, int col) {
            return ((row + 1) <= this.Size) && (this.MoveAt(row + 1, col) != null);
        }

        public bool HasStoneColorDown (int row, int col, Color color) {
            return this.HasStoneDown(row, col) && (this.MoveAt(row + 1, col).Color == color);
        }

    } // GoBoard class



    //// GoBoardAux provides stateless helper functions for GoBoard.  Public members are used
    //// outside GoBoard, while internal members are only used within GoBoard.
    ////
    public static class GoBoardAux {

        //// NoColor is necessary since Color vars cannot be null;
        ////
        internal static Color NoColor = default(Color);

        //// NoIndex represents coordinates for pass moves.
        ////
        internal static int NoIndex = -100;

        ////
        //// Coordinates Conversions
        ////

        //// Letters used for translating parsed coordinates to model coordinates.
        //// The first element is bogus because the model is 1 based to match user model.
        ////
        private static char[] letters = {((char)0), 
                                         'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j',
                                         'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's'};

        //// get_parsed_coordinates returns letter-based coordinates from _letters for
        //// writing .sgf files.  If flipped, then return the diagonal mirror image
        //// coordinates for writing files in the opponent's view of the board.
        ////
        //// SGF format is col,row (count from left edge, count from top).
        ////
        //// UWP runtime completely screws over dynamic, and while this function didn't take 4-6s
        //// like some, it just seems prudent to complicate the code with two definitions to guard against
        //// more lossage later (like they make dynamic actually not work).  And sure enough, by 2018,
        //// they screwed the pooch so bad on dynamic by throwing away the inline caching and interpreting
        //// all dynamic code such that function calls took several seconds instead of micro-seconds.
        ////
        public static string GetParsedCoordinates (Move move_or_adornment, bool flipped, int size) {
            int row = move_or_adornment.Row;
            int col = move_or_adornment.Column;
            if (move_or_adornment.IsPass)
                return "";
            if (flipped)
                // C# fails mutually distinct types on char and int, and string constructor lame.
                // Must cons array so that string constructor doesn't interpret second char as count.
                return new string(new char[] {letters[size + 1 - col], letters[size + 1 - row]});
            else
                return new string(new char[] {letters[col], letters[row]});
        }
        public static string GetParsedCoordinatesA (Adornments move_or_adornment, bool flipped, int size) {
            int row = move_or_adornment.Row;
            int col = move_or_adornment.Column;
            if (flipped)
                // C# fails mutually distinct types on char and int, and string constructor lame.
                // Must cons array so that string constructor doesn't interpret second char as count.
                return new string(new char[] { letters[size + 1 - col], letters[size + 1 - row] });
            else
                return new string(new char[] { letters[col], letters[row] });
        }

        //// FlipParsedCoordinates is only called from GameAux.FlipCoordinates, which is only used for
        //// writing files.  Hence, we represent pass coordinates as the empty string.  Apparently, some
        //// programs use "tt" due to older format conventions.
        ////
        public static string FlipParsedCoordinates(string coords, int size) {
            var mcoords = GoBoardAux.ParsedToModelCoordinates(coords);
            if (mcoords.Item1 == GoBoardAux.NoIndex)
                return "";
            else
                // C# fails mutually distinct types on char and int, and string constructor lame.
                // Must cons array so that string constructor doesn't interpret second char as count.
                return new string(new char[] { letters[size + 1 - mcoords.Item2], letters[size + 1 - mcoords.Item1] });
        }
    

        //// parsed_label_model_coordinates takes a parsed properties and returns as
        //// multiple values the row, col (in terms of the model used by goboard.py),
        //// and the label letter.  Data should be "<letter><letter>:<letter>".
        ////
        public static Tuple<int, int, char> ParsedLabelModelCoordinates (string data) {
            var tmp = GoBoardAux.ParsedToModelCoordinates(data);
            return new Tuple<int, int, char>(tmp.Item1, tmp.Item2, data[3]);
        }

        //// parsed_to_model_coordinates takes a parsed coordinates string and returns
        //// as multiple values the row, col in terms of the model used by goboard.py.
        //// This assumes coords is "<letter><letter>" and valid indexes.
        ////
        //// SGF format is col,row (count from left edge, count from top).
        ////
        public static Tuple<int, int> ParsedToModelCoordinates (string coords) {
            if (coords == "")
                // Pass move
                return new Tuple<int, int>(GoBoardAux.NoIndex, GoBoardAux.NoIndex);
            else {
                coords = coords.ToLower();
                return new Tuple<int, int>(Array.IndexOf(GoBoardAux.letters, coords[1]),
                                           Array.IndexOf(GoBoardAux.letters, coords[0]));
            }
        }

        public static char ModelCoordinateToDisplayLetter (int i) {
            var chr_offset = (i < 9) ? i : (i + 1);
            var chr_txt = (char)(chr_offset + (int)('A') - 1);
            return chr_txt;
        }

        public static int DisplayLetterToModelCoordinate (char c) {
            c = char.ToLower(c);
            var i = c - 'a';
            if (i >= 9)
                return i;
            else
                return i + 1;
        }

    } // GoBoardAux class



    //// IMoveNext exists to abstract property access across Move and ParsedNodes for computing
    //// the layout of the visual tree of game moves.  We need this first class named type because
    //// UWP has made C# dynamic so slow it is useless (computing a tree layout went from milliseconds
    //// to taking 5s (debug on .net core) and 8s (release on .net native).
    ////
    //// These cannot be implicitly implemented and have distinct names because 1) collections cannot
    //// implicitly convert to collections of supertypes, 2) Next needs to return a Move when we need
    //// all the move functionality, and 3) Color doesn't exist on ParsedNode naturally.
    ////
    public interface IMoveNext {
        Color IMNColor { get; }
        IMoveNext IMNNext { get; }
        List<IMoveNext> IMNBranches { get; }
    }


    //// Move represents moves that have been rendered on the go board, or moves created for next moves
    //// of a current move, so that the current move can point to Moves.  These next moves are marked
    //// Rendered==false.
    ////
    public class Move : IMoveNext {
        public int Row { get; set; }  // These count from top to bottom, 1 based.
        public int Column { get; set; }  // These count from left to right, 1 based.
        public Color Color { get; set; }
        public Move Previous { get; set; }
        public Move Next { get; set; }
        public int Number { get; set; }
        public bool IsPass { get; set; }  // True when row, col are both GoBoardAux.NoIndex
        // DeadStones is always a list.
        public List<Move> DeadStones { get; set; }
        public List<Adornments> Adornments { get; set; }
        public string Comments { get; set; }
        // Branches is null when there is zero or one next move.
        public List<Move> Branches { get; set; }
        public ParsedNode ParsedNode { get; set; }
        public bool Rendered { get; set; }

        public Move(int row, int col, Color color) {
            this.Row = row;
            this.Column = col;
            this.IsPass = (row == GoBoardAux.NoIndex) && (col == GoBoardAux.NoIndex);
            this.Color = color;
            // The following need to be set if placing move into a game
            this.Next = null;
            this.Previous = null;
            this.Number = 0;
            this.DeadStones = new List<Move>();
            // Adornments is for editing markup.
            this.Adornments = new List<Adornments>();
            // Comments holds text describing the move or current game state.
            this.Comments = "";
            // Branches holds all the next moves, while next points to one of these.
            // This is None until there's more than one next move.
            this.Branches = null;
            // parsed_node is a node from a .sgf file.  This holds properties we may ignore, but
            // we include them when generating a file/persistence representation of moves, merging
            // with an state that's changed from user edits.
            this.ParsedNode = null;
            // rendered by default is true assuming moves are made and immediately
            // displayed, but parsed nodes can have unprocessed branches or
            // annotations.
            this.Rendered = true;
        }

        /// Thes IMN... member need to exist because C# dynamic is unusable in UWP (see IMoveNext type).
        /// 
        public IMoveNext IMNNext { get { return this.Next; } }
        public List<IMoveNext> IMNBranches { get { return (this.Branches == null) ? null : new List<IMoveNext>(this.Branches); } }
        public Color IMNColor { get { return this.Color; } }


        public Adornments AddAdornment (Adornments a) {
            this.Adornments.Add(a);
            return a;
        }

        public void RemoveAdornment(Adornments a) {
            this.Adornments.Remove(a);
        }

    } // Move class



    public enum AdornmentKind {
        Triangle, Square, Letter, CurrentMove
    }



    //// Adornments models adornments on stones or the board.  It contains a cookie to point
    //// to a UIElement to aid in removing or managing the UI manifestation of the adornment.
    ////
    public class Adornments {
        public AdornmentKind Kind { get; set; }
        public int Row { get; set; }
        public int Column { get; set; }
        public Move Move { get; set; }
        // Letter is string when kind is letter.
        public string Letter { get; set; }
        // Cookie is for the UI layer to hold onto objects per adornment.
        public UIElement Cookie { get; set; }

        public Adornments (AdornmentKind kind, int row, int col, Move move = null, string letter = null) {
            this.Kind = kind;
            this.Letter = letter;
            this.Row = row;
            this.Column = col;
            this.Move = move;
            this.Cookie = null;
        }

        //// There's only one current move adornment.
        ////
        private static Adornments _currentAdornment = new Adornments(AdornmentKind.CurrentMove, 1, 1);
        public static Adornments CurrentMoveAdornment {
            get { return _currentAdornment; }
        }

        //// GetCurrentMove reserves or locks the current move adornment.
        ////
        public static void GetCurrentMove (Move move, UIElement cookie) {
            // This debug.assert could arguably be a throw if we think of this function
            // as platform/library.
            MyDbg.Assert(Adornments.CurrentMoveAdornment.Move == null,
                         "Already have current move adornment at row, col: "); // +
                //Adornments.CurrentMoveAdornment.Move.Row.ToString() + ", " +
                //Adornments.CurrentMoveAdornment.Move.Column.ToString() + ".");
            Adornments.CurrentMoveAdornment.Move = move;
            Adornments.CurrentMoveAdornment.Cookie = cookie;
            move.AddAdornment(Adornments.CurrentMoveAdornment);
        }

        //// ReleaseCurrentMove releases the current move adornment so that it can be
        //// acquired to apply to another move.
        ////
        public static void ReleaseCurrentMove() {
            var move = Adornments.CurrentMoveAdornment.Move;
            // This debug.assert could arguably be a throw if we think of this function
            // as platform/library.
            MyDbg.Assert(move != null, "Do not have current move adornment.");
            move.Adornments.Remove(Adornments.CurrentMoveAdornment);
            Adornments.CurrentMoveAdornment.Move = null;
            Adornments.CurrentMoveAdornment.Cookie = null;
        }

    } // Adornments class

} // namespace
