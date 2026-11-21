using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace UmcVolume
{
    // Small on-screen volume indicator that never takes focus or clicks.
    sealed class OsdForm : Form
    {
        readonly Timer hideTimer;
        readonly Font iconFont;
        readonly Font textFont;
        double fraction;
        bool muted;
        string valueText = "";

        static readonly Color Back = Color.FromArgb(36, 36, 36);
        static readonly Color Track = Color.FromArgb(80, 80, 80);
        static readonly Color Accent = Color.FromArgb(76, 194, 255);
        static readonly Color MutedFill = Color.FromArgb(130, 130, 130);

        public OsdForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            ClientSize = Dpi.Sz(300, 58);
            BackColor = Back;
            DoubleBuffered = true;
            Opacity = 0.95;
            TopMost = true;

            iconFont = MakeIconFont();
            textFont = Dpi.Font("Segoe UI", 10f);

            hideTimer = new Timer { Interval = 1500 };
            hideTimer.Tick += delegate { hideTimer.Stop(); Hide(); };
        }

        static Font MakeIconFont()
        {
            using (var probe = new Font("Segoe Fluent Icons", 16f))
            {
                if (probe.Name == "Segoe Fluent Icons") return Dpi.Font("Segoe Fluent Icons", 16f);
            }
            return Dpi.Font("Segoe MDL2 Assets", 16f);
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override CreateParams CreateParams
        {
            get
            {
                const int WS_EX_TOPMOST = 0x00000008;
                const int WS_EX_TRANSPARENT = 0x00000020;
                const int WS_EX_TOOLWINDOW = 0x00000080;
                const int WS_EX_NOACTIVATE = 0x08000000;
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= WS_EX_TOPMOST | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
                return cp;
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try
            {
                int round = 2; // DWMWCP_ROUND (Windows 11)
                DwmSetWindowAttribute(Handle, 33, ref round, sizeof(int));
            }
            catch { }
        }

        public void ShowLevel(string text, double frac, bool isMuted)
        {
            fraction = Math.Max(0, Math.Min(1, frac));
            muted = isMuted;
            valueText = text;

            Rectangle wa = Screen.PrimaryScreen.WorkingArea;
            Location = new Point(wa.Left + (wa.Width - Width) / 2, wa.Bottom - Height - Dpi.Px(60));

            if (!Visible) Show();
            BringToFrontNoActivate();
            Invalidate();
            hideTimer.Stop();
            hideTimer.Start();
        }

        // Re-assert topmost z-order every time: a window shown after a long hide can sit
        // below other topmost/foreground windows (e.g. browsers), so push it to the top
        // of the topmost band without stealing focus.
        void BringToFrontNoActivate()
        {
            const uint SWP_NOSIZE = 0x0001, SWP_NOMOVE = 0x0002, SWP_NOACTIVATE = 0x0010,
                       SWP_SHOWWINDOW = 0x0040, SWP_NOOWNERZORDER = 0x0200;
            SetWindowPos(Handle, HWND_TOPMOST, 0, 0, 0, 0,
                SWP_NOSIZE | SWP_NOMOVE | SWP_NOACTIVATE | SWP_SHOWWINDOW | SWP_NOOWNERZORDER);
        }

        static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            string glyph = muted ? "" : (fraction < 0.34 ? "" : (fraction < 0.67 ? "" : ""));
            TextRenderer.DrawText(g, glyph, iconFont, new Rectangle(Dpi.Px(12), 0, Dpi.Px(36), Height), Color.White,
                TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPadding);

            int barLeft = Dpi.Px(58);
            int barRight = Dpi.Px(88); // space reserved for the value text
            int barWidth = Width - barLeft - barRight;
            float barH = Dpi.Px(4f);
            float barY = Height / 2f - barH / 2f;
            using (var track = new SolidBrush(Track))
                FillRounded(g, track, new RectangleF(barLeft, barY, barWidth, barH));
            float fill = (float)(barWidth * fraction);
            if (fill > 0)
                using (var acc = new SolidBrush(muted ? MutedFill : Accent))
                    FillRounded(g, acc, new RectangleF(barLeft, barY, fill, barH));

            TextRenderer.DrawText(g, valueText, textFont,
                new Rectangle(Width - barRight + Dpi.Px(6), 0, barRight - Dpi.Px(14), Height), Color.White,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Right | TextFormatFlags.NoPadding);
        }

        static void FillRounded(Graphics g, Brush b, RectangleF r)
        {
            float d = r.Height;
            if (r.Width < d) { g.FillEllipse(b, r.X, r.Y, d, d); return; }
            using (var path = new GraphicsPath())
            {
                path.AddArc(r.X, r.Y, d, d, 90, 180);
                path.AddArc(r.Right - d, r.Y, d, d, 270, 180);
                path.CloseFigure();
                g.FillPath(b, path);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                hideTimer.Dispose();
                iconFont.Dispose();
                textFont.Dispose();
            }
            base.Dispose(disposing);
        }

        [DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
    }
}
