using System.Management;

namespace BrightnessTray;

/// <summary>Ein Monitor, dessen Helligkeit in Prozent (0-100) gelesen/gesetzt werden kann.</summary>
internal abstract class BrightnessMonitor : IDisposable
{
    public string Name { get; protected set; } = "Monitor";
    public int Percent { get; protected set; }
    public abstract void SetPercent(int percent);
    public virtual void Dispose() { }
}

/// <summary>Externer Monitor über DDC/CI (dxva2.dll).</summary>
internal sealed class DdcMonitor : BrightnessMonitor
{
    private readonly IntPtr _handle;
    private readonly uint _min, _max;

    private DdcMonitor(IntPtr handle, string name, uint min, uint cur, uint max)
    {
        _handle = handle; _min = min; _max = max;
        Name = name;
        Percent = ToPercent(cur);
    }

    private int ToPercent(uint raw) =>
        _max == _min ? 0 : (int)Math.Round((raw - _min) * 100.0 / (_max - _min));

    public override void SetPercent(int percent)
    {
        percent = Math.Clamp(percent, 0, 100);
        uint raw = (uint)Math.Round(_min + percent * (_max - _min) / 100.0);
        if (Native.SetMonitorBrightness(_handle, raw)) Percent = percent;
    }

    public override void Dispose() => Native.DestroyPhysicalMonitor(_handle);

    public static List<BrightnessMonitor> Enumerate()
    {
        var result = new List<BrightnessMonitor>();
        Native.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hMon, IntPtr _, ref Native.RECT __, IntPtr ___) =>
        {
            if (!Native.GetNumberOfPhysicalMonitorsFromHMONITOR(hMon, out uint n) || n == 0) return true;
            var arr = new Native.PHYSICAL_MONITOR[n];
            if (!Native.GetPhysicalMonitorsFromHMONITOR(hMon, n, arr)) return true;

            foreach (var pm in arr)
            {
                if (Native.GetMonitorBrightness(pm.hPhysicalMonitor, out uint min, out uint cur, out uint max))
                    result.Add(new DdcMonitor(pm.hPhysicalMonitor, pm.szPhysicalMonitorDescription, min, cur, max));
                else
                    Native.DestroyPhysicalMonitor(pm.hPhysicalMonitor); // kein DDC/CI-Support
            }
            return true;
        }, IntPtr.Zero);
        return result;
    }
}

/// <summary>Eingebautes Laptop-Display über WMI.</summary>
internal sealed class WmiMonitor : BrightnessMonitor
{
    private WmiMonitor(string name, int percent) { Name = name; Percent = percent; }

    public override void SetPercent(int percent)
    {
        percent = Math.Clamp(percent, 0, 100);
        using var searcher = new ManagementObjectSearcher(@"root\WMI", "SELECT * FROM WmiMonitorBrightnessMethods");
        foreach (ManagementObject mo in searcher.Get())
        {
            mo.InvokeMethod("WmiSetBrightness", new object[] { (uint)1, (byte)percent });
            Percent = percent;
        }
    }

    public static WmiMonitor? TryCreate()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(@"root\WMI", "SELECT * FROM WmiMonitorBrightness");
            foreach (ManagementObject mo in searcher.Get())
                return new WmiMonitor("Integriertes Display", Convert.ToInt32(mo["CurrentBrightness"]));
        }
        catch { /* kein Laptop-Display / keine WMI-Unterstützung */ }
        return null;
    }
}

internal static class MonitorService
{
    public static List<BrightnessMonitor> DetectAll()
    {
        var list = new List<BrightnessMonitor>();
        var internalPanel = WmiMonitor.TryCreate();
        if (internalPanel != null) list.Add(internalPanel);
        list.AddRange(DdcMonitor.Enumerate());
        return list;
    }
}
