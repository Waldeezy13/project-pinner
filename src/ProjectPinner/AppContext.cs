using System;
using System.IO;

namespace ProjectPinner
{
    /// <summary>
    /// Central, machine-local paths and constants. Everything lives under
    /// %LOCALAPPDATA% on purpose: LocalAppData is never roamed or redirected to a
    /// network share, so a symlink stored there is always a *local-to-remote* link
    /// (which Windows allows by default), never remote-to-remote (disabled by default).
    /// </summary>
    internal static class AppPaths
    {
        public const string AppName = "Project Pinner";
        public const string ExeName = "ProjectPinner.exe";

        /// <summary>Vendor attribution, surfaced in the UI and the exe metadata.</summary>
        public const string Vendor = "Waldo Development LLC";

        /// <summary>Install root: %LOCALAPPDATA%\ProjectPinner</summary>
        public static string InstallRoot =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ProjectPinner");

        /// <summary>Where the renamed symlinks live: %LOCALAPPDATA%\ProjectPinner\Links</summary>
        public static string LinksDir => Path.Combine(InstallRoot, "Links");

        /// <summary>The installed copy of this executable.</summary>
        public static string InstalledExePath => Path.Combine(InstallRoot, ExeName);

        /// <summary>On-disk copy of the app icon (used by the right-click menu + shortcut).</summary>
        public static string IconPath => Path.Combine(InstallRoot, "app.ico");

        /// <summary>Settings file.</summary>
        public static string ConfigPath => Path.Combine(InstallRoot, "settings.json");

        /// <summary>Start Menu shortcut path for the current user.</summary>
        public static string StartMenuShortcut => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Programs), AppName + ".lnk");

        public static void EnsureDir(string path)
        {
            if (!string.IsNullOrEmpty(path) && !Directory.Exists(path))
                Directory.CreateDirectory(path);
        }
    }
}
