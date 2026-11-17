namespace BrightnessTray;

/// <summary>
/// Verbindet Tastaturhook und Raw Input mit den Tastenzuweisungen und sorgt für eigene Tastenwiederholung:
/// Wer eine Taste hält, löst die Aktion (Heller/Dunkler/Wärmer/Kälter) fortlaufend aus – auch bei Tasten,
/// die von der Logitech-Software eingespielt werden und von Windows nicht automatisch wiederholt werden.
/// </summary>
internal sealed class InputManager : IDisposable
{
    private const int RepeatDelayMs = 350;   // bis die Wiederholung startet
    private const int RepeatIntervalMs = 60; // Takt der Wiederholung
    private const int MaxHoldMs = 20_000;    // Sicherung gegen "hängende" Tasten (falls ein Loslassen verloren geht)

    private readonly Settings _s;
    private readonly KeyboardHook _hook = new();
    private readonly RawInputWindow _raw = new();
    private readonly System.Windows.Forms.Timer _repeat = new() { Interval = RepeatIntervalMs };
    private readonly HashSet<int> _held = new();

    private Action<KeyBinding>? _capture;
    private BrightnessAction? _repAction;
    private int _repVk = -1;
    private string? _repHid;
    private long _repStart;

    public event Action<BrightnessAction>? ActionTriggered;

    public InputManager(Settings settings)
    {
        _s = settings;
        _hook.KeyDown = OnKey;
        _hook.KeyUp = OnKeyUp;
        _raw.HidReport += OnHid;
        _raw.HidReleased += OnHidReleased;
        _repeat.Tick += (_, _) => OnRepeatTick();
    }

    private static bool Repeatable(BrightnessAction a) =>
        a is BrightnessAction.BrightnessDown or BrightnessAction.BrightnessUp
          or BrightnessAction.Warmer or BrightnessAction.Cooler;

    // ---------- Normale Tasten ----------

    private bool OnKey(int vk, int mods)
    {
        if (!_s.HotkeysEnabled) return false;
        var b = _s.Bindings.FirstOrDefault(x => x.Hid == null && x.Vk == vk && x.Mods == mods);
        if (b == null) return false;

        // Hardware-Autorepeat ignorieren: wir wiederholen selbst (einheitlich für alle Tastaturen)
        if (!_held.Add(vk)) return true;

        ActionTriggered?.Invoke(b.Action);
        BeginRepeat(b.Action, vk, null);
        return true;
    }

    private void OnKeyUp(int vk)
    {
        _held.Remove(vk);
        if (vk == _repVk) StopRepeat();
    }

    // ---------- HID-Spezialtasten (Medientasten) ----------

    private void OnHid(string hex)
    {
        if (_capture != null)
        {
            var cb = _capture;
            _capture = null;
            _hook.Capture = null;
            cb(new KeyBinding { Hid = hex });
            return;
        }
        if (!_s.HotkeysEnabled) return;
        var b = _s.Bindings.FirstOrDefault(x => x.Hid == hex);
        if (b == null || _repHid == hex) return;

        ActionTriggered?.Invoke(b.Action);
        BeginRepeat(b.Action, -1, hex);
    }

    private void OnHidReleased()
    {
        if (_repHid != null) StopRepeat();
    }

    // ---------- Wiederholung ----------

    private void BeginRepeat(BrightnessAction action, int vk, string? hid)
    {
        if (!Repeatable(action)) return;
        _repAction = action;
        _repVk = vk;
        _repHid = hid;
        _repStart = Environment.TickCount64;
        _repeat.Start();
    }

    private void StopRepeat()
    {
        _repeat.Stop();
        _repAction = null;
        _repVk = -1;
        _repHid = null;
    }

    private void OnRepeatTick()
    {
        if (_repAction == null) { StopRepeat(); return; }

        long elapsed = Environment.TickCount64 - _repStart;
        if (elapsed > MaxHoldMs) { StopRepeat(); return; }
        // Beendet wird nur über das Loslassen (OnKeyUp / OnHidReleased). Windows' GetAsyncKeyState darf hier nicht
        // verwendet werden: Vom Hook verschluckte Tasten gelten dort nie als gedrückt.
        if (elapsed < RepeatDelayMs) return;

        ActionTriggered?.Invoke(_repAction.Value);
    }

    // ---------- Aufnehmen (Einstellungen) ----------

    /// <summary>Nächste Taste (normal oder HID) aufnehmen.</summary>
    public void BeginCapture(Action<KeyBinding> done)
    {
        StopRepeat();
        _capture = done;
        _hook.Capture = (vk, mods) =>
        {
            var cb = _capture;
            _capture = null;
            cb?.Invoke(new KeyBinding { Vk = vk, Mods = mods });
        };
    }

    public void CancelCapture()
    {
        _capture = null;
        _hook.Capture = null;
    }

    public void Dispose()
    {
        _repeat.Dispose();
        _hook.Dispose();
        _raw.Dispose();
    }
}
