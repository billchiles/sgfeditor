using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Foundation;
//using Microsoft.Foundation.Collections;
using Windows.System; // VirtualKey
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;


namespace SgfEdwin10 {

    public sealed partial class WindowSwitchingDialog : Page {

        //// SelectionConfirmed indicates whether the user selected a game to goto, wants to delete, or cancelled.
        ////
        public enum WindowSwitchResult {
            Switch, Delete, Cancel
        }
        public WindowSwitchResult SelectionConfirmed { get; set; }

        // Expose some UI elements since xaml members are private.
        //
        public string SelectedGame { get; set; }
        // Expose a control so that main UI can put focus into dialog and stop main ui kbd handling.
        public ListView GamesList { get { return this.gamesList; } }


        public WindowSwitchingDialog () {
            this.InitializeComponent();
            // Ensure dialog overlays entire main window so that it cannot handle input.
            var bounds = App.Window.Bounds;
            this.windowSwitchingDlgGrid.Width = bounds.Width;
            this.windowSwitchingDlgGrid.Height = bounds.Height;
        }

        protected override void OnNavigatedTo (NavigationEventArgs e) {
        }


        //// WindowSwitchingDialogClose is called by click handlers to signal owner.
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

    //// SampleStringList is for design time only of figuring out the dialog in the designer.
    ////
    public class SampleStringList : System.Collections.ObjectModel.ObservableCollection<string> {
        public SampleStringList () {
            Add("hello");
            Add("there");
            Add("world");
            Add("hello2");
            Add("hello3");
            Add("there4");
            Add("world5");
            Add("there6");
            Add("world7");
            Add("hello8");
            Add("there9");
            Add("worlda");
            Add("hellob");
            Add("therec");
            Add("worldd");
        }
    }



} // namespace