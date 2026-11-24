# TouchpadGood

[English](README.md) | [한국어](README.ko.md) | **日本語** | [中文 (简体)](README.zh.md) | [Español](README.es.md) | [Deutsch](README.de.md) | [Français](README.fr.md)

**Windows 11向け 超低遅延ゲーミング＆作業用 高精度タッチパッドカスタムドライバー**
純粋Win32常駐デーモン (`TouchpadGood.exe`) + Windows 11 Fluent Design 設定UI (`TouchpadGoodSettings.exe`) + オールインワンインストーラー (`TouchpadGoodSetup.exe`)

---

## 主な機能

- **入力遅延ゼロ志向の純粋Win32アーキテクチャ (`TouchpadGood.exe`)**
  - 約 `55 KB` の超軽量ヘッドレス常駐プロセス — WinForms/GDI+ 不使用、トレイアイコンなし、`WH_MOUSE_LL` フック 0個、**待機時CPU使用率 0%**。
  - `/unsafe` Zero-Copy 単一システムコール `GetRawInputData` + `byte*` 直接 `hid.dll` パース + スタック割り当て `SendInput`。
  - プロセス優先度 `High`、**MMCSS `"Games"`** スレッドスケジューリング、EcoQoS無効化、`SustainedLowLatency` GC を採用。
- **Windows 11 ネイティブ機能との完全統合**
  - 1本指の移動・タップ、**Windows 11標準の右端スクロール (`SingleFingerPanMode = 2`)**、4本指ジェスチャーはOS標準のまま維持し、遅延やカクつきを防ぎます。
  - OSカーネルレベルの1:1リニアトラッキング（加速OFF）。
  - 設定画面の **ワンクリック「Windows設定を自動最適化」** ボタンで、加速OFF・タイピング遅延0・右端スクロール・1本指タップを一括適用できます。
- **FPSゲーミング同時入力 & マルチフィンガーカスタマイズ**
  - **FPS同時入力（エイム＋射撃）**:
    - 3本指右ドラッグ開始 → 1本離しても2本指で右ドラッグ（エイム）を維持 → 3本目の指をタップ／長押しして左クリック（射撃）を同時入力。
    - 2本指左ドラッグ中に3本目の指をタップ／長押しして右クリック／右ドラッグを同時入力。
  - **基準指ポインター固定**:
    - 移動中の指追加 (`1 → 1+1` / `1 → 1+2`) では最初に動かしていた指だけをポインター基準にし、追加指はトリガー専用として動作。
    - 最初から2/3本指ドラッグ (`0 → 2` / `0 → 3`) を始めた場合は人差し指（最も左下の指）を基準にします。
  - グローバルホットキー **`Ctrl + Alt + T`** でいつでもON/OFF切替可能。
- **Windows 11 Fluent Design 設定UI（7言語・自動アップデート対応）**
  - Windows ダーク／ライトテーマに自動追従し、テーマに統一されたカスタムFluentスクロールバーとドロップダウンを採用。
  - 再起動不要のリアルタイム7言語切替と、GitHub連携の **ワンクリック自動アップデート** を内蔵。

---

## デフォルトのマルチフィンガージェスチャー

| ジェスチャー | デフォルト動作 |
| :--- | :--- |
| **1本指 移動 / タップ** | Windows標準（加速OFF・1:1リニア） |
| **右端 1本指スクロール** | Windows 11標準 (`SingleFingerPanMode = 2`) |
| **下端 1本指左右スライド** | スムーズ横スクロール (`WM_MOUSEHWHEEL`、通常時フック0) |
| **2本指 ドラッグ** | 左ドラッグ |
| **2本指 タップ (`0 → 2`)** | 右クリック |
| **1本指移動中 + 1本指タップ (`1 → 2`)** | 左クリック（保持で左ドラッグ） |
| **3本指 ドラッグ** | 右ドラッグ |
| **3本指 タップ (`0 → 3`)** | ホイールクリック（中クリック） |
| **1本指移動中 + 2本指タップ (`1 → 3`)** | 右クリック（保持で右ドラッグ） |
| **3本指右ドラッグ → 2本指維持 + 3本目** | 右ドラッグ＋左クリック同時入力（FPSエイム＋射撃） |
| **2本指左ドラッグ + 3本目** | 左ドラッグ＋右クリック同時入力 |
| **4本指ジェスチャー** | Windows標準 |

*（2本指・3本指の割り当ては `TouchpadGoodSettings.exe` で自由に変更できます。）*

---

## インストール方法

### 方法 1: オールインワンインストーラー（推奨）
1. [最新リリースページ](https://github.com/wojo0821/TouchpadGood/releases/latest) から **`TouchpadGoodSetup.exe`** をダウンロードします（[直接ダウンロード](https://github.com/wojo0821/TouchpadGood/raw/main/TouchpadGoodSetup.exe)）。
2. `TouchpadGoodSetup.exe` を実行し、**インストール** をクリックします。
   - `%LOCALAPPDATA%\Programs\TouchpadGood` にインストールされます。
   - Windows設定の自動最適化、サインイン時のUACなし管理者自動起動（タスクスケジューラ）、ショートカット作成を一括で行えます。
   - アンインストール時は元のWindowsタッチパッド設定を自動で復元します。

### 方法 2: ソースからビルド（Visual Studio不要）
```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
```

---

## ライセンス

MIT License — 詳細は [LICENSE](LICENSE) をご覧ください。
