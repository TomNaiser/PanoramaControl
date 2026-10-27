# Windows 27.5.3 — Abgleich mit dem bestätigten Android-Stand

Stand: 27. Oktober 2026. Referenz ist der vom Benutzer bestätigte Stand aus
Android **0.6.27 / Code 40**, regulärer Firmware **2.4.75** und gespeicherter
Motorkonfiguration **Revision 39**. Hauptprojekt-Commit: `665f0dc`;
Firmware-Quellcommit: `55ff4bb9e74085bacb71bb94783856b67a685f55`.
Der lokale Firmwareversuch 2.4.76 gehört nicht zu dieser Referenz.

## Windows-Funktionsstand

Die technische Android-Angleichung aus 27.5.0 ist enthalten: SQLite mit
Journalimport, atomare laufende XML-Sicherung, verknüpfte Restaufträge mit
ursprünglichen Raster-/Sphärenzielen, Stabilitätseinstellungen, Qualitätspause,
unabhängige Schreibfrist und Deutsch/Englisch. Ebenso enthalten sind die
Tempolimit-/Navigationskorrekturen aus 27.5.1 und die direkten Surface-
Touchkontakte einschließlich Loslassen und Captureverlust aus 27.5.2.

27.5.3 sichert den aktuellen Android-Praxisfall zusätzlich im Windows-
Controller ab. Die Simulatorantworten prüfen jetzt die Laufnummer bei
START, STOP, PAUSE und RESUME wie das Protokoll. Die bestehende korrekte
STOP-Übertragung in Windows wurde beibehalten. Ein neuer Test prüft:

- Genaues 43×6-Rezept mit 1700/1700 ‰ und 500/30/300 ms übertragen;
  Übertragen allein startet keine Bewegung.
- Bei Pause nach 18 Bildern einen Restauftrag ab der vorherigen Spalte
  vorbereiten; exakt 41×6 Ziele ab Pan 82,149° wie Android.
- Abgelehnter STOP erhält den pausierten Auftrag und alle 18 gemessenen
  Aufnahmen; kein CLEAR, LOAD oder START folgt.
- Erfolgreiche Vorbereitung bestätigt STOP, CLEAR, LOAD und den Geräteplan;
  erst ein ausdrücklicher START mit neuer Laufnummer startet den Restlauf.
- Verbindung, Aufnahme und Wiederaufnahme schreiben weder Motorprofil noch
  Tilt-Nullpunkt oder Grenzen. Die gemeldete Konfigurationsrevision bleibt 39.

Die WinForms-Prüfung setzt die sichtbaren Eingaben für Tempo und Zeiten und
prüft dieselben Werte im tatsächlich gesendeten LOAD_PLAN. Versionsanzeige,
Assembly, Projekt und Paket tragen einheitlich 27.5.3.

## Präzises Anfahren und gleiche Aufnahmebedingungen

Die am Kopf behobene große Tilt-Abweichung war eine gespeicherte Einstellung:
`control.travelStartBrakeLeadSeconds` wurde **0,200 → 0,110 s** geändert.
Die anderen 128 Motorparameter sind unverändert, Feinannäherung bleibt aus.
Diese geräteseitige Einstellung gilt für beide Clients. Windows setzt sie
beim Verbinden nicht zurück. 0,110 s wird nicht heimlich als neuer
Firmware-Standard oder neue Programmeinstellung angewandt.

Für einen Vergleich mit dem bestätigten Android-Lauf in Windows einstellen:

| Windows-Eingabe | Wert | Protokollwert |
|---|---:|---:|
| Lauftempo Pan / Tilt | jeweils 1,7 | jeweils 1700 ‰ |
| Beruhigung | 0,5 s | 500 ms |
| Kontrollierter Triggerpuls | 0,03 s | 30 ms |
| Belichtungswartezeit | 0,3 s | 300 ms |

Vorhandene Kameraprofile und persönliche Einstellungen werden beim Update
nicht ersetzt. Die Tabelle beschreibt die Referenzbedingungen; abweichende
Belichtungszeiten müssen weiterhin zur tatsächlichen Kamera passen.

Das vom Paketbuild optional beigelegte
`MotionConfig.precise-2.4.75-20261027.json` enthält alle 129 Werte des
geprüften Kopfs. `REFERENCE.json` nennt Firmware, Android-Basis und den
SHA-256 des beigelegten Profils. Das Profil wird **nicht automatisch
geschrieben**. Es enthält gerätespezifische Werte und Grenzen.

## Nachweis und Grenzen

Die Host-/Controllerprüfung besteht mit **1.427 Assertions**. Der Paketbuild
führt außerdem die echte .NET-4.8-WinForms-Anwendung mit simuliertem Gerät,
Vorschau-, Navigations- und nativen Touch-Nachrichtenprüfungen aus. Es werden
keine realen Bewegungen oder Kameraauslösungen gestartet.

Abschließender Paketlauf bestanden: **129 WinForms-, 16 Vorschau-,
34 Navigations- und 19 Touchprüfungen**, zusammen mit den Hostfällen
**1.625 Softwareprüfungen**. Die Versionsanzeige bleibt auch nach
Statusmeldungen bei 27.5.3. Der Paketbuild enthält sieben Warnungen aus
historischen Legacy-Klassen (unbenutzte Variablen und unerreichbarer Code),
keine Buildfehler.

Am Kopf wurden zuvor unter Android 57 Auslösungen verglichen. Alle 39 Bilder
mit 0,110 s blieben bei Tilt unter 0,3°; die Gegenprobe mit 0,200 s erreichte
0,665°. Die letzte 12er-Reihe erreichte maximal 0,291° Tilt und 0,242° Pan
ohne Fehlermeldung. Der Benutzer bestätigt präzises und schnelles Anfahren.
Diese Hardwaremessung wird nicht als Windows-Hardwareabnahme ausgegeben.

Die neue reale Windows-Abnahme auf dem Surface, Bluetooth-COM-Zuordnung,
Wiederaufnahme unter Last sowie ein vollständiges Panorama bleiben offen.
Die Pan-Achse wurde nicht separat abgestimmt. Frühere einzelne
STEP_MOVE_TIMEOUT-Meldungen sind kein pauschal behobener Fehler.
Es wurden für diese Windows-Angleichung weder Firmware geflasht noch
Motorparameter geändert oder zusätzliche Auslösungen verbraucht.

## Paket bauen

```powershell
# Im Windows-Unterprojekt; ohne Referenzprofil auch eigenständig baubar:
pwsh -File Tests/build-windows.ps1

# Im Gesamtprojekt, einschließlich des geprüften Profils:
pwsh -File PanoramaControlGitRepo/Tests/build-windows.ps1 -MotionProfile PanoramaAndroid/config/MotionConfig.precise-2.4.75-20261027.json
```

Ergebnis: `releases/27.5.3/` und `releases/PanoramaControl-27.5.3.zip`.
EXE, vorhandene Begleitdateien, Bedienhinweise und Referenzmanifest gehören
zusammen. Die persönlichen Daten unter `%LOCALAPPDATA%/PanoramaControl`
bleiben getrennt vom Paketverzeichnis. Ein verwendeter COM-Port muss auf
dem jeweiligen Windows-Gerät weiterhin zum richtigen Kopf gehören.
