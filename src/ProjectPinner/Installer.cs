using System;
using System.Diagnostics;
using System.IO;

namespace ProjectPinner
{
    internal static class Installer
    {
        /// <summary>Full path to the currently running executable.</summary>
        public static string CurrentExePath()
        {
            try { return Process.GetCurrentProcess().MainModule.FileName; }
            catch { return System.Reflection.Assembly.GetEntryAssembly()?.Location; }
        }

        public static bool IsRunningFromInstallDir()
        {
            var cur = CurrentExePath();
            return !string.IsNullOrEmpty(cur) &&
                   string.Equals(Path.GetFullPath(cur), Path.GetFullPath(AppPaths.InstalledExePath),
                                 StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Per-user install (no admin needed): copy the exe into %LOCALAPPDATA%, create a
        /// Start Menu shortcut, and register the right-click menu. Safe to run repeatedly.
        /// </summary>
        public static void InstallFilesForCurrentUser()
        {
            AppPaths.EnsureDir(AppPaths.InstallRoot);
            AppPaths.EnsureDir(AppPaths.LinksDir);

            // Drop a standalone icon file the shell can use for the menu + shortcut.
            try { AppIcon.WriteIcoToDisk(AppPaths.IconPath); } catch { }

            var src = CurrentExePath();
            if (!IsRunningFromInstallDir() && !string.IsNullOrEmpty(src) && File.Exists(src))
                CopyExeRobust(src, AppPaths.InstalledExePath);

            TryCreateStartMenuShortcut();

            // Add the "Pin with alias to Quick Access" right-click entry (HKCU, no admin).
            try { ShellMenuService.Register(); } catch { /* cosmetic; never block install */ }
        }

        /// <summary>
        /// Copies the exe into the install dir, even if an older copy there is currently
        /// running (and therefore locked). Windows allows RENAMING a running image, so we
        /// move the locked copy aside and drop the new one in its place. This keeps the
        /// right-click verb pointing at an up-to-date exe instead of a stale one.
        /// </summary>
        private static void CopyExeRobust(string src, string dest)
        {
            try { File.Copy(src, dest, true); return; }
            catch (IOException) { /* dest likely locked by a running instance */ }
            catch (UnauthorizedAccessException) { }

            try
            {
                string aside = dest + ".old";
                try { if (File.Exists(aside)) File.Delete(aside); } catch { }
                if (File.Exists(dest)) File.Move(dest, aside); // renaming a running exe is allowed
                File.Copy(src, dest, true);
            }
            catch { /* give up; the existing installed copy stays in place */ }
        }

        /// <summary>Best-effort cleanup of a renamed-aside old exe from a prior update.</summary>
        public static void CleanupOldExe()
        {
            try
            {
                string aside = AppPaths.InstalledExePath + ".old";
                if (File.Exists(aside)) File.Delete(aside);
            }
            catch { /* still in use; will clear on a later run */ }
        }

        private static void TryCreateStartMenuShortcut()
        {
            try
            {
                var shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType == null) return;
                dynamic shell = Activator.CreateInstance(shellType);
                dynamic sc = shell.CreateShortcut(AppPaths.StartMenuShortcut);
                sc.TargetPath = AppPaths.InstalledExePath;
                sc.WorkingDirectory = AppPaths.InstallRoot;
                sc.IconLocation = (File.Exists(AppPaths.IconPath) ? AppPaths.IconPath : AppPaths.InstalledExePath) + ",0";
                sc.Description = AppPaths.AppName;
                sc.Save();
            }
            catch
            {
                // A missing Start Menu shortcut is cosmetic; never fail setup for it.
            }
        }
    }
}
