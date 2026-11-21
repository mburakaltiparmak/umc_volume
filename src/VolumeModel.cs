using System;
using System.Globalization;
using System.IO;

namespace UmcVolume
{
    // Volume level (0..100 %) and mute state, with the loudness curve and persistence.
    sealed class VolumeModel
    {
        // Everyday (Windows-like) loudness curve: halving the percent halves perceived loudness,
        // i.e. -10 dB per halving. %100 = 0 dB (the source's own level), %50 = -10 dB, %25 = -20 dB.
        const double DbPerDecade = 10.0 / 0.30103; // ~33.2

        readonly Settings settings;
        readonly string statePath;

        public double Percent = 100.0;
        public bool Muted;
        public string DeviceId;   // full Core Audio endpoint id of the interface

        public VolumeModel(Settings s, string statePath)
        {
            settings = s;
            this.statePath = statePath;
            Load();
        }

        public double Fraction { get { return Percent / 100.0; } }

        public double LevelDb
        {
            get { return Percent <= 0 ? double.NegativeInfinity : DbPerDecade * Math.Log10(Percent / 100.0); }
        }

        // Linear gain handed to Windows (0..1).
        public float Scalar
        {
            get
            {
                if (Muted || Percent <= 0) return 0f;
                return (float)Math.Pow(10.0, LevelDb / 20.0);
            }
        }

        public void Step(int direction)
        {
            Muted = false;
            double step = settings.StepPercent;
            double next = Math.Round((Percent + direction * step) / step) * step;
            Percent = Clamp(Math.Round(next, 2));
        }

        public void SetPercent(double p)
        {
            Muted = false;
            Percent = Clamp(Math.Round(p, 2));
        }

        static double Clamp(double p)
        {
            return Math.Max(0.0, Math.Min(100.0, p));
        }

        void Load()
        {
            if (!File.Exists(statePath)) return;
            foreach (string raw in File.ReadAllLines(statePath))
            {
                int eq = raw.IndexOf('=');
                if (eq < 0) continue;
                string k = raw.Substring(0, eq).Trim(), v = raw.Substring(eq + 1).Trim();
                double d;
                if (k == "Percent" && Settings.TryDouble(v, out d)) Percent = Clamp(d);
                else if (k == "Muted") Muted = v == "1";
                else if (k == "Device" && v.Length > 0) DeviceId = v;
            }
        }

        public void Save()
        {
            string text = "Percent=" + Percent.ToString("0.##", CultureInfo.InvariantCulture) + "\r\n" +
                          "Muted=" + (Muted ? "1" : "0") + "\r\n" +
                          (DeviceId != null ? "Device=" + DeviceId + "\r\n" : "");
            try { File.WriteAllText(statePath, text); }
            catch (Exception ex) { Program.Log("State save failed: " + ex.Message); }
        }
    }
}
