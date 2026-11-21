using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace UmcVolume
{
    sealed class Settings
    {
        public double StepPercent = 2.0;
        public string DeviceMatch = "";   // optional extra match for non-Behringer devices
        public string Language = "auto";
        public double UiScale;   // 0 = follow Windows; e.g. 1.5 previews a 150 % display (testing)

        string path;

        // Per-user data folder: settings, volume state and log. Works even when the exe
        // is installed somewhere read-only such as Program Files.
        public static string DataDir
        {
            get
            {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "UmcVolume");
                Directory.CreateDirectory(dir);
                return dir;
            }
        }

        public static Settings Load()
        {
            var s = new Settings();
            s.path = Path.Combine(DataDir, "settings.ini");

            // First run after the move to %APPDATA%: pick up the old settings next to the exe.
            string source = s.path;
            if (!File.Exists(source))
                source = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.ini");

            if (File.Exists(source))
            {
                foreach (string raw in File.ReadAllLines(source))
                {
                    string line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith("#") || line.StartsWith(";")) continue;
                    int eq = line.IndexOf('=');
                    if (eq < 0) continue;
                    string k = line.Substring(0, eq).Trim();
                    string v = line.Substring(eq + 1).Trim();
                    double d;
                    if (Is(k, "StepPercent") && TryDouble(v, out d) && d > 0 && d <= 50) s.StepPercent = d;
                    else if (Is(k, "DeviceMatch") && v.Length > 0) s.DeviceMatch = v;
                    else if (Is(k, "Language") && v.Length > 0) s.Language = v.ToLowerInvariant();
                    else if (Is(k, "UiScale") && TryDouble(v, out d) && d >= 0.5 && d <= 4) s.UiScale = d;
                }
            }

            if (source != s.path || !File.ReadAllText(s.path).Contains("Language=")) s.Save();
            return s;
        }

        public void Save()
        {
            var sb = new StringBuilder();
            sb.Append("# UmcVolume ayarlari. Uygulama penceresinden de degistirilebilir.\r\n\r\n");
            sb.Append("# Her tusa basista degisecek miktar (yuzde). Ondalik olabilir, ornek: 1 veya 0.5\r\n");
            sb.Append("StepPercent=" + StepPercent.ToString("0.##", CultureInfo.InvariantCulture) + "\r\n\r\n");
            sb.Append("# Behringer cihazlari otomatik bulunur. Baska bir cihaz icin adinda aranacak metin (bos birakilabilir)\r\n");
            sb.Append("DeviceMatch=" + DeviceMatch + "\r\n\r\n");
            sb.Append("# Arayuz dili: auto (Windows dili), tr veya en\r\n");
            sb.Append("Language=" + Language + "\r\n");
            if (UiScale > 0)
            {
                sb.Append("\r\n# Test: arayuz olcegini zorla (bos = Windows ayari)\r\n");
                sb.Append("UiScale=" + UiScale.ToString("0.##", CultureInfo.InvariantCulture) + "\r\n");
            }
            try { File.WriteAllText(path, sb.ToString()); }
            catch (Exception ex) { Program.Log("Settings save failed: " + ex.Message); }
        }

        static bool Is(string a, string b)
        {
            return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        }

        // Accepts both "1.5" and "1,5" regardless of the Windows locale.
        public static bool TryDouble(string v, out double d)
        {
            return double.TryParse(v.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out d);
        }
    }
}
