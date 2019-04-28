using System;
using System.Collections.Generic;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

using Windows.System; // VirtualKey
using Windows.UI; // Color
using System.Reflection; // Used to get color names and map strings to colors in ColorsConverter
using System.Diagnostics; // Debug.Assert

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace SgfEdwin10 {

    public sealed partial class SomeSettings : Page {
        // Indicates whether ok or cancel button hit.
        public bool SettingsConfirmed { get; set; }

        // Needed to reset settings
        private MainWindow mainWin;

        // Used to mark if user hit reset.
        public bool ResetSettings { get; set; }

        // Expose properties to set current values into dialog and compare on done.
        //
        private int _titleSize = -1;
        public int TitleSize {
            get { return this._titleSize; }
            set { this._titleSize = value; this.titleSizeText.Text = value.ToString(); }
        }
        private int _indexesSize = -1;
        public int IndexesSize {
            get { return this._indexesSize; }
            set { this._indexesSize = value; this.indexSizeText.Text = value.ToString(); }
        }
        private int _commentSize = -1;
        public int CommentFontsize {
            get { return this._commentSize; }
            set { this._commentSize = value; this.commentSizeText.Text = value.ToString(); }
        }
        private int _treeNodeSize = -1;
        public int TreeNodeSize {
            get { return this._treeNodeSize; }
            set { this._treeNodeSize = value; this.treeNodeSizeText.Text = value.ToString(); }
        }
        private int _treeNodeFontsize = -1;
        public int TreeNodeFontsize {
            get { return this._treeNodeFontsize; }
            set { this._treeNodeFontsize = value; this.treeNodeFontSizeText.Text = value.ToString(); }
        }
        private Color _sentinelColor = Color.FromArgb(0xFF, 0, 0, 1);
        private Color _treeCurrentHighlight; // = this._sentinelColor
        public Color TreeCurrentHighlight {
            get { return this._treeCurrentHighlight; }
            set {
                this._treeCurrentHighlight = value;
                this.treeCurrentHighlight.Text = ColorsConverter.GetColorName(value);
            }
        }
        private Color _treeCommentsHighlight; // = this._sentinelColor
        public Color TreeCommentsHighlight {
            get { return this._treeCommentsHighlight; }
            set {
                this._treeCommentsHighlight = value;
                this.treeCommentsHighlight.Text = ColorsConverter.GetColorName(value);
            }
        }

        /// Need to expose this because controls are privately declared.
        public Button CancelButton { get { return this.cancelButton; } }

        public SomeSettings (MainWindow win) {
            this.InitializeComponent();
            this._treeCurrentHighlight = this._sentinelColor;
            this._treeCommentsHighlight = this._sentinelColor;
            this.mainWin = win;
            //var c = ColorsConverter.GetColorName(this.TreeCurrentHighlight); // Error test for sentinel
            // Ensure dialog overlays entire main window so that it cannot handle input.
            var bounds = Window.Current.Bounds;
            this.newDlgGrid.Width = bounds.Width;
            this.newDlgGrid.Height = bounds.Height;
        }

        //// SettingsDialogClose is called by Ok and Cancel clicks to signal owner.
        ////
        public event EventHandler SettingsDialogClose;


        //// okButton_click checks to see if any values changed, setting this.SettingsConfirmed accordingly.
        //// 
        private void okButton_click (object sender, RoutedEventArgs e) {
            // If reset, then assume something changed.  Even through resetting put new values into class
            // members, still check for the UI elements having different values after the reset.
            this.SettingsConfirmed = this.ResetSettings;
            int parsedInt = 0;
            // Check each box to see if anything changed, and grab the values.
            var c = ColorsConverter.ConvertToColor(this.treeCurrentHighlight.Text);
            var saveCurrent = this.TreeCurrentHighlight;
            if (!c.HasValue) {
                this.errorText.Text = "Use named color from https://docs.microsoft.com/en-us/uwp/api/windows.ui.colors";
                this.errorText.Foreground = new SolidColorBrush(Colors.Red);
                return;
            }
            if (c.Value != this.TreeCurrentHighlight) {
                this.SettingsConfirmed = true;
                this.TreeCurrentHighlight = c.Value;
            }
            c = ColorsConverter.ConvertToColor(this.treeCommentsHighlight.Text);
            if (!c.HasValue) {
                this.errorText.Text = "Use named color from https://docs.microsoft.com/en-us/uwp/api/windows.ui.colors";
                this.errorText.Foreground = new SolidColorBrush(Colors.Red);
                // Restore values changed and return
                this.TreeCurrentHighlight = saveCurrent;
                return;
            }
            if (c.Value != this.TreeCommentsHighlight) {
                this.SettingsConfirmed = true;
                this.TreeCommentsHighlight = c.Value;
            }
            if (int.TryParse(this.titleSizeText.Text, out parsedInt)) {
                if (parsedInt != this.TitleSize) {
                    this.SettingsConfirmed = true;
                    this.TitleSize = parsedInt;
                }
            }
            if (int.TryParse(this.indexSizeText.Text, out parsedInt)) {
                if (parsedInt != this.IndexesSize) {
                    this.SettingsConfirmed = true;
                    this.IndexesSize = parsedInt;
                }
            }
            if (int.TryParse(this.commentSizeText.Text, out parsedInt)) {
                if (parsedInt != this.CommentFontsize) {
                    this.SettingsConfirmed = true;
                    this.CommentFontsize = parsedInt;
                }
            }
            if (int.TryParse(this.treeNodeSizeText.Text, out parsedInt)) {
                if (parsedInt != this.TreeNodeSize) {
                    this.SettingsConfirmed = true;
                    this.TreeNodeSize = parsedInt;
                }
            }
            if (int.TryParse(this.treeNodeFontSizeText.Text, out parsedInt)) {
                if (parsedInt != this.TreeNodeFontsize) {
                    this.SettingsConfirmed = true;
                    this.TreeNodeFontsize = parsedInt;
                }
            }
            // Nofify dialog user it is done.
            if (this.SettingsDialogClose != null)
                this.SettingsDialogClose(this, EventArgs.Empty);
        } // okButton_click


        private void cancelButton_click (object sender, RoutedEventArgs e) {
            // Set to false to indicate nothing changed.
            this.SettingsConfirmed = false;
            if (this.SettingsDialogClose != null)
                this.SettingsDialogClose(this, EventArgs.Empty);
        }

        //// resetButton_click calls back to MainWindow to push the defaults into the dialog.
        //// The property setters notice it is not the first time they were called and restores sentinel
        //// values to the backing store, while putting default values into the UI elements.  This
        //// means the OK code will see the values changed and put the default values from the UI into code.
        ////
        private void resetButton_click (object sender, RoutedEventArgs e) {
            this.ResetSettings = true;
            this.mainWin.RestoreSettingsDefaults(this);
            this.errorText.Text = "All values are integers or color names";
            this.errorText.Foreground = new SolidColorBrush(Colors.White);
        }


        //// NewGameKeydown just handles enter and escape for Ok and Cancel buttons.
        ////
        private void SettingsKeydown (object sender, KeyRoutedEventArgs e) {
            SomeSettings win;
            if (sender.GetType() == typeof(SomeSettings))
                win = (SomeSettings)sender;
            else
                win = this;
            if (e.Key == VirtualKey.Escape) {
                e.Handled = true;
                this.cancelButton_click(null, null);
                return;
            }
            else if (e.Key == VirtualKey.Enter) {
                e.Handled = true;
                this.okButton_click(null, null);
            }
        } // SettingsKeydown


        //protected override void OnNavigatedTo (NavigationEventArgs e) {
        //}

    } // SomeSettings class


    //// Extension to convert a string color name like "Red", "red" or "RED" into a Color.
    //// Using ColorsConverter instead of ColorConverter as class name to prevent conflicts with
    //// the WPF System.Windows.Media.ColorConverter.
    ////
    internal static class ColorsConverter {

        public static Color? ConvertToColor (string colorName) {
            MyDbg.Assert(!string.IsNullOrEmpty(colorName), "Must verify string, cannot be null or empty.");
            //if (string.IsNullOrEmpty(colorName)) throw new ArgumentNullException("colorName");
            MethodBase getColorMethod = FindGetColorMethod(colorName);
            if (getColorMethod == null) {
                // Using FormatException like the WPF System.Windows.Media.ColorConverter
                //throw new FormatException("Color name " + colorName + " not found in " + typeof(Colors).FullName);
                return null;
            }
            return (Color)getColorMethod.Invoke(null, null);
        }

        private static MethodBase FindGetColorMethod (string colorName) {
            foreach (PropertyInfo propertyInfo in typeof(Colors).GetTypeInfo().DeclaredProperties) {
                if (propertyInfo.Name.Equals(colorName, StringComparison.OrdinalIgnoreCase)) {
                    MethodBase getMethod = propertyInfo.GetMethod;
                    if (getMethod.IsPublic && getMethod.IsStatic)
                        return getMethod;
                    break;
                }
            }
            return null;
        }

        private static Dictionary<Color, string> NamedColors = new Dictionary<Color, string>();
        public static string GetColorName (Color colour) {
            if (ColorsConverter.NamedColors.Count == 0) {
                foreach (var c in typeof(Colors).GetRuntimeProperties()) {
                    ColorsConverter.NamedColors[(Color)c.GetValue(null)] = c.Name;
                }
            }
            // We start with a named color and only allow users to choose named colors.
            MyDbg.Assert(ColorsConverter.NamedColors.ContainsKey(colour), "Huh?!  How did unnamed color get into UI.");
            return ColorsConverter.NamedColors[colour];
        }

    } // ColorsConverter
}

