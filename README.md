# PanoramaControl

Windows-Forms-Bedienung für den motorisierten Pan-/Tilt-Panoramakopf mit PanoDuinoControl-Firmware. Aktueller Quellstand: **27.4.4**, Zielplattform **.NET Framework 4.8**, Protokoll V2.

## Einstieg

Aktives Projekt: [PanoramaControl Version 27.csproj](PanoramaControl/PanoramaControl%20Version%2027.csproj). Das Version-26-Projekt und ältere Kommunikationsklassen sind historische beziehungsweise weiterhin eingebundene Kompatibilitätspfade und nicht das aktuelle Auslieferungsziel.

Raster- und sphärische Planung, getrenntes Übertragen/Starten, manuelle Ziel-/Haltefahrt, Pause/Fortsetzen, Spaltennavigation, Live-Zeiten, Timelapse, Kameraprofile, Warnpunkte und gespeicherte Läufe sind umgesetzt. Die Geräteposition stammt aus Statusmeldungen, nicht aus einem ACK.

27.4.4 verlagert serielle Writes aus dem UI-Thread und der ACK-/Abbruchsperre. Der dokumentierte Stand bestand **192 Protokoll-/Controller- und 115 simulierte WinForms-Prüfungen**.

## Bauen und prüfen

```powershell
dotnet run --project Tests/ProtocolTests/ProtocolTests.csproj
pwsh -File Tests/build-windows.ps1
```

Hosttests benötigen .NET 8; die eigentliche WinForms-Anwendung bleibt .NET Framework 4.8. Das Paketskript führt simulierte UI-Tests aus und erzeugt `releases/27.4.4` sowie ein ZIP. Es startet keine reale Fahrreihe.

## Daten und Konfiguration

Aktuelle Einstellungen und Capture-Journale liegen unter `%LOCALAPPDATA%/PanoramaControl/`. `PanoControlConfig.xml` neben der Anwendung enthält die bestehende Konfiguration; die versionierte Datei ist ein Beispiel und ersetzt keine persönliche Sicherung.

Das Journal ist prüfsummengeschützt. Der klassische Live-Papywizard-Writer schließt sein XML erst bei Abschluss; ein vollständiger Journal-Snapshot ist ein gesonderter Exportweg. Die laufende atomare XML-Sicherung und die umfassende Fault-Restauftragsführung der neuesten Android-App sind noch nicht portiert.

## Versionshinweise

- [27.4.4: blockierender Transport](docs/history/WINDOWS-27.4.4-ROBUSTHEIT.md)
- [27.4.3: Rastervorschau](docs/history/WINDOWS-27.4.3-RASTERVORSCHAU.md)
- [27.4.2: Bedienung](docs/history/WINDOWS-27.4.2-BEDIENUNG.md)
- [27.4.1: Funktionsabgleich](docs/history/WINDOWS-27.4.1-ABGLEICH.md)
- [27.4.0: Umsetzung und Prüfgrenzen](docs/history/WINDOWS-27.4.0-ABNAHME.md)

Das Gesamtprojekt bietet eine [englische Übersicht](https://github.com/TomNaiser/PanoramaSoftware/blob/main/README.md), ein [kurzes englisches Manual](https://github.com/TomNaiser/PanoramaSoftware/blob/main/docs/en/manual.md) und das [ausführliche deutsche Handbuch](https://github.com/TomNaiser/PanoramaSoftware/blob/main/docs/README.md). Persönliche Benutzerdateien, Buildpakete und Laufexporte werden nicht mit den Quellen verteilt.

Zur Projektgeschichte und den früheren Aufbauten: [Hardwarebeschreibung](https://sites.google.com/site/thomasnaiser/home/gigapixel-hardware) und [PanoBoard-Repository](https://github.com/TomNaiser/PanoBoard). Die aktuelle Softwarebelegung und die Grenzen der heutigen Hardwareprüfung stehen im gemeinsamen Handbuch.

Lizenz: [MIT](LICENSE). Firmware: [PanoDuinoControl](https://github.com/TomNaiser/PanoDuinoControl).
