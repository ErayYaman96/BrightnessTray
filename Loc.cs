using System.Globalization;

namespace BrightnessTray;

/// <summary>Übersetzungen. Reihenfolge je Eintrag: Deutsch, Englisch, Türkisch, Chinesisch (vereinfacht), Russisch.</summary>
internal static class Loc
{
    private static readonly string[] Codes = { "de", "en", "tr", "zh", "ru" };

    public static readonly (string Code, string Name)[] Languages =
    {
        ("de", "Deutsch"), ("en", "English"), ("tr", "Türkçe"), ("zh", "中文（简体）"), ("ru", "Русский")
    };

    private static int _idx = 1;

    /// <summary>"auto" = Windows-Sprache, sonst Sprachcode. Unbekannte Sprachen fallen auf Englisch zurück.</summary>
    public static void Set(string setting)
    {
        string code = string.IsNullOrEmpty(setting) || setting == "auto"
            ? CultureInfo.CurrentUICulture.TwoLetterISOLanguageName
            : setting;
        int i = Array.IndexOf(Codes, code);
        _idx = i >= 0 ? i : 1;
    }

    public static string T(string key) => Table.TryGetValue(key, out var v) ? v[_idx] : key;

    public static string T(string key, params object[] args) => string.Format(T(key), args);

    private static readonly Dictionary<string, string[]> Table = new()
    {
        // ---- Tray ----
        ["tray.settings"] = new[] { "Einstellungen…", "Settings…", "Ayarlar…", "设置…", "Настройки…" },
        ["tray.filter"] = new[] { "Blaulichtfilter", "Blue light filter", "Mavi ışık filtresi", "蓝光过滤", "Фильтр синего света" },
        ["tray.pause"] = new[] { "Pausieren", "Pause", "Duraklat", "暂停", "Пауза" },
        ["pause.15"] = new[] { "15 Minuten", "15 minutes", "15 dakika", "15 分钟", "15 минут" },
        ["pause.30"] = new[] { "30 Minuten", "30 minutes", "30 dakika", "30 分钟", "30 минут" },
        ["pause.60"] = new[] { "1 Stunde", "1 hour", "1 saat", "1 小时", "1 час" },
        ["pause.120"] = new[] { "2 Stunden", "2 hours", "2 saat", "2 小时", "2 часа" },
        ["pause.resume"] = new[] { "Fortsetzen", "Resume", "Devam et", "继续", "Продолжить" },
        ["tray.autostart"] = new[] { "Mit Windows starten", "Start with Windows", "Windows ile başlat", "开机自动启动", "Запускать с Windows" },
        ["tray.notify"] = new[] { "Benachrichtigungen", "Notifications", "Bildirimler", "通知", "Уведомления" },
        ["tray.exit"] = new[] { "Beenden", "Exit", "Çıkış", "退出", "Выход" },
        ["tip.brightness"] = new[] { "Helligkeit: {0} %", "Brightness: {0} %", "Parlaklık: {0} %", "亮度：{0}%", "Яркость: {0} %" },
        ["tip.paused"] = new[] { "Pausiert bis {0}", "Paused until {0}", "{0} saatine kadar duraklatıldı", "暂停至 {0}", "Пауза до {0}" },

        // ---- Popup ----
        ["flyout.none"] = new[]
        {
            "Keine steuerbaren Monitore gefunden.\n(DDC/CI im Monitor-Menü aktivieren?)",
            "No controllable monitors found.\n(Enable DDC/CI in the monitor's menu?)",
            "Kontrol edilebilir monitör bulunamadı.\n(Monitör menüsünde DDC/CI etkin mi?)",
            "未找到可控制的显示器。\n（请在显示器菜单中启用 DDC/CI？）",
            "Управляемых мониторов не найдено.\n(Включён ли DDC/CI в меню монитора?)"
        },

        // ---- Einstellungen: Titel & Abschnitte ----
        ["set.title"] = new[] { "BrightnessTray – Einstellungen", "BrightnessTray – Settings", "BrightnessTray – Ayarlar", "BrightnessTray – 设置", "BrightnessTray – Настройки" },
        ["sec.general"] = new[] { "Allgemein", "General", "Genel", "常规", "Общие" },
        ["sec.keys"] = new[] { "Tasten", "Keys", "Tuşlar", "按键", "Клавиши" },
        ["sec.filter"] = new[] { "Blaulichtfilter", "Blue light filter", "Mavi ışık filtresi", "蓝光过滤", "Фильтр синего света" },
        ["sec.schedule"] = new[] { "Zeitplan", "Schedule", "Zamanlama", "计划", "Расписание" },
        ["btn.save"] = new[] { "Speichern", "Save", "Kaydet", "保存", "Сохранить" },
        ["btn.cancel"] = new[] { "Abbrechen", "Cancel", "İptal", "取消", "Отмена" },

        // ---- Allgemein ----
        ["g.hotkeys"] = new[] { "Tastenkürzel aktiv", "Keyboard shortcuts enabled", "Klavye kısayolları etkin", "启用键盘快捷键", "Горячие клавиши включены" },
        ["g.step"] = new[] { "Schrittweite pro Tastendruck (%)", "Step per key press (%)", "Tuş başına adım (%)", "每次按键的步长 (%)", "Шаг на одно нажатие (%)" },
        ["g.osd"] = new[] { "Anzeige (OSD) bei Änderung einblenden", "Show on-screen display on change", "Değişiklikte ekran göstergesini göster", "更改时显示屏幕提示", "Показывать экранный индикатор при изменении" },
        ["g.osdms"] = new[] { "Anzeigedauer (ms)", "Display duration (ms)", "Gösterim süresi (ms)", "显示时长（毫秒）", "Время показа (мс)" },
        ["g.wheel"] = new[]
        {
            "Mausrad auf dem Tray-Symbol (Helligkeit; Strg = Farbtemperatur)",
            "Mouse wheel on tray icon (brightness; Ctrl = color temperature)",
            "Tepsi simgesinde fare tekerleği (parlaklık; Ctrl = renk sıcaklığı)",
            "托盘图标上的鼠标滚轮（亮度；Ctrl = 色温）",
            "Колёсико мыши на значке в трее (яркость; Ctrl = цветовая температура)"
        },
        ["ov.480"] = new[] { "8 Stunden", "8 hours", "8 saat", "8 小时", "8 часов" },
        ["ov.0"] = new[] { "Bis Neustart", "Until restart", "Yeniden başlatmaya kadar", "直到重启", "До перезапуска" },
        ["tray.override.restart"] = new[]
        {
            "Eigene Einstellung gilt bis Neustart – Automatik fortsetzen",
            "Your setting applies until restart – resume automation",
            "Ayarınız yeniden başlatmaya kadar geçerli – otomasyonu sürdür",
            "手动设置有效至重启 – 恢复自动",
            "Ваша настройка действует до перезапуска – вернуть автоматику"
        },
        ["tip.override.restart"] = new[] { "Eigene Einstellung bis Neustart", "Your setting until restart", "Ayarınız: yeniden başlatmaya kadar", "手动设置至重启", "Ваша настройка до перезапуска" },
        ["n.override.body.restart"] = new[]
        {
            "Programmregeln und Zeitplan pausieren bis zum Neustart",
            "App rules and schedule are paused until restart",
            "Uygulama kuralları ve zamanlama yeniden başlatmaya kadar duraklatıldı",
            "应用规则和计划已暂停至重启",
            "Правила для программ и расписание приостановлены до перезапуска"
        },
        ["g.auto"] = new[]
        {
            "Automatik aktiv (Zeitplan und Programmregeln) – Aus = nichts wird automatisch geändert, Einstellungen bleiben erhalten",
            "Automation enabled (schedule and app rules) – off = nothing changes automatically, settings are kept",
            "Otomasyon etkin (zamanlama ve uygulama kuralları) – kapalı = otomatik değişiklik yok, ayarlar korunur",
            "启用自动功能（计划和应用规则）– 关闭后不会自动更改，设置会保留",
            "Автоматика включена (расписание и правила для программ) – выкл. = ничего не меняется само, настройки сохраняются"
        },
        ["tray.auto"] = new[] { "Automatik (Zeitplan && Programme)", "Automation (schedule && apps)", "Otomasyon (zamanlama && uygulamalar)", "自动功能（计划和应用）", "Автоматика (расписание и программы)" },
        ["tip.autooff"] = new[] { "Automatik ist aus", "Automation is off", "Otomasyon kapalı", "自动功能已关闭", "Автоматика выключена" },
        ["osd.auto.off"] = new[] { "Automatik aus", "Automation off", "Otomasyon kapalı", "自动功能关", "Автоматика выкл." },
        ["osd.auto.on"] = new[] { "Automatik an", "Automation on", "Otomasyon açık", "自动功能开", "Автоматика вкл." },
        ["btn.ok"] = new[] { "OK", "OK", "Tamam", "确定", "OK" },
        ["r.combo.title"] = new[]
        {
            "Kombinationen (Programme gleichzeitig sichtbar)", "Combinations (programs visible at the same time)",
            "Kombinasyonlar (programlar aynı anda görünür)", "组合（多个程序同时可见）", "Комбинации (программы видны одновременно)"
        },
        ["r.combo.hint"] = new[]
        {
            "Greift, wenn alle genannten Programme gleichzeitig sichtbar sind, z. B. nebeneinander auf einem Breitbild-Monitor. Hat Vorrang vor einzelnen Programmregeln. Tipp: Fenster so anordnen, dann auf „Kombination hinzufügen…“ klicken – die sichtbaren Programme sind schon angehakt.",
            "Applies when all listed programs are visible at the same time, e.g. side by side on a widescreen monitor. Takes priority over single app rules. Tip: arrange your windows, then click “Add combination…” – the visible programs are already ticked.",
            "Listelenen tüm programlar aynı anda görünürken geçerli olur, örn. geniş ekranda yan yana. Tekil uygulama kurallarından önceliklidir. İpucu: pencereleri düzenleyin, sonra “Kombinasyon ekle…” düğmesine tıklayın – görünen programlar zaten işaretlidir.",
            "当列出的所有程序同时可见时生效，例如在宽屏显示器上并排显示。优先于单个应用规则。提示：先排好窗口，再点击“添加组合…”，可见的程序已被勾选。",
            "Действует, когда все перечисленные программы видны одновременно, например рядом на широком мониторе. Важнее правил для отдельных программ. Совет: расположите окна и нажмите «Добавить комбинацию…» – видимые программы уже отмечены."
        },
        ["c.combo"] = new[] { "Programme (mit + verbunden)", "Programs (joined by +)", "Programlar (+ ile bağlı)", "程序（用 + 连接）", "Программы (через +)" },
        ["r.combo.add"] = new[] { "Kombination hinzufügen…", "Add combination…", "Kombinasyon ekle…", "添加组合…", "Добавить комбинацию…" },
        ["r.combo.dlg"] = new[]
        {
            "Welche Programme müssen gleichzeitig sichtbar sein? (mindestens zwei)",
            "Which programs must be visible at the same time? (at least two)",
            "Hangi programlar aynı anda görünür olmalı? (en az iki)",
            "哪些程序必须同时可见？（至少两个）",
            "Какие программы должны быть видны одновременно? (минимум две)"
        },
        ["err.combo"] = new[]
        {
            "Jede Kombination braucht mindestens zwei Programme (mit + verbunden) und Helligkeit (0–100) und/oder Farbtemperatur (2500–6500).",
            "Each combination needs at least two programs (joined by +) and brightness (0–100) and/or color temperature (2500–6500).",
            "Her kombinasyon en az iki program (+ ile bağlı) ve parlaklık (0–100) ve/veya renk sıcaklığı (2500–6500) gerektirir.",
            "每个组合至少需要两个程序（用 + 连接），以及亮度（0–100）和/或色温（2500–6500）。",
            "Каждой комбинации нужны минимум две программы (через +) и яркость (0–100) и/или цветовая температура (2500–6500)."
        },
        ["g.override"] = new[]
        {
            "Eigene Änderungen haben Vorrang vor der Automatik (Programmregeln und Zeitplan)",
            "My own changes take priority over automation (app rules and schedule)",
            "Kendi değişikliklerim otomasyondan (uygulama kuralları ve zamanlama) önceliklidir",
            "手动更改优先于自动功能（应用规则和计划）",
            "Мои изменения важнее автоматики (правила для программ и расписание)"
        },
        ["g.override.for"] = new[] { "Vorrang gilt für", "Priority lasts for", "Öncelik süresi", "优先持续时间", "Приоритет действует" },
        ["ov.15"] = new[] { "15 Minuten", "15 minutes", "15 dakika", "15 分钟", "15 минут" },
        ["ov.30"] = new[] { "30 Minuten", "30 minutes", "30 dakika", "30 分钟", "30 минут" },
        ["ov.60"] = new[] { "1 Stunde", "1 hour", "1 saat", "1 小时", "1 час" },
        ["ov.120"] = new[] { "2 Stunden", "2 hours", "2 saat", "2 小时", "2 часа" },
        ["ov.240"] = new[] { "4 Stunden", "4 hours", "4 saat", "4 小时", "4 часа" },
        ["tray.override"] = new[]
        {
            "Eigene Einstellung gilt bis {0} – Automatik fortsetzen",
            "Your setting applies until {0} – resume automation",
            "Ayarınız {0} saatine kadar geçerli – otomasyonu sürdür",
            "手动设置有效至 {0} – 恢复自动",
            "Ваша настройка действует до {0} – вернуть автоматику"
        },
        ["tip.override"] = new[] { "Eigene Einstellung bis {0}", "Your setting until {0}", "Ayarınız: {0}'e kadar", "手动设置至 {0}", "Ваша настройка до {0}" },
        ["n.override"] = new[] { "Vorrang beginnt und endet", "Priority starts and ends", "Öncelik başlar ve biter", "手动优先开始和结束时", "Приоритет начинается и заканчивается" },
        ["n.override.title"] = new[] { "Eigene Einstellung hat Vorrang", "Your setting has priority", "Ayarınız öncelikli", "手动设置优先", "Ваша настройка в приоритете" },
        ["n.override.body"] = new[]
        {
            "Programmregeln und Zeitplan pausieren bis {0}",
            "App rules and schedule are paused until {0}",
            "Uygulama kuralları ve zamanlama {0} saatine kadar duraklatıldı",
            "应用规则和计划已暂停至 {0}",
            "Правила для программ и расписание приостановлены до {0}"
        },
        ["n.override.end.title"] = new[] { "Automatik wieder aktiv", "Automation active again", "Otomasyon yeniden etkin", "自动功能已恢复", "Автоматика снова включена" },
        ["n.override.end"] = new[]
        {
            "Programmregeln und Zeitplan gelten wieder", "App rules and schedule apply again",
            "Uygulama kuralları ve zamanlama yeniden geçerli", "应用规则和计划已恢复生效", "Правила для программ и расписание снова действуют"
        },
        ["g.theme"] = new[] { "Design", "Theme", "Tema", "主题", "Тема" },
        ["theme.system"] = new[] { "Wie Windows", "Follow Windows", "Windows'u izle", "跟随系统", "Как в Windows" },
        ["theme.light"] = new[] { "Hell", "Light", "Açık", "浅色", "Светлая" },
        ["theme.dark"] = new[] { "Dunkel", "Dark", "Koyu", "深色", "Тёмная" },
        ["g.lang"] = new[] { "Sprache", "Language", "Dil", "语言", "Язык" },
        ["lang.auto"] = new[] { "Automatisch", "Automatic", "Otomatik", "自动", "Автоматически" },

        // ---- Tasten ----
        ["a.down"] = new[] { "Dunkler", "Darker", "Daha karanlık", "调暗", "Темнее" },
        ["a.up"] = new[] { "Heller", "Brighter", "Daha parlak", "调亮", "Ярче" },
        ["a.warmer"] = new[] { "Wärmer (Filter stärker)", "Warmer (stronger filter)", "Daha sıcak (güçlü filtre)", "更暖（滤镜更强）", "Теплее (фильтр сильнее)" },
        ["a.cooler"] = new[] { "Kälter (Filter schwächer)", "Cooler (weaker filter)", "Daha soğuk (zayıf filtre)", "更冷（滤镜更弱）", "Холоднее (фильтр слабее)" },
        ["a.toggle"] = new[] { "Blaulichtfilter an/aus", "Blue light filter on/off", "Mavi ışık filtresi aç/kapat", "蓝光过滤 开/关", "Фильтр синего света вкл/выкл" },
        ["a.pause"] = new[] { "Pause an/aus (1 Stunde)", "Pause on/off (1 hour)", "Duraklat aç/kapat (1 saat)", "暂停 开/关（1 小时）", "Пауза вкл/выкл (1 час)" },
        ["b.record"] = new[] { "Aufnehmen", "Record", "Kaydet", "录制", "Записать" },
        ["b.recording"] = new[] { "Taste drücken…", "Press a key…", "Bir tuşa basın…", "请按键…", "Нажмите клавишу…" },
        ["b.delete"] = new[] { "Löschen", "Clear", "Temizle", "清除", "Очистить" },
        ["k.hint"] = new[]
        {
            "„Aufnehmen“ anklicken und die gewünschte Taste drücken (auch mit Strg/Alt/Umschalt/Win). Medientasten werden ebenfalls erkannt. Die Fn-Taste selbst sieht Windows nicht: Kommt Fn+F1 nicht an, belege die Taste in Logitech Options+ mit einer Tastenkombination (z. B. Strg+Alt+F1) und nimm diese hier auf.",
            "Click “Record” and press the desired key (modifiers like Ctrl/Alt/Shift/Win are allowed). Media keys are recognized too. Windows cannot see the Fn key itself: if Fn+F1 does not arrive, assign the key a shortcut in Logitech Options+ (e.g. Ctrl+Alt+F1) and record that here.",
            "“Kaydet”e tıklayın ve istediğiniz tuşa basın (Ctrl/Alt/Shift/Win ile birlikte de olabilir). Medya tuşları da algılanır. Windows, Fn tuşunun kendisini göremez: Fn+F1 ulaşmıyorsa, tuşu Logitech Options+ içinde bir tuş kombinasyonuna (örn. Ctrl+Alt+F1) atayın ve burada kaydedin.",
            "点击“录制”，然后按下想要的按键（可搭配 Ctrl/Alt/Shift/Win）。媒体键同样可以识别。Windows 无法识别 Fn 键本身：如果 Fn+F1 没有反应，请在 Logitech Options+ 中将该键设置为组合键（例如 Ctrl+Alt+F1），然后在此处录制该组合键。",
            "Нажмите «Записать» и затем нужную клавишу (можно с Ctrl/Alt/Shift/Win). Медиаклавиши тоже распознаются. Windows не видит саму клавишу Fn: если Fn+F1 не срабатывает, назначьте этой клавише сочетание в Logitech Options+ (например, Ctrl+Alt+F1) и запишите его здесь."
        },
        ["key.ctrl"] = new[] { "Strg", "Ctrl", "Ctrl", "Ctrl", "Ctrl" },
        ["key.alt"] = new[] { "Alt", "Alt", "Alt", "Alt", "Alt" },
        ["key.shift"] = new[] { "Umschalt", "Shift", "Shift", "Shift", "Shift" },
        ["key.win"] = new[] { "Win", "Win", "Win", "Win", "Win" },
        ["hid.up"] = new[] { "Helligkeit + (Medientaste)", "Brightness + (media key)", "Parlaklık + (medya tuşu)", "亮度 +（媒体键）", "Яркость + (медиаклавиша)" },
        ["hid.down"] = new[] { "Helligkeit − (Medientaste)", "Brightness − (media key)", "Parlaklık − (medya tuşu)", "亮度 −（媒体键）", "Яркость − (медиаклавиша)" },
        ["hid.other"] = new[] { "Spezialtaste [{0}]", "Special key [{0}]", "Özel tuş [{0}]", "特殊按键 [{0}]", "Специальная клавиша [{0}]" },

        // ---- Blaulichtfilter ----
        ["f.on"] = new[] { "Blaulichtfilter aktiv", "Blue light filter enabled", "Mavi ışık filtresi etkin", "启用蓝光过滤", "Фильтр синего света включён" },
        ["f.label"] = new[] { "Farbtemperatur (kleiner = wärmer):", "Color temperature (lower = warmer):", "Renk sıcaklığı (düşük = daha sıcak):", "色温（越低越暖）：", "Цветовая температура (меньше = теплее):" },
        ["f.hint"] = new[]
        {
            "Der Filter verringert den Blauanteil über die Farbtabelle der Grafikkarte (wie f.lux). 6500 K ist neutral. Windows begrenzt extreme Werte – sehr niedrige Temperaturen werden dann automatisch abgeschwächt. Änderungen sind sofort sichtbar, gespeichert wird mit „Speichern“.",
            "The filter reduces the blue component via the graphics card's color table (like f.lux). 6500 K is neutral. Windows limits extreme values – very low temperatures are then weakened automatically. Changes are visible immediately and saved with “Save”.",
            "Filtre, ekran kartının renk tablosu üzerinden mavi oranını azaltır (f.lux gibi). 6500 K nötrdür. Windows aşırı değerleri sınırlar; çok düşük sıcaklıklar bu durumda otomatik olarak zayıflatılır. Değişiklikler anında görünür, “Kaydet” ile saklanır.",
            "该滤镜通过显卡的色彩表减少蓝光成分（类似 f.lux）。6500 K 为中性色。Windows 会限制极端数值，过低的色温会被自动减弱。更改会立即生效，点击“保存”后才会保存。",
            "Фильтр уменьшает долю синего через таблицу цветов видеокарты (как f.lux). 6500 K — нейтральный цвет. Windows ограничивает экстремальные значения — слишком низкие температуры автоматически ослабляются. Изменения видны сразу, сохраняются кнопкой «Сохранить»."
        },

        // ---- Zeitplan ----
        ["s.on"] = new[] { "Zeitplan aktiv", "Schedule enabled", "Zamanlama etkin", "启用计划", "Расписание включено" },
        ["s.transition"] = new[] { "Sanfter Übergang (Min., 0 = sofort)", "Smooth transition (min, 0 = instant)", "Yumuşak geçiş (dk, 0 = anında)", "平滑过渡（分钟，0 = 立即）", "Плавный переход (мин, 0 = сразу)" },
        ["s.fixed"] = new[] { "Feste Uhrzeiten", "Fixed times", "Sabit saatler", "固定时间", "Фиксированное время" },
        ["s.sun"] = new[] { "Sonnenaufgang / Sonnenuntergang", "Sunrise / sunset", "Gün doğumu / gün batımı", "日出 / 日落", "Восход / закат" },
        ["c.time"] = new[] { "Uhrzeit (HH:mm)", "Time (HH:mm)", "Saat (HH:mm)", "时间 (HH:mm)", "Время (HH:mm)" },
        ["c.brightness"] = new[] { "Helligkeit (%)", "Brightness (%)", "Parlaklık (%)", "亮度 (%)", "Яркость (%)" },
        ["c.kelvin"] = new[] { "Farbtemperatur (K)", "Color temperature (K)", "Renk sıcaklığı (K)", "色温 (K)", "Цветовая температура (K)" },
        ["s.lat"] = new[] { "Breitengrad", "Latitude", "Enlem", "纬度", "Широта" },
        ["s.lon"] = new[] { "Längengrad", "Longitude", "Boylam", "经度", "Долгота" },
        ["s.offset"] = new[] { "Versatz Sonnenuntergang (Min.)", "Sunset offset (min)", "Gün batımı farkı (dk)", "日落偏移（分钟）", "Сдвиг заката (мин)" },
        ["s.day"] = new[] { "Tag: Helligkeit (%)", "Day: brightness (%)", "Gündüz: parlaklık (%)", "白天：亮度 (%)", "День: яркость (%)" },
        ["s.night"] = new[] { "Nacht: Helligkeit (%)", "Night: brightness (%)", "Gece: parlaklık (%)", "夜间：亮度 (%)", "Ночь: яркость (%)" },
        ["s.hint"] = new[]
        {
            "Jeder Eintrag gilt ab seiner Uhrzeit, bis der nächste greift. Dazwischen kannst du jederzeit von Hand verstellen (das bricht einen laufenden Übergang ab). 6500 K bedeutet neutrale Farben (kein Filter). Ein negativer Versatz startet den Nachtmodus vor dem Sonnenuntergang. Koordinaten z. B. über Google Maps ermitteln (Standard: Berlin).",
            "Each entry applies from its time until the next one takes over. In between you can adjust manually at any time (this cancels a running transition). 6500 K means neutral colors (no filter). A negative offset starts night mode before sunset. Look up coordinates e.g. in Google Maps (default: Berlin).",
            "Her giriş, kendi saatinden itibaren bir sonraki giriş devreye girene kadar geçerlidir. Bu arada istediğiniz zaman elle ayarlayabilirsiniz (devam eden geçişi iptal eder). 6500 K nötr renkler demektir (filtre yok). Negatif fark, gece modunu gün batımından önce başlatır. Koordinatları örn. Google Maps'ten bulabilirsiniz (varsayılan: Berlin).",
            "每个条目从其设定时间起生效，直到下一个条目接管。期间你可以随时手动调整（这会中断正在进行的过渡）。6500 K 表示中性色（无滤镜）。负的偏移会让夜间模式在日落前开始。坐标可通过 Google 地图等查询（默认：柏林）。",
            "Каждая запись действует с указанного времени, пока не вступит в силу следующая. В промежутке можно в любой момент менять вручную (это прерывает текущий переход). 6500 K — нейтральные цвета (без фильтра). Отрицательный сдвиг включает ночной режим до заката. Координаты можно узнать, например, в Google Maps (по умолчанию: Берлин)."
        },
        ["s.suninfo"] = new[] { "Heute: Aufgang {0}, Untergang {1}", "Today: sunrise {0}, sunset {1}", "Bugün: doğuş {0}, batış {1}", "今天：日出 {0}，日落 {1}", "Сегодня: восход {0}, закат {1}" },
        ["s.nosun"] = new[] { "Heute kein Sonnenauf-/untergang", "No sunrise/sunset today", "Bugün gün doğumu/batımı yok", "今天没有日出/日落", "Сегодня нет восхода/заката" },
        // ---- Benachrichtigungen ----
        ["n.on"] = new[]
        {
            "Benachrichtigungen bei automatischen Änderungen anzeigen (unten rechts)",
            "Show notifications for automatic changes (bottom right)",
            "Otomatik değişikliklerde bildirim göster (sağ altta)",
            "自动更改时在右下角显示通知",
            "Показывать уведомления об автоматических изменениях (справа внизу)"
        },
        ["n.rule.start"] = new[]
        {
            "Programmregel greift (Programm wird aktiv)", "App rule applies (program becomes active)",
            "Uygulama kuralı devreye girer (program etkin olur)", "应用规则生效时（程序成为活动窗口）",
            "Правило применено (программа стала активной)"
        },
        ["n.rule.end"] = new[]
        {
            "Programmregel endet (Programm wird verlassen)", "App rule ends (program is left)",
            "Uygulama kuralı sona erer (programdan çıkılır)", "应用规则结束时（离开该程序）",
            "Правило завершено (программа покинута)"
        },
        ["n.sched"] = new[] { "Zeitplan ändert Werte", "Schedule changes values", "Zamanlama değerleri değiştirir", "计划更改数值时", "Расписание меняет значения" },
        ["n.pause"] = new[] { "Pause ist abgelaufen", "Pause has ended", "Duraklatma sona erdi", "暂停结束时", "Пауза закончилась" },
        ["n.rule.title"] = new[] { "Programmregel: {0}", "App rule: {0}", "Uygulama kuralı: {0}", "应用规则：{0}", "Правило для программы: {0}" },
        ["n.rule.end.title"] = new[] { "Programmregel beendet: {0}", "App rule ended: {0}", "Uygulama kuralı sona erdi: {0}", "应用规则已结束：{0}", "Правило завершено: {0}" },
        ["n.b.set"] = new[] { "Helligkeit auf {0} % gesetzt", "Brightness set to {0} %", "Parlaklık %{0} olarak ayarlandı", "亮度已设为 {0}%", "Яркость установлена на {0} %" },
        ["n.k.set"] = new[] { "Farbtemperatur auf {0} K gesetzt", "Color temperature set to {0} K", "Renk sıcaklığı {0} K olarak ayarlandı", "色温已设为 {0} K", "Цветовая температура установлена на {0} K" },
        ["n.b.back"] = new[] { "Helligkeit wieder auf {0} %", "Brightness back to {0} %", "Parlaklık tekrar %{0}", "亮度恢复为 {0}%", "Яркость возвращена к {0} %" },
        ["n.k.back"] = new[] { "Farbtemperatur wieder auf {0} K", "Color temperature back to {0} K", "Renk sıcaklığı tekrar {0} K", "色温恢复为 {0} K", "Цветовая температура возвращена к {0} K" },
        ["n.kept"] = new[] { "Einstellungen bleiben unverändert", "Settings stay as they are", "Ayarlar olduğu gibi kalıyor", "设置保持不变", "Настройки остаются без изменений" },
        ["n.sched.title"] = new[] { "Zeitplan {0}", "Schedule {0}", "Zamanlama {0}", "计划 {0}", "Расписание {0}" },
        ["n.sched.ramp"] = new[]
        {
            "Sanfter Übergang gestartet ({0} Min.): Helligkeit → {1} %, Farbtemperatur → {2} K",
            "Smooth transition started ({0} min): brightness → {1} %, color temperature → {2} K",
            "Yumuşak geçiş başladı ({0} dk): parlaklık → %{1}, renk sıcaklığı → {2} K",
            "平滑过渡已开始（{0} 分钟）：亮度 → {1}%，色温 → {2} K",
            "Плавный переход начат ({0} мин): яркость → {1} %, цветовая температура → {2} K"
        },
        ["n.pause.end.title"] = new[] { "Pause beendet", "Pause ended", "Duraklatma bitti", "暂停已结束", "Пауза завершена" },
        ["n.pause.end"] = new[]
        {
            "Blaulichtfilter und Zeitplan sind wieder aktiv", "Blue light filter and schedule are active again",
            "Mavi ışık filtresi ve zamanlama yeniden etkin", "蓝光过滤和计划已恢复", "Фильтр синего света и расписание снова активны"
        },

        // ---- Programme ----
        ["sec.apps"] = new[] { "Programme", "Apps", "Uygulamalar", "应用程序", "Программы" },
        ["r.on"] = new[] { "Programmregeln aktiv", "App rules enabled", "Uygulama kuralları etkin", "启用应用规则", "Правила для программ включены" },
        ["c.proc"] = new[] { "Programm", "Program", "Program", "程序", "Программа" },
        ["c.title"] = new[]
        {
            "Fenstertitel enthält (optional)", "Window title contains (optional)", "Pencere başlığı şunu içerir (isteğe bağlı)",
            "窗口标题包含（可选）", "Заголовок окна содержит (необязательно)"
        },
        ["r.add"] = new[] { "Programm hinzufügen…", "Add program…", "Program ekle…", "添加程序…", "Добавить программу…" },
        ["r.remove"] = new[] { "Entfernen", "Remove", "Kaldır", "移除", "Удалить" },
        ["r.leave"] = new[] { "Beim Verlassen des Programms:", "When leaving the program:", "Programdan çıkıldığında:", "离开该程序时：", "При выходе из программы:" },
        ["r.restore"] = new[] { "Vorherige Einstellung wiederherstellen", "Restore previous settings", "Önceki ayarları geri yükle", "恢复之前的设置", "Восстановить прежние настройки" },
        ["r.keep"] = new[] { "Einstellung beibehalten", "Keep the setting", "Ayarı koru", "保持当前设置", "Оставить настройку" },
        ["r.fadeb"] = new[] { "Weicher Wechsel – Helligkeit", "Soft switch – brightness", "Yumuşak geçiş – parlaklık", "平滑切换 – 亮度", "Плавное переключение – яркость" },
        ["r.fadek"] = new[] { "Weicher Wechsel – Farbtemperatur", "Soft switch – color temperature", "Yumuşak geçiş – renk sıcaklığı", "平滑切换 – 色温", "Плавное переключение – цветовая температура" },
        ["r.instant"] = new[] { "sofort", "instant", "anında", "立即", "сразу" },
        ["unit.s"] = new[] { "s", "s", "sn", "秒", "с" },
        ["r.none"] = new[] { "Keine Programme mit Fenster gefunden", "No programs with a window found", "Pencereli program bulunamadı", "未找到带窗口的程序", "Программы с окнами не найдены" },
        ["r.hint"] = new[]
        {
            "Die Regel gilt, solange das Programm im Vordergrund ist – laufen mehrere Programme, zählt nur das aktive Fenster. Leere Felder bedeuten „nicht ändern“. Für Webseiten (z. B. Netflix im Browser) und Store-Apps trägst du zusätzlich einen Teil des Fenstertitels ein. Regeln mit Fenstertitel haben Vorrang.",
            "A rule applies while the program is in the foreground – if several programs are running, only the active window counts. Empty fields mean “don't change”. For websites (e.g. Netflix in a browser) and Store apps, also enter part of the window title. Rules with a window title take priority.",
            "Kural, program ön plandayken geçerlidir – birden fazla program çalışıyorsa yalnızca etkin pencere sayılır. Boş alanlar “değiştirme” anlamına gelir. Web siteleri (örn. tarayıcıda Netflix) ve Store uygulamaları için ayrıca pencere başlığının bir bölümünü girin. Pencere başlıklı kurallar önceliklidir.",
            "规则仅在该程序位于前台时生效——如果同时运行多个程序，只看当前活动窗口。留空表示“不更改”。对于网页（例如浏览器中的 Netflix）和应用商店应用，请同时填写窗口标题的一部分。带窗口标题的规则优先。",
            "Правило действует, пока программа на переднем плане — если запущено несколько программ, учитывается только активное окно. Пустые поля означают «не менять». Для сайтов (например, Netflix в браузере) и приложений из Store дополнительно укажите часть заголовка окна. Правила с заголовком окна имеют приоритет."
        },
        ["err.rule"] = new[]
        {
            "Ungültige Programmregel.\nProgramm angeben; Helligkeit 0–100 und/oder Farbtemperatur 2500–6500 K (leer = nicht ändern).",
            "Invalid app rule.\nEnter a program; brightness 0–100 and/or color temperature 2500–6500 K (empty = don't change).",
            "Geçersiz uygulama kuralı.\nBir program belirtin; parlaklık 0–100 ve/veya renk sıcaklığı 2500–6500 K (boş = değiştirme).",
            "应用规则无效。\n请填写程序；亮度 0–100 和/或色温 2500–6500 K（留空 = 不更改）。",
            "Недопустимое правило для программы.\nУкажите программу; яркость 0–100 и/или цветовая температура 2500–6500 K (пусто = не менять)."
        },

        ["err.title"] = new[] { "Zeitplan", "Schedule", "Zamanlama", "计划", "Расписание" },
        ["err.entry"] = new[]
        {
            "Ungültiger Zeitplan-Eintrag.\nUhrzeit als HH:mm, Helligkeit 0–100, Farbtemperatur 2500–6500 K.",
            "Invalid schedule entry.\nTime as HH:mm, brightness 0–100, color temperature 2500–6500 K.",
            "Geçersiz zamanlama girişi.\nSaat HH:mm biçiminde, parlaklık 0–100, renk sıcaklığı 2500–6500 K.",
            "计划条目无效。\n时间格式为 HH:mm，亮度 0–100，色温 2500–6500 K。",
            "Недопустимая запись расписания.\nВремя в формате HH:mm, яркость 0–100, цветовая температура 2500–6500 K."
        },
    };
}
