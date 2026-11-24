using System;
using System.Globalization;
using Microsoft.Win32;

namespace TouchpadGood
{
    public enum DragAction
    {
        None = 0,
        LeftDrag = 1,
        RightDrag = 2,
        MiddleDrag = 3
    }

    public enum ClickAction
    {
        None = 0,
        LeftClick = 1,
        RightClick = 2,
        MiddleClick = 3
    }

    public sealed class DriverConfig
    {
        public const string ConfigRegPath = @"Software\TouchpadGood";

        public bool Enabled = true;
        public bool DisableAccel = true;
        public bool HotkeySound = true;
        public string Language = DetectDefaultLanguage();

        // Gesture mappings (defaults match gaming touchpad ergonomics)
        public DragAction TwoFingerDrag = DragAction.LeftDrag;
        public ClickAction TwoFingerTap = ClickAction.RightClick;
        public ClickAction OnePlusOneTap = ClickAction.LeftClick;

        public DragAction ThreeFingerDrag = DragAction.RightDrag;
        public ClickAction ThreeFingerTap = ClickAction.MiddleClick;
        public ClickAction OnePlusTwoTap = ClickAction.RightClick;

        // Gaming ergonomics & edge scroll
        public bool EnableBottomEdgeHScroll = true; // Bottom 18% strip horizontal scroll (on-demand zero-permanent-hook)
        public bool EnableEdgePinchZoom = true;     // Right/Bottom edge scroll-zone 2-finger pinch-to-zoom
        public bool EnableFpsAdsLatch = true;       // 3F RightDrag->2F+3rd Left / 2F LeftDrag+3rd Right
        public bool LockPointerToAimFinger = true;  // 1+1/1+2 = original finger; Fresh 2F/3F = index finger (left-bottom)

        // Speed & Timing
        public bool AutoMatchDragSpeed = true;
        public int ManualDragSpeedPercent = 100;   // 40..300
        public int StaggerMs = 18;                 // 10..50
        public int MinClickHoldMs = 14;            // 8..40

        public bool NeedsTwoFingerNativeSuppress
        {
            get { return TwoFingerDrag != DragAction.None || TwoFingerTap != ClickAction.None || OnePlusOneTap != ClickAction.None; }
        }

        public bool NeedsThreeFingerNativeSuppress
        {
            get { return ThreeFingerDrag != DragAction.None || ThreeFingerTap != ClickAction.None || OnePlusTwoTap != ClickAction.None; }
        }

        public string SystemSyncSignature()
        {
            return string.Format("{0}|{1}|{2}|{3}",
                Enabled, DisableAccel, NeedsTwoFingerNativeSuppress, NeedsThreeFingerNativeSuppress);
        }

        public static string DetectDefaultLanguage()
        {
            try
            {
                string iso = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.ToLowerInvariant();
                if (iso == "ko" || iso == "en" || iso == "ja" || iso == "zh" || iso == "es" || iso == "de" || iso == "fr")
                    return iso;
            }
            catch { }
            return "en";
        }

        public static DriverConfig Load()
        {
            var cfg = new DriverConfig();
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(ConfigRegPath, false))
                {
                    if (key != null)
                    {
                        cfg.Enabled = ReadInt(key, "Enabled", 1) != 0;
                        cfg.DisableAccel = ReadInt(key, "DisableAccel", 1) != 0;
                        cfg.HotkeySound = ReadInt(key, "HotkeySound", 1) != 0;

                        object langObj = key.GetValue("Language", null);
                        if (langObj is string)
                        {
                            string l = ((string)langObj).Trim().ToLowerInvariant();
                            if (l == "ko" || l == "en" || l == "ja" || l == "zh" || l == "es" || l == "de" || l == "fr")
                                cfg.Language = l;
                        }

                        cfg.TwoFingerDrag = ClampDrag(ReadInt(key, "TwoFingerDrag", (int)DragAction.LeftDrag));
                        cfg.TwoFingerTap = ClampClick(ReadInt(key, "TwoFingerTap", (int)ClickAction.RightClick));
                        cfg.OnePlusOneTap = ClampClick(ReadInt(key, "OnePlusOneTap", (int)ClickAction.LeftClick));

                        cfg.ThreeFingerDrag = ClampDrag(ReadInt(key, "ThreeFingerDrag", (int)DragAction.RightDrag));
                        cfg.ThreeFingerTap = ClampClick(ReadInt(key, "ThreeFingerTap", (int)ClickAction.MiddleClick));
                        cfg.OnePlusTwoTap = ClampClick(ReadInt(key, "OnePlusTwoTap", (int)ClickAction.RightClick));

                        cfg.EnableBottomEdgeHScroll = ReadInt(key, "EnableBottomEdgeHScroll", 1) != 0;
                        cfg.EnableEdgePinchZoom = ReadInt(key, "EnableEdgePinchZoom", 1) != 0;
                        cfg.EnableFpsAdsLatch = ReadInt(key, "EnableFpsAdsLatch", 1) != 0;
                        cfg.LockPointerToAimFinger = ReadInt(key, "LockPointerToAimFinger", 1) != 0;

                        cfg.AutoMatchDragSpeed = ReadInt(key, "AutoMatchDragSpeed", 1) != 0;
                        cfg.ManualDragSpeedPercent = ClampInt(ReadInt(key, "ManualDragSpeedPercent", 100), 40, 300);
                        cfg.StaggerMs = ClampInt(ReadInt(key, "StaggerMs", 18), 10, 50);
                        cfg.MinClickHoldMs = ClampInt(ReadInt(key, "MinClickHoldMs", 14), 8, 40);
                    }
                }
            }
            catch { }
            return cfg;
        }

        public void Save()
        {
            try
            {
                using (var key = Registry.CurrentUser.CreateSubKey(ConfigRegPath))
                {
                    if (key == null) return;
                    key.SetValue("Enabled", Enabled ? 1 : 0, RegistryValueKind.DWord);
                    key.SetValue("DisableAccel", DisableAccel ? 1 : 0, RegistryValueKind.DWord);
                    key.SetValue("HotkeySound", HotkeySound ? 1 : 0, RegistryValueKind.DWord);
                    key.SetValue("Language", Language ?? "en", RegistryValueKind.String);

                    key.SetValue("TwoFingerDrag", (int)TwoFingerDrag, RegistryValueKind.DWord);
                    key.SetValue("TwoFingerTap", (int)TwoFingerTap, RegistryValueKind.DWord);
                    key.SetValue("OnePlusOneTap", (int)OnePlusOneTap, RegistryValueKind.DWord);

                    key.SetValue("ThreeFingerDrag", (int)ThreeFingerDrag, RegistryValueKind.DWord);
                    key.SetValue("ThreeFingerTap", (int)ThreeFingerTap, RegistryValueKind.DWord);
                    key.SetValue("OnePlusTwoTap", (int)OnePlusTwoTap, RegistryValueKind.DWord);

                    key.SetValue("EnableBottomEdgeHScroll", EnableBottomEdgeHScroll ? 1 : 0, RegistryValueKind.DWord);
                    key.SetValue("EnableEdgePinchZoom", EnableEdgePinchZoom ? 1 : 0, RegistryValueKind.DWord);
                    key.SetValue("EnableFpsAdsLatch", EnableFpsAdsLatch ? 1 : 0, RegistryValueKind.DWord);
                    key.SetValue("LockPointerToAimFinger", LockPointerToAimFinger ? 1 : 0, RegistryValueKind.DWord);

                    key.SetValue("AutoMatchDragSpeed", AutoMatchDragSpeed ? 1 : 0, RegistryValueKind.DWord);
                    key.SetValue("ManualDragSpeedPercent", ManualDragSpeedPercent, RegistryValueKind.DWord);
                    key.SetValue("StaggerMs", StaggerMs, RegistryValueKind.DWord);
                    key.SetValue("MinClickHoldMs", MinClickHoldMs, RegistryValueKind.DWord);
                }
            }
            catch { }
        }

        private static int ReadInt(RegistryKey key, string name, int fallback)
        {
            object v = key.GetValue(name, null);
            if (v == null) return fallback;
            try { return Convert.ToInt32(v); } catch { return fallback; }
        }

        private static int ClampInt(int v, int min, int max)
        {
            return v < min ? min : (v > max ? max : v);
        }

        private static DragAction ClampDrag(int v)
        {
            return (v >= 0 && v <= 3) ? (DragAction)v : DragAction.None;
        }

        private static ClickAction ClampClick(int v)
        {
            return (v >= 0 && v <= 3) ? (ClickAction)v : ClickAction.None;
        }
    }
}
