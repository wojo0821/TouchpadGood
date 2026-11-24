# Changelog

All notable changes to **TouchpadGood** are documented per release below.

## v1.0.6
### What's New & Improvements
- **Universal Smooth Real-Mouse Edge Scrolling (`±120 WHEEL_DELTA`)**: Both right-edge vertical scroll and bottom-edge horizontal scroll now emit authentic standard mouse `±120` wheel ticks via the zero-deadlock dedicated hook thread (`0` idle hooks, `0%` idle CPU), eliminating per-app stutter in classic Win32/WinForms/Office/Explorer apps and `TouchpadGoodSettings` itself.
- **Windows 11 26H2 Live Kernel `TOUCHPAD_PARAMETERS_V3` Sync**: Live-disables Windows `singleFingerPanMode` (`0`) in the kernel while active (with V1 fallback and full restore on exit) and blocks competing OS wheel events during edge scrolling, completely eliminating scroll bounce-back/shuddering.
- **Slimmer Edge Scroll Zones & Vertical Scroll Invert Toggle**: Tightened right-edge vertical strip to the outer `4.5%` (`x >= 0.955`) and bottom-edge horizontal strip to the outer `7.5%` (`y >= 0.925`), set natural vertical scrolling as the default, and added a 7-language **Invert vertical scroll direction** toggle in Settings.
- **Improved 2-Finger Right-Click Tap Reliability**: Removed the stationary 1-finger `60ms` false `1+1` trigger (now requires real 1-finger movement `>= 0.012`) and added a 2-tier `55ms` net-displacement guard (`0.0085` early / `0.0060` late) so 2-finger right-click taps fire reliably while intentional drags stay snappy.
- **Instant Administrator Elevation for Task Manager & Admin Windows**: Starting/restarting the driver from Settings or launching after Install/Auto-Update now runs the registered elevated Task Scheduler task (`schtasks /Run /TN "TouchpadGood"`) with zero UAC prompts so Task Manager and other elevated windows drag immediately.
- **Sub-Pixel Wheel Accumulation in `FluentScrollPanel`**: Fixed `TouchpadGoodSettings.exe` dropping fractional high-resolution touchpad wheel deltas (`delta / 120`).

### 변경 사항 (한국어)
- **모든 프로그램에서 마우스처럼 부드러운 가장자리 스크롤 (`±120` 표준 휠)**: 우측 세로 스크롤과 하단 가로 스크롤 모두 데드락 없는 전용 후크 스레드(평상시 후크 0개·대기 CPU 0%)로 실제 마우스 표준 `±120` 휠 신호를 전송해, `TouchpadGoodSettings`를 비롯한 모든 프로그램에서 끊김 없이 작동합니다.
- **Windows 11 26H2 커널 `TOUCHPAD_PARAMETERS_V3` 실시간 동기화**: OS 내장 `SingleFingerPanMode`를 커널에서 실시간 해제(종료·복구 시 원상 복원)하고 중복 OS 휠 신호를 차단해 스크롤 시 되돌아오며 떨리는 현상을 완전히 제거했습니다.
- **가장자리 스크롤 폭 슬림화 & 세로 스크롤 방향 반전 스위치**: 우측 4.5%(`x >= 0.955`)·하단 7.5%(`y >= 0.925`)로 가장자리 판정을 정밀하게 좁히고, 자연 스크롤을 기본값으로 적용하며 7개 국어 설정 창에 **세로 스크롤 방향 반전** 토글을 추가했습니다.
- **2손가락 탭 우클릭 판정 개선**: 정지 1손가락의 `1+1` 오인 버그를 제거하고 2단계(`55ms`) 순수 이동 거리 가드를 적용해 우클릭 탭 성공률과 드래그 반응성의 균형을 맞췄습니다.
- **작업 관리자 등 관리자 창 드래그 즉시 지원**: 설정 창 시작/관리자 재시작 및 설치·업데이트 직후 작업 스케줄러를 통해 UAC 창 없이 즉시 관리자 권한으로 실행되도록 개선했습니다.

---

## v1.0.5
### What's New
- **Right & Bottom Scroll-Edge 2-Finger Pinch-to-Zoom**: Pinch or spread two fingers inside the right-edge (`midX >= 0.78`) or bottom-edge (`midY >= 0.76`) scroll zones to zoom in/out (`Ctrl + Wheel`).
- **Physical Aspect-Ratio Calibration**: Vertical pinching on the right edge and horizontal pinching on the bottom edge share the exact same millimeter sensitivity.
- **Atomic `Ctrl + Wheel` Input**: Sends `Ctrl Down → Wheel(±120) → Ctrl Up` in a single atomic `SendInput` batch so `Ctrl` can never get stuck.
- **No Conflict with Normal 2-Finger Gestures**: Center-of-pad 2-finger Left Drag / Right Click Tap remains 0ms instant; parallel 2-finger drags near the edge still automatically hand off to 2-finger drag.
- **Settings UI Toggle**: Added **Edge scroll-zone pinch to zoom** toggle across all 7 languages.

### 변경 사항 (한국어)
- **우측·하단 스크롤 구간 2손가락 확대/축소 추가**: 우측 또는 하단 모서리 스크롤 구간에서 두 손가락을 벌리거나 오므리면 화면 확대/축소(`Ctrl + Wheel`)가 작동합니다.
- **물리 비율 보정 & 원자적 `Ctrl + Wheel` 전송**: 우측(세로)·하단(가로) 어느 방향에서나 동일한 감도로 동작하며, 단일 `SendInput` 배치 전송으로 `Ctrl` 키 눌림 먹통을 원천 차단했습니다.
- **기존 제스처 무충돌 & 설정 스위치 추가**: 중앙 영역 2손가락 드래그·탭과 충돌하지 않으며, 7개 국어 설정 창에 켜기/끄기 토글을 추가했습니다.

---

## v1.0.4
### Bug Fixes & Improvements
- **Fixed Bottom-Edge Horizontal Scroll Stutter & Post-Lift Lag**: Separated the on-demand `WH_MOUSE_LL` pointer-lock hook onto its own dedicated lightweight Win32 thread (`TouchpadGood_EdgeHook`), completely eliminating same-thread `WH_MOUSE_LL` ↔ `SendInput` `LowLevelHooksTimeout` stalls (~200–300ms).
- **60Hz Lossless `WM_MOUSEHWHEEL` Coalescing**: Paces horizontal wheel output at `14ms` intervals while accumulating every raw delta losslessly, preventing browser/Explorer scroll queue buildup so scrolling stops on the exact millisecond you lift your finger.
- **Removed `SetCursorPos` Ping-Pong**: Eliminated cursor feedback loops while maintaining 0 idle hooks and `0%` idle CPU.

### 변경 사항 (한국어)
- **가로 스크롤 끊김 및 손 뗀 뒤 지연 스크롤 완전 해결**: 임시 포인터 고정 후크(`WH_MOUSE_LL`)를 전용 초경량 스레드(`TouchpadGood_EdgeHook`)로 분리해 `SendInput` 동일 스레드 데드락(`LowLevelHooksTimeout`)을 0ms로 제거했습니다.
- **60Hz 무손실 가로 스크롤 병합 (`14ms` 페이싱)**: 앱 메시지 큐 적체를 방지해 손을 떼는 즉시 정확히 멈추며, 평상시 후크 0개·대기 CPU 0%를 유지합니다.

---

## v1.0.3
### Bug Fixes
- **Fixed Pointer Drift During Bottom-Edge Horizontal Scroll**: Precision Touchpad cursor events carry `LLMHF_INJECTED`; updated hook filtering to block all non-TouchpadGood `WM_MOUSEMOVE` events (`dwExtraInfo != ExtraInfoPtr`) so the cursor stays locked while scrolling.
- **Fixed Very-Bottom Bezel Detection**: Added a single-finger bottom-strip exception for touchpad firmware `Confidence = false` edge-curtain rejection (`ny >= 0.80`) and widened the bottom scroll zone to the bottom 18% (`ny >= 0.82`).
- **Higher-Resolution Horizontal Scroll**: Upgraded from coarse 30-unit steps to fine continuous wheel units.

### 변경 사항 (한국어)
- **가로 스크롤 중 포인터 동시 이동 버그 수정**: Windows 정밀 터치패드(PTP) 커서 이동 이벤트의 `LLMHF_INJECTED` 특성을 반영해 가로 스크롤 중 포인터를 확실히 고정했습니다.
- **터치패드 맨 아래쪽 끝 인식 불가 수정**: 맨 하단 베젤 부근 펌웨어 `Confidence = false` 차단을 1손가락 하단 구간에서 예외 처리하고 감지 영역을 하단 18%로 확장했습니다.

---

## v1.0.2
### What's New
- **Bottom-Edge Horizontal Scroll**: Slide one finger left/right along the bottom edge of the touchpad for horizontal scrolling (`WM_MOUSEHWHEEL`) without installing any permanent hooks (0 hooks during normal movement and gaming).
- **Settings UI Toggle**: Added **Bottom-edge horizontal scroll** toggle across all 7 supported languages.

### 변경 사항 (한국어)
- **하단 가장자리 가로 스크롤 추가**: 터치패드 아래쪽 가장자리를 한 손가락으로 좌우로 밀면 가로 스크롤(`WM_MOUSEHWHEEL`)이 작동합니다(평상시 후크 0개 유지).
- **설정 스위치 추가**: 7개 국어 설정 창에 **하단 가장자리 가로 스크롤** 켜기/끄기 토글을 추가했습니다.

---

## v1.0.1
### What's New
- **Built-in GitHub Update Checker & 1-Click Auto-Updater**: Check for updates directly inside `TouchpadGoodSettings.exe` (TLS 1.2 enabled) and seamlessly update in place via `TouchpadGoodSetup.exe --update` while keeping all settings intact.
- **7-Language Documentation & Automated GitHub Releases**: Added dedicated localized READMEs (English, Korean, Japanese, Simplified Chinese, Spanish, German, French) and GitHub Actions automated release builds.

### UI & Release Hardening
- **Pixel-Aligned Vector Status Dot**: Replaced font glyph `"●"` with custom anti-aliased `FluentStatusDot` and widened the hero subtitle to prevent text clipping.
- **Official Win32 Manifest, Version Info & Multi-Resolution Icon**: Embedded `PerMonitorV2` DPI manifest, FileVersion metadata, and custom `TouchpadGood.ico` into all three binaries.
- **Crash & Elevation Safety**: Added global crash handlers (immediately releasing any held mouse buttons and logging to `%LOCALAPPDATA%\TouchpadGood\crash.log`), `UnauthorizedAccessException` mutex handling when mixing Admin/Standard launches, high-DPI window height clamping for 1080p laptops, clean `WM_ENDSESSION` shutdown handling, and file-lock retry loops during updates.

### 변경 사항 (한국어)
- **GitHub 업데이트 확인 및 원클릭 자동 업데이트 추가**: 설정 앱에서 최신 버전을 확인하고 기존 설정을 유지한 채 원클릭으로 자동 업데이트할 수 있습니다.
- **UI 정렬 & 출시 안정성 하드닝**: 초록색 상태 원 벡터 정렬(`FluentStatusDot`) 및 텍스트 잘림 수정, 공식 Win32 매니페스트·버전 정보·멀티 해상도 아이콘 내장, 크래시 시 버튼 먹통 방지 및 로그 저장, 관리자/일반 권한 중복 실행 크래시 방지, 고DPI 노트북 화면 넘침 방지, 7개 언어별 README 추가.

---

## v1.0.0
### Initial Release
- **Pure Win32 Headless Driver (`TouchpadGood.exe`)**: Zero WinForms/GDI+, zero tray icon, zero `WH_MOUSE_LL` hooks, `0%` idle CPU, `/unsafe` zero-copy RawInput parsing, `High` priority + MMCSS `"Games"`.
- **Customizable Multi-Finger Gestures & FPS Simultaneous Input**: 2F Left Drag, 2F Right Click Tap, 1+1 Left Click, 3F Right Drag, 3F Middle Click Tap, 1+2 Right Click, FPS ADS+Shoot latch (`3F RightDrag → 2F hold + 3rd Left`), `2F LeftDrag + 3rd Right`, primary aim-finger lock, and zero-freeze `2+ → 1` handoff.
- **Windows 11 Fluent Design Settings UI (`TouchpadGoodSettings.exe`)**: Auto Dark/Light theme, custom unified Fluent scrollbar & borderless dropdowns, 7-language live switching, and 1-click **Auto-configure Windows settings** button.
- **All-in-One Installer (`TouchpadGoodSetup.exe`)**: Single-file installer & uninstaller with Windows auto-configuration, elevated Task Scheduler auto-start (no UAC prompts), and full Windows default restoration on uninstall.

### 변경 사항 (한국어)
- **초기 정식 출시**: 순수 Win32 무트레이 초저지연 드라이버(`TouchpadGood.exe`), FPS 동시 입력·커스텀 멀티핑거 제스처, Windows 11 Fluent Design 7개 국어 설정 앱(`TouchpadGoodSettings.exe`), Windows 설정 자동 최적화 버튼, 통합 설치·제거 프로그램(`TouchpadGoodSetup.exe`).
