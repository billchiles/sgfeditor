using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
using System.Windows; // Window, RoutedEventArgs
//using System.Windows.Controls;
//using System.Windows.Data;
//using System.Windows.Documents;
//using System.Windows.Input;
//using System.Windows.Media;
//using System.Windows.Media.Imaging;
//using System.Windows.Shapes;

namespace SgfEd {
    /// <summary>
    /// Interaction logic for Window1.xaml
    /// </summary>
    public partial class NewGameDialog : Window {

        public NewGameDialog() {
            InitializeComponent();
            this.sizeText.Text = "19";
            // For now we only handle 19x19 games
            this.sizeText.IsReadOnly = true;
            this.handicapText.Text = "0";
            this.komiText.Text = "6.5";
        }

        //// okButton_click handles new file dialog confirmation.
        ////
        private void okButton_click(object button, RoutedEventArgs e) {
            this.DialogResult = true;
        }
    }
}


