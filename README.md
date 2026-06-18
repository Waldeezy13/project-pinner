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

- **Tiny & self-contained** — a single ~180 KB `.exe`. No installer, no .NET to install,
  **no admin rights**. Runs on any Windows 10 (1903+) or Windows 11 machine.
- **Two ways to pin** — right-click any folder → *Pin with alias to Quick Access*, or use
  the app. Type a friendly name, done.
- **Safe by design** — each alias is an ordinary Windows shortcut (`.lnk`), a pure pointer.
  Creating or deleting one **never touches the target network folder**. A built-in
  *Self-test* proves it.
- **Modern dark UI** — compact, with tooltips throughout.

## How it works

Windows resolves any individual link to a *network* folder back to that folder's real name
in Quick Access — so to show your alias, Project Pinner pins **one local "Projects" folder**
to Quick Access and fills it with friendly-named shortcuts to each project. Click *Projects*,
then your readable list.

## Install & use

1. Download `ProjectPinner.exe` from the [latest release](../../releases/latest).
2. Double-click it. It quietly installs itself to `%LOCALAPPDATA%\ProjectPinner\`, adds a
   Start Menu entry, registers the right-click menu, and pins your **Projects** folder.
   *(Unsigned download: if Windows SmartScreen appears, click **More info → Run anyway**.)*
3. Add projects from the app or via the folder right-click menu (on Windows 11 it's under
   **Show more options**).

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
