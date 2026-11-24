$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$dir = Split-Path -Parent $MyInvocation.MyCommand.Path

$driverOut   = Join-Path $dir "TouchpadGood.exe"
$settingsOut = Join-Path $dir "TouchpadGoodSettings.exe"
$setupOut    = Join-Path $dir "TouchpadGoodSetup.exe"
$manifest    = Join-Path $dir "app.manifest"
$icon        = Join-Path $dir "TouchpadGood.ico"

# 1. Pure Win32 Headless Ultra-Low-Latency Driver Daemon (/unsafe zero-copy, ZERO WinForms/Drawing/Tray/Net!)
& $csc /nologo /target:winexe /optimize+ /unsafe /platform:x64 `
  "/win32manifest:$manifest" `
  "/win32icon:$icon" `
  /out:$driverOut `
  (Join-Path $dir "AppMeta.cs") `
  (Join-Path $dir "DriverConfig.cs") `
  (Join-Path $dir "WindowsTouchpadSync.cs") `
  (Join-Path $dir "RawInputTouchpad.cs") `
  (Join-Path $dir "GestureEngine.cs") `
  (Join-Path $dir "DriverProgram.cs")

if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
Write-Host "Built (Ultra-Low-Latency Win32 Driver): $driverOut" -ForegroundColor Green

# 2. Windows 11 Fluent Design Settings App (7 Languages + Custom Fluent Scrollbar & Dropdowns + GitHub Auto-Update)
& $csc /nologo /target:winexe /optimize+ /platform:x64 `
  "/win32manifest:$manifest" `
  "/win32icon:$icon" `
  /out:$settingsOut `
  /r:System.Windows.Forms.dll `
  /r:System.Drawing.dll `
  (Join-Path $dir "AppMeta.cs") `
  (Join-Path $dir "DriverConfig.cs") `
  (Join-Path $dir "WindowsTouchpadSync.cs") `
  (Join-Path $dir "Localization.cs") `
  (Join-Path $dir "FluentControls.cs") `
  (Join-Path $dir "UpdateChecker.cs") `
  (Join-Path $dir "SettingsForm.cs") `
  (Join-Path $dir "SettingsProgram.cs")

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
  (Join-Path $dir "AppMeta.cs") `
  (Join-Path $dir "DriverConfig.cs") `
  (Join-Path $dir "WindowsTouchpadSync.cs") `
  (Join-Path $dir "Localization.cs") `
  (Join-Path $dir "FluentControls.cs") `
  (Join-Path $dir "SetupProgram.cs")

if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
Write-Host "Built (All-in-One Setup Installer):     $setupOut" -ForegroundColor Green
