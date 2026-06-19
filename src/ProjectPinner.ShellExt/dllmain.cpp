// In-proc COM server entry point for the Project Pinner context-menu DLL.
//
// Exports (see .def):
//   DllGetClassObject - hands out the IClassFactory for our CLSID
//   DllCanUnloadNow   - lets COM unload us once nothing is in flight
//
// Activation for the Win11 modern menu goes through PackagedCom: the sparse
// AppxManifest declares a com:SurrogateServer pointing at this DLL by CLSID, so
// the shell loads us into a dllhost.exe surrogate and calls DllGetClassObject.
// There is NO HKCR/regsvr32 registration — the manifest is the only registry.

#include "ClassFactory.h"
#include "DebugLog.h"
#include "PinCommand.h"

#include <windows.h>
#include <new>
#include <string>

namespace ProjectPinner {
// Defined here so every translation unit shares one module-wide ref count.
LONG g_module_refs = 0;
}  // namespace ProjectPinner

namespace {
HMODULE g_module = nullptr;
}  // namespace

BOOL APIENTRY DllMain(HMODULE hModule, DWORD reason, LPVOID /*reserved*/) {
    switch (reason) {
        case DLL_PROCESS_ATTACH:
            g_module = hModule;
            DisableThreadLibraryCalls(hModule);
            ProjectPinner::DiagLog("DllMain ATTACH");
            break;
        case DLL_PROCESS_DETACH:
            ProjectPinner::DiagLog("DllMain DETACH");
            g_module = nullptr;
            break;
    }
    return TRUE;
}

// STDAPI matches the prototype in combaseapi.h; the .def exports the symbol.
STDAPI DllGetClassObject(REFCLSID rclsid, REFIID riid, LPVOID* ppv) {
    if (ppv == nullptr) return E_POINTER;
    *ppv = nullptr;

    LPOLESTR clsidStr = nullptr;
    StringFromCLSID(rclsid, &clsidStr);
    char narrow[64] = {0};
    if (clsidStr != nullptr) {
        WideCharToMultiByte(CP_UTF8, 0, clsidStr, -1, narrow,
                            ARRAYSIZE(narrow), nullptr, nullptr);
        CoTaskMemFree(clsidStr);
    }
    ProjectPinner::DiagLog(std::string("DllGetClassObject CLSID=") + narrow);

    if (IsEqualCLSID(rclsid, ProjectPinner::CLSID_PinCommand)) {
        auto* factory = new (std::nothrow) ProjectPinner::ClassFactory();
        if (factory == nullptr) return E_OUTOFMEMORY;
        HRESULT hr = factory->QueryInterface(riid, ppv);
        factory->Release();
        return hr;
    }
    return CLASS_E_CLASSNOTAVAILABLE;
}

STDAPI DllCanUnloadNow() {
    return ProjectPinner::g_module_refs == 0 ? S_OK : S_FALSE;
}
