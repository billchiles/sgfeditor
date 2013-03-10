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
    public sealed partial class GameInfo : Page {

        public bool GameInfoConfirmed { get; set; }
        public string WhiteText { get; set; }
        public string BlackText { get; set; }
        public string HandicapText { get; set; }
        public string KomiText { get; set; }

        public GameInfo () {
            this.InitializeComponent();
            this.gameInfoHA.Text = "0";
            this.gameInfoKM.Text = "6.5";
            // Ensure dialog overlays entire main window so that it cannot handle input.
            var bounds = Window.Current.Bounds;
            this.gameInfoGrid.Width = bounds.Width;
            this.gameInfoGrid.Height = bounds.Height;
        }

        /// <summary>
        /// Invoked when this page is about to be displayed in a Frame.
        /// </summary>
        /// <param name="e">Event data that describes how this page was reached.  The Parameter
        /// property is typically used to configure the page.</param>
        protected override void OnNavigatedTo (NavigationEventArgs e) {
        }

        //// NewGameDialogClose is called by Ok and Cancel clicks to signal owner.
        ////
        public event EventHandler GameInfoDialogClose;

        
        private void okButton_click (object sender, RoutedEventArgs e) {
            this.GameInfoConfirmed = true;
            this.WhiteText = this.gameInfoPW.Text;
            this.BlackText = this.gameInfoPB.Text;
            this.HandicapText = this.gameInfoHA.Text;
            this.KomiText = this.gameInfoKM.Text;
            if (this.GameInfoDialogClose != null)
                this.GameInfoDialogClose(this, EventArgs.Empty);
        }

        private void cancelButton_click (object sender, RoutedEventArgs e) {
            this.GameInfoConfirmed = false;
            if (this.GameInfoDialogClose != null)
                this.GameInfoDialogClose(this, EventArgs.Empty);
        }

    }
}
