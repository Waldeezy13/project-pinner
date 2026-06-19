#include "PinCommand.h"
#include "DebugLog.h"
#include "Paths.h"

#include <shellapi.h>  // ShellExecuteW
#include <shlwapi.h>   // SHStrDupW
#include <string>

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
    DiagLog(std::string("PinCommand::Invoke - launching pin dialog."));

    const std::wstring dir = ThisModuleDir();
    const std::wstring exe = dir + L"ProjectPinner.exe";
    // Quote the folder path so spaces survive as a single argument.
    const std::wstring args = L"--pin \"" + path + L"\"";

    HINSTANCE rc = ShellExecuteW(nullptr, L"open", exe.c_str(), args.c_str(),
                                 dir.c_str(), SW_SHOWNORMAL);
    if (reinterpret_cast<INT_PTR>(rc) <= 32) {
        DiagLog("PinCommand::Invoke - ShellExecute failed to launch ProjectPinner.exe.");
        return E_FAIL;
    }
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
