using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;

namespace ProjectPinner
{
    /// <summary>
    /// Installs/removes the Windows 11 top-level right-click menu, which is delivered by a
    /// signed sparse MSIX package + a native shell-extension DLL. Both are embedded in this
    /// exe (in release/CI builds) and dropped to %LOCALAPPDATA%\ProjectPinner at install
    /// time; the package is then registered per-user (NO admin) pointing at that external
    /// location via Add-AppxPackage -ExternalLocation.
    ///
    /// All AppX work is done through a temp .ps1 run by Windows PowerShell (which has the
    /// Appx module), so there is no managed WinRT dependency.
    /// </summary>
    internal static class ContextMenuInstaller
    {
        private const string DllResourceSuffix = "ProjectPinner.ShellExt.dll";
        private const string MsixResourceSuffix = "ProjectPinner.msix";

        /// <summary>True when this build actually carries the embedded DLL + MSIX (CI build).
        /// A local Linux build has neither, so the modern menu is simply skipped there.</summary>
        public static bool HasEmbeddedPackage =>
            FindResource(DllResourceSuffix) != null && FindResource(MsixResourceSuffix) != null;

        /// <summary>
        /// Fast, allocation-free proxy for "the Windows 11 modern menu is installed", used to drive
        /// the in-app toggle's On/Off label without spawning PowerShell on every UI refresh. The
        /// installer drops the shell-ext DLL and registers the package together, and <see cref="Remove"/>
        /// deletes the DLL, so the DLL's presence tracks the modern menu's installed state closely
        /// enough for a label. Authoritative package state is only ever needed at install/remove time.
        /// </summary>
        public static bool IsLikelyInstalled =>
            HasEmbeddedPackage && File.Exists(AppPaths.ShellExtDllPath);

        /// <summary>
        /// Drops the shell-ext DLL beside the installed exe and (re)registers the MSIX so the
        /// top-level menu works. Idempotent; also performs upgrades (kills the loaded handler
        /// first, removes the old package, adds the new one). Returns true if the package
        /// registered successfully.
        /// </summary>
        public static bool EnsureInstalled()
        {
            if (!HasEmbeddedPackage) return false;
            AppPaths.EnsureDir(AppPaths.InstallRoot);

            // Release any loaded copy of the DLL so it can be overwritten (covers updates).
            KillSurrogate();

            // Drop the DLL beside the exe (rename a locked stale copy aside, then write).
            WriteResourceRobust(DllResourceSuffix, AppPaths.ShellExtDllPath);

            // Never register the package against a missing DLL — the menu would silently
            // fail to load. Bail (keeping any classic-verb fallback) if the drop failed.
            if (!File.Exists(AppPaths.ShellExtDllPath)) return false;

            // Stage the MSIX to temp and register it pointing at the install dir.
            string msix = Path.Combine(Path.GetTempPath(), "pp_" + Guid.NewGuid().ToString("N") + ".msix");
            try
            {
                WriteResource(MsixResourceSuffix, msix);
                return RegisterPackage(msix, AppPaths.InstallRoot);
            }
            finally { try { File.Delete(msix); } catch { } }
        }

        /// <summary>Removes the MSIX package and the shell-ext DLL. Best-effort, never throws.</summary>
        public static void Remove()
        {
            KillSurrogate();
            RunScript(
                "$ErrorActionPreference='SilentlyContinue';" +
                "Get-AppxPackage -Name '" + AppPaths.ShellExtPackageWildcard + "' | Remove-AppxPackage");
            try { if (File.Exists(AppPaths.ShellExtDllPath)) File.Delete(AppPaths.ShellExtDllPath); } catch { }
            try
            {
                string aside = AppPaths.ShellExtDllPath + ".old";
                if (File.Exists(aside)) File.Delete(aside);
            }
            catch { }
        }

        // ---- internals --------------------------------------------------------

        private static bool RegisterPackage(string msixPath, string externalLocation)
        {
            string script =
                "$ErrorActionPreference='Stop';" +
                "Get-AppxPackage -Name '" + AppPaths.ShellExtPackageWildcard + "' -EA SilentlyContinue | " +
                "Remove-AppxPackage -EA SilentlyContinue;" +
                "Add-AppxPackage -Path '" + PsQuote(msixPath) + "'" +
                " -ExternalLocation '" + PsQuote(externalLocation) + "' -ForceApplicationShutdown";
            return RunScript(script);
        }

        private static void KillSurrogate()
        {
            RunScript(
                "Get-CimInstance Win32_Process -Filter \"name='dllhost.exe'\" -EA SilentlyContinue | " +
                "Where-Object { $_.CommandLine -match '" + AppPaths.ShellExtClsidFragment + "' } | " +
                "ForEach-Object { Stop-Process -Id $_.ProcessId -Force -EA SilentlyContinue }");
        }

        /// <summary>Escapes a path for use inside a single-quoted PowerShell string.</summary>
        private static string PsQuote(string s) => (s ?? "").Replace("'", "''");

        /// <summary>Writes the script to a temp .ps1 and runs it hidden. Returns exit-code==0.</summary>
        private static bool RunScript(string script)
        {
            string tmp = Path.Combine(Path.GetTempPath(), "pp_" + Guid.NewGuid().ToString("N") + ".ps1");
            try
            {
                File.WriteAllText(tmp, script);
                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = "-NoProfile -NonInteractive -ExecutionPolicy Bypass -File \"" + tmp + "\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                };
                using (var p = Process.Start(psi))
                {
                    if (p == null) return false;
                    if (!p.WaitForExit(120000)) { try { p.Kill(); } catch { } return false; }
                    return p.ExitCode == 0;
                }
            }
            catch { return false; }
            finally { try { File.Delete(tmp); } catch { } }
        }

        private static string FindResource(string suffix)
        {
            try
            {
                return Assembly.GetExecutingAssembly().GetManifestResourceNames()
                    .FirstOrDefault(n => n.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
            }
            catch { return null; }
        }

        private static void WriteResource(string suffix, string destPath)
        {
            string name = FindResource(suffix)
                ?? throw new FileNotFoundException("Embedded resource not found: " + suffix);
            using (var s = Assembly.GetExecutingAssembly().GetManifestResourceStream(name))
            using (var f = File.Create(destPath))
                s.CopyTo(f);
        }

        private static void WriteResourceRobust(string suffix, string destPath)
        {
            try { WriteResource(suffix, destPath); return; }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }

            // Stale copy locked by the surrogate: rename aside, then write the new one.
            string aside = destPath + ".old";
            try { if (File.Exists(aside)) File.Delete(aside); } catch { }
            bool movedAside = false;
            try { if (File.Exists(destPath)) { File.Move(destPath, aside); movedAside = true; } } catch { }

            try { WriteResource(suffix, destPath); }
            catch
            {
                // Write failed after moving the old DLL aside — restore it so we never leave
                // the package pointing at a missing file.
                if (movedAside && !File.Exists(destPath))
                {
                    try { File.Move(aside, destPath); } catch { }
                }
            }
        }
    }
}
