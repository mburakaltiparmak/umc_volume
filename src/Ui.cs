using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace UmcVolume
{
    static class Theme
    {
        public static readonly Color Background = Color.FromArgb(32, 32, 32);
        public static readonly Color Surface = Color.FromArgb(43, 43, 43);
        public static readonly Color Border = Color.FromArgb(56, 56, 56);
        public static readonly Color Divider = Color.FromArgb(52, 52, 52);
        public static readonly Color Control = Color.FromArgb(55, 55, 55);
        public static readonly Color ControlHover = Color.FromArgb(64, 64, 64);
        public static readonly Color ControlPress = Color.FromArgb(74, 74, 74);
        public static readonly Color Track = Color.FromArgb(88, 88, 88);
        public static readonly Color Thumb = Color.FromArgb(69, 69, 69);
        public static readonly Color Accent = Color.FromArgb(76, 194, 255);
        public static readonly Color OnAccent = Color.FromArgb(0, 0, 0);
        public static readonly Color Text = Color.FromArgb(255, 255, 255);
        public static readonly Color TextDim = Color.FromArgb(170, 170, 170);
        public static readonly Color TextFaint = Color.FromArgb(120, 120, 120);
        public static readonly Color Ok = Color.FromArgb(108, 203, 95);
        public static readonly Color Warn = Color.FromArgb(252, 185, 0);

        static string iconFamily;

        public static string IconFamily
        {
            get
            {
                if (iconFamily == null)
                {
                    using (var probe = new Font("Segoe Fluent Icons", 10f))
                        iconFamily = probe.Name == "Segoe Fluent Icons" ? "Segoe Fluent Icons" : "Segoe MDL2 Assets";
                }
                return iconFamily;
            }
        }

        public static Color Mix(Color a, Color b, float t)
        {
            t = Math.Max(0, Math.Min(1, t));
            return Color.FromArgb(
                (int)(a.A + (b.A - a.A) * t), (int)(a.R + (b.R - a.R) * t),
                (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
        }

        public static GraphicsPath Round(RectangleF r, float radius)
        {
            var p = new GraphicsPath();
            float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
            if (d <= 0) { p.AddRectangle(r); return p; }
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static void Smooth(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        }
    }

    // Base for owner-drawn controls: double buffered, paints the parent colour behind rounded shapes.
    abstract class DrawnControl : Control
    {
        protected bool Hover;

        protected DrawnControl()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.Selectable, true);
            // Every click counts: two quick clicks must toggle twice, not become a double-click.
            SetStyle(ControlStyles.StandardDoubleClick, false);
            Cursor = Cursors.Hand;
        }

        protected Color ParentColor
        {
            get { return Parent != null ? Parent.BackColor : Theme.Background; }
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); Hover = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); Hover = false; Invalidate(); }
        protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Invalidate(); }
        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }

        protected void DrawFocus(Graphics g, RectangleF r, float radius)
        {
            if (!Focused || !ShowFocusCues) return;
            using (var path = Theme.Round(RectangleF.Inflate(r, -1, -1), radius))
            using (var pen = new Pen(Color.FromArgb(160, Theme.Text), Dpi.Px(1.5f)))
                g.DrawPath(pen, path);
        }
    }

    // Rounded card with optional divider lines between rows.
    sealed class Card : Panel
    {
        public int Radius = Dpi.Px(8);
        public readonly List<int> Dividers = new List<int>();

        public Card()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
            BackColor = Theme.Surface;
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Background);
            Theme.Smooth(g);
            var r = new RectangleF(0.5f, 0.5f, Width - 1, Height - 1);
            using (var path = Theme.Round(r, Radius))
            {
                using (var b = new SolidBrush(BackColor)) g.FillPath(b, path);
                using (var p = new Pen(Theme.Border)) g.DrawPath(p, path);
            }
            using (var p = new Pen(Theme.Divider))
                foreach (int y in Dividers) g.DrawLine(p, Dpi.Px(16), y, Width - Dpi.Px(16), y);
        }
    }

    // Windows 11 style on/off switch with a short slide animation.
    sealed class ToggleSwitch : DrawnControl
    {
        public const int BaseWidth = 44, BaseHeight = 22; // at 96 DPI

        bool on;
        float pos;
        readonly Timer anim;

        public event EventHandler CheckedChanged;

        public ToggleSwitch()
        {
            Size = Dpi.Sz(BaseWidth, BaseHeight);
            anim = new Timer { Interval = 15 };
            anim.Tick += delegate
            {
                float target = on ? 1f : 0f;
                pos += (target - pos) * 0.35f;
                if (Math.Abs(target - pos) < 0.02f) { pos = target; anim.Stop(); }
                Invalidate();
            };
        }

        public bool Checked
        {
            get { return on; }
            set
            {
                if (on == value) return;
                on = value;
                anim.Start();
                if (CheckedChanged != null) CheckedChanged(this, EventArgs.Empty);
            }
        }

        // Update from code without animation or events.
        public void SetSilently(bool value)
        {
            on = value;
            pos = value ? 1f : 0f;
            Invalidate();
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            Focus();
            if (Enabled) Checked = !Checked;
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Space && Enabled) Checked = !Checked;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(ParentColor);
            Theme.Smooth(g);

            var r = new RectangleF(1, 1, Width - 2, Height - 2);
            float h = r.Height;
            Color offBorder = Hover ? Theme.Text : Theme.TextDim;
            Color fill = Theme.Mix(ParentColor, Theme.Accent, pos);
            if (!Enabled) fill = Theme.Mix(ParentColor, Theme.Control, pos);

            using (var path = Theme.Round(r, h / 2))
            {
                using (var b = new SolidBrush(fill)) g.FillPath(b, path);
                if (pos < 1)
                    using (var p = new Pen(Color.FromArgb((int)(255 * (1 - pos)), Enabled ? offBorder : Theme.TextFaint), Dpi.Px(1.2f)))
                        g.DrawPath(p, path);
            }

            float knob = Dpi.Px((Hover ? 7.5f : 6.5f) + pos * 0.8f);
            float cx = r.X + h / 2 + (r.Width - h) * pos;
            float cy = r.Y + h / 2;
            Color knobColor = Enabled ? Theme.Mix(offBorder, Theme.OnAccent, pos) : Theme.TextFaint;
            using (var b = new SolidBrush(knobColor)) g.FillEllipse(b, cx - knob, cy - knob, knob * 2, knob * 2);

            DrawFocus(g, new RectangleF(0, 0, Width, Height), Height / 2f);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) anim.Dispose();
            base.Dispose(disposing);
        }
    }

    // "−  value  +" control replacing NumericUpDown. Hold a button to repeat; mouse wheel works too.
    sealed class Stepper : DrawnControl
    {
        public double Minimum = 0, Maximum = 100, Increment = 1;
        public Func<double, string> Format = v => v.ToString();
        public event EventHandler ValueChanged;

        double value;
        int hoverPart;   // -1 minus, +1 plus, 0 middle
        int pressPart;
        readonly Timer repeat;

        public Stepper()
        {
            Size = Dpi.Sz(140, 32);
            repeat = new Timer();
            repeat.Tick += delegate { repeat.Interval = 70; Bump(pressPart); };
        }

        public double Value { get { return value; } }

        public void SetSilently(double v)
        {
            value = Math.Max(Minimum, Math.Min(Maximum, v));
            Invalidate();
        }

        void Bump(int dir)
        {
            if (dir == 0) return;
            double next = Math.Round((value + dir * Increment) / Increment) * Increment;
            next = Math.Max(Minimum, Math.Min(Maximum, next));
            if (next == value) { repeat.Stop(); return; }
            value = next;
            Invalidate();
            if (ValueChanged != null) ValueChanged(this, EventArgs.Empty);
        }

        int PartAt(int x)
        {
            if (x < Height) return -1;
            if (x > Width - Height) return 1;
            return 0;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int p = PartAt(e.X);
            if (p != hoverPart) { hoverPart = p; Invalidate(); }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            hoverPart = 0;
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();
            if (e.Button != MouseButtons.Left) return;
            pressPart = PartAt(e.X);
            if (pressPart == 0) return;
            Bump(pressPart);
            repeat.Interval = 400;
            repeat.Start();
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            repeat.Stop();
            pressPart = 0;
            Invalidate();
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            Bump(Math.Sign(e.Delta));
        }

        protected override bool IsInputKey(Keys k)
        {
            return k == Keys.Up || k == Keys.Down || k == Keys.Left || k == Keys.Right || base.IsInputKey(k);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Up || e.KeyCode == Keys.Right) Bump(1);
            if (e.KeyCode == Keys.Down || e.KeyCode == Keys.Left) Bump(-1);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(ParentColor);
            Theme.Smooth(g);

            var r = new RectangleF(0.5f, 0.5f, Width - 1, Height - 1);
            using (var path = Theme.Round(r, Dpi.Px(6)))
            {
                using (var b = new SolidBrush(Theme.Control)) g.FillPath(b, path);
                g.SetClip(path);
                PaintPart(g, -1, new RectangleF(0, 0, Height, Height));
                PaintPart(g, 1, new RectangleF(Width - Height, 0, Height, Height));
                g.ResetClip();
                using (var p = new Pen(Theme.Border)) g.DrawPath(p, path);
            }

            using (var p = new Pen(Theme.Border))
            {
                int inset = Dpi.Px(6);
                g.DrawLine(p, Height, inset, Height, Height - inset);
                g.DrawLine(p, Width - Height, inset, Width - Height, Height - inset);
            }

            DrawGlyph(g, "", new Rectangle(0, 0, Height, Height), value > Minimum);   // minus
            DrawGlyph(g, "", new Rectangle(Width - Height, 0, Height, Height), value < Maximum); // plus

            using (var f = Dpi.Font("Segoe UI Semibold", 9.75f))
                TextRenderer.DrawText(g, Format(value), f, new Rectangle(Height, 0, Width - 2 * Height, Height),
                    Theme.Text, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

            DrawFocus(g, new RectangleF(0, 0, Width, Height), Dpi.Px(6));
        }

        void PaintPart(Graphics g, int part, RectangleF r)
        {
            Color c = pressPart == part ? Theme.ControlPress : (hoverPart == part ? Theme.ControlHover : Color.Empty);
            if (c.IsEmpty) return;
            using (var b = new SolidBrush(c)) g.FillRectangle(b, r);
        }

        static void DrawGlyph(Graphics g, string glyph, Rectangle r, bool enabled)
        {
            using (var f = Dpi.Font(Theme.IconFamily, 9f))
                TextRenderer.DrawText(g, glyph, f, r, enabled ? Theme.Text : Theme.TextFaint,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) repeat.Dispose();
            base.Dispose(disposing);
        }
    }

    // Segmented control: a row of options, one selected (replaces a drop-down for short lists).
    sealed class Segmented : DrawnControl
    {
        readonly double[] values;
        readonly string[] labels;
        int selected = -1;
        int hover = -1;

        public event EventHandler SelectedChanged;

        public Segmented(double[] values, string[] labels)
        {
            this.values = values;
            this.labels = labels;
            Size = Dpi.Sz(200, 32);
        }

        public double Value { get { return selected >= 0 ? values[selected] : double.NaN; } }

        public void SetSilently(double v)
        {
            selected = -1;
            for (int i = 0; i < values.Length; i++)
                if (Math.Abs(values[i] - v) < 1e-6) selected = i;
            Invalidate();
        }

        int IndexAt(int x)
        {
            int i = x * values.Length / Math.Max(1, Width);
            return Math.Max(0, Math.Min(values.Length - 1, i));
        }

        void Select(int i)
        {
            if (i == selected || i < 0 || i >= values.Length) return;
            selected = i;
            Invalidate();
            if (SelectedChanged != null) SelectedChanged(this, EventArgs.Empty);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int i = IndexAt(e.X);
            if (i != hover) { hover = i; Invalidate(); }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            hover = -1;
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();
            if (e.Button == MouseButtons.Left) Select(IndexAt(e.X));
        }

        protected override bool IsInputKey(Keys k)
        {
            return k == Keys.Left || k == Keys.Right || base.IsInputKey(k);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Left) Select(Math.Max(0, selected - 1));
            if (e.KeyCode == Keys.Right) Select(Math.Min(values.Length - 1, selected + 1));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(ParentColor);
            Theme.Smooth(g);

            var r = new RectangleF(0.5f, 0.5f, Width - 1, Height - 1);
            using (var path = Theme.Round(r, Dpi.Px(6)))
            {
                using (var b = new SolidBrush(Theme.Control)) g.FillPath(b, path);
                using (var p = new Pen(Theme.Border)) g.DrawPath(p, path);
            }

            float w = (float)Width / values.Length;
            using (var normal = Dpi.Font("Segoe UI", 9.5f))
            using (var bold = Dpi.Font("Segoe UI Semibold", 9.5f))
            {
                for (int i = 0; i < values.Length; i++)
                {
                    float pad = Dpi.Px(3f);
                    var seg = new RectangleF(i * w + pad, pad, w - 2 * pad, Height - 2 * pad);
                    if (i == selected)
                    {
                        using (var path = Theme.Round(seg, Dpi.Px(4)))
                        using (var b = new SolidBrush(Theme.Accent)) g.FillPath(b, path);
                    }
                    else if (i == hover)
                    {
                        using (var path = Theme.Round(seg, Dpi.Px(4)))
                        using (var b = new SolidBrush(Theme.ControlHover)) g.FillPath(b, path);
                    }

                    TextRenderer.DrawText(g, labels[i], i == selected ? bold : normal,
                        Rectangle.Round(new RectangleF(i * w, 0, w, Height)),
                        i == selected ? Theme.OnAccent : Theme.Text,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                }
            }

            DrawFocus(g, new RectangleF(0, 0, Width, Height), Dpi.Px(6));
        }
    }

    // Circular button showing an icon glyph; "Active" fills it with the accent colour.
    sealed class RoundIconButton : DrawnControl
    {
        string glyph = "";
        bool active;
        bool pressed;

        public RoundIconButton()
        {
            Size = Dpi.Sz(44, 44);
        }

        public string Glyph { get { return glyph; } set { glyph = value; Invalidate(); } }
        public bool Active { get { return active; } set { active = value; Invalidate(); } }

        protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); pressed = true; Invalidate(); }
        protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); pressed = false; Invalidate(); }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter) OnClick(EventArgs.Empty);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(ParentColor);
            Theme.Smooth(g);

            Color fill = active
                ? (pressed ? Theme.Mix(Theme.Accent, Color.Black, 0.2f) : (Hover ? Theme.Mix(Theme.Accent, Color.White, 0.1f) : Theme.Accent))
                : (pressed ? Theme.ControlPress : (Hover ? Theme.ControlHover : Theme.Control));
            var r = new RectangleF(0.5f, 0.5f, Width - 1, Height - 1);
            using (var b = new SolidBrush(fill)) g.FillEllipse(b, r);
            if (!active)
                using (var p = new Pen(Theme.Border)) g.DrawEllipse(p, r);

            using (var f = Dpi.Font(Theme.IconFamily, 14f))
                TextRenderer.DrawText(g, glyph, f, new Rectangle(0, 0, Width, Height),
                    active ? Theme.OnAccent : Theme.Text,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

            if (Focused && ShowFocusCues)
                using (var p = new Pen(Color.FromArgb(160, Theme.Text), Dpi.Px(1.5f)))
                    g.DrawEllipse(p, RectangleF.Inflate(r, -Dpi.Px(2f), -Dpi.Px(2f)));
        }
    }
}
