// SettingsForm.cs: Einstellungen (Transparenz, Farbe, cm-Kalibrierung je Monitor).
using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace Bildschirmlineal
{
    internal sealed class SettingsForm : Form
    {
        private readonly Settings s;
        private readonly RulerForm ruler;
        private readonly double oldOpacity;
        private readonly Color oldColor;
        private readonly string screenName;
        private readonly double autoPxCm;

        private readonly TrackBar opacityBar = new TrackBar();
        private readonly Label opacityValue = new Label();
        private readonly Button colorButton = new Button();
        private readonly NumericUpDown pxcmBox = new NumericUpDown();
        private readonly NumericUpDown measuredBox = new NumericUpDown();
        private static readonly CultureInfo De = CultureInfo.GetCultureInfo("de-DE");

        public SettingsForm(Settings settings, RulerForm rulerForm)
        {
            s = settings;
            ruler = rulerForm;
            oldOpacity = s.Opacity;
            oldColor = s.Color;
            Screen sc = ruler.CurrentScreen;
            screenName = sc.DeviceName;
            double dpi; bool fromMonitor;
            autoPxCm = ruler.AutoPxPerCm(out dpi, out fromMonitor);

            Text = "Bildschirmlineal: Einstellungen";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            TopMost = true;
            AutoScaleMode = AutoScaleMode.Font;
            AutoScaleDimensions = new SizeF(6F, 13F);
            Font = new Font("Segoe UI", 9F);
            ClientSize = new Size(470, 380);

            int y = 14;
            Add(new Label { Text = "Deckkraft:", Location = new Point(14, y + 4), AutoSize = true });
            opacityBar.SetBounds(110, y, 270, 30);
            opacityBar.Minimum = (int)(Settings.MinOpacity * 100);
            opacityBar.Maximum = (int)(Settings.MaxOpacity * 100);
            opacityBar.TickFrequency = 5;
            opacityBar.Value = (int)Math.Round(s.Opacity * 100);
            opacityBar.ValueChanged += delegate { s.Opacity = opacityBar.Value / 100.0; ruler.ApplySettings(); UpdateLabels(); };
            Add(opacityBar);
            opacityValue.SetBounds(390, y + 4, 60, 20);
            Add(opacityValue);

            y += 44;
            Add(new Label { Text = "Farbe:", Location = new Point(14, y + 5), AutoSize = true });
            colorButton.SetBounds(110, y, 120, 28);
            colorButton.Text = "Ändern ...";
            colorButton.Click += delegate
            {
                using (var cd = new ColorDialog { Color = s.Color, FullOpen = true })
                    if (cd.ShowDialog(this) == DialogResult.OK) { s.Color = cd.Color; ruler.ApplySettings(); UpdateLabels(); }
            };
            Add(colorButton);

            y += 46;
            var grp = new GroupBox { Text = "cm-Kalibrierung für " + screenName + " (" + sc.Bounds.Width + " × " + sc.Bounds.Height + ")" };
            grp.SetBounds(10, y, 450, 220);
            Controls.Add(grp);

            var info = new Label { AutoSize = false };
            info.SetBounds(10, 22, 430, 36);
            info.Text = fromMonitor
                ? string.Format(De, "Automatisch vom Monitor: {0:0} DPI = {1:0.00} Pixel pro cm.", dpi, autoPxCm)
                : string.Format(De, "Monitor meldet keine Größe, Annahme 96 DPI = {0:0.00} Pixel pro cm. Bitte kalibrieren.", autoPxCm);
            grp.Controls.Add(info);

            grp.Controls.Add(new Label { Text = "Pixel pro cm:", Location = new Point(10, 66), AutoSize = true });
            pxcmBox.SetBounds(150, 62, 90, 24);
            pxcmBox.DecimalPlaces = 2;
            pxcmBox.Minimum = 5; pxcmBox.Maximum = 500;
            pxcmBox.Increment = 0.1M;
            pxcmBox.Value = (decimal)Math.Max(5, Math.Min(500, ruler.PxPerCm));
            grp.Controls.Add(pxcmBox);
            var autoBtn = new Button { Text = "Automatisch" };
            autoBtn.SetBounds(250, 61, 110, 27);
            autoBtn.Click += delegate { pxcmBox.Value = (decimal)Math.Max(5, Math.Min(500, autoPxCm)); };
            grp.Controls.Add(autoBtn);

            var help = new Label { AutoSize = false };
            help.SetBounds(10, 100, 430, 50);
            help.Text = "Mit einem echten Lineal nachmessen: Wie lang ist die Strecke von 0 bis 10 cm " +
                        "auf dem Bildschirmlineal in Wirklichkeit?";
            grp.Controls.Add(help);
            grp.Controls.Add(new Label { Text = "Gemessen (cm):", Location = new Point(10, 158), AutoSize = true });
            measuredBox.SetBounds(150, 154, 90, 24);
            measuredBox.DecimalPlaces = 2;
            measuredBox.Minimum = 1; measuredBox.Maximum = 50;
            measuredBox.Increment = 0.05M;
            measuredBox.Value = 10;
            grp.Controls.Add(measuredBox);
            var applyBtn = new Button { Text = "Umrechnen" };
            applyBtn.SetBounds(250, 153, 110, 27);
            applyBtn.Click += delegate
            {
                // 10 cm auf dem Lineal sind in echt "gemessen" cm lang -> Pixel pro cm korrigieren
                double v = (double)pxcmBox.Value * 10.0 / (double)measuredBox.Value;
                pxcmBox.Value = (decimal)Math.Max(5, Math.Min(500, v));
                measuredBox.Value = 10;
            };
            grp.Controls.Add(applyBtn);

            var ok = new Button { Text = "OK", DialogResult = DialogResult.OK };
            ok.SetBounds(270, 340, 90, 28);
            var cancel = new Button { Text = "Abbrechen", DialogResult = DialogResult.Cancel };
            cancel.SetBounds(368, 340, 90, 28);
            Add(ok); Add(cancel);
            AcceptButton = ok; CancelButton = cancel;
            UpdateLabels();
        }

        private void Add(Control c) { Controls.Add(c); }

        private void UpdateLabels()
        {
            opacityValue.Text = opacityBar.Value + " %";
            colorButton.BackColor = s.Color;
            colorButton.ForeColor = s.Color.GetBrightness() > 0.45f ? Color.Black : Color.White;
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            if (DialogResult == DialogResult.OK)
            {
                double v = (double)pxcmBox.Value;
                // Nur speichern, wenn wirklich vom Automatik-Wert abweichend
                if (Math.Abs(v - autoPxCm) < 0.005) s.PxPerCm.Remove(screenName);
                else s.PxPerCm[screenName] = v;
            }
            else
            {
                s.Opacity = oldOpacity;
                s.Color = oldColor;
            }
            ruler.ApplySettings();
            base.OnFormClosed(e);
        }
    }
}
