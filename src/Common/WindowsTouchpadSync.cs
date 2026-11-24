using System;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace TouchpadGood
{
    public static class WindowsTouchpadSync
    {
        private const string PtpRegPath = @"Software\Microsoft\Windows\CurrentVersion\PrecisionTouchPad";
        private const string BackupRegPath = @"Software\TouchpadGood\Backup";

        private const uint WM_SETTINGCHANGE = 0x001A;
        private const uint SMTO_ABORTIFHUNG = 0x0002;
        private static readonly IntPtr HWND_BROADCAST = new IntPtr(0xffff);

        private const uint SPI_GETMOUSE = 0x0003;
        private const uint SPI_SETMOUSE = 0x0004;
        private const uint SPI_GETMOUSESPEED = 0x0070;
        private const uint SPI_SETMOUSESPEED = 0x0071;
        private const uint SPI_GETTOUCHPADPARAMETERS = 0x00AE;
        private const uint SPI_SETTOUCHPADPARAMETERS = 0x00AF;

        private const uint SPIF_UPDATEINIFILE = 0x01;
        private const uint SPIF_SENDCHANGE = 0x02;

        // TOUCHPAD_PARAMETERS_V1 flags2 bits (winuser.h)
        private const uint FLAG2_ALLOW_WITH_MOUSE       = 0x0001;
        private const uint FLAG2_TAP_ENABLED            = 0x0004;
        private const uint FLAG2_TAP_AND_DRAG_ENABLED   = 0x0008;
        private const uint FLAG2_TWO_FINGER_TAP_ENABLED = 0x0010;
        private const uint FLAG2_RIGHT_CLICK_ZONE       = 0x0020;
        private const uint FLAG2_HONOR_MOUSE_ACCEL      = 0x0040;
        private const uint FLAG2_PAN_ENABLED            = 0x0080;
        private const uint FLAG2_ZOOM_ENABLED           = 0x0100;

        [StructLayout(LayoutKind.Sequential)]
        private struct TOUCHPAD_PARAMETERS_V1
        {
            public uint versionNumber;
            public uint maxSupportedContacts;
            public uint legacyTouchpadFeatures;
            public uint flags1;
            public uint flags2;
            public uint sensitivityLevel;
            public uint cursorSpeed;
            public uint feedbackIntensity;
            public uint clickForceSensitivity;
            public uint rightClickZoneWidth;
            public uint rightClickZoneHeight;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct TOUCHPAD_PARAMETERS_V3
        {
            public uint versionNumber;
            public uint maxSupportedContacts;
            public uint legacyTouchpadFeatures;
            public uint flags1;
            public uint flags2;
            public uint sensitivityLevel;
            public uint cursorSpeed;
            public uint feedbackIntensity;
            public uint clickForceSensitivity;
            public uint rightClickZoneWidth;
            public uint rightClickZoneHeight;
            public uint flags3;
            public uint flags4;
            public uint panSensitivity;
            public uint zoomSensitivity;
            public uint singleFingerPanMode;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SystemParametersInfo(uint uiAction, uint uiParam, int[] pvParam, uint fWinIni);

        [DllImport("user32.dll", SetLastError = true, EntryPoint = "SystemParametersInfo")]
        private static extern bool SystemParametersInfoInt(uint uiAction, uint uiParam, IntPtr pvParam, uint fWinIni);

        [DllImport("user32.dll", SetLastError = true, EntryPoint = "SystemParametersInfo")]
        private static extern bool SystemParametersInfoOutInt(uint uiAction, uint uiParam, out int pvParam, uint fWinIni);

        [DllImport("user32.dll", SetLastError = true, EntryPoint = "SystemParametersInfo")]
        private static extern bool SystemParametersInfoTouchpad(uint uiAction, uint uiParam, ref TOUCHPAD_PARAMETERS_V1 pvParam, uint fWinIni);

        [DllImport("user32.dll", SetLastError = true, EntryPoint = "SystemParametersInfo")]
        private static extern bool SystemParametersInfoTouchpadV3(uint uiAction, uint uiParam, ref TOUCHPAD_PARAMETERS_V3 pvParam, uint fWinIni);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessageTimeout(
            IntPtr hWnd, uint Msg, UIntPtr wParam, string lParam,
            uint fuFlags, uint uTimeout, out UIntPtr lpdwResult);

        private static readonly string[] ManagedKeys = new[]
        {
            "AAPThreshold",
            "HonorMouseAccelSetting",
            "EnableEdgy",
            "EdgeAutoPanningEnabled",
            "BoostedPanningEnabled",
            "SingleFingerPanMode",
            "PanEnabled",
            "ZoomEnabled",
            "TapsEnabled",
            "TwoFingerTapEnabled",
            "TapAndDrag",
            "RightClickZoneEnabled",
            "ThreeFingerSlideEnabled",
            "ThreeFingerTapEnabled",
            "FourFingerSlideEnabled",
            "FourFingerTapEnabled"
        };

        private static uint SetBit(uint flags, uint bit, bool enable)
        {
            return enable ? (flags | bit) : (flags & ~bit);
        }

        private static int ReadRegInt(RegistryKey key, string name, int fallback)
        {
            if (key == null) return fallback;
            object v = key.GetValue(name, null);
            if (v == null) return fallback;
            try { return Convert.ToInt32(v); } catch { return fallback; }
        }

        public static void BackupIfNeeded()
        {
            try
            {
                using (var existing = Registry.CurrentUser.OpenSubKey(BackupRegPath, false))
                {
                    if (existing != null && ReadRegInt(existing, "_Valid", 0) == 1)
                        return;
                }

                using (var bak = Registry.CurrentUser.CreateSubKey(BackupRegPath))
                {
                    if (bak == null) return;

                    using (var src = Registry.CurrentUser.OpenSubKey(PtpRegPath, false))
                    {
                        bool allZero = src == null || (
                            ReadRegInt(src, "PanEnabled", 0) == 0 &&
                            ReadRegInt(src, "TwoFingerTapEnabled", 0) == 0 &&
                            ReadRegInt(src, "ThreeFingerSlideEnabled", 0) == 0);

                        if (allZero)
                        {
                            bak.SetValue("AAPThreshold", 2, RegistryValueKind.DWord);
                            bak.SetValue("HonorMouseAccelSetting", 0, RegistryValueKind.DWord);
                            bak.SetValue("EnableEdgy", 1, RegistryValueKind.DWord);
                            bak.SetValue("EdgeAutoPanningEnabled", 1, RegistryValueKind.DWord);
                            bak.SetValue("BoostedPanningEnabled", 1, RegistryValueKind.DWord);
                            bak.SetValue("SingleFingerPanMode", 0, RegistryValueKind.DWord);
                            bak.SetValue("PanEnabled", 1, RegistryValueKind.DWord);
                            bak.SetValue("ZoomEnabled", 1, RegistryValueKind.DWord);
                            bak.SetValue("TapsEnabled", 1, RegistryValueKind.DWord);
                            bak.SetValue("TwoFingerTapEnabled", 1, RegistryValueKind.DWord);
                            bak.SetValue("TapAndDrag", 1, RegistryValueKind.DWord);
                            bak.SetValue("RightClickZoneEnabled", 1, RegistryValueKind.DWord);
                            bak.SetValue("ThreeFingerSlideEnabled", 1, RegistryValueKind.DWord);
                            bak.SetValue("ThreeFingerTapEnabled", 1, RegistryValueKind.DWord);
                            bak.SetValue("FourFingerSlideEnabled", 1, RegistryValueKind.DWord);
                            bak.SetValue("FourFingerTapEnabled", 1, RegistryValueKind.DWord);
                            bak.SetValue("_MouseThreshold1", 6, RegistryValueKind.DWord);
                            bak.SetValue("_MouseThreshold2", 10, RegistryValueKind.DWord);
                            bak.SetValue("_MouseSpeed", 1, RegistryValueKind.DWord);
                            bak.SetValue("_MouseSensitivity", 10, RegistryValueKind.DWord);
                        }
                        else
                        {
                            for (int i = 0; i < ManagedKeys.Length; i++)
                            {
                                string name = ManagedKeys[i];
                                int defVal = (name == "SingleFingerPanMode") ? 0 : 1;
                                bak.SetValue(name, ReadRegInt(src, name, defVal), RegistryValueKind.DWord);
                            }

                            int[] mp = new int[3];
                            if (SystemParametersInfo(SPI_GETMOUSE, 0, mp, 0))
                            {
                                bak.SetValue("_MouseThreshold1", mp[0], RegistryValueKind.DWord);
                                bak.SetValue("_MouseThreshold2", mp[1], RegistryValueKind.DWord);
                                bak.SetValue("_MouseSpeed", mp[2], RegistryValueKind.DWord);
                            }
                            int ms;
                            if (SystemParametersInfoOutInt(SPI_GETMOUSESPEED, 0, out ms, 0))
                            {
                                bak.SetValue("_MouseSensitivity", ms, RegistryValueKind.DWord);
                            }
                        }
                    }

                    bak.SetValue("_Valid", 1, RegistryValueKind.DWord);
                }
            }
            catch { }
        }

        public static void Apply(DriverConfig cfg)
        {
            ApplyInternal(cfg, false);
        }

        /// <summary>
        /// One-click optimal Windows 11 Precision Touchpad + Mouse configuration for TouchpadGood:
        /// - Zero keyboard typing delay (AAPThreshold=0 / Most Sensitive)
        /// - Acceleration OFF (SPI_SETMOUSE=0,0,0 + speed 10 + HonorMouseAccelSetting=1)
        /// - 1-finger tap ON, TapAndDrag OFF, RightClickZone OFF
        /// - Windows 11 native right-edge scroll ON (SingleFingerPanMode=2, EdgeAutoPanning=1)
        /// - Leave touchpad active when external mouse connected
        /// - Clean 2/3/4-finger non-conflict sync
        /// </summary>
        public static void ApplyRecommendedWindowsSetup(DriverConfig cfg)
        {
            ApplyInternal(cfg, true);
        }

        private static void ApplyInternal(DriverConfig cfg, bool forceFullRecommended)
        {
            if (cfg == null || (!cfg.Enabled && !forceFullRecommended))
            {
                RestoreOriginal();
                return;
            }

            BackupIfNeeded();

            try
            {
                bool native2F = !cfg.NeedsTwoFingerNativeSuppress;
                bool native3F = !cfg.NeedsThreeFingerNativeSuppress;

                if (cfg.DisableAccel || forceFullRecommended)
                {
                    int[] mp = new int[] { 0, 0, 0 };
                    SystemParametersInfo(SPI_SETMOUSE, 0, mp, SPIF_UPDATEINIFILE | SPIF_SENDCHANGE);
                    SystemParametersInfoInt(SPI_SETMOUSESPEED, 0, new IntPtr(10), SPIF_UPDATEINIFILE | SPIF_SENDCHANGE);
                }
                else
                {
                    RestoreMouseFromBackup();
                }

                using (var key = Registry.CurrentUser.CreateSubKey(PtpRegPath))
                {
                    if (key != null)
                    {
                        key.SetValue("AAPThreshold", 0, RegistryValueKind.DWord);
                        key.SetValue("HonorMouseAccelSetting", (cfg.DisableAccel || forceFullRecommended) ? 1 : 0, RegistryValueKind.DWord);
                        key.SetValue("EnableEdgy", 0, RegistryValueKind.DWord);
                        key.SetValue("EdgeAutoPanningEnabled", 0, RegistryValueKind.DWord);
                        key.SetValue("BoostedPanningEnabled", 0, RegistryValueKind.DWord);
                        // Disable Windows fractional SingleFingerPanMode BEFORE SPI_SETTOUCHPADPARAMETERS so OS unloads it live
                        key.SetValue("SingleFingerPanMode", 0, RegistryValueKind.DWord);

                        key.SetValue("TapsEnabled", 1, RegistryValueKind.DWord);
                        key.SetValue("TapAndDrag", 0, RegistryValueKind.DWord);
                        key.SetValue("PanEnabled", native2F ? 1 : 0, RegistryValueKind.DWord);
                        key.SetValue("ZoomEnabled", native2F ? 1 : 0, RegistryValueKind.DWord);
                        key.SetValue("TwoFingerTapEnabled", native2F ? 1 : 0, RegistryValueKind.DWord);
                        key.SetValue("RightClickZoneEnabled", 0, RegistryValueKind.DWord);
                        key.SetValue("ThreeFingerSlideEnabled", native3F ? 1 : 0, RegistryValueKind.DWord);
                        key.SetValue("ThreeFingerTapEnabled", native3F ? 1 : 0, RegistryValueKind.DWord);
                        key.SetValue("FourFingerSlideEnabled", 1, RegistryValueKind.DWord);
                        key.SetValue("FourFingerTapEnabled", 1, RegistryValueKind.DWord);
                    }
                }

                try
                {
                    bool setViaV3 = false;
                    var tp3 = new TOUCHPAD_PARAMETERS_V3 { versionNumber = 3 };
                    uint sz3 = (uint)Marshal.SizeOf(typeof(TOUCHPAD_PARAMETERS_V3));
                    if (SystemParametersInfoTouchpadV3(SPI_GETTOUCHPADPARAMETERS, sz3, ref tp3, 0))
                    {
                        tp3.sensitivityLevel = 0;
                        uint f2 = tp3.flags2;
                        if (forceFullRecommended)
                            f2 = SetBit(f2, FLAG2_ALLOW_WITH_MOUSE, true);
                        f2 = SetBit(f2, FLAG2_TAP_ENABLED, true);
                        f2 = SetBit(f2, FLAG2_TAP_AND_DRAG_ENABLED, false);
                        f2 = SetBit(f2, FLAG2_TWO_FINGER_TAP_ENABLED, native2F);
                        f2 = SetBit(f2, FLAG2_RIGHT_CLICK_ZONE, false);
                        f2 = SetBit(f2, FLAG2_PAN_ENABLED, native2F);
                        f2 = SetBit(f2, FLAG2_ZOOM_ENABLED, native2F);
                        f2 = SetBit(f2, FLAG2_HONOR_MOUSE_ACCEL, cfg.DisableAccel || forceFullRecommended);
                        tp3.flags2 = f2;
                        tp3.singleFingerPanMode = 0; // Disable Windows 11 26H2 OS single-finger edge pan LIVE so TouchpadGood real-mouse scroll never fights OS!

                        setViaV3 = SystemParametersInfoTouchpadV3(SPI_SETTOUCHPADPARAMETERS, sz3, ref tp3, SPIF_UPDATEINIFILE | SPIF_SENDCHANGE);
                    }

                    if (!setViaV3)
                    {
                        var tp = new TOUCHPAD_PARAMETERS_V1 { versionNumber = 1 };
                        uint sz = (uint)Marshal.SizeOf(typeof(TOUCHPAD_PARAMETERS_V1));
                        if (SystemParametersInfoTouchpad(SPI_GETTOUCHPADPARAMETERS, sz, ref tp, 0))
                        {
                            tp.sensitivityLevel = 0;
                            uint f2 = tp.flags2;
                            if (forceFullRecommended)
                                f2 = SetBit(f2, FLAG2_ALLOW_WITH_MOUSE, true);
                            f2 = SetBit(f2, FLAG2_TAP_ENABLED, true);
                            f2 = SetBit(f2, FLAG2_TAP_AND_DRAG_ENABLED, false);
                            f2 = SetBit(f2, FLAG2_TWO_FINGER_TAP_ENABLED, native2F);
                            f2 = SetBit(f2, FLAG2_RIGHT_CLICK_ZONE, false);
                            f2 = SetBit(f2, FLAG2_PAN_ENABLED, native2F);
                            f2 = SetBit(f2, FLAG2_ZOOM_ENABLED, native2F);
                            f2 = SetBit(f2, FLAG2_HONOR_MOUSE_ACCEL, cfg.DisableAccel || forceFullRecommended);
                            tp.flags2 = f2;

                            SystemParametersInfoTouchpad(SPI_SETTOUCHPADPARAMETERS, sz, ref tp, SPIF_UPDATEINIFILE | SPIF_SENDCHANGE);
                        }
                    }
                }
                catch { }

                NotifySettingsChanged();
            }
            catch { }
        }

        public static void RestoreOriginal()
        {
            try
            {
                RestoreMouseFromBackup();

                using (var bak = Registry.CurrentUser.OpenSubKey(BackupRegPath, false))
                {
                    int aap = ReadRegInt(bak, "AAPThreshold", 2);
                    bool tap = ReadRegInt(bak, "TapsEnabled", 1) != 0;
                    bool tapDrag = ReadRegInt(bak, "TapAndDrag", 1) != 0;
                    bool twoTap = ReadRegInt(bak, "TwoFingerTapEnabled", 1) != 0;
                    bool rcZone = ReadRegInt(bak, "RightClickZoneEnabled", 1) != 0;
                    bool honor = ReadRegInt(bak, "HonorMouseAccelSetting", 0) != 0;
                    bool pan = ReadRegInt(bak, "PanEnabled", 1) != 0;
                    bool zoom = ReadRegInt(bak, "ZoomEnabled", 1) != 0;

                    int singlePan = ReadRegInt(bak, "SingleFingerPanMode", 0);

                    try
                    {
                        bool restoredV3 = false;
                        var tp3 = new TOUCHPAD_PARAMETERS_V3 { versionNumber = 3 };
                        uint sz3 = (uint)Marshal.SizeOf(typeof(TOUCHPAD_PARAMETERS_V3));
                        if (SystemParametersInfoTouchpadV3(SPI_GETTOUCHPADPARAMETERS, sz3, ref tp3, 0))
                        {
                            tp3.sensitivityLevel = (uint)Math.Max(0, Math.Min(3, aap));
                            uint f2 = tp3.flags2;
                            f2 = SetBit(f2, FLAG2_TAP_ENABLED, tap);
                            f2 = SetBit(f2, FLAG2_TAP_AND_DRAG_ENABLED, tapDrag);
                            f2 = SetBit(f2, FLAG2_TWO_FINGER_TAP_ENABLED, twoTap);
                            f2 = SetBit(f2, FLAG2_RIGHT_CLICK_ZONE, rcZone);
                            f2 = SetBit(f2, FLAG2_HONOR_MOUSE_ACCEL, honor);
                            f2 = SetBit(f2, FLAG2_PAN_ENABLED, pan);
                            f2 = SetBit(f2, FLAG2_ZOOM_ENABLED, zoom);
                            tp3.flags2 = f2;
                            tp3.singleFingerPanMode = (uint)Math.Max(0, singlePan);
                            restoredV3 = SystemParametersInfoTouchpadV3(SPI_SETTOUCHPADPARAMETERS, sz3, ref tp3, SPIF_UPDATEINIFILE | SPIF_SENDCHANGE);
                        }

                        if (!restoredV3)
                        {
                            var tp = new TOUCHPAD_PARAMETERS_V1 { versionNumber = 1 };
                            uint sz = (uint)Marshal.SizeOf(typeof(TOUCHPAD_PARAMETERS_V1));
                            if (SystemParametersInfoTouchpad(SPI_GETTOUCHPADPARAMETERS, sz, ref tp, 0))
                            {
                                tp.sensitivityLevel = (uint)Math.Max(0, Math.Min(3, aap));
                                uint f2 = tp.flags2;
                                f2 = SetBit(f2, FLAG2_TAP_ENABLED, tap);
                                f2 = SetBit(f2, FLAG2_TAP_AND_DRAG_ENABLED, tapDrag);
                                f2 = SetBit(f2, FLAG2_TWO_FINGER_TAP_ENABLED, twoTap);
                                f2 = SetBit(f2, FLAG2_RIGHT_CLICK_ZONE, rcZone);
                                f2 = SetBit(f2, FLAG2_HONOR_MOUSE_ACCEL, honor);
                                f2 = SetBit(f2, FLAG2_PAN_ENABLED, pan);
                                f2 = SetBit(f2, FLAG2_ZOOM_ENABLED, zoom);
                                tp.flags2 = f2;
                                SystemParametersInfoTouchpad(SPI_SETTOUCHPADPARAMETERS, sz, ref tp, SPIF_UPDATEINIFILE | SPIF_SENDCHANGE);
                            }
                        }
                    }
                    catch { }

                    using (var dst = Registry.CurrentUser.CreateSubKey(PtpRegPath))
                    {
                        if (dst != null)
                        {
                            for (int i = 0; i < ManagedKeys.Length; i++)
                            {
                                string name = ManagedKeys[i];
                                int defVal = (name == "AAPThreshold") ? 2
                                    : (name == "HonorMouseAccelSetting" || name == "SingleFingerPanMode" ? 0 : 1);
                                dst.SetValue(name, ReadRegInt(bak, name, defVal), RegistryValueKind.DWord);
                            }
                        }
                    }
                }

                NotifySettingsChanged();
            }
            catch { }
        }

        private static void RestoreMouseFromBackup()
        {
            try
            {
                using (var bak = Registry.CurrentUser.OpenSubKey(BackupRegPath, false))
                {
                    int t1 = ReadRegInt(bak, "_MouseThreshold1", 6);
                    int t2 = ReadRegInt(bak, "_MouseThreshold2", 10);
                    int sp = ReadRegInt(bak, "_MouseSpeed", 1);
                    int sens = ReadRegInt(bak, "_MouseSensitivity", 10);
                    if (sens < 1 || sens > 20) sens = 10;

                    int[] mp = new int[] { t1, t2, sp };
                    SystemParametersInfo(SPI_SETMOUSE, 0, mp, SPIF_UPDATEINIFILE | SPIF_SENDCHANGE);
                    SystemParametersInfoInt(SPI_SETMOUSESPEED, 0, new IntPtr(sens), SPIF_UPDATEINIFILE | SPIF_SENDCHANGE);
                }
            }
            catch { }
        }

        private static void NotifySettingsChanged()
        {
            UIntPtr res;
            SendMessageTimeout(HWND_BROADCAST, WM_SETTINGCHANGE, UIntPtr.Zero, "PrecisionTouchPad", SMTO_ABORTIFHUNG, 500, out res);
        }
    }
}
