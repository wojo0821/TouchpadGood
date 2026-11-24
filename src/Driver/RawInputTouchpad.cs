using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace TouchpadGood
{
    public struct TouchContact
    {
        public int ContactId;
        public int X;
        public int Y;
        public bool TipSwitch;
        public bool Confidence;
    }

    public sealed class TouchFrame
    {
        public TouchContact[] Contacts;
        public int ContactCount;
        public int MaxX;
        public int MaxY;
        public int PhysMaxX;
        public int PhysMaxY;
        public bool PhysicalButtonDown;
        public double TimestampMs;
    }

    /// <summary>
    /// Ultra-low-latency, zero-allocation, zero-copy unsafe Win32 Raw Input parser.
    /// - Single-syscall GetRawInputData into pre-allocated unmanaged buffer.
    /// - Direct unmanaged pointer casts (zero Marshal.PtrToStructure boxing).
    /// - Direct unmanaged byte* passed to hid.dll (zero Marshal.Copy / GC pinning).
    /// - Stack-allocated usage buffer & 64-bit palm-rejection bitmask (0 bytes allocated/sec).
    /// </summary>
    public sealed unsafe class RawInputTouchpad : IDisposable
    {
        public const uint SYNTHETIC_EXTRA_INFO_TAG = 0x54504452; // "TPDR"

        public delegate void FrameHandler(TouchFrame frame);
        public delegate void NativeMouseHandler(int dx, int dy, double timestampMs);

        public event FrameHandler FrameReceived;
        public event NativeMouseHandler NativeMouseMoved;

        private const uint RID_INPUT = 0x10000003;
        private const uint RIDI_PREPARSEDDATA = 0x20000005;
        private const uint RIM_TYPEMOUSE = 0;
        private const uint RIM_TYPEHID = 2;
        private const uint RIDEV_REMOVE = 0x00000001;
        private const uint RIDEV_INPUTSINK = 0x00000100;
        private const ushort MOUSE_MOVE_ABSOLUTE = 0x0001;

        private const ushort HID_USAGE_PAGE_GENERIC = 0x01;
        private const ushort HID_USAGE_GENERIC_MOUSE = 0x02;
        private const ushort HID_USAGE_GENERIC_X = 0x30;
        private const ushort HID_USAGE_GENERIC_Y = 0x31;

        private const ushort HID_USAGE_PAGE_BUTTON = 0x09;
        private const ushort HID_USAGE_BUTTON_1 = 0x01;

        private const ushort HID_USAGE_PAGE_DIGITIZER = 0x0D;
        private const ushort HID_USAGE_DIGITIZER_TOUCH_PAD = 0x05;
        private const ushort HID_USAGE_DIGITIZER_TIP_SWITCH = 0x42;
        private const ushort HID_USAGE_DIGITIZER_CONFIDENCE = 0x47;
        private const ushort HID_USAGE_DIGITIZER_CONTACT_ID = 0x51;
        private const ushort HID_USAGE_DIGITIZER_CONTACT_COUNT = 0x54;

        private const int HidP_Input = 0;
        private const uint HIDP_STATUS_SUCCESS = 0x00110000;

        private static readonly double TicksToMs = 1000.0 / Stopwatch.Frequency;
        private static readonly uint HeaderSize = (uint)sizeof(RAWINPUTHEADER);
        private static readonly int RawHidHeaderSize = sizeof(RAWHID_HEADER);

        [StructLayout(LayoutKind.Sequential)]
        private struct RAWINPUTDEVICE
        {
            public ushort usUsagePage;
            public ushort usUsage;
            public uint dwFlags;
            public IntPtr hwndTarget;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RAWINPUTHEADER
        {
            public uint dwType;
            public uint dwSize;
            public IntPtr hDevice;
            public IntPtr wParam;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RAWHID_HEADER
        {
            public uint dwSizeHid;
            public uint dwCount;
        }

        [StructLayout(LayoutKind.Explicit, Size = 24)]
        private struct RAWMOUSE
        {
            [FieldOffset(0)] public ushort usFlags;
            [FieldOffset(4)] public uint ulButtons;
            [FieldOffset(8)] public uint ulRawButtons;
            [FieldOffset(12)] public int lLastX;
            [FieldOffset(16)] public int lLastY;
            [FieldOffset(20)] public uint ulExtraInformation;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct HIDP_CAPS
        {
            public ushort Usage;
            public ushort UsagePage;
            public ushort InputReportByteLength;
            public ushort OutputReportByteLength;
            public ushort FeatureReportByteLength;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)]
            public ushort[] Reserved;
            public ushort NumberLinkCollectionNodes;
            public ushort NumberInputButtonCaps;
            public ushort NumberInputValueCaps;
            public ushort NumberInputDataIndices;
            public ushort NumberOutputButtonCaps;
            public ushort NumberOutputValueCaps;
            public ushort NumberOutputDataIndices;
            public ushort NumberFeatureButtonCaps;
            public ushort NumberFeatureValueCaps;
            public ushort NumberFeatureDataIndices;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct HIDP_VALUE_CAPS
        {
            public ushort UsagePage;
            public byte ReportID;
            [MarshalAs(UnmanagedType.U1)] public bool IsAlias;
            public ushort BitField;
            public ushort LinkCollection;
            public ushort LinkUsage;
            public ushort LinkUsagePage;
            [MarshalAs(UnmanagedType.U1)] public bool IsRange;
            [MarshalAs(UnmanagedType.U1)] public bool IsStringRange;
            [MarshalAs(UnmanagedType.U1)] public bool IsDesignatorRange;
            [MarshalAs(UnmanagedType.U1)] public bool IsAbsolute;
            [MarshalAs(UnmanagedType.U1)] public bool HasNull;
            public byte Reserved;
            public ushort BitSize;
            public ushort ReportCount;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 5)]
            public ushort[] Reserved2;
            public uint UnitsExp;
            public uint Units;
            public int LogicalMin;
            public int LogicalMax;
            public int PhysicalMin;
            public int PhysicalMax;
            public ushort UsageMin;
            public ushort UsageMax;
            public ushort StringMin;
            public ushort StringMax;
            public ushort DesignatorMin;
            public ushort DesignatorMax;
            public ushort DataIndexMin;
            public ushort DataIndexMax;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct HIDP_BUTTON_CAPS
        {
            public ushort UsagePage;
            public byte ReportID;
            [MarshalAs(UnmanagedType.U1)] public bool IsAlias;
            public ushort BitField;
            public ushort LinkCollection;
            public ushort LinkUsage;
            public ushort LinkUsagePage;
            [MarshalAs(UnmanagedType.U1)] public bool IsRange;
            [MarshalAs(UnmanagedType.U1)] public bool IsStringRange;
            [MarshalAs(UnmanagedType.U1)] public bool IsDesignatorRange;
            [MarshalAs(UnmanagedType.U1)] public bool IsAbsolute;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 10)]
            public uint[] Reserved;
            public ushort UsageMin;
            public ushort UsageMax;
            public ushort StringMin;
            public ushort StringMax;
            public ushort DesignatorMin;
            public ushort DesignatorMax;
            public ushort DataIndexMin;
            public ushort DataIndexMax;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterRawInputDevices(RAWINPUTDEVICE* pRawInputDevices, uint uiNumDevices, uint cbSize);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetRawInputData(IntPtr hRawInput, uint uiCommand, void* pData, ref uint pcbSize, uint cbSizeHeader);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetRawInputDeviceInfo(IntPtr hDevice, uint uiCommand, IntPtr pData, ref uint pcbSize);

        [DllImport("hid.dll", SetLastError = true)]
        private static extern uint HidP_GetCaps(IntPtr preparsedData, out HIDP_CAPS capabilities);

        [DllImport("hid.dll", SetLastError = true)]
        private static extern uint HidP_GetValueCaps(int reportType, [Out] HIDP_VALUE_CAPS[] valueCaps, ref ushort valueCapsLength, IntPtr preparsedData);

        [DllImport("hid.dll", SetLastError = true)]
        private static extern uint HidP_GetButtonCaps(int reportType, [Out] HIDP_BUTTON_CAPS[] buttonCaps, ref ushort buttonCapsLength, IntPtr preparsedData);

        [DllImport("hid.dll", SetLastError = true)]
        private static extern uint HidP_GetUsageValue(int reportType, ushort usagePage, ushort linkCollection, ushort usage, out uint usageValue, IntPtr preparsedData, byte* report, uint reportLength);

        [DllImport("hid.dll", SetLastError = true)]
        private static extern uint HidP_GetUsages(int reportType, ushort usagePage, ushort linkCollection, ushort* usageList, ref uint usageLength, IntPtr preparsedData, byte* report, uint reportLength);

        private sealed class DeviceCache : IDisposable
        {
            public IntPtr PreparsedData;
            public ushort ContactCountLink;
            public ushort[] ContactLinks;
            public int MinX;
            public int MinY;
            public int MaxX = 3200;
            public int MaxY = 2000;
            public int PhysMaxX;
            public int PhysMaxY;
            public bool HasConfidence;
            public bool HasPhysicalButton;
            public ushort PhysicalButtonLink;

            public readonly TouchContact[] Pending = new TouchContact[16];
            public int PendingCount;
            public int ExpectedContacts;
            public bool PendingButtonDown;
            public ulong RejectedMask; // Fast 64-bit palm-rejection mask (0 allocations!)

            public void Dispose()
            {
                if (PreparsedData != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(PreparsedData);
                    PreparsedData = IntPtr.Zero;
                }
            }
        }

        private readonly Dictionary<IntPtr, DeviceCache> _devices = new Dictionary<IntPtr, DeviceCache>(4);
        private IntPtr _lastDeviceHandle = IntPtr.Zero;
        private DeviceCache _lastDeviceCache;
        private bool _registered;

        private byte* _rawBuf;
        private uint _rawBufSize;
        private readonly TouchContact[] _activeBuf = new TouchContact[16];
        private readonly TouchFrame _frame = new TouchFrame();

        public RawInputTouchpad()
        {
            _rawBufSize = 512;
            _rawBuf = (byte*)Marshal.AllocHGlobal((int)_rawBufSize);
        }

        public bool Register(IntPtr hwnd)
        {
            RAWINPUTDEVICE* rid = stackalloc RAWINPUTDEVICE[2];
            rid[0].usUsagePage = HID_USAGE_PAGE_DIGITIZER;
            rid[0].usUsage = HID_USAGE_DIGITIZER_TOUCH_PAD;
            rid[0].dwFlags = RIDEV_INPUTSINK;
            rid[0].hwndTarget = hwnd;

            rid[1].usUsagePage = HID_USAGE_PAGE_GENERIC;
            rid[1].usUsage = HID_USAGE_GENERIC_MOUSE;
            rid[1].dwFlags = RIDEV_INPUTSINK;
            rid[1].hwndTarget = hwnd;

            _registered = RegisterRawInputDevices(rid, 2, (uint)sizeof(RAWINPUTDEVICE));
            return _registered;
        }

        public void ResetDeviceCache()
        {
            _lastDeviceHandle = IntPtr.Zero;
            _lastDeviceCache = null;
            foreach (var kv in _devices)
                kv.Value.Dispose();
            _devices.Clear();
        }

        private DeviceCache GetOrInitDevice(IntPtr hDevice)
        {
            if (hDevice == _lastDeviceHandle && _lastDeviceCache != null)
                return _lastDeviceCache;

            DeviceCache cache;
            if (_devices.TryGetValue(hDevice, out cache))
            {
                _lastDeviceHandle = hDevice;
                _lastDeviceCache = cache;
                return cache;
            }

            uint size = 0;
            if (GetRawInputDeviceInfo(hDevice, RIDI_PREPARSEDDATA, IntPtr.Zero, ref size) != 0 || size == 0)
                return null;

            IntPtr ptr = Marshal.AllocHGlobal((int)size);
            if (GetRawInputDeviceInfo(hDevice, RIDI_PREPARSEDDATA, ptr, ref size) == unchecked((uint)-1))
            {
                Marshal.FreeHGlobal(ptr);
                return null;
            }

            HIDP_CAPS caps;
            if (HidP_GetCaps(ptr, out caps) != HIDP_STATUS_SUCCESS)
            {
                Marshal.FreeHGlobal(ptr);
                return null;
            }

            cache = new DeviceCache { PreparsedData = ptr };

            ushort valCount = caps.NumberInputValueCaps;
            if (valCount > 0)
            {
                var vcs = new HIDP_VALUE_CAPS[valCount];
                if (HidP_GetValueCaps(HidP_Input, vcs, ref valCount, ptr) == HIDP_STATUS_SUCCESS)
                {
                    var links = new List<ushort>(8);
                    for (int i = 0; i < valCount; i++)
                    {
                        var vc = vcs[i];
                        if (vc.UsagePage == HID_USAGE_PAGE_DIGITIZER && vc.UsageMin == HID_USAGE_DIGITIZER_CONTACT_COUNT)
                        {
                            cache.ContactCountLink = vc.LinkCollection;
                        }
                        else if (vc.UsagePage == HID_USAGE_PAGE_DIGITIZER && vc.UsageMin == HID_USAGE_DIGITIZER_CONTACT_ID)
                        {
                            if (vc.LinkCollection > 0 && !links.Contains(vc.LinkCollection))
                                links.Add(vc.LinkCollection);
                        }
                        else if (vc.UsagePage == HID_USAGE_PAGE_GENERIC && vc.UsageMin == HID_USAGE_GENERIC_X)
                        {
                            int span = vc.LogicalMax - vc.LogicalMin;
                            if (span > 0) { cache.MinX = vc.LogicalMin; cache.MaxX = span; }
                            int pSpan = vc.PhysicalMax - vc.PhysicalMin;
                            if (pSpan > 0) cache.PhysMaxX = pSpan;
                            if (vc.LinkCollection > 0 && !links.Contains(vc.LinkCollection))
                                links.Add(vc.LinkCollection);
                        }
                        else if (vc.UsagePage == HID_USAGE_PAGE_GENERIC && vc.UsageMin == HID_USAGE_GENERIC_Y)
                        {
                            int span = vc.LogicalMax - vc.LogicalMin;
                            if (span > 0) { cache.MinY = vc.LogicalMin; cache.MaxY = span; }
                            int pSpan = vc.PhysicalMax - vc.PhysicalMin;
                            if (pSpan > 0) cache.PhysMaxY = pSpan;
                        }
                    }
                    links.Sort();
                    cache.ContactLinks = links.ToArray();
                }
            }

            ushort btnCount = caps.NumberInputButtonCaps;
            if (btnCount > 0)
            {
                var bcs = new HIDP_BUTTON_CAPS[btnCount];
                if (HidP_GetButtonCaps(HidP_Input, bcs, ref btnCount, ptr) == HIDP_STATUS_SUCCESS)
                {
                    for (int i = 0; i < btnCount; i++)
                    {
                        var bc = bcs[i];
                        if (bc.UsagePage == HID_USAGE_PAGE_DIGITIZER &&
                            bc.UsageMin <= HID_USAGE_DIGITIZER_CONFIDENCE &&
                            bc.UsageMax >= HID_USAGE_DIGITIZER_CONFIDENCE)
                        {
                            cache.HasConfidence = true;
                        }
                        else if (bc.UsagePage == HID_USAGE_PAGE_BUTTON &&
                                 bc.UsageMin <= HID_USAGE_BUTTON_1 &&
                                 bc.UsageMax >= HID_USAGE_BUTTON_1)
                        {
                            cache.HasPhysicalButton = true;
                            cache.PhysicalButtonLink = bc.LinkCollection;
                        }
                    }
                }
            }

            if (cache.ContactLinks == null)
                cache.ContactLinks = new ushort[0];

            _devices[hDevice] = cache;
            _lastDeviceHandle = hDevice;
            _lastDeviceCache = cache;
            return cache;
        }

        public void ProcessMessage(IntPtr lParam)
        {
            // Fast single-syscall GetRawInputData directly into pre-allocated unmanaged buffer!
            uint size = _rawBufSize;
            uint res = GetRawInputData(lParam, RID_INPUT, _rawBuf, ref size, HeaderSize);
            if (res == unchecked((uint)-1))
            {
                if (GetRawInputData(lParam, RID_INPUT, null, ref size, HeaderSize) != 0 || size == 0)
                    return;
                if (size > _rawBufSize)
                {
                    if (_rawBuf != null) Marshal.FreeHGlobal((IntPtr)_rawBuf);
                    _rawBufSize = Math.Max(size, 512u);
                    _rawBuf = (byte*)Marshal.AllocHGlobal((int)_rawBufSize);
                }
                if (GetRawInputData(lParam, RID_INPUT, _rawBuf, ref size, HeaderSize) != size)
                    return;
            }
            else if (res == 0)
            {
                return;
            }

            RAWINPUTHEADER* header = (RAWINPUTHEADER*)_rawBuf;

            if (header->dwType == RIM_TYPEMOUSE)
            {
                RAWMOUSE* rm = (RAWMOUSE*)(_rawBuf + HeaderSize);
                if ((rm->usFlags & MOUSE_MOVE_ABSOLUTE) == 0 &&
                    (rm->lLastX != 0 || rm->lLastY != 0) &&
                    rm->ulExtraInformation != SYNTHETIC_EXTRA_INFO_TAG)
                {
                    var mh = NativeMouseMoved;
                    if (mh != null)
                        mh(rm->lLastX, rm->lLastY, Stopwatch.GetTimestamp() * TicksToMs);
                }
                return;
            }

            if (header->dwType != RIM_TYPEHID)
                return;

            var cache = GetOrInitDevice(header->hDevice);
            if (cache == null || cache.ContactLinks.Length == 0)
                return;

            RAWHID_HEADER* rawHid = (RAWHID_HEADER*)(_rawBuf + HeaderSize);
            uint hidSize = rawHid->dwSizeHid;
            uint hidCount = rawHid->dwCount;
            if (hidSize == 0 || hidCount == 0)
                return;

            byte* baseDataPtr = _rawBuf + HeaderSize + RawHidHeaderSize;
            ushort* usageBuf = stackalloc ushort[16];
            double nowMs = Stopwatch.GetTimestamp() * TicksToMs;
            IntPtr pp = cache.PreparsedData;

            for (uint r = 0; r < hidCount; r++)
            {
                byte* reportPtr = baseDataPtr + (r * hidSize);

                bool physBtn = false;
                if (cache.HasPhysicalButton)
                {
                    uint btnLen = 16;
                    if (HidP_GetUsages(HidP_Input, HID_USAGE_PAGE_BUTTON, cache.PhysicalButtonLink,
                        usageBuf, ref btnLen, pp, reportPtr, hidSize) == HIDP_STATUS_SUCCESS)
                    {
                        for (uint b = 0; b < btnLen; b++)
                        {
                            if (usageBuf[b] == HID_USAGE_BUTTON_1) { physBtn = true; break; }
                        }
                    }
                }

                uint contactCount = 0;
                bool hasCount = HidP_GetUsageValue(HidP_Input, HID_USAGE_PAGE_DIGITIZER, cache.ContactCountLink,
                    HID_USAGE_DIGITIZER_CONTACT_COUNT, out contactCount, pp, reportPtr, hidSize) == HIDP_STATUS_SUCCESS;

                if (hasCount)
                {
                    if (contactCount > 0)
                    {
                        cache.PendingCount = 0;
                        cache.ExpectedContacts = (int)contactCount;
                        cache.PendingButtonDown = physBtn;
                    }
                    else if (cache.ExpectedContacts == 0)
                    {
                        cache.PendingCount = 0;
                        cache.PendingButtonDown = physBtn;
                    }
                    else
                    {
                        cache.PendingButtonDown |= physBtn;
                    }
                }
                else
                {
                    cache.PendingButtonDown = physBtn;
                }

                int slotsRead = 0;
                ushort[] links = cache.ContactLinks;
                for (int li = 0; li < links.Length; li++)
                {
                    ushort link = links[li];
                    uint cid = 0, x = 0, y = 0;
                    bool hasCid = HidP_GetUsageValue(HidP_Input, HID_USAGE_PAGE_DIGITIZER, link, HID_USAGE_DIGITIZER_CONTACT_ID, out cid, pp, reportPtr, hidSize) == HIDP_STATUS_SUCCESS;
                    bool hasX = HidP_GetUsageValue(HidP_Input, HID_USAGE_PAGE_GENERIC, link, HID_USAGE_GENERIC_X, out x, pp, reportPtr, hidSize) == HIDP_STATUS_SUCCESS;
                    bool hasY = HidP_GetUsageValue(HidP_Input, HID_USAGE_PAGE_GENERIC, link, HID_USAGE_GENERIC_Y, out y, pp, reportPtr, hidSize) == HIDP_STATUS_SUCCESS;
                    if (!hasX || !hasY) continue;

                    uint usageLen = 16;
                    bool tip = false;
                    bool conf = !cache.HasConfidence;
                    if (HidP_GetUsages(HidP_Input, HID_USAGE_PAGE_DIGITIZER, link, usageBuf, ref usageLen, pp, reportPtr, hidSize) == HIDP_STATUS_SUCCESS)
                    {
                        for (uint u = 0; u < usageLen; u++)
                        {
                            ushort ug = usageBuf[u];
                            if (ug == HID_USAGE_DIGITIZER_TIP_SWITCH) tip = true;
                            else if (ug == HID_USAGE_DIGITIZER_CONFIDENCE) conf = true;
                        }
                    }

                    if (!tip && x == 0 && y == 0 && (!hasCid || (cid == 0 && cache.ExpectedContacts == 0)))
                        continue;

                    slotsRead++;
                    if (cache.PendingCount < cache.Pending.Length)
                    {
                        cache.Pending[cache.PendingCount++] = new TouchContact
                        {
                            ContactId = hasCid ? (int)cid : link,
                            X = (int)x - cache.MinX,
                            Y = (int)y - cache.MinY,
                            TipSwitch = tip,
                            Confidence = conf
                        };
                    }

                    if (cache.ExpectedContacts > 0 && cache.PendingCount >= cache.ExpectedContacts)
                        break;
                }

                if (cache.ExpectedContacts == 0 || cache.PendingCount >= cache.ExpectedContacts || slotsRead == 0)
                {
                    ulong seenMask = 0;
                    int activeCount = 0;
                    int tipCount = 0;
                    for (int i = 0; i < cache.PendingCount; i++)
                    {
                        if (cache.Pending[i].TipSwitch) tipCount++;
                    }
                    int bottomStripY = (cache.MaxY > 0) ? (int)(cache.MaxY * 0.80) : int.MaxValue;
                    int rightStripX = (cache.MaxX > 0) ? (int)(cache.MaxX * 0.82) : int.MaxValue;

                    for (int i = 0; i < cache.PendingCount; i++)
                    {
                        TouchContact tc = cache.Pending[i];
                        int idBit = tc.ContactId & 63;
                        ulong bit = 1UL << idBit;

                        if (tc.TipSwitch)
                        {
                            seenMask |= bit;
                            // Allow 1-2 finger touches along bottom/right edge strips even if PTP firmware clears Confidence near bezel
                            bool inScrollEdge = (tipCount <= 2 && (tc.Y >= bottomStripY || tc.X >= rightStripX));
                            if (!tc.Confidence && !inScrollEdge)
                            {
                                cache.RejectedMask |= bit;
                                continue;
                            }
                            if ((cache.RejectedMask & bit) != 0 && !inScrollEdge)
                                continue;

                            if (activeCount < _activeBuf.Length)
                                _activeBuf[activeCount++] = tc;
                        }
                        else
                        {
                            cache.RejectedMask &= ~bit;
                        }
                    }

                    cache.RejectedMask &= seenMask;

                    var fh = FrameReceived;
                    if (fh != null)
                    {
                        _frame.Contacts = _activeBuf;
                        _frame.ContactCount = activeCount;
                        _frame.MaxX = cache.MaxX;
                        _frame.MaxY = cache.MaxY;
                        _frame.PhysMaxX = cache.PhysMaxX;
                        _frame.PhysMaxY = cache.PhysMaxY;
                        _frame.PhysicalButtonDown = cache.PendingButtonDown;
                        _frame.TimestampMs = nowMs;
                        fh(_frame);
                    }

                    cache.PendingCount = 0;
                    cache.ExpectedContacts = 0;
                    cache.PendingButtonDown = false;
                }
            }
        }

        public void Dispose()
        {
            if (_registered)
            {
                try
                {
                    RAWINPUTDEVICE* rid = stackalloc RAWINPUTDEVICE[2];
                    rid[0].usUsagePage = HID_USAGE_PAGE_DIGITIZER;
                    rid[0].usUsage = HID_USAGE_DIGITIZER_TOUCH_PAD;
                    rid[0].dwFlags = RIDEV_REMOVE;
                    rid[0].hwndTarget = IntPtr.Zero;
                    rid[1].usUsagePage = HID_USAGE_PAGE_GENERIC;
                    rid[1].usUsage = HID_USAGE_GENERIC_MOUSE;
                    rid[1].dwFlags = RIDEV_REMOVE;
                    rid[1].hwndTarget = IntPtr.Zero;
                    RegisterRawInputDevices(rid, 2, (uint)sizeof(RAWINPUTDEVICE));
                }
                catch { }
                _registered = false;
            }

            if (_rawBuf != null)
            {
                Marshal.FreeHGlobal((IntPtr)_rawBuf);
                _rawBuf = null;
                _rawBufSize = 0;
            }

            ResetDeviceCache();
        }
    }
}
