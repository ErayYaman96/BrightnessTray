# BrightnessTray

**Deutsch** | [English below](#english)

Kleines Windows-Tray-Programm, um die Helligkeit externer Monitore (und Laptop-Displays) bequem zu steuern – per Regler, Tastenkürzel, Mausrad, Zeitplan oder automatisch je nach Programm. Mit Blaulichtfilter.

> ⚠️ Der Code ist nur auf wenigen Geräten getestet. DDC/CI verhält sich je nach Monitor sehr unterschiedlich. Nutzung auf eigene Verantwortung (siehe \[LICENSE](LICENSE)).

## Funktionen

* Helligkeitsregler im Tray-Flyout, pro Monitor
* Tastenkürzel (Standard: F1 / F2), frei belegbar – auch Medien-/HID-Tasten; gedrückt halten wiederholt
* Anzeige (OSD) wie bei der Lautstärke
* Mausrad auf dem Tray-Symbol (Strg = Farbtemperatur)
* Blaulichtfilter (2500–6500 K, 100-K-Schritte)
* Zeitplan: feste Uhrzeiten oder Sonnenauf-/untergang nach Koordinaten, mit sanften Übergängen
* Programmregeln: z. B. Photoshop 100 %, Netflix 50 % – mit weichem Wechsel und wahlweise „vorherige Einstellung wiederherstellen“ oder „beibehalten“
* Kombinationen: mehrere Programme gleichzeitig sichtbar (z. B. nebeneinander) → eigene Helligkeit/Farbtemperatur
* Manueller Vorrang: eigene Änderungen setzen die Automatik für 15 Min. bis „bis Neustart“ außer Kraft
* Automatik komplett abschaltbar (Einstellungen bleiben erhalten), Pause-Funktion
* Benachrichtigungen mit Grund der Änderung (abschaltbar, mit Unterstufen)
* Hell-/Dunkelmodus, Sprachen: Deutsch, English, Türkçe, 中文简体, Русский

## Datenschutz

Keine Tracker, keine Online-Verbindung, keine Telemetrie, nichts wird gesendet oder empfangen. Gespeichert werden nur deine Einstellungen (`%AppData%\\BrightnessTray\\settings.json`) und optional der Autostart-Eintrag. Details und eine Selbstprüfung für den Quellcode stehen in [PRIVACY.md](PRIVACY.md).

Hinweis: Das Programm nutzt einen globalen Tastatur-Hook, um deine Tastenkürzel zu erkennen. Manche Virenscanner melden das fälschlich. Der Hook vergleicht nur mit deinen Kürzeln; der Quellcode ist offen einsehbar.

## Installation

1. Unter **Releases** die `BrightnessTray.exe` herunterladen und starten. (Windows SmartScreen warnt evtl., weil die Datei nicht signiert ist: „Weitere Informationen“ → „Trotzdem ausführen“.)
2. Rechtsklick auf das Sonnensymbol im Tray → **Einstellungen**.

### Selbst bauen

Voraussetzung: [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```
EXE-erstellen.bat
```

oder manuell: `dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true`

## Tipps

* **Fn-Tasten (z. B. Logitech MX Keys):** Windows sieht die Fn-Taste selbst nicht. Workaround: in Logitech Options+ Fn+F1/F2 auf Strg+Alt+F1/F2 legen und diese Kombination hier zuweisen.
* Einstellungen zurücksetzen: Programm beenden, `settings.json` löschen.

## Unterstützen

Wenn dir das Programm gefällt, freue ich mich über eine Spende:

**paypal.me/erayaman**

## Lizenz

MIT – siehe [LICENSE](LICENSE).

\---

<a id="english"></a>

# BrightnessTray (English)

A small Windows tray app to control the brightness of external monitors (and laptop panels) – via slider, hotkeys, mouse wheel, schedule, or automatically per program. Includes a blue light filter.

> ⚠️ Only tested on a few devices. DDC/CI behaves very differently between monitors. Use at your own risk (see \[LICENSE](LICENSE)).

## Features

* Per-monitor brightness slider in the tray flyout
* Hotkeys (default F1 / F2), freely assignable incl. media/HID keys; hold to repeat
* On-screen display like the volume indicator
* Mouse wheel on the tray icon (Ctrl = color temperature)
* Blue light filter (2500–6500 K, 100 K steps)
* Schedule: fixed times or sunrise/sunset by coordinates, with smooth transitions
* App rules: e.g. Photoshop 100 %, Netflix 50 % – smooth switching, with “restore previous settings” or “keep” on leaving
* Combinations: several programs visible at the same time (e.g. side by side) → their own brightness/color temperature
* Manual priority: your own changes pause automation from 15 minutes up to “until restart”
* Automation can be switched off completely (settings are kept), pause function
* Notifications explaining what changed and why (can be turned off, with sub-levels)
* Light/dark mode; languages: Deutsch, English, Türkçe, 中文简体, Русский

## Privacy

No trackers, no online connectivity, no telemetry – nothing is sent or received. Only your settings (`%AppData%\\BrightnessTray\\settings.json`) and an optional autostart entry are stored. See [PRIVACY.md](PRIVACY.md) for details and a self-check for the source code.

Note: the app uses a global keyboard hook to detect your hotkeys. Some antivirus tools flag this falsely. The hook only compares against your own shortcuts; the source code is open.

## Installation

1. Download `BrightnessTray.exe` from **Releases** and run it. (Windows SmartScreen may warn because the file is unsigned: “More info” → “Run anyway”.)
2. Right-click the sun icon in the tray → **Settings**.

### Build from source

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0). Run `EXE-erstellen.bat`, or: `dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true`

## Tips

* **Fn keys (e.g. Logitech MX Keys):** Windows cannot see the Fn key itself. Workaround: map Fn+F1/F2 to Ctrl+Alt+F1/F2 in Logitech Options+ and assign that combination here.
* Reset settings: quit the app and delete `settings.json`.

## Support

If you like the app, a donation is very welcome:

**paypal.me/erayaman**

## License

MIT – see [LICENSE](LICENSE).

