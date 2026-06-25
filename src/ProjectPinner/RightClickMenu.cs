using System;

namespace ProjectPinner
{
    /// <summary>
    /// One place that decides WHICH right-click-menu mechanism is in play and keeps the in-app
    /// toggle honest about it. There are two delivery mechanisms:
    ///   - Windows 11 (and any build that embeds the package): the MODERN top-level menu, via the
    ///     signed sparse MSIX + native shell-ext DLL (ContextMenuInstaller).
    ///   - Windows 10 / a build without the embedded package: the CLASSIC HKCU verb (ShellMenuService),
    ///     which appears in the regular context menu (under "Show more options" on Win11).
    ///
    /// The previous code drove the toggle purely off the classic verb, so on Windows 11 — where the
    /// install flow registers the MSIX menu and then REMOVES the classic verb — the button read
    /// "Off" while the menu was actually on, and turning it "on" re-created a duplicate classic entry.
    /// This type makes enable/disable/state all agree on the mechanism actually in use.
    /// </summary>
    internal static class RightClickMenu
    {
        /// <summary>True when the right-click menu is active by EITHER mechanism.</summary>
        public static bool IsEnabled()
        {
            try { if (ContextMenuInstaller.IsLikelyInstalled) return true; } catch { }
            try { return ShellMenuService.IsRegistered(); } catch { return false; }
        }

        /// <summary>
        /// Installs the menu using the best available mechanism: the modern MSIX menu when this build
        /// embeds the package and registration succeeds (and the classic verb is then removed so the
        /// entry isn't duplicated under "Show more options"); otherwise the classic verb as a fallback.
        /// Returns a short status line describing what happened.
        /// </summary>
        public static string Enable()
        {
            Installer.InstallFilesForCurrentUser(); // ensure the installed exe + support files exist

            bool modern = false;
            if (ContextMenuInstaller.HasEmbeddedPackage)
            {
                try { modern = ContextMenuInstaller.EnsureInstalled(); } catch { }
            }

            if (modern)
            {
                // The modern menu is live; drop any classic verb so it isn't shown twice.
                try { ShellMenuService.Unregister(); } catch { }
                return "Added the Windows 11 right-click menu \"Pin with alias to Quick Access\".";
            }

            // Windows 10, or the modern package wasn't available / failed: use the classic verb.
            ShellMenuService.Register();
            return "Added \"Pin with alias to Quick Access\" to the folder right-click menu.";
        }

        /// <summary>Removes the menu via BOTH mechanisms, so it is genuinely gone whichever was active.</summary>
        public static string Disable()
        {
            try { ContextMenuInstaller.Remove(); } catch { }
            try { ShellMenuService.Unregister(); } catch { }
            return "Removed the \"Pin with alias\" right-click menu entry.";
        }
    }
}
