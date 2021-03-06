﻿using System;
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

namespace SgfEdwin10 {

    //// FocusableInputControl exists because in WinRT, grids cannot have keyboard or mouse handlers.
    //// This is only referenced in XAML for MainWindow to capture input.
    ////
    public sealed partial class FocusableInputControl : UserControl {

        public FocusableInputControl () {
            this.InitializeComponent();
            //this.SizeChanged += FocusableInputControl_SizeChanged;
        }

        //// FocusableInputControl_SizeChanged was incorrect attempt to square the Go Board as window resized,
        //// otherwise it would stretch in funny distorted ways.  Clearly it isn't right to set the focused input
        //// control's dimensions to hard values when we want the input protection to stretch to the bounds of the 
        //// grid cell.  This is also wrong to handle this the same for all uses (though we use it one time) since
        //// the handler should be per instance where this is used in the UI.
        ////
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
