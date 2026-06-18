using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ProjectPinner
{
    /// <summary>A renamed project: a local symlink whose name is friendly, pointing at the real folder.</summary>
    internal sealed class ProjectLink
    {
        public string DisplayName { get; set; }   // e.g. "Acme Tower - 1234567890"
        public string LinkPath { get; set; }       // %LOCALAPPDATA%\ProjectPinner\Links\Acme Tower - 1234567890
        public string Target { get; set; }         // \\server\share\1234567890
        public bool Pinned { get; set; }
    }

    internal static class ProjectService
    {
        private static readonly char[] InvalidNameChars = Path.GetInvalidFileNameChars();

        // Names Windows reserves for legacy DOS devices: a folder named exactly one of
        // these (optionally with an extension) cannot be created and CreateSymbolicLinkW
        // fails with a non-obvious error.
        private static readonly HashSet<string> ReservedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "CON", "PRN", "AUX", "NUL",
            "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
            "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
        };

        /// <summary>Strips characters that are illegal in a folder name and collapses whitespace.</summary>
        public static string SanitizeName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "";
            var cleaned = new string(name.Where(c => Array.IndexOf(InvalidNameChars, c) < 0).ToArray());
            cleaned = string.Join(" ", cleaned.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
            var result = cleaned.Trim().Trim('.'); // trailing dots/spaces are illegal on Windows

            // Windows treats a name as reserved only when the base (before the first dot)
            // equals a device name, so "CON - 12345" is fine but a bare "CON" is not.
            if (result.Length > 0 && ReservedNames.Contains(result.Split('.')[0]))
                result = "_" + result;

            return result;
        }

        /// <summary>The project number is the leaf folder name of the target path.</summary>
        public static string DeriveProjectNumber(string targetPath)
        {
            if (string.IsNullOrEmpty(targetPath)) return "";
            string trimmed = targetPath.TrimEnd('\\', '/');
            int slash = trimmed.LastIndexOfAny(new[] { '\\', '/' });
            return slash >= 0 ? trimmed.Substring(slash + 1) : trimmed;
        }

        public static string BuildDisplayName(string friendlyName, string projectNumber, string separator)
        {
            friendlyName = SanitizeName(friendlyName);
            projectNumber = SanitizeName(projectNumber);
            if (string.IsNullOrEmpty(friendlyName)) return projectNumber;
            if (string.IsNullOrEmpty(projectNumber)) return friendlyName;
            return friendlyName + separator + projectNumber;
        }

        /// <summary>
        /// Creates the renamed link for a project and optionally pins it. Returns the new
        /// ProjectLink. Throws on validation problems or symlink-creation failure.
        /// </summary>
        public static ProjectLink CreateProject(string friendlyName, string targetPath, Config cfg)
        {
            if (string.IsNullOrWhiteSpace(targetPath))
                throw new ArgumentException("Pick the project folder first.");
            if (string.IsNullOrWhiteSpace(friendlyName))
                throw new ArgumentException("Enter a friendly name.");

            targetPath = targetPath.Trim().TrimEnd('\\', '/');
            if (!Directory.Exists(targetPath))
                throw new DirectoryNotFoundException(
                    "That folder can't be reached:\n" + targetPath +
                    "\n\nCheck you're connected to the network and the path is correct.");

            if (cfg.ResolveUncForMappedDrives)
                targetPath = SymlinkService.ResolveToUnc(targetPath);

            string number = DeriveProjectNumber(targetPath);
            string display = BuildDisplayName(friendlyName, number, cfg.Separator);
            if (string.IsNullOrEmpty(display))
                throw new ArgumentException("The name is empty after removing invalid characters.");

            // Create the alias as an ordinary shortcut (.lnk) inside the single "Projects"
            // hub folder. A .lnk is a pure pointer - the network folder is never touched.
            string linkPath = ProjectsHubService.CreateProjectShortcut(display, targetPath);

            var link = new ProjectLink
            {
                DisplayName = display,
                LinkPath = linkPath,
                Target = targetPath,
                Pinned = false
            };

            // Pin the hub once (not the individual project). Idempotent.
            if (cfg.AutoPin && !ProjectsHubService.IsHubPinned())
                ProjectsHubService.PinHub();
            link.Pinned = ProjectsHubService.IsHubPinned();

            return link;
        }

        /// <summary>
        /// Enumerates the Links folder. Each subdirectory that is a reparse point is one
        /// project. Self-healing: reflects whatever is actually on disk.
        /// </summary>
        public static List<ProjectLink> ListProjects()
        {
            var result = new List<ProjectLink>();
            try
            {
                string hub = ProjectsHubService.HubDir;
                if (!Directory.Exists(hub)) return result;
                foreach (var lnk in Directory.GetFiles(hub, "*.lnk"))
                {
                    result.Add(new ProjectLink
                    {
                        DisplayName = Path.GetFileNameWithoutExtension(lnk),
                        LinkPath = lnk,
                        Target = ProjectsHubService.ReadTarget(lnk),
                        Pinned = false // individual items aren't pinned; the hub is
                    });
                }
            }
            catch
            {
                // Return whatever we managed to enumerate.
            }
            return result.OrderBy(p => p.DisplayName, StringComparer.OrdinalIgnoreCase).ToList();
        }

        /// <summary>Removes a project by deleting ONLY its shortcut file. The network folder
        /// is never touched (a .lnk is a pure pointer).</summary>
        public static void RemoveProject(ProjectLink link)
        {
            if (link == null) return;
            ProjectsHubService.RemoveShortcut(link.LinkPath);
        }

        /// <summary>The hub/.lnk model works for any user, so the app is always ready.</summary>
        public static bool CanCreateLinks() => true;

        /// <summary>
        /// One-time migration: clears wrongly-named pins created by earlier builds (symlinks or
        /// folder shortcuts under the old Links dir). Unpins each from Quick Access and removes
        /// only the LOCAL placeholder - never the network target. Returns how many were cleared.
        /// </summary>
        public static int CleanupLegacyPins()
        {
            int removed = 0;
            try
            {
                if (!Directory.Exists(AppPaths.LinksDir)) return 0;
                foreach (var dir in Directory.GetDirectories(AppPaths.LinksDir))
                {
                    try
                    {
                        bool isSymlink = SymlinkService.IsSymlink(dir);
                        bool isFolderShortcut = !isSymlink && FolderShortcutService.IsFolderShortcut(dir);
                        if (!isSymlink && !isFolderShortcut) continue;

                        // Old pins resolved to the network target's path in Quick Access, so
                        // unpin by both the local placeholder path and the resolved target.
                        QuickAccessService.Unpin(dir);
                        string target = isSymlink ? SymlinkService.GetLinkTarget(dir) : FolderShortcutService.ReadTarget(dir);
                        if (!string.IsNullOrEmpty(target) && target != "(unknown)")
                            QuickAccessService.Unpin(target);

                        if (isSymlink) SymlinkService.SafeRemoveLink(dir);
                        else FolderShortcutService.SafeRemove(dir);
                        removed++;
                    }
                    catch { /* skip anything we can't safely handle */ }
                }
            }
            catch { }
            return removed;
        }
    }
}
