# Project Pinner — User Guide & Test Plan

Give long, unfriendly project numbers an easy-to-read name, gathered in one **"Projects"**
folder pinned to File Explorer's **Quick Access**.

- Each project becomes a **friendly-named shortcut** (e.g. `Acme Tower - 1003948572`) inside
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
A single file: **`ProjectPinner.exe`** (~120 KB). No installer, no .NET to install,
**no admin rights**. Runs on any Windows 10 (1903+) or Windows 11 machine.

## 2. First run
Double-click `ProjectPinner.exe`. It quietly installs itself to `%LOCALAPPDATA%\ProjectPinner\`,
adds a Start Menu shortcut, registers the right-click menu, and (the first time you add a
project) pins your **Projects** folder to Quick Access. No prompts, no admin.

*(Unsigned download: if Windows shows "Windows protected your PC", click **More info → Run
anyway**. Ask about code-signing to remove this.)*

## 3. Two ways to add a project

**A) Right-click a folder**
1. Right-click the project folder. **Windows 11:** click **Show more options** first.
2. Choose **"Pin with alias to Quick Access."**
3. Type the friendly name → **Pin**. It's added to your Projects folder.

**B) From the app**
1. Open **Project Pinner**. Paste a network path (or **Browse…**), type a **Friendly name**,
   check the preview, click **Create & Pin**.

Either way: open Quick Access → **Projects** → click your friendly name to jump to the project.

In the app's list you can **Open** a project, **Pin Projects folder** (re-pin the hub if you
ever unpin it), or **Remove link** (deletes only that shortcut).

---

## 4. How to test it (on Windows)

### Test A — Self-test (proves safety, no network needed)
App → **Run self-test**. It creates a temp folder + file, makes a friendly shortcut to it,
deletes the shortcut, and **verifies the folder and file are untouched**. Expect:
*"All checks passed — aliases are just links, and removing one is safe."*

### Test B — Local dry run
1. Make `C:\Temp\9999999` with a file inside.
2. App → Browse to it → name `Test Project` → **Create & Pin**.
3. Quick Access now shows a **Projects** folder → open it → you see **`Test Project - 9999999`**
   → it opens `C:\Temp\9999999`.
4. Select it → **Remove link** → confirm the shortcut is gone but `C:\Temp\9999999` and its
   file remain.

### Test C — Right-click a real network folder
Right-click a project on the network → (Win11: *Show more options* →) **Pin with alias to
Quick Access** → name it → **Pin**. Confirm it appears in **Projects** with your alias and
opens the live project. Remove it and confirm the network folder is untouched.

---

## 5. Good to know
- **Rename the hub / see where it lives:** click **Folder name & location** (top-right of the
  app) to expand a panel where you can rename the Quick Access folder (default "Projects") and
  see/open its local path. Renaming just renames the **local** folder and re-pins it — your
  network folders are never moved or renamed.
- **Where it lives:** by default `%LOCALAPPDATA%\ProjectPinner\Projects`, holding one `.lnk`
  per project. The friendly name = the shortcut's filename.
- **Upgrading from the earlier build:** on first launch the app automatically clears the old,
  wrongly-named pins it had created (removing only its own local placeholders — never your
  network folders) and shows a note.
- **Right-click menu on/off:** toggle from the app (bottom-right).
- **Uninstall:** turn the right-click menu off in the app, then delete
  `%LOCALAPPDATA%\ProjectPinner\` and the Start Menu shortcut, and unpin Projects.

## 6. Troubleshooting
| Symptom | Fix |
|---|---|
| Right-click item missing (Win11) | It's under **Show more options** (or press Shift+F10). |
| Projects folder not pinned | App → **Pin Projects folder**. |
| Clicking the menu opens the main app, not the prompt | The installed copy is stale; close all windows and re-run the new exe (it now self-updates even if one is running). |
| "Windows protected your PC" | Unsigned download: **More info → Run anyway** (or code-sign it). |
| Startup error dialog | Paste me the message or `%LOCALAPPDATA%\ProjectPinner\error.log`. |
