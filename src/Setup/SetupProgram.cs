using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace TouchpadGood
{
    internal static class SetupProgram
    {
        [DllImport("user32.dll")]
        private static extern bool SetProcessDpiAwarenessContext(IntPtr value);

        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

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

            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (s, ev) => AppMeta.LogCrash("TouchpadGoodSetup", ev.Exception);
            AppDomain.CurrentDomain.UnhandledException += (s, ev) => AppMeta.LogCrash("TouchpadGoodSetup", ev.ExceptionObject as Exception);

            bool autoUninstall = false;
            bool autoUpdate = false;
            string customTargetDir = null;

            if (args != null)
            {
                for (int i = 0; i < args.Length; i++)
                {
                    string a = args[i] ?? "";
                    if (string.Equals(a, "--uninstall", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(a, "/uninstall", StringComparison.OrdinalIgnoreCase))
                    {
                        autoUninstall = true;
                    }
                    else if (string.Equals(a, "--update", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(a, "/update", StringComparison.OrdinalIgnoreCase))
                    {
                        autoUpdate = true;
                    }
                    else if (a.StartsWith("--dir=", StringComparison.OrdinalIgnoreCase))
                    {
                        string d = a.Substring(6).Trim('"');
                        if (!string.IsNullOrEmpty(d)) customTargetDir = d;
                    }
                }
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new SetupForm(autoUninstall, autoUpdate, customTargetDir));
        }
    }

    internal sealed class SetupForm : Form
    {
        private const string UninstallRegPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\TouchpadGood";
        private const string RunRegPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

        private static readonly uint WM_GOOD_STOP = RegisterWindowMessage("WM_TOUCHPADGOOD_STOP");
        private static readonly uint WM_LEGACY_STOP = RegisterWindowMessage("WM_TOUCHPADDRIVER_STOP");

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern uint RegisterWindowMessage(string message);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hwnd, uint msg, IntPtr wparam, IntPtr lparam);

        private readonly FluentTheme _t;
        private readonly float _scale = 1.0f;
        private readonly bool _startUninstallMode;
        private readonly bool _startUpdateMode;
        private readonly string _installDir;
        private bool _busy;
        private bool _selfCleanupOnClose;

        private Label _lblTitle, _lblSub, _lblPath, _lblStatus;
        private FluentDropdown _cbLang;
        private Label _lblOptWin, _lblOptAuto, _lblOptDesk, _lblOptLaunch;
        private FluentToggle _tgOptWin, _tgOptAuto, _tgOptDesk, _tgOptLaunch;
        private FluentButton _btnInstall, _btnUninstall, _btnClose;

        public SetupForm(bool startUninstallMode, bool startUpdateMode, string customTargetDir)
        {
            _startUninstallMode = startUninstallMode;
            _startUpdateMode = startUpdateMode;
            _t = FluentTheme.DetectSystemTheme();
            _installDir = !string.IsNullOrEmpty(customTargetDir)
                ? customTargetDir
                : Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Programs", AppMeta.AppName);

            var cfg = DriverConfig.Load();
            L10n.CurrentLang = cfg.Language;

            using (var g = Graphics.FromHwnd(IntPtr.Zero))
                _scale = g.DpiX / 96.0f;

            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            DoubleBuffered = true;
            InitUi();
            ApplyLanguage();
            RefreshInstalledState();
        }

        private int S(int px) { return (int)Math.Round(px * _scale); }

        private bool IsInstalled
        {
            get { return File.Exists(Path.Combine(_installDir, "TouchpadGood.exe")); }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            FluentTheme.ApplyWindowFrame(Handle, _t.IsDark, true);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            if (_startUpdateMode)
            {
                BeginAutoUpdate();
            }
            else if (_startUninstallMode && IsInstalled)
            {
                BeginUninstall();
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);
            if (_selfCleanupOnClose)
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = "/C ping 127.0.0.1 -n 3 > nul & rd /s /q \"" + _installDir + "\"",
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        WindowStyle = ProcessWindowStyle.Hidden
                    };
                    Process.Start(psi);
                }
                catch { }
            }
        }

        private void InitUi()
        {
            AutoScaleMode = AutoScaleMode.None;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = _t.Canvas;
            ForeColor = _t.TextPrimary;
            Font = FluentTheme.CreateUiFont(9.0f, FontStyle.Regular);

            int formW = 568;
            int formH = 476;
            Rectangle work = Screen.FromPoint(Cursor.Position).WorkingArea;
            int actualH = Math.Min(S(formH), Math.Max(S(380), work.Height - S(48)));
            ClientSize = new Size(S(formW), actualH);

            int pad = 22;
            int cardW = formW - pad * 2;
            int y = 18;

            _lblTitle = new Label
            {
                Location = new Point(S(pad), S(y)),
                Size = new Size(S(cardW - 140), S(30)),
                Font = FluentTheme.CreateUiFont(14.2f, FontStyle.Bold),
                ForeColor = _t.TextPrimary,
                BackColor = _t.Canvas,
                AutoEllipsis = true
            };
            Controls.Add(_lblTitle);

            _cbLang = new FluentDropdown(_t, _scale)
            {
                Location = new Point(S(pad + cardW - 130), S(y + 2)),
                Size = new Size(S(130), S(28)),
                Font = FluentTheme.CreateUiFont(8.4f, FontStyle.Regular),
                BackColor = _t.Canvas
            };
            _cbLang.SetItems(L10n.LangNames, L10n.LangIndex(L10n.CurrentLang));
            _cbLang.SelectedIndexChanged += (s, e) =>
            {
                int idx = _cbLang.SelectedIndex;
                if (idx >= 0 && idx < L10n.LangCodes.Length)
                {
                    L10n.CurrentLang = L10n.LangCodes[idx];
                    ApplyLanguage();
                    RefreshInstalledState();
                }
            };
            Controls.Add(_cbLang);
            y += 34;

            _lblSub = new Label
            {
                Location = new Point(S(pad), S(y)),
                Size = new Size(S(cardW), S(20)),
                Font = FluentTheme.CreateUiFont(8.4f, FontStyle.Regular),
                ForeColor = _t.TextSecondary,
                BackColor = _t.Canvas,
                AutoEllipsis = true
            };
            Controls.Add(_lblSub);
            y += 28;

            var pathCard = new FluentCard(_t, 7f * _scale)
            {
                Location = new Point(S(pad), S(y)),
                Size = new Size(S(cardW), S(40))
            };
            _lblPath = new Label
            {
                Location = new Point(S(14), S(10)),
                Size = new Size(S(cardW - 28), S(20)),
                Font = FluentTheme.CreateUiFont(8.3f, FontStyle.Regular),
                ForeColor = _t.TextSecondary,
                BackColor = _t.Card,
                AutoEllipsis = true
            };
            pathCard.Controls.Add(_lblPath);
            Controls.Add(pathCard);
            y += 50;

            var optCard = new FluentCard(_t, 8f * _scale)
            {
                Location = new Point(S(pad), S(y)),
                Size = new Size(S(cardW), S(208))
            };
            AddOptionRow(optCard, 12, cardW, out _lblOptWin, out _tgOptWin, true);
            AddDivider(optCard, 56, cardW);
            AddOptionRow(optCard, 64, cardW, out _lblOptAuto, out _tgOptAuto, true);
            AddDivider(optCard, 108, cardW);
            AddOptionRow(optCard, 116, cardW, out _lblOptDesk, out _tgOptDesk, true);
            AddDivider(optCard, 160, cardW);
            AddOptionRow(optCard, 168, cardW, out _lblOptLaunch, out _tgOptLaunch, true);
            Controls.Add(optCard);
            y += 220;

            _lblStatus = new Label
            {
                Location = new Point(S(pad + 2), S(y)),
                Size = new Size(S(cardW - 4), S(28)),
                Font = FluentTheme.CreateUiFont(8.5f, FontStyle.Regular),
                ForeColor = _t.Accent,
                BackColor = _t.Canvas,
                AutoEllipsis = true
            };
            Controls.Add(_lblStatus);

            int footerH = S(60);
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

            _btnUninstall = new FluentButton(_t, _scale, false)
            {
                Location = new Point(S(pad), S(14)),
                Size = new Size(S(116), S(32)),
                Font = FluentTheme.CreateUiFont(8.6f, FontStyle.Regular),
                BackColor = _t.Card,
                Danger = true
            };
            _btnUninstall.Click += (s, e) => BeginUninstall();

            _btnClose = new FluentButton(_t, _scale, false)
            {
                Location = new Point(S(formW - pad - 246), S(14)),
                Size = new Size(S(94), S(32)),
                Font = FluentTheme.CreateUiFont(8.6f, FontStyle.Regular),
                BackColor = _t.Card
            };
            _btnClose.Click += (s, e) => Close();

            _btnInstall = new FluentButton(_t, _scale, true)
            {
                Location = new Point(S(formW - pad - 144), S(14)),
                Size = new Size(S(144), S(32)),
                Font = FluentTheme.CreateUiFont(8.8f, FontStyle.Bold),
                BackColor = _t.Card
            };
            _btnInstall.Click += (s, e) => BeginInstall();

            footer.Controls.Add(_btnUninstall);
            footer.Controls.Add(_btnClose);
            footer.Controls.Add(_btnInstall);
            Controls.Add(footer);
        }

        private void AddOptionRow(FluentCard card, int y, int cardW, out Label lbl, out FluentToggle tg, bool defaultVal)
        {
            lbl = new Label
            {
                Location = new Point(S(16), S(y + 4)),
                Size = new Size(S(cardW - 120), S(28)),
                Font = FluentTheme.CreateUiFont(8.6f, FontStyle.Regular),
                ForeColor = _t.TextPrimary,
                BackColor = _t.Card,
                AutoEllipsis = true
            };
            tg = new FluentToggle(_t, _scale)
            {
                Location = new Point(S(cardW - 104), S(y + 4)),
                Font = FluentTheme.CreateUiFont(8.4f, FontStyle.Regular),
                Checked = defaultVal
            };
            card.Controls.Add(lbl);
            card.Controls.Add(tg);
        }

        private void AddDivider(FluentCard card, int y, int cardW)
        {
            var div = new Panel
            {
                Location = new Point(S(16), S(y)),
                Size = new Size(S(cardW - 32), 1),
                BackColor = _t.Divider
            };
            card.Controls.Add(div);
        }

        private void ApplyLanguage()
        {
            Text = L10n.T("setup_title") + " v" + AppMeta.Version;
            _lblTitle.Text = L10n.T("setup_title");
            _lblSub.Text = L10n.T("setup_sub");
            _lblPath.Text = L10n.T("setup_ready") + _installDir;
            _lblOptWin.Text = L10n.T("setup_opt_win");
            _lblOptAuto.Text = L10n.T("setup_opt_auto");
            _lblOptDesk.Text = L10n.T("setup_opt_desk");
            _lblOptLaunch.Text = L10n.T("setup_opt_launch");
            _btnUninstall.Text = L10n.T("setup_btn_uninstall");
            _btnClose.Text = L10n.T("setup_btn_close");

            _tgOptWin.Invalidate();
            _tgOptAuto.Invalidate();
            _tgOptDesk.Invalidate();
            _tgOptLaunch.Invalidate();
        }

        private void RefreshInstalledState()
        {
            bool inst = IsInstalled;
            _btnInstall.Text = inst ? L10n.T("setup_btn_reinstall") : L10n.T("setup_btn_install");
            _btnUninstall.Visible = inst;
            _btnUninstall.Enabled = inst && !_busy;
            if (inst && string.IsNullOrEmpty(_lblStatus.Text))
            {
                _lblStatus.ForeColor = _t.TextSecondary;
                _lblStatus.Text = L10n.T("setup_installed") + " · v" + AppMeta.Version;
            }
            _btnInstall.Invalidate();
            _btnUninstall.Invalidate();
        }

        private void SetBusy(bool busy)
        {
            _busy = busy;
            _btnInstall.Enabled = !busy;
            _btnUninstall.Enabled = !busy && IsInstalled;
            _btnClose.Enabled = !busy;
            _tgOptWin.Enabled = !busy;
            _tgOptAuto.Enabled = !busy;
            _tgOptDesk.Enabled = !busy;
            _tgOptLaunch.Enabled = !busy;
            _cbLang.Enabled = !busy;
        }

        private void BeginAutoUpdate()
        {
            if (_busy) return;
            SetBusy(true);
            _lblStatus.ForeColor = _t.Accent;
            _lblStatus.Text = L10n.T("upd_installing");

            string lang = L10n.CurrentLang;
            ThreadPool.QueueUserWorkItem(_ =>
            {
                string error = null;
                try
                {
                    PerformInPlaceUpdate(lang);
                }
                catch (Exception ex)
                {
                    error = ex.Message;
                    AppMeta.LogCrash("TouchpadGoodSetup.Update", ex);
                }

                try
                {
                    BeginInvoke((Action)(() =>
                    {
                        SetBusy(false);
                        if (error == null)
                        {
                            _lblStatus.ForeColor = _t.Success;
                            _lblStatus.Text = L10n.T("setup_done");
                            var closeTimer = new System.Windows.Forms.Timer { Interval = 900 };
                            closeTimer.Tick += (s2, e2) => { closeTimer.Stop(); closeTimer.Dispose(); Close(); };
                            closeTimer.Start();
                        }
                        else
                        {
                            _lblStatus.ForeColor = _t.Danger;
                            _lblStatus.Text = "Error: " + error;
                        }
                    }));
                }
                catch { }
            });
        }

        private void BeginInstall()
        {
            if (_busy) return;
            SetBusy(true);
            _lblStatus.ForeColor = _t.Accent;
            _lblStatus.Text = "...";

            bool optWin = _tgOptWin.Checked;
            bool optAuto = _tgOptAuto.Checked;
            bool optDesk = _tgOptDesk.Checked;
            bool optLaunch = _tgOptLaunch.Checked;
            string lang = L10n.CurrentLang;

            ThreadPool.QueueUserWorkItem(_ =>
            {
                string error = null;
                try
                {
                    PerformInstall(optWin, optAuto, optDesk, optLaunch, lang);
                }
                catch (Exception ex)
                {
                    error = ex.Message;
                    AppMeta.LogCrash("TouchpadGoodSetup.Install", ex);
                }

                try
                {
                    BeginInvoke((Action)(() =>
                    {
                        SetBusy(false);
                        if (error == null)
                        {
                            _lblStatus.ForeColor = _t.Success;
                            _lblStatus.Text = L10n.T("setup_done");
                        }
                        else
                        {
                            _lblStatus.ForeColor = _t.Danger;
                            _lblStatus.Text = "Error: " + error;
                        }
                        RefreshInstalledState();
                    }));
                }
                catch { }
            });
        }

        private void BeginUninstall()
        {
            if (_busy) return;
            SetBusy(true);
            _lblStatus.ForeColor = _t.Accent;
            _lblStatus.Text = "...";

            ThreadPool.QueueUserWorkItem(_ =>
            {
                string error = null;
                try
                {
                    PerformUninstall();
                }
                catch (Exception ex)
                {
                    error = ex.Message;
                    AppMeta.LogCrash("TouchpadGoodSetup.Uninstall", ex);
                }

                try
                {
                    BeginInvoke((Action)(() =>
                    {
                        SetBusy(false);
                        if (error == null)
                        {
                            _lblStatus.ForeColor = _t.Success;
                            _lblStatus.Text = L10n.T("setup_uninstalled");
                        }
                        else
                        {
                            _lblStatus.ForeColor = _t.Danger;
                            _lblStatus.Text = "Error: " + error;
                        }
                        RefreshInstalledState();
                    }));
                }
                catch { }
            });
        }

        // Seamless in-place update preserving user's existing settings and install location
        private void PerformInPlaceUpdate(string lang)
        {
            StopAllRunningApps();
            Directory.CreateDirectory(_installDir);

            string drvPath = Path.Combine(_installDir, "TouchpadGood.exe");
            string setPath = Path.Combine(_installDir, "TouchpadGoodSettings.exe");
            string uninstPath = Path.Combine(_installDir, "TouchpadGoodUninstall.exe");

            ExtractResourceWithRetry("TouchpadGood.exe", drvPath);
            ExtractResourceWithRetry("TouchpadGoodSettings.exe", setPath);

            // Update uninstaller & Add/Remove Programs version if standard install folder
            string selfPath = Assembly.GetExecutingAssembly().Location;
            if (File.Exists(uninstPath) && !string.Equals(Path.GetFullPath(selfPath), Path.GetFullPath(uninstPath), StringComparison.OrdinalIgnoreCase))
            {
                CopyFileWithRetry(selfPath, uninstPath);
            }
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(UninstallRegPath, true))
                {
                    if (key != null) key.SetValue("DisplayVersion", AppMeta.Version);
                }
            }
            catch { }

            StartDriverViaTaskOrDirect(drvPath);
            Thread.Sleep(200);
            try { Process.Start(setPath); } catch { }
        }

        private void PerformInstall(bool optWin, bool optAuto, bool optDesk, bool optLaunch, string lang)
        {
            StopAllRunningApps();
            Directory.CreateDirectory(_installDir);

            string drvPath = Path.Combine(_installDir, "TouchpadGood.exe");
            string setPath = Path.Combine(_installDir, "TouchpadGoodSettings.exe");
            string uninstPath = Path.Combine(_installDir, "TouchpadGoodUninstall.exe");

            ExtractResourceWithRetry("TouchpadGood.exe", drvPath);
            ExtractResourceWithRetry("TouchpadGoodSettings.exe", setPath);

            string selfPath = Assembly.GetExecutingAssembly().Location;
            if (!string.Equals(Path.GetFullPath(selfPath), Path.GetFullPath(uninstPath), StringComparison.OrdinalIgnoreCase))
            {
                CopyFileWithRetry(selfPath, uninstPath);
            }

            string startDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Programs), AppMeta.AppName);
            Directory.CreateDirectory(startDir);
            CreateShortcut(Path.Combine(startDir, "TouchpadGood Settings.lnk"), setPath, _installDir, "", "TouchpadGood Settings");
            CreateShortcut(Path.Combine(startDir, "Uninstall TouchpadGood.lnk"), uninstPath, _installDir, "--uninstall", "Uninstall TouchpadGood");

            string deskLink = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                "TouchpadGood Settings.lnk");
            if (optDesk)
                CreateShortcut(deskLink, setPath, _installDir, "", "TouchpadGood Settings");
            else if (File.Exists(deskLink))
                try { File.Delete(deskLink); } catch { }

            using (var key = Registry.CurrentUser.CreateSubKey(UninstallRegPath))
            {
                if (key != null)
                {
                    key.SetValue("DisplayName", AppMeta.AppName);
                    key.SetValue("DisplayVersion", AppMeta.Version);
                    key.SetValue("Publisher", "TouchpadGood");
                    key.SetValue("InstallLocation", _installDir);
                    key.SetValue("DisplayIcon", setPath);
                    key.SetValue("UninstallString", "\"" + uninstPath + "\" --uninstall");
                    key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                    key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                }
            }

            var cfg = DriverConfig.Load();
            cfg.Language = lang;
            cfg.Enabled = true;
            cfg.DisableAccel = true;
            cfg.Save();

            if (optWin)
                WindowsTouchpadSync.ApplyRecommendedWindowsSetup(cfg);
            else
                WindowsTouchpadSync.Apply(cfg);

            RemoveAutoStart("TouchpadDriver", false);

            if (optAuto)
                RegisterAutoStart(AppMeta.AppName, drvPath);
            else
                RemoveAutoStart(AppMeta.AppName, false);

            if (optLaunch)
            {
                StartDriverViaTaskOrDirect(drvPath);
                Thread.Sleep(180);
                try { Process.Start(setPath); } catch { }
            }
        }

        private static void StartDriverViaTaskOrDirect(string drvPath)
        {
            try
            {
                var q = new ProcessStartInfo
                {
                    FileName = "schtasks.exe",
                    Arguments = "/Query /TN \"" + AppMeta.AppName + "\"",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                bool hasTask = false;
                using (var p = Process.Start(q))
                {
                    p.WaitForExit(2000);
                    hasTask = (p.ExitCode == 0);
                }

                if (hasTask)
                {
                    var run = new ProcessStartInfo
                    {
                        FileName = "schtasks.exe",
                        Arguments = "/Run /TN \"" + AppMeta.AppName + "\"",
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        WindowStyle = ProcessWindowStyle.Hidden
                    };
                    using (var p = Process.Start(run))
                    {
                        p.WaitForExit(2500);
                        if (p.ExitCode == 0) return;
                    }
                }
            }
            catch { }

            try { Process.Start(drvPath); } catch { }
        }

        private void PerformUninstall()
        {
            StopAllRunningApps();
            RemoveAutoStart(AppMeta.AppName, true);
            RemoveAutoStart("TouchpadDriver", false);

            WindowsTouchpadSync.RestoreOriginal();

            try
            {
                string startDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Programs), AppMeta.AppName);
                if (Directory.Exists(startDir)) Directory.Delete(startDir, true);
            }
            catch { }

            try
            {
                string deskLink = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                    "TouchpadGood Settings.lnk");
                if (File.Exists(deskLink)) File.Delete(deskLink);
            }
            catch { }

            try { Registry.CurrentUser.DeleteSubKeyTree(UninstallRegPath, false); } catch { }
            try { Registry.CurrentUser.DeleteSubKeyTree(@"Software\TouchpadGood", false); } catch { }

            string drvPath = Path.Combine(_installDir, "TouchpadGood.exe");
            string setPath = Path.Combine(_installDir, "TouchpadGoodSettings.exe");
            string uninstPath = Path.Combine(_installDir, "TouchpadGoodUninstall.exe");
            string selfPath = Path.GetFullPath(Assembly.GetExecutingAssembly().Location);

            TryDeleteFileWithRetry(drvPath);
            TryDeleteFileWithRetry(setPath);

            if (string.Equals(selfPath, Path.GetFullPath(uninstPath), StringComparison.OrdinalIgnoreCase))
            {
                _selfCleanupOnClose = true;
            }
            else
            {
                TryDeleteFileWithRetry(uninstPath);
                try { if (Directory.Exists(_installDir)) Directory.Delete(_installDir, true); } catch { }
            }
        }

        private static void ExtractResourceWithRetry(string resourceName, string destPath)
        {
            Exception lastErr = null;
            for (int attempt = 0; attempt < 12; attempt++)
            {
                try
                {
                    var asm = Assembly.GetExecutingAssembly();
                    using (Stream s = asm.GetManifestResourceStream(resourceName))
                    {
                        if (s == null)
                            throw new InvalidOperationException("Embedded resource missing: " + resourceName);
                        using (var fs = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None))
                        {
                            byte[] buf = new byte[65536];
                            int read;
                            while ((read = s.Read(buf, 0, buf.Length)) > 0)
                                fs.Write(buf, 0, read);
                        }
                    }
                    return;
                }
                catch (IOException ex)
                {
                    lastErr = ex;
                    Thread.Sleep(180);
                }
                catch (UnauthorizedAccessException ex)
                {
                    lastErr = ex;
                    Thread.Sleep(180);
                }
            }
            if (lastErr != null) throw lastErr;
        }

        private static void CopyFileWithRetry(string srcPath, string destPath)
        {
            for (int attempt = 0; attempt < 10; attempt++)
            {
                try
                {
                    File.Copy(srcPath, destPath, true);
                    return;
                }
                catch
                {
                    Thread.Sleep(150);
                }
            }
        }

        private static void TryDeleteFileWithRetry(string path)
        {
            if (!File.Exists(path)) return;
            for (int attempt = 0; attempt < 10; attempt++)
            {
                try
                {
                    File.Delete(path);
                    return;
                }
                catch
                {
                    Thread.Sleep(150);
                }
            }
        }

        private static void StopAllRunningApps()
        {
            IntPtr hGood = FindWindow("TouchpadGood_HostClass", null);
            if (hGood != IntPtr.Zero) PostMessage(hGood, WM_GOOD_STOP, new IntPtr(1), IntPtr.Zero);

            IntPtr hLeg = FindWindow("TouchpadDriver_HostClass", null);
            if (hLeg != IntPtr.Zero) PostMessage(hLeg, WM_LEGACY_STOP, new IntPtr(1), IntPtr.Zero);

            Thread.Sleep(300);

            int myPid = Process.GetCurrentProcess().Id;
            string[] names = new[] { "TouchpadGood", "TouchpadGoodSettings", "TouchpadDriver", "TouchpadSettings" };
            for (int i = 0; i < names.Length; i++)
            {
                try
                {
                    var procs = Process.GetProcessesByName(names[i]);
                    for (int j = 0; j < procs.Length; j++)
                    {
                        try
                        {
                            if (procs[j].Id != myPid && !procs[j].HasExited)
                            {
                                procs[j].Kill();
                                procs[j].WaitForExit(2000);
                            }
                        }
                        catch { }
                        finally { procs[j].Dispose(); }
                    }
                }
                catch { }
            }
            Thread.Sleep(150);
        }

        private static void RegisterAutoStart(string taskName, string exePath)
        {
            string user = Environment.UserDomainName + "\\" + Environment.UserName;
            string exe = exePath.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
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

            string tmpXml = Path.Combine(Path.GetTempPath(), "TouchpadGood_SetupTask.xml");
            File.WriteAllText(tmpXml, xml, Encoding.Unicode);

            bool taskOk = false;
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "schtasks.exe",
                    Arguments = "/Create /TN \"" + taskName + "\" /XML \"" + tmpXml + "\" /F",
                    UseShellExecute = true,
                    Verb = "runas",
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                using (var p = Process.Start(psi))
                {
                    p.WaitForExit(8000);
                    taskOk = (p.ExitCode == 0);
                }
            }
            catch { }
            finally
            {
                try { File.Delete(tmpXml); } catch { }
            }

            if (taskOk)
            {
                try
                {
                    using (var k = Registry.CurrentUser.OpenSubKey(RunRegPath, true))
                        if (k != null) k.DeleteValue(taskName, false);
                }
                catch { }
            }
            else
            {
                try
                {
                    using (var k = Registry.CurrentUser.OpenSubKey(RunRegPath, true))
                        if (k != null) k.SetValue(taskName, "\"" + exePath + "\"");
                }
                catch { }
            }
        }

        private static void RemoveAutoStart(string taskName, bool allowUac)
        {
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(RunRegPath, true))
                    if (k != null) k.DeleteValue(taskName, false);
            }
            catch { }

            bool exists = false;
            try
            {
                var q = new ProcessStartInfo
                {
                    FileName = "schtasks.exe",
                    Arguments = "/Query /TN \"" + taskName + "\"",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                using (var p = Process.Start(q))
                {
                    p.WaitForExit(2000);
                    exists = (p.ExitCode == 0);
                }
            }
            catch { }

            if (!exists) return;

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "schtasks.exe",
                    Arguments = "/Delete /TN \"" + taskName + "\" /F",
                    UseShellExecute = allowUac,
                    Verb = allowUac ? "runas" : "",
                    CreateNoWindow = !allowUac,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                using (var p = Process.Start(psi)) { p.WaitForExit(5000); }
            }
            catch { }
        }

        private static void CreateShortcut(string linkPath, string targetPath, string workDir, string args, string desc)
        {
            try
            {
                Type t = Type.GetTypeFromProgID("WScript.Shell");
                if (t == null) return;
                object shell = Activator.CreateInstance(t);
                object sc = t.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { linkPath });
                Type st = sc.GetType();
                st.InvokeMember("TargetPath", BindingFlags.SetProperty, null, sc, new object[] { targetPath });
                st.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, sc, new object[] { workDir });
                st.InvokeMember("Arguments", BindingFlags.SetProperty, null, sc, new object[] { args });
                st.InvokeMember("Description", BindingFlags.SetProperty, null, sc, new object[] { desc });
                st.InvokeMember("Save", BindingFlags.InvokeMethod, null, sc, null);
                Marshal.FinalReleaseComObject(sc);
                Marshal.FinalReleaseComObject(shell);
            }
            catch { }
        }
    }
}
