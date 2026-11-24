# TouchpadGood

[English](README.md) | **한국어** | [日本語](README.ja.md) | [中文 (简体)](README.zh.md) | [Español](README.es.md) | [Deutsch](README.de.md) | [Français](README.fr.md)

**Windows 11 초저지연 게이밍 · 작업용 정밀 터치패드(Precision Touchpad) 커스텀 드라이버**
순수 Win32 무트레이 백그라운드 드라이버(`TouchpadGood.exe`) + Windows 11 Fluent Design 설정 앱(`TouchpadGoodSettings.exe`) + 통합 설치 프로그램(`TouchpadGoodSetup.exe`)

---

## 주요 기능

- **인풋렉 0ms 지향 순수 Win32 백그라운드 아키텍처 (`TouchpadGood.exe`)**
  - 약 `55 KB` 초경량 순수 Win32 상주 데몬 — WinForms/GDI+ 미사용, 트레이 아이콘 없음, `WH_MOUSE_LL` 후크 0개, **대기 CPU 사용률 0%** (상시 타이머 없음).
  - `/unsafe` Zero-Copy 단일 시스템콜 `GetRawInputData` + 직접 `byte*` `hid.dll` 파싱 + 스택 할당 `SendInput`.
  - 프로세스 `High` 우선순위, **MMCSS `"Games"`** 스레드 스케줄링, EcoQoS 해제, `SustainedLowLatency` GC 적용.
- **Windows 11 네이티브 긴밀 통합**
  - 1손가락 이동·탭, **Windows 11 기본 우측 가장자리 스크롤(`SingleFingerPanMode = 2`)**, 4손가락 제스처는 OS 커널 기본 기능을 그대로 유지해 반응속도 손실과 렉이 전혀 없습니다.
  - OS 커널 레벨 1:1 선형 모드(포인터 가속 완전 끄기).
  - 설정 창 내 **원클릭 "Windows 설정 자동 최적화"** 버튼으로 가속 끄기, 타이핑 지연 0, 우측 스크롤, 1손가락 탭 설정을 TouchpadGood에 맞게 한 번에 구성할 수 있습니다.
- **FPS 게이밍 동시 입력 및 자유로운 멀티핑거 커스터마이징**
  - **FPS 동시 입력 (조준 + 사격)**:
    - 3손가락 우클릭 드래그 시작 → 1손가락을 떼어도 2손가락으로 우클릭 드래그(조준) 유지 → 3번째 손가락을 탭하거나 꾹 누르면 조준하면서 동시에 좌클릭(사격) 입력.
    - 2손가락 좌클릭 드래그 중 → 3번째 손가락을 탭하거나 꾹 누르면 좌클릭 드래그를 유지한 채 우클릭 탭/드래그 동시 입력.
  - **포인터 기준 손가락 고정**:
    - 1손가락 이동 중 손가락 추가(`1 → 1+1` / `1 → 1+2`) 시 기존 이동 손가락만 포인터를 제어하고, 새로 얹은 손가락은 트리거 역할만 수행.
    - 처음부터 2/3손가락 드래그(`0 → 2` / `0 → 3`) 시작 시 검지(가장 왼쪽·아래쪽 손가락)를 기준으로 포인터 이동.
  - **손가락 전환 끊김 방지**: `2+ → 1` 전환 시 Windows 1손가락 속도를 자동 학습해 이질감 없이 부드럽게 이어집니다.
  - 전역 단축키 **`Ctrl + Alt + T`** 로 언제든 즉시 켜고 끌 수 있습니다.
- **Windows 11 Fluent Design 설정 UI (7개 국어 · 자동 업데이트)**
  - Windows 다크/라이트 테마 자동 추종, 배경과 통일된 커스텀 Fluent 스크롤바 및 드롭다운 적용.
  - 7개 국어 재시작 없는 실시간 언어 전환 지원.
  - GitHub 기반 **업데이트 확인 및 원클릭 자동 업데이트** 내장.

---

## 기본 멀티핑거 제스처

| 제스처 | 기본 동작 |
| :--- | :--- |
| **1손가락 이동 / 탭** | Windows 기본 (가속 끔, 1:1 선형) |
| **우측 가장자리 1손가락 스크롤** | Windows 11 기본 기능 (`SingleFingerPanMode = 2`) |
| **하단 가장자리 좌우 스크롤** | 부드러운 가로 스크롤 (`WM_MOUSEHWHEEL`, 평상시 후크 0개) |
| **우측·하단 모서리 2손가락 핀치** | 화면 확대 / 축소 (`Ctrl + Wheel`) |
| **2손가락 드래그** | 좌클릭 드래그 |
| **2손가락 탭 (`0 → 2`)** | 우클릭 |
| **1손가락 이동 중 + 1손가락 탭 (`1 → 2`)** | 좌클릭 (유지 시 좌클릭 드래그) |
| **3손가락 드래그** | 우클릭 드래그 |
| **3손가락 탭 (`0 → 3`)** | 휠클릭 |
| **1손가락 이동 중 + 2손가락 탭 (`1 → 3`)** | 우클릭 (유지 시 우클릭 드래그) |
| **3F 우드래그 → 2F 유지 + 3번째 손가락** | 우클릭 드래그 + 좌클릭 동시 입력 (FPS 조준+사격) |
| **2F 좌드래그 + 3번째 손가락** | 좌클릭 드래그 + 우클릭 동시 입력 |
| **4손가락 제스처** | Windows 기본 유지 |

*(2손가락·3손가락의 모든 동작은 `TouchpadGoodSettings.exe`에서 자유롭게 변경할 수 있습니다.)*

---

## 설치 방법

### 방법 1: 통합 설치 프로그램 사용 (권장)
1. [최신 릴리즈 페이지](https://github.com/wojo0821/TouchpadGood/releases/latest)에서 **`TouchpadGoodSetup.exe`** 를 다운로드합니다 ([직접 다운로드](https://github.com/wojo0821/TouchpadGood/raw/main/TouchpadGoodSetup.exe)).
2. `TouchpadGoodSetup.exe`를 실행하고 **설치 시작**을 누릅니다.
   - `%LOCALAPPDATA%\Programs\TouchpadGood` 경로에 설치됩니다.
   - Windows 터치패드 자동 최적화, 로그인 시 UAC 창 없는 관리자 자동 실행(작업 스케줄러), 시작 메뉴/바탕 화면 바로가기를 한 번에 설정할 수 있습니다.
   - **Windows 설정 → 앱 → TouchpadGood** 또는 설치 파일 재실행으로 언제든 완전 제거할 수 있으며, 제거 시 원래 Windows 터치패드 설정이 자동 복구됩니다.

### 방법 2: 소스코드 직접 빌드 (Visual Studio 불필요)
Windows 10/11 기본 내장 `.NET Framework 4.x csc.exe`만으로 바로 빌드됩니다:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
```

빌드 산출물 (x64):
- `TouchpadGood.exe` — 무트레이 백그라운드 드라이버
- `TouchpadGoodSettings.exe` — Fluent Design 설정 앱
- `TouchpadGoodSetup.exe` — 위 2개 바이너리가 내장된 통합 설치 프로그램

---

## 라이선스

MIT License — [LICENSE](LICENSE) 참고.
