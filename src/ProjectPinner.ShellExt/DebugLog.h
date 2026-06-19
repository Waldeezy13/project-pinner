// Process-wide diagnostic logger for the shell extension.
//
// Writes timestamped lines to <dll dir>\shellext-activation.log (i.e. inside
// %LOCALAPPDATA%\ProjectPinner, which the COM surrogate dllhost.exe can always
// write to for a per-user install). The trail tells us exactly how far COM
// activation got when the modern menu fails to appear:
//
//   DllMain ATTACH               — Windows loaded our DLL at all
//   DllGetClassObject CLSID=...   — COM resolver asked us for our class
//   ClassFactory::CreateInstance  — Windows asked us to construct the object
//   PinCommand: ctor              — Windows actually got an IExplorerCommand
//   PinCommand::GetState/Invoke   — Windows queried/fired the verb
//
// If the log is missing after a right-click, DllMain never ran — Windows
// vetoed the COM binding before our code (usually a signing/publisher or
// SurrogateServer issue). If DllMain logs but nothing else, it's a CLSID or
// vtable mismatch. This mirrors the proven Vault shell-ext diagnostic flow.

#pragma once

#include <string>

namespace ProjectPinner {

void DiagLog(const char* message);
void DiagLog(const std::string& message);

}  // namespace ProjectPinner
