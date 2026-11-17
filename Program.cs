using System.Drawing;
using System.Windows.Forms;
using Microsoft.Win32;

namespace BrightnessTray;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        using var mutex = new Mutex(true, "BrightnessTray_SingleInstance", out bool isNew);
        if (!isNew) return;

        ApplicationConfiguration.Initialize();
        // Wird für UI-Aufrufe aus Hintergrund-Threads gebraucht (OSD, Tooltip)
        SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
        Application.Run(new TrayContext());
    }
}

internal sealed class TrayContext : ApplicationContext
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "BrightnessTray";
    private const int PauseHotkeyMinutes = 60;

    private readonly Settings _s = Settings.Load();
    private readonly SynchronizationContext _ui;
    private readonly MonitorHub _hub = new();
    private readonly FlyoutForm _flyout;
    private readonly OsdForm _osd = new();
    private readonly ColorController _color = new();
    private readonly Scheduler _sched;
    private readonly InputManager _input;
    private readonly MouseHook _mouse;
    private readonly NotifyIcon _tray;
    private readonly ContextMenuStrip _menu = new();
    private readonly ToolStripMenuItem _settingsItem = new(), _filterItem = new(), _pauseItem = new(),
        _resumeItem = new(), _notifyItem = new(), _autostartItem = new(), _exitItem = new();
    private readonly ToolStripMenuItem _overrideItem = new() { Visible = false };
    private readonly ToolStripMenuItem _autoItem = new();
    private readonly ToolStripMenuItem[] _pauseOptions;
    private readonly System.Windows.Forms.Timer _ramp = new() { Interval = 5000 };
    private SettingsForm? _settingsForm;

    // Helligkeit per Tastenwiederholung (mehrere Schritte werden zusammengefasst)
    private int _pendingDelta;
    private int _worker; // 0 = frei, 1 = läuft
    private bool _dirty; // Einstellungen geändert, noch nicht gespeichert

    // Pause (Blaulichtfilter + Zeitplan)
    private DateTime? _pausedUntil;
    private bool Paused => _pausedUntil != null;

    // Manueller Vorrang: eigene Änderungen setzen Programmregeln und Zeitplan für eine Weile außer Kraft
    private DateTime? _overrideUntil;
    private bool Overriding => _overrideUntil != null;
    private bool _autoWas;                   // Automatik-Schalter beim letzten Speichern
    private bool _overrideEnding;            // Zeitplan wird nach dem Vorrang weich (statt hart) angewendet

    // Programmregeln (Vordergrundprogramm)
    private readonly ForegroundWatcher _watcher = new();
    private string _fgProc = "", _fgTitle = "";
    private AppRule? _activeRule;
    private int _baseP = -1;                 // Helligkeit vor der Regel
    private bool _ruleChangedBrightness;     // hat eine Regel die Helligkeit gesetzt?
    private int? _ruleKelvin;                // Farbtemperatur-Überlagerung durch die Regel

    // Kombinationen (mehrere Programme gleichzeitig sichtbar)
    private readonly System.Windows.Forms.Timer _comboTimer = new() { Interval = 1500 };
    private AppRule? _comboCurrent;          // aktuell zutreffende Kombination (nach Entprellung)
    private AppRule? _comboSeen;             // beim letzten Scan zutreffende Kombination
    private int _comboStable;

    // Weicher Wechsel bei Programmregeln
    private readonly Fader _fader;
    private int? _fadeKelvin;                // Zwischenwert eines laufenden weichen Farbwechsels
    private int? _fadeTargetP;               // Ziel einer laufenden weichen Helligkeitsänderung
    private int _wantP = -1, _wroteP = -1, _bWorker;

    // Sanfter Übergang (Zeitplan)
    private DateTime _rampStart, _rampEnd;
    private double _fromP, _toP, _fromK, _toK;
    private bool _rampBrightness;
    private int _lastRampP = -1;

    // Mausrad auf dem Tray-Symbol
    private Point _hoverPos = new(int.MinValue, int.MinValue);
    private long _hoverTick;
    private int _wheelAcc;

    public TrayContext()
    {
        Loc.Set(_s.Language);
        Theme.Refresh(_s.Theme);

        _ui = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
        _fader = new Fader(
            QueueBrightness, () => _fadeTargetP = null,
            k => { _fadeKelvin = k; ApplyColor(); },
            () => { _fadeKelvin = null; ApplyColor(); });
        _flyout = new FlyoutForm(_hub);
        _flyout.UserAdjusted += () => { CancelRamp(); CancelBrightnessFade(); StartOverride(); };
        _hub.Refresh();

        // --- Menü ---
        _notifyItem.CheckOnClick = true;
        _notifyItem.Checked = _s.NotifyEnabled;
        _notifyItem.CheckedChanged += (_, _) => { _s.NotifyEnabled = _notifyItem.Checked; _dirty = true; };
        _autostartItem.CheckOnClick = true;
        _autostartItem.Checked = IsAutostart();
        _autostartItem.CheckedChanged += (_, _) => SetAutostart(_autostartItem.Checked);
        _settingsItem.Click += (_, _) => OpenSettings();
        _filterItem.Click += (_, _) => OnAction(BrightnessAction.FilterToggle);
        _exitItem.Click += (_, _) => ExitThread();
        _resumeItem.Click += (_, _) => Resume();
        _overrideItem.Click += (_, _) => EndOverride(true);
        _autoItem.Checked = _s.AutomationEnabled;
        _autoItem.Click += (_, _) => SetAutomation(!_s.AutomationEnabled);
        _autoWas = _s.AutomationEnabled;

        _pauseOptions = new[] { 15, 30, 60, 120 }.Select(minutes =>
        {
            var item = new ToolStripMenuItem();
            item.Click += (_, _) => Pause(minutes);
            return item;
        }).ToArray();
        _pauseItem.DropDownItems.AddRange(_pauseOptions);
        _pauseItem.DropDownItems.Add(new ToolStripSeparator());
        _pauseItem.DropDownItems.Add(_resumeItem);

        _menu.Items.AddRange(new ToolStripItem[]
        {
            _autoItem, _overrideItem, _settingsItem, _filterItem, _pauseItem, _notifyItem, _autostartItem, new ToolStripSeparator(), _exitItem
        });

        _tray = new NotifyIcon { Icon = CreateSunIcon(), Text = "BrightnessTray", ContextMenuStrip = _menu, Visible = true };
        _tray.MouseClick += (_, e) =>
        {
            if (e.Button != MouseButtons.Left) return;
            if (_flyout.Visible) _flyout.Hide(); else _flyout.ShowFlyout();
        };
        _tray.MouseMove += (_, _) => { _hoverPos = Control.MousePosition; _hoverTick = Environment.TickCount64; };

        ApplyLanguage();
        Theme.ApplyMenu(_menu);

        // --- Eingaben, Zeitplan, Systemereignisse ---
        _input = new InputManager(_s);
        _input.ActionTriggered += OnAction;
        _mouse = new MouseHook { Wheel = OnWheel };

        _ramp.Tick += (_, _) => RampTick();
        _sched = new Scheduler(_s);
        _sched.EntryDue += OnEntryDue;
        _sched.Tick += OnTick;
        _watcher.Changed += OnForegroundChanged;
        _comboTimer.Tick += (_, _) => ComboTick();
        _comboTimer.Start();

        SystemEvents.DisplaySettingsChanged += OnDisplayChanged;
        SystemEvents.PowerModeChanged += OnPowerChanged;
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;

        ApplyColor();
        _sched.Check();
    }

    // ---------- Aktionen ----------

    private void OnAction(BrightnessAction action)
    {
        // Hook-Callback soll schnell zurückkehren: Rest auf den UI-Thread verschieben
        switch (action)
        {
            case BrightnessAction.BrightnessDown: Nudge(-_s.StepPercent); break;
            case BrightnessAction.BrightnessUp:   Nudge(+_s.StepPercent); break;
            case BrightnessAction.PauseToggle:
                _ui.Post(_ => { if (Paused) Resume(); else Pause(PauseHotkeyMinutes); }, null);
                break;
            default: _ui.Post(_ => HandleColorAction(action), null); break;
        }
    }

    private void HandleColorAction(BrightnessAction action)
    {
        CancelRamp();
        if (Paused) { _pausedUntil = null; UpdatePauseUi(); } // Wer den Filter bedient, beendet die Pause
        int visibleK = _fadeKelvin ?? StateKelvin;            // wirksamer Wert (auch mitten in einem weichen Wechsel)
        CancelKelvinFade();
        StartOverride();

        switch (action)
        {
            case BrightnessAction.FilterToggle:   // schaltet den aktuell sichtbaren Zustand um
                if (visibleK < ColorController.MaxKelvin)
                {
                    _s.FilterEnabled = false;
                }
                else
                {
                    _s.FilterEnabled = true;
                    if (_s.Kelvin >= ColorController.MaxKelvin) _s.Kelvin = 3400;
                }
                break;
            case BrightnessAction.Warmer:   // 100-K-Schritte, ausgehend vom aktuell wirksamen Wert
                _s.Kelvin = Math.Clamp(visibleK - 100, ColorController.MinKelvin, ColorController.MaxKelvin);
                _s.FilterEnabled = _s.Kelvin < ColorController.MaxKelvin;
                break;
            case BrightnessAction.Cooler:
                _s.Kelvin = Math.Clamp(visibleK + 100, ColorController.MinKelvin, ColorController.MaxKelvin);
                _s.FilterEnabled = _s.Kelvin < ColorController.MaxKelvin;
                break;
        }
        _ruleKelvin = null;  // eigene Eingabe hat Vorrang vor der Programmregel
        _dirty = true; // wird beim nächsten Takt gespeichert (schont die Platte bei gehaltener Taste)
        ApplyColor();
        ShowKelvinOsd();
    }

    private void ShowKelvinOsd()
    {
        int k = EffectiveKelvin;
        ShowOsd("☾", $"{k} K", (k - ColorController.MinKelvin) / (double)(ColorController.MaxKelvin - ColorController.MinKelvin));
    }

    /// <summary>Helligkeit relativ ändern. Mehrere schnelle Tastendrücke werden zusammengefasst.</summary>
    private void Nudge(int delta)
    {
        CancelRamp();
        CancelBrightnessFade();
        StartOverride();
        Interlocked.Add(ref _pendingDelta, delta);
        if (Interlocked.CompareExchange(ref _worker, 1, 0) != 0) return;

        Task.Run(() =>
        {
            int avg = -1;
            while (true)
            {
                int d = Interlocked.Exchange(ref _pendingDelta, 0);
                if (d != 0) avg = _hub.Step(d);
                Volatile.Write(ref _worker, 0);
                if (Volatile.Read(ref _pendingDelta) == 0 ||
                    Interlocked.CompareExchange(ref _worker, 1, 0) != 0) break;
            }
            if (avg >= 0)
                _ui.Post(_ =>
                {
                    if (!Paused && !Overriding) _tray.Text = Loc.T("tip.brightness", avg);
                    ShowOsd("☀", $"{avg} %", avg / 100.0);
                }, null);
        });
    }

    private void ShowOsd(string glyph, string text, double fraction)
    {
        if (_s.OsdEnabled) _osd.ShowOsd(glyph, text, fraction, _s.OsdMs);
    }

    // ---------- Mausrad auf dem Tray-Symbol ----------

    private bool OnWheel(Point pos, int delta)
    {
        if (!_s.TrayWheelEnabled) return false;
        // Nur wenn die Maus über dem Tray-Symbol steht (zuletzt dort bewegt, gleiche Position)
        if (Environment.TickCount64 - _hoverTick > 30_000) return false;
        if (Math.Abs(pos.X - _hoverPos.X) + Math.Abs(pos.Y - _hoverPos.Y) > 4) return false;

        _wheelAcc += delta;
        int notches = _wheelAcc / 120;
        _wheelAcc -= notches * 120;
        if (notches == 0) return true;

        if (KeyboardHook.IsDown(0x11)) // Strg = Farbtemperatur (hoch = kälter)
        {
            var action = notches > 0 ? BrightnessAction.Cooler : BrightnessAction.Warmer;
            for (int i = 0; i < Math.Abs(notches); i++) OnAction(action);
        }
        else
        {
            Nudge(notches * _s.StepPercent);
        }
        return true;
    }

    // ---------- Zeitplan & sanfte Übergänge ----------

    private void OnEntryDue(int percent, int kelvin, DateTime when, bool immediate)
    {
        if (!_s.AutomationEnabled) return;   // Automatik aus: Zeitplan ruht (Einstellungen bleiben erhalten)
        if (Overriding) return;   // eigene Einstellung hat Vorrang; nach Ablauf wird der aktuelle Eintrag angewendet

        if (_overrideEnding && !Paused && _activeRule == null)
        {
            // Vorrang gerade beendet: Zeitplanwerte weich einblenden statt hart umzuschalten
            int fromK = EffectiveKelvin;
            if (percent >= 0) ChangeBrightness(percent, _s.RuleFadeBrightnessSec);
            _s.Kelvin = kelvin;
            _s.FilterEnabled = kelvin < ColorController.MaxKelvin;
            _dirty = true;
            FadeColorFrom(fromK, _s.RuleFadeColorSec);
            return;
        }

        if (Paused)
        {
            // Während der Pause nur merken, was gelten würde; Helligkeit bleibt unangetastet
            _s.Kelvin = kelvin;
            _s.FilterEnabled = kelvin < ColorController.MaxKelvin;
            _dirty = true;
            return;
        }

        if (_activeRule != null)
        {
            // Programmregel aktiv: Zeitplan aktualisiert nur den Ausgangszustand und überschreibt die Regel nicht
            _baseP = percent;
            _s.Kelvin = kelvin;
            _s.FilterEnabled = kelvin < ColorController.MaxKelvin;
            _dirty = true;
            if (_activeRule.Percent == null && percent >= 0) Task.Run(() => _hub.SetAll(percent));
            ApplyColor();
            return;
        }

        double remaining = _s.TransitionMinutes * 60.0 - (DateTime.Now - when).TotalSeconds;
        bool ramped = !(immediate || remaining <= 10);
        if (ramped)
        {
            StartRamp(percent, kelvin, remaining);
        }
        else
        {
            CancelRamp();
            ApplyTarget(percent, kelvin);
        }

        // Beim Programmstart / Speichern der Einstellungen (immediate) keine Meldung
        if (!immediate && _s.NotifySchedule) NotifySchedule(percent, kelvin, when, ramped ? remaining : 0);
    }

    private void NotifySchedule(int percent, int kelvin, DateTime when, double rampSeconds)
    {
        string text = rampSeconds > 0
            ? Loc.T("n.sched.ramp", Math.Max(1, (int)Math.Round(rampSeconds / 60.0)), percent, kelvin)
            : Loc.T("n.b.set", percent) + "\n" + Loc.T("n.k.set", kelvin);
        Notify(Loc.T("n.sched.title", when.ToString("HH:mm")), text);
    }

    // ---------- Benachrichtigungen ----------

    /// <summary>Windows-Benachrichtigung (erscheint als Toast unten rechts).</summary>
    private void Notify(string title, string body)
    {
        if (!_s.NotifyEnabled || string.IsNullOrWhiteSpace(body)) return;
        _tray.ShowBalloonTip(5000, title, body, ToolTipIcon.None);
    }

    private static string RuleName(AppRule r) =>
        r.Combo.Count > 0 ? string.Join(" + ", r.Combo.Select(NormalizeProc))
        : string.IsNullOrWhiteSpace(r.Title) ? NormalizeProc(r.Process) : r.Title.Trim();

    private void ApplyTarget(int percent, int kelvin)
    {
        CancelBrightnessFade();
        CancelKelvinFade();
        if (percent >= 0) Task.Run(() => _hub.SetAll(percent));
        _s.Kelvin = kelvin;
        _s.FilterEnabled = kelvin < ColorController.MaxKelvin;
        _dirty = true;
        ApplyColor();
    }

    private void StartRamp(int percent, int kelvin, double seconds)
    {
        var snap = _hub.Snapshot();
        _rampBrightness = snap.Count > 0 && percent >= 0;
        _fromP = _rampBrightness ? snap.Average(x => x.Percent) : 0;
        _toP = percent;
        _fromK = EffectiveKelvin;
        CancelKelvinFade();        // Zeitplan-Übergang übernimmt ab hier
        CancelBrightnessFade();
        _toK = kelvin;
        _lastRampP = -1;
        _rampStart = DateTime.Now;
        _rampEnd = _rampStart.AddSeconds(seconds);
        _ramp.Start();
        RampTick();
    }

    private void RampTick()
    {
        double total = (_rampEnd - _rampStart).TotalSeconds;
        double t = total <= 0 ? 1 : Math.Clamp((DateTime.Now - _rampStart).TotalSeconds / total, 0, 1);

        if (_rampBrightness)
        {
            int p = (int)Math.Round(_fromP + (_toP - _fromP) * t);
            if (p != _lastRampP)
            {
                _lastRampP = p;
                Task.Run(() => _hub.SetAll(p));
            }
        }

        // Farbtemperatur in 100-K-Schritten
        int k = (int)(Math.Round((_fromK + (_toK - _fromK) * t) / 100.0) * 100);
        k = Math.Clamp(k, ColorController.MinKelvin, ColorController.MaxKelvin);
        if (k != EffectiveKelvin)
        {
            _s.Kelvin = k;
            _s.FilterEnabled = k < ColorController.MaxKelvin;
            ApplyColor();
        }

        if (t >= 1)
        {
            _ramp.Stop();
            _dirty = true;
        }
    }

    /// <summary>Laufenden Übergang abbrechen (z. B. weil der Nutzer selbst verstellt).</summary>
    private void CancelRamp() => _ramp.Stop();

    // ---------- Programmregeln ----------

    private void OnForegroundChanged(string process, string title)
    {
        _fgProc = process;
        _fgTitle = title;
        EvaluateRules();
    }

    private static string NormalizeProc(string name)
    {
        name = name.Trim();
        return name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? name[..^4] : name;
    }

    private AppRule? FindRule(string process, string title) =>
        _s.AppRules
            .Where(r => !string.IsNullOrWhiteSpace(r.Process)
                && string.Equals(NormalizeProc(r.Process), process, StringComparison.OrdinalIgnoreCase)
                && (string.IsNullOrWhiteSpace(r.Title)
                    || title.Contains(r.Title.Trim(), StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(r => !string.IsNullOrWhiteSpace(r.Title))   // Regel mit Fenstertitel zuerst
            .FirstOrDefault();

    private int CurrentBrightness()
    {
        var snap = _hub.Snapshot();
        return snap.Count > 0 ? (int)Math.Round(snap.Average(x => x.Percent)) : -1;
    }

    private void EvaluateRules()
    {
        if (Overriding) return;   // eigene Einstellung hat Vorrang vor Programmregeln
        AppRule? rule = null;
        if (_s.AppRulesEnabled && _s.AutomationEnabled)
        {
            rule = _comboCurrent;   // Kombination ist spezifischer als eine Einzelregel
            if (rule == null && _fgProc.Length > 0) rule = FindRule(_fgProc, _fgTitle);
        }
        if (ReferenceEquals(rule, _activeRule)) return;

        if (rule == null) { LeaveRule(_s.RulesRestoreOnLeave); return; }

        if (_activeRule == null) CaptureBaseline();
        _activeRule = rule;
        ApplyRule(rule);
    }

    private AppRule? FindCombo(HashSet<string> visible) =>
        _s.ComboRules
            .Where(c => c.Combo.Count >= 2 && c.Combo.All(p => visible.Contains(NormalizeProc(p))))
            .OrderByDescending(c => c.Combo.Count)   // die spezifischste Kombination zuerst
            .FirstOrDefault();

    /// <summary>Alle 1,5 s: welche Kombination passt gerade? Erst nach zwei gleichen Messungen anwenden (kein Flackern beim Fensterverschieben).</summary>
    private void ComboTick()
    {
        if (_s.ComboRules.Count == 0)
        {
            if (_comboCurrent != null) { _comboCurrent = null; _comboSeen = null; EvaluateRules(); }
            return;
        }
        var match = FindCombo(WindowScanner.VisibleProcesses());
        if (ReferenceEquals(match, _comboSeen)) _comboStable++; else { _comboSeen = match; _comboStable = 0; }
        if (_comboStable >= 1 && !ReferenceEquals(match, _comboCurrent))
        {
            _comboCurrent = match;
            EvaluateRules();
        }
    }

    /// <summary>Merkt sich den Zustand vor der ersten Regel (damit er später wiederhergestellt werden kann).</summary>
    private void CaptureBaseline()
    {
        if (_ramp.Enabled)   // laufender Übergang: sein Ziel gilt als Ausgangszustand
        {
            _baseP = _rampBrightness ? (int)_toP : CurrentBrightness();
            _s.Kelvin = (int)_toK;
            _s.FilterEnabled = _toK < ColorController.MaxKelvin;
            CancelRamp();
        }
        else
        {
            _baseP = _fadeTargetP ?? CurrentBrightness();   // laufender weicher Wechsel: dessen Ziel zählt
        }
        _ruleChangedBrightness = false;
    }

    private void ApplyRule(AppRule rule)
    {
        int fromK = EffectiveKelvin;   // aktuell sichtbare Farbtemperatur (Startwert des weichen Wechsels)

        int? targetB = null;
        if (rule.Percent is int p)
        {
            _ruleChangedBrightness = true;
            targetB = p;
        }
        else if (_ruleChangedBrightness && _baseP >= 0)   // vorherige Regel hatte die Helligkeit verändert
        {
            _ruleChangedBrightness = false;
            targetB = _baseP;
        }
        if (targetB is int tb) ChangeBrightness(tb, _s.RuleFadeBrightnessSec);

        _ruleKelvin = rule.Kelvin;
        FadeColorFrom(fromK, _s.RuleFadeColorSec);

        if (rule.Percent is int shown) ShowOsd("☀", $"{shown} %", shown / 100.0);
        else if (rule.Kelvin != null) ShowKelvinOsd();

        if (_s.NotifyRuleStart)
        {
            var lines = new List<string>();
            if (rule.Percent is int pb) lines.Add(Loc.T("n.b.set", pb));
            if (rule.Kelvin is int pk) lines.Add(Loc.T("n.k.set", pk));
            Notify(Loc.T("n.rule.title", RuleName(rule)), string.Join("\n", lines));
        }
    }

    /// <summary>Programm nicht mehr im Vordergrund: vorherigen Zustand wiederherstellen oder Regelwerte behalten.</summary>
    private void LeaveRule(bool restore, bool synchronous = false, bool notify = true)
    {
        if (_activeRule == null) return;

        var ended = _activeRule;
        int fromK = EffectiveKelvin;   // Startwert des weichen Wechsels zurück
        int? hadKelvin = _ruleKelvin;
        bool changedBrightness = _ruleChangedBrightness;
        int? restored = null;
        if (restore)
        {
            if (_ruleChangedBrightness && _baseP >= 0)
            {
                int back = _baseP;
                if (synchronous) _hub.SetAll(back); else ChangeBrightness(back, _s.RuleFadeBrightnessSec);
                restored = back;
            }
        }
        else if (_ruleKelvin is int keep)   // beibehalten: Farbtemperatur der Regel dauerhaft übernehmen
        {
            _s.Kelvin = keep;
            _s.FilterEnabled = keep < ColorController.MaxKelvin;
            _dirty = true;
        }

        _ruleKelvin = null;
        _activeRule = null;
        _ruleChangedBrightness = false;
        _baseP = -1;

        if (synchronous) return;
        FadeColorFrom(fromK, _s.RuleFadeColorSec);
        if (restored is int value) ShowOsd("☀", $"{value} %", value / 100.0);

        if (notify && _s.NotifyRuleEnd && (changedBrightness || hadKelvin != null))
        {
            var lines = new List<string>();
            if (restore)
            {
                if (restored is int rb) lines.Add(Loc.T("n.b.back", rb));
                if (hadKelvin != null) lines.Add(Loc.T("n.k.back", EffectiveKelvin));
            }
            else
            {
                lines.Add(Loc.T("n.kept"));
            }
            Notify(Loc.T("n.rule.end.title", RuleName(ended)), string.Join("\n", lines));
        }
    }

    // ---------- Weicher Wechsel ----------

    /// <summary>Helligkeit weich (oder bei 0 s sofort) auf den Zielwert bringen.</summary>
    private void ChangeBrightness(int target, double seconds)
    {
        int from = CurrentBrightness();
        if (seconds <= 0 || from < 0 || from == target)
        {
            CancelBrightnessFade();
            Task.Run(() => _hub.SetAll(target));
            return;
        }
        _wroteP = -1;
        _fadeTargetP = target;
        _fader.StartBrightness(from, target, seconds);
    }

    /// <summary>
    /// Farbtemperatur von <paramref name="fromK"/> weich zum aktuellen Zielzustand überblenden.
    /// Der Zielzustand (Regel/Einstellung) wird vorher gesetzt; das Überblenden betrifft nur die Anzeige.
    /// </summary>
    private void FadeColorFrom(int fromK, double seconds)
    {
        int toK = StateKelvin;
        if (seconds <= 0 || Paused || fromK == toK)
        {
            CancelKelvinFade();
            ApplyColor();
            return;
        }
        _fadeKelvin = fromK;
        _fader.StartKelvin(fromK, toK, seconds);
    }

    private void CancelBrightnessFade()
    {
        _fader.CancelBrightness();
        _fadeTargetP = null;
    }

    private void CancelKelvinFade()
    {
        _fader.CancelKelvin();
        _fadeKelvin = null;
    }

    /// <summary>DDC/CI ist langsam: immer nur den neuesten Wert schreiben, Zwischenwerte überspringen.</summary>
    private void QueueBrightness(int percent)
    {
        Volatile.Write(ref _wantP, percent);
        if (Interlocked.CompareExchange(ref _bWorker, 1, 0) != 0) return;

        Task.Run(() =>
        {
            while (true)
            {
                int want = Volatile.Read(ref _wantP);
                if (want != _wroteP)
                {
                    _hub.SetAll(want);
                    _wroteP = want;
                }
                Volatile.Write(ref _bWorker, 0);
                if (Volatile.Read(ref _wantP) == _wroteP ||
                    Interlocked.CompareExchange(ref _bWorker, 1, 0) != 0) break;
            }
        });
    }

    // ---------- Manueller Vorrang ----------

    /// <summary>
    /// Eigene Änderung: Programmregel und Zeitplan ruhen für die eingestellte Zeit (jede weitere Änderung verlängert sie).
    /// Der gerade gesetzte Wert bleibt unangetastet; eine aktive Regel wird ohne Zurücksetzen beendet.
    /// </summary>
    private void StartOverride()
    {
        if (!_s.OverrideEnabled || !_s.AutomationEnabled) return;

        bool first = !Overriding;
        _overrideUntil = _s.OverrideMinutes <= 0 ? DateTime.MaxValue : DateTime.Now.AddMinutes(_s.OverrideMinutes);

        if (_activeRule != null)
        {
            // Farbtemperatur der Regel als aktuelle Einstellung übernehmen, damit sie nicht plötzlich springt
            if (_ruleKelvin is int k)
            {
                _s.Kelvin = k;
                _s.FilterEnabled = k < ColorController.MaxKelvin;
                _dirty = true;
            }
            _ruleKelvin = null;
            _activeRule = null;
            _ruleChangedBrightness = false;
            _baseP = -1;
        }

        UpdateStatusUi();
        if (first && _s.NotifyOverride)
            Notify(Loc.T("n.override.title"), OverrideText("n.override.body"));
    }

    /// <summary>Vorrang beenden (abgelaufen oder per Menü): Zeitplan und Programmregeln greifen wieder, weich eingeblendet.</summary>
    private void EndOverride(bool notify)
    {
        if (!Overriding) return;
        _overrideUntil = null;
        UpdateStatusUi();

        ResumeAutomationSoft();

        if (notify && _s.NotifyOverride) Notify(Loc.T("n.override.end.title"), Loc.T("n.override.end"));
    }

    /// <summary>Zeitplan und Programmregeln wieder anwenden, weich eingeblendet.</summary>
    private void ResumeAutomationSoft()
    {
        _overrideEnding = true;
        try
        {
            _sched.Reset();   // aktuellen Zeitplan-Eintrag anwenden (OnEntryDue blendet weich ein)
            EvaluateRules();  // danach ggf. die Regel des Vordergrundprogramms
        }
        finally { _overrideEnding = false; }
    }

    /// <summary>Text mit Endzeit oder (bei „bis Neustart“) die Variante ohne Uhrzeit.</summary>
    private string OverrideText(string key) =>
        _overrideUntil == DateTime.MaxValue ? Loc.T(key + ".restart") : Loc.T(key, _overrideUntil!.Value.ToString("HH:mm"));

    /// <summary>Hauptschalter: Automatik (Zeitplan + Programmregeln) komplett aus/an, ohne Einstellungen zu verlieren.</summary>
    private void SetAutomation(bool on)
    {
        if (_s.AutomationEnabled == on) return;
        _s.AutomationEnabled = on;
        _autoWas = on;
        _autoItem.Checked = on;
        _dirty = true;

        if (!on)
        {
            _overrideUntil = null;
            CancelRamp();
            LeaveRule(false, notify: false);   // aktuelle Werte bleiben, wie sie sind
            UpdateStatusUi();
            ShowOsd("⏻", Loc.T("osd.auto.off"), 0);
        }
        else
        {
            UpdateStatusUi();
            ResumeAutomationSoft();
            ShowOsd("⏻", Loc.T("osd.auto.on"), 1);
        }
    }

    // ---------- Pause ----------

    private void Pause(int minutes)
    {
        CancelRamp();
        CancelKelvinFade();
        _pausedUntil = DateTime.Now.AddMinutes(minutes);
        ApplyColor();
        UpdatePauseUi();
        ShowOsd("❚❚", _pausedUntil.Value.ToString("HH:mm"), 0);
    }

    private void Resume()
    {
        _pausedUntil = null;
        UpdatePauseUi();
        ApplyColor();
        ShowKelvinOsd();
    }

    private void UpdatePauseUi() => UpdateStatusUi();

    /// <summary>Menüeinträge und Tray-Tooltip für Pause und manuellen Vorrang.</summary>
    private void UpdateStatusUi()
    {
        _resumeItem.Enabled = Paused;
        _pauseItem.Checked = Paused;
        _overrideItem.Visible = Overriding;
        if (Overriding) _overrideItem.Text = OverrideText("tray.override");
        _tray.Text = Paused ? Loc.T("tip.paused", _pausedUntil!.Value.ToString("HH:mm"))
            : !_s.AutomationEnabled ? Loc.T("tip.autooff")
            : Overriding ? OverrideText("tip.override")
            : "BrightnessTray";
    }

    // ---------- Farbe ----------

    /// <summary>Zielzustand der Farbtemperatur (Regel hat Vorrang vor Einstellung), ohne Pause und ohne Überblendung.</summary>
    private int StateKelvin => _ruleKelvin ?? (_s.FilterEnabled ? _s.Kelvin : ColorController.MaxKelvin);

    /// <summary>Tatsächlich angezeigte Farbtemperatur.</summary>
    private int EffectiveKelvin => Paused ? ColorController.MaxKelvin : _fadeKelvin ?? StateKelvin;

    private void ApplyColor()
    {
        _color.Apply(EffectiveKelvin);
        _filterItem.Checked = _s.FilterEnabled;
    }

    // ---------- Takt, Einstellungen & Systemereignisse ----------

    private void OnTick()
    {
        // Farbtabelle erneut setzen, falls ein Spiel o. Ä. sie zurückgesetzt hat
        if (EffectiveKelvin < ColorController.MaxKelvin) _color.Reapply();
        if (_overrideUntil != null && DateTime.Now >= _overrideUntil) EndOverride(true);
        if (_pausedUntil != null && DateTime.Now >= _pausedUntil)
        {
            Resume();
            if (_s.NotifyPause) Notify(Loc.T("n.pause.end.title"), Loc.T("n.pause.end"));
        }
        if (_dirty) { _s.Save(); _dirty = false; }
    }

    private void ApplyLanguage()
    {
        _autoItem.Text = Loc.T("tray.auto");
        _settingsItem.Text = Loc.T("tray.settings");
        _filterItem.Text = Loc.T("tray.filter");
        _pauseItem.Text = Loc.T("tray.pause");
        _notifyItem.Text = Loc.T("tray.notify");
        string[] keys = { "pause.15", "pause.30", "pause.60", "pause.120" };
        for (int i = 0; i < _pauseOptions.Length; i++) _pauseOptions[i].Text = Loc.T(keys[i]);
        _resumeItem.Text = Loc.T("pause.resume");
        _autostartItem.Text = Loc.T("tray.autostart");
        _exitItem.Text = Loc.T("tray.exit");
        UpdatePauseUi();
    }

    private void RefreshTheme()
    {
        Theme.Refresh(_s.Theme);
        Theme.ApplyMenu(_menu);
        if (_settingsForm is { IsDisposed: false }) Theme.Apply(_settingsForm);
    }

    private void OpenSettings()
    {
        if (_settingsForm is { IsDisposed: false })
        {
            _settingsForm.Activate();
            return;
        }
        _settingsForm = new SettingsForm(_s, _input, _color, OnSettingsSaved);
        _settingsForm.FormClosed += (_, _) => ApplyColor(); // Vorschau beenden (berücksichtigt Pause)
        _settingsForm.Show();
    }

    private void OnSettingsSaved()
    {
        Loc.Set(_s.Language);
        _notifyItem.Checked = _s.NotifyEnabled;
        RefreshTheme();
        ApplyLanguage();
        _autoItem.Checked = _s.AutomationEnabled;
        if (_autoWas && !_s.AutomationEnabled) { _overrideUntil = null; LeaveRule(false, notify: false); }
        _autoWas = _s.AutomationEnabled;
        UpdateStatusUi();
        _comboCurrent = _s.ComboRules.Count > 0 ? FindCombo(WindowScanner.VisibleProcesses()) : null;   // Liste wurde ersetzt
        _comboSeen = _comboCurrent;
        if (!_s.OverrideEnabled) EndOverride(false);
        else if (Overriding) UpdateStatusUi();   // Sprache evtl. geändert
        LeaveRule(true, notify: false);   // Regeln könnten sich geändert haben: still zurücksetzen ...
        ApplyColor();
        _sched.Reset();
        EvaluateRules();       // ... und für das aktuelle Vordergrundprogramm neu auswerten
    }

    private void OnDisplayChanged(object? sender, EventArgs e) =>
        _ui.Post(_ => { _hub.Refresh(); ApplyColor(); }, null);

    private void OnPowerChanged(object? sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode != PowerModes.Resume) return;
        _ui.Post(_ => { _hub.Refresh(); ApplyColor(); _sched.Check(); }, null);
    }

    private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category is UserPreferenceCategory.General or UserPreferenceCategory.Color)
            _ui.Post(_ => RefreshTheme(), null);
    }

    protected override void ExitThreadCore()
    {
        SystemEvents.DisplaySettingsChanged -= OnDisplayChanged;
        SystemEvents.PowerModeChanged -= OnPowerChanged;
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        LeaveRule(_s.RulesRestoreOnLeave, synchronous: true);
        _s.Save();
        _watcher.Dispose();
        _comboTimer.Dispose();
        _ramp.Dispose();
        _fader.Dispose();
        _mouse.Dispose();
        _input.Dispose();
        _sched.Dispose();
        _color.Dispose();        // Farben zurücksetzen
        _tray.Visible = false;
        _tray.Dispose();
        _flyout.Dispose();
        _osd.Dispose();
        _hub.Dispose();
        base.ExitThreadCore();
    }

    // ---------- Tray-Icon & Autostart ----------

    private static Icon CreateSunIcon()
    {
        using var bmp = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var pen = new Pen(Color.White, 2);
            for (int i = 0; i < 8; i++)
            {
                double a = i * Math.PI / 4;
                g.DrawLine(pen,
                    16 + (float)(Math.Cos(a) * 9), 16 + (float)(Math.Sin(a) * 9),
                    16 + (float)(Math.Cos(a) * 14), 16 + (float)(Math.Sin(a) * 14));
            }
            g.FillEllipse(Brushes.White, 9, 9, 14, 14);
        }
        return Icon.FromHandle(bmp.GetHicon());
    }

    private static bool IsAutostart()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue(AppName) != null;
    }

    private static void SetAutostart(bool enable)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
        if (key == null) return;
        if (enable) key.SetValue(AppName, $"\"{Environment.ProcessPath}\"");
        else key.DeleteValue(AppName, false);
    }
}
