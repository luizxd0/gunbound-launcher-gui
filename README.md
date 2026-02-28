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
  The launcher UI has version-check and update panels, but the **patch/update logic is not implemented yet**. When you add it, use these in `Launcher.ini`:  
  - `[URLs] Manifest=` – URL of your patch manifest (e.g. file list).  
  - `[URLs] BaseFiles=` – Base URL for downloading game files.  
  - `[URLs] LauncherVersion=` – URL of a text file with latest launcher version.  
  Until then, the launcher goes straight to the login screen.

- **Login verification:** Set `[URLs] LoginCheckUrl=` or `[LauncherConfig] LoginCheckUrl=` to an HTTP(S) URL. When the user clicks "Game Start", the launcher POSTs `username` and `password` (form-encoded) to that URL. Your server should return **200** for valid credentials and **401** (or any non-2xx) for invalid. If the URL is omitted, the launcher does not verify and starts the game immediately.

- **Screen:** `[Screen] WindowedMode=0` (fullscreen, default) or `1` (windowed; requires dxwnd.dll or ddraw.dll in the game folder).

- **Server / game:** `[LauncherConfig] ServerIP=`, `BuddyIP=`, and optional `[GameConfig]` as in the example.

### Features

- Original Softnyx interface! Even includes the raon MessageBox (documentation pending).
- Thinned version of [gunbound-launcher](https://github.com/jglim/gunbound-launcher). Most of the technical details are in that repo.
- Most of the UI plumbing is already done. Just add logic! (and your own update mechanism)

![Kitchen Sink Screenshot](https://raw.github.com/jglim/gunbound-launcher-gui/master/Other/kitchen_sink.gif)

# License

MIT

Game client and artwork assets belong to Softnyx