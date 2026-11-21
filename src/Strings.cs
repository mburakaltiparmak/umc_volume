using System;
using System.Globalization;

namespace UmcVolume
{
    // UI text in Turkish and English. The language follows Windows' display language unless
    // settings.ini says "Language=tr" or "Language=en".
    static class L
    {
        static bool tr = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "tr";

        public static CultureInfo Culture
        {
            get { return CultureInfo.GetCultureInfo(tr ? "tr-TR" : "en-US"); }
        }

        public static void Set(string language)
        {
            if (string.Equals(language, "tr", StringComparison.OrdinalIgnoreCase)) tr = true;
            else if (string.Equals(language, "en", StringComparison.OrdinalIgnoreCase)) tr = false;
            // anything else ("auto", empty): keep the Windows language
        }

        static string T(string turkish, string english) { return tr ? turkish : english; }

        // Turkish writes the percent sign first (%55, %12,5); English after (55%, 12.5%).
        public static string Percent(double value)
        {
            string n = value.ToString("0.#", Culture);
            return tr ? "%" + n : n + "%";
        }

        public static string Db(double db)
        {
            if (double.IsNegativeInfinity(db)) return Off;
            return (db < -0.05 ? "−" : "") + Math.Abs(db).ToString("0.0", Culture) + " dB";
        }

        public static string WindowTitle { get { return T("UMC Ses", "UMC Volume"); } }
        public static string Open { get { return T("Aç", "Open"); } }
        public static string Mute { get { return T("Sessiz", "Mute"); } }
        public static string Muted { get { return T("Sessiz", "Muted"); } }
        public static string Exit { get { return T("Çıkış", "Exit"); } }
        public static string Off { get { return T("kapalı", "off"); } }
        public static string LevelPrefix { get { return T("Seviye: ", "Level: "); } }
        public static string TrayInactive(string model) { return T(model + " varsayılan çıkış değil (tuşlar Windows'ta)", model + " is not the default output (keys go to Windows)"); }
        public static string NoDevice { get { return T("Behringer ses kartı bulunamadı", "No Behringer audio interface found"); } }
        public static string NoDeviceSub { get { return T("USB ses kartını bağlayın", "Connect your USB audio interface"); } }
        public static string ChipDisconnected { get { return T("Bağlı değil", "Not connected"); } }

        public static string VolumeCaption { get { return T("SES DÜZEYİ", "VOLUME"); } }
        public static string MuteTooltip { get { return T("Sessiz (aç / kapat)", "Mute (toggle)"); } }
        public static string StepTitle { get { return T("Tuş başına adım", "Step per key press"); } }
        public static string StepSub { get { return T("Klavye ses tuşlarının hassasiyeti", "Volume key sensitivity"); } }
        public static string AppsTitle { get { return T("Kontrol edilen uygulamalar", "Controlled apps"); } }
        public static string AppsNone { get { return T("Şu anda ses çalan uygulama yok", "No apps are playing right now"); } }
        public static string AutostartTitle { get { return T("Windows ile başlat", "Start with Windows"); } }
        public static string AutostartSub { get { return T("Açılışta tepside sessizce başlar", "Starts quietly in the tray at sign-in"); } }
        public static string Footer { get { return T("Windows uygulama ses düzeyleriyle çalışır · DAW / ASIO etkilenmez", "Uses Windows app volumes · DAW / ASIO not affected"); } }

        public static string HeroActive { get { return T("Varsayılan çıkış · klavye tuşları etkin", "Default output · keyboard keys active"); } }
        public static string HeroInactive { get { return T("Varsayılan çıkış değil · tuşlar Windows'ta", "Not the default output · keys go to Windows"); } }
        public static string ChipActive { get { return T("Aktif", "Active"); } }
        public static string ChipInactive { get { return T("Pasif", "Inactive"); } }
        public static string SystemSounds { get { return T("Sistem sesleri", "System sounds"); } }
    }
}
