# System Widget

A tiny always-on-top gauge for Windows showing GPU power draw, VRAM, CPU and
RAM at a glance.

*Read this in [Français](README.fr.md).*

<img src="docs/screenshot.png" alt="The widget showing GPU power, VRAM, CPU and RAM gauges" width="546">

It sits above the taskbar and never disappears behind it, because the
executable is built with the `uiAccess` privilege — the same one the Magnifier
and the on-screen keyboard use. It re-asserts topmost only when the taskbar
has actually covered it, instead of twice a second — no flicker.

**Temperatures**: the GPU thermometer shows the NVIDIA core temperature (via
`nvidia-smi`, no driver involved). The CPU thermometer reads the CPU's own
sensor ("CPU Package", or Tctl/Tdie on AMD) through the embedded
[LibreHardwareMonitor](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor)
0.9.6 (MIT) — the engine behind most sensor tools. Reading that sensor means
talking to the CPU directly, which no user-mode program may do, so a kernel
driver is required; see [The CPU sensor driver](#the-cpu-sensor-driver) below.
The read also needs administrator rights: the installer starts the widget
elevated, and *Start with Windows* registers a scheduled task with highest
privileges, so no UAC prompt appears at logon. Without either the driver or
those rights the thermometer stays blank rather than showing a made-up
number. The shipped binaries live in `lib/` (LibreHardwareMonitor and its
runtime dependencies, MIT) — everything else builds from source.

## Install

1. [**Download the repository as a ZIP**](https://github.com/Defacedz/system-usage-widget/archive/refs/heads/main.zip)
   (or *Code → Download ZIP* at the top of this page)
2. Extract it anywhere
3. Double-click **`Installer.bat`** and accept the administrator prompt

Three clicks, nothing to type, and you can read every line before running it —
the sensible habit for a program that installs a driver.

Closed the widget by hand? Double-click **`Launch.bat`** to bring it back — no
reinstall needed. If it is not installed yet, it offers to run the installer.

Updating later takes one click: the widget watches this repository, turns its
border orange when a newer version exists, and *Update available* in the
right-click menu downloads and installs it.

<details>
<summary>One-line install (usually blocked by Defender)</summary>

```powershell
irm https://raw.githubusercontent.com/Defacedz/system-usage-widget/main/web-install.ps1 | iex
```

Recent Microsoft Defender builds refuse to run this: download-and-execute in
one line is the exact command shape of a malware dropper, so it is killed
before it starts (`Trojan:Win32/Commando.A!ml`; PowerShell just reports
*Access denied*). The detection is about the shape of the command, not about
what it downloads — nothing is wrong with your machine. Use the ZIP above
instead.

</details>

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
- **Two themes** — right-click → Theme: the original *Dark*, or *Ivory*,
  built on Anthropic's palette so the panel sits on a light Windows taskbar
  instead of punching a black hole in it
- **Built-in updates** — the widget checks this repository every 6 hours, and
  on every *Refresh* click; when a newer version is published the border turns
  Claude-orange and an *Update available* entry appears at the top of the
  right-click menu

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

Those four readings use documented Windows APIs and `nvidia-smi` only. The
CPU **temperature** is the one exception, and it needs a kernel driver:

## The CPU sensor driver

CPU temperature lives in a register only kernel code may read. LibreHardwareMonitor
0.9.6 reads it through [PawnIO](https://pawnio.eu), a signed driver that runs
small verified modules instead of handing ring 0 to whoever asks. **The
installer downloads and installs it for you**, from its official release, after
checking the exact SHA-256 of the file and its Authenticode signature. If that
fails, installation carries on and the CPU thermometer simply stays blank.

PawnIO is a separate program: remove it from *Settings → Apps* like any other.
The widget keeps working without it.

**If you installed a build from before September 2026**, it embedded
LibreHardwareMonitor 0.9.3, which uses **WinRing0** — a driver on Microsoft's
vulnerable-driver blocklist ([CVE-2020-14979](https://nvd.nist.gov/vuln/detail/CVE-2020-14979):
any local program can reach ring 0 through it), which recent Defender builds
flag as `VulnerableDriver:WinNT/Winring0`. Updating removes it: the installer
stops and deletes the `R0SystemWidget` service and its `SystemWidget.sys`
file. That is why this change exists.

Apart from that driver the widget runs as your own user, reads nothing else,
accesses no network and sends no telemetry.

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
then falls back to `PATH`. On a non-NVIDIA card this is expected. Hover the
GPU gauge: the tooltip carries the exact reason, including what `nvidia-smi`
answered.

**The GPU gauge shows a percentage but no wattage in its tooltip.** That
card does not report `power.draw` — common on laptop GPUs. The gauge falls
back to the engine load, and the tooltip says so. Every other field is read
independently, so a missing one never blanks the rest.

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
