#pragma once

#include <windows.h>
#include <unknwn.h>

namespace ProjectPinner {

// COM class factory for the single COM class this DLL hosts (PinCommand).
// Explorer requests an IClassFactory via DllGetClassObject; CreateInstance
// constructs a PinCommand.
class ClassFactory : public IClassFactory {
 public:
    ClassFactory();

    IFACEMETHODIMP QueryInterface(REFIID riid, void** ppv) override;
    IFACEMETHODIMP_(ULONG) AddRef() override;
    IFACEMETHODIMP_(ULONG) Release() override;

    IFACEMETHODIMP CreateInstance(IUnknown* pUnkOuter, REFIID riid,
                                  void** ppv) override;
    IFACEMETHODIMP LockServer(BOOL fLock) override;

 private:
    ~ClassFactory();
    LONG ref_count_ = 1;
};

}  // namespace ProjectPinner
