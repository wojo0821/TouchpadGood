# TouchpadGood

**Ultra-low-latency gaming & productivity Precision Touchpad driver for Windows 11.**
Pure Win32 headless daemon (`TouchpadGood.exe`) + Windows 11 Fluent Design Settings UI (`TouchpadGoodSettings.exe`) + All-in-One Installer (`TouchpadGoodSetup.exe`).

> 한국어 안내도 아래에 포함되어 있습니다. / Korean guide included below.

---

## Key Features

- **Zero Input Lag Architecture (`TouchpadGood.exe`)**
  - Pure Win32 headless daemon (`~44 KB`) — zero WinForms/GDI+, zero tray icon, zero `WH_MOUSE_LL` hooks, **0% idle CPU** (zero idle timers).
  - `/unsafe` zero-copy single-syscall `GetRawInputData` + direct `byte*` `hid.dll` parsing + stack-allocated `SendInput`.
  - Runs with `High` process priority, **MMCSS `"Games"`** thread scheduling, EcoQoS disabled, and `SustainedLowLatency` GC.
- **Native Windows 11 Integration**
  - 1-finger movement/tap, **Windows 11 native right-edge scroll (`SingleFingerPanMode = 2`)**, and 4-finger gestures stay 100% native OS kernel with zero overhead.
  - Hardware 1:1 linear tracking (disables pointer acceleration at OS kernel level).
  - **One-click "Auto-configure Windows settings"** button inside Settings automatically applies optimal Windows touchpad & mouse settings (acceleration OFF, zero typing delay, native right-edge scroll, 1-finger tap ON, non-conflicting multi-finger flags).
- **FPS Gaming & Customizable Multi-Finger Gestures**
  - **FPS Simultaneous Input (ADS + Shoot)**:
    - Start 3-finger Right Drag → release 1 finger (2 fingers stay in Right Drag / ADS) → tap or hold a 3rd finger for Left Click / Shoot while aiming!
    - Start 2-finger Left Drag → tap or hold a 3rd finger for Right Click / Right Drag simultaneously!
  - **Primary Aim-Finger Lock**:
    - Adding fingers while moving (`1 → 1+1` / `1 → 1+2`) locks pointer movement to the original moving finger; added fingers act strictly as triggers.
    - Fresh 2/3-finger drags (`0 → 2` / `0 → 3`) lock pointer movement to the index finger (leftmost-lowest contact).
  - **Zero-Freeze Handoff**: Smoothly bridges `2+ → 1` finger release transitions using auto-learned native 1-finger speed.
  - Global hotkey **`Ctrl + Alt + T`** to toggle anytime.
- **Windows 11 Fluent Design Settings UI (7 Languages)**
  - Follows Windows 11 Dark / Light system theme automatically.
  - Custom Fluent scrollbar & custom borderless Fluent dropdowns unified with the canvas palette.
  - Live language switching without restart: **한국어, English, 日本語, 中文 (简体), Español, Deutsch, Français**.

---

## Default Multi-Finger Gestures

| Gesture | Default Action |
| :--- | :--- |
| **1 Finger Move / Tap** | Native Windows (1:1 linear, accel OFF) |
| **Right-Edge 1-Finger Scroll** | Native Windows 11 (`SingleFingerPanMode = 2`) |
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
1. Download **`TouchpadGoodSetup.exe`** from [Releases](../../releases).
2. Run `TouchpadGoodSetup.exe` and click **Install**.
   - Installs to `%LOCALAPPDATA%\Programs\TouchpadGood`.
   - Optionally auto-configures Windows 11 Precision Touchpad settings, registers elevated Task Scheduler auto-start (no UAC prompt at logon), and creates Start Menu / Desktop shortcuts.
   - Cleanly uninstallable anytime from **Windows Settings → Apps → TouchpadGood** or by re-running `TouchpadGoodSetup.exe` (automatically restores your original Windows touchpad settings).

### Option 2: Build from Source (No Visual Studio Required)
Requires Windows 10/11 (uses built-in `.NET Framework 4.x csc.exe`):

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
```

This compiles all three standalone x64 binaries:
- `TouchpadGood.exe` — Headless background driver
- `TouchpadGoodSettings.exe` — Fluent Design Settings UI
- `TouchpadGoodSetup.exe` — Standalone All-in-One Installer (embeds both binaries above)

---

## 한국어 요약

**TouchpadGood**은 Windows 11 Precision Touchpad를 게이밍·작업 환경에 맞게 초저지연으로 커스터마이징하는 순수 Win32 백그라운드 드라이버입니다.

- **인풋렉 최소화**: 순수 Win32 무트레이 상주 (`TouchpadGood.exe`, ~44 KB), 후크 0개, 대기 CPU 0%, `MMCSS Games` 우선순위, `/unsafe` Zero-Copy RawInput 처리.
- **Windows 설정 자동 최적화 버튼**: `TouchpadGoodSettings.exe`에서 버튼 한 번으로 가속 끄기, 타이핑 지연 0, 우측 가장자리 네이티브 스크롤, 1손가락 탭 설정을 TouchpadGood에 맞게 자동 구성.
- **7개 국어 UI 지원**: 한국어, English, 日本語, 中文(简体), Español, Deutsch, Français 실시간 전환.
- **통합 설치 프로그램**: `TouchpadGoodSetup.exe` 하나로 설치, 자동 시작 등록, Windows 최적화, 제거(Windows 원래 설정 자동 복구)까지 한 번에 처리합니다.

---

## License

MIT License — see [LICENSE](LICENSE).
