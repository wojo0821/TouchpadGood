# TouchpadGood

[English](README.md) | [한국어](README.ko.md) | [日本語](README.ja.md) | **中文 (简体)** | [Español](README.es.md) | [Deutsch](README.de.md) | [Français](README.fr.md)

**面向 Windows 11 的超低延迟游戏与效率精密触控板自定义驱动**
纯 Win32 无托盘后台守护程序 (`TouchpadGood.exe`) + Windows 11 Fluent Design 设置界面 (`TouchpadGoodSettings.exe`) + 一体化安装程序 (`TouchpadGoodSetup.exe`)

---

## 核心特性

- **零输入延迟的纯 Win32 后台架构 (`TouchpadGood.exe`)**
  - 仅约 `55 KB` 的轻量级纯 Win32 后台进程 —— 零 WinForms/GDI+、无托盘图标、零 `WH_MOUSE_LL` 钩子、**空闲 CPU 占用 0%**。
  - `/unsafe` Zero-Copy 单次系统调用 `GetRawInputData` + `byte*` 直接解析 `hid.dll` + 栈分配 `SendInput`。
  - 进程 `High` 优先级、**MMCSS `"Games"`** 线程调度、关闭 EcoQoS 节流、`SustainedLowLatency` GC。
- **与 Windows 11 原生体验深度融合**
  - 单指移动/轻触、**Windows 11 原生右侧边缘滚动 (`SingleFingerPanMode = 2`)**、四指手势完全保留系统内核原生处理，无卡顿无延迟。
  - 内核级 1:1 线性追踪（彻底关闭指针加速）。
  - 设置界面内置 **一键“自动优化 Windows 设置”** 按钮，自动配置关闭加速、零打字延迟、右侧边缘滚动与单指轻触。
- **FPS 游戏同时输入与可自定义多指手势**
  - **FPS 同时输入（瞄准 + 射击）**：
    - 三指右键拖动开始 → 抬起一指后双指继续保持右键拖动（开镜瞄准）→ 第三指轻触或按住即可在瞄准的同时触发左键射击。
    - 双指左键拖动期间 → 第三指轻触或按住即可同时触发右键点击/拖动。
  - **锁定主瞄准手指**：
    - 移动中追加手指 (`1 → 1+1` / `1 → 1+2`) 时仅由原本移动的手指控制指针，新放上的手指只作为按键触发器。
    - 直接双指/三指拖动 (`0 → 2` / `0 → 3`) 时以食指（最左下方触点）为指针基准。
  - 全局快捷键 **`Ctrl + Alt + T`** 随时一键开关。
- **Windows 11 Fluent Design 设置界面（7 国语言 · 自动更新）**
  - 自动跟随系统深色/浅色主题，配备与界面配色统一的自定义 Fluent 滚动条和无边框下拉菜单。
  - 支持 7 种语言免重启实时切换，内置 GitHub **一键检查与自动更新**。

---

## 默认多指手势

| 手势 | 默认功能 |
| :--- | :--- |
| **单指移动 / 轻触** | Windows 原生（关闭加速，1:1 线性） |
| **右侧边缘单指滚动** | Windows 11 原生 (`SingleFingerPanMode = 2`) |
| **底部边缘单指左右滑动** | 平滑横向滚动 (`WM_MOUSEHWHEEL`，平时 0 钩子) |
| **右侧/底部边缘双指缩放** | 放大 / 缩小 (`Ctrl + Wheel`) |
| **双指拖动** | 左键拖动 |
| **双指轻触 (`0 → 2`)** | 右键点击 |
| **单指移动中 + 1指轻触 (`1 → 2`)** | 左键点击（按住为左键拖动） |
| **三指拖动** | 右键拖动 |
| **三指轻触 (`0 → 3`)** | 中键点击（滚轮点击） |
| **单指移动中 + 2指轻触 (`1 → 3`)** | 右键点击（按住为右键拖动） |
| **三指右拖 → 双指保持 + 第三指** | 右键拖动 + 左键同时输入（FPS 瞄准+射击） |
| **双指左拖 + 第三指** | 左键拖动 + 右键同时输入 |
| **四指手势** | Windows 原生 |

*（所有双指和三指手势均可在 `TouchpadGoodSettings.exe` 中自由修改。）*

---

## 安装方式

### 方式 1：使用一体化安装程序（推荐）
1. 前往 [Releases 最新版本](https://github.com/wojo0821/TouchpadGood/releases/latest) 下载 **`TouchpadGoodSetup.exe`**（或[直接下载](https://github.com/wojo0821/TouchpadGood/raw/main/TouchpadGoodSetup.exe)）。
2. 运行 `TouchpadGoodSetup.exe` 并点击 **开始安装**。
   - 默认安装至 `%LOCALAPPDATA%\Programs\TouchpadGood`。
   - 支持一键优化 Windows 触控板设置、注册无 UAC 弹窗的管理员开机自启（任务计划程序）及快捷方式。
   - 可随时在 **Windows 设置 → 应用** 中彻底卸载，并自动恢复 Windows 原始触控板设置。

### 方式 2：从源码构建（无需安装 Visual Studio）
```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
```

---

## 开源许可

MIT License — 详见 [LICENSE](LICENSE)。
