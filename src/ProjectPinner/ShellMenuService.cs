using System;
using System.IO;
using Microsoft.Win32;

namespace ProjectPinner
{
    /// <summary>
    /// Adds/removes the "Pin with alias to Quick Access" entry to the folder right-click
    /// menu in File Explorer. Written under HKCU\Software\Classes, so it needs NO admin
    /// rights and only affects the current user.
    ///
    /// Note: on Windows 11 classic verbs like this appear under "Show more options"
    /// (Shift+F10), not the top-level modern menu. On Windows 10 it shows directly.
    /// </summary>
    internal static class ShellMenuService
    {
        private const string MenuText = "Pin with alias to Quick Access";

        // Registering under both "Directory" (a folder) and "Drive" would be overkill;
        // a folder is what gets pinned, so Directory is enough. UNC and mapped-drive
        // folders both resolve to the Directory class in Explorer.
        private const string KeyPath = @"Software\Classes\Directory\shell\ProjectPinnerAlias";

        private static bool IsRunningAsPackage()
        {
            try
            {
                uint len = 0;
                return NativeMethods.GetCurrentPackageName(ref len, null) != 15700;
            }
            catch { return false; }
        }

        private static string TargetExe()
        {
            string exe = AppPaths.InstalledExePath;
            if (!File.Exists(exe)) exe = Installer.CurrentExePath();
            return exe;
        }

        /// <summary>Ensures the on-disk app.ico exists; returns its path, or null on failure.</summary>
        private static string EnsureIconFile()
        {
            try
            {
                if (!File.Exists(AppPaths.IconPath)) AppIcon.WriteIcoToDisk(AppPaths.IconPath);
                return File.Exists(AppPaths.IconPath) ? AppPaths.IconPath : null;
            }
            catch { return null; }
        }

        public static void Register()
        {
            // MSIX packages register the verb via AppxManifest/IExplorerCommand — no registry needed.
            if (IsRunningAsPackage()) return;
            string exe = TargetExe();
            if (string.IsNullOrEmpty(exe)) return;

            // Prefer a standalone .ico file for the menu glyph (most reliable); fall back to
            // the exe's embedded icon. The "Icon" value is an icon-location string (the shell
            // splits it on a comma, then calls ExtractIcon) - NOT a command line - so it must
            // be UNQUOTED; the comma-split means spaces in the path still resolve fine.
            string icon = EnsureIconFile();
            string iconValue = icon != null ? icon : exe + ",0";

            using (var key = Registry.CurrentUser.CreateSubKey(KeyPath))
            {
                key.SetValue(null, MenuText);                 // the menu label
                key.SetValue("Icon", iconValue);              // app icon on the menu item
            }
            using (var cmd = Registry.CurrentUser.CreateSubKey(KeyPath + @"\command"))
            {
                // %1 is the folder the user right-clicked.
                cmd.SetValue(null, "\"" + exe + "\" --pin \"%1\"");
            }
        }

        public static void Unregister()
        {
            try { Registry.CurrentUser.DeleteSubKeyTree(KeyPath, false); }
            catch { /* not present == already removed */ }
        }

        public static bool IsRegistered()
        {
            try
            {
                using (var cmd = Registry.CurrentUser.OpenSubKey(KeyPath + @"\command"))
                {
                    var v = cmd?.GetValue(null) as string;
                    return !string.IsNullOrEmpty(v) &&
                           v.IndexOf("--pin", StringComparison.OrdinalIgnoreCase) >= 0;
                }
            }
            catch { return false; }
        }
    }
}
