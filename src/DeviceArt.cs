using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace UmcVolume
{
    // Hero card: product photo of the interface, model name and a status chip.
    sealed class DeviceArt : Control
    {
        static int Radius { get { return Dpi.Px(8); } }
        static int PhotoTop { get { return Dpi.Px(16); } }
        static int PhotoHeight { get { return Dpi.Px(130); } }

        // Processed photos per model ("UMC202HD" -> assets\umc202hd.png); null = no photo for it.
        static readonly Dictionary<string, Bitmap> photos = new Dictionary<string, Bitmap>(StringComparer.OrdinalIgnoreCase);

        Bitmap photo;
        bool active;
        bool connected;
        string title = "";
        string model = "";
        string subtitle = "";

        // Folder holding the product photos; defaults to "assets" next to the exe (tests override it).
        public static string AssetsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets");

        public DeviceArt()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            title = L.NoDevice; // until SetDevice reports a connected interface
        }

        public bool Active { get { return active; } set { if (active != value) { active = value; Invalidate(); } } }
        public string Subtitle { get { return subtitle; } set { subtitle = value ?? ""; Invalidate(); } }

        // Shows the given interface (its name and photo), or the "not connected" state for null.
        public void SetDevice(DeviceInfo device)
        {
            bool nowConnected = device != null;
            string nowModel = nowConnected ? device.Model : "";
            if (nowConnected == connected && nowModel == model) return;

            connected = nowConnected;
            model = nowModel;
            title = connected ? device.Title : L.NoDevice;
            photo = connected ? PhotoFor(model) : null;
            Invalidate();
        }

        // ---- photo loading ------------------------------------------------------

        static Bitmap PhotoFor(string model)
        {
            Bitmap bmp;
            if (photos.TryGetValue(model, out bmp)) return bmp;
            string file = Regex.Replace(model.ToLowerInvariant(), @"[^a-z0-9\-]", "") + ".png";
            bmp = LoadPhoto(Path.Combine(AssetsDir, file));
            photos[model] = bmp;
            return bmp;
        }

        static Bitmap LoadPhoto(string path)
        {
            if (!File.Exists(path)) return null;
            try
            {
                using (var src = (Bitmap)Image.FromFile(path))
                {
                    Bitmap trimmed = Trim(RemoveWhiteBackground(Downscale(src, 900)));
                    return trimmed;
                }
            }
            catch (Exception ex)
            {
                Program.Log("Photo load failed: " + ex.Message);
                return null;
            }
        }

        static Bitmap Downscale(Bitmap src, int maxWidth)
        {
            int w = Math.Min(maxWidth, src.Width);
            int h = (int)Math.Round(src.Height * (double)w / src.Width);
            var dst = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(dst))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.DrawImage(src, new Rectangle(0, 0, w, h));
            }
            return dst;
        }

        // Product shots come on white. Flood-fill from the borders through near-white pixels and
        // make them transparent, with a soft ramp so the edges are not jagged. White details inside
        // the device (labels, LEDs) are untouched because they are not connected to the border.
        static Bitmap RemoveWhiteBackground(Bitmap bmp)
        {
            int w = bmp.Width, h = bmp.Height;
            var rect = new Rectangle(0, 0, w, h);
            BitmapData data = bmp.LockBits(rect, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
            int stride = data.Stride;
            var px = new byte[stride * h];
            Marshal.Copy(data.Scan0, px, 0, px.Length);

            // Already transparent? Nothing to do.
            if (px[3] < 250)
            {
                bmp.UnlockBits(data);
                return bmp;
            }

            const int Hard = 246;  // brightness at/above this: fully transparent
            const int Soft = 218;  // between Soft and Hard: partially transparent edge
            var visited = new bool[w * h];
            var stack = new Stack<int>();
            for (int x = 0; x < w; x++) { stack.Push(x); stack.Push((h - 1) * w + x); }
            for (int y = 0; y < h; y++) { stack.Push(y * w); stack.Push(y * w + w - 1); }

            while (stack.Count > 0)
            {
                int i = stack.Pop();
                if (visited[i]) continue;
                visited[i] = true;
                int x = i % w, y = i / w;
                int o = y * stride + x * 4;
                int b = px[o], g = px[o + 1], r = px[o + 2];
                int min = Math.Min(r, Math.Min(g, b));
                int max = Math.Max(r, Math.Max(g, b));
                if (min < Soft || max - min > 24) continue; // not background: stop here

                int alpha = min >= Hard ? 0 : 255 * (Hard - min) / (Hard - Soft);
                px[o + 3] = (byte)Math.Min(px[o + 3], alpha);

                if (x > 0) stack.Push(i - 1);
                if (x < w - 1) stack.Push(i + 1);
                if (y > 0) stack.Push(i - w);
                if (y < h - 1) stack.Push(i + w);
            }

            Marshal.Copy(px, 0, data.Scan0, px.Length);
            bmp.UnlockBits(data);
            return bmp;
        }

        // Crop fully transparent margins so the device fills the space we give it.
        static Bitmap Trim(Bitmap bmp)
        {
            int w = bmp.Width, h = bmp.Height;
            BitmapData data = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            var px = new byte[data.Stride * h];
            Marshal.Copy(data.Scan0, px, 0, px.Length);
            int stride = data.Stride;
            bmp.UnlockBits(data);

            int left = w, right = -1, top = h, bottom = -1;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    if (px[y * stride + x * 4 + 3] > 16)
                    {
                        if (x < left) left = x;
                        if (x > right) right = x;
                        if (y < top) top = y;
                        if (y > bottom) bottom = y;
                    }
            if (right < left || bottom < top) return bmp;

            var crop = new Rectangle(left, top, right - left + 1, bottom - top + 1);
            Bitmap result = bmp.Clone(crop, PixelFormat.Format32bppArgb);
            bmp.Dispose();
            return result;
        }

        // ---- painting -----------------------------------------------------------

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Background);
            Theme.Smooth(g);

            var card = new RectangleF(0.5f, 0.5f, Width - 1, Height - 1);
            using (var path = Theme.Round(card, Radius))
            {
                using (var bg = new LinearGradientBrush(card, Color.FromArgb(52, 58, 66), Color.FromArgb(36, 38, 42), 90f))
                    g.FillPath(bg, path);

                g.SetClip(path);
                // Soft accent glow behind the device.
                var glow = new RectangleF(Width * 0.08f, PhotoTop - Dpi.Px(30), Width * 0.84f, PhotoHeight + Dpi.Px(60));
                using (var gp = new GraphicsPath())
                {
                    gp.AddEllipse(glow);
                    using (var pb = new PathGradientBrush(gp))
                    {
                        pb.CenterColor = Color.FromArgb(active ? 70 : 30, Theme.Accent);
                        pb.SurroundColors = new[] { Color.FromArgb(0, Theme.Accent) };
                        g.FillPath(pb, gp);
                    }
                }
                g.ResetClip();

                using (var p = new Pen(Theme.Border)) g.DrawPath(p, path);
            }

            if (photo != null) DrawPhoto(g);
            else DrawGeneric(g);

            DrawCaption(g);
        }

        void DrawPhoto(Graphics g)
        {
            float maxW = Width - Dpi.Px(28);
            float scale = Math.Min(maxW / photo.Width, PhotoHeight / (float)photo.Height);
            float w = photo.Width * scale, h = photo.Height * scale;
            float x = (Width - w) / 2f, y = PhotoTop + (PhotoHeight - h) / 2f;

            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.DrawImage(photo, x, y, w, h);
        }

        // No photo for this model (or nothing connected): a large speaker glyph in a soft disc.
        void DrawGeneric(Graphics g)
        {
            int d = Dpi.Px(96);
            var disc = new RectangleF((Width - d) / 2f, PhotoTop + (PhotoHeight - d) / 2f, d, d);
            using (var b = new SolidBrush(Color.FromArgb(connected ? 40 : 18, Theme.Accent))) g.FillEllipse(b, disc);
            using (var f = Dpi.Font(Theme.IconFamily, 34f))
                TextRenderer.DrawText(g, connected ? "" : "", f, Rectangle.Round(disc),
                    connected ? Theme.Accent : Theme.TextFaint,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }

        void DrawCaption(Graphics g)
        {
            int y = PhotoTop + PhotoHeight + Dpi.Px(12);
            using (var titleFont = Dpi.Font("Segoe UI Semibold", 13f))
            using (var sub = Dpi.Font("Segoe UI", 9f))
            {
                // Status chip on the right (measured first so the title can be ellipsised before it).
                string text = !connected ? L.ChipDisconnected : (active ? L.ChipActive : L.ChipInactive);
                Size ts = TextRenderer.MeasureText(text, sub, Size.Empty, TextFormatFlags.NoPadding);
                int chipW = ts.Width + Dpi.Px(30), chipH = Dpi.Px(24);
                var chip = new RectangleF(Width - chipW - Dpi.Px(18), y + Dpi.Px(6), chipW, chipH);

                var titleBox = new Rectangle(Dpi.Px(18), y, (int)chip.X - Dpi.Px(30), Dpi.Px(28));
                TextRenderer.DrawText(g, title, titleFont, titleBox, Theme.Text,
                    TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
                TextRenderer.DrawText(g, subtitle, sub, new Point(Dpi.Px(19), y + Dpi.Px(28)), Theme.TextDim, TextFormatFlags.NoPadding);

                Color c = !connected ? Theme.TextDim : (active ? Theme.Ok : Theme.Warn);
                using (var path = Theme.Round(chip, chipH / 2f))
                {
                    using (var b = new SolidBrush(Color.FromArgb(38, c))) g.FillPath(b, path);
                    using (var p = new Pen(Color.FromArgb(90, c))) g.DrawPath(p, path);
                }
                using (var b = new SolidBrush(c)) g.FillEllipse(b, chip.X + Dpi.Px(11f), chip.Y + chipH / 2f - Dpi.Px(3.5f), Dpi.Px(7f), Dpi.Px(7f));
                TextRenderer.DrawText(g, text, sub, new Point((int)chip.X + Dpi.Px(23), (int)(chip.Y + (chipH - ts.Height) / 2f)), c, TextFormatFlags.NoPadding);
            }
        }

    }
}
