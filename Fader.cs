namespace BrightnessTray;

/// <summary>
/// Weicher Wechsel (Soft-Switch): Blendet Helligkeit und Farbtemperatur unabhängig voneinander mit je eigener
/// Dauer zum Zielwert über. Sanfter Start und Auslauf (Smoothstep), Farbtemperatur in 100-K-Schritten.
/// Läuft auf dem UI-Thread; die Rückrufe müssen schnell zurückkehren.
/// </summary>
internal sealed class Fader : IDisposable
{
    private sealed class Channel
    {
        public bool Active;
        public double From, To, Seconds;
        public DateTime Start;
        public int Last = int.MinValue;
    }

    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 40 };
    private readonly Channel _brightness = new(), _kelvin = new();
    private readonly Action<int> _applyBrightness, _applyKelvin;
    private readonly Action _doneBrightness, _doneKelvin;

    public Fader(Action<int> applyBrightness, Action doneBrightness, Action<int> applyKelvin, Action doneKelvin)
    {
        _applyBrightness = applyBrightness;
        _doneBrightness = doneBrightness;
        _applyKelvin = applyKelvin;
        _doneKelvin = doneKelvin;
        _timer.Tick += (_, _) => Tick();
    }

    public void StartBrightness(double from, double to, double seconds) => Start(_brightness, from, to, seconds);
    public void StartKelvin(double from, double to, double seconds) => Start(_kelvin, from, to, seconds);

    /// <summary>Bricht ab, ohne den "fertig"-Rückruf auszulösen.</summary>
    public void CancelBrightness() { _brightness.Active = false; StopIfIdle(); }
    public void CancelKelvin() { _kelvin.Active = false; StopIfIdle(); }

    private void Start(Channel c, double from, double to, double seconds)
    {
        c.From = from;
        c.To = to;
        c.Seconds = Math.Max(0.05, seconds);
        c.Start = DateTime.Now;
        c.Last = int.MinValue;
        c.Active = true;
        _timer.Start();
    }

    private void Tick()
    {
        Step(_brightness, v => (int)Math.Round(v), _applyBrightness, _doneBrightness);
        Step(_kelvin, v => (int)(Math.Round(v / 100.0) * 100), _applyKelvin, _doneKelvin);
        StopIfIdle();
    }

    private static void Step(Channel c, Func<double, int> quantize, Action<int> apply, Action done)
    {
        if (!c.Active) return;

        double t = Math.Clamp((DateTime.Now - c.Start).TotalSeconds / c.Seconds, 0, 1);
        double eased = t * t * (3 - 2 * t); // Smoothstep: weich anlaufen, weich auslaufen
        int value = quantize(t >= 1 ? c.To : c.From + (c.To - c.From) * eased);

        if (value != c.Last)
        {
            c.Last = value;
            apply(value);
        }
        if (t >= 1)
        {
            c.Active = false;
            done();
        }
    }

    private void StopIfIdle()
    {
        if (!_brightness.Active && !_kelvin.Active) _timer.Stop();
    }

    public void Dispose() => _timer.Dispose();
}
