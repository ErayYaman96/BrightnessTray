using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace BrightnessTray;

/// <summary>Blaulichtfilter über die Gamma-Tabelle der Grafikkarte (wie f.lux / Redshift).</summary>
internal sealed class ColorController : IDisposable
{
    public const int MinKelvin = 2500, MaxKelvin = 6500;

    [DllImport("gdi32.dll", EntryPoint = "CreateDCW", CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateDC(string? driver, string device, string? port, IntPtr initData);
    [DllImport("gdi32.dll")]
    private static extern bool DeleteDC(IntPtr hdc);
    [DllImport("gdi32.dll")]
    private static extern bool SetDeviceGammaRamp(IntPtr hdc, [In] ushort[] ramp);

    public int Kelvin { get; private set; } = MaxKelvin;

    /// <summary>Farbtemperatur setzen (6500 = neutral / Filter aus).</summary>
    public void Apply(int kelvin)
    {
        Kelvin = Math.Clamp(kelvin, MinKelvin, MaxKelvin);
        Reapply();
    }

    /// <summary>Erneut anwenden, z. B. nach Display-Wechsel oder wenn ein Spiel die Tabelle zurückgesetzt hat.</summary>
    public void Reapply()
    {
        foreach (var screen in Screen.AllScreens)
        {
            IntPtr dc = CreateDC("DISPLAY", screen.DeviceName, null, IntPtr.Zero);
            if (dc == IntPtr.Zero) continue;
            try { SetRamp(dc, Kelvin); }
            finally { DeleteDC(dc); }
        }
    }

    private static void SetRamp(IntPtr dc, int kelvin)
    {
        var (fr, fg, fb) = Factors(kelvin);

        // Windows lehnt extreme Tabellen ab -> notfalls schrittweise abschwächen
        foreach (double strength in new[] { 1.0, 0.8, 0.6, 0.4, 0.2, 0.0 })
        {
            double r = 1 - strength * (1 - fr), g = 1 - strength * (1 - fg), b = 1 - strength * (1 - fb);
            var ramp = new ushort[768];
            for (int i = 0; i < 256; i++)
            {
                double v = i * 257.0;
                ramp[i] = (ushort)(v * r);
                ramp[256 + i] = (ushort)(v * g);
                ramp[512 + i] = (ushort)(v * b);
            }
            if (SetDeviceGammaRamp(dc, ramp)) return;
        }
    }

    private static (double R, double G, double B) Factors(int kelvin)
    {
        if (kelvin >= MaxKelvin) return (1, 1, 1);
        var (r, g, b) = Rgb(kelvin);
        var (r0, g0, b0) = Rgb(MaxKelvin);
        return (Math.Min(1, r / r0), Math.Min(1, g / g0), Math.Min(1, b / b0));
    }

    // Näherung Kelvin -> RGB (Tanner Helland)
    private static (double R, double G, double B) Rgb(double kelvin)
    {
        double t = kelvin / 100.0, r, g, b;
        if (t <= 66)
        {
            r = 255;
            g = 99.4708025861 * Math.Log(t) - 161.1195681661;
            b = t <= 19 ? 0 : 138.5177312231 * Math.Log(t - 10) - 305.0447927307;
        }
        else
        {
            r = 329.698727446 * Math.Pow(t - 60, -0.1332047592);
            g = 288.1221695283 * Math.Pow(t - 60, -0.0755148492);
            b = 255;
        }
        static double C(double x) => Math.Clamp(x, 0, 255);
        return (C(r), C(g), C(b));
    }

    public void Dispose() => Apply(MaxKelvin); // beim Beenden Farben zurücksetzen
}
