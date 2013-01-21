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

        private MainWindow mainWin = null;

        public NewGameDialog () {
            this.InitializeComponent();
            this.sizeText.Text = "19";
            // For now we only handle 19x19 games
            this.sizeText.IsReadOnly = true;
            this.handicapText.Text = "0";
            this.komiText.Text = "6.5";
        }

        /// <summary>
        /// Invoked when this page is about to be displayed in a Frame.
        /// </summary>
        /// <param name="e">Event data that describes how this page was reached.  The Parameter
        /// property is typically used to configure the page.</param>
        protected override void OnNavigatedTo (NavigationEventArgs e) {
            this.mainWin = (MainWindow)e.Parameter;
            this.mainWin.NewDiaogDone = false;
        }

        private void okButton_click (object sender, RoutedEventArgs e) {
            this.mainWin.NewDialogInfo = Tuple.Create(this.whiteText.Text, this.blackText.Text, this.sizeText.Text,
                                                      this.handicapText.Text, this.komiText.Text);
            this.mainWin.NewDiaogDone = true;
            var x = this.Frame.CanGoBack;
            if (this.Frame.CanGoBack) {
                this.Frame.GoBack();
            }
        }

        private void cancelButton_click (object sender, RoutedEventArgs e) {
            this.mainWin.NewDialogInfo = null;
            this.mainWin.NewDiaogDone = false;
            if (this.Frame.CanGoBack) {
                this.Frame.GoBack();
            }
        }
    }
}
