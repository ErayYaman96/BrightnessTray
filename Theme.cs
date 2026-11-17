using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace BrightnessTray;

/// <summary>Hell/Dunkel-Design für die eigenen Fenster (WinForms bringt unter .NET 8 keinen Dark Mode mit).</summary>
internal static class Theme
{
    public static bool Dark { get; private set; }
    public static Color Back { get; private set; } = SystemColors.Control;
    public static Color Fore { get; private set; } = SystemColors.ControlText;
    public static Color Input { get; private set; } = SystemColors.Window;
    public static Color Border { get; private set; } = SystemColors.ControlDark;
    public static Color Muted { get; private set; } = SystemColors.GrayText;
    public static Color ButtonBack { get; private set; } = SystemColors.Control;
    public static Color Accent { get; private set; } = SystemColors.Highlight;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
    private static extern int SetWindowTheme(IntPtr hwnd, string? subAppName, string? subIdList);

    /// <summary>mode: "system", "light" oder "dark".</summary>
    public static void Refresh(string mode)
    {
        Dark = mode == "dark" || (mode != "light" && SystemUsesDark());
        if (Dark)
        {
            Back = Color.FromArgb(32, 32, 32);
            Fore = Color.FromArgb(240, 240, 240);
            Input = Color.FromArgb(45, 45, 45);
            Border = Color.FromArgb(85, 85, 85);
            Muted = Color.FromArgb(160, 160, 160);
            ButtonBack = Color.FromArgb(55, 55, 55);
            Accent = Color.FromArgb(0, 120, 212);
        }
        else
        {
            Back = SystemColors.Control;
            Fore = SystemColors.ControlText;
            Input = SystemColors.Window;
            Border = SystemColors.ControlDark;
            Muted = SystemColors.GrayText;
            ButtonBack = SystemColors.Control;
            Accent = SystemColors.Highlight;
        }
    }

    private static bool SystemUsesDark()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int v && v == 0;
        }
        catch { return false; }
    }

    // ---------- Anwenden ----------

    public static void Apply(Control c)
    {
        if (c is Form f)
        {
            f.BackColor = Back;
            f.ForeColor = Fore;
            OnHandle(f, () =>
            {
                int v = Dark ? 1 : 0;
                DwmSetWindowAttribute(f.Handle, 20, ref v, sizeof(int)); // dunkle Titelleiste
            });
        }
        else
        {
            ApplyOne(c);
        }

        if (c is DataGridView or NumericUpDown or ComboBox) return; // keine Kind-Steuerelemente anfassen
        foreach (Control child in c.Controls) Apply(child);
    }

    private static void ApplyOne(Control c)
    {
        switch (c)
        {
            case Button b:
                b.FlatStyle = Dark ? FlatStyle.Flat : FlatStyle.Standard;
                if (Dark) b.BackColor = ButtonBack; else b.UseVisualStyleBackColor = true;
                b.ForeColor = Fore;
                b.FlatAppearance.BorderColor = Border;
                break;
            case TextBox t:
                t.BackColor = Input; t.ForeColor = Fore;
                t.BorderStyle = Dark ? BorderStyle.FixedSingle : BorderStyle.Fixed3D;
                break;
            case NumericUpDown n:
                n.BackColor = Input; n.ForeColor = Fore;
                NativeTheme(n);
                break;
            case ComboBox cb:
                cb.BackColor = Input; cb.ForeColor = Fore;
                cb.FlatStyle = Dark ? FlatStyle.Flat : FlatStyle.Standard;
                NativeTheme(cb);
                break;
            case DataGridView g:
                StyleGrid(g);
                NativeTheme(g);
                break;
            case TrackBar tb:
                tb.BackColor = Back;
                break;
            case Label l when l.Tag as string == "hint":
                l.BackColor = Back; l.ForeColor = Muted;
                break;
            case Panel p when p.Tag as string == "divider":
                p.BackColor = Border;
                break;
            default:
                c.BackColor = Back; c.ForeColor = Fore;
                if (c is ScrollableControl { AutoScroll: true }) NativeTheme(c); // dunkle Scrollleiste
                break;
        }
    }

    private static void StyleGrid(DataGridView g)
    {
        g.BackgroundColor = Input;
        g.GridColor = Border;
        g.EnableHeadersVisualStyles = !Dark;
        g.DefaultCellStyle.BackColor = Input;
        g.DefaultCellStyle.ForeColor = Fore;
        g.DefaultCellStyle.SelectionBackColor = Accent;
        g.DefaultCellStyle.SelectionForeColor = Color.White;
        g.ColumnHeadersDefaultCellStyle.BackColor = ButtonBack;
        g.ColumnHeadersDefaultCellStyle.ForeColor = Fore;
        g.ColumnHeadersDefaultCellStyle.SelectionBackColor = ButtonBack;
        g.ColumnHeadersDefaultCellStyle.SelectionForeColor = Fore;
    }

    private static void NativeTheme(Control c) =>
        OnHandle(c, () => SetWindowTheme(c.Handle, Dark ? "DarkMode_Explorer" : null, null));

    private static void OnHandle(Control c, Action action)
    {
        if (c.IsHandleCreated) action();
        else c.HandleCreated += (_, _) => action();
    }

    // ---------- Kontextmenü ----------

    public static void ApplyMenu(ContextMenuStrip menu)
    {
        menu.Renderer = Dark
            ? new ToolStripProfessionalRenderer(new DarkColors())
            : new ToolStripProfessionalRenderer();
        menu.ForeColor = Dark ? Fore : SystemColors.ControlText;
        menu.BackColor = Dark ? Color.FromArgb(43, 43, 43) : SystemColors.Control;
    }

    private sealed class DarkColors : ProfessionalColorTable
    {
        private static readonly Color Bg = Color.FromArgb(43, 43, 43);
        private static readonly Color Sel = Color.FromArgb(70, 70, 70);
        private static readonly Color Line = Color.FromArgb(85, 85, 85);

        public override Color ToolStripDropDownBackground => Bg;
        public override Color ImageMarginGradientBegin => Bg;
        public override Color ImageMarginGradientMiddle => Bg;
        public override Color ImageMarginGradientEnd => Bg;
        public override Color MenuBorder => Line;
        public override Color MenuItemBorder => Sel;
        public override Color MenuItemSelected => Sel;
        public override Color MenuItemSelectedGradientBegin => Sel;
        public override Color MenuItemSelectedGradientEnd => Sel;
        public override Color MenuItemPressedGradientBegin => Sel;
        public override Color MenuItemPressedGradientMiddle => Sel;
        public override Color MenuItemPressedGradientEnd => Sel;
        public override Color SeparatorDark => Line;
        public override Color SeparatorLight => Bg;
        public override Color CheckBackground => Sel;
        public override Color CheckSelectedBackground => Sel;
        public override Color CheckPressedBackground => Sel;
    }
}
