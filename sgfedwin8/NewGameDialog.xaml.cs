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

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace SgfEdwin8 {
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class NewGameDialog : Page {

        public bool NewGameConfirmed { get; set; }
        public string WhiteText { get; set; }
        public string BlackText { get; set; }
        public string SizeText { get; set; }
        public string HandicapText { get; set; }
        public string KomiText { get; set; }

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
    }
}
