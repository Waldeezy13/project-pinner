#include "PinCommand.h"
#include "DebugLog.h"
#include "Paths.h"

#include <shellapi.h>     // ShellExecuteW
#include <shlwapi.h>      // SHStrDupW
#include <knownfolders.h> // FOLDERID_LocalAppData
#include <strsafe.h>      // StringCchPrintfA
#include <string>
#include <vector>

// Fallbacks in case the SDK headers don't surface these (Win10 RS2+).
#ifndef PROC_THREAD_ATTRIBUTE_DESKTOP_APP_POLICY
#define PROC_THREAD_ATTRIBUTE_DESKTOP_APP_POLICY 0x00020012
#endif
#ifndef PROCESS_CREATION_DESKTOP_APP_BREAKAWAY_ENABLE_PROCESS_TREE
#define PROCESS_CREATION_DESKTOP_APP_BREAKAWAY_ENABLE_PROCESS_TREE 0x01
#endif

#pragma comment(lib, "shlwapi.lib")

namespace ProjectPinner {

// {A3D8F1E2-6C4B-4A91-B7E5-2F9C8D013A64}
const CLSID CLSID_PinCommand = {
    0xA3D8F1E2, 0x6C4B, 0x4A91,
    {0xB7, 0xE5, 0x2F, 0x9C, 0x8D, 0x01, 0x3A, 0x64}};

namespace {

HRESULT WriteStringResult(const std::wstring& s, LPWSTR* out) {
    if (out == nullptr) return E_POINTER;
    return SHStrDupW(s.c_str(), out);
}

// The filesystem path of the first selected item (we only act on one folder).
bool ExtractFirstPath(IShellItemArray* items, std::wstring& outPath) {
    if (items == nullptr) return false;
    DWORD count = 0;
    if (FAILED(items->GetCount(&count)) || count == 0) return false;

    IShellItem* item = nullptr;
    if (FAILED(items->GetItemAt(0, &item)) || item == nullptr) return false;

    LPWSTR psz = nullptr;
    HRESULT hr = item->GetDisplayName(SIGDN_FILESYSPATH, &psz);
    item->Release();
    if (FAILED(hr) || psz == nullptr) return false;

    outPath = psz;
    CoTaskMemFree(psz);
    return true;
}

// Icon-location string for the menu glyph: the app.ico that the installer drops
// next to the DLL. Falls back to the exe's embedded icon, then nothing.
std::wstring IconLocation() {
    const std::wstring dir = ThisModuleDir();
    if (dir.empty()) return L"";
    const std::wstring ico = dir + L"app.ico";
    if (GetFileAttributesW(ico.c_str()) != INVALID_FILE_ATTRIBUTES) {
        return ico;
    }
    return dir + L"ProjectPinner.exe,0";
}

std::string WideToUtf8(const std::wstring& w) {
    if (w.empty()) return "";
    int n = WideCharToMultiByte(CP_UTF8, 0, w.c_str(), static_cast<int>(w.size()),
                                nullptr, 0, nullptr, nullptr);
    std::string s(static_cast<size_t>(n > 0 ? n : 0), '\0');
    if (n > 0) {
        WideCharToMultiByte(CP_UTF8, 0, w.c_str(), static_cast<int>(w.size()),
                            &s[0], n, nullptr, nullptr);
    }
    return s;
}

// The installer always places ProjectPinner.exe at
// %LOCALAPPDATA%\ProjectPinner\ProjectPinner.exe. Resolve it THERE first — that
// real per-user path is reliable even if the packaged DLL's own module path is
// presented to us virtualized. Fall back to the DLL's own directory.
std::wstring InstalledExePath() {
    PWSTR local = nullptr;
    if (SUCCEEDED(SHGetKnownFolderPath(FOLDERID_LocalAppData, 0, nullptr, &local)) &&
        local != nullptr) {
        std::wstring p = local;
        CoTaskMemFree(local);
        p += L"\\ProjectPinner\\ProjectPinner.exe";
        if (GetFileAttributesW(p.c_str()) != INVALID_FILE_ATTRIBUTES) return p;
    }
    const std::wstring dir = ThisModuleDir();
    if (!dir.empty()) return dir + L"ProjectPinner.exe";  // best effort
    return L"";
}

// Launch a normal desktop process from inside the packaged COM surrogate.
// ShellExecute is denied here (SE_ERR_ACCESSDENIED) because we run with package
// identity; CreateProcess WITH the desktop-app-breakaway policy is the
// documented way to spawn a child that runs OUTSIDE the package (so it gets the
// real environment, real AppData, normal Quick Access access). Returns true on
// success; sets win32Err on failure.
bool LaunchDetached(const std::wstring& exe, const std::wstring& args, DWORD& win32Err) {
    std::wstring cmd = L"\"" + exe + L"\" " + args;
    std::vector<wchar_t> cmdline(cmd.begin(), cmd.end());
    cmdline.push_back(L'\0');

    STARTUPINFOEXW si = {};
    si.StartupInfo.cb = sizeof(si);

    SIZE_T attrSize = 0;
    InitializeProcThreadAttributeList(nullptr, 1, 0, &attrSize);
    bool haveAttr = false;
    if (attrSize > 0) {
        si.lpAttributeList = reinterpret_cast<LPPROC_THREAD_ATTRIBUTE_LIST>(
            HeapAlloc(GetProcessHeap(), 0, attrSize));
        if (si.lpAttributeList != nullptr &&
            InitializeProcThreadAttributeList(si.lpAttributeList, 1, 0, &attrSize)) {
            DWORD policy = PROCESS_CREATION_DESKTOP_APP_BREAKAWAY_ENABLE_PROCESS_TREE;
            if (UpdateProcThreadAttribute(si.lpAttributeList, 0,
                    PROC_THREAD_ATTRIBUTE_DESKTOP_APP_POLICY,
                    &policy, sizeof(policy), nullptr, nullptr)) {
                haveAttr = true;
            }
        }
    }

    PROCESS_INFORMATION pi = {};
    DWORD flags = haveAttr ? EXTENDED_STARTUPINFO_PRESENT : 0;
    si.StartupInfo.cb = haveAttr ? sizeof(si) : sizeof(STARTUPINFOW);
    BOOL ok = CreateProcessW(exe.c_str(), cmdline.data(), nullptr, nullptr, FALSE,
                             flags, nullptr, nullptr, &si.StartupInfo, &pi);
    win32Err = ok ? 0 : GetLastError();
    if (ok) { CloseHandle(pi.hThread); CloseHandle(pi.hProcess); }

    if (si.lpAttributeList != nullptr) {
        DeleteProcThreadAttributeList(si.lpAttributeList);
        HeapFree(GetProcessHeap(), 0, si.lpAttributeList);
    }
    return ok != FALSE;
}

}  // namespace

PinCommand::PinCommand() {
    DiagLog("PinCommand: ctor - Windows instantiated us via the modern menu binding.");
}
PinCommand::~PinCommand() {
    if (site_ != nullptr) site_->Release();
}

IFACEMETHODIMP PinCommand::QueryInterface(REFIID riid, void** ppv) {
    if (ppv == nullptr) return E_POINTER;
    *ppv = nullptr;
    if (IsEqualIID(riid, IID_IUnknown) ||
        IsEqualIID(riid, IID_IExplorerCommand)) {
        *ppv = static_cast<IExplorerCommand*>(this);
    } else if (IsEqualIID(riid, IID_IObjectWithSite)) {
        *ppv = static_cast<IObjectWithSite*>(this);
    } else {
        return E_NOINTERFACE;
    }
    AddRef();
    return S_OK;
}
IFACEMETHODIMP_(ULONG) PinCommand::AddRef() {
    return InterlockedIncrement(&ref_count_);
}
IFACEMETHODIMP_(ULONG) PinCommand::Release() {
    LONG r = InterlockedDecrement(&ref_count_);
    if (r == 0) delete this;
    return static_cast<ULONG>(r);
}

IFACEMETHODIMP PinCommand::GetTitle(IShellItemArray*, LPWSTR* name) {
    return WriteStringResult(L"Pin with alias to Quick Access", name);
}
IFACEMETHODIMP PinCommand::GetIcon(IShellItemArray*, LPWSTR* iconRef) {
    if (iconRef == nullptr) return E_POINTER;
    const std::wstring icon = IconLocation();
    if (icon.empty()) {
        *iconRef = nullptr;
        return E_NOTIMPL;
    }
    return SHStrDupW(icon.c_str(), iconRef);
}
IFACEMETHODIMP PinCommand::GetToolTip(IShellItemArray*, LPWSTR* tt) {
    return WriteStringResult(L"Give this folder a friendly alias pinned to Quick Access", tt);
}
IFACEMETHODIMP PinCommand::GetCanonicalName(GUID* guid) {
    if (guid != nullptr) *guid = CLSID_PinCommand;
    return S_OK;
}
IFACEMETHODIMP PinCommand::GetState(IShellItemArray*, BOOL, EXPCMDSTATE* state) {
    if (state == nullptr) return E_POINTER;
    // Always available on folders (the manifest only binds us to Directory /
    // Directory\Background, so there is nothing to filter here).
    *state = ECS_ENABLED;
    return S_OK;
}
IFACEMETHODIMP PinCommand::Invoke(IShellItemArray* items, IBindCtx*) {
    std::wstring path;
    if (!ExtractFirstPath(items, path)) {
        DiagLog("PinCommand::Invoke - no path in selection.");
        return E_INVALIDARG;
    }

    const std::wstring exe = InstalledExePath();
    DiagLog(std::string("PinCommand::Invoke - path=") + WideToUtf8(path) +
            " exe=" + WideToUtf8(exe));

    if (exe.empty() || GetFileAttributesW(exe.c_str()) == INVALID_FILE_ATTRIBUTES) {
        DiagLog("PinCommand::Invoke - ProjectPinner.exe not found at resolved path.");
        return E_FAIL;
    }

    // Quote the folder path so spaces survive as a single argument.
    const std::wstring args = L"--pin \"" + path + L"\"";

    // Primary: CreateProcess with desktop-app breakaway (ShellExecute is denied
    // from the packaged surrogate).
    DWORD err = 0;
    if (LaunchDetached(exe, args, err)) {
        DiagLog("PinCommand::Invoke - launched OK (CreateProcess breakaway).");
        return S_OK;
    }

    // Fallback: ShellExecute (works in non-packaged contexts).
    HINSTANCE rc = ShellExecuteW(nullptr, L"open", exe.c_str(), args.c_str(),
                                 nullptr, SW_SHOWNORMAL);
    const INT_PTR code = reinterpret_cast<INT_PTR>(rc);
    if (code <= 32) {
        char buf[128] = {0};
        StringCchPrintfA(buf, ARRAYSIZE(buf),
                         "PinCommand::Invoke - launch failed: CreateProcess err=%lu, ShellExecute code=%lld",
                         err, (long long)code);
        DiagLog(buf);
        return E_FAIL;
    }
    DiagLog("PinCommand::Invoke - launched OK (ShellExecute fallback).");
    return S_OK;
}
IFACEMETHODIMP PinCommand::GetFlags(EXPCMDFLAGS* flags) {
    if (flags != nullptr) *flags = ECF_DEFAULT;
    return S_OK;
}
IFACEMETHODIMP PinCommand::EnumSubCommands(IEnumExplorerCommand** out) {
    if (out != nullptr) *out = nullptr;
    return E_NOTIMPL;
}

IFACEMETHODIMP PinCommand::SetSite(IUnknown* site) {
    if (site_ != nullptr) { site_->Release(); site_ = nullptr; }
    if (site != nullptr) { site->AddRef(); site_ = site; }
    return S_OK;
}
IFACEMETHODIMP PinCommand::GetSite(REFIID riid, void** ppv) {
    if (ppv == nullptr) return E_POINTER;
    *ppv = nullptr;
    if (site_ == nullptr) return E_NOINTERFACE;
    return site_->QueryInterface(riid, ppv);
}

}  // namespace ProjectPinner
