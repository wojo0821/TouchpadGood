using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace TouchpadGood
{
    internal sealed class FluentTheme
    {
        public bool IsDark;
        public Color Canvas;
        public Color Card;
        public Color CardHover;
        public Color CardBorder;
        public Color Divider;
        public Color TextPrimary;
        public Color TextSecondary;
        public Color TextDisabled;
        public Color Accent;
        public Color AccentHover;
        public Color TextOnAccent;
        public Color ControlBg;
        public Color ControlHover;
        public Color ControlBorder;
        public Color ControlBorderHover;
        public Color DropdownPopupBg;
        public Color ScrollTrack;
        public Color ScrollThumb;
        public Color ScrollThumbHover;
        public Color ToggleOffBorder;
        public Color ToggleOffThumb;
        public Color SliderTrack;
        public Color Success;
        public Color Danger;

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        public static void ApplyWindowFrame(IntPtr hwnd, bool isDark, bool roundCorners)
        {
            try
            {
                int dark = isDark ? 1 : 0;
                DwmSetWindowAttribute(hwnd, 20, ref dark, sizeof(int)); // DWMWA_USE_IMMERSIVE_DARK_MODE
                if (roundCorners)
                {
                    int round = 2;
                    DwmSetWindowAttribute(hwnd, 33, ref round, sizeof(int)); // DWMWA_WINDOW_CORNER_PREFERENCE = ROUND
                }
            }
            catch { }
        }

        public static FluentTheme DetectSystemTheme()
        {
            bool dark = true;
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", false))
                {
                    if (key != null)
                    {
                        object v = key.GetValue("AppsUseLightTheme", 0);
                        if (v != null && Convert.ToInt32(v) == 1) dark = false;
                    }
                }
            }
            catch { }

            if (dark)
            {
                return new FluentTheme
                {
                    IsDark = true,
                    Canvas = Color.FromArgb(32, 32, 32),
                    Card = Color.FromArgb(43, 43, 43),
                    CardHover = Color.FromArgb(50, 50, 50),
                    CardBorder = Color.FromArgb(55, 55, 55),
                    Divider = Color.FromArgb(52, 52, 52),
                    TextPrimary = Color.FromArgb(255, 255, 255),
                    TextSecondary = Color.FromArgb(172, 172, 172),
                    TextDisabled = Color.FromArgb(115, 115, 115),
                    Accent = Color.FromArgb(96, 205, 255),
                    AccentHover = Color.FromArgb(124, 216, 255),
                    TextOnAccent = Color.FromArgb(12, 12, 12),
                    ControlBg = Color.FromArgb(49, 49, 49),
                    ControlHover = Color.FromArgb(58, 58, 58),
                    ControlBorder = Color.FromArgb(62, 62, 62),
                    ControlBorderHover = Color.FromArgb(80, 80, 80),
                    DropdownPopupBg = Color.FromArgb(44, 44, 44),
                    ScrollTrack = Color.FromArgb(32, 32, 32),
                    ScrollThumb = Color.FromArgb(66, 66, 66),
                    ScrollThumbHover = Color.FromArgb(98, 98, 98),
                    ToggleOffBorder = Color.FromArgb(152, 152, 152),
                    ToggleOffThumb = Color.FromArgb(204, 204, 204),
                    SliderTrack = Color.FromArgb(86, 86, 86),
                    Success = Color.FromArgb(108, 203, 95),
                    Danger = Color.FromArgb(255, 153, 164)
                };
            }
            else
            {
                return new FluentTheme
                {
                    IsDark = false,
                    Canvas = Color.FromArgb(243, 243, 243),
                    Card = Color.FromArgb(251, 251, 251),
                    CardHover = Color.FromArgb(245, 245, 245),
                    CardBorder = Color.FromArgb(228, 228, 228),
                    Divider = Color.FromArgb(226, 226, 226),
                    TextPrimary = Color.FromArgb(26, 26, 26),
                    TextSecondary = Color.FromArgb(94, 94, 94),
                    TextDisabled = Color.FromArgb(155, 155, 155),
                    Accent = Color.FromArgb(0, 103, 192),
                    AccentHover = Color.FromArgb(24, 118, 200),
                    TextOnAccent = Color.FromArgb(255, 255, 255),
                    ControlBg = Color.FromArgb(249, 249, 249),
                    ControlHover = Color.FromArgb(241, 241, 241),
                    ControlBorder = Color.FromArgb(222, 222, 222),
                    ControlBorderHover = Color.FromArgb(196, 196, 196),
                    DropdownPopupBg = Color.FromArgb(252, 252, 252),
                    ScrollTrack = Color.FromArgb(243, 243, 243),
                    ScrollThumb = Color.FromArgb(202, 202, 202),
                    ScrollThumbHover = Color.FromArgb(156, 156, 156),
                    ToggleOffBorder = Color.FromArgb(132, 132, 132),
                    ToggleOffThumb = Color.FromArgb(94, 94, 94),
                    SliderTrack = Color.FromArgb(174, 174, 174),
                    Success = Color.FromArgb(15, 123, 15),
                    Danger = Color.FromArgb(196, 43, 28)
                };
            }
        }

        public static GraphicsPath RoundedRect(RectangleF r, float radius)
        {
            float d = radius * 2f;
            if (d > r.Width) d = r.Width;
            if (d > r.Height) d = r.Height;
            if (d < 1f) d = 1f;
            var path = new GraphicsPath();
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        public static Font CreateUiFont(float pt, FontStyle style)
        {
            // Segoe UI / Malgun Gothic cleanly renders CJK + Latin on Windows 11
            return new Font("Malgun Gothic", pt, style);
        }
    }

    internal sealed class FluentStatusDot : Control
    {
        private Color _dotColor;
        private readonly float _scale;

        public Color DotColor
        {
            get { return _dotColor; }
            set { if (_dotColor != value) { _dotColor = value; Invalidate(); } }
        }

        public FluentStatusDot(Color backColor, Color dotColor, float scale)
        {
            _dotColor = dotColor;
            _scale = scale;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = backColor;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            float d = 10.0f * _scale;
            float x = (Width - d) * 0.5f;
            float y = (Height - d) * 0.5f;
            using (var br = new SolidBrush(_dotColor))
            {
                g.FillEllipse(br, x, y, d, d);
            }
        }
    }

    internal sealed class FluentCard : Panel
    {
        private readonly FluentTheme _t;
        private readonly float _radius;

        public FluentCard(FluentTheme theme, float radius)
        {
            _t = theme;
            _radius = radius;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = theme.Canvas;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            using (var path = FluentTheme.RoundedRect(r, _radius))
            using (var br = new SolidBrush(_t.Card))
            using (var pen = new Pen(_t.CardBorder, 1f))
            {
                e.Graphics.FillPath(br, path);
                e.Graphics.DrawPath(pen, path);
            }
        }
    }

    internal sealed class FluentToggle : Control
    {
        private readonly FluentTheme _t;
        private readonly float _scale;
        private bool _checked;
        private bool _hover;
        private bool _showText = true;

        public event EventHandler CheckedChanged;

        public bool ShowText
        {
            get { return _showText; }
            set { _showText = value; Invalidate(); }
        }

        public bool Checked
        {
            get { return _checked; }
            set
            {
                if (_checked == value) return;
                _checked = value;
                Invalidate();
                var h = CheckedChanged;
                if (h != null) h(this, EventArgs.Empty);
            }
        }

        public FluentToggle(FluentTheme theme, float scale)
        {
            _t = theme;
            _scale = scale;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Selectable, true);
            Cursor = Cursors.Hand;
            BackColor = theme.Card;
            Size = new Size((int)Math.Round(90 * scale), (int)Math.Round(24 * scale));
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnClick(EventArgs e) { if (Enabled) Checked = !Checked; base.OnClick(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            float trackW = 40f * _scale;
            float trackH = 20f * _scale;
            float trackX = Width - trackW - 1f;
            float trackY = (Height - trackH) * 0.5f;

            if (_showText)
            {
                string txt = _checked ? L10n.T("on") : L10n.T("off");
                var textRect = new RectangleF(0, 0, trackX - 6f * _scale, Height);
                using (var sf = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center })
                using (var brTxt = new SolidBrush(Enabled ? _t.TextPrimary : _t.TextDisabled))
                {
                    g.DrawString(txt, Font, brTxt, textRect, sf);
                }
            }

            var tr = new RectangleF(trackX, trackY, trackW, trackH);
            using (var path = FluentTheme.RoundedRect(tr, trackH * 0.5f))
            {
                if (_checked)
                {
                    Color fill = Enabled ? (_hover ? _t.AccentHover : _t.Accent) : _t.TextDisabled;
                    using (var br = new SolidBrush(fill)) g.FillPath(br, path);
                }
                else
                {
                    if (_hover && Enabled)
                    {
                        using (var br = new SolidBrush(_t.ControlHover)) g.FillPath(br, path);
                    }
                    using (var pen = new Pen(Enabled ? _t.ToggleOffBorder : _t.TextDisabled, 1.2f * _scale)) g.DrawPath(pen, path);
                }
            }

            float thumbD = (_hover && Enabled ? 13.5f : 12.0f) * _scale;
            float margin = (trackH - thumbD) * 0.5f;
            float thumbX = _checked ? (tr.Right - thumbD - margin) : (tr.X + margin);
            float thumbY = tr.Y + margin;
            using (var brThumb = new SolidBrush(_checked ? _t.TextOnAccent : (Enabled ? _t.ToggleOffThumb : _t.TextDisabled)))
            {
                g.FillEllipse(brThumb, thumbX, thumbY, thumbD, thumbD);
            }
        }
    }

    internal sealed class FluentSlider : Control
    {
        private readonly FluentTheme _t;
        private readonly float _scale;
        private int _min, _max, _val;
        private bool _hover, _dragging;

        public event EventHandler ValueChanged;

        public int Minimum { get { return _min; } set { _min = value; Invalidate(); } }
        public int Maximum { get { return _max; } set { _max = value; Invalidate(); } }
        public int Value
        {
            get { return _val; }
            set
            {
                int v = value < _min ? _min : (value > _max ? _max : value);
                if (_val == v) return;
                _val = v;
                Invalidate();
                var h = ValueChanged;
                if (h != null) h(this, EventArgs.Empty);
            }
        }

        public FluentSlider(FluentTheme theme, float scale, int min, int max)
        {
            _t = theme;
            _scale = scale;
            _min = min;
            _max = max;
            _val = min;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Selectable, true);
            Cursor = Cursors.Hand;
            BackColor = theme.Card;
        }

        private void SetFromX(int mouseX)
        {
            if (!Enabled) return;
            float pad = 10f * _scale;
            float usable = Math.Max(1f, Width - pad * 2f);
            float frac = (mouseX - pad) / usable;
            if (frac < 0f) frac = 0f;
            if (frac > 1f) frac = 1f;
            Value = _min + (int)Math.Round(frac * (_max - _min));
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && Enabled)
            {
                _dragging = true;
                Capture = true;
                SetFromX(e.X);
                Invalidate();
            }
            base.OnMouseDown(e);
        }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (_dragging) SetFromX(e.X);
            base.OnMouseMove(e);
        }
        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (_dragging) { _dragging = false; Capture = false; Invalidate(); }
            base.OnMouseUp(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            float pad = 10f * _scale;
            float trackH = 4f * _scale;
            float cy = Height * 0.5f;
            float trackW = Math.Max(1f, Width - pad * 2f);
            float frac = (_max > _min) ? ((float)(_val - _min) / (_max - _min)) : 0f;
            float thumbX = pad + frac * trackW;

            var bgRect = new RectangleF(pad, cy - trackH * 0.5f, trackW, trackH);
            using (var p = FluentTheme.RoundedRect(bgRect, trackH * 0.5f))
            using (var br = new SolidBrush(_t.SliderTrack))
                g.FillPath(br, p);

            if (frac > 0.001f)
            {
                var fgRect = new RectangleF(pad, cy - trackH * 0.5f, Math.Max(trackH, frac * trackW), trackH);
                using (var p = FluentTheme.RoundedRect(fgRect, trackH * 0.5f))
                using (var br = new SolidBrush(Enabled ? _t.Accent : _t.TextDisabled))
                    g.FillPath(br, p);
            }

            float outerD = 18f * _scale;
            float innerD = (_dragging ? 9f : (_hover ? 12.5f : 10.5f)) * _scale;
            var outerR = new RectangleF(thumbX - outerD * 0.5f, cy - outerD * 0.5f, outerD, outerD);
            var innerR = new RectangleF(thumbX - innerD * 0.5f, cy - innerD * 0.5f, innerD, innerD);

            using (var brOut = new SolidBrush(_t.IsDark ? Color.FromArgb(69, 69, 69) : Color.White))
            using (var penOut = new Pen(_t.ControlBorder, 1f))
            {
                g.FillEllipse(brOut, outerR);
                g.DrawEllipse(penOut, outerR);
            }
            using (var brIn = new SolidBrush(Enabled ? (_hover ? _t.AccentHover : _t.Accent) : _t.TextDisabled))
            {
                g.FillEllipse(brIn, innerR);
            }
        }
    }

    internal sealed class FluentButton : Control
    {
        private readonly FluentTheme _t;
        private readonly float _scale;
        private bool _primary;
        private bool _danger;
        private bool _hover, _down;

        public bool Primary
        {
            get { return _primary; }
            set { _primary = value; Invalidate(); }
        }

        public bool Danger
        {
            get { return _danger; }
            set { _danger = value; Invalidate(); }
        }

        public FluentButton(FluentTheme theme, float scale, bool primary)
        {
            _t = theme;
            _scale = scale;
            _primary = primary;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Selectable, true);
            Cursor = Cursors.Hand;
            BackColor = theme.Card;
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; _down = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left && Enabled) { _down = true; Invalidate(); } base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { _down = false; Invalidate(); base.OnMouseUp(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            using (var path = FluentTheme.RoundedRect(r, 5f * _scale))
            {
                Color bg, fg, bd;
                if (!Enabled)
                {
                    bg = _t.ControlBg; fg = _t.TextDisabled; bd = _t.CardBorder;
                }
                else if (_primary)
                {
                    bg = _down ? _t.Accent : (_hover ? _t.AccentHover : _t.Accent);
                    fg = _t.TextOnAccent;
                    bd = bg;
                }
                else if (_danger)
                {
                    bg = _down ? _t.Card : (_hover ? _t.ControlHover : _t.ControlBg);
                    fg = _t.Danger;
                    bd = _hover ? _t.Danger : _t.ControlBorder;
                }
                else
                {
                    bg = _down ? _t.Card : (_hover ? _t.ControlHover : _t.ControlBg);
                    fg = _t.TextPrimary;
                    bd = _hover ? _t.ControlBorderHover : _t.ControlBorder;
                }

                using (var br = new SolidBrush(bg)) g.FillPath(br, path);
                using (var pen = new Pen(bd, 1f)) g.DrawPath(pen, path);
                using (var brTxt = new SolidBrush(fg))
                using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                {
                    g.DrawString(Text, Font, brTxt, r, sf);
                }
            }
        }
    }

    // 100% custom-drawn Fluent dropdown (zero ugly Win32 gray ComboBox border!)
    internal sealed class FluentDropdown : Control
    {
        private readonly FluentTheme _t;
        private readonly float _scale;
        private string[] _items = new string[0];
        private int _selectedIndex = -1;
        private bool _hover;
        private bool _isOpen;
        private int _lastClosedTick;
        private FluentDropdownPopup _popup;

        public event EventHandler SelectedIndexChanged;

        public int SelectedIndex
        {
            get { return _selectedIndex; }
            set
            {
                int v = value;
                if (_items.Length == 0) v = -1;
                else if (v < 0) v = 0;
                else if (v >= _items.Length) v = _items.Length - 1;
                if (_selectedIndex == v) return;
                _selectedIndex = v;
                Invalidate();
                var h = SelectedIndexChanged;
                if (h != null) h(this, EventArgs.Empty);
            }
        }

        public FluentDropdown(FluentTheme theme, float scale)
        {
            _t = theme;
            _scale = scale;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Selectable, true);
            Cursor = Cursors.Hand;
            BackColor = theme.Card;
        }

        public void SetItems(string[] items, int selectedIndex)
        {
            _items = items ?? new string[0];
            if (_items.Length == 0) _selectedIndex = -1;
            else if (selectedIndex < 0) _selectedIndex = 0;
            else if (selectedIndex >= _items.Length) _selectedIndex = _items.Length - 1;
            else _selectedIndex = selectedIndex;
            Invalidate();
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && Enabled && _items.Length > 0)
            {
                if (_isOpen)
                {
                    ClosePopup();
                }
                else if (unchecked(Environment.TickCount - _lastClosedTick) > 160)
                {
                    OpenPopup();
                }
            }
            base.OnMouseDown(e);
        }

        private void OpenPopup()
        {
            ClosePopup();
            _isOpen = true;
            Invalidate();

            int itemH = (int)Math.Round(30 * _scale);
            int padV = (int)Math.Round(4 * _scale);
            int popupH = _items.Length * itemH + padV * 2;
            int popupW = Width;

            Point screenPt = PointToScreen(new Point(0, Height + (int)Math.Round(3 * _scale)));
            Rectangle work = Screen.FromControl(this).WorkingArea;
            if (screenPt.Y + popupH > work.Bottom)
            {
                screenPt = PointToScreen(new Point(0, -popupH - (int)Math.Round(3 * _scale)));
            }

            _popup = new FluentDropdownPopup(_t, _scale, _items, _selectedIndex, Font, itemH, padV);
            _popup.Location = screenPt;
            _popup.Size = new Size(popupW, popupH);
            _popup.ItemChosen += idx =>
            {
                ClosePopup();
                SelectedIndex = idx;
            };
            _popup.FormClosed += (s, e) =>
            {
                _isOpen = false;
                _lastClosedTick = Environment.TickCount;
                _popup = null;
                if (!IsDisposed) Invalidate();
            };

            Form owner = FindForm();
            if (owner != null) _popup.Show(owner);
            else _popup.Show();
        }

        private void ClosePopup()
        {
            if (_popup != null)
            {
                var p = _popup;
                _popup = null;
                try { if (!p.IsDisposed) p.Close(); } catch { }
            }
            _isOpen = false;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) ClosePopup();
            base.Dispose(disposing);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            using (var path = FluentTheme.RoundedRect(r, 5f * _scale))
            {
                Color bg = _isOpen ? _t.ControlHover : (_hover ? _t.ControlHover : _t.ControlBg);
                Color bd = _isOpen ? _t.Accent : (_hover ? _t.ControlBorderHover : _t.ControlBorder);

                using (var br = new SolidBrush(bg)) g.FillPath(br, path);
                using (var pen = new Pen(bd, 1f)) g.DrawPath(pen, path);
            }

            string text = (_selectedIndex >= 0 && _selectedIndex < _items.Length) ? _items[_selectedIndex] : "";
            float padL = 10f * _scale;
            float chevArea = 24f * _scale;
            var tr = new RectangleF(padL, 0, Math.Max(1f, Width - padL - chevArea), Height);
            using (var brTxt = new SolidBrush(Enabled ? _t.TextPrimary : _t.TextDisabled))
            using (var sf = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
            {
                g.DrawString(text, Font, brTxt, tr, sf);
            }

            // Crisp Fluent chevron
            float cx = Width - 13f * _scale;
            float cy = Height * 0.5f;
            float cw = 3.6f * _scale;
            float ch = 2.0f * _scale;
            using (var penChev = new Pen(Enabled ? _t.TextSecondary : _t.TextDisabled, 1.35f * _scale))
            {
                penChev.StartCap = LineCap.Round;
                penChev.EndCap = LineCap.Round;
                penChev.LineJoin = LineJoin.Round;
                if (_isOpen)
                {
                    g.DrawLines(penChev, new[]
                    {
                        new PointF(cx - cw, cy + ch),
                        new PointF(cx, cy - ch),
                        new PointF(cx + cw, cy + ch)
                    });
                }
                else
                {
                    g.DrawLines(penChev, new[]
                    {
                        new PointF(cx - cw, cy - ch),
                        new PointF(cx, cy + ch),
                        new PointF(cx + cw, cy - ch)
                    });
                }
            }
        }
    }

    internal sealed class FluentDropdownPopup : Form
    {
        private readonly FluentTheme _t;
        private readonly float _scale;
        private readonly string[] _items;
        private readonly int _selectedIndex;
        private readonly int _itemH;
        private readonly int _padV;
        private int _hoverIndex = -1;

        public event Action<int> ItemChosen;

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ClassStyle |= 0x00020000; // CS_DROPSHADOW
                return cp;
            }
        }

        public FluentDropdownPopup(FluentTheme theme, float scale, string[] items, int selectedIndex, Font font, int itemH, int padV)
        {
            _t = theme;
            _scale = scale;
            _items = items;
            _selectedIndex = selectedIndex;
            _itemH = itemH;
            _padV = padV;

            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = true;
            DoubleBuffered = true;
            BackColor = theme.DropdownPopupBg;
            Font = font;
            Cursor = Cursors.Hand;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            FluentTheme.ApplyWindowFrame(Handle, _t.IsDark, true);
        }

        protected override void OnDeactivate(EventArgs e)
        {
            base.OnDeactivate(e);
            try { Close(); } catch { }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                Close();
                return;
            }
            base.OnKeyDown(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int idx = (e.Y - _padV) / _itemH;
            if (idx < 0 || idx >= _items.Length) idx = -1;
            if (idx != _hoverIndex)
            {
                _hoverIndex = idx;
                Invalidate();
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            if (_hoverIndex != -1) { _hoverIndex = -1; Invalidate(); }
            base.OnMouseLeave(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                int idx = (e.Y - _padV) / _itemH;
                if (idx >= 0 && idx < _items.Length)
                {
                    var h = ItemChosen;
                    if (h != null) h(idx);
                }
            }
            base.OnMouseUp(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            using (var brBg = new SolidBrush(_t.DropdownPopupBg))
                g.FillRectangle(brBg, ClientRectangle);

            using (var penBd = new Pen(_t.ControlBorder, 1f))
                g.DrawRectangle(penBd, 0, 0, Width - 1, Height - 1);

            float padH = 4f * _scale;
            for (int i = 0; i < _items.Length; i++)
            {
                float y = _padV + i * _itemH;
                var itemR = new RectangleF(padH, y + 1f, Width - padH * 2f, _itemH - 2f);

                bool isSel = (i == _selectedIndex);
                bool isHov = (i == _hoverIndex);

                if (isHov || isSel)
                {
                    Color fill = isHov ? _t.ControlHover : Color.FromArgb(_t.IsDark ? 52 : 238, _t.IsDark ? 52 : 238, _t.IsDark ? 52 : 238);
                    using (var path = FluentTheme.RoundedRect(itemR, 4f * _scale))
                    using (var br = new SolidBrush(fill))
                        g.FillPath(br, path);
                }

                if (isSel)
                {
                    float barH = _itemH * 0.48f;
                    var barR = new RectangleF(itemR.X + 2f * _scale, y + (_itemH - barH) * 0.5f, 3f * _scale, barH);
                    using (var bp = FluentTheme.RoundedRect(barR, 1.5f * _scale))
                    using (var brAcc = new SolidBrush(_t.Accent))
                        g.FillPath(brAcc, bp);
                }

                var txtR = new RectangleF(itemR.X + 10f * _scale, y, itemR.Width - 14f * _scale, _itemH);
                using (var brTxt = new SolidBrush(_t.TextPrimary))
                using (var sf = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
                {
                    g.DrawString(_items[i], Font, brTxt, txtR, sf);
                }
            }
        }
    }

    // Custom Fluent Scrollable Container — completely eliminates ugly Win32 gray scrollbar!
    internal sealed class FluentScrollPanel : Panel, IMessageFilter
    {
        private readonly FluentTheme _t;
        private readonly float _scale;
        private readonly Panel _content;
        private int _contentHeight;
        private int _scrollY;
        private bool _hoverBar;
        private bool _draggingBar;
        private int _dragStartMouseY;
        private int _dragStartScrollY;
        private bool _filterAdded;

        private int BarWidth { get { return (int)Math.Round(14 * _scale); } }
        public Panel Content { get { return _content; } }

        public FluentScrollPanel(FluentTheme theme, float scale)
        {
            _t = theme;
            _scale = scale;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = theme.Canvas;
            AutoScroll = false;

            _content = new Panel
            {
                Location = new Point(0, 0),
                BackColor = theme.Canvas
            };
            Controls.Add(_content);
        }

        public void SetContentHeight(int heightPx)
        {
            _contentHeight = Math.Max(heightPx, Height);
            _content.Size = new Size(Math.Max(10, Width - BarWidth), _contentHeight);
            SetScrollY(_scrollY);
            Invalidate();
        }

        private int MaxScroll
        {
            get { return Math.Max(0, _contentHeight - Height); }
        }

        private void SetScrollY(int val)
        {
            int max = MaxScroll;
            if (val < 0) val = 0;
            if (val > max) val = max;
            if (_scrollY != val)
            {
                _scrollY = val;
                _content.Top = -_scrollY;
                Invalidate(new Rectangle(Width - BarWidth - 2, 0, BarWidth + 2, Height));
            }
            else
            {
                _content.Top = -_scrollY;
            }
        }

        protected override void OnResize(EventArgs eventargs)
        {
            base.OnResize(eventargs);
            _content.Width = Math.Max(10, Width - BarWidth);
            SetScrollY(_scrollY);
            Invalidate();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (!_filterAdded)
            {
                Application.AddMessageFilter(this);
                _filterAdded = true;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _filterAdded)
            {
                Application.RemoveMessageFilter(this);
                _filterAdded = false;
            }
            base.Dispose(disposing);
        }

        public bool PreFilterMessage(ref Message m)
        {
            if (m.Msg == 0x020A && Visible && IsHandleCreated && !IsDisposed) // WM_MOUSEWHEEL
            {
                Form f = FindForm();
                if (f != null && Form.ActiveForm == f)
                {
                    Point pt = Cursor.Position;
                    if (RectangleToScreen(ClientRectangle).Contains(pt))
                    {
                        int delta = (short)((m.WParam.ToInt64() >> 16) & 0xFFFF);
                        int step = (int)Math.Round(46 * _scale);
                        SetScrollY(_scrollY - (delta / 120) * step);
                        return true;
                    }
                }
            }
            return false;
        }

        private RectangleF GetThumbRect()
        {
            int max = MaxScroll;
            if (max <= 0 || _contentHeight <= 0) return RectangleF.Empty;

            float padV = 6f * _scale;
            float trackH = Math.Max(20f, Height - padV * 2f);
            float thumbH = Math.Max(32f * _scale, trackH * ((float)Height / _contentHeight));
            if (thumbH > trackH) thumbH = trackH;

            float frac = (float)_scrollY / max;
            float thumbY = padV + frac * (trackH - thumbH);

            float thumbW = (_hoverBar || _draggingBar) ? (6.5f * _scale) : (4.5f * _scale);
            float thumbX = Width - (BarWidth + thumbW) * 0.5f - 1f * _scale;
            return new RectangleF(thumbX, thumbY, thumbW, thumbH);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (_draggingBar)
            {
                int max = MaxScroll;
                if (max > 0)
                {
                    float padV = 6f * _scale;
                    float trackH = Math.Max(20f, Height - padV * 2f);
                    float thumbH = Math.Max(32f * _scale, trackH * ((float)Height / _contentHeight));
                    float travel = Math.Max(1f, trackH - thumbH);
                    int dy = e.Y - _dragStartMouseY;
                    SetScrollY(_dragStartScrollY + (int)Math.Round(dy / travel * max));
                }
            }
            else
            {
                bool over = (e.X >= Width - BarWidth - 2) && MaxScroll > 0;
                if (over != _hoverBar)
                {
                    _hoverBar = over;
                    Invalidate(new Rectangle(Width - BarWidth - 2, 0, BarWidth + 2, Height));
                }
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            if (_hoverBar && !_draggingBar)
            {
                _hoverBar = false;
                Invalidate(new Rectangle(Width - BarWidth - 2, 0, BarWidth + 2, Height));
            }
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && e.X >= Width - BarWidth - 2 && MaxScroll > 0)
            {
                var thumb = GetThumbRect();
                if (e.Y >= thumb.Top - 4 && e.Y <= thumb.Bottom + 4)
                {
                    _draggingBar = true;
                    _dragStartMouseY = e.Y;
                    _dragStartScrollY = _scrollY;
                    Capture = true;
                }
                else
                {
                    // Jump proportionally to clicked track position
                    float padV = 6f * _scale;
                    float frac = (e.Y - padV) / Math.Max(1f, Height - padV * 2f);
                    SetScrollY((int)Math.Round(frac * MaxScroll));
                }
                Invalidate(new Rectangle(Width - BarWidth - 2, 0, BarWidth + 2, Height));
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (_draggingBar)
            {
                _draggingBar = false;
                Capture = false;
                Invalidate(new Rectangle(Width - BarWidth - 2, 0, BarWidth + 2, Height));
            }
            base.OnMouseUp(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Track matches canvas seamlessly!
            using (var brTrack = new SolidBrush(_t.ScrollTrack))
                g.FillRectangle(brTrack, Width - BarWidth - 2, 0, BarWidth + 2, Height);

            if (MaxScroll <= 0) return;

            var thumb = GetThumbRect();
            if (thumb.IsEmpty) return;

            Color c = (_draggingBar || _hoverBar) ? _t.ScrollThumbHover : _t.ScrollThumb;
            using (var path = FluentTheme.RoundedRect(thumb, thumb.Width * 0.5f))
            using (var brThumb = new SolidBrush(c))
            {
                g.FillPath(brThumb, path);
            }
        }
    }
}
