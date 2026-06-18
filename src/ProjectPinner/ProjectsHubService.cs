using System;
using System.IO;

namespace ProjectPinner
{
    /// <summary>
    /// The "Projects hub" model: a single local folder ("Projects") that is pinned to Quick
    /// Access once, holding one ordinary Windows shortcut (.lnk) per project, each named with
    /// the friendly alias and pointing at the real network folder.
    ///
    /// SAFETY: a .lnk is a pure pointer file. Creating, renaming, or deleting one NEVER opens,
    /// writes to, renames, or otherwise touches the target network folder. The alias exists
    /// only as the local shortcut's file name. This is the safest possible mechanism.
    /// </summary>
    internal static class ProjectsHubService
    {
        /// <summary>Display name of the pinned hub folder (what shows in Quick Access).
        /// Set from Config at startup; changeable via the app's settings.</summary>
        public static string HubFolderName = "Projects";

        public static string HubDir => Path.Combine(AppPaths.InstallRoot, SafeFolderName());

        private static string SafeFolderName()
        {
            var n = ProjectService.SanitizeName(HubFolderName);
            return string.IsNullOrEmpty(n) ? "Projects" : n;
        }

        public static void EnsureHub() => AppPaths.EnsureDir(HubDir);

        /// <summary>
        /// Renames the hub folder (moving all the shortcuts with it), updates settings, and
        /// re-pins it under the new name if it was pinned. Only the local hub folder is moved;
        /// nothing on the network is touched.
        /// </summary>
        public static void RenameHub(string newName, Config cfg)
        {
            string clean = ProjectService.SanitizeName(newName);
            if (string.IsNullOrEmpty(clean))
                throw new ArgumentException("Enter a folder name.");

            string oldDir = HubDir;
            string newDir = Path.Combine(AppPaths.InstallRoot, clean);

            if (!string.Equals(oldDir, newDir, StringComparison.OrdinalIgnoreCase))
            {
                if (Directory.Exists(newDir))
                    throw new IOException("A folder named \"" + clean + "\" already exists.");

                bool wasPinned = false;
                try { wasPinned = IsHubPinned(); } catch { }

                if (Directory.Exists(oldDir))
                {
                    if (wasPinned) { try { QuickAccessService.Unpin(oldDir); } catch { } }
                    Directory.Move(oldDir, newDir);
                }

                HubFolderName = clean;
                if (cfg != null) { cfg.HubFolderName = clean; cfg.Save(); }

                if (wasPinned) { try { PinHub(); } catch { } }
            }
            else
            {
                HubFolderName = clean;
                if (cfg != null) { cfg.HubFolderName = clean; cfg.Save(); }
            }
        }

        /// <summary>Low-level: write a shortcut (.lnk) at an exact path pointing to target.</summary>
        public static void WriteShortcut(string lnkPath, string targetPath)
        {
            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null)
                throw new InvalidOperationException("Windows Script Host (WScript.Shell) is unavailable.");
            dynamic shell = Activator.CreateInstance(shellType);
            dynamic sc = shell.CreateShortcut(lnkPath);
            sc.TargetPath = targetPath;
            try { sc.WorkingDirectory = targetPath; } catch { }
            try { sc.Description = Path.GetFileNameWithoutExtension(lnkPath); } catch { }
            sc.Save();
        }

        /// <summary>Creates the project's alias shortcut inside the hub. Returns the .lnk path.</summary>
        public static string CreateProjectShortcut(string displayName, string targetPath)
        {
            EnsureHub();
            string lnk = Path.Combine(HubDir, displayName + ".lnk");
            if (File.Exists(lnk))
                throw new IOException("A pinned project named \"" + displayName + "\" already exists.");
            WriteShortcut(lnk, targetPath);
            return lnk;
        }

        /// <summary>Reads the target a shortcut points at.</summary>
        public static string ReadTarget(string lnkPath)
        {
            try
            {
                var shellType = Type.GetTypeFromProgID("WScript.Shell");
                dynamic shell = Activator.CreateInstance(shellType);
                dynamic sc = shell.CreateShortcut(lnkPath);
                string t = (string)sc.TargetPath;
                return string.IsNullOrEmpty(t) ? "(unknown)" : t;
            }
            catch { return "(unknown)"; }
        }

        /// <summary>
        /// Deletes ONLY the shortcut file. Refuses anything that is not a .lnk file, so it can
        /// never delete a directory or anything on the network.
        /// </summary>
        public static void RemoveShortcut(string lnkPath)
        {
            if (string.IsNullOrEmpty(lnkPath) || !File.Exists(lnkPath)) return;
            if (!lnkPath.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Refusing to delete \"" + lnkPath + "\": not a shortcut file.");
            File.Delete(lnkPath); // removes only the pointer; the target is never touched
        }

        public static bool PinHub()
        {
            EnsureHub();
            return QuickAccessService.Pin(HubDir);
        }

        public static bool IsHubPinned() => QuickAccessService.IsPinned(HubDir);
    }
}
