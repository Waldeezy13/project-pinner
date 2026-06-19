<div align="center">

<img src="assets/icon_preview.png" width="96" alt="Project Pinner icon" />

# Project Pinner

**Give long, unfriendly project numbers an easy-to-read name in File Explorer's Quick Access.**

[Download the latest release »](../../releases/latest)

</div>

---

A lightweight Windows utility that turns a network folder like
`\\fileserver\projects\1003948572-PH2` into a readable, pinned shortcut such as
**`Acme Tower - 1003948572-PH2`** — without ever moving, renaming, or modifying the
real folder on the network.

- **One file, no admin** — a single signed `.exe`. Double-click to install: it sets up the
  Windows 11 **top-level** right-click menu and opens the app. Runs on Windows 10 (1903+)
  and Windows 11.
- **Right-click to pin** — right-click any folder → *Pin with alias to Quick Access*, type an
  alias, done. (Or add projects from the app.)
- **Safe by design** — each alias is an ordinary Windows shortcut (`.lnk`), a pure pointer.
  Creating or deleting one **never touches the target network folder**. A built-in
  *Self-test* proves it.
- **Signed & modern** — code-signed by Waldo Development LLC (no SmartScreen "unknown
  publisher"). Compact dark UI with tooltips throughout.

## How it works

Windows resolves any individual link to a *network* folder back to that folder's real name
in Quick Access — so to show your alias, Project Pinner pins **one local "Projects" folder**
to Quick Access and fills it with friendly-named shortcuts to each project. Click *Projects*,
then your readable list.

## Install & use

1. Download `ProjectPinner.exe` from the [latest release](../../releases/latest).
2. Double-click it. It installs per-user to `%LOCALAPPDATA%\ProjectPinner\` (**no admin**),
   registers the right-click menu, and opens. *The first launch pauses a few seconds while it
   sets up the menu — that's normal.*
3. Right-click any folder → **Pin with alias to Quick Access** (the **top-level** menu on
   Windows 11; the regular context menu on Windows 10). Type an alias. Or add projects from
   the app.

**Update:** download the new `.exe` and run it — it replaces the installed copy and refreshes
the menu. **Uninstall:** open Project Pinner → **Uninstall** (removes everything cleanly), or
*Settings → Apps → Project Pinner*.

Full guide: [docs/USER_GUIDE.md](docs/USER_GUIDE.md).

## Build from source

Requires the [.NET SDK](https://dotnet.microsoft.com/download) (builds the .NET Framework 4.8
target via NuGet reference assemblies — works on Windows, macOS, or Linux):

```bash
./build.sh
# or:
dotnet build src/ProjectPinner/ProjectPinner.csproj -c Release
```

The output is `src/ProjectPinner/bin/Release/ProjectPinner.exe`.

## License

Proprietary — © 2026 Waldo Development LLC. All rights reserved. See [LICENSE](LICENSE).

<div align="center"><sub>Developed by <b>Waldo Development LLC</b></sub></div>
