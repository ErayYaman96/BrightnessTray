using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace BrightnessTray;

/// <summary>
/// Beobachtet, welches Fenster gerade im Vordergrund ist (nur das aktive Fenster zählt – egal, was sonst läuft).
/// Meldet Programmname und Fenstertitel, sobald sich beides ändert. Eigene Fenster und Windows-Oberfläche
/// (Taskleiste, Startmenü, Alt-Tab …) werden ignoriert, damit ein Klick aufs Tray-Symbol keine Regel beendet.
/// </summary>
internal sealed class ForegroundWatcher : IDisposable
{
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int maxCount);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder text, int maxCount);

    internal static readonly HashSet<string> IgnoredClasses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Shell_TrayWnd", "Shell_SecondaryTrayWnd", "NotifyIconOverflowWindow", "TopLevelWindowForOverflowXamlIsland",
        "XamlExplorerHostIslandWindow", "Windows.UI.Core.CoreWindow", "ForegroundStaging", "MultitaskingViewFrame",
        "TaskListThumbnailWnd", "TaskSwitcherWnd", "TaskSwitcherOverlayWnd", "Progman", "WorkerW", "#32768"
    };

    internal static readonly HashSet<string> IgnoredProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "SearchHost", "SearchApp", "StartMenuExperienceHost", "ShellExperienceHost", "LockApp", "TextInputHost"
    };

    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 600 };
    private IntPtr _lastHwnd;
    private string _lastTitle = "";

    /// <summary>(Prozessname ohne .exe, Fenstertitel)</summary>
    public event Action<string, string>? Changed;

    public ForegroundWatcher()
    {
        _timer.Tick += (_, _) => Poll();
        _timer.Start();
    }

    private void Poll()
    {
        IntPtr hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return;

        var titleBuilder = new StringBuilder(512);
        GetWindowText(hwnd, titleBuilder, titleBuilder.Capacity);
        string title = titleBuilder.ToString();
        if (hwnd == _lastHwnd && title == _lastTitle) return;
        _lastHwnd = hwnd;
        _lastTitle = title;

        GetWindowThreadProcessId(hwnd, out uint pid);
        if (pid == (uint)Environment.ProcessId) return;

        var classBuilder = new StringBuilder(256);
        GetClassName(hwnd, classBuilder, classBuilder.Capacity);
        if (IgnoredClasses.Contains(classBuilder.ToString())) return;

        string name;
        try
        {
            using var proc = Process.GetProcessById((int)pid);
            name = proc.ProcessName;
        }
        catch { return; }
        if (IgnoredProcesses.Contains(name)) return;

        Changed?.Invoke(name, title);
    }

    public void Dispose() => _timer.Dispose();
}
