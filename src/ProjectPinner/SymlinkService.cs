using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace ProjectPinner
{
    /// <summary>
    /// Read-only helpers for legacy directory symlinks (created by earlier builds) plus
    /// mapped-drive -> UNC resolution. The current app no longer CREATES symlinks - it uses
    /// the hub/.lnk model - so only the inspection/removal/UNC helpers remain.
    /// </summary>
    internal static class SymlinkService
    {
        /// <summary>True when the path is a reparse point (symlink/junction), not a real folder.</summary>
        public static bool IsSymlink(string path)
        {
            try
            {
                if (!Directory.Exists(path) && !File.Exists(path)) return false;
                var attr = File.GetAttributes(path);
                return (attr & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Removes ONLY the link. Refuses to act on anything that isn't a reparse point, and
        /// uses RemoveDirectory (which deletes the link itself) - never a recursive delete that
        /// could walk into and destroy the real network folder behind the link.
        /// </summary>
        public static void SafeRemoveLink(string linkPath)
        {
            if (string.IsNullOrEmpty(linkPath)) return;
            if (!Directory.Exists(linkPath) && !File.Exists(linkPath)) return;

            if (!IsSymlink(linkPath))
                throw new InvalidOperationException(
                    "Refusing to delete \"" + linkPath + "\": it is a real folder, not a link.");

            if (!NativeMethods.RemoveDirectory(linkPath))
            {
                int err = Marshal.GetLastWin32Error();
                throw new Win32Exception(err, "Could not remove the link (Windows error " + err + ").");
            }
        }

        /// <summary>Reads the target a symlink points at, if available.</summary>
        public static string GetLinkTarget(string linkPath)
        {
            try { return ReparseTargetReader.Read(linkPath) ?? "(unknown)"; }
            catch { return "(unknown)"; }
        }

        /// <summary>
        /// Resolves a mapped network drive (Z:\path) to its UNC form (\\server\share\path).
        /// Returns the original path unchanged for local paths, UNC paths, or on failure.
        /// </summary>
        public static string ResolveToUnc(string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path)) return path;
                if (path.StartsWith(@"\\")) return path; // already UNC
                if (path.Length < 2 || path[1] != ':') return path;

                string drive = path.Substring(0, 2); // "Z:"
                var sb = new StringBuilder(512);
                int len = sb.Capacity;
                int rc = NativeMethods.WNetGetConnectionW(drive, sb, ref len);
                if (rc == NativeMethods.ERROR_MORE_DATA)
                {
                    sb = new StringBuilder(len);
                    rc = NativeMethods.WNetGetConnectionW(drive, sb, ref len);
                }
                if (rc == NativeMethods.NO_ERROR && sb.Length > 0)
                {
                    string remainder = path.Substring(2); // "\path"
                    return sb.ToString().TrimEnd('\\') + remainder;
                }
            }
            catch
            {
                // fall through to original path
            }
            return path; // local drive or not a network mapping
        }
    }
}
