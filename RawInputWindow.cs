using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace BrightnessTray;

/// <summary>
/// Lauscht per Raw Input auf HID-"Consumer Control"-Tasten (Medien-/Helligkeitstasten), die als
/// Fn-Funktion von Tastaturen kommen und keinen normalen Tastencode erzeugen.
/// Raw Input kann Tasten nur mitlesen, nicht verschlucken.
/// </summary>
internal sealed class RawInputWindow : NativeWindow, IDisposable
{
    private const int WM_INPUT = 0x00FF;
    private const uint RID_INPUT = 0x10000003;
    private const uint RIDEV_INPUTSINK = 0x00000100;
    private const uint RIM_TYPEHID = 2;

    [StructLayout(LayoutKind.Sequential)]
    private struct RAWINPUTDEVICE { public ushort UsagePage; public ushort Usage; public uint Flags; public IntPtr Target; }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterRawInputDevices([In] RAWINPUTDEVICE[] devices, uint count, uint size);
    [DllImport("user32.dll")]
    private static extern uint GetRawInputData(IntPtr hRawInput, uint command, IntPtr data, ref uint size, uint headerSize);

    /// <summary>Hex-Signatur des gedrückten HID-Reports (nur Tastendruck, nicht Loslassen).</summary>
    public event Action<string>? HidReport;

    /// <summary>Wird gemeldet, wenn die Medientaste losgelassen wird (Report ohne Nutzdaten).</summary>
    public event Action? HidReleased;

    public RawInputWindow()
    {
        CreateHandle(new CreateParams());
        var devices = new[]
        {
            new RAWINPUTDEVICE { UsagePage = 0x0C, Usage = 0x01, Flags = RIDEV_INPUTSINK, Target = Handle }
        };
        RegisterRawInputDevices(devices, (uint)devices.Length, (uint)Marshal.SizeOf<RAWINPUTDEVICE>());
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_INPUT) HandleInput(m.LParam);
        base.WndProc(ref m);
    }

    private void HandleInput(IntPtr hRaw)
    {
        uint headerSize = (uint)(8 + 2 * IntPtr.Size); // RAWINPUTHEADER
        uint size = 0;
        GetRawInputData(hRaw, RID_INPUT, IntPtr.Zero, ref size, headerSize);
        if (size == 0) return;

        IntPtr buffer = Marshal.AllocHGlobal((int)size);
        try
        {
            if (GetRawInputData(hRaw, RID_INPUT, buffer, ref size, headerSize) != size) return;
            if ((uint)Marshal.ReadInt32(buffer) != RIM_TYPEHID) return;

            int off = (int)headerSize;
            int sizeHid = Marshal.ReadInt32(buffer, off);
            int count = Marshal.ReadInt32(buffer, off + 4);
            for (int i = 0; i < count; i++)
            {
                var bytes = new byte[sizeHid];
                Marshal.Copy(buffer + off + 8 + i * sizeHid, bytes, 0, sizeHid);
                // Byte 0 = Report-ID; sind alle übrigen Bytes 0, war es nur das Loslassen
                if (bytes.Length < 2) continue;
                if (bytes.Skip(1).All(b => b == 0)) { HidReleased?.Invoke(); continue; }
                HidReport?.Invoke(Convert.ToHexString(bytes));
            }
        }
        finally { Marshal.FreeHGlobal(buffer); }
    }

    public void Dispose()
    {
        if (Handle != IntPtr.Zero) DestroyHandle();
    }
}
