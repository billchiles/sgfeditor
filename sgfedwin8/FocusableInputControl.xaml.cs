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
    public sealed partial class FocusableInputControl : UserControl {

        public FocusableInputControl () {
            this.InitializeComponent();


            this.SizeChanged += FocusableInputControl_SizeChanged;
        }

        void FocusableInputControl_SizeChanged(object sender, SizeChangedEventArgs e) {
            var width = this.ActualWidth;
            var height = this.ActualHeight;
            if (width != height) {
                var size = Math.Min(width, height);
                if (this.ActualWidth != size)
                    this.Width = size;
                else
                    this.Height = size;
                var parent = this.Parent as Grid;
                if (parent != null) {
                    parent.ColumnDefinitions[0].Width = new GridLength(size + 2);
                }
            }
        }

    }
}
