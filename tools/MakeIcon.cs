using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

// Generates assets\app.ico (16..256 px, PNG-compressed entries): a speaker glyph on a rounded
// accent-coloured tile. Run by build.cmd only when the icon is missing.
static class MakeIcon
{
    static int Main(string[] args)
    {
        int[] sizes = { 16, 20, 24, 32, 40, 48, 64, 256 };
        var images = new List<byte[]>();
        foreach (int s in sizes) images.Add(Render(s));

        using (var fs = File.Create(args[0]))
        using (var w = new BinaryWriter(fs))
        {
            w.Write((short)0); w.Write((short)1); w.Write((short)sizes.Length);
            int offset = 6 + 16 * sizes.Length;
            for (int i = 0; i < sizes.Length; i++)
            {
                w.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i]));
                w.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i]));
                w.Write((byte)0); w.Write((byte)0);
                w.Write((short)1); w.Write((short)32);
                w.Write(images[i].Length);
                w.Write(offset);
                offset += images[i].Length;
            }
            foreach (byte[] img in images) w.Write(img);
        }
        Console.WriteLine("icon written: " + args[0]);
        return 0;
    }

    static byte[] Render(int size)
    {
        using (var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb))
        {
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                float k = size / 32f;

                // Rounded tile with a vertical accent gradient.
                var tile = new RectangleF(0.5f * k, 0.5f * k, 31f * k, 31f * k);
                using (var path = Round(tile, 7f * k))
                using (var bg = new LinearGradientBrush(tile, Color.FromArgb(94, 205, 255), Color.FromArgb(24, 120, 214), 90f))
                    g.FillPath(bg, path);

                // Speaker body.
                using (var white = new SolidBrush(Color.White))
                    g.FillPolygon(white, new[] {
                        new PointF(6f * k, 12.5f * k), new PointF(11f * k, 12.5f * k), new PointF(17f * k, 7f * k),
                        new PointF(17f * k, 25f * k), new PointF(11f * k, 19.5f * k), new PointF(6f * k, 19.5f * k) });

                // Sound waves (only where they stay legible).
                if (size >= 20)
                    using (var pen = new Pen(Color.White, Math.Max(1.4f, 2.2f * k)) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                    {
                        g.DrawArc(pen, 15.5f * k, 11.5f * k, 6f * k, 9f * k, -55, 110);
                        g.DrawArc(pen, 15f * k, 7.5f * k, 11f * k, 17f * k, -55, 110);
                    }
            }
            using (var ms = new MemoryStream())
            {
                bmp.Save(ms, ImageFormat.Png);
                return ms.ToArray();
            }
        }
    }

    static GraphicsPath Round(RectangleF r, float radius)
    {
        var p = new GraphicsPath();
        float d = radius * 2;
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }
}
