using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage; // StorageFile for OnFileActivated(), create collision options
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;


// The Blank Application template is documented at http://go.microsoft.com/fwlink/?LinkId=234227

namespace SgfEdwin8
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    sealed partial class App : Application
    {
        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            this.InitializeComponent();
            this.Suspending += OnSuspending;
        }

        protected override void OnLaunched (LaunchActivatedEventArgs args) {
            Frame rootFrame = Window.Current.Content as Frame;
            // Do not repeat app initialization when the Window already has content,
            // just ensure that the window is active
            if (rootFrame == null) {
                // Create a Frame to act as the navigation context and navigate to the first page
                rootFrame = new Frame();
                if (args.PreviousExecutionState == ApplicationExecutionState.Terminated) {
                    //TODO: Load state from previously suspended application
                }
                // Place the frame in the current Window
                Window.Current.Content = rootFrame;
            }
            if (rootFrame.Content == null) {
                // When the navigation stack isn't restored navigate to the first page,
                // configuring the new page by passing required information as a navigation
                // parameter
                if (! rootFrame.Navigate(typeof(MainWindow), args.Arguments)) {
                    throw new Exception("Failed to create initial page");
                }
            }
            // Ensure the current window is active
            Window.Current.Activate();
        }

        protected override async void OnFileActivated (FileActivatedEventArgs args) {
            base.OnFileActivated(args);
            MainWindow mainwin;
            // We only handle one file for now ...
            var sf = args.Files[0] as StorageFile;
            // Check dirty save if running already
            if (args.PreviousExecutionState == ApplicationExecutionState.Running ||
                    args.PreviousExecutionState == ApplicationExecutionState.Suspended) {
                mainwin = ((Frame)Window.Current.Content).Content as MainWindow;
                // If user dbl clicked file already open, we're done.
                if (sf.Name == mainwin.Game.Filename)
                    return;
                await mainwin.CheckDirtySave();
            } else {
                // else create main UI as OnLaunched does
                var frame = new Frame();
                Window.Current.Content = frame;
                // Need to set this so that an unnamed auto save doesn't prompt to re-open when user specified file.
                MainWindow.FileActivatedNoUnnamedAutoSave = true;
                frame.Navigate(typeof(MainWindow));
                mainwin = frame.Content as MainWindow;
                Window.Current.Activate();
            }
            await mainwin.GetFileGameCheckingAutoSave(sf);
            mainwin.DrawGameTree();
            //win.FocusOnStones();
        }


        private async void OnSuspending(object sender, SuspendingEventArgs e)
        {
            var deferral = e.SuspendingOperation.GetDeferral();
            // Save application state and stop any background activity
            var mainwin = ((Frame)Window.Current.Content).Content as MainWindow;
            await mainwin.MaybeAutoSave();
            // Signal done.
            deferral.Complete();
        }

    }
}
