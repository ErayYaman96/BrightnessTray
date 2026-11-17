using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace BrightnessTray;

/// <summary>Kleine Einblendung unten in der Bildschirmmitte (wie die Lautstärkeanzeige), ohne Fokus zu stehlen.</summary>
internal sealed class OsdForm : Form
{
    private const int WS_EX_TOPMOST = 0x8, WS_EX_TOOLWINDOW = 0x80, WS_EX_NOACTIVATE = 0x08000000;

    private readonly System.Windows.Forms.Timer _hide = new();
    private string _glyph = "☀";
    private string _text = "";
    private double _frac;

    public OsdForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        DoubleBuffered = true;
        Size = new Size(300, 64);
        _hide.Tick += (_, _) => { _hide.Stop(); Hide(); };
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_TOPMOST;
            return cp;
        }
    }

    public void ShowOsd(string glyph, string text, double fraction, int milliseconds)
    {
        _glyph = glyph;
        _text = text;
        _frac = Math.Clamp(fraction, 0, 1);
        BackColor = Theme.Dark ? Color.FromArgb(38, 38, 38) : Color.FromArgb(246, 246, 246);

        float s = DeviceDpi / 96f;
        Size = new Size((int)(300 * s), (int)(64 * s));
        using (var path = RoundRect(new RectangleF(0, 0, Width, Height), 16 * s))
            Region = new Region(path);

        var wa = Screen.PrimaryScreen!.WorkingArea;
        Location = new Point(wa.Left + (wa.Width - Width) / 2, wa.Bottom - Height - (int)(56 * s));

        if (!Visible) Show();
        Invalidate();
        _hide.Stop();
        _hide.Interval = Math.Max(300, milliseconds);
        _hide.Start();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        float s = DeviceDpi / 96f;

        var fore = Theme.Dark ? Color.White : Color.FromArgb(32, 32, 32);
        var trackColor = Theme.Dark ? Color.FromArgb(80, 80, 80) : Color.FromArgb(205, 205, 205);
        using var foreBrush = new SolidBrush(fore);
        using var trackBrush = new SolidBrush(trackColor);
        using var center = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        using var right = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center };

        using (var glyphFont = new Font("Segoe UI Symbol", 17))
            g.DrawString(_glyph, glyphFont, foreBrush, new RectangleF(8 * s, 0, 46 * s, Height), center);

        float barX = 60 * s, barW = Width - 60 * s - 78 * s, barH = 6 * s, barY = Height / 2f - barH / 2;
        using (var track = RoundRect(new RectangleF(barX, barY, barW, barH), barH / 2))
            g.FillPath(trackBrush, track);
        if (_frac > 0)
            using (var fill = RoundRect(new RectangleF(barX, barY, Math.Max(barH, (float)(barW * _frac)), barH), barH / 2))
                g.FillPath(foreBrush, fill);

        using var textFont = new Font("Segoe UI", 12, FontStyle.Bold);
        g.DrawString(_text, textFont, foreBrush, new RectangleF(Width - 74 * s, 0, 66 * s, Height), right);
    }

    private static GraphicsPath RoundRect(RectangleF r, float radius)
    {
        float d = radius * 2;
        var p = new GraphicsPath();
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }
}
