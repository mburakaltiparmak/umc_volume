using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace UmcVolume
{
    // Owner-drawn 0..100 slider in the same style as the on-screen indicator.
    sealed class VolumeSlider : Control
    {
        static int Pad { get { return Dpi.Px(12); } }   // room for the thumb at both ends
        static float TrackHeight { get { return Dpi.Px(6f); } }
        static float ThumbRadius { get { return Dpi.Px(9f); } }

        double value;
        bool dragging;
        bool hover;

        public event EventHandler ValueChanged; // raised for user changes only
        public double Step = 2.0;
        public bool Muted;

        public VolumeSlider()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.Selectable, true);
            TabStop = true;
            Height = Dpi.Px(32);
            Cursor = Cursors.Hand;
        }

        public double Value
        {
            get { return value; }
            set { this.value = Math.Max(0, Math.Min(100, value)); Invalidate(); }
        }

        void SetByUser(double v)
        {
            v = Math.Max(0, Math.Min(100, Math.Round(v, 1)));
            if (v == value && !Muted) return;
            value = v;
            Invalidate();
            if (ValueChanged != null) ValueChanged(this, EventArgs.Empty);
        }

        double FromX(int x)
        {
            int w = Math.Max(1, Width - 2 * Pad);
            return (x - Pad) * 100.0 / w;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            Focus();
            dragging = true;
            Capture = true;
            SetByUser(FromX(e.X));
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (dragging) SetByUser(FromX(e.X));
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            dragging = false;
            Capture = false;
            Invalidate();
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); hover = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hover = false; Invalidate(); }
        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            SetByUser(Snap(value + Math.Sign(e.Delta) * Step));
        }

        double Snap(double v)
        {
            return Math.Round(v / Step) * Step;
        }

        protected override bool IsInputKey(Keys keyData)
        {
            switch (keyData)
            {
                case Keys.Left: case Keys.Right: case Keys.Up: case Keys.Down: return true;
            }
            return base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            switch (e.KeyCode)
            {
                case Keys.Left: case Keys.Down: SetByUser(Snap(value - Step)); break;
                case Keys.Right: case Keys.Up: SetByUser(Snap(value + Step)); break;
                case Keys.PageDown: SetByUser(Snap(value - 10)); break;
                case Keys.PageUp: SetByUser(Snap(value + 10)); break;
                case Keys.Home: SetByUser(0); break;
                case Keys.End: SetByUser(100); break;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Surface);

            int w = Width - 2 * Pad;
            float cy = Height / 2f;
            var track = new RectangleF(Pad, cy - TrackHeight / 2f, w, TrackHeight);
            float x = Pad + (float)(w * value / 100.0);

            using (var b = new SolidBrush(Theme.Track)) FillRounded(g, b, track);
            Color fill = Muted ? Theme.TextDim : Theme.Accent;
            if (x > Pad)
                using (var b = new SolidBrush(fill))
                    FillRounded(g, b, new RectangleF(Pad, track.Y, x - Pad, TrackHeight));

            float r = ThumbRadius + ((dragging || hover) ? Dpi.Px(1f) : 0);
            using (var b = new SolidBrush(Theme.Thumb)) g.FillEllipse(b, x - r, cy - r, 2 * r, 2 * r);
            using (var b = new SolidBrush(fill)) g.FillEllipse(b, x - r / 2.2f, cy - r / 2.2f, r / 1.1f, r / 1.1f);

            if (Focused)
                using (var p = new Pen(Color.FromArgb(90, Theme.Accent), Dpi.Px(2f)))
                {
                    float o = Dpi.Px(3f);
                    g.DrawEllipse(p, x - r - o, cy - r - o, 2 * (r + o), 2 * (r + o));
                }
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
    }

}
