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

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace SgfEdwin8 {

    //// FocusableInputControl exists because in WinRT, grids cannot have keyboard or mouse handlers.
    //// This is only referenced in XAML for MainWindow to capture input.
    ////
    public sealed partial class FocusableInputControl : UserControl {

        public FocusableInputControl () {
            this.InitializeComponent();
            this.SizeChanged += FocusableInputControl_SizeChanged;
        }

        //// FocusableInputControl_SizeChanged only gets called on launch to ensure the game board is square.
        //// Not sure why it is never called again, but guessing some size setting in the xaml or setting size
        //// here on launch somehow permanently sets it since win10 seems to not allow resizing below a certain
        //// height (but lets you resize anywhere horizontally, just clipping the boar after squishing the
        //// commands, comment, and tree UI elements.
        ////
        void FocusableInputControl_SizeChanged (object sender, SizeChangedEventArgs e) {
            var width = this.ActualWidth;
            var height = this.ActualHeight;
            if (width != height) {
                var size = Math.Min(width, height);
                this.Width = size;
                this.Height = size;
                //if (this.ActualWidth != size)
                //    this.Width = size;
                //else
                //    this.Height = size;
                //var parent = this.Parent as Grid;
                //if (parent != null) {
                //    parent.ColumnDefinitions[0].Width = new GridLength(size);
                //}
            }
        }

    }
}
