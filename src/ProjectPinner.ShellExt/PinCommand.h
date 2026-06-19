// IExplorerCommand implementation for the Windows 11 modern right-click menu.
//
// A single command: "Pin with alias to Quick Access". Explorer instantiates
// this once per right-click on a folder, asks GetState whether to show it, and
// calls Invoke when clicked. Invoke just launches the installed
// ProjectPinner.exe with --pin "<folder>" — all the real work (the dialog,
// the .lnk, the Quick Access pin) stays in the managed app.
//
// The CLSID is stable and referenced by GUID in AppxManifest.xml under both
// the com:SurrogateServer <com:Class Id=...> and the desktop5:Verb Clsid=...
// (they MUST match).

#pragma once

#include <windows.h>
#include <shlobj.h>
#include <shobjidl_core.h>

namespace ProjectPinner {

// {A3D8F1E2-6C4B-4A91-B7E5-2F9C8D013A64}
extern const CLSID CLSID_PinCommand;

class PinCommand : public IExplorerCommand, public IObjectWithSite {
 public:
    PinCommand();

    // IUnknown
    IFACEMETHODIMP QueryInterface(REFIID riid, void** ppv) override;
    IFACEMETHODIMP_(ULONG) AddRef() override;
    IFACEMETHODIMP_(ULONG) Release() override;

    // IExplorerCommand
    IFACEMETHODIMP GetTitle(IShellItemArray* items, LPWSTR* name) override;
    IFACEMETHODIMP GetIcon(IShellItemArray* items, LPWSTR* iconRef) override;
    IFACEMETHODIMP GetToolTip(IShellItemArray* items, LPWSTR* tooltip) override;
    IFACEMETHODIMP GetCanonicalName(GUID* guid) override;
    IFACEMETHODIMP GetState(IShellItemArray* items, BOOL okToBeSlow,
                            EXPCMDSTATE* state) override;
    IFACEMETHODIMP Invoke(IShellItemArray* items, IBindCtx* ctx) override;
    IFACEMETHODIMP GetFlags(EXPCMDFLAGS* flags) override;
    IFACEMETHODIMP EnumSubCommands(IEnumExplorerCommand** out) override;

    // IObjectWithSite — Explorer hands us its site pointer.
    IFACEMETHODIMP SetSite(IUnknown* site) override;
    IFACEMETHODIMP GetSite(REFIID riid, void** ppv) override;

 private:
    ~PinCommand();
    LONG ref_count_ = 1;
    IUnknown* site_ = nullptr;
};

}  // namespace ProjectPinner
