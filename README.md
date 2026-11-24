# TouchpadGood

**English** | [한국어](docs/README.ko.md) | [日本語](docs/README.ja.md) | [中文 (简体)](docs/README.zh.md) | [Español](docs/README.es.md) | [Deutsch](docs/README.de.md) | [Français](docs/README.fr.md)

**Ultra-low-latency gaming & productivity Precision Touchpad driver for Windows 11.**
> *Made because default Windows touchpad settings were way too inconvenient — and yes, I also built this to play games on a touchpad.*

Pure Win32 headless daemon (`TouchpadGood.exe`) + Windows 11 Fluent Design Settings UI (`TouchpadGoodSettings.exe`) + All-in-One Installer (`TouchpadGoodSetup.exe`).

---

## Key Features

- **Zero Input Lag Architecture (`TouchpadGood.exe`)**
  - Pure Win32 headless daemon (`~55 KB`) — zero WinForms/GDI+, zero tray icon, zero `WH_MOUSE_LL` hooks, **0% idle CPU** (zero idle timers).
  - `/unsafe` zero-copy single-syscall `GetRawInputData` + direct `byte*` `hid.dll` parsing + stack-allocated `SendInput`.
  - Runs with `High` process priority, **MMCSS `"Games"`** thread scheduling, EcoQoS disabled, and `SustainedLowLatency` GC.
- **Native Windows 11 Integration**
  - 1-finger movement/tap, **Windows 11 native right-edge scroll (`SingleFingerPanMode = 2`)**, and 4-finger gestures remain 100% native OS kernel with zero overhead.
  - Hardware 1:1 linear tracking (disables pointer acceleration at OS kernel level).
  - **One-click "Auto-configure Windows settings"** button inside Settings automatically applies optimal Windows touchpad & mouse settings (acceleration OFF, zero typing delay, native right-edge scroll, 1-finger tap ON, non-conflicting multi-finger flags).
- **FPS Gaming & Customizable Multi-Finger Gestures**
  - **FPS Simultaneous Input (ADS + Shoot)**:
    - Start 3-finger Right Drag → release 1 finger (2 fingers stay in Right Drag / ADS) → tap or hold a 3rd finger for Left Click / Shoot while aiming.
    - Start 2-finger Left Drag → tap or hold a 3rd finger for Right Click / Right Drag simultaneously.
  - **Primary Aim-Finger Lock**:
    - Adding fingers while moving (`1 → 1+1` / `1 → 1+2`) locks pointer movement to the original moving finger; added fingers act strictly as triggers.
    - Fresh 2/3-finger drags (`0 → 2` / `0 → 3`) lock pointer movement to the index finger (leftmost-lowest contact).
  - **Zero-Freeze Handoff**: Smoothly bridges `2+ → 1` finger release transitions using auto-learned native 1-finger speed.
  - Global hotkey **`Ctrl + Alt + T`** to toggle anytime.
- **Windows 11 Fluent Design Settings UI (7 Languages & Auto-Update)**
  - Follows Windows 11 Dark / Light system theme automatically.
  - Custom Fluent scrollbar & custom borderless Fluent dropdowns unified with the canvas palette.
  - Live language switching without restart across 7 languages.
  - Built-in **Check for Updates & 1-click Auto-Update** directly from GitHub.

---

## Default Multi-Finger Gestures

| Gesture | Default Action |
| :--- | :--- |
| **1 Finger Move / Tap** | Native Windows (1:1 linear, accel OFF) |
| **Right-Edge 1-Finger Scroll** | Native Windows 11 (`SingleFingerPanMode = 2`) |
| **Bottom-Edge 1-Finger Swipe** | Smooth Horizontal Scroll (`WM_MOUSEHWHEEL`, 0 idle hooks) |
| **Right/Bottom Edge 2-Finger Pinch** | Zoom In / Zoom Out (`Ctrl + Wheel`) |
| **2 Fingers Drag** | Left Click Drag |
| **2 Fingers Tap (`0 → 2`)** | Right Click |
| **Moving 1 Finger + 1 Finger Tap (`1 → 2`)** | Left Click (or hold = Left Drag) |
| **3 Fingers Drag** | Right Click Drag |
| **3 Fingers Tap (`0 → 3`)** | Middle Click |
| **Moving 1 Finger + 2 Fingers Tap (`1 → 3`)** | Right Click (or hold = Right Drag) |
| **3F Right Drag → 2F Hold + 3rd Finger** | Simultaneous Right Drag + Left Click (FPS ADS + Shoot) |
| **2F Left Drag + 3rd Finger** | Simultaneous Left Drag + Right Click |
| **4 Fingers** | Native Windows |

*(All 2-finger and 3-finger actions can be freely customized in `TouchpadGoodSettings.exe`.)*

---

## Installation

### Option 1: All-in-One Installer (Recommended)
1. Download **[TouchpadGoodSetup.exe](https://github.com/wojo0821/TouchpadGood/releases/latest)** (also available directly via [raw/main/TouchpadGoodSetup.exe](https://github.com/wojo0821/TouchpadGood/raw/main/TouchpadGoodSetup.exe)).
2. Run `TouchpadGoodSetup.exe` and click **Install**.
   - Installs to `%LOCALAPPDATA%\Programs\TouchpadGood`.
   - Optionally auto-configures Windows 11 Precision Touchpad settings, registers elevated Task Scheduler auto-start (no UAC prompt at logon), and creates Start Menu / Desktop shortcuts.
   - Cleanly uninstallable anytime from **Windows Settings → Apps → TouchpadGood** or by re-running `TouchpadGoodSetup.exe` (automatically restores your original Windows touchpad settings).

### Option 2: Build from Source (No Visual Studio Required)
Requires Windows 10/11 (uses built-in `.NET Framework 4.x csc.exe`):

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
```

Compiles all three standalone x64 binaries:
- `TouchpadGood.exe` — Headless background driver
- `TouchpadGoodSettings.exe` — Fluent Design Settings UI
- `TouchpadGoodSetup.exe` — Standalone All-in-One Installer (embeds both binaries above)

---

## License

MIT License — see [LICENSE](LICENSE).
