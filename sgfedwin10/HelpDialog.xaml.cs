using System;
using Windows.System; // VirtualKey
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input; // KeyRoutedEventArgs

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace SgfEdwin10 {

    public sealed partial class HelpDialog : Page {

        // Expose a control so that main UI can put focus into dialog and stop main ui kbd handling.
        public Button OK_Button { get { return this.okButton; } }

        public HelpDialog () {
            this.InitializeComponent();
            // Ensure dialog overlays entire main window so that it cannot handle input.
            var bounds = Window.Current.Bounds;
            this.HelpDlgGrid.Width = bounds.Width;
            this.HelpDlgGrid.Height = bounds.Height;
        }

        //// HelpDialogClose is called by Ok clicks to signal owner.
        ////
        public event EventHandler HelpDialogClose;

        private void okButton_click (object sender, RoutedEventArgs e) {
            if (this.HelpDialogClose != null)
                this.HelpDialogClose(this, EventArgs.Empty);
        }
        //// HelpDlgKeydown just handles enter and escape for Ok button and stops input from going through.
        ////
        private void HelpDlgKeydown (object sender, KeyRoutedEventArgs e) {
            HelpDialog win;
            if (sender.GetType() == typeof(HelpDialog))
                win = (HelpDialog)sender;
            else
                win = this;
            if ((e.Key == VirtualKey.Escape) || (e.Key == VirtualKey.Enter)) {
                e.Handled = true;
                this.okButton_click(null, null);
                return;
            }
        } // HelpDlgKeydown

    } // HelpDialog Class
} // namespace
