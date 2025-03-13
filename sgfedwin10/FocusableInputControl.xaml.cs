using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Foundation;
//using Microsoft.Foundation.Collections;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace SgfEdwin10 {

    //// FocusableInputControl exists because in WinRT, grids cannot have keyboard or mouse handlers.
    //// This is only referenced in XAML for MainWinPg to capture input.
    ////
    public sealed partial class FocusableInputControl : UserControl {

        public FocusableInputControl () {
            this.InitializeComponent();
            //this.SizeChanged += FocusableInputControl_SizeChanged;
        }

        /// FocusableInputControl_SizeChanged was incorrect attempt to square the Go Board as window esized,
        /// otherwise it would stretch in funny distorted ways.  Clearly it isn't right to set the ocused input
        /// control's dimensions to hard values when we want the input protection to stretch to the ounds of the 
        /// grid cell.  This is also wrong to handle this the same for all uses (though we use it one ime) since
        /// the handler should be per instance where this is used in the UI.
        ///
        void FocusableInputControl_SizeChanged (object sender, SizeChangedEventArgs e) {
            //var width = this.ActualWidth;
            //var height = this.ActualHeight;
            //if (width != height) {
            //    var size = Math.Min(width, height);
            //    this.Width = size;
            //    this.Height = size;
            //    //if (this.ActualWidth != size)
            //    //    this.Width = size;
            //    //else
            //    //    this.Height = size;
            //    var parent = this.Parent as Grid;
            //    // Do NOT set parent, let layout recalc parent because setting it locked it and made automatic
            //    // resizing behaviors/aesthetics sub optimal.
            //    //if (parent != null)
            //    //{
            //    //    parent.ColumnDefinitions[0].Width = new GridLength(size);
            //    //}
            //}
        }

    }
}
