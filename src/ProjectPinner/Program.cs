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

            // COM EXE server mode: launched by Windows shell to serve IExplorerCommand.
            // COM passes "-Embedding" or "/Embedding" when activating an ExeServer.
            if (args.Any(a => string.Equals(a, "-Embedding", StringComparison.OrdinalIgnoreCase) ||
                               string.Equals(a, "/Embedding", StringComparison.OrdinalIgnoreCase)))
            {
                return ComServer.Run();
            }

            // From here on it's the GUI. Catch *everything* so a startup failure shows a
            // visible error + writes a log, instead of the window silently never appearing.
            try
            {
                AppDomain.CurrentDomain.UnhandledException += (s, e) =>
                    ReportFatal(e.ExceptionObject as Exception, "background thread");

                var cfg = Config.Load();
                ProjectsHubService.HubFolderName =
                    string.IsNullOrWhiteSpace(cfg.HubFolderName) ? "Projects" : cfg.HubFolderName;

                // Self-install only when running as a plain exe (not an MSIX package —
                // MSIX manages its own install/update lifecycle via the OS).
                bool isPackaged = IsRunningAsPackage();
                if (!isPackaged)
                {
                    try
                    {
                        if (!Installer.IsRunningFromInstallDir())
                            Installer.InstallFilesForCurrentUser();
                        Installer.CleanupOldExe();
                    }
                    catch { /* non-fatal */ }
                }

                var app = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
                app.DispatcherUnhandledException += OnDispatcherException;

                // Launched from the old-style registry right-click verb: "--pin <folder>".
                // (The MSIX path uses COM/IExplorerCommand instead, not this flag.)
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

        /// <summary>
        /// Returns true when the process has MSIX package identity (installed via .msix).
        /// Unpackaged plain-exe launches return false.
        /// </summary>
        private static bool IsRunningAsPackage()
        {
            try
            {
                uint len = 0;
                // ERROR_INSUFFICIENT_BUFFER (122) means packaged; 15700 means no package.
                int rc = NativeMethods.GetCurrentPackageName(ref len, null);
                return rc != 15700; // APPMODEL_ERROR_NO_PACKAGE
            }
            catch { return false; }
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
