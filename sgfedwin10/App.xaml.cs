using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;
using Microsoft.Windows.AppLifecycle;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Task = System.Threading.Tasks.Task;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.ApplicationModel;
//using Microsoft.ApplicationModel.Activation;
using Microsoft.Foundation;
using Windows.ApplicationModel.Activation;
using Windows.Storage;
using Microsoft.UI.Dispatching;
//using System.Reflection;
//using Microsoft.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace SgfEdwin10
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            this.InitializeComponent();
        }

        /// OnLaunched runs when the application is launched normally by the end user.  
        /// The argument to this method in winui3 is bogus and meaninguless, see function body.
        ///
        protected override async void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs e)
        {
            // This code makes the app a single instance app. Multi instance apps are described
            // here https://docs.microsoft.com/en-us/windows/apps/windows-app-sdk/migrate-to-windows-app-sdk/guides/applifecycle#single-instancing-in-applicationonlaunched
            //
            // Register as "main" if first launch or retrieve the first instance.
            var mainInstance = Microsoft.Windows.AppLifecycle.AppInstance.FindOrRegisterForKey("main");
            // Get real launched args because framework can't do it and pass them.
            var activatedEventArgs = Microsoft.Windows.AppLifecycle.AppInstance.
                                     GetCurrent().GetActivatedEventArgs();
            if (! mainInstance.IsCurrent)
            {
                // Redirect the activation (and args) to the "main" instance, and exit.
                await mainInstance.RedirectActivationToAsync(activatedEventArgs);
                System.Diagnostics.Process.GetCurrentProcess().Kill();
                return;
            }
            mainInstance.Activated += (object? sender, AppActivationArguments arg) => {
                if (arg.Kind == ExtendedActivationKind.File)
                    App.MainWinPgInst.DispatcherQueue.TryEnqueue(
                        async () => await OnFileActivated(arg, false));
            };
            // Now handle file activation in current app instance.
            // https://docs.microsoft.com/en-us/windows/apps/windows-app-sdk/migrate-to-windows-app-sdk/guides/applifecycle#file-type-association
            if (activatedEventArgs.Kind == ExtendedActivationKind.File) {
                await OnFileActivated(activatedEventArgs, true);
            }
            // Initialize MainWindow here.  THIS IS MIGRATION TOOL GEN, so NOT my MainWinPg : Page
            Window = new MainWindow();
            Window.Activate();
            WindowHandle = WinRT.Interop.WindowNative.GetWindowHandle(Window);
        }


        /// OnFileActivated handles file launches.  This is not a handler but named as so by the
        /// uwp->winui3 upgrade tool.  I had to add the first flag because the new app args never
        /// report already running, always report file launch.  Need flag to check if already have
        /// file open or need to prompt to save a dirty game.
        ///
        public async Task OnFileActivated(AppActivationArguments args, bool first) {
            MainWinPg mainwinpg = App.MainWinPgInst;
            var argdata = args.Data as IFileActivatedEventArgs;
            // We only handle one file for now ...
            var sf = argdata.Files[0] as StorageFile;
            //var si = argdata.Files[0] as IStorageItem;
            // Check dirty save if running already
            if (! first) {
                //argdata.PreviousExecutionState == ApplicationExecutionState.Running ||
                //    argdata.PreviousExecutionState == ApplicationExecutionState.Suspended) {
                mainwinpg = App.MainWinPgInst;
                // If file is already open, just show it.  NOTE, if change this, check DoOpenButton
                var gindex = GameAux.ListFind(sf.Path, mainwinpg.Games, (sfname, g) => ((string)sfname) == ((Game)g).Filename);
                if (gindex != -1) {
                    await mainwinpg.GotoOpenGame(gindex);
                    return;
                }
                // Get new open file.
                await mainwinpg.CheckDirtySave();
            }
            // Stash this in case we hit an error reading the file or making the game 
            var game = mainwinpg.Game;
            try {
                mainwinpg.LastCreatedGame = null;
                await mainwinpg.GetFileGameCheckingAutoSave(sf);
            }
            catch (IOException err) {
                // Essentially handles unexpected EOF or malformed property values.
                FileActivatedErrorCleanup(mainwinpg, game);
                var ignoreTask = // Squelch warning that we're not awaiting Message, which we can't in catch blocks.
                GameAux.Message(err.Message + err.StackTrace);
            }
            catch (Exception err) {
                // No code paths should throw from incomplete, unrecoverable state, so should be fine to continue.
                // For example, game state should be intact (other than IsDirty) for continuing.
                FileActivatedErrorCleanup(mainwinpg, game);
                var ignoreTask = // Squelch warning that we're not awaiting Message, which we can't in catch blocks.
                GameAux.Message(err.Message + err.StackTrace);
            }
            mainwinpg.DrawGameTree();
        }


        /// FileActivatedErrorCleanup ensures that if we hit errors reading a file when activating he app via
        /// a file, then we restore state to the last current game or a default game.  This takes the ainwin and
        /// the game that was current when the file activation happened.
        ///
        private static void FileActivatedErrorCleanup (MainWinPg mainwin, Game game) {
            if (mainwin.LastCreatedGame != null) {
                // Error after creating game, so remove it and reset board to last game or default game.
                // The games list may not contain g because creating new games may delete the initial default game.
                mainwin.UndoLastGameCreation(mainwin.Games.Contains(game) ? game : null);
            }
        }




        /// MIGRATION TOOL generated, so this is their new MainWinow.
        public static MainWindow Window { get; private set; }

        public static IntPtr WindowHandle { get; private set; }
        // MainWinPgInst set in MainWinPg constructor to enable file open handling here.
        public static MainWinPg MainWinPgInst { get; internal set; }
    }
}
