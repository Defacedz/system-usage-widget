# System Widget

A tiny always-on-top gauge for Windows showing GPU power draw, VRAM, CPU and
RAM at a glance.

*Read this in [Français](README.fr.md).*

<img src="docs/screenshot.png" alt="The widget showing GPU power, VRAM, CPU and RAM gauges" width="546">

It sits above the taskbar and never disappears behind it, because the
executable is built with the `uiAccess` privilege — the same one the Magnifier
and the on-screen keyboard use.

## Install

Paste this into **PowerShell** and accept the administrator prompt:

```powershell
irm https://raw.githubusercontent.com/Defacedz/system-usage-widget/main/web-install.ps1 | iex
```

Or from **cmd.exe**:

```bat
powershell -NoProfile -ExecutionPolicy Bypass -Command "irm https://raw.githubusercontent.com/Defacedz/system-usage-widget/main/web-install.ps1 | iex"
```

That downloads this repository to a temporary folder and runs `Installer.ps1`.
If you would rather see what you are running first — which is the sensible
habit with any `| iex` command — clone the repository and double-click
`Installer.bat` instead.

### What the installer does

- Builds `SystemWidget.cs` **on your machine** with the C# compiler already
  included in Windows. Nothing is downloaded beyond this repository's source,
  and no build toolchain is needed.
- Creates a self-signed certificate `CN=SystemWidget Local` and adds it to the
  machine's trusted root store. Windows only grants `uiAccess` to a signed
  executable installed under `Program Files`, so both steps are mandatory for
  the widget to stay above the taskbar. **Adding a root certificate is not a
  trivial change** — see [Uninstall](#uninstall) to remove it.
- Copies the signed binary to `C:\Program Files\SystemWidget\` and starts it.

## Features

- **GPU W** — power draw as a percentage of the card's limit
- **VRAM** — video memory in use
- **CPU** — total processor usage
- **RAM** — physical memory in use
- Continuous colour gradient: green when idle, amber, then red near the limit
- **Gets out of the way of games**: hides itself while a full-screen app is in
  the foreground, including borderless-fullscreen, and stops re-asserting
  topmost so it cannot kick a game out of its display mode
- Hover any gauge for exact figures (watts, GB, engine load)
- Drag to move, position is remembered; adjustable opacity; optional start
  with Windows
- **English, Français, Español, Deutsch** — right-click → Language

## Requirements

- Windows 10 or 11
- .NET Framework 4.x (present on every supported Windows — nothing to install)
- **NVIDIA GPU** for the GPU and VRAM gauges: they read `nvidia-smi`, which
  ships with the NVIDIA driver. On any other card those two gauges show `--`;
  CPU and RAM keep working.

## How the readings are taken

| Gauge | Source |
|---|---|
| CPU | `GetSystemTimes`, sampled once per second |
| RAM | `GlobalMemoryStatusEx` |
| GPU W, VRAM | `nvidia-smi --query-gpu=...`, one hidden invocation per second |

No driver, no kernel module, no elevated helper service. The widget runs as
your own user and reads nothing else; there is no network access and no
telemetry.

## Adding a language

Everything lives in the `I18n` class in `SystemWidget.cs`. Copy one of the
`English()` / `French()` blocks, translate the values, and append it to
`Catalog`:

```csharp
public static readonly Strings[] Catalog = { English(), French(), Spanish(), German(), Italian() };
```

The language menu and the config file are both driven by `Catalog` — there is
nothing else to wire up. Save the file as **UTF-8 with a BOM**; pull requests
welcome.

## Troubleshooting

**GPU and VRAM show `--`.** `nvidia-smi` was not found or returned nothing.
The widget looks in `System32` and in `Program Files\NVIDIA Corporation\NVSMI`,
then falls back to `PATH`. On a non-NVIDIA card this is expected.

**The widget vanished.** A full-screen application is in the foreground; it
comes back on its own. Untick *Hide in full-screen apps* to keep it visible
over full-screen video, at the cost of it reappearing over games.

## Uninstall

1. Right-click the widget → *Quit*
2. Delete `C:\Program Files\SystemWidget`
3. Delete `%APPDATA%\SystemWidget`
4. Remove the certificate: `certlm.msc` → *Trusted Root Certification
   Authorities* → *Certificates* → delete **SystemWidget Local**, then do the
   same under *Trusted Publishers* and *Personal*

## License

MIT — see [LICENSE](LICENSE).
