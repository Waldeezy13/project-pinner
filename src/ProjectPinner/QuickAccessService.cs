using System;
using System.IO;

namespace ProjectPinner
{
    /// <summary>
    /// Pins/unpins folders to File Explorer's Quick Access using the shell verbs
    /// "pintohome" / "unpinfromhome". Driven via the Shell.Application COM object so it
    /// works for a normal (non-admin) user. All methods are best-effort and never throw.
    /// </summary>
    internal static class QuickAccessService
    {
        // Quick Access virtual folder.
        private const string QuickAccessShell = "shell:::{679f85cb-0220-4080-b29b-5540cc05aab6}";

        private static dynamic CreateShell()
        {
            var t = Type.GetTypeFromProgID("Shell.Application");
            return t == null ? null : Activator.CreateInstance(t);
        }

        public static bool Pin(string folderPath)
        {
            try
            {
                dynamic shell = CreateShell();
                if (shell == null) return false;

                // Pin the child item as seen IN ITS PARENT (so its own alias name + identity
                // are used), mirroring a manual right-click > Pin to Quick Access. Pinning the
                // resolved Self can lose the alias name.
                string trimmed = folderPath.TrimEnd('\\');
                string parent = Path.GetDirectoryName(trimmed);
                string leaf = Path.GetFileName(trimmed);

                dynamic item = null;
                if (!string.IsNullOrEmpty(parent) && !string.IsNullOrEmpty(leaf))
                {
                    dynamic ns = shell.Namespace(parent);
                    if (ns != null) item = ns.ParseName(leaf);
                }
                if (item == null)
                {
                    dynamic ns = shell.Namespace(folderPath); // fallback
                    item = ns?.Self;
                }
                if (item == null) return false;

                item.InvokeVerb("pintohome");
                return IsPinned(folderPath);
            }
            catch
            {
                return false;
            }
        }

        public static bool Unpin(string folderPath)
        {
            try
            {
                dynamic shell = CreateShell();
                if (shell == null) return false;
                dynamic qa = shell.Namespace(QuickAccessShell);
                if (qa == null) return false;
                dynamic items = qa.Items();
                foreach (dynamic item in items)
                {
                    if (Matches(item, folderPath))
                    {
                        item.InvokeVerb("unpinfromhome");
                        return true;
                    }
                }
                return true; // nothing to unpin == success
            }
            catch
            {
                return false;
            }
        }

        public static bool IsPinned(string folderPath)
        {
            try
            {
                dynamic shell = CreateShell();
                if (shell == null) return false;
                dynamic qa = shell.Namespace(QuickAccessShell);
                if (qa == null) return false;
                dynamic items = qa.Items();
                foreach (dynamic item in items)
                {
                    if (Matches(item, folderPath)) return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        private static string SafePath(dynamic item)
        {
            try { return (string)item.Path; } catch { return null; }
        }

        /// <summary>A pinned item matches only when its filesystem path equals the folder.
        /// (Name-only matching was dropped — it risked unpinning an unrelated Quick Access
        /// entry that merely shared a display name.)</summary>
        private static bool Matches(dynamic item, string folderPath)
        {
            return PathEquals(SafePath(item), folderPath);
        }

        private static bool PathEquals(string a, string b)
        {
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return false;
            return string.Equals(a.TrimEnd('\\'), b.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase);
        }
    }
}
