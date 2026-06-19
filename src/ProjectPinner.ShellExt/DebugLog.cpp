#include "DebugLog.h"
#include "Paths.h"

#include <windows.h>
#include <strsafe.h>

#include <cstring>

namespace ProjectPinner {

namespace {

std::wstring LogPath() {
    std::wstring dir = ThisModuleDir();
    if (dir.empty()) return L"";
    return dir + L"shellext-activation.log";
}

void AppendBytes(const char* bytes, DWORD length) {
    const std::wstring path = LogPath();
    if (path.empty()) return;
    HANDLE file = CreateFileW(
        path.c_str(),
        FILE_APPEND_DATA,
        FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE,
        nullptr,
        OPEN_ALWAYS,
        FILE_ATTRIBUTE_NORMAL,
        nullptr);
    if (file == INVALID_HANDLE_VALUE) return;
    DWORD written = 0;
    WriteFile(file, bytes, length, &written, nullptr);
    CloseHandle(file);
}

// Logging defaults ON so the first releases are diagnosable on the user's
// machine. Set PROJECTPINNER_SHELLEXT_DEBUG=0 in the Explorer environment to
// silence it. Evaluated once per process (env snapshotted at DLL load).
bool DiagEnabled() {
    static const bool enabled = []() {
        wchar_t buf[8] = {0};
        DWORD n = GetEnvironmentVariableW(L"PROJECTPINNER_SHELLEXT_DEBUG",
                                          buf, ARRAYSIZE(buf));
        if (n == 0) return true;             // unset -> on
        return buf[0] != L'0';               // "0" -> off, anything else -> on
    }();
    return enabled;
}

}  // namespace

void DiagLog(const char* message) {
    if (!DiagEnabled()) return;

    SYSTEMTIME t;
    GetLocalTime(&t);
    char stamp[96] = {0};
    StringCchPrintfA(stamp, ARRAYSIZE(stamp),
                     "[%04d-%02d-%02d %02d:%02d:%02d.%03d pid=%lu] ",
                     t.wYear, t.wMonth, t.wDay,
                     t.wHour, t.wMinute, t.wSecond, t.wMilliseconds,
                     GetCurrentProcessId());

    AppendBytes(stamp, static_cast<DWORD>(std::strlen(stamp)));
    if (message != nullptr) {
        AppendBytes(message, static_cast<DWORD>(std::strlen(message)));
    }
    AppendBytes("\r\n", 2);
}

void DiagLog(const std::string& message) {
    DiagLog(message.c_str());
}

}  // namespace ProjectPinner
