#include "ClassFactory.h"
#include "DebugLog.h"
#include "PinCommand.h"

#include <strsafe.h>
#include <new>
#include <string>

namespace ProjectPinner {

// Module-wide reference count. DllCanUnloadNow returns S_FALSE while any live
// ClassFactory / command exists; the COM surrogate polls this to decide when
// our DLL can be unloaded.
extern LONG g_module_refs;

ClassFactory::ClassFactory() {
    InterlockedIncrement(&g_module_refs);
}
ClassFactory::~ClassFactory() {
    InterlockedDecrement(&g_module_refs);
}

IFACEMETHODIMP ClassFactory::QueryInterface(REFIID riid, void** ppv) {
    if (ppv == nullptr) return E_POINTER;
    *ppv = nullptr;
    if (IsEqualIID(riid, IID_IUnknown) ||
        IsEqualIID(riid, IID_IClassFactory)) {
        *ppv = static_cast<IClassFactory*>(this);
        AddRef();
        return S_OK;
    }
    return E_NOINTERFACE;
}
IFACEMETHODIMP_(ULONG) ClassFactory::AddRef() {
    return InterlockedIncrement(&ref_count_);
}
IFACEMETHODIMP_(ULONG) ClassFactory::Release() {
    LONG result = InterlockedDecrement(&ref_count_);
    if (result == 0) delete this;
    return static_cast<ULONG>(result);
}

IFACEMETHODIMP ClassFactory::CreateInstance(IUnknown* pUnkOuter, REFIID riid,
                                            void** ppv) {
    DiagLog("ClassFactory::CreateInstance");
    if (ppv == nullptr) return E_POINTER;
    *ppv = nullptr;
    if (pUnkOuter != nullptr) return CLASS_E_NOAGGREGATION;

    auto* cmd = new (std::nothrow) PinCommand();
    if (cmd == nullptr) return E_OUTOFMEMORY;
    HRESULT hr = cmd->QueryInterface(riid, ppv);
    cmd->Release();
    char buf[32] = {0};
    StringCchPrintfA(buf, ARRAYSIZE(buf), "0x%08lX", static_cast<unsigned long>(hr));
    DiagLog(std::string("ClassFactory::CreateInstance hr=") + buf);
    return hr;
}

IFACEMETHODIMP ClassFactory::LockServer(BOOL fLock) {
    if (fLock) {
        InterlockedIncrement(&g_module_refs);
    } else {
        InterlockedDecrement(&g_module_refs);
    }
    return S_OK;
}

}  // namespace ProjectPinner
