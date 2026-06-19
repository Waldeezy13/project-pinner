Project Pinner — Windows 11 top-level right-click menu
======================================================

This bundle adds "Pin with alias to Quick Access" directly to the Windows 11
right-click menu for folders (the main menu — not under "Show more options").

INSTALL
-------
1. Keep all files in this folder together.
2. Double-click  Install.bat
   (or right-click Install.ps1 -> Run with PowerShell)

No administrator rights are required. Explorer restarts once to refresh the menu.

Then right-click any folder -> "Pin with alias to Quick Access".

UNINSTALL
---------
Double-click  Uninstall.bat
(You can also remove it from Settings -> Apps -> Project Pinner.)

FILES
-----
ProjectPinner.exe            The app (also runs on its own; double-click to open).
ProjectPinner.ShellExt.dll   The right-click menu handler (native).
ProjectPinner.msix           Registers the menu (installed per-user).
app.ico                      Menu / app icon.
Install.bat / Install.ps1    Installer.
Uninstall.bat / Uninstall.ps1  Remover.

Everything installs to:  %LOCALAPPDATA%\ProjectPinner

If the menu does not appear, the file
  %LOCALAPPDATA%\ProjectPinner\shellext-activation.log
records how far Windows got loading the handler — send it to support.

© 2026 Waldo Development LLC. All rights reserved.
