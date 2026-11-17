using System.Drawing;
using System.Windows.Forms;

namespace BrightnessTray;

/// <summary>Kleines Popup mit je einem Regler pro Monitor (öffnet sich über dem Tray-Icon).</summary>
internal sealed class FlyoutForm : Form
{
    private readonly MonitorHub _hub;
    private readonly System.Windows.Forms.Timer _debounce = new() { Interval = 60 };
    private readonly Dictionary<BrightnessMonitor, int> _pending = new();

    /// <summary>Der Nutzer hat per Regler Helligkeit verstellt (z. B. um einen Übergang abzubrechen).</summary>
    public event Action? UserAdjusted;

    public FlyoutForm(MonitorHub hub)
    {
        _hub = hub;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;

        Deactivate += (_, _) => Hide();
        _debounce.Tick += (_, _) => FlushPending();
    }

    public void ShowFlyout()
    {
        Rebuild();
        PositionNearTray();
        Show();
        Activate();
    }

    private void Rebuild()
    {
        _hub.Refresh();
        var monitors = _hub.Snapshot();

        SuspendLayout();
        Controls.Clear();

        int y = 12;
        if (monitors.Count == 0)
        {
            Controls.Add(new Label
            {
                Text = Loc.T("flyout.none"), AutoSize = true, Left = 14, Top = y, Tag = "hint"
            });
            y += 50;
        }

        foreach (var (mon, name, percent) in monitors)
        {
            var title = new Label { Text = name, AutoSize = true, Left = 14, Top = y, Tag = "hint" };
            var value = new Label { Text = percent + " %", AutoSize = true, Top = y, Left = 260 };
            var bar = new TrackBar
            {
                Minimum = 0, Maximum = 100, TickStyle = TickStyle.None,
                Value = Math.Clamp(percent, 0, 100),
                Left = 8, Top = y + 20, Width = 300, Height = 32
            };
            bar.ValueChanged += (_, _) =>
            {
                value.Text = bar.Value + " %";
                _pending[mon] = bar.Value;
                _debounce.Stop(); _debounce.Start();
            };
            Controls.AddRange(new Control[] { title, value, bar });
            y += 62;
        }

        ClientSize = new Size(322, y + 6);
        Theme.Apply(this);
        ResumeLayout();
    }

    private void FlushPending()
    {
        _debounce.Stop();
        var work = _pending.ToList();
        _pending.Clear();
        UserAdjusted?.Invoke();
        // DDC/CI ist langsam -> nicht auf dem UI-Thread
        Task.Run(() => { foreach (var (mon, val) in work) _hub.Set(mon, val); });
    }

    private void PositionNearTray()
    {
        var area = Screen.FromPoint(Cursor.Position).WorkingArea;
        Location = new Point(area.Right - Width - 12, area.Bottom - Height - 12);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var pen = new Pen(Theme.Border);
        e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
    }
}
