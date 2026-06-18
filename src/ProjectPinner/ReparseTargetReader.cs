using System;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace ProjectPinner
{
    /// <summary>
    /// Reads the target path out of a directory symlink / junction reparse point.
    /// .NET Framework 4.8 has no managed API for this, so we issue
    /// FSCTL_GET_REPARSE_POINT and parse the REPARSE_DATA_BUFFER by hand.
    /// </summary>
    internal static class ReparseTargetReader
    {
        private const uint FSCTL_GET_REPARSE_POINT = 0x000900A8;
        private const uint IO_REPARSE_TAG_SYMLINK = 0xA000000C;
        private const uint IO_REPARSE_TAG_MOUNT_POINT = 0xA0000003;

        private const int FILE_FLAG_OPEN_REPARSE_POINT = 0x00200000;
        private const int FILE_FLAG_BACKUP_SEMANTICS = 0x02000000;
        private const int OPEN_EXISTING = 3;
        private const int FILE_SHARE_READ = 0x1;
        private const int FILE_SHARE_WRITE = 0x2;
        private const int MAX_BUFFER = 16 * 1024;

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern SafeFileHandle CreateFileW(
            string lpFileName, int dwDesiredAccess, int dwShareMode, IntPtr lpSecurityAttributes,
            int dwCreationDisposition, int dwFlagsAndAttributes, IntPtr hTemplateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DeviceIoControl(
            SafeFileHandle hDevice, uint dwIoControlCode, IntPtr lpInBuffer, int nInBufferSize,
            IntPtr lpOutBuffer, int nOutBufferSize, out int lpBytesReturned, IntPtr lpOverlapped);

        public static string Read(string reparsePath)
        {
            SafeFileHandle handle = null;
            IntPtr outBuffer = IntPtr.Zero;
            try
            {
                handle = CreateFileW(reparsePath, 0,
                    FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING,
                    FILE_FLAG_OPEN_REPARSE_POINT | FILE_FLAG_BACKUP_SEMANTICS, IntPtr.Zero);
                if (handle.IsInvalid) return null;

                outBuffer = Marshal.AllocHGlobal(MAX_BUFFER);
                if (!DeviceIoControl(handle, FSCTL_GET_REPARSE_POINT, IntPtr.Zero, 0,
                        outBuffer, MAX_BUFFER, out int bytesReturned, IntPtr.Zero))
                    return null;

                byte[] data = new byte[bytesReturned];
                Marshal.Copy(outBuffer, data, 0, bytesReturned);

                uint tag = BitConverter.ToUInt32(data, 0);
                // Common header: tag(4) reparseDataLength(2) reserved(2) = 8 bytes.
                // SubstituteNameOffset(2) SubstituteNameLength(2) PrintNameOffset(2) PrintNameLength(2)
                int p = 8;
                ushort subOffset = BitConverter.ToUInt16(data, p + 0);
                ushort subLen = BitConverter.ToUInt16(data, p + 2);
                ushort printOffset = BitConverter.ToUInt16(data, p + 4);
                ushort printLen = BitConverter.ToUInt16(data, p + 6);

                int pathBufferStart;
                if (tag == IO_REPARSE_TAG_SYMLINK)
                    pathBufferStart = p + 8 + 4; // symlink has an extra Flags(4) field
                else if (tag == IO_REPARSE_TAG_MOUNT_POINT)
                    pathBufferStart = p + 8;
                else
                    return null;

                string printName = SafeSub(data, pathBufferStart + printOffset, printLen);
                if (!string.IsNullOrEmpty(printName)) return printName;

                string subName = SafeSub(data, pathBufferStart + subOffset, subLen);
                if (subName != null && subName.StartsWith(@"\??\")) subName = subName.Substring(4);
                return subName;
            }
            catch
            {
                return null;
            }
            finally
            {
                if (outBuffer != IntPtr.Zero) Marshal.FreeHGlobal(outBuffer);
                handle?.Dispose();
            }
        }

        private static string SafeSub(byte[] data, int offset, int byteLen)
        {
            if (byteLen <= 0 || offset < 0 || offset + byteLen > data.Length) return null;
            return Encoding.Unicode.GetString(data, offset, byteLen);
        }
    }
}
