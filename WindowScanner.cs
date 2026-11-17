using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace BrightnessTray;

/// <summary>
/// Ermittelt, welche Programme gerade sichtbar sind (nicht minimiert, nicht komplett verdeckt) – auch nebeneinander.
/// Es werden nur Programmnamen im Speicher verglichen; nichts wird gespeichert oder gesendet.
/// </summary>
internal static class WindowScanner
{
    private delegate bool EnumProc(IntPtr hWnd, IntPtr lParam);
    [StructLayout(LayoutKind.Sequential)] private struct Rc { public int Left, Top, Right, Bottom; }

    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumProc cb, IntPtr lParam);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hWnd, out Rc rect);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")] private static extern int GetWindowLong(IntPtr hWnd, int index);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr hWnd, StringBuilder sb, int max);
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(IntPtr hWnd, int attr, out int value, int size);
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(IntPtr hWnd, int attr, out Rc value, int size);

    private const int GwlExStyle = -20, WsExToolWindow = 0x80, WsExNoActivate = 0x08000000;
    private const int DwmaCloaked = 14, DwmaFrameBounds = 9;
    private const int GridX = 8, GridY = 6, MinVisibleSamples = 10;   // ≥ ca. 20 % der Fensterfläche sichtbar

    private static readonly Dictionary<uint, (string Name, long Tick)> Names = new();

    private static string? NameOf(uint pid)
    {
        long now = Environment.TickCount64;
        if (Names.TryGetValue(pid, out var c) && now - c.Tick < 30_000) return c.Name;
        try
        {
            using var p = Process.GetProcessById((int)pid);
            string name = p.ProcessName;
            if (Names.Count > 300) Names.Clear();
            Names[pid] = (name, now);
            return name;
        }
        catch { return null; }
    }

    /// <summary>Namen (ohne .exe) aller Programme mit einem deutlich sichtbaren Fenster.</summary>
    public static HashSet<string> VisibleProcesses()
    {
        var wins = new List<(string Name, Rc R)>();
        var cls = new StringBuilder(256);
        int own = Environment.ProcessId;

        EnumWindows((h, _) =>
        {
            if (!IsWindowVisible(h) || IsIconic(h)) return true;
            int ex = GetWindowLong(h, GwlExStyle);
            if ((ex & (WsExToolWindow | WsExNoActivate)) != 0) return true;
            if (DwmGetWindowAttribute(h, DwmaCloaked, out int cloaked, sizeof(int)) == 0 && cloaked != 0) return true;

            cls.Clear();
            GetClassName(h, cls, cls.Capacity);
            if (ForegroundWatcher.IgnoredClasses.Contains(cls.ToString())) return true;

            GetWindowThreadProcessId(h, out uint pid);
            if (pid == (uint)own) return true;
            string? name = NameOf(pid);
            if (name == null || ForegroundWatcher.IgnoredProcesses.Contains(name)) return true;

            if (DwmGetWindowAttribute(h, DwmaFrameBounds, out Rc r, Marshal.SizeOf<Rc>()) != 0 && !GetWindowRect(h, out r))
                return true;
            if (r.Right - r.Left < 150 || r.Bottom - r.Top < 100) return true;

            wins.Add((name, r));
            return true;
        }, IntPtr.Zero);

        // Z-Reihenfolge: EnumWindows liefert von oben nach unten. Pro Fenster Rasterpunkte prüfen,
        // welches Fenster dort ganz oben liegt.
        var visible = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var hits = new int[wins.Count];
        for (int i = 0; i < wins.Count; i++)
        {
            var r = wins[i].R;
            double w = r.Right - r.Left, h = r.Bottom - r.Top;
            for (int gx = 0; gx < GridX; gx++)
                for (int gy = 0; gy < GridY; gy++)
                {
                    int x = (int)(r.Left + (gx + 0.5) * w / GridX), y = (int)(r.Top + (gy + 0.5) * h / GridY);
                    for (int j = 0; j < wins.Count; j++)
                    {
                        var o = wins[j].R;
                        if (x < o.Left || x >= o.Right || y < o.Top || y >= o.Bottom) continue;
                        if (j == i) hits[i]++;
                        break;   // oberstes Fenster an diesem Punkt gefunden
                    }
                }
        }
        for (int i = 0; i < wins.Count; i++)
            if (hits[i] >= MinVisibleSamples) visible.Add(wins[i].Name);
        return visible;
    }
}
