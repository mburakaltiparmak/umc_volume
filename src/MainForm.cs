using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace UmcVolume
{
    // Main window: device card, volume and settings. Closing it only hides it; the app keeps running in the tray.
    // All layout numbers are 96-DPI values passed through Dpi.Px / Pt / Sz.
    sealed class MainForm : Form
    {
        const int Gap = 16;
        const int CardWidth = 428;
        const int RowHeight = 64;

        readonly TrayApp app;
        readonly DeviceArt hero;
        readonly Label percentLabel, dbLabel, footer;
        readonly RoundIconButton muteButton;
        readonly VolumeSlider slider;
        readonly Segmented stepPicker;
        readonly ToggleSwitch autostartSwitch;
        readonly Label appsSub, appsCount;
        readonly ToolTip tips = new ToolTip();
        bool updating;

        public MainForm(TrayApp app)
        {
            this.app = app;

            AutoScaleMode = AutoScaleMode.None; // we scale explicitly
            Text = L.WindowTitle;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.Manual;
            BackColor = Theme.Background;
            ForeColor = Theme.Text;
            Font = Dpi.Font("Segoe UI", 9.75f);
            Icon = app.WindowIcon;
            KeyPreview = true;

            int y = Gap;

            // ---- device ---------------------------------------------------------
            hero = new DeviceArt { Location = Dpi.Pt(Gap, y), Size = Dpi.Sz(CardWidth, 216) };
            Controls.Add(hero);
            y += 216 + 12;

            // ---- volume ---------------------------------------------------------
            var vol = new Card { Location = Dpi.Pt(Gap, y), Size = Dpi.Sz(CardWidth, 152) };
            Controls.Add(vol);
            vol.Controls.Add(Caption(L.VolumeCaption, 20, 16));

            percentLabel = new Label
            {
                Location = Dpi.Pt(14, 34), Size = Dpi.Sz(280, 58), AutoSize = false,
                Font = Dpi.Font("Segoe UI Light", 32f), ForeColor = Theme.Text, TextAlign = ContentAlignment.MiddleLeft
            };
            dbLabel = new Label { Location = Dpi.Pt(21, 90), AutoSize = true, ForeColor = Theme.TextDim, Font = Dpi.Font("Segoe UI", 9f) };
            vol.Controls.Add(percentLabel);
            vol.Controls.Add(dbLabel);

            muteButton = new RoundIconButton { Location = Dpi.Pt(CardWidth - 20 - 44, 38) };
            muteButton.Click += delegate { app.ToggleMute(false); };
            tips.SetToolTip(muteButton, L.MuteTooltip);
            vol.Controls.Add(muteButton);

            slider = new VolumeSlider { Location = Dpi.Pt(8, 110), Size = Dpi.Sz(CardWidth - 16, 32) };
            slider.ValueChanged += delegate { if (!updating) app.SetPercent(slider.Value, false); };
            vol.Controls.Add(slider);
            y += 152 + 12;

            // ---- settings -------------------------------------------------------
            var set = new Card { Location = Dpi.Pt(Gap, y), Size = Dpi.Sz(CardWidth, RowHeight * 3 + 8) };
            Controls.Add(set);

            stepPicker = new Segmented(new[] { 0.5, 1.0, 2.0, 5.0 }, new[] { L.Percent(0.5), L.Percent(1), L.Percent(2), L.Percent(5) })
            { Size = Dpi.Sz(196, 32) };
            stepPicker.SelectedChanged += delegate { if (!updating) app.SetStep(stepPicker.Value); };
            AddRow(set, 0, L.StepTitle, L.StepSub, stepPicker, 196, 32);

            appsCount = new Label
            {
                Size = Dpi.Sz(36, 28), TextAlign = ContentAlignment.MiddleCenter,
                Font = Dpi.Font("Segoe UI Semibold", 10f), ForeColor = Theme.Text, BackColor = Theme.Control
            };
            appsSub = AddRow(set, 1, L.AppsTitle, "", appsCount, 36, 28);

            autostartSwitch = new ToggleSwitch();
            autostartSwitch.CheckedChanged += delegate { if (!updating) app.SetAutostart(autostartSwitch.Checked); };
            AddRow(set, 2, L.AutostartTitle, L.AutostartSub, autostartSwitch, ToggleSwitch.BaseWidth, ToggleSwitch.BaseHeight);

            for (int i = 1; i < 3; i++) set.Dividers.Add(Dpi.Px(4 + i * RowHeight));
            y += RowHeight * 3 + 8 + 10;

            footer = new Label
            {
                Location = Dpi.Pt(Gap + 4, y), Size = Dpi.Sz(CardWidth - 8, 18),
                ForeColor = Theme.TextFaint, Font = Dpi.Font("Segoe UI", 8f), AutoEllipsis = true
            };
            Controls.Add(footer);
            y += 18 + 10;

            ClientSize = Dpi.Sz(CardWidth + 2 * Gap, y);

            app.StateChanged += OnStateChanged;
            RefreshAll();
        }

        static Label Caption(string text, int x, int y)
        {
            return new Label
            {
                Text = text, Location = Dpi.Pt(x, y), AutoSize = true,
                ForeColor = Theme.TextDim, Font = Dpi.Font("Segoe UI Semibold", 8f)
            };
        }

        // One settings row: title + subtitle on the left, control (w x h at 96 DPI) on the right.
        // Returns the subtitle label.
        Label AddRow(Card card, int index, string title, string subtitle, Control control, int w, int h)
        {
            int top = 4 + index * RowHeight;
            int textWidth = CardWidth - 40 - w - 16;
            card.Controls.Add(new Label
            {
                Text = title, Location = Dpi.Pt(20, top + 13), Size = Dpi.Sz(textWidth, 20),
                ForeColor = Theme.Text, Font = Dpi.Font("Segoe UI", 10f), AutoEllipsis = true
            });
            var sub = new Label
            {
                Text = subtitle, Location = Dpi.Pt(20, top + 34), Size = Dpi.Sz(textWidth, 18),
                ForeColor = Theme.TextDim, Font = Dpi.Font("Segoe UI", 8.5f), AutoEllipsis = true
            };
            card.Controls.Add(sub);
            control.Size = Dpi.Sz(w, h);
            control.Location = Dpi.Pt(CardWidth - 20 - w, top + (RowHeight - h) / 2);
            card.Controls.Add(control);
            return sub;
        }

        void OnStateChanged(object sender, EventArgs e)
        {
            if (IsDisposed) return;
            RefreshAll();
        }

        void RefreshAll()
        {
            updating = true;
            try
            {
                VolumeModel v = app.Volume;
                Settings s = app.Settings;

                hero.SetDevice(app.Device);
                hero.Active = app.DeviceIsDefault;
                hero.Subtitle = app.Device == null ? L.NoDeviceSub : (app.DeviceIsDefault ? L.HeroActive : L.HeroInactive);

                percentLabel.Text = v.Muted ? L.Muted : L.Percent(v.Percent);
                percentLabel.ForeColor = v.Muted ? Theme.TextDim : Theme.Text;
                dbLabel.Text = L.Db(v.LevelDb);
                muteButton.Glyph = v.Muted ? "" : "";
                muteButton.Active = v.Muted;

                slider.Muted = v.Muted;
                slider.Step = s.StepPercent;
                slider.Value = v.Percent;
                slider.Invalidate();

                stepPicker.SetSilently(s.StepPercent);

                string[] apps = app.Sessions.SessionNames;
                appsCount.Text = apps.Length.ToString(L.Culture);
                appsSub.Text = apps.Length == 0 ? L.AppsNone : string.Join(", ", apps);
                tips.SetToolTip(appsSub, string.Join("\n", apps));

                bool auto = app.IsAutostart();
                if (autostartSwitch.Checked != auto) autostartSwitch.SetSilently(auto);

                footer.Text = L.Footer;
            }
            finally { updating = false; }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Escape) Hide();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try
            {
                int on = 1;
                DwmSetWindowAttribute(Handle, 20, ref on, sizeof(int)); // dark title bar
                int round = 2;
                DwmSetWindowAttribute(Handle, 33, ref round, sizeof(int)); // rounded corners
            }
            catch { }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
                return;
            }
            base.OnFormClosing(e);
        }

        public void ShowNearTray()
        {
            if (!Visible)
            {
                Rectangle wa = Screen.PrimaryScreen.WorkingArea;
                int m = Dpi.Px(16);
                Location = new Point(wa.Right - Width - m, Math.Max(wa.Top + m, wa.Bottom - Height - m));
                Show();
            }
            if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
            Activate();
            slider.Focus();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) app.StateChanged -= OnStateChanged;
            base.Dispose(disposing);
        }

        [DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
    }
}
