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

namespace SgfEdwin10 {

    public sealed partial class HelpDialog : Page {
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

    }
}
