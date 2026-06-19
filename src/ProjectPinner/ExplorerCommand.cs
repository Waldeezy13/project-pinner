using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Threading;

namespace ProjectPinner
{
    // ---- Stable CLSID for the context-menu command (matches Package.appxmanifest) ------
    internal static class ExplorerCommandClsid
    {
        public const string Value = "E4B8A120-71F3-4E2C-9AC5-3D8B7F2E1047";
    }

    // ---- COM interfaces (vtables must match the Windows SDK C++ layout exactly) --------

    [ComImport, Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IShellItem
    {
        [PreserveSig] int BindToHandler(IntPtr pbc, ref Guid bhid, ref Guid riid, out IntPtr ppv);
        [PreserveSig] int GetParent(out IShellItem ppsi);
        [PreserveSig] int GetDisplayName(uint sigdnName, [MarshalAs(UnmanagedType.LPWStr)] out string ppszName);
        [PreserveSig] int GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);
        [PreserveSig] int Compare(IShellItem psi, uint hint, out int piOrder);
    }

    [ComImport, Guid("B63EA76D-1F85-456F-A19C-48159EFA858B"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IShellItemArray
    {
        [PreserveSig] int BindToHandler(IntPtr pbc, ref Guid bhid, ref Guid riid, out IntPtr ppv);
        [PreserveSig] int GetPropertyStore(int flags, ref Guid riid, out IntPtr ppv);
        [PreserveSig] int GetPropertyDescriptionList(IntPtr keyType, ref Guid riid, out IntPtr ppv);
        [PreserveSig] int GetAttributes(int AttribFlags, uint sfgaoMask, out uint psfgaoAttribs);
        [PreserveSig] int GetCount(out uint pdwNumItems);
        [PreserveSig] int GetItemAt(uint dwIndex, out IShellItem ppsi);
        [PreserveSig] int EnumItems(out IntPtr ppenumShellItems);
    }

    [ComImport, Guid("A3B3BCAF-6F50-4ED7-AFED-8BCdC57C4D33"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IEnumExplorerCommand { }

    internal enum ExplorerCommandState : uint
    {
        Enabled    = 0x000,
        Disabled   = 0x001,
        Hidden     = 0x002,
    }

    internal enum ExplorerCommandFlags : uint
    {
        Default         = 0x000,
        HasSubcommands  = 0x001,
        IsSeparator     = 0x008,
    }

    [ComImport, Guid("A08CE4D0-FA25-44AB-B57C-C7240467C4AD"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IExplorerCommand
    {
        [PreserveSig] int GetTitle(IShellItemArray psiItemArray,
            [MarshalAs(UnmanagedType.LPWStr)] out string ppszName);
        [PreserveSig] int GetIcon(IShellItemArray psiItemArray,
            [MarshalAs(UnmanagedType.LPWStr)] out string ppszIcon);
        [PreserveSig] int GetToolTip(IShellItemArray psiItemArray,
            [MarshalAs(UnmanagedType.LPWStr)] out string ppszInfotip);
        [PreserveSig] int GetCanonicalName(out Guid pguidCommandName);
        [PreserveSig] int GetState(IShellItemArray psiItemArray, bool fOkToBeSlow,
            out ExplorerCommandState pCmdState);
        [PreserveSig] int Invoke(IShellItemArray psiItemArray, IBindCtx pbc);
        [PreserveSig] int GetFlags(out ExplorerCommandFlags pFlags);
        [PreserveSig] int EnumSubCommands(out IEnumExplorerCommand ppEnum);
    }

    // ---- IExplorerCommand implementation -------------------------------------------

    [ComVisible(true)]
    [Guid(ExplorerCommandClsid.Value)]
    [ClassInterface(ClassInterfaceType.None)]
    internal sealed class ProjectPinnerCommand : IExplorerCommand
    {
        // SIGDN_FILESYSPATH — returns the Win32 filesystem path of the shell item.
        private const uint SIGDN_FILESYSPATH = 0x80058000;
        private const int S_OK = 0;
        private const int E_NOTIMPL = unchecked((int)0x80004001);

        public int GetTitle(IShellItemArray psiItemArray, out string ppszName)
        {
            ppszName = "Pin with alias to Quick Access";
            return S_OK;
        }

        public int GetIcon(IShellItemArray psiItemArray, out string ppszIcon)
        {
            // Icon-location string: path,index — shell calls ExtractIcon on it.
            ppszIcon = AppPaths.IconPath + ",0";
            return S_OK;
        }

        public int GetToolTip(IShellItemArray psiItemArray, out string ppszInfotip)
        {
            ppszInfotip = null;
            return E_NOTIMPL;
        }

        public int GetCanonicalName(out Guid pguidCommandName)
        {
            pguidCommandName = new Guid(ExplorerCommandClsid.Value);
            return S_OK;
        }

        public int GetState(IShellItemArray psiItemArray, bool fOkToBeSlow,
            out ExplorerCommandState pCmdState)
        {
            pCmdState = ExplorerCommandState.Enabled;
            return S_OK;
        }

        public int Invoke(IShellItemArray psiItemArray, IBindCtx pbc)
        {
            try
            {
                if (psiItemArray == null) return E_NOTIMPL;
                psiItemArray.GetItemAt(0, out IShellItem item);
                if (item == null) return E_NOTIMPL;
                item.GetDisplayName(SIGDN_FILESYSPATH, out string path);
                if (string.IsNullOrEmpty(path)) return E_NOTIMPL;

                // Run the pin dialog on a fresh STA thread so WPF is happy.
                // We start it, but don't wait — returning quickly lets the shell
                // dismiss its own UI immediately.
                var t = new Thread(() =>
                {
                    try
                    {
                        var cfg = Config.Load();
                        ProjectsHubService.HubFolderName =
                            string.IsNullOrWhiteSpace(cfg.HubFolderName) ? "Projects" : cfg.HubFolderName;
                        var app = new System.Windows.Application
                            { ShutdownMode = System.Windows.ShutdownMode.OnMainWindowClose };
                        app.Run(new QuickPinWindow(path, cfg));
                    }
                    catch { }
                });
                t.SetApartmentState(ApartmentState.STA);
                t.IsBackground = false; // keep process alive until the window closes
                t.Start();
            }
            catch { }
            return S_OK;
        }

        public int GetFlags(out ExplorerCommandFlags pFlags)
        {
            pFlags = ExplorerCommandFlags.Default;
            return S_OK;
        }

        public int EnumSubCommands(out IEnumExplorerCommand ppEnum)
        {
            ppEnum = null;
            return E_NOTIMPL;
        }
    }

    // ---- IClassFactory for COM EXE server activation --------------------------------

    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.None)]
    internal sealed class ProjectPinnerCommandFactory : IClassFactory
    {
        private const int S_OK = 0;
        private const int CLASS_E_NOAGGREGATION = unchecked((int)0x80040110);
        private const int E_NOINTERFACE = unchecked((int)0x80004002);

        public int CreateInstance(object pUnkOuter, ref Guid riid, out object ppvObject)
        {
            ppvObject = null;
            if (pUnkOuter != null) return CLASS_E_NOAGGREGATION;
            var cmd = new ProjectPinnerCommand();
            ppvObject = cmd;
            return S_OK;
        }

        public int LockServer(bool fLock) => S_OK;
    }

    [ComImport, Guid("00000001-0000-0000-C000-000000000046"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IClassFactory
    {
        [PreserveSig] int CreateInstance(object pUnkOuter, ref Guid riid, out object ppvObject);
        [PreserveSig] int LockServer(bool fLock);
    }

    // ---- COM EXE server helpers ------------------------------------------------------

    internal static class ComServer
    {
        private const uint CLSCTX_LOCAL_SERVER = 0x4;
        private const uint REGCLS_MULTIPLEUSE  = 1;
        private const uint REGCLS_SUSPENDED    = 4;

        [DllImport("ole32.dll")]
        private static extern int CoRegisterClassObject(
            ref Guid rclsid, [MarshalAs(UnmanagedType.IUnknown)] object pUnk,
            uint dwClsContext, uint flags, out uint lpdwRegister);

        [DllImport("ole32.dll")]
        private static extern int CoRevokeClassObject(uint dwRegister);

        [DllImport("ole32.dll")]
        private static extern int CoResumeClassObjects();

        /// <summary>
        /// Enters COM EXE-server mode: registers our class factory, pumps messages
        /// (via WPF Application.Run with no window), then unregisters on exit.
        /// Called when the process is launched by COM with the -Embedding flag.
        /// </summary>
        public static int Run()
        {
            var clsid = new Guid(ExplorerCommandClsid.Value);
            uint cookie = 0;
            try
            {
                int hr = CoRegisterClassObject(ref clsid, new ProjectPinnerCommandFactory(),
                    CLSCTX_LOCAL_SERVER, REGCLS_MULTIPLEUSE | REGCLS_SUSPENDED, out cookie);
                if (hr < 0) return hr;

                CoResumeClassObjects();

                // WPF Application.Run() pumps the STA message loop, which COM needs to
                // dispatch incoming calls on the main STA thread.
                var app = new System.Windows.Application
                    { ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown };
                app.Run(); // exits when Application.Current.Shutdown() is called
            }
            finally
            {
                if (cookie != 0) CoRevokeClassObject(cookie);
            }
            return 0;
        }
    }
}
