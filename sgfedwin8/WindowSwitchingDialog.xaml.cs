using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.System; // VirtualKey
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace SgfEdwin8 {

    public sealed partial class WindowSwitchingDialog : Page {

        // Indicates whether ok or cancel button hit.
        public WindowSwitchResult SelectionConfirmed { get; set; }

        public enum WindowSwitchResult {
            Switch, Delete, Cancel
        }

        // Expose values since members are by default private.
        public string SelectedGame { get; set; }
        // Expose a control so that main UI can put focus into dialog and stop main ui kbd handling.
        public ListBox GamesList { get { return this.gamesList; } }


        public WindowSwitchingDialog () {
            this.InitializeComponent();
            // Ensure dialog overlays entire main window so that it cannot handle input.
            var bounds = Window.Current.Bounds;
            this.windowSwitchingDlgGrid.Width = bounds.Width;
            this.windowSwitchingDlgGrid.Height = bounds.Height;
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
        public event EventHandler WindowSwitchingDialogClose;

        private void selectButton_click (object sender, RoutedEventArgs e) {
            this.SelectionConfirmed = WindowSwitchResult.Switch;
            this.SelectedGame = (string)this.gamesList.SelectedItem;
            if (this.WindowSwitchingDialogClose != null)
                this.WindowSwitchingDialogClose(this, EventArgs.Empty);
        }

        private void cancelButton_click (object sender, RoutedEventArgs e) {
            this.SelectionConfirmed = WindowSwitchResult.Cancel;
            if (this.WindowSwitchingDialogClose != null)
                this.WindowSwitchingDialogClose(this, EventArgs.Empty);
        }

        private void deleteButton_click (object sender, RoutedEventArgs e) {
            this.SelectionConfirmed = WindowSwitchResult.Delete;
            if (this.WindowSwitchingDialogClose != null)
                this.WindowSwitchingDialogClose(this, EventArgs.Empty);

        }



        
        //// WindowSwitchingKeyDown just handles enter and escape for Select and Cancel buttons.
        ////
        private void WindowSwitchingKeyDown (object sender, KeyRoutedEventArgs e) {
            WindowSwitchingDialog win;
            if (sender.GetType() == typeof(WindowSwitchingDialog))
                win = (WindowSwitchingDialog)sender;
            else
                win = this;
            if (e.Key == VirtualKey.Escape) {
                e.Handled = true;
                this.cancelButton_click(null, null);
                return;
            }
            else if (e.Key == VirtualKey.Enter) {
                e.Handled = true;
                this.selectButton_click(null, null);
            }
        }

    } // WindowSwitchingDialog class

} // namespace