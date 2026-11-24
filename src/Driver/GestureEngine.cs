using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

namespace TouchpadGood
{
    /// <summary>
    /// Zero-hook, zero-allocation, event-driven customizable multi-finger gaming gesture engine.
    /// Compiled with zero UI/WinForms dependencies for pure headless Win32 driver operation.
    /// </summary>
    public sealed unsafe class GestureEngine : IDisposable
    {
        private const uint INPUT_MOUSE = 0;
        private const uint INPUT_KEYBOARD = 1;
        private const uint MOUSEEVENTF_MOVE = 0x0001;
        private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const uint MOUSEEVENTF_LEFTUP = 0x0004;
        private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
        private const uint MOUSEEVENTF_RIGHTUP = 0x0010;
        private const uint MOUSEEVENTF_MIDDLEDOWN = 0x0020;
        private const uint MOUSEEVENTF_MIDDLEUP = 0x0040;
        private const uint MOUSEEVENTF_WHEEL = 0x0800;
        private const uint MOUSEEVENTF_HWHEEL = 0x01000;
        private const uint KEYEVENTF_KEYUP = 0x0002;
        private const ushort VK_CONTROL = 0x11;
        private const int WHEEL_DELTA = 120;

        private const int WH_MOUSE_LL = 14;
        private const int WM_MOUSEMOVE = 0x0200;
        private const int WM_MOUSEWHEEL = 0x020A;
        private const int WM_MOUSEHWHEEL = 0x020E;
        private const uint WM_QUIT = 0x0012;
        private const uint WM_APP_HOOK_ON = 0x8001;
        private const uint WM_APP_HOOK_OFF = 0x8002;

        // Right-edge vertical scroll & Bottom-edge horizontal scroll (real-mouse ±120 WHEEL_DELTA, zero-deadlock dedicated hook thread)
        private const double RIGHT_EDGE_X_START = 0.955;
        private const double RIGHT_EDGE_X_CANCEL = 0.89;
        private const double RIGHT_VSCROLL_TRIGGER_Y = 0.0035;
        private const double RIGHT_VSCROLL_NOTCHES_PER_HEIGHT = 18.0;
        private const double RIGHT_VSCROLL_INTERVAL_MS = 16.0;

        private const double BOTTOM_EDGE_Y_START = 0.925;
        private const double BOTTOM_EDGE_Y_CANCEL = 0.84;
        private const double BOTTOM_HSCROLL_TRIGGER_X = 0.0045;
        private const double BOTTOM_HSCROLL_NOTCHES_PER_WIDTH = 16.0;
        private const double BOTTOM_HSCROLL_INTERVAL_MS = 20.0;

        // Right/Bottom scroll-edge 2-finger pinch-to-zoom
        private const double EDGE_PINCH_RIGHT_MID_X = 0.78;
        private const double EDGE_PINCH_BOTTOM_MID_Y = 0.76;
        private const double EDGE_PINCH_TRIGGER_DIST = 0.014;
        private const double EDGE_PINCH_STEP_DIST = 0.042;
        private const double EDGE_PINCH_MIN_INTERVAL_MS = 32.0;

        private static readonly IntPtr ExtraInfoPtr = new IntPtr(unchecked((int)RawInputTouchpad.SYNTHETIC_EXTRA_INFO_TAG));
        private static readonly double TicksToMs = 1000.0 / Stopwatch.Frequency;
        private static readonly int InputSize = sizeof(INPUT);

        private const double STATIONARY_HOLD_MS = 175.0;
        private const double ONE_ESTABLISH_TRAVEL = 0.0120;
        private const double DRAG_MOVE_THRESHOLD = 0.0020;
        private const double FRESH_DRAG_EARLY_NET_THRESHOLD = 0.0085;
        private const double FRESH_DRAG_LATE_NET_THRESHOLD = 0.0060;
        private const double FRESH_DRAG_EARLY_WINDOW_MS = 55.0;
        private const double TAP_MAX_MS = 240.0;
        private const double TAP_MAX_TRAVEL = 0.035;
        private const double POST_DRAG_NO_TAP_MS = 110.0;
        private const double ADS_FIRST_TRIGGER_DWELL_MS = 32.0;
        private const double ADS_REPEAT_TRIGGER_DWELL_MS = 16.0;

        private enum Branch
        {
            None = 0,
            Fresh2 = 1,
            OnePlusOne = 2,
            Fresh3 = 3,
            OnePlusTwo = 4,
            Native4 = 5
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public uint type;
            public INPUTUNION u;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct INPUTUNION
        {
            [FieldOffset(0)] public MOUSEINPUT mi;
            [FieldOffset(0)] public KEYBDINPUT ki;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MSLLHOOKSTRUCT
        {
            public POINT pt;
            public uint mouseData;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
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

        private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, INPUT* pInputs, int cbSize);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool PeekMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax, uint wRemoveMsg);

        [DllImport("user32.dll")]
        private static extern bool TranslateMessage(ref MSG lpMsg);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr DispatchMessage(ref MSG lpMsg);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool PostThreadMessage(uint idThread, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern bool SetCursorPos(int X, int Y);

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        private struct PrevContact
        {
            public int Id;
            public int X;
            public int Y;
        }

        private readonly PrevContact[] _prev = new PrevContact[16];
        private int _prevStoredCount;
        private readonly Action<int> _scheduleTimerMs;

        private DriverConfig _config;

        // Session state
        private int _prevCount;
        private int _peakFingers;
        private double _multiStartMs;
        private double _lastCountChangeMs;
        private double _accumTravel;
        private double _multiNetDx, _multiNetDy;

        // 1-finger tracking & aiming finger lock
        private double _oneStartMs;
        private double _oneTravel;
        private bool _oneEstablished;
        private int _primaryAimId = -1;

        // Gesture resolution state
        private Branch _branch;
        private DragAction _activeDrag;
        private bool _enteredFromMovingOne;
        private bool _pressedButtonInSession;
        private bool _cooldownUntilLift;
        private bool _suppressedByPhysBtn;
        private bool _pendingResolve;
        private double _postDragNoTapUntilMs;

        // FPS ADS + Shoot latch state (3F RightDrag -> 2F holds RightDrag + 3rd finger LeftClick)
        // AND LeftDrag + Alt-Right combo (2F LeftDrag + 3rd finger RightClick / RightDrag!)
        private bool _rightDragLatched;
        private double _rightDragDropTo2Ms;
        private bool _adsFiring;
        private bool _hasFiredInAds;
        private bool _leftDragAltRight;
        private int _adsAimId1 = -1, _adsAimId2 = -1;

        // Held mouse button state & minimum hold timestamps
        private bool _leftDown;
        private bool _rightDown;
        private bool _middleDown;
        private double _leftDownMs;
        private double _rightDownMs;
        private double _middleDownMs;
        private double _pendingLeftUpMs;
        private double _pendingRightUpMs;
        private double _pendingMiddleUpMs;

        // Sub-pixel remainders
        private double _remX, _remY;

        // Zero-freeze 2+ -> 1 handoff bridge + native 1-finger speed auto-matcher
        private bool _bridgingOne;
        private double _bridgeStartMs;
        private double _bridgeRemX, _bridgeRemY;
        private double _lastOneFrameMs;
        private double _nativePxPerUnitX, _nativePxPerUnitY;
        private double _calTouchX, _calTouchY;
        private double _calNativeX, _calNativeY;

        // Bottom-edge horizontal scroll:
        // Dedicated hook thread ONLY installs WH_MOUSE_LL while actively scrolling so SendInput on the main thread
        // NEVER deadlocks with WH_MOUSE_LL (zero LowLevelHooksTimeout stalls, 0 hooks when idle!).
        private readonly LowLevelMouseProc _mouseHookProc;
        private Thread _hookThread;
        private volatile uint _hookThreadId;
        private IntPtr _mouseHook = IntPtr.Zero;
        private volatile bool _suppressMouseMove;
        private volatile int _anchorX, _anchorY;

        private bool _bottomScrollCandidate;
        private bool _bottomScrollActive;
        private bool _rightScrollCandidate;
        private bool _rightScrollActive;
        private double _bottomStartX, _bottomStartY;
        private double _bottomHScrollAccum;
        private double _lastHScrollEmitMs;
        private double _rightVScrollAccum;
        private double _lastVScrollEmitMs;
        private POINT _bottomAnchorPt;

        // Right/Bottom edge scroll-zone 2-finger pinch-to-zoom state
        private bool _edgePinchCandidate;
        private bool _edgePinchActive;
        private double _pinchStartDist;
        private double _pinchPrevDist;
        private double _pinchStartMidX, _pinchStartMidY;
        private double _pinchAccum;
        private double _lastPinchEmitMs;

        public GestureEngine(DriverConfig config, Action<int> scheduleTimerMs)
        {
            _config = config;
            _scheduleTimerMs = scheduleTimerMs;
            _mouseHookProc = OnLowLevelMouseHook;
        }

        private void EnsureHookThreadStarted()
        {
            if (_hookThreadId != 0) return;
            using (var ready = new ManualResetEvent(false))
            {
                _hookThread = new Thread(() => HookThreadMain(ready))
                {
                    IsBackground = true,
                    Priority = ThreadPriority.AboveNormal,
                    Name = "TouchpadGood_EdgeHook"
                };
                _hookThread.Start();
                ready.WaitOne(1000);
            }
        }

        private void HookThreadMain(ManualResetEvent ready)
        {
            MSG msg;
            // Force Win32 message queue creation before exposing thread ID to PostThreadMessage
            PeekMessage(out msg, IntPtr.Zero, 0, 0, 0);
            _hookThreadId = GetCurrentThreadId();
            ready.Set();

            IntPtr hMod = GetModuleHandle(null);
            try
            {
                while (GetMessage(out msg, IntPtr.Zero, 0, 0) > 0)
                {
                    if (msg.message == WM_APP_HOOK_ON)
                    {
                        if (_mouseHook == IntPtr.Zero && _suppressMouseMove)
                        {
                            _mouseHook = SetWindowsHookEx(WH_MOUSE_LL, _mouseHookProc, hMod, 0);
                            SetCursorPos(_anchorX, _anchorY);
                        }
                    }
                    else if (msg.message == WM_APP_HOOK_OFF)
                    {
                        if (_mouseHook != IntPtr.Zero)
                        {
                            UnhookWindowsHookEx(_mouseHook);
                            _mouseHook = IntPtr.Zero;
                        }
                    }
                    else
                    {
                        TranslateMessage(ref msg);
                        DispatchMessage(ref msg);
                    }
                }
            }
            finally
            {
                if (_mouseHook != IntPtr.Zero)
                {
                    UnhookWindowsHookEx(_mouseHook);
                    _mouseHook = IntPtr.Zero;
                }
                _hookThreadId = 0;
            }
        }

        public static double NowMs()
        {
            return Stopwatch.GetTimestamp() * TicksToMs;
        }

        public void WarmUp()
        {
            try
            {
                var flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
                MethodInfo[] methods = typeof(GestureEngine).GetMethods(flags);
                for (int i = 0; i < methods.Length; i++)
                {
                    if (!methods[i].IsAbstract && !methods[i].ContainsGenericParameters)
                        RuntimeHelpers.PrepareMethod(methods[i].MethodHandle);
                }

                INPUT dummy = new INPUT();
                SendInput(0, &dummy, InputSize);
            }
            catch { }
        }

        public void UpdateConfig(DriverConfig config)
        {
            _config = config;
            if (!_config.Enabled)
                ForceReleaseAll();
            else
            {
                if (!_config.EnableBottomEdgeHScroll)
                    CancelBottomEdgeScroll();
                if (!_config.EnableEdgePinchZoom)
                    CancelEdgePinch();
            }
        }

        private IntPtr OnLowLevelMouseHook(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && _suppressMouseMove)
            {
                int msg = wParam.ToInt32();
                if (msg == WM_MOUSEMOVE || msg == WM_MOUSEWHEEL || msg == WM_MOUSEHWHEEL)
                {
                    MSLLHOOKSTRUCT* ms = (MSLLHOOKSTRUCT*)lParam;
                    // Block all non-TouchpadGood cursor moves AND any competing OS PTP wheel events while edge-scrolling/zooming!
                    if (ms->dwExtraInfo != ExtraInfoPtr)
                        return new IntPtr(1);
                }
            }
            return CallNextHookEx(_mouseHook, nCode, wParam, lParam);
        }

        private void SetTempMouseSuppression(bool enable)
        {
            if (enable)
            {
                EnsureHookThreadStarted();
                _anchorX = _bottomAnchorPt.X;
                _anchorY = _bottomAnchorPt.Y;
                _suppressMouseMove = true;
                uint tid = _hookThreadId;
                if (tid != 0) PostThreadMessage(tid, WM_APP_HOOK_ON, IntPtr.Zero, IntPtr.Zero);
            }
            else
            {
                _suppressMouseMove = false;
                uint tid = _hookThreadId;
                if (tid != 0) PostThreadMessage(tid, WM_APP_HOOK_OFF, IntPtr.Zero, IntPtr.Zero);
            }
        }

        private void CancelBottomEdgeScroll()
        {
            _bottomScrollCandidate = false;
            _bottomScrollActive = false;
            _rightScrollCandidate = false;
            _rightScrollActive = false;
            _bottomHScrollAccum = 0;
            _lastHScrollEmitMs = 0;
            _rightVScrollAccum = 0;
            _lastVScrollEmitMs = 0;
            if (!_edgePinchActive)
                SetTempMouseSuppression(false);
        }

        private void CancelEdgePinch()
        {
            _edgePinchCandidate = false;
            _edgePinchActive = false;
            _pinchAccum = 0;
            _lastPinchEmitMs = 0;
            if (!_bottomScrollActive && !_rightScrollActive)
                SetTempMouseSuppression(false);
        }

        private static bool IsInScrollEdgeZone(TouchContact a, TouchContact b, int maxX, int maxY)
        {
            double invX = 1.0 / Math.Max(1.0, maxX);
            double invY = 1.0 / Math.Max(1.0, maxY);
            double ax = a.X * invX, ay = a.Y * invY;
            double bx = b.X * invX, by = b.Y * invY;
            double midX = (ax + bx) * 0.5;
            double midY = (ay + by) * 0.5;
            bool rightZone = (midX >= EDGE_PINCH_RIGHT_MID_X) && (ax >= 0.85 || bx >= 0.85);
            bool bottomZone = (midY >= EDGE_PINCH_BOTTOM_MID_Y) && (ay >= 0.80 || by >= 0.80);
            return rightZone || bottomZone;
        }

        private static double CalcPinchDistance(TouchContact a, TouchContact b, int maxX, int maxY, int physMaxX, int physMaxY)
        {
            double dx = (double)(b.X - a.X) / Math.Max(1.0, maxX);
            double dy = (double)(b.Y - a.Y) / Math.Max(1.0, maxY);
            double aspect = (physMaxX > 0 && physMaxY > 0) ? ((double)physMaxY / physMaxX) : 0.62;
            dy *= aspect;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        public void ForceReleaseAll()
        {
            ReleaseAllButtonsImmediate();
            ResetSession();
            _postDragNoTapUntilMs = 0;
            RescheduleTimer(NowMs());
        }

        public void OnNativeMouseMove(int dx, int dy, double now)
        {
            if (!_config.Enabled || _bottomScrollActive || _rightScrollActive || _edgePinchActive) return;

            if (_bridgingOne)
            {
                if ((now - _bridgeStartMs) >= 8.0)
                {
                    _bridgingOne = false;
                    _bridgeRemX = 0;
                    _bridgeRemY = 0;
                }
                return;
            }

            if (_prevCount == 1 && !_suppressedByPhysBtn &&
                (now - _oneStartMs) >= 55.0 &&
                (now - _lastOneFrameMs) <= 25.0 &&
                (_calTouchX > 0 || _calTouchY > 0))
            {
                _calNativeX += Math.Abs(dx);
                _calNativeY += Math.Abs(dy);

                if (_calTouchX >= 220.0 && _calNativeX >= 20.0)
                {
                    double rx = _calNativeX / _calTouchX;
                    if (rx >= 0.10 && rx <= 3.0)
                        _nativePxPerUnitX = (_nativePxPerUnitX > 0.05) ? (_nativePxPerUnitX * 0.75 + rx * 0.25) : rx;
                    _calTouchX = 0;
                    _calNativeX = 0;
                }
                if (_calTouchY >= 220.0 && _calNativeY >= 20.0)
                {
                    double ry = _calNativeY / _calTouchY;
                    if (ry >= 0.10 && ry <= 3.0)
                        _nativePxPerUnitY = (_nativePxPerUnitY > 0.05) ? (_nativePxPerUnitY * 0.75 + ry * 0.25) : ry;
                    _calTouchY = 0;
                    _calNativeY = 0;
                }
            }
        }

        public void OnTimerTick()
        {
            double now = NowMs();
            ProcessPendingButtonUps(now);

            if (_config.Enabled && _pendingResolve && _activeDrag == DragAction.None &&
                !_cooldownUntilLift && !_suppressedByPhysBtn && !_edgePinchCandidate && !_edgePinchActive && _prevCount >= 2)
            {
                TryResolvePending(now, _prevCount);
            }

            RescheduleTimer(now);
        }

        public void ProcessFrame(TouchFrame frame)
        {
            double now = frame.TimestampMs > 0 ? frame.TimestampMs : NowMs();
            ProcessPendingButtonUps(now);

            if (!_config.Enabled)
            {
                RescheduleTimer(now);
                return;
            }

            int count = (frame.Contacts != null) ? frame.ContactCount : 0;
            int maxX = frame.MaxX > 0 ? frame.MaxX : 3200;
            int maxY = frame.MaxY > 0 ? frame.MaxY : 2000;

            if (frame.PhysicalButtonDown)
            {
                if (!_suppressedByPhysBtn)
                {
                    _suppressedByPhysBtn = true;
                    _pendingResolve = false;
                    _bridgingOne = false;
                    CancelBottomEdgeScroll();
                    CancelEdgePinch();
                    EndAllDrags(now);
                }
                StorePrevContacts(frame.Contacts, count);
                _prevCount = count;
                if (count == 0) ResetSession();
                RescheduleTimer(now);
                return;
            }
            else if (_suppressedByPhysBtn && count == 0)
            {
                _suppressedByPhysBtn = false;
            }

            // --- ALL FINGERS LIFTED (count == 0) ---
            if (count == 0)
            {
                if (_prevCount == 0)
                {
                    RescheduleTimer(now);
                    return;
                }

                if (_prevCount >= 2 && !_suppressedByPhysBtn && !_cooldownUntilLift &&
                    _activeDrag == DragAction.None && !_rightDragLatched && !_pressedButtonInSession)
                {
                    TryFireTap(now);
                }

                EndAllDrags(now);
                ResetSession();
                RescheduleTimer(now);
                return;
            }

            double sumDx = 0, sumDy = 0;
            int matched = 0;
            double maxMagSq = -1.0, minMagSq = double.MaxValue;
            double fastDx = 0, fastDy = 0;

            bool primaryPresent = false;
            bool primaryMatched = false;
            double primaryDx = 0, primaryDy = 0;

            double adsSumDx = 0, adsSumDy = 0;
            int adsMatched = 0;

            for (int i = 0; i < count; i++)
            {
                TouchContact c = frame.Contacts[i];
                if (_primaryAimId >= 0 && c.ContactId == _primaryAimId)
                    primaryPresent = true;

                for (int j = 0; j < _prevStoredCount; j++)
                {
                    if (_prev[j].Id == c.ContactId)
                    {
                        double cdx = c.X - _prev[j].X;
                        double cdy = c.Y - _prev[j].Y;
                        sumDx += cdx;
                        sumDy += cdy;
                        matched++;

                        if (_primaryAimId >= 0 && c.ContactId == _primaryAimId)
                        {
                            primaryMatched = true;
                            primaryDx = cdx;
                            primaryDy = cdy;
                        }

                        if (_adsAimId1 >= 0 && (c.ContactId == _adsAimId1 || c.ContactId == _adsAimId2))
                        {
                            adsSumDx += cdx;
                            adsSumDy += cdy;
                            adsMatched++;
                        }

                        double magSq = cdx * cdx + cdy * cdy;
                        if (magSq > maxMagSq)
                        {
                            maxMagSq = magSq;
                            fastDx = cdx;
                            fastDy = cdy;
                        }
                        if (magSq < minMagSq)
                            minMagSq = magSq;
                        break;
                    }
                }
            }

            if (_primaryAimId >= 0 && !primaryPresent)
                _primaryAimId = _config.LockPointerToAimFinger ? PickIndexFingerId(frame.Contacts, count, maxX, maxY) : -1;

            StorePrevContacts(frame.Contacts, count);

            int oldCount = _prevCount;
            _prevCount = count;

            // --- 1 FINGER (Native Move/Tap/Right-Edge Scroll + On-Demand Bottom-Edge Horizontal Scroll) ---
            if (count == 1)
            {
                TouchContact c = frame.Contacts[0];
                _primaryAimId = c.ContactId;
                _lastOneFrameMs = now;
                double cnx = (double)c.X / maxX;
                double cny = (double)c.Y / maxY;

                if (oldCount == 0)
                {
                    _oneStartMs = now;
                    _oneTravel = 0;
                    _oneEstablished = false;
                    _cooldownUntilLift = false;
                    _bridgingOne = false;
                    _bridgeRemX = 0;
                    _bridgeRemY = 0;
                    _calTouchX = 0;
                    _calTouchY = 0;
                    _calNativeX = 0;
                    _calNativeY = 0;

                    CancelBottomEdgeScroll();
                    bool onRight = (cnx >= RIGHT_EDGE_X_START);
                    bool onBottom = (_config.EnableBottomEdgeHScroll && cny >= BOTTOM_EDGE_Y_START);
                    if (onRight || onBottom)
                    {
                        _rightScrollCandidate = onRight;
                        _bottomScrollCandidate = onBottom;
                        _bottomStartX = cnx;
                        _bottomStartY = cny;
                        GetCursorPos(out _bottomAnchorPt);
                    }
                }
                else if (oldCount >= 2)
                {
                    bool wasEdgePinch = _edgePinchActive;
                    CancelBottomEdgeScroll();
                    CancelEdgePinch();
                    if (!wasEdgePinch && !_suppressedByPhysBtn && !_cooldownUntilLift &&
                        _activeDrag == DragAction.None && !_rightDragLatched && !_pressedButtonInSession)
                    {
                        TryFireTap(now);
                    }

                    EndAllDrags(now);
                    _branch = Branch.None;
                    _peakFingers = 0;
                    _accumTravel = 0;
                    _multiNetDx = 0;
                    _multiNetDy = 0;
                    _enteredFromMovingOne = false;
                    _pressedButtonInSession = false;
                    _cooldownUntilLift = false;
                    _pendingResolve = false;
                    _oneEstablished = true;

                    if (!_suppressedByPhysBtn)
                    {
                        _bridgingOne = true;
                        _bridgeStartMs = now;
                        _bridgeRemX = 0;
                        _bridgeRemY = 0;
                        if (matched == 1)
                            ApplyBridgeMove(sumDx, sumDy, maxX, frame.PhysMaxX, frame.PhysMaxY, maxY, 0.12);
                    }
                }
                else
                {
                    _cooldownUntilLift = false;

                    if ((_rightScrollCandidate || _bottomScrollCandidate) && !_suppressedByPhysBtn)
                    {
                        if (_rightScrollActive)
                        {
                            if (matched == 1)
                            {
                                double ndy = sumDy / maxY;
                                if (Math.Abs(ndy) < 0.35)
                                    EmitRightVScrollDelta(ndy, now);
                            }
                            RescheduleTimer(now);
                            return;
                        }
                        if (_bottomScrollActive)
                        {
                            if (matched == 1)
                            {
                                double ndx = sumDx / maxX;
                                if (Math.Abs(ndx) < 0.35)
                                    EmitBottomHScrollDelta(ndx, now);
                            }
                            RescheduleTimer(now);
                            return;
                        }

                        double adx = Math.Abs(cnx - _bottomStartX);
                        double ady = Math.Abs(cny - _bottomStartY);

                        if (_rightScrollCandidate && (cnx < RIGHT_EDGE_X_CANCEL || (adx >= 0.012 && adx >= ady * 0.9 && !_bottomScrollCandidate)))
                            _rightScrollCandidate = false;
                        if (_bottomScrollCandidate && (cny < BOTTOM_EDGE_Y_CANCEL || (ady >= 0.012 && ady >= adx * 0.9 && !_rightScrollCandidate)))
                            _bottomScrollCandidate = false;

                        if (_rightScrollCandidate && ady >= RIGHT_VSCROLL_TRIGGER_Y && ady > adx * 1.25)
                        {
                            _rightScrollActive = true;
                            _bottomScrollCandidate = false;
                            _lastVScrollEmitMs = 0;
                            double dirSign = _config.InvertVerticalScroll ? -1.0 : 1.0;
                            double initDir = ((cny >= _bottomStartY) ? 1.0 : -1.0) * dirSign;
                            _rightVScrollAccum = initDir * 0.70;
                            SetTempMouseSuppression(true);
                            EmitRightVScrollDelta(cny - _bottomStartY, now);
                            RescheduleTimer(now);
                            return;
                        }
                        if (_bottomScrollCandidate && adx >= BOTTOM_HSCROLL_TRIGGER_X && adx > ady * 1.25)
                        {
                            _bottomScrollActive = true;
                            _rightScrollCandidate = false;
                            _lastHScrollEmitMs = 0;
                            double initDir = (cnx >= _bottomStartX) ? 1.0 : -1.0;
                            _bottomHScrollAccum = initDir * 0.65;
                            SetTempMouseSuppression(true);
                            EmitBottomHScrollDelta(cnx - _bottomStartX, now);
                            RescheduleTimer(now);
                            return;
                        }
                    }

                    if (matched == 1)
                    {
                        double ndx = sumDx / maxX;
                        double ndy = sumDy / maxY;
                        double d = Math.Sqrt(ndx * ndx + ndy * ndy);

                        if (d < 0.45)
                        {
                            _oneTravel += d;
                            if (!_oneEstablished && _oneTravel >= ONE_ESTABLISH_TRAVEL)
                                _oneEstablished = true;
                        }

                        if (_bridgingOne && !_suppressedByPhysBtn)
                        {
                            ApplyBridgeMove(sumDx, sumDy, maxX, frame.PhysMaxX, frame.PhysMaxY, maxY, 0.45);
                        }
                        else if (!_bridgingOne && !_suppressedByPhysBtn && !_bottomScrollCandidate && !_rightScrollCandidate && (now - _oneStartMs) >= 55.0)
                        {
                            _calTouchX += Math.Abs(sumDx);
                            _calTouchY += Math.Abs(sumDy);
                        }
                    }
                }

                RescheduleTimer(now);
                return;
            }

            if (_bottomScrollCandidate || _bottomScrollActive || _rightScrollCandidate || _rightScrollActive)
                CancelBottomEdgeScroll();

            if (_suppressedByPhysBtn)
            {
                _bridgingOne = false;
                RescheduleTimer(now);
                return;
            }

            // --- MULTI-FINGER ENTRY (oldCount < 2 && count >= 2) ---
            if (oldCount < 2)
            {
                _bridgingOne = false;
                _bridgeRemX = 0;
                _bridgeRemY = 0;

                _multiStartMs = now;
                _lastCountChangeMs = now;
                _peakFingers = count;
                _accumTravel = 0;
                _multiNetDx = 0;
                _multiNetDy = 0;
                _pressedButtonInSession = false;
                _cooldownUntilLift = false;
                _rightDragLatched = false;
                _adsFiring = false;
                _hasFiredInAds = false;
                _leftDragAltRight = false;
                _adsAimId1 = -1;
                _adsAimId2 = -1;
                _enteredFromMovingOne = (oldCount == 1 && _oneEstablished);

                if (!_config.LockPointerToAimFinger)
                    _primaryAimId = -1;
                else if (!_enteredFromMovingOne)
                    _primaryAimId = PickIndexFingerId(frame.Contacts, count, maxX, maxY);

                if (count == 2)
                {
                    CancelEdgePinch();
                    if (_config.EnableEdgePinchZoom && IsInScrollEdgeZone(frame.Contacts[0], frame.Contacts[1], maxX, maxY))
                    {
                        _edgePinchCandidate = true;
                        _enteredFromMovingOne = false;
                        _pinchStartDist = CalcPinchDistance(frame.Contacts[0], frame.Contacts[1], maxX, maxY, frame.PhysMaxX, frame.PhysMaxY);
                        _pinchPrevDist = _pinchStartDist;
                        _pinchStartMidX = (frame.Contacts[0].X + frame.Contacts[1].X) * 0.5 / Math.Max(1.0, maxX);
                        _pinchStartMidY = (frame.Contacts[0].Y + frame.Contacts[1].Y) * 0.5 / Math.Max(1.0, maxY);
                        _pinchAccum = 0;
                        _lastPinchEmitMs = 0;
                        GetCursorPos(out _bottomAnchorPt);
                    }
                    _branch = _enteredFromMovingOne ? Branch.OnePlusOne : Branch.Fresh2;
                    _pendingResolve = !_edgePinchCandidate && (_config.TwoFingerDrag != DragAction.None);
                }
                else if (count == 3)
                {
                    _branch = _enteredFromMovingOne ? Branch.OnePlusTwo : Branch.Fresh3;
                    _pendingResolve = (_config.ThreeFingerDrag != DragAction.None);
                }
                else
                {
                    _branch = Branch.Native4;
                    _pendingResolve = false;
                }

                if (_enteredFromMovingOne && _primaryAimId >= 0 && primaryMatched && (count == 2 || count == 3))
                {
                    double pDist = Math.Sqrt((primaryDx / maxX) * (primaryDx / maxX) + (primaryDy / maxY) * (primaryDy / maxY));
                    if (pDist < 0.45)
                        ApplyMove(primaryDx, primaryDy, maxX, frame.PhysMaxX, frame.PhysMaxY, maxY, true);
                }

                RescheduleTimer(now);
                return;
            }

            // --- MULTI-FINGER COUNT CHANGE (oldCount >= 2 && count != oldCount) ---
            if (count != oldCount)
            {
                if (_edgePinchActive)
                {
                    CancelEdgePinch();
                    _cooldownUntilLift = true;
                    _pendingResolve = false;
                    RescheduleTimer(now);
                    return;
                }
                if (_edgePinchCandidate)
                    CancelEdgePinch();

                _lastCountChangeMs = now;

                // === FPS ADS + SHOOT MODE (Latched Right-Drag!) ===
                if (_rightDragLatched)
                {
                    if (count == 2 && oldCount >= 3)
                    {
                        if (_adsFiring)
                            ReleaseAdsFire(now);
                        _rightDragDropTo2Ms = now;
                        if (_primaryAimId < 0)
                        {
                            _adsAimId1 = frame.Contacts[0].ContactId;
                            _adsAimId2 = frame.Contacts[1].ContactId;
                        }
                        if (_primaryAimId >= 0 && primaryMatched)
                        {
                            double pDist = Math.Sqrt((primaryDx / maxX) * (primaryDx / maxX) + (primaryDy / maxY) * (primaryDy / maxY));
                            if (pDist < 0.45)
                                ApplyMove(primaryDx, primaryDy, maxX, frame.PhysMaxX, frame.PhysMaxY, maxY, _config.AutoMatchDragSpeed);
                        }
                        RescheduleTimer(now);
                        return;
                    }
                    else if (count == 3 && oldCount == 2)
                    {
                        double requiredDwell = _hasFiredInAds ? ADS_REPEAT_TRIGGER_DWELL_MS : ADS_FIRST_TRIGGER_DWELL_MS;
                        if (_rightDragDropTo2Ms > 0 && (now - _rightDragDropTo2Ms) >= requiredDwell)
                        {
                            StartAdsFire(now);
                        }
                        if (_primaryAimId >= 0 && primaryMatched)
                        {
                            double pDist = Math.Sqrt((primaryDx / maxX) * (primaryDx / maxX) + (primaryDy / maxY) * (primaryDy / maxY));
                            if (pDist < 0.45)
                                ApplyMove(primaryDx, primaryDy, maxX, frame.PhysMaxX, frame.PhysMaxY, maxY, _config.AutoMatchDragSpeed);
                        }
                        RescheduleTimer(now);
                        return;
                    }
                    else if (count >= 4)
                    {
                        EndAllDrags(now);
                        _branch = Branch.Native4;
                        _pendingResolve = false;
                        _cooldownUntilLift = true;
                        RescheduleTimer(now);
                        return;
                    }
                }

                // === FPS LEFT-DRAG + ALT-RIGHT COMBO (2F LeftDrag + 3rd finger RightClick / RightDrag!) ===
                if (_activeDrag == DragAction.LeftDrag && _config.EnableFpsAdsLatch)
                {
                    if (count == 3 && oldCount == 2)
                    {
                        StartLeftDragAltRight(now);
                        if (_primaryAimId >= 0 && primaryMatched)
                        {
                            double pDist = Math.Sqrt((primaryDx / maxX) * (primaryDx / maxX) + (primaryDy / maxY) * (primaryDy / maxY));
                            if (pDist < 0.45)
                                ApplyMove(primaryDx, primaryDy, maxX, frame.PhysMaxX, frame.PhysMaxY, maxY, _config.AutoMatchDragSpeed);
                        }
                        RescheduleTimer(now);
                        return;
                    }
                    else if (count == 2 && oldCount >= 3)
                    {
                        if (_leftDragAltRight)
                            ReleaseLeftDragAltRight(now);
                        if (_primaryAimId >= 0 && primaryMatched)
                        {
                            double pDist = Math.Sqrt((primaryDx / maxX) * (primaryDx / maxX) + (primaryDy / maxY) * (primaryDy / maxY));
                            if (pDist < 0.45)
                                ApplyMove(primaryDx, primaryDy, maxX, frame.PhysMaxX, frame.PhysMaxY, maxY, _config.AutoMatchDragSpeed);
                        }
                        RescheduleTimer(now);
                        return;
                    }
                    else if (count >= 4)
                    {
                        EndAllDrags(now);
                        _branch = Branch.Native4;
                        _pendingResolve = false;
                        _cooldownUntilLift = true;
                        RescheduleTimer(now);
                        return;
                    }
                }

                // === Normal Multi-Finger Count Changes ===
                if (count > oldCount)
                {
                    if (count > _peakFingers) _peakFingers = count;

                    if (count == 3)
                    {
                        if (_cooldownUntilLift)
                        {
                            RescheduleTimer(now);
                            return;
                        }
                        if (_activeDrag != DragAction.None)
                        {
                            EndAllDrags(now);
                        }
                        _branch = _enteredFromMovingOne ? Branch.OnePlusTwo : Branch.Fresh3;
                        if (_config.LockPointerToAimFinger && !_enteredFromMovingOne)
                            _primaryAimId = PickIndexFingerId(frame.Contacts, count, maxX, maxY);
                        _accumTravel = 0;
                        _multiNetDx = 0;
                        _multiNetDy = 0;
                        _pendingResolve = (_config.ThreeFingerDrag != DragAction.None);
                    }
                    else if (count >= 4)
                    {
                        EndAllDrags(now);
                        _branch = Branch.Native4;
                        _pendingResolve = false;
                        _cooldownUntilLift = true;
                    }
                }
                else
                {
                    if (_activeDrag != DragAction.None)
                    {
                        EndAllDrags(now);
                        _cooldownUntilLift = true;
                        _pendingResolve = false;
                    }
                    else
                    {
                        _pendingResolve = false;
                    }
                }

                if (_enteredFromMovingOne && _primaryAimId >= 0 && primaryMatched && count <= 3 && !_cooldownUntilLift)
                {
                    double pDist = Math.Sqrt((primaryDx / maxX) * (primaryDx / maxX) + (primaryDy / maxY) * (primaryDy / maxY));
                    if (pDist < 0.45)
                        ApplyMove(primaryDx, primaryDy, maxX, frame.PhysMaxX, frame.PhysMaxY, maxY, _config.AutoMatchDragSpeed);
                }

                RescheduleTimer(now);
                return;
            }

            // --- STEADY MULTI-FINGER MOVEMENT (count == oldCount >= 2) ---
            if (_cooldownUntilLift || _branch == Branch.Native4 || matched == 0)
            {
                RescheduleTimer(now);
                return;
            }

            // Right/Bottom edge scroll-zone 2-finger pinch-to-zoom
            if (count == 2 && (_edgePinchCandidate || _edgePinchActive))
            {
                TouchContact c0 = frame.Contacts[0];
                TouchContact c1 = frame.Contacts[1];
                double curDist = CalcPinchDistance(c0, c1, maxX, maxY, frame.PhysMaxX, frame.PhysMaxY);
                double aspect = (frame.PhysMaxX > 0 && frame.PhysMaxY > 0) ? ((double)frame.PhysMaxY / frame.PhysMaxX) : 0.62;

                if (!_edgePinchActive)
                {
                    double distDelta = Math.Abs(curDist - _pinchStartDist);
                    double curMidX = (c0.X + c1.X) * 0.5 / Math.Max(1.0, maxX);
                    double curMidY = (c0.Y + c1.Y) * 0.5 / Math.Max(1.0, maxY);
                    double mdx = curMidX - _pinchStartMidX;
                    double mdy = (curMidY - _pinchStartMidY) * aspect;
                    double midDisp = Math.Sqrt(mdx * mdx + mdy * mdy);

                    if (distDelta >= EDGE_PINCH_TRIGGER_DIST && distDelta >= midDisp * 0.65)
                    {
                        _edgePinchActive = true;
                        _pendingResolve = false;
                        _pressedButtonInSession = true;
                        SetTempMouseSuppression(true);
                        EmitPinchZoomDelta(curDist - _pinchStartDist, now);
                        _pinchPrevDist = curDist;
                        RescheduleTimer(now);
                        return;
                    }
                    else if (midDisp >= 0.018 && midDisp > distDelta * 1.8)
                    {
                        // User is dragging both fingers in parallel, not pinching -> hand off to normal 2F drag!
                        _edgePinchCandidate = false;
                        _pendingResolve = (_config.TwoFingerDrag != DragAction.None);
                    }
                    else
                    {
                        _pinchPrevDist = curDist;
                        RescheduleTimer(now);
                        return;
                    }
                }
                else
                {
                    double dDist = curDist - _pinchPrevDist;
                    _pinchPrevDist = curDist;
                    if (Math.Abs(dDist) < 0.30)
                        EmitPinchZoomDelta(dDist, now);
                    RescheduleTimer(now);
                    return;
                }
            }

            double dx, dy;
            if (_config.LockPointerToAimFinger && _primaryAimId >= 0)
            {
                if (!primaryMatched)
                {
                    RescheduleTimer(now);
                    return;
                }
                dx = primaryDx;
                dy = primaryDy;
            }
            else if (_rightDragLatched && _adsFiring && adsMatched > 0)
            {
                dx = adsSumDx / adsMatched;
                dy = adsSumDy / adsMatched;
            }
            else
            {
                dx = sumDx / matched;
                dy = sumDy / matched;
                if (matched >= 2 && maxMagSq > 0.25 && minMagSq < maxMagSq * 0.09)
                {
                    dx = fastDx;
                    dy = fastDy;
                }
            }

            double normDx = dx / maxX;
            double normDy = dy / maxY;
            double dist = Math.Sqrt(normDx * normDx + normDy * normDy);
            if (dist > 0.45)
            {
                RescheduleTimer(now);
                return;
            }

            if (_activeDrag == DragAction.None)
            {
                _accumTravel += dist;
                _multiNetDx += normDx;
                _multiNetDy += normDy;
                if (_pendingResolve)
                    TryResolvePending(now, count);

                if (_activeDrag == DragAction.None && _enteredFromMovingOne)
                    ApplyMove(dx, dy, maxX, frame.PhysMaxX, frame.PhysMaxY, maxY, true);
            }

            if (_activeDrag != DragAction.None)
            {
                ApplyMove(dx, dy, maxX, frame.PhysMaxX, frame.PhysMaxY, maxY, _config.AutoMatchDragSpeed);
            }

            RescheduleTimer(now);
        }

        private static bool IsSameButton(DragAction drag, ClickAction click)
        {
            return (drag == DragAction.LeftDrag && click == ClickAction.LeftClick)
                || (drag == DragAction.RightDrag && click == ClickAction.RightClick)
                || (drag == DragAction.MiddleDrag && click == ClickAction.MiddleClick);
        }

        private void TryResolvePending(double now, int count)
        {
            double since = now - _lastCountChangeMs;
            double stagger = _config.StaggerMs;
            if (since < stagger)
                return;

            DragAction dragAct = (count == 2) ? _config.TwoFingerDrag : (count == 3 ? _config.ThreeFingerDrag : DragAction.None);
            if (dragAct == DragAction.None)
            {
                _pendingResolve = false;
                return;
            }

            ClickAction tapAct = ClickAction.None;
            if (_branch == Branch.OnePlusOne) tapAct = _config.OnePlusOneTap;
            else if (_branch == Branch.OnePlusTwo) tapAct = _config.OnePlusTwoTap;
            else if (_branch == Branch.Fresh2) tapAct = _config.TwoFingerTap;
            else if (_branch == Branch.Fresh3) tapAct = _config.ThreeFingerTap;

            bool immediateAfterStagger = (_enteredFromMovingOne && (tapAct == ClickAction.None || IsSameButton(dragAct, tapAct)));
            bool needTapGuard = (!_enteredFromMovingOne && tapAct != ClickAction.None && !IsSameButton(dragAct, tapAct));
            double netDist = Math.Sqrt(_multiNetDx * _multiNetDx + _multiNetDy * _multiNetDy);
            double freshThreshold = (since < FRESH_DRAG_EARLY_WINDOW_MS)
                ? FRESH_DRAG_EARLY_NET_THRESHOLD
                : FRESH_DRAG_LATE_NET_THRESHOLD;
            bool movedEnough = needTapGuard
                ? (netDist >= freshThreshold)
                : (_accumTravel >= DRAG_MOVE_THRESHOLD);

            if (immediateAfterStagger || movedEnough || since >= STATIONARY_HOLD_MS)
            {
                StartDrag(dragAct, count == 3, now);
                _pressedButtonInSession = true;
                _pendingResolve = false;
            }
        }

        private void TryFireTap(double now)
        {
            if (now < _postDragNoTapUntilMs) return;

            double dt = now - _multiStartMs;
            if (dt < 0 || dt > TAP_MAX_MS) return;

            double netDist = Math.Sqrt(_multiNetDx * _multiNetDx + _multiNetDy * _multiNetDy);
            ClickAction act = ClickAction.None;
            if (_branch == Branch.OnePlusOne) act = _config.OnePlusOneTap;
            else if (_branch == Branch.OnePlusTwo) act = _config.OnePlusTwoTap;
            else if (_branch == Branch.Fresh2 && _peakFingers == 2 && netDist <= TAP_MAX_TRAVEL) act = _config.TwoFingerTap;
            else if (_branch == Branch.Fresh3 && _peakFingers == 3 && netDist <= TAP_MAX_TRAVEL) act = _config.ThreeFingerTap;

            if (act == ClickAction.LeftClick)
                FireTimedClick(MOUSEEVENTF_LEFTDOWN, MOUSEEVENTF_LEFTUP, ref _leftDown, ref _pendingLeftUpMs, ref _leftDownMs, now);
            else if (act == ClickAction.RightClick)
                FireTimedClick(MOUSEEVENTF_RIGHTDOWN, MOUSEEVENTF_RIGHTUP, ref _rightDown, ref _pendingRightUpMs, ref _rightDownMs, now);
            else if (act == ClickAction.MiddleClick)
                FireTimedClick(MOUSEEVENTF_MIDDLEDOWN, MOUSEEVENTF_MIDDLEUP, ref _middleDown, ref _pendingMiddleUpMs, ref _middleDownMs, now);
        }

        private void StartDrag(DragAction btn, bool isThreeFinger, double now)
        {
            _activeDrag = btn;
            _remX = 0;
            _remY = 0;

            if (btn == DragAction.LeftDrag)
            {
                if (_pendingLeftUpMs > 0) _pendingLeftUpMs = 0;
                else if (!_leftDown) { SendFlag(MOUSEEVENTF_LEFTDOWN); _leftDown = true; _leftDownMs = now; }
            }
            else if (btn == DragAction.RightDrag)
            {
                if (isThreeFinger && _config.EnableFpsAdsLatch)
                {
                    _rightDragLatched = true;
                    _rightDragDropTo2Ms = 0;
                    _adsFiring = false;
                    _hasFiredInAds = false;
                }

                if (_pendingRightUpMs > 0) _pendingRightUpMs = 0;
                else if (!_rightDown) { SendFlag(MOUSEEVENTF_RIGHTDOWN); _rightDown = true; _rightDownMs = now; }
            }
            else if (btn == DragAction.MiddleDrag)
            {
                if (_pendingMiddleUpMs > 0) _pendingMiddleUpMs = 0;
                else if (!_middleDown) { SendFlag(MOUSEEVENTF_MIDDLEDOWN); _middleDown = true; _middleDownMs = now; }
            }
        }

        private void StartAdsFire(double now)
        {
            _adsFiring = true;
            _hasFiredInAds = true;
            if (_pendingLeftUpMs > 0) _pendingLeftUpMs = 0;
            else if (!_leftDown) { SendFlag(MOUSEEVENTF_LEFTDOWN); _leftDown = true; _leftDownMs = now; }
        }

        private void ReleaseAdsFire(double now)
        {
            _adsFiring = false;
            double minHold = _config.MinClickHoldMs;
            if (_leftDown)
            {
                if ((now - _leftDownMs) < minHold)
                    _pendingLeftUpMs = _leftDownMs + minHold;
                else
                {
                    SendFlag(MOUSEEVENTF_LEFTUP);
                    _leftDown = false;
                    _pendingLeftUpMs = 0;
                }
            }
        }

        private void StartLeftDragAltRight(double now)
        {
            _leftDragAltRight = true;
            if (_pendingRightUpMs > 0) _pendingRightUpMs = 0;
            else if (!_rightDown) { SendFlag(MOUSEEVENTF_RIGHTDOWN); _rightDown = true; _rightDownMs = now; }
        }

        private void ReleaseLeftDragAltRight(double now)
        {
            _leftDragAltRight = false;
            double minHold = _config.MinClickHoldMs;
            if (_rightDown)
            {
                if ((now - _rightDownMs) < minHold)
                    _pendingRightUpMs = _rightDownMs + minHold;
                else
                {
                    SendFlag(MOUSEEVENTF_RIGHTUP);
                    _rightDown = false;
                    _pendingRightUpMs = 0;
                }
            }
        }

        private void EndAllDrags(double now)
        {
            bool hadDrag = (_activeDrag != DragAction.None || _rightDragLatched || _adsFiring || _leftDragAltRight);
            double minHold = _config.MinClickHoldMs;

            if (_adsFiring)
                ReleaseAdsFire(now);
            if (_leftDragAltRight)
                ReleaseLeftDragAltRight(now);

            if (_activeDrag == DragAction.LeftDrag && _leftDown)
            {
                if ((now - _leftDownMs) < minHold) _pendingLeftUpMs = _leftDownMs + minHold;
                else { SendFlag(MOUSEEVENTF_LEFTUP); _leftDown = false; _pendingLeftUpMs = 0; }
            }
            else if ((_activeDrag == DragAction.RightDrag || _rightDragLatched) && _rightDown)
            {
                if ((now - _rightDownMs) < minHold) _pendingRightUpMs = _rightDownMs + minHold;
                else { SendFlag(MOUSEEVENTF_RIGHTUP); _rightDown = false; _pendingRightUpMs = 0; }
            }
            else if (_activeDrag == DragAction.MiddleDrag && _middleDown)
            {
                if ((now - _middleDownMs) < minHold) _pendingMiddleUpMs = _middleDownMs + minHold;
                else { SendFlag(MOUSEEVENTF_MIDDLEUP); _middleDown = false; _pendingMiddleUpMs = 0; }
            }

            _activeDrag = DragAction.None;
            _rightDragLatched = false;
            _rightDragDropTo2Ms = 0;
            _adsFiring = false;
            _hasFiredInAds = false;
            _leftDragAltRight = false;
            _adsAimId1 = -1;
            _adsAimId2 = -1;

            if (hadDrag)
                _postDragNoTapUntilMs = now + POST_DRAG_NO_TAP_MS;
        }

        private void FireTimedClick(uint downFlag, uint upFlag, ref bool isDown, ref double pendingUpMs, ref double downMs, double now)
        {
            if (isDown && pendingUpMs > 0)
            {
                SendFlag(upFlag);
                isDown = false;
                pendingUpMs = 0;
            }
            if (!isDown)
            {
                SendFlag(downFlag);
                isDown = true;
                downMs = now;
                pendingUpMs = now + _config.MinClickHoldMs;
            }
        }

        private void ProcessPendingButtonUps(double now)
        {
            if (_pendingLeftUpMs > 0 && now >= _pendingLeftUpMs)
            {
                _pendingLeftUpMs = 0;
                if (_leftDown && _activeDrag != DragAction.LeftDrag && !_adsFiring)
                {
                    SendFlag(MOUSEEVENTF_LEFTUP);
                    _leftDown = false;
                }
            }
            if (_pendingRightUpMs > 0 && now >= _pendingRightUpMs)
            {
                _pendingRightUpMs = 0;
                if (_rightDown && _activeDrag != DragAction.RightDrag && !_rightDragLatched && !_leftDragAltRight)
                {
                    SendFlag(MOUSEEVENTF_RIGHTUP);
                    _rightDown = false;
                }
            }
            if (_pendingMiddleUpMs > 0 && now >= _pendingMiddleUpMs)
            {
                _pendingMiddleUpMs = 0;
                if (_middleDown && _activeDrag != DragAction.MiddleDrag)
                {
                    SendFlag(MOUSEEVENTF_MIDDLEUP);
                    _middleDown = false;
                }
            }
        }

        private void ReleaseAllButtonsImmediate()
        {
            _activeDrag = DragAction.None;
            _rightDragLatched = false;
            _adsFiring = false;
            _hasFiredInAds = false;
            _leftDragAltRight = false;
            _pendingLeftUpMs = 0;
            _pendingRightUpMs = 0;
            _pendingMiddleUpMs = 0;
            if (_leftDown) { SendFlag(MOUSEEVENTF_LEFTUP); _leftDown = false; }
            if (_rightDown) { SendFlag(MOUSEEVENTF_RIGHTUP); _rightDown = false; }
            if (_middleDown) { SendFlag(MOUSEEVENTF_MIDDLEUP); _middleDown = false; }
        }

        private void RescheduleTimer(double now)
        {
            if (_scheduleTimerMs == null) return;

            double nextMs = double.MaxValue;

            if (_pendingLeftUpMs > 0) nextMs = Math.Min(nextMs, Math.Max(1.0, _pendingLeftUpMs - now));
            if (_pendingRightUpMs > 0) nextMs = Math.Min(nextMs, Math.Max(1.0, _pendingRightUpMs - now));
            if (_pendingMiddleUpMs > 0) nextMs = Math.Min(nextMs, Math.Max(1.0, _pendingMiddleUpMs - now));

            if (_pendingResolve && _activeDrag == DragAction.None && !_cooldownUntilLift && !_suppressedByPhysBtn && _prevCount >= 2)
            {
                DragAction dragAct = (_prevCount == 2) ? _config.TwoFingerDrag : (_prevCount == 3 ? _config.ThreeFingerDrag : DragAction.None);
                ClickAction tapAct = ClickAction.None;
                if (_branch == Branch.OnePlusOne) tapAct = _config.OnePlusOneTap;
                else if (_branch == Branch.OnePlusTwo) tapAct = _config.OnePlusTwoTap;
                else if (_branch == Branch.Fresh2) tapAct = _config.TwoFingerTap;
                else if (_branch == Branch.Fresh3) tapAct = _config.ThreeFingerTap;

                bool fastCommit = (_enteredFromMovingOne && (tapAct == ClickAction.None || IsSameButton(dragAct, tapAct)));
                double target = _lastCountChangeMs + (fastCommit ? _config.StaggerMs : STATIONARY_HOLD_MS);
                nextMs = Math.Min(nextMs, Math.Max(1.0, target - now));
            }

            if (nextMs < double.MaxValue)
                _scheduleTimerMs((int)Math.Ceiling(nextMs));
            else
                _scheduleTimerMs(0);
        }

        private void ApplyBridgeMove(double dx, double dy, int maxX, int physMaxX, int physMaxY, int maxY, double maxDist)
        {
            double ndx = dx / maxX;
            double ndy = dy / maxY;
            if (Math.Sqrt(ndx * ndx + ndy * ndy) > maxDist) return;

            double pxX, pxY;
            GetScale(maxX, physMaxX, physMaxY, maxY, true, out pxX, out pxY);
            _bridgeRemX += dx * pxX;
            _bridgeRemY += dy * pxY;
            int ix = (int)Math.Truncate(_bridgeRemX);
            int iy = (int)Math.Truncate(_bridgeRemY);
            if (ix != 0 || iy != 0)
            {
                _bridgeRemX -= ix;
                _bridgeRemY -= iy;
                SendMove(ix, iy);
            }
        }

        private void ApplyMove(double dx, double dy, int maxX, int physMaxX, int physMaxY, int maxY, bool preferNative)
        {
            double pxX, pxY;
            GetScale(maxX, physMaxX, physMaxY, maxY, preferNative, out pxX, out pxY);
            _remX += dx * pxX;
            _remY += dy * pxY;
            int ix = (int)Math.Truncate(_remX);
            int iy = (int)Math.Truncate(_remY);
            if (ix != 0 || iy != 0)
            {
                _remX -= ix;
                _remY -= iy;
                SendMove(ix, iy);
            }
        }

        private void GetScale(int maxX, int physMaxX, int physMaxY, int maxY, bool preferNative, out double pxX, out double pxY)
        {
            if (preferNative && _nativePxPerUnitX > 0.05)
            {
                pxX = _nativePxPerUnitX;
                if (_nativePxPerUnitY > 0.05)
                {
                    pxY = _nativePxPerUnitY;
                    return;
                }
            }
            else
            {
                double mult = _config.AutoMatchDragSpeed ? 1.0 : (_config.ManualDragSpeedPercent / 100.0);
                pxX = (1800.0 * mult) / Math.Max(1.0, maxX);
            }

            pxY = pxX;
            if (physMaxX > 0 && physMaxY > 0 && maxX > 0 && maxY > 0)
            {
                double uX = (double)maxX / physMaxX;
                double uY = (double)maxY / physMaxY;
                if (uY > 0.001) pxY = pxX * (uX / uY);
            }
        }

        private static int PickIndexFingerId(TouchContact[] contacts, int count, int maxX, int maxY)
        {
            if (contacts == null || count <= 0) return -1;
            int bestId = contacts[0].ContactId;
            double bestScore = double.NegativeInfinity;
            double invX = 1.0 / Math.Max(1.0, maxX);
            double invY = 1.0 / Math.Max(1.0, maxY);

            for (int i = 0; i < count; i++)
            {
                double nx = contacts[i].X * invX;
                double ny = contacts[i].Y * invY;
                double score = -nx + 0.5 * ny;
                if (score > bestScore)
                {
                    bestScore = score;
                    bestId = contacts[i].ContactId;
                }
            }
            return bestId;
        }

        private void StorePrevContacts(TouchContact[] contacts, int count)
        {
            if (contacts == null || count <= 0)
            {
                _prevStoredCount = 0;
                return;
            }
            int n = Math.Min(count, _prev.Length);
            for (int i = 0; i < n; i++)
            {
                _prev[i].Id = contacts[i].ContactId;
                _prev[i].X = contacts[i].X;
                _prev[i].Y = contacts[i].Y;
            }
            _prevStoredCount = n;
        }

        private void ResetSession()
        {
            _prevStoredCount = 0;
            _prevCount = 0;
            _peakFingers = 0;
            _accumTravel = 0;
            _multiNetDx = 0;
            _multiNetDy = 0;
            _oneTravel = 0;
            _oneEstablished = false;
            _primaryAimId = -1;
            _branch = Branch.None;
            _activeDrag = DragAction.None;
            _enteredFromMovingOne = false;
            _pressedButtonInSession = false;
            _cooldownUntilLift = false;
            _suppressedByPhysBtn = false;
            _pendingResolve = false;
            _rightDragLatched = false;
            _rightDragDropTo2Ms = 0;
            _adsFiring = false;
            _hasFiredInAds = false;
            _leftDragAltRight = false;
            _adsAimId1 = -1;
            _adsAimId2 = -1;
            _bridgingOne = false;
            _bridgeRemX = 0;
            _bridgeRemY = 0;
            CancelBottomEdgeScroll();
            CancelEdgePinch();
        }

        private void EmitPinchZoomDelta(double dDist, double now)
        {
            _pinchAccum += dDist;
            if (_lastPinchEmitMs == 0 || (now - _lastPinchEmitMs) >= EDGE_PINCH_MIN_INTERVAL_MS)
            {
                if (Math.Abs(_pinchAccum) >= EDGE_PINCH_STEP_DIST)
                {
                    int sign = _pinchAccum > 0 ? 1 : -1;
                    _pinchAccum -= sign * EDGE_PINCH_STEP_DIST;
                    // Clamp leftover accumulator so rapid bursts never overshoot after fingers stop
                    if (Math.Abs(_pinchAccum) > EDGE_PINCH_STEP_DIST * 1.5)
                        _pinchAccum = sign * EDGE_PINCH_STEP_DIST * 0.5;
                    _lastPinchEmitMs = now;
                    SendPinchZoom(sign * WHEEL_DELTA);
                }
            }
        }

        private void SendPinchZoom(int wheelDelta)
        {
            bool ctrlAlreadyDown = (GetAsyncKeyState(VK_CONTROL) & 0x8000) != 0;
            if (ctrlAlreadyDown)
            {
                INPUT inp = new INPUT();
                inp.type = INPUT_MOUSE;
                inp.u.mi.mouseData = unchecked((uint)wheelDelta);
                inp.u.mi.dwFlags = MOUSEEVENTF_WHEEL;
                inp.u.mi.dwExtraInfo = ExtraInfoPtr;
                SendInput(1, &inp, InputSize);
                return;
            }

            // Atomic Ctrl Down + Wheel + Ctrl Up in one SendInput batch (Ctrl can NEVER stick!)
            INPUT* batch = stackalloc INPUT[3];
            batch[0] = new INPUT();
            batch[0].type = INPUT_KEYBOARD;
            batch[0].u.ki.wVk = VK_CONTROL;
            batch[0].u.ki.dwFlags = 0;
            batch[0].u.ki.dwExtraInfo = ExtraInfoPtr;

            batch[1] = new INPUT();
            batch[1].type = INPUT_MOUSE;
            batch[1].u.mi.mouseData = unchecked((uint)wheelDelta);
            batch[1].u.mi.dwFlags = MOUSEEVENTF_WHEEL;
            batch[1].u.mi.dwExtraInfo = ExtraInfoPtr;

            batch[2] = new INPUT();
            batch[2].type = INPUT_KEYBOARD;
            batch[2].u.ki.wVk = VK_CONTROL;
            batch[2].u.ki.dwFlags = KEYEVENTF_KEYUP;
            batch[2].u.ki.dwExtraInfo = ExtraInfoPtr;

            SendInput(3, batch, InputSize);
        }

        private void EmitRightVScrollDelta(double ndy, double now)
        {
            if (ndy == 0.0) return;

            // Default (InvertVerticalScroll == false): natural direction (slide DOWN -> +120, slide UP -> -120)
            // Inverted (InvertVerticalScroll == true): classic direction (slide DOWN -> -120, slide UP -> +120)
            double dirSign = _config.InvertVerticalScroll ? -1.0 : 1.0;
            double signedNotch = ndy * RIGHT_VSCROLL_NOTCHES_PER_HEIGHT * dirSign;

            // Instant direction reversal
            if ((signedNotch > 0 && _rightVScrollAccum < 0) || (signedNotch < 0 && _rightVScrollAccum > 0))
                _rightVScrollAccum = 0;

            _rightVScrollAccum += signedNotch;

            if (_rightVScrollAccum > 1.5) _rightVScrollAccum = 1.5;
            else if (_rightVScrollAccum < -1.5) _rightVScrollAccum = -1.5;

            if (_lastVScrollEmitMs == 0 || (now - _lastVScrollEmitMs) >= RIGHT_VSCROLL_INTERVAL_MS)
            {
                if (Math.Abs(_rightVScrollAccum) >= 1.0)
                {
                    int sign = _rightVScrollAccum > 0 ? 1 : -1;
                    _rightVScrollAccum -= sign;
                    _lastVScrollEmitMs = now;
                    SendVWheel(sign * WHEEL_DELTA);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SendVWheel(int delta)
        {
            INPUT inp = new INPUT();
            inp.type = INPUT_MOUSE;
            inp.u.mi.mouseData = unchecked((uint)delta);
            inp.u.mi.dwFlags = MOUSEEVENTF_WHEEL;
            inp.u.mi.dwExtraInfo = ExtraInfoPtr;
            SendInput(1, &inp, InputSize);
        }

        private void EmitBottomHScrollDelta(double ndx, double now)
        {
            if (ndx == 0.0) return;

            // Instant direction reversal: don't make the user unwind leftover opposite-direction remainder
            if ((ndx > 0 && _bottomHScrollAccum < 0) || (ndx < 0 && _bottomHScrollAccum > 0))
                _bottomHScrollAccum = 0;

            _bottomHScrollAccum += ndx * BOTTOM_HSCROLL_NOTCHES_PER_WIDTH;

            // Clamp accumulator so rapid swipes never queue up trailing scroll ticks after finger lift
            if (_bottomHScrollAccum > 1.5) _bottomHScrollAccum = 1.5;
            else if (_bottomHScrollAccum < -1.5) _bottomHScrollAccum = -1.5;

            if (_lastHScrollEmitMs == 0 || (now - _lastHScrollEmitMs) >= BOTTOM_HSCROLL_INTERVAL_MS)
            {
                if (Math.Abs(_bottomHScrollAccum) >= 1.0)
                {
                    int sign = _bottomHScrollAccum > 0 ? 1 : -1;
                    _bottomHScrollAccum -= sign;
                    _lastHScrollEmitMs = now;
                    // Exact standard mouse WHEEL_DELTA (±120) so ALL Windows apps (Win32, Excel, Explorer, Chrome, VS Code, etc.)
                    // handle it identically to a real horizontal mouse wheel without dropping sub-120 deltas!
                    SendHWheel(sign * WHEEL_DELTA);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SendHWheel(int delta)
        {
            INPUT inp = new INPUT();
            inp.type = INPUT_MOUSE;
            inp.u.mi.mouseData = unchecked((uint)delta);
            inp.u.mi.dwFlags = MOUSEEVENTF_HWHEEL;
            inp.u.mi.dwExtraInfo = ExtraInfoPtr;
            SendInput(1, &inp, InputSize);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SendFlag(uint flag)
        {
            INPUT inp = new INPUT();
            inp.type = INPUT_MOUSE;
            inp.u.mi.dwFlags = flag;
            inp.u.mi.dwExtraInfo = ExtraInfoPtr;
            SendInput(1, &inp, InputSize);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SendMove(int dx, int dy)
        {
            INPUT inp = new INPUT();
            inp.type = INPUT_MOUSE;
            inp.u.mi.dx = dx;
            inp.u.mi.dy = dy;
            inp.u.mi.dwFlags = MOUSEEVENTF_MOVE;
            inp.u.mi.dwExtraInfo = ExtraInfoPtr;
            SendInput(1, &inp, InputSize);
        }

        public void Dispose()
        {
            ForceReleaseAll();
            uint tid = _hookThreadId;
            if (tid != 0)
            {
                PostThreadMessage(tid, WM_QUIT, IntPtr.Zero, IntPtr.Zero);
                try { if (_hookThread != null) _hookThread.Join(300); } catch { }
            }
        }
    }
}
