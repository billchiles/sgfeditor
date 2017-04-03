
**Project Description**
SGF Editor reads and writes .sgf files, edits game trees, etc.  It has several useful commands for reviewing games, including saving in reverse view to give a copy to your opponent.  You can also just use it as a Go board to play a game.  (For search purposes: goban, baduk, weiqi.)

_The project has a Python version (IronPython/WPF required for UI), C#/WPF desktop version, and Windows 8 Windows App Store version.  The Python version is the farthest behind (see sgfpy\notes.txt), and the WPF version is behind by a couple of features, also listed in notes.txt).  I use PTVS to hack on the IPy version.  I use Visual Studio for the C#/WPF and C#/WinRT versions._

The Windows 8 app store page is [here](http://apps.microsoft.com/windows/app/sgfeditor/4770d48e-0179-4ada-a7ed-8382c85d949a).

Here's an image of the win8 app followed by an image of the WPF version (Surface screen res seems too high for uploading screen capture, but if you zoom your browser window, it looks better :-)):

![](Home_screenshotwin8.png)


and WPF version ...
![](Home_capture.png)


**history/status/caveat:**
* I started this as an IronPython project to play with WPF and the tooling that later became PTVS.  I then ported to C# for fun and to get ready to convert the WPF UI to Windows 8 WinRT UI.  
* The C# is mostly fine IMO, BUT
0. It still reflects some python-isms (for example, comments refer to python_style_names, there is inadvertent mixed naming style, big static classes for stateless module scoped helper functions, few uses of 'dynamic' instead of interfaces/generics/overloads) and 
0. While I have had one serious C# expert positively review architecture and code, the code style is not the Microsoft C# sample style.  I like K&R style and more comments.


**From the Help command ...**

PLACING STONES AND ANNOTATIONS:
Click on board location to place alternating colored stones.
Shift click to place square annotations, ctrl click for triangles, and
alt click to place letter annotations.  If you click on an adornment location
twice (for example shift-click twice in one spot), the second click removes
the adornment.

If the last move is a misclick (at the end of a branch), click the last move again to undo it.

KEEPING FOCUS ON BOARD FOR KEY BINDINGS
Escape will always return focus to the board so that the arrow keys work
and are not swallowed by the comment editing pane.  Sometimes opening and
saving files leaves WPF in a weird state such that you must click somewhere
to fix focus.

NAVIGATING MOVES IN GAME TREE
Right arrow moves to the next move, left moves to the previous, up arrow selects
another branch or the main branch, down arrow selects another branch, home moves
to the game start, and end moves to the end of the game following the currently
selected branches.  You can always click a node in the game tree graph.

*win8 version*: If the current move has branches following it, the selected branch's first node has a
light grey square outlining it. Nodes that have comments have a light green
highlight, and the current node has a light blue highlight.


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
a branch up, you must be on the first move of a branch, and then you can use
ctrl-uparrow, and to move a branch down, use ctrl-downarrow.

PASSING
The Pass button or c-p will make a pass move.

F1 produces this help.

There is an auto save feature if Windows suspends the app and also every 30s.
Opening a file that has a newer auto save file prompts for which to open.  Launching
the app checks for an unnamed auto save file less than 12 hours old.

---------------

sgfedwin8\sgfedwin8.sln is the VS 2012 solution file for the Windows App Store version.
   This is the most up to date, actively developed version.
   sgfedwin8\zzzz-notes.txt has some specific notes to win8 port and issues.
   I periodically port features to the WPF version, but the python version is pretty out of date.
sgfed.sln is the VS 2010 version solution file for the WPF/desktop (sgfed) and Python (sgfpy) versions.
sgfed2.sln is the VS 2012 version solution file for the WPF/desktop (sgfed) and Python (sgfpy) versions.

See project page on codeplex (sgfeditor.codeplex.com) for program help text.
It is also a big string literal at the top of sgfed\mainwindow.xaml.cs.



ARCHITECTURE ...

General notes:
  * There is a bit of tension between some things being public vs. internal due to thinking
    of some of the types as being re-usable a la a platform or perhaps for extensions.
  * I have chosen to use static classes with "stateless" helpers where in python the natural
    style would be to use a global helper instead of an instance method.
  * The static classes are public if some helpers are used across files since they then feel
    more like a general platform helper, and members are marked 'internal' if they are meant
    only for the current file or class they extend.  Yes, 'internal' in C# is program-wide
    access, so all members could be used anywhere, but 'public' marks those meant to be used
    anywhere.

Types:
    MainWindow
       Refers to helpers in MainWindowAux.
       Refers to Game, GameAux.
       Refers to Move.
    MainWindowAux
       Refers to Move, GoBoard.
       Refers to Game, GameAux.
    Game
       Refers to MainWindow to control UI.
       Refers to GoBoard, GoBoardAux, Move to update model.
       Refers to ParsedGame to update model as advanced through parsed game tree.
       Refers to Adornments to update view model.
    GameAux
       Refers to GoBoard, GoBoardAux, Move to update model.
       Refers to ParsedGame, ParsedNode to update model as advanced through parsed game tree
          and to generated a parsed representation for printing.
       Refers to Adornments to update view model.
    GoBoard
       Refers to Move.
    GoBoardAux
       Pure model.
    Move
       Refers to GoBoardAux for NoIndex constant.
    Adornments
       Refers to Move to hold model refs.
    ParsedGame
       Refers to ParsedNode
    ParsedNode
       Pure model.
    ParserAux
       Refers to Lexer.
       Refers to ParsedGame, ParsedNode



PORTING FROM WIN8 TO WPF ...

wpf divergence from win8
 * meh
   * did not port auto saving to wpf
   * do not do file activation association on wpf
   * reworked command panel, needs menus then in lieu of app bar,
        some cmds kbd only anyway in wpf (cut, paste, move branch, etc.)
 * do not have scrolling help dialog
 * ??? do not have esc and enter as shortcuts for new game / info dialogs
 * ??? put focus in dialogs so user can just type (6bdc0dfb5581, 13 APR 13)
 * ??? generalized AB / HA handling (tolerate no HA, was setting next color wrong, 2ba0ce365a66 30 JUN 13)
 * ??? bug fix for cutting a move at start of a branch (subtle, hard to repro in normal usage, 4e79ecf160c3)


79df691220d6
 by billchiles
 Dec 14, 2014 12:21 PM  

    changed title info display to be two lines for better separation of info, bigger font, better filename display, etc.
 

commit 20ec0b41e7d7f3c4deb1fbe5a7209fe13cc215bf
Author: billchiles <bill-chiles@hotmail.com>
Date:   Wed Dec 3 08:41:47 2014 -0800

    fixed bug if user deletes file from disk while editing it, then the storage object is no longer good to write the same file, so must prompt with save-as to reacquire user intention and rights to write the file.  Made mainwin.saveas public and writegame handle file write failure.

commit 2141e1cb3da77976b480d4600b1fcaf54635e0c3
Author: billchiles <bill-chiles@hotmail.com>
Date:   Wed Oct 15 18:03:35 2014 -0700

    fixed kbd input to check whether comment box was tabbed to or clicked on to ensure root kbd handling does not do work when comment box should have swallowed key, and added c-t feature to convert the current moves comment such that if it refers to itself with display coordinates, then we convert that referenced to 'this'

commit 0bf4457ea03e845f9f79f372aaa1cc92bf2d7d66
Author: billchiles <bill-chiles@hotmail.com>
Date:   Fri Aug 22 10:51:25 2014 -0700

    added functionality to UpdateCurrentComment to stash previous comment on clipboard just in case, has proven useful already due to extra quick editing commands.

commit 30de951748c7cb8bcc95bbf50c08f07cee4db268
Author: billchiles <bill-chiles@hotmail.com>
Date:   Sat Jun 7 11:31:59 2014 -0700

    added a couple of pet commands for frequent editing operations, c-s-? and c-1..5.  Cleaned up some comments.

commit 4c2e02b0268d021088151283e195e2a35f973a0f
Author: billchiles <bill-chiles@hotmail.com>
Date:   Sun Dec 22 19:32:37 2013 -0800

    changed current node in tree to have fuscia background since the lightskyblue was just not easy enough to see.

commit 7345087aaf1b8463c9681124ec3b370ba017fa0a
Author: billchiles <bill-chiles@hotmail.com>
Date:   Thu Oct 17 09:54:29 2013 -0700

    ensured dismissing game info puts focus back on go board, not txt box, and added c-i binding to pop game info.  Commented out old key binding used for testing.

commit 84404cfc2db657457780a43ca520e78b03ee8ada
Author: billchiles <bill-chiles@hotmail.com>
Date:   Tue Oct 1 12:35:54 2013 -0700

    fixed bug in CutNextMove for case where previous move has parsed node and a next move, but the cut move was created by user (that is, has no parsed node).

081d1cf05779
 by billchiles 
 Sep 7, 2013 3:48 PM  
 
    Added c-k to clear commentbox


 
ported to wpf ...
 * pass move support
 * text in game info dialog explaining how to set handicap
 * handling AW property
 * added box around current node to make it more visible
 * c-i to get game info
 * fixed bug writing flipped view file that left flipped coordinates in unrendered parsed nodes
 * new game dialog input checks
 * esc saving comment and updating title if dirty
 * canonicalized newline sequences in comments 
 * tolerating HA[0]
 * tree drawing tweaks for constants on node size
 * simplified UpdateTitle code
 * handicap stone placement tweak (broke when added code review updates)
 * tap last move to undo misclick
 * fixed home key and tapping start node to restore empty board adornments
 * green highlights on game tree nodes with comments
 * game metadata editing dialog
 * finished cleaning up UpdateTitle so no calls pass args
 * enhanced highlighting of current node in tree view
 * fixed bug in writing flipped view files
 * AW handling
 * more general AB handling
 * game info dialog mentions new game to change handicap
 * [caught up to 07 JUL 13, modulo things only in win8 due to async]




----------------------------------------------------------------------------

See the sgfpy\notes.txt file for informal to-do list, and other older notes.


