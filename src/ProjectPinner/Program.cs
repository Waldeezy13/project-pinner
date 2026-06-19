using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Threading;

namespace ProjectPinner
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            // Headless self-test (handy for CI / quick checks): "--selftest".
            if (args.Any(a => string.Equals(a, "--selftest", StringComparison.OrdinalIgnoreCase)))
            {
                var r = SelfTest.Run();
                Console.WriteLine(r.Summary);
                Console.WriteLine(r.Details);
                return r.Passed ? 0 : 1;
            }

            // Silent full uninstall (used by the in-app Uninstall button's deferred path and by
            // any external caller): remove the MSIX menu, classic verb, pins, shortcuts, then
            // delete the install dir once we exit.
            if (args.Any(a => string.Equals(a, "--uninstall", StringComparison.OrdinalIgnoreCase)))
            {
                try { Installer.FullUninstall(); } catch { }
                Installer.ScheduleInstallDirDeletion();
                return 0;
            }

            // From here on it's the GUI. Catch *everything* so a startup failure shows a
            // visible error + writes a log, instead of the window silently never appearing.
            //
            // The exe always runs UNPACKAGED — either launched directly by the user, or
            // launched by the native shell-ext DLL with "--pin <folder>". The Win11 modern
            // menu is provided by the separate sparse MSIX + ProjectPinner.ShellExt.dll, not
            // by this process, so there is no COM-server / packaged mode here.
            try
            {
                AppDomain.CurrentDomain.UnhandledException += (s, e) =>
                    ReportFatal(e.ExceptionObject as Exception, "background thread");

                var cfg = Config.Load();
                ProjectsHubService.HubFolderName =
                    string.IsNullOrWhiteSpace(cfg.HubFolderName) ? "Projects" : cfg.HubFolderName;

                // First launch / a freshly downloaded build (i.e. not yet running from the
                // install dir) = install-or-update: copy ourselves into LocalAppData, make a
                // Start Menu shortcut, and set up the right-click menu. Idempotent; on a normal
                // launch from the install dir this whole block is skipped.
                try
                {
                    if (!Installer.IsRunningFromInstallDir())
                    {
                        Installer.InstallFilesForCurrentUser();  // exe, Start Menu, icon, classic verb

                        // Register the signed MSIX + native DLL for the Windows 11 top-level
                        // menu. If that succeeds, drop the classic verb so the entry isn't
                        // duplicated under "Show more options"; if the package isn't available
                        // (e.g. Windows 10), the classic verb remains as the fallback.
                        bool modern = false;
                        try { modern = ContextMenuInstaller.EnsureInstalled(); } catch { }
                        if (modern) { try { ShellMenuService.Unregister(); } catch { } }
                    }
                    Installer.CleanupOldExe();
                }
                catch { /* non-fatal */ }

                var app = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
                app.DispatcherUnhandledException += OnDispatcherException;

                // Launched from a right-click verb (classic registry verb OR the native
                // shell-ext DLL's IExplorerCommand): "--pin <folder>".
                string pinPath = ArgValue(args, "--pin");
                Window window = !string.IsNullOrEmpty(pinPath)
                    ? (Window)new QuickPinWindow(pinPath, cfg)
                    : new MainWindow(cfg);
                return app.Run(window);
            }
            catch (Exception ex)
            {
                ReportFatal(ex, "startup");
                return 1;
            }
        }

        private static void OnDispatcherException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            ReportFatal(e.Exception, "UI thread");
            e.Handled = true; // keep the app alive so the user can read the message
        }

        /// <summary>Writes a crash log and shows a message box - turns "nothing happened" into a clue.</summary>
        private static void ReportFatal(Exception ex, string where)
        {
            string logPath = null;
            try
            {
                AppPaths.EnsureDir(AppPaths.InstallRoot);
                logPath = Path.Combine(AppPaths.InstallRoot, "error.log");
                File.AppendAllText(logPath,
                    "[" + DateTime.Now.ToString("u") + "] (" + where + ")\r\n" + ex + "\r\n\r\n");
            }
            catch { /* logging must never throw */ }

            try
            {
                System.Windows.MessageBox.Show(
                    "Project Pinner hit an error while starting (" + where + "):\r\n\r\n" +
                    (ex?.Message ?? "Unknown error") +
                    (logPath != null ? "\r\n\r\nDetails saved to:\r\n" + logPath : ""),
                    AppPaths.AppName, MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch { /* if even MessageBox fails, the log is still written */ }
        }

        private static string ArgValue(string[] args, string key)
        {
            for (int i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], key, StringComparison.OrdinalIgnoreCase))
                    return args[i + 1];
            return null;
        }
    }
}
