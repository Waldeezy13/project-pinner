#pragma once

#include <string>

namespace ProjectPinner {

// Returns the directory this DLL lives in (with trailing backslash), e.g.
// "C:\\Users\\me\\AppData\\Local\\ProjectPinner\\". The installer places
// ProjectPinner.exe and app.ico alongside the DLL, so everything the shell
// extension needs is resolved relative to this single location — no hard-coded
// install paths.
std::wstring ThisModuleDir();

}  // namespace ProjectPinner
