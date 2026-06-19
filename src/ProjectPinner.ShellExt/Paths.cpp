#include "Paths.h"

#include <windows.h>

namespace ProjectPinner {

std::wstring ThisModuleDir() {
    HMODULE module = nullptr;
    // Resolve OUR module handle from the address of this very function, so it
    // works no matter what process (Explorer, dllhost surrogate) loaded us.
    if (!GetModuleHandleExW(
            GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS |
                GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
            reinterpret_cast<LPCWSTR>(&ThisModuleDir),
            &module)) {
        return L"";
    }

    wchar_t path[MAX_PATH * 2] = {0};
    if (GetModuleFileNameW(module, path, ARRAYSIZE(path)) == 0) {
        return L"";
    }

    std::wstring dir = path;
    const auto slash = dir.find_last_of(L"\\/");
    if (slash == std::wstring::npos) return L"";
    dir.resize(slash + 1);  // keep the trailing separator
    return dir;
}

}  // namespace ProjectPinner
