using System;
using System.Drawing;
using System.Windows.Forms;

namespace UmcVolume
{
    // High-DPI helper. The app is system-DPI aware (app.manifest), so Windows no longer bitmap-
    // stretches it; every pixel constant in the UI is written for 96 DPI and passed through Px().
    // Fonts are in points and scale on their own. "UiScale" in settings.ini can force a scale
    // (e.g. 1.5) to preview a 150 % display on a 100 % one; fonts are adjusted to match.
    static class Dpi
    {
        static float system = 1f;   // real system scale (DPI / 96)
        static float scale = 1f;    // scale the UI is laid out for

        public static float Scale { get { return scale; } }

        public static void Init(double overrideScale)
        {
            using (var g = Graphics.FromHwnd(IntPtr.Zero))
                system = g.DpiX / 96f;
            scale = overrideScale > 0 ? (float)overrideScale : system;
        }

        public static int Px(int v) { return (int)Math.Round(v * scale); }
        public static float Px(float v) { return v * scale; }
        public static Point Pt(int x, int y) { return new Point(Px(x), Px(y)); }
        public static Size Sz(int w, int h) { return new Size(Px(w), Px(h)); }

        // Point sizes already follow the real DPI; only a forced UiScale needs a correction.
        public static Font Font(string family, float points, FontStyle style = FontStyle.Regular)
        {
            return new Font(family, points * scale / system, style);
        }
    }
}
