// HotkeyWindow.cs: unsichtbares Nachrichtenfenster für globale Tastenkürzel.
// Eigenes Fenster, damit die Kürzel auch wirken, wenn das Lineal ausgeblendet ist.
using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace Bildschirmlineal
{
    internal sealed class HotkeyWindow : NativeWindow, IDisposable
    {
        public event Action<int> Pressed;
        private readonly List<int> ids = new List<int>();

        public HotkeyWindow()
        {
            var cp = new CreateParams();
            cp.Parent = new IntPtr(-3); // HWND_MESSAGE: reines Nachrichtenfenster
            CreateHandle(cp);
        }

        /// <summary>Kürzel wie "Ctrl+Alt+L" registrieren. false = ungültig oder schon belegt.</summary>
        public bool Register(int id, string hotkey)
        {
            uint mods; Keys key;
            if (!TryParse(hotkey, out mods, out key)) return false;
            if (!Native.RegisterHotKey(Handle, id, mods | Native.MOD_NOREPEAT, (uint)key)) return false;
            ids.Add(id);
            return true;
        }

        public static bool TryParse(string text, out uint mods, out Keys key)
        {
            mods = 0; key = Keys.None;
            if (string.IsNullOrEmpty(text)) return false;
            string[] parts = text.Split('+');
            for (int i = 0; i < parts.Length; i++)
            {
                string p = parts[i].Trim();
                string l = p.ToLowerInvariant();
                if (i < parts.Length - 1)
                {
                    if (l == "ctrl" || l == "strg" || l == "control") mods |= Native.MOD_CONTROL;
                    else if (l == "alt") mods |= Native.MOD_ALT;
                    else if (l == "shift" || l == "umschalt") mods |= Native.MOD_SHIFT;
                    else if (l == "win") mods |= Native.MOD_WIN;
                    else return false;
                }
                else
                {
                    try { key = (Keys)Enum.Parse(typeof(Keys), p, true); }
                    catch { return false; }
                }
            }
            return key != Keys.None && mods != 0;
        }

        /// <summary>Anzeige auf Deutsch: Ctrl+Alt+Shift+L wird Strg+Alt+Umschalt+L.</summary>
        public static string ToGerman(string hotkey)
        {
            return hotkey.Replace("Ctrl", "Strg").Replace("Shift", "Umschalt");
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Native.WM_HOTKEY && Pressed != null) Pressed(m.WParam.ToInt32());
            base.WndProc(ref m);
        }

        public void Dispose()
        {
            foreach (int id in ids) Native.UnregisterHotKey(Handle, id);
            ids.Clear();
            DestroyHandle();
        }
    }
}
