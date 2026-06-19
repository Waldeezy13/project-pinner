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

        /// <summary>
        /// Removes the per-user shell registrations: the classic right-click verb, the Start
        /// Menu shortcut, and the Quick Access pin. (File deletion is handled separately.)
        /// </summary>
        public static void Uninstall()
        {
            try { ShellMenuService.Unregister(); } catch { }
            try { QuickAccessService.Unpin(ProjectsHubService.HubDir); } catch { }
            try { if (File.Exists(AppPaths.StartMenuShortcut)) File.Delete(AppPaths.StartMenuShortcut); } catch { }
        }

        /// <summary>
        /// Full removal: the MSIX context-menu package + shell-ext DLL, the classic verb, the
        /// Start Menu shortcut, and the Quick Access pin. The installed files themselves are
        /// removed afterwards via <see cref="ScheduleInstallDirDeletion"/> (this exe may be
        /// running from the install dir, so the directory is deleted once we exit).
        /// </summary>
        public static void FullUninstall()
        {
            try { ContextMenuInstaller.Remove(); } catch { }
            Uninstall();
        }

        /// <summary>
        /// Schedules deletion of the whole install dir once this process exits. A detached
        /// cmd retries for ~20s so it succeeds even after the user dismisses a dialog (the
        /// running exe is only unlocked once we exit). Removes the exe, DLL, icon, settings,
        /// logs, and the local shortcuts (hub) folder — but never anything on the network.
        /// </summary>
        public static void ScheduleInstallDirDeletion()
        {
            try
            {
                string dir = AppPaths.InstallRoot;
                // Safety: only ever delete a fully-rooted path that is our own install folder.
                // Guards against a misconfigured %LOCALAPPDATA% yielding a relative/unexpected
                // path that rmdir /s /q would otherwise walk.
                if (string.IsNullOrEmpty(dir) || !Path.IsPathRooted(dir) ||
                    !string.Equals(Path.GetFileName(dir.TrimEnd('\\')), "ProjectPinner",
                                   StringComparison.OrdinalIgnoreCase))
                    return;

                // Retry loop: wait, try rmdir, stop once the dir is gone (covers the exe still
                // being locked while this process is finishing).
                string cmd =
                    "/c for /l %i in (1,1,20) do ( ping 127.0.0.1 -n 2 >nul & " +
                    "rmdir /s /q \"" + dir + "\" 2>nul & if not exist \"" + dir + "\" exit )";
                var psi = new ProcessStartInfo("cmd.exe", cmd)
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Hidden,
                };
                Process.Start(psi);
            }
            catch { }
        }

        /// <summary>Best-effort cleanup of renamed-aside old binaries from a prior update
        /// (the exe and the shell-ext DLL, either of which may have been locked at update time).</summary>
        public static void CleanupOldExe()
        {
            foreach (var aside in new[] { AppPaths.InstalledExePath + ".old", AppPaths.ShellExtDllPath + ".old" })
            {
                try { if (File.Exists(aside)) File.Delete(aside); }
                catch { /* still in use; will clear on a later run */ }
            }
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
