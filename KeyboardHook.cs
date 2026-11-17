using System.Diagnostics;
using System.Runtime.InteropServices;

namespace BrightnessTray;

/// <summary>
/// Globaler Low-Level-Tastaturhook. Meldet Tastendrücke an einen Handler (true = Taste verschlucken)
/// und kann die nächste Taste für das "Aufnehmen" in den Einstellungen abfangen.
/// Auch von Logitech-Software eingespielte (injizierte) Tasten werden erkannt.
/// </summary>
internal sealed class KeyboardHook : IDisposable
{
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100, WM_KEYUP = 0x0101, WM_SYSKEYDOWN = 0x0104, WM_SYSKEYUP = 0x0105;

    [StructLayout(LayoutKind.Sequential)]
    private struct KBDLLHOOKSTRUCT { public uint vkCode, scanCode, flags, time; public IntPtr dwExtraInfo; }

    private delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, HookProc fn, IntPtr hMod, uint threadId);
    [DllImport("user32.dll")]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);
    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string? name);

    private readonly HookProc _proc; // Referenz halten, sonst räumt der GC den Callback weg
    private IntPtr _hook;
    private readonly HashSet<int> _swallowed = new();

    /// <summary>(vk, modifiers) -> true = Taste verschlucken.</summary>
    public Func<int, int, bool>? KeyDown { get; set; }

    /// <summary>Wenn gesetzt: nächste Nicht-Modifier-Taste wird hierhin geliefert und verschluckt (einmalig).</summary>
    public Action<int, int>? Capture { get; set; }

    /// <summary>Wird aufgerufen, wenn eine zuvor verschluckte Taste losgelassen wird.</summary>
    public Action<int>? KeyUp { get; set; }

    /// <summary>Ist die Taste laut Windows gerade gedrückt (auch von Software eingespielte Tasten)?</summary>
    public static bool IsDown(int vk) => Down(vk);

    public KeyboardHook()
    {
        _proc = Callback;
        using var module = Process.GetCurrentProcess().MainModule!;
        _hook = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(module.ModuleName), 0);
    }

    private static bool Down(int vk) => (GetAsyncKeyState(vk) & 0x8000) != 0;

    private static int Mods() =>
        (Down(0x11) ? 1 : 0) | (Down(0x12) ? 2 : 0) | (Down(0x10) ? 4 : 0) | ((Down(0x5B) || Down(0x5C)) ? 8 : 0);

    private static bool IsModifier(uint vk) => vk is 0x10 or 0x11 or 0x12 or (>= 0xA0 and <= 0xA5) or 0x5B or 0x5C;

    private IntPtr Callback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var kb = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
            int msg = wParam.ToInt32();
            int vk = (int)kb.vkCode;
            bool down = msg is WM_KEYDOWN or WM_SYSKEYDOWN;
            bool up = msg is WM_KEYUP or WM_SYSKEYUP;

            if (down && !IsModifier(kb.vkCode))
            {
                if (Capture != null)
                {
                    var capture = Capture;
                    Capture = null;
                    _swallowed.Add(vk);
                    capture(vk, Mods());
                    return (IntPtr)1;
                }
                if (KeyDown?.Invoke(vk, Mods()) == true)
                {
                    _swallowed.Add(vk);
                    return (IntPtr)1;
                }
            }
            else if (up && _swallowed.Remove(vk))
            {
                KeyUp?.Invoke(vk);
                return (IntPtr)1;
            }
        }
        return CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        if (_hook != IntPtr.Zero) { UnhookWindowsHookEx(_hook); _hook = IntPtr.Zero; }
    }
}
