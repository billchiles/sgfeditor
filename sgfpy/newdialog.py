### newdialog.py is just that, the New Game dialog to get basic game info for
### the New button.
###

import wpf
import clr

### Don't need this now due to new wpf module, but left as documentation of
### usage.
###
### Needed for System.Windows
#clr.AddReference('PresentationFramework')

from System.Windows import Window #, MessageBox


__all__ = ["NewGameDialog"]



class NewGameDialog(Window):
    def __init__(self):
        ## LoadComponent reads xaml to fill in Window contents, creates
        ## NewGameDialog members for named elements in the xaml, and hooks up
        ## event handling methods.
        wpf.LoadComponent(self, "NewGameDialog.xaml")
        self.sizeText.Text = "19"
        ## For now we only handle 19x19 games
        self.sizeText.IsReadOnly = True
        self.handicapText.Text = "0"
        self.komiText.Text = "0.5"

    ### stones_mouse_left_down handles stonesGrid clicks to add moves as if a
    ### game is in session, add adornments, and handle the current move
    ### adornment.
    ###
    def okButton_click (self, button,  e):
        self.DialogResult = True

