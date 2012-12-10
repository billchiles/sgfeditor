### goboard.py provides the board and move models.  The main classes are
### GoBoard, Move, and Adornments.

import clr

## Debugging only ...
## Needed for System.Windows
#clr.AddReference('PresentationFramework')
#from System.Windows import MessageBox

__all__ = ["GoBoard", "Move", "Adornments", "parsed_move_model_coordinates", 
           "parsed_label_model_coordinates", "parsed_to_model_coordinates"]


### GoBoard models the board in terms of what stones are where on the
### board and has a list/tree of Moves.  The board model is that rows
### increase going down from the top, and columns increase to the right.
###
class GoBoard (object):
    
    supported_sizes = [9, 13, 19]
    
    def __init__ (self, size):
        ## size holds the number of lines on the board.
        if size not in GoBoard.supported_sizes:
            raise Exception("Board size must be 9, 13, or 19 -- got " + str(size))
        self.size = size
        ## moves holds Move objects or None if there's no stone at the location
        self.moves = [[None for col in xrange(size)] for row in xrange(size)]

    ### add_stone adds move to the model, assuming it has valid indexes.
    ### row, col are one-based, as we talk about go boards.
    ###
    def add_stone (self, move):
        if self.moves[move.row - 1][move.column - 1] is not None:
            raise Exception("Ensure board has no stone at location.")
        self.moves[move.row - 1][move.column - 1] = move
        return move
    
    ### remove_stone removes the move indicated by move's row and column.
    ### There is no identity check that the argument is in the model.  Row, col
    ### are one-based, and this assumes they are valid indexes.
    ###
    def remove_stone (self, move):
        self.moves[move.row - 1][move.column - 1] = None
    
    def remove_stone_at (self, row, col):
        if self.move_at(row, col):
            self.remove_stone(self.move_at(row, col))
    
    ### goto_start removes all stones from the model.
    ###
    def goto_start (self):
        for row in xrange(self.size):
            for col in xrange(self.size):
                self.moves[row][col] = None
    
    ### move_at returns the move at row, col (one-based indexes), or None if
    ### there is no move here.
    ###
    def move_at (self, row, col):
        return self.moves[row - 1][col - 1]
    
    ### color_at returns the color of the stone at row, col (one-based), or None if there is not stone there.
    ### This assumes row, col are valid indexes.
    ###
    def color_at (self, row, col):
        m = self.moves[row - 1][col - 1]
        if m:
            return m.color
        else:
            return None


    ### has_stone returns true if the go board location row,col (one-based) has
    ### a stone.  This function assumes row and col are valid locations.
    ###
    def has_stone (self, row, col):
        return self.move_at(row, col) is not None


    ### The following functions return whether the specified location has a
    ### stone or a stone of a particular color at the specified location.  The
    ### row, col are one-based, and if an index is invalid, the functions
    ### return false so that the edges of the board never have a stone
    ### essentially.

    def has_stone_left (self, row, col):
        return ((col - 1) >= 1) and (self.move_at(row, col - 1) is not None)
    
    def has_stone_color_left (self, row, col, color):
        return self.has_stone_left(row, col) and (self.move_at(row, col - 1).color == color)
    
    
    def has_stone_right (self, row, col):
        return ((col + 1) <= self.size) and (self.move_at(row, col + 1) is not None)
    
    def has_stone_color_right (self, row, col, color):
        return self.has_stone_right(row, col) and (self.move_at(row, col + 1).color == color)
    
    
    def has_stone_up (self, row, col):
        return ((row - 1) >= 1) and (self.move_at(row - 1, col) is not None)
    
    def has_stone_color_up (self, row, col, color):
        return self.has_stone_up(row, col) and (self.move_at(row - 1, col).color == color)
    
    
    def has_stone_down (self, row, col):
        return ((row + 1) <= self.size) and (self.move_at(row + 1, col) is not None)
    
    def has_stone_color_down (self, row, col, color):
        return self.has_stone_down(row, col) and (self.move_at(row + 1, col).color == color)
    


### Move models a move or stone on the board and links to the previous
### and next moves.
###
class Move (object):
    
    def __init__ (self, row, column, color):
        ## Row and column are None when this is a pass move.
        self.row = row
        self.column = column
        self.is_pass = row is None and column is None
        self.color = color
        ## The following need to be set if placing move into a game
        self.next = None
        self.previous = None
        self.number = 0
        self.dead_stones = []
        ## Adornments is for editing markup.
        self.adornments = []
        ## Comments holds text describing the move or current game state.
        self.comments = ""
        ## Branches holds all the next moves, while next points to one of these.
        ## This is None until there's more than one next move.
        self.branches = None
        ## parsed_node is a node from a .sgf file.
        self.parsed_node = None
        ## rendered by default is true assuming moves are made and immediately
        ## displayed, but parsed nodes can have unprocessed branches or
        ## annotations.
        self.rendered = True
    
    
    ### The adornment functions do no checking on the objects added, whether
    ### objects are really in the collection for removing, etc.  It's up to
    ### users to use them correctly.
    
    def add_adornment (self, a):
        self.adornments.append(a)
        return a
    
    def remove_adornment (self, a):
        self.adornments.Remove(a)

###
### Coordinates conversions
###

### letters used for translating parsed coordinates to get model coordinates.
###
_letters = ["skip_0_goboard_one_based", 
            "a", "b", "c", "d", "e", "f", "g", "h", "i", "j",
            "k", "l", "m", "n", "o", "p", "q", "r", "s"]

### get_parsed_coordinates returns letter-based coordinates from _letters for
### writing .sgf files.  If flipped, then return the diagonal mirror image
### coordinates for writing files in the opponent's view of the board.
###
def get_parsed_coordinates (move_or_adornment, flipped):
    if type(move_or_adornment) is Move and move_or_adornment.is_pass:
        return ""
    if flipped:
        return (_letters[20 - move_or_adornment.column] +
                _letters[20 - move_or_adornment.row])
    else:
        return (_letters[move_or_adornment.column] +
                _letters[move_or_adornment.row])

def flip_parsed_coordinates (coords):
    coords = parsed_to_model_coordinates(coords)
    if None in coords:
        return ""
    else:
        return _letters[20 - coords[1]] + _letters[20 - coords[0]]
    


### parsed_label_model_coordinates takes a parsed properties and returns as
### multiple values the row, col (in terms of the model used by goboard.py),
### and the label letter.  Data should be "<letter><letter>:<letter>".
###
def parsed_label_model_coordinates (data):
    return parsed_to_model_coordinates(data) + (data[3],)
    
### parsed_to_model_coordinates takes a parsed coordinates string and returns
### as multiple values the row, col in terms of the model used by goboard.py.
### This assumes coords is "<letter><letter>" and valid indexes.
###
def parsed_to_model_coordinates (coords):
    if coords == "":
        ## Pass move
        return None, None
    else:
        return (_letters.index(coords[1]), _letters.index(coords[0]))

###
### Adornments
###

### Adornments is probably misnamed, but instances represent adornments to
### locations on the go board, such as triangles, squares, letters, etc., that
### let comments identify moves by moves and locations by more than
### coordinates.  The class object has "static" members to represent the
### constant kinds of adornments.  There are some helpes to manage the unique
### current move adornment.
###
class Adornments (object):
    
    ### __init__ takes a kind, which is a unique instance of Adornments set
    ### below after the class.  It also takes the go board location
    ### (one-based).  If move is supplied, then the new adornment is on a move
    ### that is on the board at row, col.
    ###
    def __init__ (self, kind, row, col, move = None, letter = None):
        ## Kind is static member of class set below.
        self.kind = kind
        ## Letter is string when kind is letter.
        self.letter = letter
        self.row = row
        self.column = col
        self.move = move
        ## Cookie is for the UI layer to hold onto objects per adornment.
        self.cookie = None

    ## There's only one current move adornment.
    current_move_adornment = None

    @staticmethod
    def get_current_move (move, cookie):
        if Adornments.current_move_adornment.move is not None:
            raise Exception("Already have current move adornment at row, col: " +
                            str(Adornments.current_move_adornment.move.row) + ", " +
                            str(Adornments.current_move_adornment.move.column) + ".")
        Adornments.current_move_adornment.move = move
        Adornments.current_move_adornment.cookie = cookie
        move.add_adornment(Adornments.current_move_adornment)
    
    @staticmethod
    def release_current_move ():
        move = Adornments.current_move_adornment.move
        if move is None:
            raise Exception("Do not have current move adornment.")
        move.adornments.remove(Adornments.current_move_adornment)        
        Adornments.current_move_adornment.move = None
        Adornments.current_move_adornment.cookie = None

### Kinds of adornments ...
Adornments.triangle = object()
Adornments.square = object()
Adornments.letter = object()
Adornments.current_move = object()
### The unique current move adornment
Adornments.current_move_adornment = Adornments(Adornments.current_move, 1, 1)


