# PanoramaControl

Windows-Forms-Bedienung für den motorisierten Pan-/Tilt-Panoramakopf mit PanoDuinoControl-Firmware. Aktueller Quellstand: **27.5.4**, Zielplattform **.NET Framework 4.8**, Protokoll V2.

## Einstieg

Aktives Projekt: [PanoramaControl Version 27.csproj](PanoramaControl/PanoramaControl%20Version%2027.csproj). Das Version-26-Projekt und ältere Kommunikationsklassen sind historische beziehungsweise weiterhin eingebundene Kompatibilitätspfade und nicht das aktuelle Auslieferungsziel.

Raster- und sphärische Planung, getrenntes Übertragen/Starten, manuelle Ziel-/Haltefahrt, Pause/Fortsetzen, Spaltennavigation, Live-Zeiten, Timelapse, Kameraprofile, Warnpunkte und gespeicherte Läufe sind umgesetzt. Die Geräteposition stammt aus Statusmeldungen, nicht aus einem ACK.

27.5.0 ergänzt SQLite mit Journalimport, laufende atomare XML-Sicherung, verknüpfte Restaufträge, Stabilitätseinstellungen, Qualitätspause, unabhängige Schreibfrist und Deutsch/Englisch. Das bestehende WinForms-Layout bleibt erhalten. [Änderungen, Bedienung und Prüfgrenzen](docs/history/WINDOWS-27.5.0-ANDROID-PARITAET.md).

27.5.1 korrigiert die deutsche Überlappungsbeschriftung und Haltefahrt bei niedrigerem Geräte-Höchsttempo. [Die damalige offene Pan-Diagnose](docs/history/WINDOWS-27.5.1-NAVIGATION-DIAGNOSE.md) wurde inzwischen durch reale Vergleichsreihen und die Pan-Korrektur mit Revision 40 fortgeführt.

## Bauen und prüfen

27.5.4 ergänzt ausdrücklich wählbare Android-Referenzwerte für Aufnahmezeiten und beide Lauftempi, korrigiert deren Übernahme vor dem Laden und verhindert überlappende große Diagnoseübertragungen. Nach den [ursprünglichen Vergleichsläufen](docs/WINDOWS-27.5.4-PAN-VERGLEICH.md) wurde das gespeicherte Pan-Ankunftsfenster gezielt auf 0,15°/0,25° korrigiert: **Revision 40**, andere 127 Werte unverändert. **96 reale Aufnahmen** ergaben Pan maximal 0,236°, Median 0,089°; der 72er-Lauf enthielt 13 Zielverfehlungswarnungen. Firmware 2.4.75 und Android 0.6.27 bleiben erhalten. [Pan-Korrektur, Profilpaket und Grenzen](docs/WINDOWS-27.5.4-PAN-KORREKTUR.md).

27.5.3 führt den Windows-Stand mit der bestätigten Referenz Android 0.6.27 / Firmware 2.4.75 / Motorprofil Revision 39 zusammen. Das Android-Praxisraster, pausierte Restaufträge und die Erhaltung des präzisen Motorprofils sind zusätzlich geprüft. Surface-Touch aus 27.5.2 bleibt enthalten. [Referenzbedingungen, Paket und Nachweisgrenzen](docs/WINDOWS-27.5.3-ANDROID-0.6.27.md).

```powershell
dotnet run --project Tests/ProtocolTests/ProtocolTests.csproj
pwsh -File Tests/build-windows.ps1
```

Hosttests benötigen .NET 8; die eigentliche WinForms-Anwendung bleibt .NET Framework 4.8. Das Paketskript führt simulierte UI-Tests aus und erzeugt ohne Profil `releases/27.5.4` sowie ein ZIP. Vom Gesamtprojekt aus erzeugt `pwsh -File PanoramaControlGitRepo/Tests/build-windows.ps1 -MotionProfile PanoramaAndroid/config/MotionConfig.precise-pan-2.4.75-20261027.json` das separate Paket `27.5.4-pan-r40` mit dem geprüften vollständigen Profil. Es wird nicht automatisch angewandt. Das Skript startet keine reale Fahrreihe. Der letzte Paketbuild bestand 1.633 Softwareprüfungen.

## Daten und Konfiguration

Aktuelle Einstellungen und Capture-Journale liegen unter `%LOCALAPPDATA%/PanoramaControl/`. `PanoControlConfig.xml` neben der Anwendung enthält die bestehende Konfiguration; die versionierte Datei ist ein Beispiel und ersetzt keine persönliche Sicherung.

Neue Aufnahmen und Wiederaufnahmestände liegen transaktional in `Runs/captures.db`; alte prüfsummengeschützte Journale werden unverändert importiert. Vollständige Papywizard-XML wird unter `Papywizard/` im Hintergrund atomar ersetzt. Die Wiederaufnahme benötigt eine eindeutig bestätigte Bluetooth-Gerätezuordnung, unveränderte Boot-/Tilt-Referenz und einen bestätigten Geräteplan. Laden und Start bleiben getrennt.

Auslieferungsziel ist Windows 10/11 x64 mit .NET Framework 4.8 und dem Windows-Systemprovider `winsqlite3.dll`. Ein Laufzeitwechsel wurde nicht vorgenommen. Neue SQLite-Läufe sind nicht direkt mit alten Windows-Versionen lesbar.

## Versionshinweise

- [27.5.4 / Revision 40: reale Pan-Korrektur und 96 Aufnahmen](docs/WINDOWS-27.5.4-PAN-KORREKTUR.md)
- [27.5.4: ursprünglicher Pan-Vergleich und Diagnosekorrektur](docs/WINDOWS-27.5.4-PAN-VERGLEICH.md)
- [27.5.3: bestätigter Android-/Firmwarestand und geprüfte Aufnahmebedingungen](docs/WINDOWS-27.5.3-ANDROID-0.6.27.md)

- [27.5.1: Layout, Haltefahrt und offene Bewegungsdiagnose](docs/history/WINDOWS-27.5.1-NAVIGATION-DIAGNOSE.md)
- [27.5.0: technische Android-Angleichung](docs/history/WINDOWS-27.5.0-ANDROID-PARITAET.md)

- [27.4.4: blockierender Transport](docs/history/WINDOWS-27.4.4-ROBUSTHEIT.md)
- [27.4.3: Rastervorschau](docs/history/WINDOWS-27.4.3-RASTERVORSCHAU.md)
- [27.4.2: Bedienung](docs/history/WINDOWS-27.4.2-BEDIENUNG.md)
- [27.4.1: Funktionsabgleich](docs/history/WINDOWS-27.4.1-ABGLEICH.md)
- [27.4.0: Umsetzung und Prüfgrenzen](docs/history/WINDOWS-27.4.0-ABNAHME.md)

Das Gesamtprojekt bietet eine [englische Übersicht](https://github.com/TomNaiser/PanoramaSoftware/blob/main/README.md), ein [kurzes englisches Manual](https://github.com/TomNaiser/PanoramaSoftware/blob/main/docs/en/manual.md) und das [ausführliche deutsche Handbuch](https://github.com/TomNaiser/PanoramaSoftware/blob/main/docs/README.md). Persönliche Benutzerdateien, Buildpakete und Laufexporte werden nicht mit den Quellen verteilt.

Zur Projektgeschichte und den früheren Aufbauten: [Hardwarebeschreibung](https://sites.google.com/site/thomasnaiser/home/gigapixel-hardware) und [PanoBoard-Repository](https://github.com/TomNaiser/PanoBoard). Die aktuelle Softwarebelegung und die Grenzen der heutigen Hardwareprüfung stehen im gemeinsamen Handbuch.

Lizenz: [MIT](LICENSE). Firmware: [PanoDuinoControl](https://github.com/TomNaiser/PanoDuinoControl).
