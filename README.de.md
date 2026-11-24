# TouchpadGood

[English](README.md) | [한국어](README.ko.md) | [日本語](README.ja.md) | [中文 (简体)](README.zh.md) | [Español](README.es.md) | **Deutsch** | [Français](README.fr.md)

**Ultra-Low-Latency Gaming- & Produktivitäts-Precision-Touchpad-Treiber für Windows 11.**
Reiner Win32-Hintergrunddienst (`TouchpadGood.exe`) + Windows 11 Fluent Design Einstellungen (`TouchpadGoodSettings.exe`) + All-in-One Installer (`TouchpadGoodSetup.exe`).

---

## Hauptfunktionen

- **Zero-Input-Lag Win32-Architektur (`TouchpadGood.exe`)**
  - Schlanker Headless-Daemon (`~55 KB`) — kein WinForms/GDI+, kein Tray-Icon, 0 `WH_MOUSE_LL`-Hooks, **0 % Leerlauf-CPU**.
  - `/unsafe` Zero-Copy Single-Syscall `GetRawInputData` + direktes `byte*` `hid.dll`-Parsing + Stack-`SendInput`.
  - Prozesspriorität `High`, **MMCSS `"Games"`** Thread-Scheduling, EcoQoS deaktiviert, `SustainedLowLatency` GC.
- **Native Windows 11 Integration**
  - 1-Finger-Bewegung/Tippen, **natives Windows 11 Randscrollen rechts (`SingleFingerPanMode = 2`)** und 4-Finger-Gesten bleiben zu 100 % Windows-nativ.
  - Lineare 1:1-Zeigersteuerung (deaktiviert Zeigerbeschleunigung im OS-Kernel).
  - **1-Klick „Windows-Einstellungen automatisch optimieren“** konfiguriert Beschleunigung AUS, Tippverzögerung 0, Randscrollen und 1-Finger-Tippen automatisch.
- **FPS-Gaming & anpassbare Mehrfinger-Gesten**
  - **FPS-Simultaneingabe (Zielen + Schießen)**:
    - 3-Finger-Rechtsziehen starten → 1 Finger heben (2 Finger halten Rechtsziehen / Zielen) → 3. Finger tippen oder halten für Linksklick / Schießen während des Zielens.
    - 2-Finger-Linksziehen + 3. Finger tippen oder halten für gleichzeitigen Rechtsklick / Rechtsziehen.
  - **Zielfinger-Fixierung**:
    - Finger beim Bewegen hinzufügen (`1 → 1+1` / `1 → 1+2`) fixiert den Zeiger auf den ursprünglichen Finger.
    - Direktes 2/3-Finger-Ziehen (`0 → 2` / `0 → 3`) nutzt den Zeigefinger (links-unten) als Referenz.
  - Globaler Hotkey **`Strg + Alt + T`** zum sofortigen Umschalten.
- **Windows 11 Fluent Design UI (7 Sprachen & Auto-Update)**
  - Automatischer Dark-/Light-Modus, angepasste Fluent-Scrollbar und rahmenlose Fluent-Dropdowns.
  - Live-Sprachwechsel (7 Sprachen) und integrierter **GitHub-Updater** per Klick.

---

## Standard-Gesten

| Geste | Standardaktion |
| :--- | :--- |
| **1 Finger Bewegen / Tippen** | Windows-nativ (1:1 linear, Beschleunigung AUS) |
| **Randscrollen rechts (1 Finger)** | Windows 11 nativ (`SingleFingerPanMode = 2`) |
| **Randscrollen unten (1 Finger)** | Flüssiges horizontales Scrollen (`WM_MOUSEHWHEEL`, 0 Idle-Hooks) |
| **2-Finger-Pinch am rechten/unteren Rand** | Vergrößern / Verkleinern (`Ctrl + Wheel`) |
| **2 Finger Ziehen** | Links ziehen |
| **2 Finger Tippen (`0 → 2`)** | Rechtsklick |
| **1 Finger Bewegung + 1 Tipp (`1 → 2`)** | Linksklick (Halten = Links ziehen) |
| **3 Finger Ziehen** | Rechts ziehen |
| **3 Finger Tippen (`0 → 3`)** | Mittelklick |
| **1 Finger Bewegung + 2 Tipps (`1 → 3`)** | Rechtsklick (Halten = Rechts ziehen) |
| **3F Rechts → 2F halten + 3. Finger** | Rechts ziehen + Linksklick gleichzeitig (FPS) |
| **2F Links + 3. Finger** | Links ziehen + Rechtsklick gleichzeitig |
| **4 Finger** | Windows-nativ |

---

## Installation

### Option 1: All-in-One Installer (Empfohlen)
1. Lade **[TouchpadGoodSetup.exe](https://github.com/wojo0821/TouchpadGood/releases/latest)** herunter ([Direktdownload](https://github.com/wojo0821/TouchpadGood/raw/main/TouchpadGoodSetup.exe)).
2. Starte `TouchpadGoodSetup.exe` und klicke auf **Installieren**.
   - Installiert nach `%LOCALAPPDATA%\Programs\TouchpadGood` und stellt bei der Deinstallation automatisch die Windows-Standardwerte wieder her.

### Option 2: Aus dem Quellcode kompilieren (Kein Visual Studio nötig)
```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
```

---

## Lizenz

MIT-Lizenz — siehe [LICENSE](LICENSE).
