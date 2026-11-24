using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace TouchpadGood
{
    public sealed class SettingsForm : Form
    {
        private const string DriverWindowClass = "TouchpadGood_HostClass";
        private const string TaskName = "TouchpadGood";
        private const string RunRegPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const uint MSGFLT_ALLOW = 1;

        private static readonly uint WM_DRIVER_RELOAD = RegisterWindowMessage("WM_TOUCHPADGOOD_RELOAD");
        private static readonly uint WM_DRIVER_STOP = RegisterWindowMessage("WM_TOUCHPADGOOD_STOP");
        private static readonly uint WM_DRIVER_PING = RegisterWindowMessage("WM_TOUCHPADGOOD_PING");
        private static readonly uint WM_DRIVER_CONFIG_CHANGED = RegisterWindowMessage("WM_TOUCHPADGOOD_CONFIG_CHANGED");

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern uint RegisterWindowMessage(string message);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hwnd, uint msg, IntPtr wparam, IntPtr lparam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SendMessageTimeout(
            IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam,
            uint fuFlags, uint uTimeout, out IntPtr lpdwResult);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool ChangeWindowMessageFilterEx(IntPtr hWnd, uint message, uint action, IntPtr pChangeFilterStruct);

        private readonly FluentTheme _t;
        private DriverConfig _config;
        private float _scale = 1.0f;
        private bool _isLoadingUi;
        private System.Windows.Forms.Timer _statusTimer;
        private System.Windows.Forms.Timer _saveDebounce;

        // Top header & language
        private Label _lblPageTitle;
        private FluentDropdown _cbLanguage;

        // Hero status card
        private FluentStatusDot _statusDot;
        private Label _lblStatusText;
        private Label _lblHeroSub;
        private FluentButton _btnAdmin;
        private FluentButton _btnToggleDriver;

        // Section titles
        private Label _secCore, _secGestures, _secFps, _secTune, _secAbout;

        // Core cards
        private Label _lblEnabledTitle, _lblEnabledSub;
        private Label _lblAccelTitle, _lblAccelSub;
        private Label _lblWinOptTitle, _lblWinOptSub;
        private Label _lblAutoStartTitle, _lblAutoStartSub;
        private Label _lblSoundTitle, _lblSoundSub;
        private FluentToggle _tgEnabled;
        private FluentToggle _tgDisableAccel;
        private FluentButton _btnWinOpt;
        private FluentToggle _tgAutoStart;
        private FluentToggle _tgHotkeySound;

        // Gesture cards
        private Label _lblCard2F, _lblCard3F;
        private Label _lbl2Drag, _lbl2Tap, _lbl11Tap;
        private Label _lbl3Drag, _lbl3Tap, _lbl12Tap;
        private FluentDropdown _cb2Drag, _cb2Tap, _cb11Tap;
        private FluentDropdown _cb3Drag, _cb3Tap, _cb12Tap;

        // FPS cards
        private Label _lblFpsTitle, _lblFpsSub;
        private Label _lblAimLockTitle, _lblAimLockSub;
        private FluentToggle _tgAdsLatch;
        private FluentToggle _tgAimLock;

        // Sensitivity & latency cards
        private Label _lblAutoSpeedTitle, _lblAutoSpeedSub, _lblManualLabel;
        private Label _lblStaggerTitle, _lblStaggerSub;
        private Label _lblHoldTitle, _lblHoldSub;
        private FluentToggle _tgAutoSpeed;
        private FluentSlider _slManualSpeed, _slStagger, _slClickHold;
        private Label _lblManualSpeedVal, _lblStaggerVal, _lblClickHoldVal;

        // Update card
        private Label _lblUpdateTitle, _lblUpdateSub;
        private FluentButton _btnUpdate;
        private bool _updateBusy;
        private UpdateCheckResult _pendingUpdate;
        private string _updateStateKey = "sub_update_idle";
        private string _updateStateArg = AppMeta.Version;

        // Footer
        private Label _lblFooterInfo;
        private FluentButton _btnRestore, _btnReset;
        private string _footerKey = "footer_default";

        public SettingsForm()
        {
            _t = FluentTheme.DetectSystemTheme();
            _config = DriverConfig.Load();
            L10n.CurrentLang = _config.Language;

            using (var g = Graphics.FromHwnd(IntPtr.Zero))
                _scale = g.DpiX / 96.0f;

            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            DoubleBuffered = true;
            InitUi();
            ApplyLanguage();
            LoadConfigToUi();
            EnsureDriverRunningIfEnabled();
            RefreshDriverStatus();

            _saveDebounce = new System.Windows.Forms.Timer { Interval = 70 };
            _saveDebounce.Tick += (s, e) => { _saveDebounce.Stop(); CommitSave(); };

            _statusTimer = new System.Windows.Forms.Timer { Interval = 1000 };
            _statusTimer.Tick += (s, e) => RefreshDriverStatus();
            _statusTimer.Start();
        }

        private int S(int px) { return (int)Math.Round(px * _scale); }

        private static string DriverExePath
        {
            get
            {
                string local = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TouchpadGood.exe");
                if (File.Exists(local)) return local;
                string installed = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Programs", AppMeta.AppName, "TouchpadGood.exe");
                if (File.Exists(installed)) return installed;
                return local;
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            FluentTheme.ApplyWindowFrame(this.Handle, _t.IsDark, true);
            try
            {
                ChangeWindowMessageFilterEx(this.Handle, (uint)SettingsProgram.WM_SHOW_SETTINGS, MSGFLT_ALLOW, IntPtr.Zero);
                ChangeWindowMessageFilterEx(this.Handle, WM_DRIVER_CONFIG_CHANGED, MSGFLT_ALLOW, IntPtr.Zero);
            }
            catch { }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            BringToForeground();
        }

        private void BringToForeground()
        {
            if (this.WindowState == FormWindowState.Minimized)
                this.WindowState = FormWindowState.Normal;
            this.Show();
            this.BringToFront();
            this.Activate();
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == SettingsProgram.WM_SHOW_SETTINGS)
            {
                BringToForeground();
                return;
            }
            if ((uint)m.Msg == WM_DRIVER_CONFIG_CHANGED)
            {
                _config = DriverConfig.Load();
                LoadConfigToUi();
                SetFooter(_config.Enabled ? "footer_hk_on" : "footer_hk_off");
                return;
            }
            base.WndProc(ref m);
        }

        private Font UiFont(float pt, FontStyle style)
        {
            return FluentTheme.CreateUiFont(pt, style);
        }

        private void SetFooter(string key)
        {
            _footerKey = key;
            if (_lblFooterInfo != null) _lblFooterInfo.Text = L10n.T(key);
        }

        private void InitUi()
        {
            AutoScaleMode = AutoScaleMode.None;
            ShowInTaskbar = true;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = _t.Canvas;
            ForeColor = _t.TextPrimary;
            Font = UiFont(9.0f, FontStyle.Regular);

            int formW = 620;
            int desiredH = 716;
            Rectangle work = Screen.FromPoint(Cursor.Position).WorkingArea;
            int actualH = Math.Min(S(desiredH), Math.Max(S(460), work.Height - S(52)));
            ClientSize = new Size(S(formW), actualH);

            // Fixed Bottom Footer Bar
            int footerH = S(54);
            var footer = new Panel
            {
                Location = new Point(0, actualH - footerH),
                Size = new Size(S(formW), footerH),
                BackColor = _t.Card
            };
            footer.Paint += (s, e) =>
            {
                using (var pen = new Pen(_t.Divider, 1f))
                    e.Graphics.DrawLine(pen, 0, 0, footer.Width, 0);
            };

            _lblFooterInfo = new Label
            {
                Location = new Point(S(20), S(16)),
                Size = new Size(S(352), S(22)),
                Font = UiFont(8.2f, FontStyle.Regular),
                ForeColor = _t.TextSecondary,
                BackColor = _t.Card,
                AutoEllipsis = true
            };
            _btnRestore = new FluentButton(_t, _scale, false)
            {
                Location = new Point(S(formW - 236), S(12)),
                Size = new Size(S(108), S(30)),
                Font = UiFont(8.5f, FontStyle.Regular),
                BackColor = _t.Card
            };
            _btnRestore.Click += (s, e) => RestoreWindowsDefaults();

            _btnReset = new FluentButton(_t, _scale, true)
            {
                Location = new Point(S(formW - 120), S(12)),
                Size = new Size(S(100), S(30)),
                Font = UiFont(8.5f, FontStyle.Bold),
                BackColor = _t.Card
            };
            _btnReset.Click += (s, e) => ApplyDefaultPreset();

            footer.Controls.Add(_lblFooterInfo);
            footer.Controls.Add(_btnRestore);
            footer.Controls.Add(_btnReset);
            Controls.Add(footer);

            // Custom Fluent Scrollable Body
            var scrollBody = new FluentScrollPanel(_t, _scale)
            {
                Location = new Point(0, 0),
                Size = new Size(S(formW), actualH - footerH)
            };
            Controls.Add(scrollBody);

            Panel body = scrollBody.Content;

            int pad = 20;
            int cardW = formW - pad * 2 - 14;
            int y = 16;

            // Top Header: Page Title (left) + Language Selector (right)
            _lblPageTitle = new Label
            {
                Location = new Point(S(pad), S(y)),
                Size = new Size(S(cardW - 146), S(32)),
                Font = UiFont(15.5f, FontStyle.Bold),
                ForeColor = _t.TextPrimary,
                BackColor = _t.Canvas,
                AutoEllipsis = true
            };
            body.Controls.Add(_lblPageTitle);

            _cbLanguage = new FluentDropdown(_t, _scale)
            {
                Location = new Point(S(pad + cardW - 134), S(y + 3)),
                Size = new Size(S(134), S(28)),
                Font = UiFont(8.5f, FontStyle.Regular),
                BackColor = _t.Canvas
            };
            _cbLanguage.SetItems(L10n.LangNames, L10n.LangIndex(_config.Language));
            _cbLanguage.SelectedIndexChanged += (s, e) =>
            {
                if (_isLoadingUi) return;
                int idx = _cbLanguage.SelectedIndex;
                if (idx >= 0 && idx < L10n.LangCodes.Length)
                {
                    _config.Language = L10n.LangCodes[idx];
                    L10n.CurrentLang = _config.Language;
                    _config.Save();
                    ApplyLanguage();
                }
            };
            body.Controls.Add(_cbLanguage);
            y += 40;

            // Hero Status Card (pixel-aligned vector status dot + full non-clipped subtitle)
            var hero = new FluentCard(_t, 8f * _scale)
            {
                Location = new Point(S(pad), S(y)),
                Size = new Size(S(cardW), S(68))
            };
            _statusDot = new FluentStatusDot(_t.Card, _t.Success, _scale)
            {
                Location = new Point(S(14), S(13)),
                Size = new Size(S(16), S(22))
            };
            _lblStatusText = new Label
            {
                Location = new Point(S(34), S(13)),
                Size = new Size(S(cardW - 234), S(22)),
                Font = UiFont(9.8f, FontStyle.Bold),
                ForeColor = _t.TextPrimary,
                BackColor = _t.Card,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true
            };
            _lblHeroSub = new Label
            {
                Location = new Point(S(34), S(38)),
                Size = new Size(S(cardW - 234), S(18)),
                Font = UiFont(8.0f, FontStyle.Regular),
                ForeColor = _t.TextSecondary,
                BackColor = _t.Card,
                AutoEllipsis = true
            };
            _btnAdmin = new FluentButton(_t, _scale, false)
            {
                Location = new Point(S(cardW - 194), S(19)),
                Size = new Size(S(100), S(30)),
                Font = UiFont(8.3f, FontStyle.Regular)
            };
            _btnAdmin.Click += (s, e) => RestartDriverAsAdmin();

            _btnToggleDriver = new FluentButton(_t, _scale, false)
            {
                Location = new Point(S(cardW - 86), S(19)),
                Size = new Size(S(70), S(30)),
                Font = UiFont(8.3f, FontStyle.Regular)
            };
            _btnToggleDriver.Click += (s, e) => ToggleDriverProcess();

            hero.Controls.Add(_statusDot);
            hero.Controls.Add(_lblStatusText);
            hero.Controls.Add(_lblHeroSub);
            hero.Controls.Add(_btnAdmin);
            hero.Controls.Add(_btnToggleDriver);
            body.Controls.Add(hero);
            y += 82;

            // Section 1: Core Controls
            y = AddSectionTitle(body, pad, y, cardW, out _secCore);
            y = AddToggleCard(body, pad, y, cardW, out _lblEnabledTitle, out _lblEnabledSub, out _tgEnabled, (s, e) => ScheduleSave(true));
            y = AddToggleCard(body, pad, y, cardW, out _lblAccelTitle, out _lblAccelSub, out _tgDisableAccel, (s, e) => ScheduleSave(true));
            y = AddActionCard(body, pad, y, cardW, out _lblWinOptTitle, out _lblWinOptSub, out _btnWinOpt, true, (s, e) => ApplyWindowsAutoOptimize());
            y = AddToggleCard(body, pad, y, cardW, out _lblAutoStartTitle, out _lblAutoStartSub, out _tgAutoStart, (s, e) =>
            {
                if (!_isLoadingUi) ToggleAutoStartTask();
            });
            y = AddToggleCard(body, pad, y, cardW, out _lblSoundTitle, out _lblSoundSub, out _tgHotkeySound, (s, e) => ScheduleSave(true));
            y += 12;

            // Section 2: Multi-Finger Gestures
            y = AddSectionTitle(body, pad, y, cardW, out _secGestures);
            y = AddDropdownCard(body, pad, y, cardW, out _lblCard2F,
                out _lbl2Drag, out _cb2Drag,
                out _lbl2Tap, out _cb2Tap,
                out _lbl11Tap, out _cb11Tap);
            y = AddDropdownCard(body, pad, y, cardW, out _lblCard3F,
                out _lbl3Drag, out _cb3Drag,
                out _lbl3Tap, out _cb3Tap,
                out _lbl12Tap, out _cb12Tap);
            y += 12;

            // Section 3: FPS Gaming & Pointer Control
            y = AddSectionTitle(body, pad, y, cardW, out _secFps);
            y = AddToggleCard(body, pad, y, cardW, out _lblFpsTitle, out _lblFpsSub, out _tgAdsLatch, (s, e) => ScheduleSave(true));
            y = AddToggleCard(body, pad, y, cardW, out _lblAimLockTitle, out _lblAimLockSub, out _tgAimLock, (s, e) => ScheduleSave(true));
            y += 12;

            // Section 4: Sensitivity & Ultra-Low-Latency Timing
            y = AddSectionTitle(body, pad, y, cardW, out _secTune);

            var speedCard = new FluentCard(_t, 7f * _scale)
            {
                Location = new Point(S(pad), S(y)),
                Size = new Size(S(cardW), S(98))
            };
            _lblAutoSpeedTitle = new Label
            {
                Location = new Point(S(16), S(12)),
                Size = new Size(S(cardW - 120), S(20)),
                Font = UiFont(9.3f, FontStyle.Regular),
                ForeColor = _t.TextPrimary,
                BackColor = _t.Card,
                AutoEllipsis = true
            };
            _lblAutoSpeedSub = new Label
            {
                Location = new Point(S(16), S(32)),
                Size = new Size(S(cardW - 120), S(16)),
                Font = UiFont(8.0f, FontStyle.Regular),
                ForeColor = _t.TextSecondary,
                BackColor = _t.Card,
                AutoEllipsis = true
            };
            _tgAutoSpeed = new FluentToggle(_t, _scale)
            {
                Location = new Point(S(cardW - 104), S(16)),
                Font = UiFont(8.6f, FontStyle.Regular)
            };
            _tgAutoSpeed.CheckedChanged += (s, e) =>
            {
                _slManualSpeed.Enabled = !_tgAutoSpeed.Checked;
                UpdateSliderLabels();
                ScheduleSave(true);
            };
            _lblManualLabel = new Label
            {
                Location = new Point(S(16), S(62)),
                Size = new Size(S(82), S(20)),
                Font = UiFont(8.7f, FontStyle.Regular),
                ForeColor = _t.TextSecondary,
                BackColor = _t.Card
            };
            _slManualSpeed = new FluentSlider(_t, _scale, 40, 300)
            {
                Location = new Point(S(100), S(58)),
                Size = new Size(S(cardW - 186), S(28))
            };
            _slManualSpeed.ValueChanged += (s, e) => { UpdateSliderLabels(); ScheduleSave(false); };
            _lblManualSpeedVal = new Label
            {
                Location = new Point(S(cardW - 80), S(62)),
                Size = new Size(S(64), S(20)),
                Font = UiFont(9.0f, FontStyle.Bold),
                ForeColor = _t.Accent,
                BackColor = _t.Card,
                TextAlign = ContentAlignment.MiddleRight
            };
            speedCard.Controls.Add(_lblAutoSpeedTitle);
            speedCard.Controls.Add(_lblAutoSpeedSub);
            speedCard.Controls.Add(_tgAutoSpeed);
            speedCard.Controls.Add(_lblManualLabel);
            speedCard.Controls.Add(_slManualSpeed);
            speedCard.Controls.Add(_lblManualSpeedVal);
            body.Controls.Add(speedCard);
            y += 104;

            y = AddSliderCard(body, pad, y, cardW, 10, 50, out _lblStaggerTitle, out _lblStaggerSub, out _slStagger, out _lblStaggerVal);
            y = AddSliderCard(body, pad, y, cardW, 8, 40, out _lblHoldTitle, out _lblHoldSub, out _slClickHold, out _lblClickHoldVal);
            y += 12;

            // Section 5: Updates & About
            y = AddSectionTitle(body, pad, y, cardW, out _secAbout);
            y = AddActionCard(body, pad, y, cardW, out _lblUpdateTitle, out _lblUpdateSub, out _btnUpdate, false, (s, e) => OnUpdateButtonClick());

            scrollBody.SetContentHeight(S(y + 18));
        }

        private int AddSectionTitle(Control parent, int x, int y, int w, out Label lbl)
        {
            lbl = new Label
            {
                Location = new Point(S(x + 2), S(y)),
                Size = new Size(S(w), S(22)),
                Font = UiFont(9.1f, FontStyle.Bold),
                ForeColor = _t.TextPrimary,
                BackColor = _t.Canvas,
                AutoEllipsis = true
            };
            parent.Controls.Add(lbl);
            return y + 26;
        }

        private int AddToggleCard(Control parent, int x, int y, int w,
            out Label titleLbl, out Label subLbl, out FluentToggle toggle, EventHandler onChanged)
        {
            var card = new FluentCard(_t, 7f * _scale)
            {
                Location = new Point(S(x), S(y)),
                Size = new Size(S(w), S(58))
            };
            titleLbl = new Label
            {
                Location = new Point(S(16), S(10)),
                Size = new Size(S(w - 124), S(20)),
                Font = UiFont(9.3f, FontStyle.Regular),
                ForeColor = _t.TextPrimary,
                BackColor = _t.Card,
                AutoEllipsis = true
            };
            subLbl = new Label
            {
                Location = new Point(S(16), S(32)),
                Size = new Size(S(w - 124), S(16)),
                Font = UiFont(7.9f, FontStyle.Regular),
                ForeColor = _t.TextSecondary,
                BackColor = _t.Card,
                AutoEllipsis = true
            };
            toggle = new FluentToggle(_t, _scale)
            {
                Location = new Point(S(w - 104), S(17)),
                Font = UiFont(8.6f, FontStyle.Regular)
            };
            toggle.CheckedChanged += onChanged;
            card.Controls.Add(titleLbl);
            card.Controls.Add(subLbl);
            card.Controls.Add(toggle);
            parent.Controls.Add(card);
            return y + 63;
        }

        private int AddActionCard(Control parent, int x, int y, int w,
            out Label titleLbl, out Label subLbl, out FluentButton btn, bool primary, EventHandler onClick)
        {
            var card = new FluentCard(_t, 7f * _scale)
            {
                Location = new Point(S(x), S(y)),
                Size = new Size(S(w), S(60))
            };
            titleLbl = new Label
            {
                Location = new Point(S(16), S(10)),
                Size = new Size(S(w - 154), S(20)),
                Font = UiFont(9.3f, FontStyle.Bold),
                ForeColor = _t.TextPrimary,
                BackColor = _t.Card,
                AutoEllipsis = true
            };
            subLbl = new Label
            {
                Location = new Point(S(16), S(32)),
                Size = new Size(S(w - 154), S(18)),
                Font = UiFont(7.9f, FontStyle.Regular),
                ForeColor = _t.TextSecondary,
                BackColor = _t.Card,
                AutoEllipsis = true
            };
            btn = new FluentButton(_t, _scale, primary)
            {
                Location = new Point(S(w - 134), S(15)),
                Size = new Size(S(118), S(30)),
                Font = UiFont(8.4f, FontStyle.Bold)
            };
            btn.Click += onClick;
            card.Controls.Add(titleLbl);
            card.Controls.Add(subLbl);
            card.Controls.Add(btn);
            parent.Controls.Add(card);
            return y + 65;
        }

        private int AddDropdownCard(Control parent, int x, int y, int w, out Label cardTitleLbl,
            out Label l1, out FluentDropdown c1,
            out Label l2, out FluentDropdown c2,
            out Label l3, out FluentDropdown c3)
        {
            var card = new FluentCard(_t, 7f * _scale)
            {
                Location = new Point(S(x), S(y)),
                Size = new Size(S(w), S(136))
            };
            cardTitleLbl = new Label
            {
                Location = new Point(S(16), S(10)),
                Size = new Size(S(w - 32), S(20)),
                Font = UiFont(9.4f, FontStyle.Bold),
                ForeColor = _t.TextPrimary,
                BackColor = _t.Card,
                AutoEllipsis = true
            };
            card.Controls.Add(cardTitleLbl);

            c1 = AddDropdownRow(card, 37, w, out l1);
            c2 = AddDropdownRow(card, 69, w, out l2);
            c3 = AddDropdownRow(card, 101, w, out l3);

            parent.Controls.Add(card);
            return y + 142;
        }

        private FluentDropdown AddDropdownRow(FluentCard card, int y, int w, out Label lbl)
        {
            lbl = new Label
            {
                Location = new Point(S(16), S(y + 4)),
                Size = new Size(S(190), S(20)),
                Font = UiFont(8.8f, FontStyle.Regular),
                ForeColor = _t.TextSecondary,
                BackColor = _t.Card,
                AutoEllipsis = true
            };
            var dd = new FluentDropdown(_t, _scale)
            {
                Location = new Point(S(w - 208), S(y)),
                Size = new Size(S(192), S(26)),
                Font = UiFont(8.7f, FontStyle.Regular)
            };
            dd.SelectedIndexChanged += (s, e) => ScheduleSave(true);
            card.Controls.Add(lbl);
            card.Controls.Add(dd);
            return dd;
        }

        private int AddSliderCard(Control parent, int x, int y, int w, int min, int max,
            out Label titleLbl, out Label subLbl, out FluentSlider slider, out Label valLbl)
        {
            var card = new FluentCard(_t, 7f * _scale)
            {
                Location = new Point(S(x), S(y)),
                Size = new Size(S(w), S(78))
            };
            titleLbl = new Label
            {
                Location = new Point(S(16), S(10)),
                Size = new Size(S(w - 100), S(20)),
                Font = UiFont(9.3f, FontStyle.Regular),
                ForeColor = _t.TextPrimary,
                BackColor = _t.Card,
                AutoEllipsis = true
            };
            valLbl = new Label
            {
                Location = new Point(S(w - 84), S(10)),
                Size = new Size(S(68), S(20)),
                Font = UiFont(9.2f, FontStyle.Bold),
                ForeColor = _t.Accent,
                BackColor = _t.Card,
                TextAlign = ContentAlignment.MiddleRight
            };
            subLbl = new Label
            {
                Location = new Point(S(16), S(30)),
                Size = new Size(S(w - 32), S(16)),
                Font = UiFont(7.9f, FontStyle.Regular),
                ForeColor = _t.TextSecondary,
                BackColor = _t.Card,
                AutoEllipsis = true
            };
            slider = new FluentSlider(_t, _scale, min, max)
            {
                Location = new Point(S(14), S(48)),
                Size = new Size(S(w - 28), S(26))
            };
            slider.ValueChanged += (s, e) => { UpdateSliderLabels(); ScheduleSave(false); };
            card.Controls.Add(titleLbl);
            card.Controls.Add(subLbl);
            card.Controls.Add(valLbl);
            card.Controls.Add(slider);
            parent.Controls.Add(card);
            return y + 84;
        }

        private void RefreshUpdateLabels()
        {
            _lblUpdateTitle.Text = L10n.T("lbl_update");
            string fmt = L10n.T(_updateStateKey);
            _lblUpdateSub.Text = (_updateStateArg != null && fmt.IndexOf("{0}", StringComparison.Ordinal) >= 0)
                ? string.Format(fmt, _updateStateArg)
                : fmt;
            _btnUpdate.Text = (_pendingUpdate != null && _pendingUpdate.HasUpdate)
                ? L10n.T("btn_do_upd")
                : L10n.T("btn_check_upd");
            _btnUpdate.Primary = (_pendingUpdate != null && _pendingUpdate.HasUpdate);
            _btnUpdate.Invalidate();
        }

        private void ApplyLanguage()
        {
            bool prev = _isLoadingUi;
            _isLoadingUi = true;

            Text = L10n.T("win_title") + "  v" + AppMeta.Version;
            _lblPageTitle.Text = L10n.T("page_title");
            _lblHeroSub.Text = L10n.T("hero_sub");
            _btnAdmin.Text = L10n.T("btn_admin");

            _secCore.Text = L10n.T("sec_core");
            _lblEnabledTitle.Text = L10n.T("lbl_enabled");
            _lblEnabledSub.Text = L10n.T("sub_enabled");
            _lblAccelTitle.Text = L10n.T("lbl_accel");
            _lblAccelSub.Text = L10n.T("sub_accel");
            _lblWinOptTitle.Text = L10n.T("lbl_winopt");
            _lblWinOptSub.Text = L10n.T("sub_winopt");
            _btnWinOpt.Text = L10n.T("btn_winopt");
            _lblAutoStartTitle.Text = L10n.T("lbl_autostart");
            _lblAutoStartSub.Text = L10n.T("sub_autostart");
            _lblSoundTitle.Text = L10n.T("lbl_sound");
            _lblSoundSub.Text = L10n.T("sub_sound");

            _secGestures.Text = L10n.T("sec_gestures");
            _lblCard2F.Text = L10n.T("card_2f");
            _lblCard3F.Text = L10n.T("card_3f");
            _lbl2Drag.Text = L10n.T("row_drag");
            _lbl2Tap.Text = L10n.T("row_2tap");
            _lbl11Tap.Text = L10n.T("row_11tap");
            _lbl3Drag.Text = L10n.T("row_drag");
            _lbl3Tap.Text = L10n.T("row_3tap");
            _lbl12Tap.Text = L10n.T("row_12tap");

            string[] dragChoices = L10n.DragChoices();
            string[] clickChoices = L10n.ClickChoices();
            _cb2Drag.SetItems(dragChoices, Clamp((int)_config.TwoFingerDrag, 0, dragChoices.Length - 1));
            _cb2Tap.SetItems(clickChoices, Clamp((int)_config.TwoFingerTap, 0, clickChoices.Length - 1));
            _cb11Tap.SetItems(clickChoices, Clamp((int)_config.OnePlusOneTap, 0, clickChoices.Length - 1));
            _cb3Drag.SetItems(dragChoices, Clamp((int)_config.ThreeFingerDrag, 0, dragChoices.Length - 1));
            _cb3Tap.SetItems(clickChoices, Clamp((int)_config.ThreeFingerTap, 0, clickChoices.Length - 1));
            _cb12Tap.SetItems(clickChoices, Clamp((int)_config.OnePlusTwoTap, 0, clickChoices.Length - 1));

            _secFps.Text = L10n.T("sec_fps");
            _lblFpsTitle.Text = L10n.T("lbl_fps");
            _lblFpsSub.Text = L10n.T("sub_fps");
            _lblAimLockTitle.Text = L10n.T("lbl_aimlock");
            _lblAimLockSub.Text = L10n.T("sub_aimlock");

            _secTune.Text = L10n.T("sec_tune");
            _lblAutoSpeedTitle.Text = L10n.T("lbl_autospeed");
            _lblAutoSpeedSub.Text = L10n.T("sub_autospeed");
            _lblManualLabel.Text = L10n.T("lbl_manual");
            _lblStaggerTitle.Text = L10n.T("lbl_stagger");
            _lblStaggerSub.Text = L10n.T("sub_stagger");
            _lblHoldTitle.Text = L10n.T("lbl_hold");
            _lblHoldSub.Text = L10n.T("sub_hold");

            _secAbout.Text = L10n.T("sec_about");
            RefreshUpdateLabels();

            _btnRestore.Text = L10n.T("btn_restore");
            _btnReset.Text = L10n.T("btn_reset");
            _lblFooterInfo.Text = L10n.T(_footerKey);

            _tgEnabled.Invalidate();
            _tgDisableAccel.Invalidate();
            _tgAutoStart.Invalidate();
            _tgHotkeySound.Invalidate();
            _tgAdsLatch.Invalidate();
            _tgAimLock.Invalidate();
            _tgAutoSpeed.Invalidate();
            _btnWinOpt.Invalidate();
            _btnRestore.Invalidate();
            _btnReset.Invalidate();

            _isLoadingUi = prev;
            UpdateSliderLabels();
            RefreshDriverStatus();
        }

        private void OnUpdateButtonClick()
        {
            if (_updateBusy) return;

            if (_pendingUpdate != null && _pendingUpdate.HasUpdate)
            {
                StartDownloadAndUpdate(_pendingUpdate.DownloadUrl);
                return;
            }

            _updateBusy = true;
            _btnUpdate.Enabled = false;
            _updateStateKey = "upd_checking";
            _updateStateArg = null;
            _lblUpdateSub.ForeColor = _t.TextSecondary;
            RefreshUpdateLabels();

            ThreadPool.QueueUserWorkItem(_ =>
            {
                UpdateCheckResult res = UpdateChecker.CheckForUpdate();
                try
                {
                    BeginInvoke((Action)(() =>
                    {
                        _updateBusy = false;
                        _btnUpdate.Enabled = true;
                        if (!res.Success)
                        {
                            _pendingUpdate = null;
                            _updateStateKey = "upd_err";
                            _updateStateArg = null;
                            _lblUpdateSub.ForeColor = _t.Danger;
                        }
                        else if (res.HasUpdate)
                        {
                            _pendingUpdate = res;
                            _updateStateKey = "upd_avail";
                            _updateStateArg = res.LatestVersion;
                            _lblUpdateSub.ForeColor = _t.Accent;
                        }
                        else
                        {
                            _pendingUpdate = null;
                            _updateStateKey = "upd_latest";
                            _updateStateArg = AppMeta.Version;
                            _lblUpdateSub.ForeColor = _t.Success;
                        }
                        RefreshUpdateLabels();
                    }));
                }
                catch { }
            });
        }

        private void StartDownloadAndUpdate(string downloadUrl)
        {
            _updateBusy = true;
            _btnUpdate.Enabled = false;
            _updateStateKey = "upd_downloading";
            _updateStateArg = "0";
            _lblUpdateSub.ForeColor = _t.Accent;
            RefreshUpdateLabels();

            string currentDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\');

            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    string setupPath = UpdateChecker.DownloadInstaller(downloadUrl, pct =>
                    {
                        try
                        {
                            BeginInvoke((Action)(() =>
                            {
                                _updateStateKey = "upd_downloading";
                                _updateStateArg = pct.ToString();
                                RefreshUpdateLabels();
                            }));
                        }
                        catch { }
                    });

                    try
                    {
                        BeginInvoke((Action)(() =>
                        {
                            _updateStateKey = "upd_installing";
                            _updateStateArg = null;
                            RefreshUpdateLabels();
                        }));
                    }
                    catch { }

                    var psi = new ProcessStartInfo
                    {
                        FileName = setupPath,
                        Arguments = "--update \"--dir=" + currentDir + "\"",
                        UseShellExecute = true
                    };
                    Process.Start(psi);

                    try { BeginInvoke((Action)(() => Close())); } catch { }
                }
                catch (Exception ex)
                {
                    AppMeta.LogCrash("TouchpadGoodSettings.Update", ex);
                    try
                    {
                        BeginInvoke((Action)(() =>
                        {
                            _updateBusy = false;
                            _btnUpdate.Enabled = true;
                            _updateStateKey = "upd_err";
                            _updateStateArg = null;
                            _lblUpdateSub.ForeColor = _t.Danger;
                            RefreshUpdateLabels();
                        }));
                    }
                    catch { }
                }
            });
        }

        private void LoadConfigToUi()
        {
            _isLoadingUi = true;
            _cbLanguage.SelectedIndex = L10n.LangIndex(_config.Language);
            _tgEnabled.Checked = _config.Enabled;
            _tgDisableAccel.Checked = _config.DisableAccel;
            _tgAutoStart.Checked = IsTaskRegistered();
            _tgHotkeySound.Checked = _config.HotkeySound;

            _cb2Drag.SelectedIndex = Clamp((int)_config.TwoFingerDrag, 0, 3);
            _cb2Tap.SelectedIndex = Clamp((int)_config.TwoFingerTap, 0, 3);
            _cb11Tap.SelectedIndex = Clamp((int)_config.OnePlusOneTap, 0, 3);

            _cb3Drag.SelectedIndex = Clamp((int)_config.ThreeFingerDrag, 0, 3);
            _cb3Tap.SelectedIndex = Clamp((int)_config.ThreeFingerTap, 0, 3);
            _cb12Tap.SelectedIndex = Clamp((int)_config.OnePlusTwoTap, 0, 3);

            _tgAdsLatch.Checked = _config.EnableFpsAdsLatch;
            _tgAimLock.Checked = _config.LockPointerToAimFinger;

            _tgAutoSpeed.Checked = _config.AutoMatchDragSpeed;
            _slManualSpeed.Value = Clamp(_config.ManualDragSpeedPercent, _slManualSpeed.Minimum, _slManualSpeed.Maximum);
            _slManualSpeed.Enabled = !_config.AutoMatchDragSpeed;

            _slStagger.Value = Clamp(_config.StaggerMs, _slStagger.Minimum, _slStagger.Maximum);
            _slClickHold.Value = Clamp(_config.MinClickHoldMs, _slClickHold.Minimum, _slClickHold.Maximum);
            _isLoadingUi = false;

            UpdateSliderLabels();
        }

        private void UpdateSliderLabels()
        {
            _lblManualSpeedVal.Text = _tgAutoSpeed.Checked ? L10n.T("auto") : (_slManualSpeed.Value / 100.0).ToString("0.00") + "x";
            _lblStaggerVal.Text = _slStagger.Value + " ms";
            _lblClickHoldVal.Text = _slClickHold.Value + " ms";
        }

        private void ScheduleSave(bool immediate)
        {
            if (_isLoadingUi) return;
            if (immediate)
            {
                if (_saveDebounce != null) _saveDebounce.Stop();
                CommitSave();
            }
            else if (_saveDebounce != null)
            {
                _saveDebounce.Stop();
                _saveDebounce.Start();
            }
        }

        private void CommitSave()
        {
            if (_isLoadingUi) return;

            _config.Enabled = _tgEnabled.Checked;
            _config.DisableAccel = _tgDisableAccel.Checked;
            _config.HotkeySound = _tgHotkeySound.Checked;

            _config.TwoFingerDrag = (DragAction)Math.Max(0, _cb2Drag.SelectedIndex);
            _config.TwoFingerTap = (ClickAction)Math.Max(0, _cb2Tap.SelectedIndex);
            _config.OnePlusOneTap = (ClickAction)Math.Max(0, _cb11Tap.SelectedIndex);

            _config.ThreeFingerDrag = (DragAction)Math.Max(0, _cb3Drag.SelectedIndex);
            _config.ThreeFingerTap = (ClickAction)Math.Max(0, _cb3Tap.SelectedIndex);
            _config.OnePlusTwoTap = (ClickAction)Math.Max(0, _cb12Tap.SelectedIndex);

            _config.EnableFpsAdsLatch = _tgAdsLatch.Checked;
            _config.LockPointerToAimFinger = _tgAimLock.Checked;

            _config.AutoMatchDragSpeed = _tgAutoSpeed.Checked;
            _config.ManualDragSpeedPercent = _slManualSpeed.Value;
            _config.StaggerMs = _slStagger.Value;
            _config.MinClickHoldMs = _slClickHold.Value;

            _config.Save();

            IntPtr drv = FindWindow(DriverWindowClass, null);
            if (drv != IntPtr.Zero)
                PostMessage(drv, WM_DRIVER_RELOAD, IntPtr.Zero, IntPtr.Zero);
            else
            {
                var copy = DriverConfig.Load();
                ThreadPool.QueueUserWorkItem(_ => WindowsTouchpadSync.Apply(copy));
            }

            UpdateSliderLabels();
            SetFooter("footer_saved");
        }

        private void ApplyWindowsAutoOptimize()
        {
            _isLoadingUi = true;
            _tgEnabled.Checked = true;
            _tgDisableAccel.Checked = true;
            _isLoadingUi = false;

            _config.Enabled = true;
            _config.DisableAccel = true;
            _config.Save();

            var snap = DriverConfig.Load();
            _btnWinOpt.Enabled = false;
            ThreadPool.QueueUserWorkItem(_ =>
            {
                WindowsTouchpadSync.ApplyRecommendedWindowsSetup(snap);
                IntPtr drv = FindWindow(DriverWindowClass, null);
                if (drv != IntPtr.Zero)
                    PostMessage(drv, WM_DRIVER_RELOAD, IntPtr.Zero, IntPtr.Zero);

                try
                {
                    BeginInvoke((Action)(() =>
                    {
                        _btnWinOpt.Enabled = true;
                        EnsureDriverRunningIfEnabled();
                        SetFooter("footer_winopt");
                    }));
                }
                catch { }
            });
        }

        private void ApplyDefaultPreset()
        {
            string keepLang = _config.Language;
            _config = new DriverConfig { Language = keepLang };
            LoadConfigToUi();
            CommitSave();
            EnsureDriverRunningIfEnabled();
            SetFooter("footer_reset");
        }

        private void RestoreWindowsDefaults()
        {
            _isLoadingUi = true;
            _tgEnabled.Checked = false;
            _isLoadingUi = false;

            _config.Enabled = false;
            _config.Save();

            IntPtr drv = FindWindow(DriverWindowClass, null);
            if (drv != IntPtr.Zero)
                PostMessage(drv, WM_DRIVER_RELOAD, IntPtr.Zero, IntPtr.Zero);

            ThreadPool.QueueUserWorkItem(_ =>
            {
                WindowsTouchpadSync.RestoreOriginal();
                try
                {
                    BeginInvoke((Action)(() => SetFooter("footer_restored")));
                }
                catch { }
            });
        }

        private int QueryDriverState()
        {
            IntPtr drv = FindWindow(DriverWindowClass, null);
            if (drv == IntPtr.Zero) return 0;
            IntPtr res;
            IntPtr ok = SendMessageTimeout(drv, WM_DRIVER_PING, IntPtr.Zero, IntPtr.Zero, 0x0002, 250, out res);
            if (ok == IntPtr.Zero) return 1;
            return res.ToInt32();
        }

        private void EnsureDriverRunningIfEnabled()
        {
            if (_config.Enabled && QueryDriverState() == 0 && File.Exists(DriverExePath))
            {
                try { Process.Start(DriverExePath); } catch { }
            }
        }

        private void ToggleDriverProcess()
        {
            IntPtr drv = FindWindow(DriverWindowClass, null);
            if (drv != IntPtr.Zero)
            {
                PostMessage(drv, WM_DRIVER_STOP, IntPtr.Zero, IntPtr.Zero);
                SetFooter("footer_stopped");
                RefreshStatusLater();
            }
            else if (File.Exists(DriverExePath))
            {
                try
                {
                    Process.Start(DriverExePath);
                    SetFooter("footer_started");
                }
                catch { }
                RefreshStatusLater();
            }
        }

        private void RestartDriverAsAdmin()
        {
            if (!File.Exists(DriverExePath)) return;
            try
            {
                IntPtr drv = FindWindow(DriverWindowClass, null);
                if (drv != IntPtr.Zero)
                    PostMessage(drv, WM_DRIVER_STOP, new IntPtr(1), IntPtr.Zero);

                var psi = new ProcessStartInfo
                {
                    FileName = DriverExePath,
                    Arguments = "--wait-prev",
                    UseShellExecute = true,
                    Verb = "runas"
                };
                Process.Start(psi);
                SetFooter("footer_admin_ok");
                RefreshStatusLater();
            }
            catch
            {
                SetFooter("footer_admin_cancel");
                EnsureDriverRunningIfEnabled();
                RefreshStatusLater();
            }
        }

        private void RefreshStatusLater()
        {
            var t = new System.Windows.Forms.Timer { Interval = 250 };
            int n = 0;
            t.Tick += (s, e) =>
            {
                n++;
                RefreshDriverStatus();
                if (n >= 8) { t.Stop(); t.Dispose(); }
            };
            t.Start();
        }

        private void RefreshDriverStatus()
        {
            int state = QueryDriverState();
            if (state == 2)
            {
                _statusDot.DotColor = _t.Success;
                _lblStatusText.Text = L10n.T("status_admin");
                _btnToggleDriver.Text = L10n.T("btn_stop");
                _btnAdmin.Enabled = false;
            }
            else if (state == 1)
            {
                _statusDot.DotColor = _t.Success;
                _lblStatusText.Text = L10n.T("status_std");
                _btnToggleDriver.Text = L10n.T("btn_stop");
                _btnAdmin.Enabled = true;
            }
            else
            {
                _statusDot.DotColor = _t.Danger;
                _lblStatusText.Text = L10n.T("status_stopped");
                _btnToggleDriver.Text = L10n.T("btn_start");
                _btnAdmin.Enabled = true;
            }
            _btnToggleDriver.Invalidate();
            _btnAdmin.Invalidate();
        }

        private static bool IsTaskRegistered()
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "schtasks.exe",
                    Arguments = "/Query /TN \"" + TaskName + "\"",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                using (var p = Process.Start(psi))
                {
                    p.WaitForExit(2000);
                    if (p.ExitCode == 0) return true;
                }
            }
            catch { }

            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(RunRegPath, false))
                    if (k != null && k.GetValue(TaskName) != null) return true;
            }
            catch { }
            return false;
        }

        private void ToggleAutoStartTask()
        {
            bool wantEnable = _tgAutoStart.Checked;
            try
            {
                if (!wantEnable)
                {
                    try
                    {
                        using (var k = Registry.CurrentUser.OpenSubKey(RunRegPath, true))
                            if (k != null) k.DeleteValue(TaskName, false);
                    }
                    catch { }

                    var psi = new ProcessStartInfo
                    {
                        FileName = "schtasks.exe",
                        Arguments = "/Delete /TN \"" + TaskName + "\" /F",
                        UseShellExecute = true,
                        Verb = "runas",
                        WindowStyle = ProcessWindowStyle.Hidden
                    };
                    using (var p = Process.Start(psi)) { p.WaitForExit(5000); }
                    SetFooter("footer_task_off");
                }
                else
                {
                    string user = Environment.UserDomainName + "\\" + Environment.UserName;
                    string exe = DriverExePath.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
                    string xml =
                        "<?xml version=\"1.0\" encoding=\"UTF-16\"?>\r\n" +
                        "<Task version=\"1.2\" xmlns=\"http://schemas.microsoft.com/windows/2004/02/mit/task\">\r\n" +
                        "  <Triggers><LogonTrigger><Enabled>true</Enabled><UserId>" + user + "</UserId></LogonTrigger></Triggers>\r\n" +
                        "  <Principals><Principal id=\"Author\"><UserId>" + user + "</UserId><LogonType>InteractiveToken</LogonType><RunLevel>HighestAvailable</RunLevel></Principal></Principals>\r\n" +
                        "  <Settings>\r\n" +
                        "    <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>\r\n" +
                        "    <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>\r\n" +
                        "    <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>\r\n" +
                        "    <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>\r\n" +
                        "    <Priority>5</Priority>\r\n" +
                        "  </Settings>\r\n" +
                        "  <Actions Context=\"Author\"><Exec><Command>" + exe + "</Command></Exec></Actions>\r\n" +
                        "</Task>";

                    string tmpXml = Path.Combine(Path.GetTempPath(), "TouchpadGood_Task.xml");
                    File.WriteAllText(tmpXml, xml, Encoding.Unicode);

                    try
                    {
                        var psi = new ProcessStartInfo
                        {
                            FileName = "schtasks.exe",
                            Arguments = "/Create /TN \"" + TaskName + "\" /XML \"" + tmpXml + "\" /F",
                            UseShellExecute = true,
                            Verb = "runas",
                            WindowStyle = ProcessWindowStyle.Hidden
                        };
                        using (var p = Process.Start(psi)) { p.WaitForExit(6000); }
                    }
                    finally
                    {
                        try { File.Delete(tmpXml); } catch { }
                    }

                    try
                    {
                        using (var k = Registry.CurrentUser.OpenSubKey(RunRegPath, true))
                            if (k != null) k.DeleteValue(TaskName, false);
                    }
                    catch { }

                    SetFooter("footer_task_on");
                }
            }
            catch
            {
                SetFooter("footer_task_cancel");
            }

            _isLoadingUi = true;
            _tgAutoStart.Checked = IsTaskRegistered();
            _isLoadingUi = false;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (_saveDebounce != null && _saveDebounce.Enabled)
            {
                _saveDebounce.Stop();
                CommitSave();
            }
            if (_saveDebounce != null) { _saveDebounce.Dispose(); _saveDebounce = null; }
            if (_statusTimer != null) { _statusTimer.Stop(); _statusTimer.Dispose(); _statusTimer = null; }
            base.OnFormClosing(e);
        }

        private static int Clamp(int v, int min, int max)
        {
            return v < min ? min : (v > max ? max : v);
        }
    }
}
