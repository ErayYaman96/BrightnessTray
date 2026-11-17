using System.Text.Json;
using System.Text.Json.Serialization;

namespace BrightnessTray;

internal enum BrightnessAction { BrightnessDown, BrightnessUp, Warmer, Cooler, FilterToggle, PauseToggle }

/// <summary>Eine Tastenzuweisung: entweder normale Taste (Vk + Modifier) oder HID-Spezialtaste (Hid = Hex-Signatur).</summary>
internal sealed class KeyBinding
{
    public BrightnessAction Action { get; set; }
    public int Vk { get; set; }
    public int Mods { get; set; }          // 1 = Strg, 2 = Alt, 4 = Umschalt, 8 = Win
    public string? Hid { get; set; }

    public bool SameKey(KeyBinding o) =>
        Hid != null || o.Hid != null ? Hid == o.Hid : Vk == o.Vk && Mods == o.Mods;

    public string Describe()
    {
        if (Hid != null)
        {
            try
            {
                var bytes = Convert.FromHexString(Hid);
                if (bytes.Length >= 3)
                {
                    int usage = bytes[1] | bytes[2] << 8;
                    string? name = usage switch
                    {
                        0x6F => Loc.T("hid.up"),
                        0x70 => Loc.T("hid.down"),
                        _ => null
                    };
                    if (name != null) return name;
                }
            }
            catch { /* ungültiger Hex-String */ }
            return Loc.T("hid.other", Hid);
        }

        var parts = new List<string>();
        if ((Mods & 1) != 0) parts.Add(Loc.T("key.ctrl"));
        if ((Mods & 2) != 0) parts.Add(Loc.T("key.alt"));
        if ((Mods & 4) != 0) parts.Add(Loc.T("key.shift"));
        if ((Mods & 8) != 0) parts.Add(Loc.T("key.win"));
        parts.Add(((System.Windows.Forms.Keys)Vk).ToString());
        return string.Join("+", parts);
    }
}

internal sealed class ScheduleEntry
{
    public string Time { get; set; } = "20:00";   // HH:mm
    public int Percent { get; set; } = 20;        // Helligkeit
    public int Kelvin { get; set; } = 6500;       // 6500 = neutral (kein Filter)
}

/// <summary>Regel für ein Vordergrundprogramm. Leere Werte (null) bedeuten "nicht ändern".</summary>
internal sealed class AppRule
{
    public string Process { get; set; } = "";   // Prozessname, z. B. "Photoshop" (ohne .exe)
    public string Title { get; set; } = "";     // optional: Teil des Fenstertitels, z. B. "Netflix"
    public int? Percent { get; set; }           // Helligkeit
    public int? Kelvin { get; set; }            // Farbtemperatur (6500 = neutral)
    public List<string> Combo { get; set; } = new();   // nur bei Kombinationen: alle diese Programme müssen sichtbar sein
}

internal sealed class Settings
{
    // Allgemein
    public bool HotkeysEnabled { get; set; } = true;
    public int StepPercent { get; set; } = 10;
    public bool OsdEnabled { get; set; } = true;
    public int OsdMs { get; set; } = 1500;
    public bool TrayWheelEnabled { get; set; } = true;
    public string Theme { get; set; } = "system";     // system | light | dark
    public string Language { get; set; } = "auto";    // auto | de | en | tr | zh | ru

    // Blaulichtfilter
    public bool FilterEnabled { get; set; }
    public int Kelvin { get; set; } = 3400;

    // Zeitplan
    public bool ScheduleEnabled { get; set; }
    public bool UseSun { get; set; }
    public int TransitionMinutes { get; set; } = 15;  // 0 = sofort umschalten
    public double Latitude { get; set; } = 52.52;     // Standard: Berlin – bitte anpassen
    public double Longitude { get; set; } = 13.405;
    public int SunsetOffsetMin { get; set; }
    public int DayPercent { get; set; } = 80;
    public int DayKelvin { get; set; } = 6500;
    public int NightPercent { get; set; } = 20;
    public int NightKelvin { get; set; } = 3400;
    public List<ScheduleEntry> Entries { get; set; } = new()
    {
        new ScheduleEntry { Time = "07:00", Percent = 80, Kelvin = 6500 },
        new ScheduleEntry { Time = "20:00", Percent = 20, Kelvin = 3400 },
    };

    // Benachrichtigungen bei automatischen Änderungen
    public bool NotifyEnabled { get; set; } = true;      // Hauptschalter
    public bool NotifyRuleStart { get; set; } = true;    // Programmregel greift
    public bool NotifyRuleEnd { get; set; } = true;      // Programm verlassen
    public bool NotifySchedule { get; set; } = true;     // Zeitplan ändert Werte
    public bool NotifyPause { get; set; } = true;        // Pause abgelaufen
    public bool NotifyOverride { get; set; } = true;     // Manueller Vorrang beginnt / endet

    // Manueller Vorrang: eigene Änderungen schalten Programmregeln und Zeitplan für eine Weile aus
    public bool OverrideEnabled { get; set; } = true;
    public int OverrideMinutes { get; set; } = 30;   // 0 = bis Neustart

    // Programmregeln
    public bool AutomationEnabled { get; set; } = true;     // Hauptschalter: aus = weder Zeitplan noch Programmregeln
    public bool AppRulesEnabled { get; set; } = true;
    public bool RulesRestoreOnLeave { get; set; } = true;   // false = Einstellung beibehalten
    public double RuleFadeBrightnessSec { get; set; } = 1.5; // weicher Wechsel bei Programmregeln (0 = sofort)
    public double RuleFadeColorSec { get; set; } = 2.0;
    public List<AppRule> AppRules { get; set; } = new();
    public List<AppRule> ComboRules { get; set; } = new();   // Kombinationen (Programme gleichzeitig sichtbar)

    // Tasten
    public List<KeyBinding> Bindings { get; set; } = new()
    {
        new KeyBinding { Action = BrightnessAction.BrightnessDown, Vk = 0x70 }, // F1
        new KeyBinding { Action = BrightnessAction.BrightnessUp,   Vk = 0x71 }, // F2
    };

    // ---- Speichern / Laden ----
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BrightnessTray", "settings.json");

    public static Settings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath), Json) ?? new Settings();
        }
        catch { /* kaputte Datei -> Standardwerte */ }
        return new Settings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, Json));
        }
        catch { /* Schreibfehler ignorieren */ }
    }
}
