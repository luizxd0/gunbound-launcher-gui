# Gunbound (Thor's Hammer) Launcher (GUI)

[gunbound-launcher](https://github.com/jglim/gunbound-launcher), with the original interface!

![Launcher Screenshot](https://raw.github.com/jglim/gunbound-launcher-gui/master/Other/launcher.png)

NOTE: This project is very preliminary, and requires some configuration before being usable.

### Quick Start

- Copy `Launcher.ini.example` to `Launcher.ini` next to the launcher and edit it (server IP, notice URL, etc.)
- Build with Visual Studio
- Place Launcher.exe and Launcher.ini in your GunBound installation folder (same folder as GunBound.gme)

### Configuration (Launcher.ini)

Create `Launcher.ini` from `Launcher.ini.example`. Main sections:

- **News / message**  
  - **Launcher panel:** Set `[URLs] Notice=` to a local file or a full URL.  
    Examples: `Notice=.\notice.html` or `Notice=http://yoursite.com/news.html`  
    If omitted, the launcher loads `notice.html` from the same folder as the exe.  
  - You can put a ready-made `notice.html` in the launcher folder (see `Other\notice.html` as a template).  
  - **In-game notice:** If you set `Notice` (or `[LauncherConfig] NoticeURL=`), that URL is also written to the game registry so the client can show the same notice in-game.

- **Patching**  
  The launcher now performs startup patch checks and file updates before login. Configure:
  - `[URLs] Manifest=` - URL to the manifest file.
  - `[URLs] BaseFiles=` - Base URL where patch files are hosted (optional if each manifest line has a direct URL column).
  - `[URLs] LauncherVersion=` - Optional URL with launcher version text (`1.2.3.4`); if newer than local, launcher shows the Full Download panel.
  - If not configured in `Launcher.ini`, launcher defaults to:
  - `http://classic-gunbound.servegame.com/update/manifest.txt`
  - `http://classic-gunbound.servegame.com/update/gamefiles/`
  - If patching fails (manifest download/parse, hash mismatch, file replace error), launcher shows the Full Download panel.
  - Supported manifest line formats (comments/blank lines allowed):
  - `relative/path.ext|<hash>`
  - `relative/path.ext|<hash>|<size>`
  - `relative/path.ext|<hash>|<size>|https://absolute/file/url.ext`
  - `<hash>|relative/path.ext|<size>`
  - separators: `|`, `,`, `;`, or tab
  - hash lengths: 32 (MD5), 40 (SHA1), 64 (SHA256)

- **Login verification:** Set `[URLs] LoginCheckUrl=` or `[LauncherConfig] LoginCheckUrl=` to an HTTP(S) URL. When the user clicks "Game Start", the launcher POSTs `username` and `password` (form-encoded) to that URL. Your server should return **200** for valid credentials and **401** (or any non-2xx) for invalid. If the URL is omitted, the launcher does not verify and starts the game immediately.
- **Input:** Optional `[LauncherConfig] ForceCapsLockOff=1` (default behavior) turns Caps Lock off before launching the client, to reduce false caps-lock warnings in legacy UI.

- **Screen:**  
  - Set `DisplayProfile=` (`fullscreen_voodoo2` default/recommended, `fullscreen` native, `windowed`).  
  - Legacy profiles (`fullscreen_dxwnd`, `fullscreen_compat`, `compact`, and numeric `2/4`) are mapped to `fullscreen_voodoo2`.
  - Profile files are copied from `compat\...` into the game folder at launch (matching `agasready/gunbound_launcher` behavior). See `Launcher.ini.example` for folder mapping.  
  - You can reuse profile files from https://github.com/agasready/gunbound_launcher/tree/main/compat.
  - Windowed mode is forced to `800x600` for stability. `1024x768` selection is ignored in windowed mode.
  - Selected `GraphResolution` is also applied to wrapper config files (`dxwnd.dxw`, `windowed.ini`, `DdrawCompat.ini`) when present.
  - For DxWnd proxy mode, the launcher rewrites `dxwnd.dxw` with explicit `path0`, `startfolder0`, and `launchpath0` for the current `GunBound.gme` path (legacy malformed entries are normalized automatically).
  - Launcher also tries to clear Windows `AppCompatFlags\Layers` overrides for `GunBound.gme` (e.g. `DWM8And16BitMitigation`) because those can force letterboxed/compat-like rendering regardless of selected mode.
  - A runtime trace is written to `launcher-debug.log` next to `Launcher.exe` for troubleshooting mode selection and backend decisions.

- **Server / game:** `[LauncherConfig] ServerIP=`, `BuddyIP=`, and optional `[GameConfig]` as in the example.

### Patch Build Workflow (Static Host)

Use `Tools/build-patch.ps1` to generate a full manifest and copy only changed/new files into a publish folder:

```powershell
powershell -ExecutionPolicy Bypass -File .\Tools\build-patch.ps1 `
  -ClientRoot C:\GB\Client `
  -PublishRoot C:\GB\PatchPublish `
  -PruneDeleted
```

Or just double-click `Tools\build-patch.bat` (it wraps the same script with prompts).

Results:
- `manifest.txt` in `PatchPublish`
- patch files in `PatchPublish\gamefiles\...`
- script excludes `Launcher.exe` by default (launcher self-update should stay on the full-download path).

Then upload the publish folder to your web host and set:
- `[URLs] Manifest=https://your-host/manifest.txt`
- `[URLs] BaseFiles=https://your-host/gamefiles/`

For this project host:
- `[URLs] Manifest=http://classic-gunbound.servegame.com/update/manifest.txt`
- `[URLs] BaseFiles=http://classic-gunbound.servegame.com/update/gamefiles/`

### Step-by-Step Release (Classic Host)

1. Keep your latest playable client in a local folder (example `C:\GB\Client`).
2. Double-click `Tools\build-classic-update.bat`.
3. If needed, edit `DEFAULT_CLIENT_ROOT` and `DEFAULT_STAGING_UPDATE_ROOT` in `Tools\build-classic-update.bat`.
4. After build, upload all contents of your staging folder to website folder `/update`.
5. Confirm these URLs are reachable:
6. `http://classic-gunbound.servegame.com/update/manifest.txt`
7. `http://classic-gunbound.servegame.com/update/gamefiles/`
8. Launch client and verify it patches only changed files.

### Features

- Original Softnyx interface! Even includes the raon MessageBox (documentation pending).
- Thinned version of [gunbound-launcher](https://github.com/jglim/gunbound-launcher). Most of the technical details are in that repo.
- Includes startup patch check/update flow and server-configurable update manifest support.

![Kitchen Sink Screenshot](https://raw.github.com/jglim/gunbound-launcher-gui/master/Other/kitchen_sink.gif)

# License

MIT

Game client and artwork assets belong to Softnyx
