using System;
using System.IO;

namespace ProjectPinner
{
    /// <summary>
    /// Creates a Windows "folder shortcut" - a real local folder (named with the friendly
    /// alias) that the shell redirects to a target path. This is the same mechanism Windows
    /// uses for "Add a network location" entries.
    ///
    /// Why this instead of a symlink:
    ///  - Quick Access shows the FOLDER'S OWN name (the alias), because it is a real folder,
    ///    not a reparse point that the shell resolves to the network target's name.
    ///  - It needs NO admin rights and NO "create symbolic links" privilege.
    ///  - Deleting it removes only the local folder + two tiny placeholder files; the network
    ///    target is never touched (it is referenced by a .lnk, never linked into).
    /// </summary>
    internal static class FolderShortcutService
    {
        private const string TargetLnk = "target.lnk";
        private const string DesktopIni = "desktop.ini";

        // The shell "Folder Shortcut" handler. A folder marked System whose desktop.ini
        // names this CLSID2 is redirected to the target.lnk inside it.
        private const string FolderShortcutClsid = "{0AFACED1-E828-11D1-9187-B532F1E9575D}";

        public static void Create(string aliasFolderPath, string targetPath)
        {
            Directory.CreateDirectory(aliasFolderPath);

            string lnkPath = Path.Combine(aliasFolderPath, TargetLnk);
            string iniPath = Path.Combine(aliasFolderPath, DesktopIni);

            // Clear attributes if we're recreating, so the writes can't be blocked.
            ClearAttrs(lnkPath);
            ClearAttrs(iniPath);

            CreateTargetShortcut(lnkPath, targetPath);

            File.WriteAllText(iniPath,
                "[.ShellClassInfo]\r\n" +
                "CLSID2=" + FolderShortcutClsid + "\r\n" +
                "Flags=2\r\n");

            // Hide the plumbing; mark the folder System so the shell honors desktop.ini.
            File.SetAttributes(lnkPath, FileAttributes.Hidden | FileAttributes.System);
            File.SetAttributes(iniPath, FileAttributes.Hidden | FileAttributes.System);
            var di = new DirectoryInfo(aliasFolderPath);
            di.Attributes |= FileAttributes.System;
        }

        private static void CreateTargetShortcut(string lnkPath, string targetPath)
        {
            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null)
                throw new InvalidOperationException("Windows Script Host (WScript.Shell) is unavailable.");
            dynamic shell = Activator.CreateInstance(shellType);
            dynamic sc = shell.CreateShortcut(lnkPath);
            sc.TargetPath = targetPath;
            try { sc.WorkingDirectory = targetPath; } catch { }
            sc.Save();
        }

        /// <summary>True if the folder looks like one of our folder shortcuts.</summary>
        public static bool IsFolderShortcut(string folderPath)
        {
            try
            {
                string lnk = Path.Combine(folderPath, TargetLnk);
                string ini = Path.Combine(folderPath, DesktopIni);
                if (!File.Exists(lnk) || !File.Exists(ini)) return false;
                // Only ours: desktop.ini must reference the folder-shortcut CLSID. This stops
                // an unrelated local folder that merely contains a target.lnk from matching.
                return File.ReadAllText(ini).IndexOf(FolderShortcutClsid, StringComparison.OrdinalIgnoreCase) >= 0;
            }
            catch { return false; }
        }

        /// <summary>Reads the network/target path the folder shortcut points at.</summary>
        public static string ReadTarget(string folderPath)
        {
            try
            {
                string lnk = Path.Combine(folderPath, TargetLnk);
                if (!File.Exists(lnk)) return "(unknown)";
                var shellType = Type.GetTypeFromProgID("WScript.Shell");
                dynamic shell = Activator.CreateInstance(shellType);
                dynamic sc = shell.CreateShortcut(lnk);
                string t = (string)sc.TargetPath;
                return string.IsNullOrEmpty(t) ? "(unknown)" : t;
            }
            catch { return "(unknown)"; }
        }

        /// <summary>
        /// Deletes only the local placeholder folder and its two files. Refuses to act on a
        /// reparse point, so it can never recurse into a real network folder.
        /// </summary>
        public static void SafeRemove(string aliasFolderPath)
        {
            if (string.IsNullOrEmpty(aliasFolderPath) || !Directory.Exists(aliasFolderPath)) return;

            if (SymlinkService.IsSymlink(aliasFolderPath))
                throw new InvalidOperationException(
                    "Refusing to delete \"" + aliasFolderPath + "\": it is a link/reparse point.");

            // A folder shortcut is a plain local folder containing only desktop.ini + target.lnk.
            // Recursive delete therefore removes only those local files - never the target.
            new DirectoryInfo(aliasFolderPath).Attributes = FileAttributes.Directory; // drop System/ReadOnly
            string lnk = Path.Combine(aliasFolderPath, TargetLnk);
            string ini = Path.Combine(aliasFolderPath, DesktopIni);
            ClearAttrs(lnk);
            ClearAttrs(ini);
            try { if (File.Exists(lnk)) File.Delete(lnk); } catch { }
            try { if (File.Exists(ini)) File.Delete(ini); } catch { }
            // Non-recursive on purpose: if anything unexpected is still inside, this throws
            // instead of wiping it. We only ever delete our own two placeholder files.
            Directory.Delete(aliasFolderPath, false);
        }

        private static void ClearAttrs(string path)
        {
            try { if (File.Exists(path)) File.SetAttributes(path, FileAttributes.Normal); }
            catch { }
        }
    }
}
