// Settings.cs: Einstellungen laden und speichern (lineal.ini neben der .exe).
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text;

namespace Bildschirmlineal
{
    internal sealed class Settings
    {
        public const int MinLength = 50;
        public const int MinThickness = 20, MaxThickness = 400;
        public const double MinOpacity = 0.20, MaxOpacity = 0.95; // unter 100 %, damit "Durchklickbar" immer funktioniert

        public int X = int.MinValue, Y = int.MinValue;   // int.MinValue = noch nie gespeichert
        public bool Vertical;
        public int Length = 600;
        public int Thickness = 60;
        public double Opacity = 0.75;
        public Color Color = Color.FromArgb(255, 236, 140);
        public bool ClickThrough;
        public string HotkeyToggle = "Ctrl+Alt+L";
        public string HotkeyRotate = "Ctrl+Alt+R";

        /// <summary>Kalibrierte Pixel pro cm je Monitor (Schlüssel: Gerätename wie \\.\DISPLAY1).</summary>
        public readonly Dictionary<string, double> PxPerCm = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static string FilePath
        {
            get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "lineal.ini"); }
        }

        public static Settings Load()
        {
            var s = new Settings();
            if (!File.Exists(FilePath)) return s;
            try
            {
                foreach (string raw in File.ReadAllLines(FilePath, Encoding.UTF8))
                {
                    string line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith("#") || line.StartsWith(";")) continue;
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    string k = line.Substring(0, eq).Trim();
                    string v = line.Substring(eq + 1).Trim();
                    s.Apply(k, v);
                }
            }
            catch { /* kaputte Datei: mit Standardwerten weiter */ }
            s.Clamp();
            return s;
        }

        private void Apply(string k, string v)
        {
            int i; double d;
            switch (k.ToLowerInvariant())
            {
                case "x": if (int.TryParse(v, NumberStyles.Integer, Inv, out i)) X = i; break;
                case "y": if (int.TryParse(v, NumberStyles.Integer, Inv, out i)) Y = i; break;
                case "vertical": Vertical = v == "1" || v.Equals("true", StringComparison.OrdinalIgnoreCase); break;
                case "length": if (int.TryParse(v, NumberStyles.Integer, Inv, out i)) Length = i; break;
                case "thickness": if (int.TryParse(v, NumberStyles.Integer, Inv, out i)) Thickness = i; break;
                case "opacity": if (double.TryParse(v, NumberStyles.Float, Inv, out d)) Opacity = d; break;
                case "color":
                    try { Color = ColorTranslator.FromHtml(v); } catch { }
                    break;
                case "clickthrough": ClickThrough = v == "1" || v.Equals("true", StringComparison.OrdinalIgnoreCase); break;
                case "hotkeytoggle": if (v.Length > 0) HotkeyToggle = v; break;
                case "hotkeyrotate": if (v.Length > 0) HotkeyRotate = v; break;
                default:
                    if (k.StartsWith("PxPerCm.", StringComparison.OrdinalIgnoreCase)
                        && double.TryParse(v, NumberStyles.Float, Inv, out d) && d > 1 && d < 1000)
                        PxPerCm[k.Substring(8)] = d;
                    break;
            }
        }

        public void Clamp()
        {
            Length = Math.Max(MinLength, Length);
            Thickness = Math.Max(MinThickness, Math.Min(MaxThickness, Thickness));
            Opacity = Math.Max(MinOpacity, Math.Min(MaxOpacity, Opacity));
        }

        public void Save()
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Bildschirmlineal: Einstellungen (wird beim Beenden automatisch geschrieben)");
            sb.AppendLine("X=" + X.ToString(Inv));
            sb.AppendLine("Y=" + Y.ToString(Inv));
            sb.AppendLine("Vertical=" + (Vertical ? "1" : "0"));
            sb.AppendLine("Length=" + Length.ToString(Inv));
            sb.AppendLine("Thickness=" + Thickness.ToString(Inv));
            sb.AppendLine("Opacity=" + Opacity.ToString("0.00", Inv));
            sb.AppendLine("Color=" + string.Format("#{0:X2}{1:X2}{2:X2}", Color.R, Color.G, Color.B));
            sb.AppendLine("ClickThrough=" + (ClickThrough ? "1" : "0"));
            sb.AppendLine("# Tastenkürzel, z. B. Ctrl+Alt+L oder Ctrl+Alt+Shift+L (Neustart nötig)");
            sb.AppendLine("HotkeyToggle=" + HotkeyToggle);
            sb.AppendLine("HotkeyRotate=" + HotkeyRotate);
            foreach (var kv in PxPerCm)
                sb.AppendLine("PxPerCm." + kv.Key + "=" + kv.Value.ToString("0.000", Inv));

            // Erst in eine Hilfsdatei schreiben, dann ersetzen: so bleibt bei einem Absturz die alte Datei heil.
            string tmp = FilePath + ".tmp";
            File.WriteAllText(tmp, sb.ToString(), new UTF8Encoding(false));
            if (File.Exists(FilePath)) File.Replace(tmp, FilePath, null);
            else File.Move(tmp, FilePath);
        }
    }
}
