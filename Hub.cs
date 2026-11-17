namespace BrightnessTray;

/// <summary>Gemeinsame Monitorliste für Popup, Tastenkürzel und Zeitplan (thread-sicher).</summary>
internal sealed class MonitorHub : IDisposable
{
    private readonly object _lock = new();
    private List<BrightnessMonitor> _monitors = new();

    public void Refresh()
    {
        lock (_lock)
        {
            foreach (var m in _monitors) m.Dispose();
            _monitors = MonitorService.DetectAll();
        }
    }

    /// <summary>Momentaufnahme: Name + aktueller Wert.</summary>
    public List<(BrightnessMonitor Monitor, string Name, int Percent)> Snapshot()
    {
        lock (_lock) return _monitors.Select(m => (m, m.Name, m.Percent)).ToList();
    }

    public void Set(BrightnessMonitor m, int percent)
    {
        lock (_lock) m.SetPercent(percent);
    }

    /// <summary>Alle Monitore auf denselben Wert setzen.</summary>
    public void SetAll(int percent)
    {
        lock (_lock)
            foreach (var m in _monitors) m.SetPercent(Math.Clamp(percent, 0, 100));
    }

    /// <summary>Alle Monitore relativ verstellen. Gibt den Durchschnittswert zurück (-1 = keine Monitore).</summary>
    public int Step(int delta)
    {
        lock (_lock)
        {
            if (_monitors.Count == 0) return -1;
            foreach (var m in _monitors) m.SetPercent(Math.Clamp(m.Percent + delta, 0, 100));
            return (int)_monitors.Average(m => m.Percent);
        }
    }

    public void Dispose()
    {
        lock (_lock) { foreach (var m in _monitors) m.Dispose(); _monitors.Clear(); }
    }
}
