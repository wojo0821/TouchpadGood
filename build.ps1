$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$dir = Split-Path -Parent $MyInvocation.MyCommand.Path

$driverOut   = Join-Path $dir "TouchpadGood.exe"
$settingsOut = Join-Path $dir "TouchpadGoodSettings.exe"
$setupOut    = Join-Path $dir "TouchpadGoodSetup.exe"

$manifest    = Join-Path $dir "assets\app.manifest"
$icon        = Join-Path $dir "assets\TouchpadGood.ico"

$srcCommon   = Join-Path $dir "src\Common"
$srcDriver   = Join-Path $dir "src\Driver"
$srcSettings = Join-Path $dir "src\Settings"
$srcSetup    = Join-Path $dir "src\Setup"

# 1. Pure Win32 Headless Ultra-Low-Latency Driver Daemon (/unsafe zero-copy, ZERO WinForms/Drawing/Tray/Net!)
& $csc /nologo /target:winexe /optimize+ /unsafe /platform:x64 `
  "/win32manifest:$manifest" `
  "/win32icon:$icon" `
  /out:$driverOut `
  (Join-Path $srcCommon "AppMeta.cs") `
  (Join-Path $srcCommon "DriverConfig.cs") `
  (Join-Path $srcCommon "WindowsTouchpadSync.cs") `
  (Join-Path $srcDriver "RawInputTouchpad.cs") `
  (Join-Path $srcDriver "GestureEngine.cs") `
  (Join-Path $srcDriver "DriverProgram.cs")

if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
Write-Host "Built (Ultra-Low-Latency Win32 Driver): $driverOut" -ForegroundColor Green

# 2. Windows 11 Fluent Design Settings App (7 Languages + Custom Fluent Scrollbar & Dropdowns + GitHub Auto-Update)
& $csc /nologo /target:winexe /optimize+ /platform:x64 `
  "/win32manifest:$manifest" `
  "/win32icon:$icon" `
  /out:$settingsOut `
  /r:System.Windows.Forms.dll `
  /r:System.Drawing.dll `
  (Join-Path $srcCommon "AppMeta.cs") `
  (Join-Path $srcCommon "DriverConfig.cs") `
  (Join-Path $srcCommon "WindowsTouchpadSync.cs") `
  (Join-Path $srcCommon "Localization.cs") `
  (Join-Path $srcSettings "FluentControls.cs") `
  (Join-Path $srcSettings "UpdateChecker.cs") `
  (Join-Path $srcSettings "SettingsForm.cs") `
  (Join-Path $srcSettings "SettingsProgram.cs")

if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
Write-Host "Built (Fluent Design Settings UI):      $settingsOut" -ForegroundColor Green

# 3. All-in-One Standalone Installer / Updater / Uninstaller (embeds TouchpadGood.exe + TouchpadGoodSettings.exe)
& $csc /nologo /target:winexe /optimize+ /platform:x64 `
  "/win32manifest:$manifest" `
  "/win32icon:$icon" `
  /out:$setupOut `
  /r:System.Windows.Forms.dll `
  /r:System.Drawing.dll `
  "/resource:$driverOut,TouchpadGood.exe" `
  "/resource:$settingsOut,TouchpadGoodSettings.exe" `
  (Join-Path $srcCommon "AppMeta.cs") `
  (Join-Path $srcCommon "DriverConfig.cs") `
  (Join-Path $srcCommon "WindowsTouchpadSync.cs") `
  (Join-Path $srcCommon "Localization.cs") `
  (Join-Path $srcSettings "FluentControls.cs") `
  (Join-Path $srcSetup "SetupProgram.cs")

if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
Write-Host "Built (All-in-One Setup Installer):     $setupOut" -ForegroundColor Green
