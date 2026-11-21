using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace UmcVolume
{
    // Test harness (not part of the app): checks model-name parsing for known Behringer driver
    // names and renders the device card for each to a PNG, without the hardware attached.
    // Build: see build-test.cmd. Run from the project folder so assets\ is found.
    static class DeviceArtTest
    {
        [STAThread]
        static int Main(string[] args)
        {
            string outDir = args.Length > 0 ? args[0] : ".";
            Dpi.Init(0);
            L.Set("tr");
            DeviceArt.AssetsDir = Path.Combine(Environment.CurrentDirectory, "assets"); // build-test.cmd runs from the project folder

            var cases = new[]
            {
                "BEHRINGER UMC 202HD 192k", "BEHRINGER UMC 204HD 192k", "BEHRINGER UMC 404HD 192k",
                "BEHRINGER UMC 1820", "BEHRINGER UMC 22", "BEHRINGER UMC 204 96k", "X-AIR XR18",
            };
            int fail = 0;
            var expected = new[] { "UMC202HD", "UMC204HD", "UMC404HD", "UMC1820", "UMC22", "UMC204", "X-AIR XR18" };
            for (int i = 0; i < cases.Length; i++)
            {
                var d = new DeviceInfo { Id = "test" + i, Name = "OUT 1-2 (" + cases[i] + ")", InterfaceName = cases[i], Vid = 0x1397, Pid = 0x500 + i };
                bool ok = d.Model == expected[i];
                if (!ok) fail++;
                Console.WriteLine((ok ? "OK   " : "FAIL ") + cases[i] + " -> model '" + d.Model + "', title '" + d.Title + "'");

                using (var host = new Form { BackColor = Theme.Background, ClientSize = Dpi.Sz(460, 248) })
                using (var art = new DeviceArt { Location = Dpi.Pt(16, 16), Size = Dpi.Sz(428, 216) })
                {
                    host.Controls.Add(art);
                    art.SetDevice(d);
                    art.Active = i % 2 == 0;
                    art.Subtitle = art.Active ? L.HeroActive : L.HeroInactive;
                    using (var bmp = new Bitmap(art.Width, art.Height))
                    {
                        art.CreateControl();
                        art.DrawToBitmap(bmp, new Rectangle(Point.Empty, art.Size));
                        bmp.Save(Path.Combine(outDir, "card_" + d.Model.Replace(" ", "_") + ".png"));
                    }
                }
            }

            using (var host = new Form { BackColor = Theme.Background, ClientSize = Dpi.Sz(460, 248) })
            using (var art = new DeviceArt { Location = Dpi.Pt(16, 16), Size = Dpi.Sz(428, 216) })
            {
                host.Controls.Add(art);
                art.SetDevice(null);
                art.Subtitle = L.NoDeviceSub;
                using (var bmp = new Bitmap(art.Width, art.Height))
                {
                    art.CreateControl();
                    art.DrawToBitmap(bmp, new Rectangle(Point.Empty, art.Size));
                    bmp.Save(Path.Combine(outDir, "card_none.png"));
                }
            }
            Console.WriteLine(fail == 0 ? "all model names OK" : fail + " model name(s) wrong");
            return fail;
        }
    }
}
