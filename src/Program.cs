// Program.cs: Start, Symbol im Infobereich, Menü, Tastenkürzel, Autostart.
// Screen Ruler (Bildschirmlineal), MIT License (see LICENSE).
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

namespace Bildschirmlineal
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            bool created;
            using (var mutex = new Mutex(true, "Bildschirmlineal_EinzigeInstanz", out created))
            {
                if (!created)
                {
                    MessageBox.Show("Das Bildschirmlineal läuft bereits (Symbol im Infobereich unten rechts).",
                        "Bildschirmlineal", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                Native.EnablePerMonitorDpi();
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new LinealContext());
            }
        }
    }

    internal sealed class LinealContext : ApplicationContext
    {
        private const int IdToggle = 1, IdRotate = 2;
        private const string AutostartName = "Bildschirmlineal.lnk";

        private readonly Settings settings;
        private readonly RulerForm ruler;
        private readonly NotifyIcon tray;
        private readonly HotkeyWindow hotkeys;
        private readonly ToolStripMenuItem miShow, miClickThrough, miAutostart;
        private readonly System.Windows.Forms.Timer saveTimer;
        private string keyToggle = "", keyRotate = "";
        private bool exiting;

        public LinealContext()
        {
            settings = Settings.Load();
            ruler = new RulerForm(settings);
            ruler.StateChanged += delegate { ScheduleSave(); };

            // Speichern leicht verzögert, damit beim Mausrad nicht bei jedem Schritt geschrieben wird
            saveTimer = new System.Windows.Forms.Timer { Interval = 1500 };
            saveTimer.Tick += delegate { saveTimer.Stop(); SaveNow(); };

            // Menü (gleich für Infobereich und Rechtsklick auf das Lineal)
            var menu = new ContextMenuStrip();
            miShow = new ToolStripMenuItem("Lineal anzeigen", null, delegate { Toggle(); });
            var miRotate = new ToolStripMenuItem("Drehen (waagerecht/senkrecht)", null, delegate { RotateRuler(); });
            miClickThrough = new ToolStripMenuItem("Durchklickbar (Maus geht durch)", null, delegate { ToggleClickThrough(); });
            var miSettings = new ToolStripMenuItem("Einstellungen ...", null, delegate { ShowSettings(); });
            miAutostart = new ToolStripMenuItem("Mit Windows starten", null, delegate { ToggleAutostart(); });
            var miExit = new ToolStripMenuItem("Beenden", null, delegate { Exit(); });
            menu.Items.AddRange(new ToolStripItem[] {
                miShow, miRotate, miClickThrough, new ToolStripSeparator(),
                miSettings, miAutostart, new ToolStripSeparator(), miExit });
            menu.Opening += delegate
            {
                miShow.Checked = ruler.Visible;
                miClickThrough.Checked = settings.ClickThrough;
                miAutostart.Checked = File.Exists(AutostartPath);
            };
            ruler.ContextMenuStrip = menu;

            tray = new NotifyIcon
            {
                Icon = MakeIcon(),
                ContextMenuStrip = menu,
                Visible = true
            };
            tray.MouseClick += (s, e) => { if (e.Button == MouseButtons.Left) Toggle(); };

            // Globale Tastenkürzel, bei Belegung Ersatz mit zusätzlicher Umschalttaste
            hotkeys = new HotkeyWindow();
            hotkeys.Pressed += id => { if (id == IdToggle) Toggle(); else if (id == IdRotate) RotateRuler(); };
            string problems = "";
            keyToggle = RegisterWithFallback(IdToggle, settings.HotkeyToggle, "Lineal ein/aus", ref problems);
            keyRotate = RegisterWithFallback(IdRotate, settings.HotkeyRotate, "Drehen", ref problems);
            tray.Text = Truncate("Bildschirmlineal (" + HotkeyWindow.ToGerman(keyToggle) + " / " + HotkeyWindow.ToGerman(keyRotate) + ")", 63);
            if (problems.Length > 0)
                MessageBox.Show(problems + "\nDie Kürzel lassen sich in lineal.ini ändern (HotkeyToggle, HotkeyRotate).",
                    "Bildschirmlineal: Tastenkürzel belegt", MessageBoxButtons.OK, MessageBoxIcon.Warning);

            Application.ApplicationExit += delegate { if (!exiting) SaveNow(); };
            Microsoft.Win32.SystemEvents.SessionEnding += delegate { SaveNow(); };

            ruler.Show();
        }

        private string RegisterWithFallback(int id, string wanted, string what, ref string problems)
        {
            if (hotkeys.Register(id, wanted)) return wanted;
            uint m; Keys k;
            string alt = null;
            if (HotkeyWindow.TryParse(wanted, out m, out k))
            {
                // Ersatz vorschlagen: zusätzlich Umschalt (oder Win, falls Umschalt schon drin ist)
                alt = (m & Native.MOD_SHIFT) == 0 ? "Ctrl+Alt+Shift+" + k : "Ctrl+Alt+Win+" + k;
                if (hotkeys.Register(id, alt))
                {
                    problems += HotkeyWindow.ToGerman(wanted) + " (" + what + ") ist schon von einem anderen Programm belegt.\n" +
                                "Stattdessen gilt jetzt: " + HotkeyWindow.ToGerman(alt) + "\n\n";
                    return alt;
                }
            }
            problems += HotkeyWindow.ToGerman(wanted) + " (" + what + ") ist belegt oder ungültig" +
                        (alt != null ? ", auch " + HotkeyWindow.ToGerman(alt) + " geht nicht" : "") +
                        ". Bitte über das Symbol im Infobereich bedienen.\n\n";
            return "-";
        }

        private static string Truncate(string t, int max) { return t.Length <= max ? t : t.Substring(0, max); }

        private void Toggle()
        {
            if (ruler.Visible) ruler.Hide();
            else { ruler.Show(); ruler.TopMost = true; }
        }

        private void RotateRuler()
        {
            if (!ruler.Visible) { ruler.Show(); ruler.TopMost = true; }
            ruler.Rotate();
        }

        private void ToggleClickThrough()
        {
            settings.ClickThrough = !settings.ClickThrough;
            ruler.ApplyClickThrough();
            if (settings.ClickThrough)
                tray.ShowBalloonTip(4000, "Bildschirmlineal", "Durchklickbar ist an. Verschieben geht erst wieder nach " +
                    "Ausschalten (Rechtsklick auf das Symbol im Infobereich).", ToolTipIcon.Info);
            ScheduleSave();
        }

        private void ShowSettings()
        {
            using (var f = new SettingsForm(settings, ruler))
                f.ShowDialog();
            ScheduleSave();
        }

        // ---------- Autostart (Verknüpfung im Autostart-Ordner des Benutzers) ----------

        private static string AutostartPath
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), AutostartName); }
        }

        private void ToggleAutostart()
        {
            string link = AutostartPath;
            try
            {
                if (File.Exists(link))
                {
                    if (MessageBox.Show("Autostart ausschalten?\n\nDiese Verknüpfung wird entfernt:\n" + link,
                            "Bildschirmlineal", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                        File.Delete(link);
                    return;
                }
                if (MessageBox.Show("Mit Windows starten?\n\nDafür wird eine Verknüpfung im Autostart-Ordner angelegt (auf Laufwerk C:):\n" +
                        link + "\n\nDas Programm selbst bleibt auf D:.", "Bildschirmlineal",
                        MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                    return;

                // Verknüpfung über WScript.Shell anlegen (Teil von Windows, keine Zusatzbibliothek)
                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                object shell = Activator.CreateInstance(shellType);
                object sc = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { link });
                Type t = sc.GetType();
                t.InvokeMember("TargetPath", BindingFlags.SetProperty, null, sc, new object[] { Application.ExecutablePath });
                t.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, sc, new object[] { Path.GetDirectoryName(Application.ExecutablePath) });
                t.InvokeMember("Description", BindingFlags.SetProperty, null, sc, new object[] { "Bildschirmlineal" });
                t.InvokeMember("Save", BindingFlags.InvokeMethod, null, sc, null);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Autostart konnte nicht geändert werden:\n" + ex.Message, "Bildschirmlineal",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ---------- Speichern und Beenden ----------

        private void ScheduleSave()
        {
            saveTimer.Stop();
            saveTimer.Start();
        }

        private void SaveNow()
        {
            try
            {
                ruler.CaptureState();
                settings.Save();
            }
            catch (Exception ex)
            {
                if (!exiting) tray.ShowBalloonTip(5000, "Bildschirmlineal", "Speichern fehlgeschlagen: " + ex.Message, ToolTipIcon.Error);
            }
        }

        private void Exit()
        {
            saveTimer.Stop();
            SaveNow();
            exiting = true;
            hotkeys.Dispose();
            tray.Visible = false;
            tray.Dispose();
            ExitThread();
        }

        /// <summary>Symbol für den Infobereich: kleines gelbes Lineal, zur Laufzeit gezeichnet.</summary>
        private static Icon MakeIcon()
        {
            Size sz = SystemInformation.SmallIconSize;
            using (var bmp = new Bitmap(sz.Width, sz.Height))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.None;
                    float u = sz.Width / 16f;
                    var r = new RectangleF(0, 4 * u, sz.Width - 1, 8 * u);
                    using (var b = new SolidBrush(Color.FromArgb(255, 220, 90))) g.FillRectangle(b, r);
                    using (var p = new Pen(Color.FromArgb(60, 50, 20), Math.Max(1f, u)))
                    {
                        g.DrawRectangle(p, r.X, r.Y, r.Width, r.Height);
                        for (int i = 1; i < 8; i++)
                        {
                            float x = i * 2 * u;
                            g.DrawLine(p, x, r.Y, x, r.Y + (i % 2 == 0 ? 4 : 2) * u);
                        }
                    }
                }
                return Icon.FromHandle(bmp.GetHicon());
            }
        }
    }
}
