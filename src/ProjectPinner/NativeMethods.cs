using System;
using System.Runtime.InteropServices;
using System.Text;

namespace ProjectPinner
{
    /// <summary>All raw P/Invoke surface lives here, isolated from app logic.</summary>
    internal static class NativeMethods
    {
        // ---- Remove a directory reparse point (kernel32) ----------------------
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool RemoveDirectory(string lpPathName);

        // ---- Mapped drive -> UNC (mpr) ----------------------------------------
        public const int NO_ERROR = 0;
        public const int ERROR_MORE_DATA = 234;

        [DllImport("mpr.dll", CharSet = CharSet.Unicode)]
        public static extern int WNetGetConnectionW(string lpLocalName, StringBuilder lpRemoteName, ref int lpnLength);

        // ---- Dark titlebar (dwmapi) -------------------------------------------
        public const int DWMWA_USE_IMMERSIVE_DARK_MODE_OLD = 19;
        public const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        [DllImport("dwmapi.dll", PreserveSig = true)]
        public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        // ---- MSIX package identity detection (kernel32) ----------------------
        // Returns APPMODEL_ERROR_NO_PACKAGE (15700) when the process has no identity.
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        public static extern int GetCurrentPackageName(ref uint packageNameLength,
            System.Text.StringBuilder packageName);

        // ---- Shell change notifications (shell32) -----------------------------
        // Tells Explorer a folder's appearance changed (e.g. desktop.ini icon update).
        public const int SHCNE_UPDATEDIR = 0x00001000;
        public const int SHCNF_PATH      = 0x0001;
        public const int SHCNF_FLUSH     = 0x1000;

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        public static extern void SHChangeNotify(int wEventId, int uFlags, string dwItem1, string dwItem2);
    }
}
