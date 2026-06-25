# Project Pinner — User Guide & Test Plan

Give long, unfriendly project numbers an easy-to-read name, gathered in one **"Projects"**
folder pinned to File Explorer's **Quick Access**.

- Each project becomes an **alias shortcut** (e.g. `Acme Tower - 1003948572`) inside
  your pinned **Projects** folder. Click it → it opens the real network folder.
- **Your network folders are never touched.** An alias is just an ordinary Windows shortcut
  (`.lnk`) — a pure pointer. Creating, renaming, or deleting one never opens, writes,
  renames, or modifies anything on the network.

> **Why a "Projects" hub instead of pinning each project on its own?** Windows resolves any
> link to a *network* folder back to that folder's real name in Quick Access (so individual
> pins always showed the project number, not your alias). The fix that actually works: pin
> **one** local "Projects" folder, and put friendly-named shortcuts inside it. The shortcut
> filename is your alias, fully under your control.

---

## 1. What you download
A single **signed** file: **`ProjectPinner.exe`**. No separate installer, no .NET to install,
**no admin rights**. Runs on Windows 10 (1903+) and Windows 11.

## 2. First run
Double-click `ProjectPinner.exe`. It installs per-user to `%LOCALAPPDATA%\ProjectPinner\`,
adds a Start Menu shortcut, and sets up the right-click menu, then opens. *The first launch
pauses a few seconds while it registers the menu — that's normal.* No prompts, no admin.

Because it's code-signed by Waldo Development LLC, there's **no SmartScreen "unknown
publisher"** warning.

## 3. Two ways to add a project

**A) Right-click a folder**
1. Right-click the project folder. On **Windows 11** the entry is in the **main** menu
   (**Pin with alias to Quick Access**); on Windows 10 it's in the regular context menu.
2. Type the **alias** → **Pin**. It's added to your Projects folder.

**B) From the app**
1. Open **Project Pinner**. Paste a network path (or **Browse…**), type an **Alias**,
   check the preview, click **Create & Pin**.

Either way: open Quick Access → **Projects** → click your alias to jump to the project.

In the app's list you can **Open** a project, **Pin Projects folder** (re-pin the hub if you
ever unpin it), or **Remove** (deletes only that shortcut).

---

## 4. How to test it (on Windows)

### Test A — Self-test (proves safety, no network needed)
App → **Settings** → **Run safety self-test**. It creates a temp folder + file, makes a friendly
shortcut to it, deletes the shortcut, and **verifies the folder and file are untouched**. Expect:
*"All checks passed — aliases are just links, and removing one is safe."*

### Test B — Local dry run
1. Make `C:\Temp\9999999` with a file inside.
2. App → Browse to it → name `Test Project` → **Create & Pin**.
3. Quick Access now shows a **Projects** folder → open it → you see **`Test Project - 9999999`**
   → it opens `C:\Temp\9999999`.
4. Select it → **Remove** → confirm the shortcut is gone but `C:\Temp\9999999` and its
   file remain.

### Test C — Right-click a real network folder
Right-click a project on the network → **Pin with alias to Quick Access** (top-level menu on
Win11) → name it → **Pin**. Confirm it appears in **Projects** with your alias and opens the
live project. Remove it and confirm the network folder is untouched.

---

## 5. Good to know
- **Settings panel:** click **Settings** (top-right of the app) to expand a panel with the
  appearance (theme) controls, the Quick Access folder name/location, and the safety self-test.
- **Light / Dark theme:** in **Settings → Appearance** pick **Auto** (matches your Windows
  light/dark setting), **Light**, or **Dark**. High Contrast mode is respected automatically.
- **Rename the hub / see where it lives:** in **Settings**, rename the Quick Access folder
  (default "Projects") and see/open its local path. Renaming just renames the **local** folder
  and re-pins it — your network folders are never moved or renamed.
- **Where it lives:** by default `%LOCALAPPDATA%\ProjectPinner\Projects`, holding one `.lnk`
  per project. The alias = the shortcut's filename.
- **Update:** download the new `ProjectPinner.exe` and run it — it replaces the installed copy
  and refreshes the menu.
- **Uninstall:** open Project Pinner → **Uninstall** (removes the menu, app, shortcuts, pins,
  and the install folder cleanly), or *Settings → Apps → Project Pinner*. Your network folders
  are never touched.

## 6. Troubleshooting
| Symptom | Fix |
|---|---|
| Top-level menu entry missing (Win11) | Re-run the exe to re-register; sign out/in if needed. The entry can also appear under **Show more options** as a fallback. |
| Projects folder not pinned | App → **Pin folder**. |
| Right-click does nothing | See `%LOCALAPPDATA%\ProjectPinner\shellext-activation.log` and send it over. |
| Startup error dialog | Paste me the message or `%LOCALAPPDATA%\ProjectPinner\error.log`. |
