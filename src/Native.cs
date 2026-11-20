// Native.cs: Windows-API-Aufrufe (Tastenkürzel, DPI, Fensterstile).
using System;
using System.Drawing;
using System.Runtime.InteropServices;

namespace Bildschirmlineal
{
    internal static class Native
    {
        public const int WM_HOTKEY = 0x0312;
        public const int WM_MOUSEHWHEEL = 0x020E;
        public const int WM_DPICHANGED = 0x02E0;

        public const int GWL_EXSTYLE = -20;
        public const int WS_EX_TOPMOST = 0x00000008;
        public const int WS_EX_TRANSPARENT = 0x00000020;
        public const int WS_EX_TOOLWINDOW = 0x00000080;
        public const int WS_EX_LAYERED = 0x00080000;

        public const uint MOD_ALT = 0x1, MOD_CONTROL = 0x2, MOD_SHIFT = 0x4, MOD_WIN = 0x8, MOD_NOREPEAT = 0x4000;

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X, Y; }

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll")]
        public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

        [DllImport("shcore.dll")]
        private static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);

        [DllImport("user32.dll")]
        private static extern uint GetDpiForWindow(IntPtr hwnd);

        [DllImport("user32.dll")]
        private static extern bool SetProcessDpiAwarenessContext(IntPtr value);

        /// <summary>Per-Monitor-DPI (V2) einschalten. Zusätzlich steht es im Manifest.</summary>
        public static void EnablePerMonitorDpi()
        {
            try { SetProcessDpiAwarenessContext(new IntPtr(-4)); } catch { /* ältere Windows-Versionen: Manifest genügt */ }
        }

        /// <summary>Skalierungs-DPI des Monitors, auf dem das Fenster liegt (96 = 100 %).</summary>
        public static int WindowDpi(IntPtr hwnd)
        {
            try
            {
                uint d = GetDpiForWindow(hwnd);
                return d == 0 ? 96 : (int)d;
            }
            catch { return 96; }
        }

        /// <summary>
        /// Physische DPI des Monitors an einem Bildschirmpunkt (aus den EDID-Daten des Monitors).
        /// Gibt false zurück, wenn Windows keinen plausiblen Wert liefert.
        /// </summary>
        public static bool TryGetRawDpi(Point p, out double dpi)
        {
            dpi = 96;
            try
            {
                POINT pt; pt.X = p.X; pt.Y = p.Y;
                IntPtr mon = MonitorFromPoint(pt, 2 /* MONITOR_DEFAULTTONEAREST */);
                uint x, y;
                if (GetDpiForMonitor(mon, 2 /* MDT_RAW_DPI */, out x, out y) != 0) return false;
                if (x < 40 || x > 1200) return false;
                dpi = x;
                return true;
            }
            catch { return false; }
        }
    }
}
