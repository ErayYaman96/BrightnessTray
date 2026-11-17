# BrightnessTray – Datenschutz & Transparenz

BrightnessTray arbeitet vollständig lokal. Der Quellcode enthält

- **keine Netzwerkzugriffe** (kein HTTP, keine Sockets, kein DNS, kein E-Mail-Versand),
- **keine Tracker, Telemetrie, Analyse- oder Absturzberichts-Dienste**,
- **keine automatische Update-Prüfung**,
- **keinen Datei-Upload oder -Download**,
- **keine Fremd-Bibliotheken außer `System.Management`** (Microsoft, für die Helligkeit von Laptop-Displays).

## Was lokal gespeichert wird

| Was | Wo | Wozu |
|---|---|---|
| Deine Einstellungen (Tasten, Zeitplan, Programmregeln, Sprache, Design …) | `%AppData%\BrightnessTray\settings.json` | Einstellungen merken |
| Autostart-Eintrag (nur wenn du „Mit Windows starten“ aktivierst) | `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` | Start mit Windows |

Sonst wird nichts geschrieben – keine Protokolle, keine Verlaufsdaten.

## Was das Programm zur Laufzeit liest (nur im Arbeitsspeicher, nie gespeichert oder gesendet)

- **Tastatur/Mausrad:** Der globale Hook vergleicht Tastendrücke nur mit deinen eingestellten Tastenkürzeln. Tastendrücke werden nicht aufgezeichnet.
- **Vordergrundfenster:** Prozessname und Fenstertitel werden nur mit deinen Programmregeln verglichen. Nur die Regeln, die *du* anlegst, landen in den Einstellungen.
- **Sichtbare Fenster (Kombinationen):** Für Kombinationsregeln wird alle 1,5 s geprüft, welche Programme sichtbar sind (nur Programmnamen und Fensterposition, rein im Arbeitsspeicher). Es wird nichts gespeichert, protokolliert oder gesendet; gespeichert werden nur die Kombinationen, die *du* anlegst.
- **Windows-Design (Hell/Dunkel)** aus der Registry, **Monitor-Helligkeit** über DDC/CI bzw. lokales WMI.

## Selbst nachprüfen

Im Projektordner (PowerShell):

```powershell
Select-String -Path *.cs,*.csproj -Pattern "System\.Net|HttpClient|WebClient|Socket|https?://|Telemetry|Process\.Start"
```

Die Ausgabe muss leer sein. Zusätzlich kannst du die Windows-Firewall so einstellen, dass `BrightnessTray.exe` ausgehende Verbindungen blockiert – das Programm funktioniert unverändert.

## Hinweis zum Bauen

Nicht das Programm, sondern die Entwicklungswerkzeuge verbinden sich mit dem Internet:
`dotnet build` lädt einmalig das Paket `System.Management` von nuget.org, und das .NET SDK sendet standardmäßig Nutzungsdaten an Microsoft. Letzteres schaltest du vor dem Bauen ab mit:

```powershell
setx DOTNET_CLI_TELEMETRY_OPTOUT 1
```
