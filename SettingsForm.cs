using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace BrightnessTray;

/// <summary>Einstellungen auf einer einzigen Seite (nur vertikales Scrollen), gegliedert in Abschnitte.</summary>
internal sealed class SettingsForm : Form
{
    private const int HintWidth = 660;

    private readonly Settings _s;
    private readonly InputManager _input;
    private readonly ColorController _color;
    private readonly Action _saved;

    // Allgemein
    private readonly CheckBox _hotkeysOn = Chk("g.hotkeys");
    private readonly NumericUpDown _step = Num(1, 25, 1, 0, 70);
    private readonly CheckBox _osdOn = Chk("g.osd");
    private readonly NumericUpDown _osdMs = Num(500, 5000, 100, 0, 80);
    private readonly CheckBox _trayWheel = Chk("g.wheel");
    private readonly ComboBox _theme = Combo(190);
    private readonly ComboBox _lang = Combo(190);
    private readonly CheckBox _autoOn = Chk("g.auto");
    private readonly CheckBox _ovOn = Chk("g.override");
    private readonly ComboBox _ovMin = Combo(150);
    private readonly CheckBox _nOver = new() { Text = Loc.T("n.override"), AutoSize = true, Margin = new Padding(26, 3, 3, 3) };
    private readonly CheckBox _nOn = Chk("n.on");
    private readonly CheckBox _nRuleStart = new() { Text = Loc.T("n.rule.start"), AutoSize = true, Margin = new Padding(26, 3, 3, 3) };
    private readonly CheckBox _nRuleEnd = new() { Text = Loc.T("n.rule.end"), AutoSize = true, Margin = new Padding(26, 3, 3, 3) };
    private readonly CheckBox _nSched = new() { Text = Loc.T("n.sched"), AutoSize = true, Margin = new Padding(26, 3, 3, 3) };
    private readonly CheckBox _nPause = new() { Text = Loc.T("n.pause"), AutoSize = true, Margin = new Padding(26, 3, 3, 3) };

    // Tasten
    private readonly Dictionary<BrightnessAction, TextBox> _texts = new();
    private readonly Dictionary<BrightnessAction, KeyBinding?> _binds = new();
    private BrightnessAction? _capturing;
    private Button? _capButton;

    // Blaulicht
    private readonly CheckBox _filterOn = Chk("f.on");
    private readonly TrackBar _kelvin = new()
    {
        Minimum = 25, Maximum = 65, TickFrequency = 5, SmallChange = 1, LargeChange = 5, Width = 620
    };
    private readonly Label _kelvinLabel = new() { AutoSize = true, Font = new Font(Control.DefaultFont, FontStyle.Bold) };

    // Zeitplan
    private readonly CheckBox _schedOn = Chk("s.on");
    private readonly NumericUpDown _transition = Num(0, 120, 1, 0, 70);
    private readonly RadioButton _fixed = new() { Text = Loc.T("s.fixed"), AutoSize = true };
    private readonly RadioButton _sun = new() { Text = Loc.T("s.sun"), AutoSize = true };
    private readonly DataGridView _grid = new()
    {
        AllowUserToAddRows = true, AllowUserToDeleteRows = true, RowHeadersVisible = false,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, Width = 620, Height = 130,
        BackgroundColor = SystemColors.Window, Margin = new Padding(3, 3, 3, 10)
    };
    private readonly NumericUpDown _lat = Num(-90, 90, 0.1m, 4, 90);
    private readonly NumericUpDown _lon = Num(-180, 180, 0.1m, 4, 90);
    private readonly NumericUpDown _offset = Num(-240, 240, 5, 0, 70);
    private readonly NumericUpDown _dayPct = Num(0, 100, 5, 0, 70);
    private readonly NumericUpDown _dayK = Num(2500, 6500, 100, 0, 80);
    private readonly NumericUpDown _nightPct = Num(0, 100, 5, 0, 70);
    private readonly NumericUpDown _nightK = Num(2500, 6500, 100, 0, 80);
    private readonly Label _sunInfo = new() { AutoSize = true, Anchor = AnchorStyles.Left };

    // Programme
    private readonly CheckBox _rulesOn = Chk("r.on");
    private readonly DataGridView _rulesGrid = new()
    {
        AllowUserToAddRows = true, AllowUserToDeleteRows = true, RowHeadersVisible = false,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, Width = 620, Height = 150,
        BackgroundColor = SystemColors.Window, Margin = new Padding(3, 3, 3, 6)
    };
    private readonly DataGridView _comboGrid = new()
    {
        AllowUserToAddRows = true, AllowUserToDeleteRows = true, RowHeadersVisible = false,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, Width = 620, Height = 110,
        BackgroundColor = SystemColors.Window, Margin = new Padding(3, 3, 3, 6)
    };
    private readonly TrackBar _fadeB = FadeBar();
    private readonly TrackBar _fadeK = FadeBar();
    private readonly Label _fadeBLabel = new() { AutoSize = true, Anchor = AnchorStyles.Left };
    private readonly Label _fadeKLabel = new() { AutoSize = true, Anchor = AnchorStyles.Left };
    private readonly RadioButton _ruleRestore = new() { Text = Loc.T("r.restore"), AutoSize = true };
    private readonly RadioButton _ruleKeep = new() { Text = Loc.T("r.keep"), AutoSize = true };

    // Apps, bei denen meist der Fenstertitel entscheidet (Browser, Store-Apps)
    private static readonly HashSet<string> TitleApps = new(StringComparer.OrdinalIgnoreCase)
        { "chrome", "msedge", "firefox", "brave", "opera", "vivaldi", "ApplicationFrameHost" };

    // ---------- Helfer ----------

    private static CheckBox Chk(string key) =>
        new() { Text = Loc.T(key), AutoSize = true, MaximumSize = new Size(HintWidth, 0) };

    /// <summary>Zahlenfeld, dessen Mausrad genau einen Schritt weiterspringt (statt 3).</summary>
    private static NumericUpDown Num(decimal min, decimal max, decimal inc, int decimals, int width)
    {
        var n = new NumericUpDown { Minimum = min, Maximum = max, Increment = inc, DecimalPlaces = decimals, Width = width };
        n.MouseWheel += (_, e) =>
        {
            if (e is HandledMouseEventArgs h) h.Handled = true;
            decimal v = n.Value + Math.Sign(e.Delta) * n.Increment;
            n.Value = Math.Clamp(v, n.Minimum, n.Maximum);
        };
        return n;
    }

    /// <summary>Regler 0–10 s in 0,5-s-Schritten (Wert = halbe Sekunden); Mausrad = ein Schritt.</summary>
    private static TrackBar FadeBar()
    {
        var bar = new TrackBar
        {
            Minimum = 0, Maximum = 20, TickFrequency = 2, SmallChange = 1, LargeChange = 2, Width = 250
        };
        bar.MouseWheel += (_, e) =>
        {
            if (e is HandledMouseEventArgs h) h.Handled = true;
            bar.Value = Math.Clamp(bar.Value + Math.Sign(e.Delta), bar.Minimum, bar.Maximum);
        };
        return bar;
    }

    private static string FadeText(int halfSeconds) =>
        halfSeconds == 0 ? Loc.T("r.instant") : $"{halfSeconds / 2.0:0.0} {Loc.T("unit.s")}";

    /// <summary>Auswahlfeld, das beim Scrollen über die Seite nicht versehentlich umspringt.</summary>
    private static ComboBox Combo(int width)
    {
        var c = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = width };
        c.MouseWheel += (_, e) => { if (e is HandledMouseEventArgs h) h.Handled = true; };
        return c;
    }

    private static Label L(string text) =>
        new() { Text = text, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 6, 12, 6) };

    private static Label Hint(string text) =>
        new()
        {
            Text = text, AutoSize = true, MaximumSize = new Size(HintWidth, 0), Tag = "hint",
            Margin = new Padding(3, 10, 3, 3)
        };

    private static TableLayoutPanel Table() =>
        new()
        {
            AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 4,
            Anchor = AnchorStyles.Top | AnchorStyles.Left, Margin = new Padding(0, 0, 0, 6)
        };

    private static void AddSection(TableLayoutPanel root, string title, Control content)
    {
        root.Controls.Add(new Label
        {
            Text = title, AutoSize = true, Margin = new Padding(0, 14, 0, 0),
            Font = new Font(Control.DefaultFont.FontFamily, 12f, FontStyle.Bold)
        });
        root.Controls.Add(new Panel
        {
            Height = 1, Dock = DockStyle.Fill, Tag = "divider", Margin = new Padding(0, 3, 0, 8)
        });
        root.Controls.Add(content);
    }

    // ---------- Aufbau ----------

    public SettingsForm(Settings settings, InputManager input, ColorController color, Action saved)
    {
        _s = settings; _input = input; _color = color; _saved = saved;

        Text = Loc.T("set.title");
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        ShowInTaskbar = true;
        TopMost = true;   // bleibt sichtbar, bis es mit X (oder Speichern/Abbrechen) geschlossen wird
        ClientSize = new Size(760, 640);
        MinimumSize = new Size(760, 420);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1, Padding = new Padding(14, 0, 14, 14)
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        AddSection(root, Loc.T("sec.general"), BuildGeneral());
        AddSection(root, Loc.T("sec.keys"), BuildKeys());
        AddSection(root, Loc.T("sec.filter"), BuildFilter());
        AddSection(root, Loc.T("sec.schedule"), BuildSchedule());
        AddSection(root, Loc.T("sec.apps"), BuildApps());

        var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
        scroll.Controls.Add(root);

        var ok = new Button { Text = Loc.T("btn.save"), Width = 110, Height = 28 };
        var cancel = new Button { Text = Loc.T("btn.cancel"), Width = 110, Height = 28, DialogResult = DialogResult.Cancel };
        ok.Click += OnSave;
        var bottom = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom, Height = 46, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(8)
        };
        bottom.Controls.Add(cancel);
        bottom.Controls.Add(ok);

        Controls.Add(scroll);   // zuerst: Fill
        Controls.Add(bottom);
        AcceptButton = ok;
        CancelButton = cancel;

        LoadValues();
        _theme.SelectedIndexChanged += (_, _) => { Theme.Refresh(ThemeValue()); Theme.Apply(this); };
        Theme.Apply(this);
    }

    private static readonly int[] OverrideChoices = { 15, 30, 60, 120, 240, 480, 0 };   // 0 = bis Neustart

    private Control BuildGeneral()
    {
        _theme.Items.AddRange(new object[] { Loc.T("theme.system"), Loc.T("theme.light"), Loc.T("theme.dark") });
        _lang.Items.Add(Loc.T("lang.auto"));
        foreach (var (_, name) in Loc.Languages) _lang.Items.Add(name);

        foreach (var m in OverrideChoices) _ovMin.Items.Add(Loc.T("ov." + m));

        var t = Table();
        int r = 0;
        void Wide(Control c) { t.Controls.Add(c, 0, r); t.SetColumnSpan(c, 4); r++; }
        void Row(string key, Control c) { t.Controls.Add(L(Loc.T(key)), 0, r); t.Controls.Add(c, 1, r); r++; }
        Wide(_autoOn);
        Wide(_hotkeysOn);
        Row("g.step", _step);
        Wide(_osdOn);
        Row("g.osdms", _osdMs);
        Wide(_trayWheel);
        Wide(_ovOn);
        Row("g.override.for", _ovMin);
        Wide(_nOn);
        Wide(_nRuleStart);
        Wide(_nRuleEnd);
        Wide(_nSched);
        Wide(_nPause);
        Wide(_nOver);
        Row("g.theme", _theme);
        Row("g.lang", _lang);
        _ovOn.CheckedChanged += (_, _) => _ovMin.Enabled = _ovOn.Checked;
        _nOn.CheckedChanged += (_, _) => UpdateNotifyEnabled();
        return t;
    }

    private Control BuildKeys()
    {
        var t = Table();
        var names = new (BrightnessAction Action, string Key)[]
        {
            (BrightnessAction.BrightnessDown, "a.down"),
            (BrightnessAction.BrightnessUp, "a.up"),
            (BrightnessAction.Warmer, "a.warmer"),
            (BrightnessAction.Cooler, "a.cooler"),
            (BrightnessAction.FilterToggle, "a.toggle"),
            (BrightnessAction.PauseToggle, "a.pause"),
        };

        int row = 0;
        foreach (var (action, key) in names)
        {
            var box = new TextBox { ReadOnly = true, Width = 190 };
            var rec = new Button { Text = Loc.T("b.record"), Width = 110, Height = 26 };
            var del = new Button { Text = Loc.T("b.delete"), Width = 80, Height = 26 };
            _texts[action] = box;

            var a = action;
            rec.Click += (_, _) =>
            {
                if (_capturing == a) { CancelCapture(); return; }
                StartCapture(a, rec);
            };
            del.Click += (_, _) => { CancelCapture(); _binds[a] = null; box.Text = ""; };

            t.Controls.Add(L(Loc.T(key)), 0, row);
            t.Controls.Add(box, 1, row);
            t.Controls.Add(rec, 2, row);
            t.Controls.Add(del, 3, row);
            row++;
        }

        var hint = Hint(Loc.T("k.hint"));
        t.Controls.Add(hint, 0, row); t.SetColumnSpan(hint, 4);
        return t;
    }

    private Control BuildFilter()
    {
        var t = Table();
        t.Controls.Add(_filterOn, 0, 0); t.SetColumnSpan(_filterOn, 4);
        var l = L(Loc.T("f.label"));
        t.Controls.Add(l, 0, 1); t.SetColumnSpan(l, 4);
        t.Controls.Add(_kelvin, 0, 2); t.SetColumnSpan(_kelvin, 4);
        t.Controls.Add(_kelvinLabel, 0, 3); t.SetColumnSpan(_kelvinLabel, 4);
        var hint = Hint(Loc.T("f.hint"));
        t.Controls.Add(hint, 0, 4); t.SetColumnSpan(hint, 4);

        _kelvin.MouseWheel += (_, e) =>
        {
            if (e is HandledMouseEventArgs h) h.Handled = true;
            _kelvin.Value = Math.Clamp(_kelvin.Value + Math.Sign(e.Delta), _kelvin.Minimum, _kelvin.Maximum);
        };
        _kelvin.ValueChanged += (_, _) => { UpdateKelvinLabel(); PreviewColor(); };
        _filterOn.CheckedChanged += (_, _) => PreviewColor();
        return t;
    }

    private Control BuildSchedule()
    {
        var t = Table();
        t.Controls.Add(_schedOn, 0, 0); t.SetColumnSpan(_schedOn, 4);
        t.Controls.Add(L(Loc.T("s.transition")), 0, 1); t.Controls.Add(_transition, 1, 1);

        t.Controls.Add(_fixed, 0, 2); t.SetColumnSpan(_fixed, 4);
        _grid.Columns.Add("t", Loc.T("c.time"));
        _grid.Columns.Add("p", Loc.T("c.brightness"));
        _grid.Columns.Add("k", Loc.T("c.kelvin"));
        t.Controls.Add(_grid, 0, 3); t.SetColumnSpan(_grid, 4);

        t.Controls.Add(_sun, 0, 4); t.SetColumnSpan(_sun, 4);
        t.Controls.Add(L(Loc.T("s.lat")), 0, 5); t.Controls.Add(_lat, 1, 5);
        t.Controls.Add(L(Loc.T("s.lon")), 2, 5); t.Controls.Add(_lon, 3, 5);
        t.Controls.Add(L(Loc.T("s.offset")), 0, 6); t.Controls.Add(_offset, 1, 6);
        t.Controls.Add(_sunInfo, 2, 6); t.SetColumnSpan(_sunInfo, 2);
        t.Controls.Add(L(Loc.T("s.day")), 0, 7); t.Controls.Add(_dayPct, 1, 7);
        t.Controls.Add(L(Loc.T("c.kelvin")), 2, 7); t.Controls.Add(_dayK, 3, 7);
        t.Controls.Add(L(Loc.T("s.night")), 0, 8); t.Controls.Add(_nightPct, 1, 8);
        t.Controls.Add(L(Loc.T("c.kelvin")), 2, 8); t.Controls.Add(_nightK, 3, 8);
        var hint = Hint(Loc.T("s.hint"));
        t.Controls.Add(hint, 0, 9); t.SetColumnSpan(hint, 4);

        _fixed.CheckedChanged += (_, _) => UpdateScheduleEnabled();
        _lat.ValueChanged += (_, _) => UpdateSunInfo();
        _lon.ValueChanged += (_, _) => UpdateSunInfo();
        return t;
    }

    private Control BuildApps()
    {
        var t = Table();
        t.Controls.Add(_rulesOn, 0, 0); t.SetColumnSpan(_rulesOn, 4);

        _rulesGrid.Columns.Add("p", Loc.T("c.proc"));
        _rulesGrid.Columns.Add("t", Loc.T("c.title"));
        _rulesGrid.Columns.Add("b", Loc.T("c.brightness"));
        _rulesGrid.Columns.Add("k", Loc.T("c.kelvin"));
        t.Controls.Add(_rulesGrid, 0, 1); t.SetColumnSpan(_rulesGrid, 4);

        var add = new Button { Text = Loc.T("r.add"), AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
        var remove = new Button { Text = Loc.T("r.remove"), AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
        add.Click += (_, _) => ShowProgramPicker(add);
        remove.Click += (_, _) =>
        {
            if (_rulesGrid.CurrentRow is { IsNewRow: false } row) _rulesGrid.Rows.Remove(row);
        };
        var buttons = new FlowLayoutPanel
        {
            AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Margin = new Padding(0, 0, 0, 6)
        };
        buttons.Controls.Add(add);
        buttons.Controls.Add(remove);
        t.Controls.Add(buttons, 0, 2); t.SetColumnSpan(buttons, 4);

        // Kombinationen: mehrere Programme gleichzeitig sichtbar (z. B. nebeneinander)
        var comboTitle = new Label
        {
            Text = Loc.T("r.combo.title"), AutoSize = true, Margin = new Padding(0, 12, 0, 3),
            Font = new Font(Control.DefaultFont, FontStyle.Bold)
        };
        t.Controls.Add(comboTitle, 0, 3); t.SetColumnSpan(comboTitle, 4);
        var comboHint = Hint(Loc.T("r.combo.hint"));
        comboHint.Margin = new Padding(3, 0, 3, 3);
        t.Controls.Add(comboHint, 0, 4); t.SetColumnSpan(comboHint, 4);
        _comboGrid.Columns.Add("p", Loc.T("c.combo"));
        _comboGrid.Columns.Add("b", Loc.T("c.brightness"));
        _comboGrid.Columns.Add("k", Loc.T("c.kelvin"));
        _comboGrid.Columns[0].FillWeight = 160;
        t.Controls.Add(_comboGrid, 0, 5); t.SetColumnSpan(_comboGrid, 4);

        var addCombo = new Button { Text = Loc.T("r.combo.add"), AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
        var removeCombo = new Button { Text = Loc.T("r.remove"), AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
        addCombo.Click += (_, _) => ShowComboDialog();
        removeCombo.Click += (_, _) =>
        {
            if (_comboGrid.CurrentRow is { IsNewRow: false } row) _comboGrid.Rows.Remove(row);
        };
        var comboButtons = new FlowLayoutPanel
        {
            AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Margin = new Padding(0, 0, 0, 6)
        };
        comboButtons.Controls.Add(addCombo);
        comboButtons.Controls.Add(removeCombo);
        t.Controls.Add(comboButtons, 0, 6); t.SetColumnSpan(comboButtons, 4);

        // Weicher Wechsel: Dauer getrennt für Helligkeit und Farbtemperatur
        t.Controls.Add(L(Loc.T("r.fadeb")), 0, 7); t.Controls.Add(_fadeB, 1, 7); t.SetColumnSpan(_fadeB, 2);
        t.Controls.Add(_fadeBLabel, 3, 7);
        t.Controls.Add(L(Loc.T("r.fadek")), 0, 8); t.Controls.Add(_fadeK, 1, 8); t.SetColumnSpan(_fadeK, 2);
        t.Controls.Add(_fadeKLabel, 3, 8);
        _fadeB.ValueChanged += (_, _) => _fadeBLabel.Text = FadeText(_fadeB.Value);
        _fadeK.ValueChanged += (_, _) => _fadeKLabel.Text = FadeText(_fadeK.Value);

        var leave = L(Loc.T("r.leave"));
        t.Controls.Add(leave, 0, 9); t.SetColumnSpan(leave, 4);
        t.Controls.Add(_ruleRestore, 0, 10); t.SetColumnSpan(_ruleRestore, 4);
        t.Controls.Add(_ruleKeep, 0, 11); t.SetColumnSpan(_ruleKeep, 4);

        var hint = Hint(Loc.T("r.hint"));
        t.Controls.Add(hint, 0, 12); t.SetColumnSpan(hint, 4);
        return t;
    }

    /// <summary>Liste der laufenden Programme mit Fenster zum Anklicken.</summary>
    private void ShowProgramPicker(Button anchor)
    {
        var running = new List<(string Name, string Title)>();
        foreach (var p in Process.GetProcesses())
        {
            try
            {
                if (p.Id == Environment.ProcessId) continue;
                string title = p.MainWindowTitle;
                if (!string.IsNullOrWhiteSpace(title)) running.Add((p.ProcessName, title));
            }
            catch { /* Prozess beendet oder kein Zugriff */ }
            finally { p.Dispose(); }
        }

        var menu = new ContextMenuStrip();
        Theme.ApplyMenu(menu);
        foreach (var (name, title) in running
                     .GroupBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
                     .Select(g => g.First())
                     .OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase))
        {
            string shortTitle = title.Length > 48 ? title[..48] + "…" : title;
            var item = new ToolStripMenuItem($"{name}  –  {shortTitle}");
            string chosen = name;
            item.Click += (_, _) => AddRuleRow(chosen);
            menu.Items.Add(item);
        }
        if (menu.Items.Count == 0) menu.Items.Add(new ToolStripMenuItem(Loc.T("r.none")) { Enabled = false });
        menu.Show(anchor, new Point(0, anchor.Height));
    }

    /// <summary>Dialog: Programme für eine Kombination auswählen; aktuell sichtbare sind vorangehakt.</summary>
    private void ShowComboDialog()
    {
        var visible = WindowScanner.VisibleProcesses();
        var running = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in visible) running.Add(name);
        foreach (var p in Process.GetProcesses())
        {
            try
            {
                if (p.Id != Environment.ProcessId && !string.IsNullOrWhiteSpace(p.MainWindowTitle)) running.Add(p.ProcessName);
            }
            catch { /* Prozess beendet oder kein Zugriff */ }
            finally { p.Dispose(); }
        }

        using var dlg = new Form
        {
            Text = Loc.T("r.combo.title"), StartPosition = FormStartPosition.CenterParent, ClientSize = new Size(420, 400),
            FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false, ShowInTaskbar = false,
            TopMost = true
        };
        var label = new Label { Text = Loc.T("r.combo.dlg"), Dock = DockStyle.Top, Height = 44, Padding = new Padding(10, 10, 10, 0) };
        var list = new CheckedListBox { Dock = DockStyle.Fill, CheckOnClick = true, IntegralHeight = false };
        foreach (var name in running) list.Items.Add(name, visible.Contains(name));
        var ok = new Button { Text = Loc.T("btn.ok"), Width = 100, Height = 28, DialogResult = DialogResult.OK };
        var cancel = new Button { Text = Loc.T("btn.cancel"), Width = 100, Height = 28, DialogResult = DialogResult.Cancel };
        var bottom = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 44, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(8) };
        bottom.Controls.Add(cancel);
        bottom.Controls.Add(ok);
        dlg.Controls.Add(list);
        dlg.Controls.Add(label);
        dlg.Controls.Add(bottom);
        dlg.AcceptButton = ok;
        dlg.CancelButton = cancel;
        Theme.Apply(dlg);

        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        var chosen = list.CheckedItems.Cast<string>().ToList();
        if (chosen.Count < 2)
        {
            MessageBox.Show(this, Loc.T("err.combo"), Loc.T("r.combo.title"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        int index = _comboGrid.Rows.Add(string.Join(" + ", chosen), "", "");
        _comboGrid.CurrentCell = _comboGrid.Rows[index].Cells[1];
        _comboGrid.BeginEdit(true);
    }

    private void AddRuleRow(string process)
    {
        int index = _rulesGrid.Rows.Add(process, "", "", "");
        int column = TitleApps.Contains(process) ? 1 : 2;   // Browser/Store-App: zuerst den Titel eintragen
        _rulesGrid.CurrentCell = _rulesGrid.Rows[index].Cells[column];
        _rulesGrid.BeginEdit(true);
    }

    // ---------- Werte laden / speichern ----------

    private string ThemeValue() => _theme.SelectedIndex switch { 1 => "light", 2 => "dark", _ => "system" };

    private void LoadValues()
    {
        _hotkeysOn.Checked = _s.HotkeysEnabled;
        _step.Value = Math.Clamp(_s.StepPercent, 1, 25);
        _osdOn.Checked = _s.OsdEnabled;
        _osdMs.Value = Math.Clamp(_s.OsdMs, 500, 5000);
        _trayWheel.Checked = _s.TrayWheelEnabled;
        _nOn.Checked = _s.NotifyEnabled;
        _nRuleStart.Checked = _s.NotifyRuleStart;
        _nRuleEnd.Checked = _s.NotifyRuleEnd;
        _nSched.Checked = _s.NotifySchedule;
        _nPause.Checked = _s.NotifyPause;
        _nOver.Checked = _s.NotifyOverride;
        _autoOn.Checked = _s.AutomationEnabled;
        _ovOn.Checked = _s.OverrideEnabled;
        int oi = Array.IndexOf(OverrideChoices, _s.OverrideMinutes);
        _ovMin.SelectedIndex = oi >= 0 ? oi : 1;
        _ovMin.Enabled = _ovOn.Checked;
        UpdateNotifyEnabled();
        _theme.SelectedIndex = _s.Theme == "light" ? 1 : _s.Theme == "dark" ? 2 : 0;
        int li = Array.FindIndex(Loc.Languages, l => l.Code == _s.Language);
        _lang.SelectedIndex = li >= 0 ? li + 1 : 0;

        foreach (var action in _texts.Keys)
        {
            var b = _s.Bindings.FirstOrDefault(x => x.Action == action);
            _binds[action] = b == null ? null : new KeyBinding { Action = b.Action, Vk = b.Vk, Mods = b.Mods, Hid = b.Hid };
            _texts[action].Text = b?.Describe() ?? "";
        }

        _filterOn.Checked = _s.FilterEnabled;
        _kelvin.Value = Math.Clamp(_s.Kelvin / 100, _kelvin.Minimum, _kelvin.Maximum);
        UpdateKelvinLabel();

        foreach (var c in _s.ComboRules.Where(c => c.Combo.Count > 0))
            _comboGrid.Rows.Add(string.Join(" + ", c.Combo), c.Percent?.ToString() ?? "", c.Kelvin?.ToString() ?? "");

        _schedOn.Checked = _s.ScheduleEnabled;
        _transition.Value = Math.Clamp(_s.TransitionMinutes, 0, 120);
        _fixed.Checked = !_s.UseSun;
        _sun.Checked = _s.UseSun;
        foreach (var e in _s.Entries) _grid.Rows.Add(e.Time, e.Percent.ToString(), e.Kelvin.ToString());
        _lat.Value = (decimal)Math.Clamp(_s.Latitude, -90, 90);
        _lon.Value = (decimal)Math.Clamp(_s.Longitude, -180, 180);
        _offset.Value = Math.Clamp(_s.SunsetOffsetMin, -240, 240);
        _dayPct.Value = Math.Clamp(_s.DayPercent, 0, 100);
        _dayK.Value = Math.Clamp(_s.DayKelvin, 2500, 6500);
        _nightPct.Value = Math.Clamp(_s.NightPercent, 0, 100);
        _nightK.Value = Math.Clamp(_s.NightKelvin, 2500, 6500);
        UpdateScheduleEnabled();
        UpdateSunInfo();

        _rulesOn.Checked = _s.AppRulesEnabled;
        _ruleRestore.Checked = _s.RulesRestoreOnLeave;
        _ruleKeep.Checked = !_s.RulesRestoreOnLeave;
        _fadeB.Value = Math.Clamp((int)Math.Round(_s.RuleFadeBrightnessSec * 2), 0, 20);
        _fadeK.Value = Math.Clamp((int)Math.Round(_s.RuleFadeColorSec * 2), 0, 20);
        _fadeBLabel.Text = FadeText(_fadeB.Value);
        _fadeKLabel.Text = FadeText(_fadeK.Value);
        foreach (var r in _s.AppRules)
            _rulesGrid.Rows.Add(r.Process, r.Title, r.Percent?.ToString() ?? "", r.Kelvin?.ToString() ?? "");
    }

    private void OnSave(object? sender, EventArgs e)
    {
        _grid.EndEdit();
        var entries = new List<ScheduleEntry>();
        foreach (DataGridViewRow r in _grid.Rows)
        {
            if (r.IsNewRow) continue;
            string time = Convert.ToString(r.Cells[0].Value)?.Trim() ?? "";
            if (!DateTime.TryParseExact(time, new[] { "H:mm", "HH:mm" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out _)
                || !int.TryParse(Convert.ToString(r.Cells[1].Value), out int pct) || pct < 0 || pct > 100
                || !int.TryParse(Convert.ToString(r.Cells[2].Value), out int kel) || kel < 2500 || kel > 6500)
            {
                MessageBox.Show(Loc.T("err.entry"), Loc.T("err.title"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            entries.Add(new ScheduleEntry { Time = time, Percent = pct, Kelvin = kel });
        }

        _rulesGrid.EndEdit();
        var rules = new List<AppRule>();
        foreach (DataGridViewRow row in _rulesGrid.Rows)
        {
            if (row.IsNewRow) continue;
            string proc = Convert.ToString(row.Cells[0].Value)?.Trim() ?? "";
            string ruleTitle = Convert.ToString(row.Cells[1].Value)?.Trim() ?? "";
            string pText = Convert.ToString(row.Cells[2].Value)?.Trim() ?? "";
            string kText = Convert.ToString(row.Cells[3].Value)?.Trim() ?? "";
            if (proc.Length == 0 && ruleTitle.Length == 0 && pText.Length == 0 && kText.Length == 0) continue; // leere Zeile

            int? percent = null, kelvin = null;
            bool valid = proc.Length > 0;
            if (pText.Length > 0)
            {
                if (int.TryParse(pText, out int pv) && pv is >= 0 and <= 100) percent = pv; else valid = false;
            }
            if (kText.Length > 0)
            {
                if (int.TryParse(kText, out int kv) && kv is >= 2500 and <= 6500) kelvin = kv; else valid = false;
            }
            if (!valid || (percent == null && kelvin == null))
            {
                MessageBox.Show(Loc.T("err.rule"), Loc.T("sec.apps"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            rules.Add(new AppRule { Process = proc, Title = ruleTitle, Percent = percent, Kelvin = kelvin });
        }

        _comboGrid.EndEdit();
        var combos = new List<AppRule>();
        foreach (DataGridViewRow row in _comboGrid.Rows)
        {
            if (row.IsNewRow) continue;
            string names = Convert.ToString(row.Cells[0].Value)?.Trim() ?? "";
            string pText = Convert.ToString(row.Cells[1].Value)?.Trim() ?? "";
            string kText = Convert.ToString(row.Cells[2].Value)?.Trim() ?? "";
            if (names.Length == 0 && pText.Length == 0 && kText.Length == 0) continue;

            var procs = names.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(n => n.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? n[..^4] : n)
                .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            int? percent = null, kelvin = null;
            bool valid = procs.Count >= 2;
            if (pText.Length > 0)
            {
                if (int.TryParse(pText, out int pv) && pv is >= 0 and <= 100) percent = pv; else valid = false;
            }
            if (kText.Length > 0)
            {
                if (int.TryParse(kText, out int kv) && kv is >= 2500 and <= 6500) kelvin = kv; else valid = false;
            }
            if (!valid || (percent == null && kelvin == null))
            {
                MessageBox.Show(this, Loc.T("err.combo"), Loc.T("r.combo.title"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            combos.Add(new AppRule { Combo = procs, Percent = percent, Kelvin = kelvin });
        }

        CancelCapture();
        _s.ComboRules = combos;
        _s.AppRulesEnabled = _rulesOn.Checked;
        _s.RulesRestoreOnLeave = _ruleRestore.Checked;
        _s.RuleFadeBrightnessSec = _fadeB.Value / 2.0;
        _s.RuleFadeColorSec = _fadeK.Value / 2.0;
        _s.AppRules = rules;
        _s.HotkeysEnabled = _hotkeysOn.Checked;
        _s.StepPercent = (int)_step.Value;
        _s.OsdEnabled = _osdOn.Checked;
        _s.OsdMs = (int)_osdMs.Value;
        _s.TrayWheelEnabled = _trayWheel.Checked;
        _s.NotifyEnabled = _nOn.Checked;
        _s.NotifyRuleStart = _nRuleStart.Checked;
        _s.NotifyRuleEnd = _nRuleEnd.Checked;
        _s.NotifySchedule = _nSched.Checked;
        _s.NotifyPause = _nPause.Checked;
        _s.NotifyOverride = _nOver.Checked;
        _s.AutomationEnabled = _autoOn.Checked;
        _s.OverrideEnabled = _ovOn.Checked;
        _s.OverrideMinutes = OverrideChoices[Math.Max(0, _ovMin.SelectedIndex)];
        _s.Theme = ThemeValue();
        _s.Language = _lang.SelectedIndex <= 0 ? "auto" : Loc.Languages[_lang.SelectedIndex - 1].Code;
        _s.Bindings = _binds.Values.Where(b => b != null).Select(b => b!).ToList();
        _s.FilterEnabled = _filterOn.Checked;
        _s.Kelvin = _kelvin.Value * 100;
        _s.ScheduleEnabled = _schedOn.Checked;
        _s.TransitionMinutes = (int)_transition.Value;
        _s.UseSun = _sun.Checked;
        _s.Entries = entries;
        _s.Latitude = (double)_lat.Value;
        _s.Longitude = (double)_lon.Value;
        _s.SunsetOffsetMin = (int)_offset.Value;
        _s.DayPercent = (int)_dayPct.Value;
        _s.DayKelvin = (int)_dayK.Value;
        _s.NightPercent = (int)_nightPct.Value;
        _s.NightKelvin = (int)_nightK.Value;
        _s.Save();

        _saved();
        DialogResult = DialogResult.OK;
        Close();
    }

    // ---------- Tasten aufnehmen ----------

    private void StartCapture(BrightnessAction action, Button button)
    {
        CancelCapture();
        _capturing = action;
        _capButton = button;
        button.Text = Loc.T("b.recording");
        _input.BeginCapture(binding =>
        {
            binding.Action = action;
            // gleiche Taste nicht doppelt belegen
            foreach (var other in _binds.Keys.ToList())
                if (other != action && _binds[other]?.SameKey(binding) == true)
                {
                    _binds[other] = null;
                    _texts[other].Text = "";
                }
            _binds[action] = binding;
            _texts[action].Text = binding.Describe();
            EndCaptureUi();
        });
    }

    private void CancelCapture()
    {
        if (_capturing == null) return;
        _input.CancelCapture();
        EndCaptureUi();
    }

    private void EndCaptureUi()
    {
        if (_capButton != null) _capButton.Text = Loc.T("b.record");
        _capturing = null;
        _capButton = null;
    }

    // ---------- Hilfsmethoden ----------

    /// <summary>Die Einzelstufen sind nur wählbar, solange der Hauptschalter an ist.</summary>
    private void UpdateNotifyEnabled()
    {
        foreach (var box in new[] { _nRuleStart, _nRuleEnd, _nSched, _nPause, _nOver }) box.Enabled = _nOn.Checked;
    }

    private void UpdateKelvinLabel() => _kelvinLabel.Text = $"{_kelvin.Value * 100} K";

    private void PreviewColor() => _color.Apply(_filterOn.Checked ? _kelvin.Value * 100 : ColorController.MaxKelvin);

    private void UpdateScheduleEnabled()
    {
        bool sun = _sun.Checked;
        _grid.Enabled = !sun;
        foreach (Control c in new Control[] { _lat, _lon, _offset, _dayPct, _dayK, _nightPct, _nightK, _sunInfo })
            c.Enabled = sun;
    }

    private void UpdateSunInfo()
    {
        var (rise, set) = Sun.Times(DateTime.Today, (double)_lat.Value, (double)_lon.Value);
        _sunInfo.Text = rise == null || set == null
            ? Loc.T("s.nosun")
            : Loc.T("s.suninfo", rise.Value.ToString("HH:mm"), set.Value.ToString("HH:mm"));
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        CancelCapture();
        Theme.Refresh(_s.Theme);   // Design-Vorschau zurücknehmen
        // Filter-Vorschau auf den gespeicherten Stand zurücksetzen
        _color.Apply(_s.FilterEnabled ? _s.Kelvin : ColorController.MaxKelvin);
        base.OnFormClosed(e);
    }
}
