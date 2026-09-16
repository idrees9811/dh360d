# DH360D - Windows driver for the Darkflash DH360D pump LCD

Unofficial replacement for the broken Darkflash Windows app. Drives the DH360D pump screen over USB serial (CH340) with live CPU temperature, RPM, CPU load, and RAM usage.

Works as a system tray app with a settings UI, optional Windows startup, and a one-click installer for public distribution.

Protocol reference: https://github.com/clarkse/dh360d

## Compatibility fixes vs the official Darkflash app

| Issue | Official Darkflash app | DH360D |
|-------|------------------------|--------|
| RPM gauge on **MSI B850** boards | Often blank / 0 RPM | Works — reads fan RPM via LibreHardwareMonitor + PawnIO |

On some MSI B850 setups the stock Darkflash software never populates the RPM field on the pump LCD. DH360D reads radiator/case fan tach from the motherboard (default) or pump tach (Settings → **RPM on LCD**), so the gauge should show a live value after PawnIO is installed and the correct COM port is saved.

If RPM is still 0, open **Settings**, try **Radiator / case fan** vs **Pump tachometer**, run **Diagnostics** from the tray menu (`DH360D.exe sensors`), and confirm PawnIO is installed.

## Why you might see warnings while the LCD still works

The pump LCD has four visible fields: temperature, one RPM gauge, CPU %, and RAM %.

- CPU % and RAM % update without special drivers.
- Temperature and RPM need the PawnIO kernel driver (installed once from Settings).
- The LCD reads the pump RPM field (bytes 2-3). Fan RPM (bytes 4-5) is not shown on most DH360D units.
- Occasional serial timeout messages during reconnect are normal; the tray app retries automatically.

First-time setup:

1. Launch DH360D (UAC prompt is normal - admin is required for sensors).
2. Settings opens automatically on first run.
3. Click **Install PawnIO** (one-time), reboot if prompted.
4. Pick COM port and RPM source, then **Save**.

## Quick install (end users)

### Installer (recommended)

1. Download the latest `DH360D-Setup-X.Y.0.exe` from [GitHub Releases](https://github.com/idrees9811/dh360d/releases).
2. Run the installer (requires Administrator).
3. Settings opens after install; complete first-time setup above.
4. Optional: enable **Start minimized** so later launches stay in the tray.

### Portable

```powershell
.\publish\DH360D.exe
```

First launch opens Settings. Accept the UAC prompt - the app always runs as Administrator so temp and RPM sensors work.

## Settings

| LCD field | Source options |
|-----------|----------------|
| Temperature | CPU (always C on wire) |
| RPM on screen | Radiator fan (default) / Auto / Pump tach |
| CPU usage | CPU total load |
| RAM usage | Memory load |

Config: `%LOCALAPPDATA%\DH360DFeed\config.json`

## Logging and memory

| Location | Limit |
|----------|-------|
| `%LOCALAPPDATA%\DH360DFeed\logs\dh360d.log` | Rotates at 1 MB; keeps 2 backups |
| In-app buffer (Live Logs window) | Last 300 lines |
| Feed loop | Connect, errors, and ACK misses only |

## Build: exe vs installer

`.\build.ps1` always produces `publish\DH360D.exe` (the app itself).

**Inno Setup** is optional. It only wraps that exe into `dist\DH360D-Setup-1.0.0.exe` for end users who want a normal Windows installer on GitHub Releases. You do not need Inno to run or develop the app.

If build says Inno was not found:

```powershell
winget install JRSoftware.InnoSetup
# Close and reopen the terminal, then:
.\build.ps1
```

## Project layout

```
  Dh360dFeed.csproj
  app.manifest
  build.ps1
  scripts/Compute-ReleaseVersion.ps1
  .github/workflows/release.yml
  assets/app.ico, assets/logo.png
  installer/dh360d.iss
  vendor/LibreHardwareMonitor/  (git submodule)
  driver/PawnIO_setup.exe       (downloaded at build, gitignored)
  publish/                      (portable exe, gitignored)
  dist/                         (installer output, gitignored)
  src/
    Core/       AppConfig, AppPaths, DisplaySettings, ...
    Hardware/   HardwareMetrics, PawnIoBootstrap
    Serial/     PumpLink
    Feed/       FeedWorker
    Logging/    AppLog
    Ui/         SettingsForm, LiveLogsForm, TrayAppContext
    Cli/        ConsoleCommands
    System/     StartupHelper
```

## Build from source

Requirements: Windows 10/11 x64, .NET 8 SDK, Inno Setup 6 (optional, for installer only).

```powershell
git submodule update --init --recursive   # if using vendor/LibreHardwareMonitor
.\build.ps1
```

Quit the tray app before rebuilding - `publish\DH360D.exe` is locked while running.

## Releases (CI/CD)

Pushes to `main` that change build inputs (`src/`, `assets/`, `installer/`, `build.ps1`, `Dh360dFeed.csproj`, vendor submodule, etc.) automatically build, tag, and publish a GitHub Release. **Docs-only commits** (e.g. README, LICENSE) skip the release job — no version bump.

| Action | Result |
|--------|--------|
| Push code/build files to `main` | Minor version auto-increments (`v1.0.0` → `v1.1.0` → `v1.2.0`) |
| Edit `<Version>` in `Dh360dFeed.csproj` to `2.0.0`, then push | Major release `v2.0.0` |
| Push README/docs only | Release skipped |
| **Actions → Release → Run workflow** | Manual release on demand |

**Manual major bump:** change `<Version>1.0.0</Version>` to `<Version>2.0.0</Version>` in [`Dh360dFeed.csproj`](Dh360dFeed.csproj) before pushing. CI detects the new major and resets the release line. You do not need to edit the installer script or create tags manually.

**Release artifacts attached automatically:**
- `DH360D-Setup-X.Y.0.exe` (installer)
- `DH360D.exe` (portable)

### Pre-push checklist

1. Delete `%LOCALAPPDATA%\DH360DFeed\config.json` and confirm Settings opens on first launch.
2. Confirm UAC prompt appears once (no manual "Run as administrator" needed).
3. Install PawnIO from Settings; verify temp and RPM on the LCD.
4. Reboot; confirm startup works if enabled (logon scheduled task with highest privileges).

### Repo hygiene (do not commit)

- `publish/`, `dist/`, `driver/PawnIO_setup.exe`, `bin/`, `obj/`

## CLI

```powershell
DH360D.exe sensors
DH360D.exe install-driver
DH360D.exe handshake
DH360D.exe detect
```

## License

MIT - see LICENSE. LibreHardwareMonitor is used under its own license (see vendor submodule).