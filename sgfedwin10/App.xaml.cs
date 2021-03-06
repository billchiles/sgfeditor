﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage; // StorageFile for OnFileActivated(), create collision options
using Windows.UI.ViewManagement; // used for experiment to set min size, commented out
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace SgfEdwin10
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
            Microsoft.ApplicationInsights.WindowsAppInitializer.InitializeAsync(
                Microsoft.ApplicationInsights.WindowsCollectors.Metadata |
                Microsoft.ApplicationInsights.WindowsCollectors.Session);
            this.InitializeComponent();
            this.Suspending += OnSuspending;
        }

        protected override void OnLaunched (LaunchActivatedEventArgs e) {

#if DEBUG
            //if (System.Diagnostics.Debugger.IsAttached)
            //{
            //    this.DebugSettings.EnableFrameRateCounter = true;
            //}
#endif

            // Testing min size ... no op if > 500x500
            //ApplicationView.GetForCurrentView().SetPreferredMinSize(new Size(800, 600));

            Frame rootFrame = Window.Current.Content as Frame;

            // Do not repeat app initialization when the Window already has content,
            // just ensure that the window is active
            if (rootFrame == null) {
                // Create a Frame to act as the navigation context and navigate to the first page
                rootFrame = new Frame();

                rootFrame.NavigationFailed += OnNavigationFailed;

                if (e.PreviousExecutionState == ApplicationExecutionState.Terminated) {
                    //TODO: Load state from previously suspended application
                }

                // Place the frame in the current Window
                Window.Current.Content = rootFrame;
            }

            if (rootFrame.Content == null) {
                // When the navigation stack isn't restored navigate to the first page,
                // configuring the new page by passing required information as a navigation
                // parameter
                rootFrame.Navigate(typeof(MainWindow), e.Arguments);
            }
            // Ensure the current window is active
            Window.Current.Activate();
        }


        void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            throw new Exception("Failed to load Page " + e.SourcePageType.FullName);
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
                // If file is already open, just show it.  NOTE, if change this, check DoOpenButton
                var gindex = GameAux.ListFind(sf.Path, mainwin.Games, (sfname, g) => ((string)sfname) == ((Game)g).Filename);
                if (gindex != -1) {
                    await mainwin.GotoOpenGame(gindex);
                    return;
                }
                // Get new open file.
                await mainwin.CheckDirtySave();
            }
            else {
                // else create main UI as OnLaunched does
                var frame = new Frame();
                Window.Current.Content = frame;
                // Need to set this so that an unnamed auto save doesn't prompt to re-open when user specified file.
                MainWindow.FileActivatedNoUnnamedAutoSave = true;
                frame.Navigate(typeof(MainWindow));
                mainwin = frame.Content as MainWindow;
                Window.Current.Activate();
            }
            // Stash this in case we hit an error reading the file or making the game in the activated file.
            var game = mainwin.Game;
            try {
                mainwin.LastCreatedGame = null;
                await mainwin.GetFileGameCheckingAutoSave(sf);
            }
            catch (IOException err) {
                // Essentially handles unexpected EOF or malformed property values.
                FileActivatedErrorCleanup(mainwin, game);
                var ignoreTask = // Squelch warning that we're not awaiting Message, which we can't in catch blocks.
                GameAux.Message(err.Message + err.StackTrace);
            }
            catch (Exception err) {
                // No code paths should throw from incomplete, unrecoverable state, so should be fine to continue.
                // For example, game state should be intact (other than IsDirty) for continuing.
                FileActivatedErrorCleanup(mainwin, game);
                var ignoreTask = // Squelch warning that we're not awaiting Message, which we can't in catch blocks.
                GameAux.Message(err.Message + err.StackTrace);
            }
            mainwin.DrawGameTree();
        }

        //// FileActivatedErrorCleanup ensures that if we hit errors reading a file when activating the app via
        //// a file, then we restore state to the last current game or a default game.  This takes the mainwin and
        //// the game that was current when the file activation happened.
        ////
        private static void FileActivatedErrorCleanup (MainWindow mainwin, Game game) {
            if (mainwin.LastCreatedGame != null) {
                // Error after creating game, so remove it and reset board to last game or default game.
                // The games list may not contain g because creating new games may delete the initial default game.
                mainwin.UndoLastGameCreation(mainwin.Games.Contains(game) ? game : null);
            }
        }





        //// Invoked when application execution suspended.  Saves state
        //// without knowing whether the application terminates, or can later resume with the contents
        //// of memory still intact.
        private async void OnSuspending (object sender, SuspendingEventArgs e) {
            var deferral = e.SuspendingOperation.GetDeferral();
            //TODO: Save application state and stop any background activity
            var mainwin = ((Frame)Window.Current.Content).Content as MainWindow;
            await mainwin.MaybeAutoSave();
            deferral.Complete();
        }
    }
}
