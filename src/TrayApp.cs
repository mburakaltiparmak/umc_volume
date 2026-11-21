using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Timer = System.Windows.Forms.Timer;
using Microsoft.Win32;

namespace UmcVolume
{
    sealed class TrayApp : ApplicationContext
    {
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string AppName = "UmcVolume";

        public readonly Settings Settings;
        public readonly VolumeModel Volume;
        public readonly SessionVolume Sessions;
        public event EventHandler StateChanged;

        readonly OsdForm osd;
        readonly NotifyIcon tray;
        readonly KeyboardHook hook;
        readonly Timer deviceTimer;
        readonly Timer saveTimer;
        readonly ToolStripMenuItem levelItem;
        readonly ToolStripMenuItem muteItem;
        MainForm window;
        Icon activeIcon, inactiveIcon;

        volatile bool deviceIsDefault;
        bool muteKeyHeld;

        public bool DeviceIsDefault { get { return deviceIsDefault; } }
        public DeviceInfo Device { get; private set; }   // interface being controlled, or null
        // The coloured app icon embedded in the exe (the tray keeps its monochrome glyph).
        public Icon WindowIcon
        {
            get
            {
                try { return Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? activeIcon; }
                catch { return activeIcon; }
            }
        }

        readonly EventWaitHandle showEvent;
        readonly EventWaitHandle exitEvent;

        public TrayApp(bool showWindowAtStart)
        {
            Settings = Settings.Load();
            L.Set(Settings.Language);
            Dpi.Init(Settings.UiScale);
            Volume = new VolumeModel(Settings, Path.Combine(Settings.DataDir, "state.ini"));

            osd = new OsdForm();
            IntPtr forceHandle = osd.Handle; // create the window now so BeginInvoke works

            Sessions = new SessionVolume();
            Sessions.SetTarget(Volume.Scalar);
            Sessions.SessionsChanged += delegate
            {
                try { osd.BeginInvoke((MethodInvoker)Notify); } catch { }
            };

            levelItem = new ToolStripMenuItem { Enabled = false };
            muteItem = new ToolStripMenuItem(L.Mute, null, delegate { ToggleMute(true); });

            var menu = new ContextMenuStrip();
            menu.Items.Add(new ToolStripMenuItem(L.Open, null, delegate { ShowWindow(); }) { Font = new Font(menu.Font, FontStyle.Bold) });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(levelItem);
            menu.Items.Add(muteItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(new ToolStripMenuItem(L.Exit, null, delegate { ExitThread(); }));

            BuildIcons();
            tray = new NotifyIcon { ContextMenuStrip = menu, Icon = inactiveIcon, Visible = true };
            tray.MouseClick += delegate(object s, MouseEventArgs e)
            {
                if (e.Button == MouseButtons.Left) ShowWindow();
            };

            saveTimer = new Timer { Interval = 60 };
            saveTimer.Tick += delegate { saveTimer.Stop(); SaveNow(); };

            RefreshDevice();

            hook = new KeyboardHook(OnKey);

            deviceTimer = new Timer { Interval = 1500 };
            deviceTimer.Tick += delegate { RefreshDevice(); };
            deviceTimer.Start();

            // A second launch of the exe signals this event to bring our window up.
            showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, Program.ShowEventName);
            ThreadPool.RegisterWaitForSingleObject(showEvent, delegate
            {
                try { osd.BeginInvoke((MethodInvoker)ShowWindow); } catch { }
            }, null, -1, false);

            exitEvent = new EventWaitHandle(false, EventResetMode.AutoReset, Program.ExitEventName);
            ThreadPool.RegisterWaitForSingleObject(exitEvent, delegate
            {
                try { osd.BeginInvoke((MethodInvoker)ExitThread); } catch { }
            }, null, -1, true);

            // Keep the autostart entry pointing at this exe with the /tray flag.
            if (IsAutostart()) SetAutostart(true);

            if (showWindowAtStart) ShowWindow();
        }

        // ---- public actions used by the window ------------------------------

        public void SetPercent(double percent, bool showOsd)
        {
            Volume.SetPercent(percent);
            Changed(showOsd);
        }

        public void ToggleMute(bool showOsd)
        {
            Volume.Muted = !Volume.Muted;
            Changed(showOsd);
        }

        public void SetStep(double step)
        {
            Settings.StepPercent = step;
            Settings.Save();
            Notify();
        }

        public bool IsAutostart()
        {
            using (var k = Registry.CurrentUser.OpenSubKey(RunKey))
                return k != null && k.GetValue(AppName) != null;
        }

        public void SetAutostart(bool on)
        {
            using (var k = Registry.CurrentUser.CreateSubKey(RunKey))
            {
                if (on) k.SetValue(AppName, "\"" + Application.ExecutablePath + "\" /tray");
                else k.DeleteValue(AppName, false);
            }
            Notify();
        }

        void ShowWindow()
        {
            if (window == null || window.IsDisposed) window = new MainForm(this);
            window.ShowNearTray();
        }

        // ---- keyboard ---------------------------------------------------------

        // Runs inside the hook callback: decide fast, do the work later on the UI thread.
        bool OnKey(int vk, bool down)
        {
            if (!deviceIsDefault || Volume.DeviceId == null) return false;

            if (vk == KeyboardHook.VK_VOLUME_MUTE)
            {
                if (!down) { muteKeyHeld = false; return true; }
                if (muteKeyHeld) return true; // ignore auto-repeat
                muteKeyHeld = true;
            }

            if (down)
            {
                int key = vk;
                osd.BeginInvoke((MethodInvoker)delegate { HandleKey(key); });
            }
            return true;
        }

        void HandleKey(int vk)
        {
            if (vk == KeyboardHook.VK_VOLUME_UP) Volume.Step(+1);
            else if (vk == KeyboardHook.VK_VOLUME_DOWN) Volume.Step(-1);
            else if (vk == KeyboardHook.VK_VOLUME_MUTE) Volume.Muted = !Volume.Muted;
            Changed(true);
        }

        // ---- state ------------------------------------------------------------

        void Changed(bool showOsd)
        {
            if (showOsd) osd.ShowLevel(LevelText(), Volume.Fraction, Volume.Muted);
            Sessions.SetTarget(Volume.Scalar); // applied immediately on the worker thread
            saveTimer.Stop();
            saveTimer.Start(); // coalesce key-repeat / slider-drag bursts into one file write
            Notify();
        }

        void Notify()
        {
            UpdateTray();
            if (StateChanged != null) StateChanged(this, EventArgs.Empty);
        }

        // Turkish convention puts the percent sign first: %55, %12,5
        string LevelText()
        {
            if (Volume.Muted) return L.Muted;
            return L.Percent(Volume.Percent);
        }

        void SaveNow()
        {
            try { Volume.Save(); }
            catch (Exception ex) { Program.Log("Save failed: " + ex); }
        }

        // Any Behringer USB audio output (USB vendor 0x1397), plus anything matching DeviceMatch.
        bool Supported(DeviceInfo d)
        {
            if (d.IsBehringer) return true;
            string match = Settings.DeviceMatch;
            return !string.IsNullOrEmpty(match) &&
                   (d.Name.IndexOf(match, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    d.InterfaceName.IndexOf(match, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        // Picks the interface to control: the default output if it is supported, otherwise the one
        // used last time, otherwise the first supported one. Keys only act while it is the default.
        void RefreshDevice()
        {
            bool beforeDefault = deviceIsDefault;
            string beforeId = Device != null ? Device.Id : null;
            try
            {
                List<DeviceInfo> all = AudioDevices.ListRender();
                string defaultId = AudioDevices.DefaultRenderId();

                DeviceInfo def = all.Find(d => d.Id == defaultId);
                bool defSupported = def != null && Supported(def);
                DeviceInfo target = defSupported ? def
                    : (all.Find(d => d.Id == Volume.DeviceId && Supported(d)) ?? all.Find(Supported));

                Device = target;
                if (target != null)
                {
                    if (Volume.DeviceId != target.Id) { Volume.DeviceId = target.Id; ScheduleSave(); }
                    Sessions.SetDevice(target.Id);
                }
                deviceIsDefault = defSupported;
            }
            catch (Exception ex)
            {
                deviceIsDefault = false;
                Program.Log("Device refresh failed: " + ex);
            }
            string afterId = Device != null ? Device.Id : null;
            if (beforeDefault != deviceIsDefault || beforeId != afterId) Notify();
            else UpdateTray();
        }

        void ScheduleSave()
        {
            saveTimer.Stop();
            saveTimer.Start();
        }

        void UpdateTray()
        {
            string level = LevelText();
            levelItem.Text = L.LevelPrefix + level;
            muteItem.Checked = Volume.Muted;
            tray.Icon = deviceIsDefault ? activeIcon : inactiveIcon;

            string tip = Device == null ? L.NoDevice
                : deviceIsDefault ? Device.Model + " · " + level
                : L.TrayInactive(Device.Model);
            tray.Text = tip.Length > 63 ? tip.Substring(0, 63) : tip;
        }

        // ---- icons ------------------------------------------------------------

        void BuildIcons()
        {
            bool lightTaskbar = false;
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                    lightTaskbar = k != null && Convert.ToInt32(k.GetValue("SystemUsesLightTheme", 0)) == 1;
            }
            catch { }
            Color on = lightTaskbar ? Color.FromArgb(20, 20, 20) : Color.White;
            activeIcon = MakeIcon(on, true);
            inactiveIcon = MakeIcon(Color.Gray, false);
        }

        static Icon MakeIcon(Color c, bool waves)
        {
            using (var bmp = new Bitmap(32, 32))
            {
                using (var g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    using (var b = new SolidBrush(c))
                        g.FillPolygon(b, new[] {
                            new Point(3, 12), new Point(9, 12), new Point(17, 4),
                            new Point(17, 28), new Point(9, 20), new Point(3, 20) });
                    if (waves)
                        using (var p = new Pen(c, 3f))
                        {
                            g.DrawArc(p, 12, 9, 10, 14, -55, 110);
                            g.DrawArc(p, 11, 3, 18, 26, -55, 110);
                        }
                }
                IntPtr h = bmp.GetHicon();
                using (var tmp = Icon.FromHandle(h))
                {
                    var icon = (Icon)tmp.Clone();
                    DestroyIcon(h);
                    return icon;
                }
            }
        }

        protected override void ExitThreadCore()
        {
            hook.Dispose();
            deviceTimer.Stop();
            Sessions.Dispose(); // puts every app back to 100 %
            if (saveTimer.Enabled) { saveTimer.Stop(); SaveNow(); }
            tray.Visible = false;
            tray.Dispose();
            if (window != null && !window.IsDisposed) window.Dispose();
            osd.Dispose();
            base.ExitThreadCore();
        }

        [DllImport("user32.dll")]
        static extern bool DestroyIcon(IntPtr handle);
    }
}
