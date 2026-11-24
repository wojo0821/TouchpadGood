using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading;

namespace TouchpadGood
{
    internal static class DriverProgram
    {
        public const string WindowClassName = "TouchpadGood_HostClass";

        private const int ProcessPowerThrottling = 4;
        private const uint PROCESS_POWER_THROTTLING_CURRENT_VERSION = 1;
        private const uint PROCESS_POWER_THROTTLING_EXECUTION_SPEED = 0x1;

        private const uint WS_POPUP = 0x80000000;
        private const uint WS_EX_TOOLWINDOW = 0x00000080;
        private const uint WS_EX_NOACTIVATE = 0x08000000;

        private const uint WM_DESTROY = 0x0002;
        private const uint WM_ENDSESSION = 0x0016;
        private const uint WM_INPUT = 0x00FF;
        private const uint WM_TIMER = 0x0113;
        private const uint WM_POWERBROADCAST = 0x0218;
        private const uint WM_DEVICECHANGE = 0x0219;
        private const uint WM_WTSSESSION_CHANGE = 0x02B1;
        private const uint WM_HOTKEY = 0x0312;

        private const int PBT_APMSUSPEND = 0x0004;
        private const int PBT_APMRESUMESUSPEND = 0x0007;
        private const int PBT_APMRESUMEAUTOMATIC = 0x0012;
        private const int DBT_DEVNODES_CHANGED = 0x0007;
        private const int WTS_SESSION_UNLOCK = 0x8;
        private const uint NOTIFY_FOR_THIS_SESSION = 0;

        private const int HOTKEY_TOGGLE_ID = 9001;
        private const uint MOD_ALT = 0x0001;
        private const uint MOD_CONTROL = 0x0002;
        private const uint VK_T = 0x54;
        private const uint MSGFLT_ALLOW = 1;

        private static readonly IntPtr ENGINE_TIMER_ID = new IntPtr(101);
        private static readonly IntPtr HWND_BROADCAST = new IntPtr(0xffff);

        private static uint WM_DRIVER_RELOAD;
        private static uint WM_DRIVER_STOP;
        private static uint WM_DRIVER_PING;
        private static uint WM_DRIVER_CONFIG_CHANGED;

        [StructLayout(LayoutKind.Sequential)]
        private struct PROCESS_POWER_THROTTLING_STATE
        {
            public uint Version;
            public uint ControlMask;
            public uint StateMask;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WNDCLASSEX
        {
            public uint cbSize;
            public uint style;
            public WndProcDelegate lpfnWndProc;
            public int cbClsExtra;
            public int cbWndExtra;
            public IntPtr hInstance;
            public IntPtr hIcon;
            public IntPtr hCursor;
            public IntPtr hbrBackground;
            public string lpszMenuName;
            public string lpszClassName;
            public IntPtr hIconSm;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MSG
        {
            public IntPtr hwnd;
            public uint message;
            public IntPtr wParam;
            public IntPtr lParam;
            public uint time;
            public int ptX;
            public int ptY;
        }

        private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool SetProcessDpiAwarenessContext(IntPtr value);

        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetProcessInformation(
            IntPtr hProcess, int processInformationClass,
            ref PROCESS_POWER_THROTTLING_STATE processInformation, int processInformationSize);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern ushort RegisterClassEx(ref WNDCLASSEX lpwcx);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr CreateWindowEx(
            uint dwExStyle, string lpClassName, string lpWindowName, uint dwStyle,
            int x, int y, int nWidth, int nHeight,
            IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyWindow(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr DefWindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

        [DllImport("user32.dll")]
        private static extern bool TranslateMessage(ref MSG lpMsg);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr DispatchMessage(ref MSG lpMsg);

        [DllImport("user32.dll")]
        private static extern void PostQuitMessage(int nExitCode);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern uint RegisterWindowMessage(string lpString);

        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool ChangeWindowMessageFilterEx(IntPtr hWnd, uint message, uint action, IntPtr pChangeFilterStruct);

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetTimer(IntPtr hWnd, IntPtr nIDEvent, uint uElapse, IntPtr lpTimerFunc);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool KillTimer(IntPtr hWnd, IntPtr uIDEvent);

        [DllImport("winmm.dll")]
        private static extern uint timeBeginPeriod(uint uPeriod);

        [DllImport("winmm.dll")]
        private static extern uint timeEndPeriod(uint uPeriod);

        [DllImport("wtsapi32.dll", SetLastError = true)]
        private static extern bool WTSRegisterSessionNotification(IntPtr hWnd, uint dwFlags);

        [DllImport("wtsapi32.dll", SetLastError = true)]
        private static extern bool WTSUnRegisterSessionNotification(IntPtr hWnd);

        [DllImport("kernel32.dll")]
        private static extern bool Beep(uint dwFreq, uint dwDuration);

        [DllImport("avrt.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr AvSetMmThreadCharacteristics(string taskName, ref uint taskIndex);

        [DllImport("avrt.dll", SetLastError = true)]
        private static extern bool AvRevertMmThreadCharacteristics(IntPtr avrtHandle);

        private static IntPtr _hwnd;
        private static IntPtr _mmcssHandle = IntPtr.Zero;
        private static WndProcDelegate _wndProcPinned;
        private static DriverConfig _config;
        private static string _lastSyncSig;
        private static RawInputTouchpad _raw;
        private static GestureEngine _engine;
        private static bool _timerArmed;
        private static bool _hiResTimer;
        private static bool _restoreOnExit = true;
        private static bool _isAdmin;

        private static bool CheckIsAdministrator()
        {
            try
            {
                using (var id = WindowsIdentity.GetCurrent())
                    return new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch { return false; }
        }

        [STAThread]
        private static void Main(string[] args)
        {
            try
            {
                if (!SetProcessDpiAwarenessContext(new IntPtr(-4)))
                    SetProcessDPIAware();
            }
            catch
            {
                try { SetProcessDPIAware(); } catch { }
            }

            bool waitPrev = false;
            if (args != null)
            {
                for (int i = 0; i < args.Length; i++)
                {
                    if (string.Equals(args[i], "--wait-prev", StringComparison.OrdinalIgnoreCase))
                        waitPrev = true;
                }
            }

            AppDomain.CurrentDomain.UnhandledException += (s, ev) =>
            {
                try { if (_engine != null) _engine.ForceReleaseAll(); } catch { }
                AppMeta.LogCrash("TouchpadGood", ev.ExceptionObject as Exception);
            };

            Mutex mutex = null;
            bool createdNew = false;
            int tries = waitPrev ? 35 : 1;
            for (int i = 0; i < tries; i++)
            {
                try
                {
                    mutex = new Mutex(true, "TouchpadGood_SingleInstance_Mutex", out createdNew);
                }
                catch (UnauthorizedAccessException)
                {
                    // Already running as elevated Administrator
                    createdNew = false;
                    mutex = null;
                    if (!waitPrev) return;
                }
                if (createdNew) break;
                try { if (mutex != null) mutex.Close(); } catch { }
                mutex = null;
                if (waitPrev) Thread.Sleep(100);
            }

            if (!createdNew || mutex == null) return;

            using (mutex)
            {
                _isAdmin = CheckIsAdministrator();

                try
                {
                    GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;
                    var proc = Process.GetCurrentProcess();
                    proc.PriorityClass = ProcessPriorityClass.High;
                    Thread.CurrentThread.Priority = ThreadPriority.Highest;

                    // Register input thread with Windows MMCSS "Games" profile for sub-ms wakeup under 100% game load
                    uint taskIdx = 0;
                    _mmcssHandle = AvSetMmThreadCharacteristics("Games", ref taskIdx);

                    var pts = new PROCESS_POWER_THROTTLING_STATE
                    {
                        Version = PROCESS_POWER_THROTTLING_CURRENT_VERSION,
                        ControlMask = PROCESS_POWER_THROTTLING_EXECUTION_SPEED,
                        StateMask = 0
                    };
                    SetProcessInformation(proc.Handle, ProcessPowerThrottling, ref pts, Marshal.SizeOf(typeof(PROCESS_POWER_THROTTLING_STATE)));
                }
                catch { }

                WM_DRIVER_RELOAD = RegisterWindowMessage("WM_TOUCHPADGOOD_RELOAD");
                WM_DRIVER_STOP = RegisterWindowMessage("WM_TOUCHPADGOOD_STOP");
                WM_DRIVER_PING = RegisterWindowMessage("WM_TOUCHPADGOOD_PING");
                WM_DRIVER_CONFIG_CHANGED = RegisterWindowMessage("WM_TOUCHPADGOOD_CONFIG_CHANGED");

                _wndProcPinned = WndProc;
                IntPtr hInstance = GetModuleHandle(null);
                var wc = new WNDCLASSEX
                {
                    cbSize = (uint)Marshal.SizeOf(typeof(WNDCLASSEX)),
                    lpfnWndProc = _wndProcPinned,
                    hInstance = hInstance,
                    lpszClassName = WindowClassName
                };
                RegisterClassEx(ref wc);

                _hwnd = CreateWindowEx(
                    WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE,
                    WindowClassName,
                    "TouchpadGood_Headless",
                    WS_POPUP,
                    0, 0, 0, 0,
                    IntPtr.Zero, IntPtr.Zero, hInstance, IntPtr.Zero);

                if (_hwnd == IntPtr.Zero) return;

                try
                {
                    // Allow standard-user TouchpadSettings.exe to control elevated TouchpadDriver.exe
                    ChangeWindowMessageFilterEx(_hwnd, WM_DRIVER_RELOAD, MSGFLT_ALLOW, IntPtr.Zero);
                    ChangeWindowMessageFilterEx(_hwnd, WM_DRIVER_STOP, MSGFLT_ALLOW, IntPtr.Zero);
                    ChangeWindowMessageFilterEx(_hwnd, WM_DRIVER_PING, MSGFLT_ALLOW, IntPtr.Zero);
                }
                catch { }

                _config = DriverConfig.Load();
                _lastSyncSig = _config.SystemSyncSignature();
                _raw = new RawInputTouchpad();
                _engine = new GestureEngine(_config, ScheduleOneShotTimer);

                _raw.FrameReceived += _engine.ProcessFrame;
                _raw.NativeMouseMoved += _engine.OnNativeMouseMove;
                _raw.Register(_hwnd);
                RegisterHotKey(_hwnd, HOTKEY_TOGGLE_ID, MOD_CONTROL | MOD_ALT, VK_T);
                try { WTSRegisterSessionNotification(_hwnd, NOTIFY_FOR_THIS_SESSION); } catch { }

                WarmUpAll();
                WindowsTouchpadSync.Apply(_config);

                try
                {
                    MSG msg;
                    while (GetMessage(out msg, IntPtr.Zero, 0, 0) > 0)
                    {
                        TranslateMessage(ref msg);
                        DispatchMessage(ref msg);
                    }
                }
                finally
                {
                    Cleanup();
                }
            }
        }

        private static void WarmUpAll()
        {
            _engine.WarmUp();
            try
            {
                var flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
                MethodInfo[] rawMethods = typeof(RawInputTouchpad).GetMethods(flags);
                for (int i = 0; i < rawMethods.Length; i++)
                {
                    if (!rawMethods[i].IsAbstract && !rawMethods[i].ContainsGenericParameters)
                        RuntimeHelpers.PrepareMethod(rawMethods[i].MethodHandle);
                }
            }
            catch { }
        }

        private static void SetHiResTimer(bool enable)
        {
            if (enable && !_hiResTimer)
            {
                try { timeBeginPeriod(1); _hiResTimer = true; } catch { }
            }
            else if (!enable && _hiResTimer)
            {
                try { timeEndPeriod(1); } catch { }
                _hiResTimer = false;
            }
        }

        private static void ScheduleOneShotTimer(int delayMs)
        {
            if (_hwnd == IntPtr.Zero) return;
            if (delayMs <= 0)
            {
                if (_timerArmed)
                {
                    KillTimer(_hwnd, ENGINE_TIMER_ID);
                    _timerArmed = false;
                }
                SetHiResTimer(false);
            }
            else
            {
                SetHiResTimer(true);
                SetTimer(_hwnd, ENGINE_TIMER_ID, (uint)delayMs, IntPtr.Zero);
                _timerArmed = true;
            }
        }

        private static void ReloadConfig()
        {
            DriverConfig next = DriverConfig.Load();
            string nextSig = next.SystemSyncSignature();
            bool needSync = !string.Equals(nextSig, _lastSyncSig, StringComparison.Ordinal);

            _config = next;
            _engine.UpdateConfig(_config);

            if (needSync)
            {
                _lastSyncSig = nextSig;
                WindowsTouchpadSync.Apply(_config);
            }
        }

        private static void ToggleByHotkey()
        {
            _config.Enabled = !_config.Enabled;
            _config.Save();
            _engine.UpdateConfig(_config);
            _lastSyncSig = _config.SystemSyncSignature();
            WindowsTouchpadSync.Apply(_config);

            PostMessage(HWND_BROADCAST, WM_DRIVER_CONFIG_CHANGED, IntPtr.Zero, IntPtr.Zero);

            if (_config.HotkeySound)
            {
                bool on = _config.Enabled;
                ThreadPool.QueueUserWorkItem(_ =>
                {
                    try
                    {
                        if (on) { Beep(880, 55); Beep(1320, 65); }
                        else { Beep(520, 80); }
                    }
                    catch { }
                });
            }
        }

        private static IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            if (msg == WM_INPUT)
            {
                if (_raw != null) _raw.ProcessMessage(lParam);
                return IntPtr.Zero;
            }
            if (msg == WM_TIMER && wParam == ENGINE_TIMER_ID)
            {
                KillTimer(hWnd, ENGINE_TIMER_ID);
                _timerArmed = false;
                SetHiResTimer(false);
                if (_engine != null) _engine.OnTimerTick();
                return IntPtr.Zero;
            }
            if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_TOGGLE_ID)
            {
                ToggleByHotkey();
                return IntPtr.Zero;
            }
            if (msg == WM_DRIVER_RELOAD)
            {
                ReloadConfig();
                return IntPtr.Zero;
            }
            if (msg == WM_DRIVER_PING)
            {
                // 2 = Elevated Admin, 1 = Standard User
                return new IntPtr(_isAdmin ? 2 : 1);
            }
            if (msg == WM_DRIVER_STOP)
            {
                // wParam == 1 means handover/restart (skip restoring Windows defaults mid-swap)
                if (wParam == new IntPtr(1))
                    _restoreOnExit = false;
                PostQuitMessage(0);
                return IntPtr.Zero;
            }
            if (msg == WM_POWERBROADCAST)
            {
                int pbt = wParam.ToInt32();
                if (pbt == PBT_APMSUSPEND)
                {
                    if (_engine != null) _engine.ForceReleaseAll();
                }
                else if (pbt == PBT_APMRESUMEAUTOMATIC || pbt == PBT_APMRESUMESUSPEND)
                {
                    if (_engine != null) _engine.ForceReleaseAll();
                    if (_raw != null) _raw.ResetDeviceCache();
                    if (_config != null && _config.Enabled) WindowsTouchpadSync.Apply(_config);
                }
                return IntPtr.Zero;
            }
            if (msg == WM_WTSSESSION_CHANGE)
            {
                if (_engine != null) _engine.ForceReleaseAll();
                if (wParam.ToInt32() == WTS_SESSION_UNLOCK && _config != null && _config.Enabled)
                    WindowsTouchpadSync.Apply(_config);
                return IntPtr.Zero;
            }
            if (msg == WM_DEVICECHANGE && wParam.ToInt32() == DBT_DEVNODES_CHANGED)
            {
                if (_engine != null) _engine.ForceReleaseAll();
                if (_raw != null) _raw.ResetDeviceCache();
                return IntPtr.Zero;
            }
            if (msg == WM_ENDSESSION)
            {
                _restoreOnExit = false;
                if (_engine != null) _engine.ForceReleaseAll();
                PostQuitMessage(0);
                return IntPtr.Zero;
            }
            if (msg == WM_DESTROY)
            {
                PostQuitMessage(0);
                return IntPtr.Zero;
            }

            return DefWindowProc(hWnd, msg, wParam, lParam);
        }

        private static void Cleanup()
        {
            try { if (_timerArmed && _hwnd != IntPtr.Zero) KillTimer(_hwnd, ENGINE_TIMER_ID); } catch { }
            SetHiResTimer(false);
            try { if (_hwnd != IntPtr.Zero) WTSUnRegisterSessionNotification(_hwnd); } catch { }
            try { if (_hwnd != IntPtr.Zero) UnregisterHotKey(_hwnd, HOTKEY_TOGGLE_ID); } catch { }
            try { if (_engine != null) _engine.Dispose(); } catch { }
            try { if (_raw != null) _raw.Dispose(); } catch { }

            if (_restoreOnExit)
            {
                try { WindowsTouchpadSync.RestoreOriginal(); } catch { }
            }

            try
            {
                if (_mmcssHandle != IntPtr.Zero)
                {
                    AvRevertMmThreadCharacteristics(_mmcssHandle);
                    _mmcssHandle = IntPtr.Zero;
                }
            }
            catch { }

            try
            {
                if (_hwnd != IntPtr.Zero)
                {
                    DestroyWindow(_hwnd);
                    _hwnd = IntPtr.Zero;
                }
            }
            catch { }
        }
    }
}
