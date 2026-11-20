// RulerForm.cs: das eigentliche Lineal-Fenster (randlos, halbtransparent, immer im Vordergrund).
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Globalization;
using System.Windows.Forms;

namespace Bildschirmlineal
{
    internal sealed class RulerForm : Form
    {
        private readonly Settings s;
        private bool dragging;
        private Point dragStartCursor, dragStartLocation;
        private int hoverPos = -1;     // Mausposition entlang des Lineals in Pixel, -1 = Maus nicht über dem Lineal
        private int wheelRest;         // Rest bei Mäusen mit feinem Mausrad (Delta kleiner als 120)

        /// <summary>Position, Größe oder Ausrichtung haben sich geändert (zum Speichern).</summary>
        public event EventHandler StateChanged;

        private static readonly CultureInfo De = CultureInfo.GetCultureInfo("de-DE");

        public RulerForm(Settings settings)
        {
            s = settings;
            Text = "Bildschirmlineal";
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            AutoScaleMode = AutoScaleMode.None;   // wir rechnen selbst in echten Bildschirmpixeln
            KeyPreview = true;
            MinimumSize = new Size(1, 1);
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            ApplySettings();
        }

        // Werkzeugfenster: nicht in Taskleiste und Alt+Tab. Optional durchklickbar.
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= Native.WS_EX_TOOLWINDOW | Native.WS_EX_TOPMOST;
                if (s != null && s.ClickThrough) cp.ExStyle |= Native.WS_EX_TRANSPARENT | Native.WS_EX_LAYERED;
                return cp;
            }
        }

        // Beim Einblenden per Kürzel nicht den Fokus vom Malprogramm wegnehmen.
        protected override bool ShowWithoutActivation { get { return true; } }

        /// <summary>Größte erlaubte Länge: Windows begrenzt Fenster etwa auf die Größe aller Monitore zusammen.</summary>
        public static int MaxLength
        {
            get
            {
                Size m = SystemInformation.MaxWindowTrackSize;
                return Math.Max(m.Width, m.Height);
            }
        }

        /// <summary>Einstellungen auf das Fenster übertragen.</summary>
        public void ApplySettings()
        {
            s.Clamp();
            s.Length = Math.Min(s.Length, MaxLength);
            if (s.X == int.MinValue || s.Y == int.MinValue || !IsOnAnyScreen(new Point(s.X + 10, s.Y + 10)))
            {
                // Erststart oder Monitor fehlt: mittig auf dem Hauptmonitor
                Rectangle wa = Screen.PrimaryScreen.WorkingArea;
                s.X = wa.Left + (wa.Width - (s.Vertical ? s.Thickness : s.Length)) / 2;
                s.Y = wa.Top + (wa.Height - (s.Vertical ? s.Length : s.Thickness)) / 2;
            }
            Bounds = new Rectangle(s.X, s.Y, s.Vertical ? s.Thickness : s.Length, s.Vertical ? s.Length : s.Thickness);
            BackColor = s.Color;
            Opacity = s.Opacity;
            ApplyClickThrough();
            Invalidate();
        }

        private static bool IsOnAnyScreen(Point p)
        {
            foreach (Screen sc in Screen.AllScreens)
                if (sc.Bounds.Contains(p)) return true;
            return false;
        }

        /// <summary>Durchklickbar ein/aus: Mausklicks gehen dann an das Programm darunter.</summary>
        public void ApplyClickThrough()
        {
            if (!IsHandleCreated) return;
            int ex = Native.GetWindowLong(Handle, Native.GWL_EXSTYLE);
            if (s.ClickThrough) ex |= Native.WS_EX_TRANSPARENT | Native.WS_EX_LAYERED;
            else ex &= ~Native.WS_EX_TRANSPARENT;
            Native.SetWindowLong(Handle, Native.GWL_EXSTYLE, ex);
            if (s.ClickThrough) { hoverPos = -1; Invalidate(); }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            ApplyClickThrough();
        }

        /// <summary>Waagerecht/senkrecht umschalten. Die linke obere Ecke bleibt stehen.</summary>
        public void Rotate()
        {
            CaptureState();
            s.Vertical = !s.Vertical;
            ApplySettings();
            OnStateChanged();
        }

        /// <summary>Aktuelle Fensterposition in die Einstellungen übernehmen.</summary>
        public void CaptureState()
        {
            s.X = Left;
            s.Y = Top;
        }

        private void OnStateChanged()
        {
            CaptureState();
            if (StateChanged != null) StateChanged(this, EventArgs.Empty);
        }

        // ---------- Pixel pro cm ----------

        /// <summary>Monitor, auf dem die Mitte des Lineals liegt.</summary>
        public Screen CurrentScreen
        {
            get { return Screen.FromPoint(new Point(Left + Width / 2, Top + Height / 2)); }
        }

        /// <summary>Automatisch ermittelte Pixel pro cm (physische DPI des Monitors / 2,54).</summary>
        public double AutoPxPerCm(out double dpi, out bool fromMonitor)
        {
            fromMonitor = Native.TryGetRawDpi(new Point(Left + Width / 2, Top + Height / 2), out dpi);
            return dpi / 2.54;
        }

        /// <summary>Pixel pro cm für den aktuellen Monitor (kalibrierter Wert, sonst automatisch).</summary>
        public double PxPerCm
        {
            get
            {
                double v;
                if (s.PxPerCm.TryGetValue(CurrentScreen.DeviceName, out v)) return v;
                double dpi; bool ok;
                return AutoPxPerCm(out dpi, out ok);
            }
        }

        // ---------- Maus ----------

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            dragging = true;
            dragStartCursor = Cursor.Position;
            dragStartLocation = Location;
            Capture = true;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (dragging)
            {
                Point c = Cursor.Position;
                Location = new Point(dragStartLocation.X + c.X - dragStartCursor.X,
                                     dragStartLocation.Y + c.Y - dragStartCursor.Y);
            }
            int pos = s.Vertical ? e.Y : e.X;
            if (pos != hoverPos) { hoverPos = pos; Invalidate(); }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (dragging && e.Button == MouseButtons.Left)
            {
                dragging = false;
                Capture = false;
                OnStateChanged();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            hoverPos = -1;
            Invalidate();
        }

        // Mausrad = Länge, Umschalt+Mausrad = Breite. Mit Strg in 1-Pixel-Schritten.
        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            bool shift = (ModifierKeys & Keys.Shift) != 0;
            HandleWheel(e.Delta, shift);
        }

        private void HandleWheel(int delta, bool thickness)
        {
            wheelRest += delta;
            int notches = wheelRest / 120;
            if (notches == 0) return;
            wheelRest -= notches * 120;
            bool fine = (ModifierKeys & Keys.Control) != 0;
            if (thickness) s.Thickness += notches * (fine ? 1 : 5);
            else s.Length += notches * (fine ? 1 : 10);
            CaptureState();
            ApplySettings();
            OnStateChanged();
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Native.WM_DPICHANGED)
            {
                // Beim Wechsel auf einen Monitor mit anderer Skalierung NICHT vergrößern/verkleinern:
                // das Lineal behält seine Länge in echten Pixeln, nur die cm-Skala passt sich an.
                Invalidate();
                m.Result = IntPtr.Zero;
                return;
            }
            if (m.Msg == Native.WM_MOUSEHWHEEL)
            {
                // Manche Mäuse/Treiber machen aus Umschalt+Mausrad ein waagerechtes Rad: dann Breite ändern.
                int delta = (short)((m.WParam.ToInt64() >> 16) & 0xFFFF);
                HandleWheel(delta, true);
                m.Result = IntPtr.Zero;
                return;
            }
            base.WndProc(ref m);
        }

        // ---------- Tastatur (nur wenn das Lineal aktiv ist) ----------

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            Keys code = keyData & Keys.KeyCode;
            int step = (keyData & Keys.Shift) != 0 ? 10 : 1;
            int dx = 0, dy = 0;
            switch (code)
            {
                case Keys.Left: dx = -step; break;
                case Keys.Right: dx = step; break;
                case Keys.Up: dy = -step; break;
                case Keys.Down: dy = step; break;
                case Keys.Escape: Hide(); return true;
                default: return base.ProcessCmdKey(ref msg, keyData);
            }
            Location = new Point(Left + dx, Top + dy);
            OnStateChanged();
            return true;
        }

        // Alt+F4 blendet nur aus, beendet nicht (Beenden über das Symbol im Infobereich).
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
                return;
            }
            CaptureState();
            base.OnFormClosing(e);
        }

        // ---------- Zeichnen ----------

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            int L = s.Length, T = s.Thickness;
            g.Clear(s.Color);
            g.SmoothingMode = SmoothingMode.None;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

            // Senkrecht: Koordinaten drehen, dann wird genau wie waagerecht gezeichnet.
            // Lineal-Koordinate (x, y) landet auf dem Bildschirm bei (T - y, x).
            if (s.Vertical)
            {
                g.TranslateTransform(T, 0);
                g.RotateTransform(90);
            }

            bool light = s.Color.GetBrightness() > 0.45f;
            Color ink = light ? Color.FromArgb(25, 25, 25) : Color.FromArgb(245, 245, 245);
            float scale = Native.WindowDpi(Handle) / 96f;
            float fontPx = Math.Max(9f, Math.Min(11f * scale, T / 4f));

            using (var pen = new Pen(ink, 1))
            using (var brush = new SolidBrush(ink))
            using (var font = new Font("Segoe UI", fontPx, GraphicsUnit.Pixel))
            {
                g.DrawRectangle(pen, 0, 0, L - 1, T - 1);

                // Oben: Pixel-Skala (Strich alle 5 px, Zahl alle 100 px)
                for (int x = 0; x < L; x += 5)
                {
                    float len = x % 100 == 0 ? T * 0.35f : x % 50 == 0 ? T * 0.24f : x % 10 == 0 ? T * 0.14f : T * 0.07f;
                    g.DrawLine(pen, x, 0, x, Math.Max(2f, len));
                    if (x % 100 == 0)
                    {
                        string t = x == 0 ? "0 px" : x.ToString(De);
                        if (x + 2 + g.MeasureString(t, font).Width < L) // am Ende nicht abschneiden
                            g.DrawString(t, font, brush, x + 2, 1);
                    }
                }

                // Unten: cm-Skala (Strich je mm, Zahl je cm)
                double pxcm = PxPerCm;
                double mm = pxcm / 10.0;
                for (int i = 0; i * mm < L; i++)
                {
                    bool cm = i % 10 == 0, half = i % 5 == 0;
                    if (!cm && !half && mm < 3) continue; // zu eng: nur halbe und ganze cm
                    float xf = (float)Math.Round(i * mm);
                    float len = cm ? T * 0.35f : half ? T * 0.24f : T * 0.14f;
                    g.DrawLine(pen, xf, T - 1, xf, T - 1 - len);
                    if (cm)
                    {
                        string t = i == 0 ? "0 cm" : (i / 10).ToString(De);
                        if (xf + 2 + g.MeasureString(t, font).Width < L)
                            g.DrawString(t, font, brush, xf + 2, T - 2 - font.Height);
                    }
                }

                // Mitte: Messwert an der Mausposition, sonst Gesamtlänge
                string info;
                if (hoverPos >= 0 && hoverPos < L)
                {
                    using (var red = new Pen(Color.FromArgb(220, 30, 30), 1))
                        g.DrawLine(red, hoverPos, 0, hoverPos, T);
                    info = string.Format(De, "{0} px  =  {1:0.00} cm", hoverPos, hoverPos / pxcm);
                }
                else
                {
                    info = string.Format(De, "{0} px  =  {1:0.0} cm", L, L / pxcm);
                }
                if (T >= font.Height * 3)
                {
                    SizeF sz = g.MeasureString(info, font);
                    float tx = hoverPos >= 0 ? hoverPos + 6 : (L - sz.Width) / 2;
                    if (tx + sz.Width > L - 4) tx = hoverPos - 6 - sz.Width;
                    g.DrawString(info, font, brush, Math.Max(2, tx), (T - sz.Height) / 2);
                }
            }
        }
    }
}
