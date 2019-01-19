using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

using Windows.System; // VirtualKeyModifiers


namespace SgfEdwin10 {
    public sealed partial class NewGameDialog : Page {
        // Indicates whether ok or cancel button hit.
        public bool NewGameConfirmed { get; set; }
        // Expose values since members are by default private.
        public string WhiteText { get; set; }
        public string BlackText { get; set; }
        public string SizeText { get; set; }
        public string HandicapText { get; set; }
        public string KomiText { get; set; }
        // Expose a control so that main UI can put focus into dialog and stop main ui kbd handling.
        public TextBox WhiteTextBox { get { return this.whiteText; } }

        public NewGameDialog () {
            this.InitializeComponent();
            this.sizeText.Text = "19";
            // For now we only handle 19x19 games
            this.sizeText.IsReadOnly = true;
            this.handicapText.Text = "0";
            this.komiText.Text = "6.5";
            // Ensure dialog overlays entire main window so that it cannot handle input.
            var bounds = Window.Current.Bounds;
            this.newDlgGrid.Width = bounds.Width;
            this.newDlgGrid.Height = bounds.Height;
        }

        ////// Needed this when trying to navigate to the dialog as a page.
        ////// That didn't work since win8 tears down the main page, and it
        ////// required crufty hacks to coordinate and synchronize with MianWindow.
        //////
        //protected override void OnNavigatedTo (NavigationEventArgs e) {
        //    this.mainWin = (MainWindow)e.Parameter;
        //    this.mainWin.NewDiaogDone = false;
        //}


        //// NewGameDialogClose is called by Ok and Cancel clicks to signal owner.
        ////
        public event EventHandler NewGameDialogClose;

        private void okButton_click (object sender, RoutedEventArgs e) {
            this.NewGameConfirmed = true;
            this.WhiteText = this.whiteText.Text;
            this.BlackText = this.blackText.Text;
            this.SizeText = this.sizeText.Text;
            this.HandicapText = this.handicapText.Text;
            this.KomiText = this.komiText.Text;
            if (this.NewGameDialogClose != null)
                this.NewGameDialogClose(this, EventArgs.Empty);
            // This is from trying to navigate to the new dialog as a page.  See OnNavigatedTo.
            //
            //this.mainWin.NewDialogInfo = Tuple.Create(this.whiteText.Text, this.blackText.Text, this.sizeText.Text,
            //                                          this.handicapText.Text, this.komiText.Text);
            //
            //this.mainWin.NewDiaogDone = true;
            //if (this.Frame.CanGoBack) {
            //    this.Frame.GoBack();
            //}
        }

        private void cancelButton_click (object sender, RoutedEventArgs e) {
            this.NewGameConfirmed = false;
            if (this.NewGameDialogClose != null)
                this.NewGameDialogClose(this, EventArgs.Empty);
            // This is from trying to navigate to the new dialog as a page.  See OnNavigatedTo.
            //
            //this.mainWin.NewDialogInfo = null;
            //this.mainWin.NewDiaogDone = false;
            //if (this.Frame.CanGoBack) {
            //    this.Frame.GoBack();
            //}
        }

        //// NewGameKeydown just handles enter and escape for Ok and Cancel buttons.
        ////
        private void NewGameKeydown (object sender, KeyRoutedEventArgs e) {
            NewGameDialog win;
            if (sender.GetType() == typeof(NewGameDialog))
                win = (NewGameDialog)sender;
            else
                win = this;
            if (e.Key == VirtualKey.Escape) {
                e.Handled = true;
                this.cancelButton_click(null, null);
                return;
            }
            else if (e.Key == VirtualKey.Enter) {
                e.Handled = true;
                this.okButton_click(null, null);
            }

        }
    }
}
