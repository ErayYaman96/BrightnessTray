using System.Globalization;

namespace BrightnessTray;

/// <summary>
/// Prüft alle 30 s, welcher Zeitplan-Eintrag zuletzt fällig war, und meldet ihn genau einmal.
/// So greift auch nach einem Neustart um 21 Uhr noch der 20-Uhr-Eintrag.
/// </summary>
internal sealed class Scheduler : IDisposable
{
    private readonly Settings _s;
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 30_000 };
    private string? _lastKey;

    /// <summary>(Helligkeit %, Kelvin, Zeitpunkt des Eintrags, sofort anwenden statt sanft überblenden)</summary>
    public event Action<int, int, DateTime, bool>? EntryDue;
    /// <summary>Bei jedem Takt (z. B. um Farbtabelle erneut zu setzen).</summary>
    public event Action? Tick;

    public Scheduler(Settings s)
    {
        _s = s;
        _timer.Tick += (_, _) => Check();
        _timer.Start();
    }

    /// <summary>Nach Änderung der Einstellungen: aktuellen Eintrag neu (sofort) anwenden.</summary>
    public void Reset()
    {
        _lastKey = null;
        Check();
    }

    public void Check()
    {
        Tick?.Invoke();
        if (!_s.ScheduleEnabled) return;

        var now = DateTime.Now;
        var due = Build(now.Date.AddDays(-1)).Concat(Build(now.Date))
            .Where(e => e.When <= now)
            .OrderBy(e => e.When)
            .ToList();
        if (due.Count == 0) return;

        var current = due[^1];
        string key = current.When.ToString("o");
        if (key == _lastKey) return;

        bool first = _lastKey == null;   // Programmstart / Reset: sofort, ohne Überblendung
        _lastKey = key;
        EntryDue?.Invoke(current.Percent, current.Kelvin, current.When, first);
    }

    private List<(DateTime When, int Percent, int Kelvin)> Build(DateTime day)
    {
        var list = new List<(DateTime, int, int)>();
        if (_s.UseSun)
        {
            var (rise, set) = Sun.Times(day, _s.Latitude, _s.Longitude);
            if (rise != null) list.Add((rise.Value, _s.DayPercent, _s.DayKelvin));
            if (set != null) list.Add((set.Value.AddMinutes(_s.SunsetOffsetMin), _s.NightPercent, _s.NightKelvin));
        }
        else
        {
            foreach (var e in _s.Entries)
                if (DateTime.TryParseExact(e.Time, new[] { "H:mm", "HH:mm" }, CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out var t))
                    list.Add((day.Date + t.TimeOfDay, e.Percent, e.Kelvin));
        }
        return list;
    }

    public void Dispose() => _timer.Dispose();
}
