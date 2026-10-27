# Windows 27.5.0 — technische Angleichung an Android 0.6.26

Stand: 25. Oktober 2026. WinForms / .NET Framework 4.8, x64. Auslieferungsziel: Windows 10/11 mit dem Systemprovider `winsqlite3.dll`. Firmware-Vertrag: V2 einschließlich der optionalen Aufnahme-Recovery- und Stabilitätsfähigkeiten von 2.4.75. Die Firmware wurde für diese Änderung nicht verändert.

## Änderungen

- SQLite ersetzt das Journal als maßgebliche neue Laufablage. Vorhandene `.panojournal`-Dateien werden wiederholbar importiert und bleiben unverändert erhalten. Unvollständige oder ungültige Records werden ausgelassen und gemeldet. Historische Mess-/Rekonstruktionskennzeichen und Warnflags bleiben erhalten; fehlende Wiederaufnahmereferenzen werden nicht erfunden.
- Der Sitzungscontroller speichert Captures, Gerätepläne, Status-/Segmentkontext und Fehlerstände unabhängig von der dargestellten Formansicht. Capture-Replay wird dedupliziert; reale Wiederholungsaufnahmen bleiben eigene Einträge.
- Vollständige Papywizard-XML wird im Hintergrund aus SQLite erzeugt, geflusht und atomar ersetzt. Die letzte gültige Fassung bleibt bei Dateisperren und Schreibfehlern erhalten. Ein Neustart ergänzt noch nicht veröffentlichte Captures. Exporter überlappender Forminstanzen sind für dasselbe Zielverzeichnis serialisiert.
- Restaufträge verknüpfen den unterbrochenen Lauf mit einer neuen Laufkennung. Reservierung erfolgt vor CLEAR/LOAD. Rasterspalten und sphärische Originalbänder bleiben erhalten; nötiges Zurückverlegen einer sphärischen Schnittgrenze wird angezeigt. Start erfolgt separat.
- Wiederaufnahme prüft Bluetooth-Gerätezuordnung, Bootkennung, Tilt-Referenz, aktuelle Grenzen, Statusfrische und exakten Geräteplan. Ein COM-Portname allein genügt nicht. Nach einem Abbruch zwischen CLEAR und LOAD wird die gespeicherte Reservierung wiederverwendet; ein bereits geladener passender Restplan wird nach Neuverbindung abgeglichen. Ein abweichender Geräteplan darf die Reservierung nicht überschreiben und bleibt für Start gesperrt.
- Stabilitätswerte können mit Konfigurationsrevision gelesen, gespeichert und zurückgelesen werden. Die optionale Qualitätspause über 1° bleibt von Warnschwellen und Firmware-Faults getrennt. Erhaltene Fehlerpausen und `RETRY_COLUMN` werden unterstützt; eine vorbereitete Spaltenwiederholung bleibt bis zum ausdrücklichen Fortsetzen pausiert.
- Eine unabhängige Schreibfrist beendet blockierte Protokollaufträge. COM-Schreiben und das Schließen alter Portobjekte halten die Sitzungssperre nicht mehr fest; alte Portfehler dürfen eine neue Verbindung nicht schließen. Heartbeat und Statusprüfung laufen unabhängig vom UI. Diagnose schreibt über eine begrenzte Warteschlange und rotiert pro Sitzung bis zu vier Dateien.
- Deutsch/Englisch wird gespeichert. Sprachwechsel aktualisiert bestehende Beschriftungen und Meldungen, ohne Verbindung, Auftrag, Eingaben oder Grafikausschnitt neu anzulegen. Berechnete Werte und bestätigte Zustände werden nicht aus zwischengespeicherten Startbeschriftungen zurückgesetzt.

## Bedienung

Die Anordnung des Hauptfensters und die vorhandene Navigationsgrafik bleiben erhalten.

1. Bei einer Unterbrechung den Vorfall quittieren und den Gerätestand prüfen lassen. Eine weiterhin bestehende Störung bleibt gesperrt. Bereits gespeicherte Aufnahmen bleiben erhalten.
2. Unter **Läufe & Warnpunkte → Restauftrag …** die angezeigte Spalte beziehungsweise das Band prüfen; bei Bedarf die vorherige Einheit wählen. Eine mögliche zusätzliche unbestätigte Kameradatei wird ausgewiesen.
3. **Restauftrag laden; Start erfolgt separat** wählen. Anschließend startet die vorhandene Start-Schaltfläche den bestätigten Restauftrag. Ein erhaltenes pausiertes Raster kann alternativ über die Spaltenvorbereitung und anschließend Weiter fortgesetzt werden.
4. **XML neu sichern** baut die automatische Sicherung erneut auf. Die Anzeige nennt Sicherungsstand, Ziel und Fehler. **XML + CSV** exportiert das zusammengehörige Panorama einschließlich echter Wiederholungen und eine zusätzliche `.interruptions.tsv` mit Fehler-/Statuskontext.
5. **Antrieb & Grenzen → Fahrt / Referenz / Warnungen …** enthält Stabilitätswerte, Qualitätspause, Kamera-Rollwinkel und Sprache. Lokale Eingaben speichern keine Gerätewerte ohne ausdrückliche Geräteaktion.

Der Rollwinkel bleibt für bestehende Profile standardmäßig 90°. Für einen Android-kompatiblen Workflow kann 0° gewählt werden; die Orientierung wird im Kameraprofil und am Lauf gespeichert. Ein Sprachwechsel ändert keine Protokoll- oder CSV-Felder. Historische Freitexte und bereits ausgegebene Logs werden nicht nachträglich übersetzt.

## Daten und Rückfall

| Ort unter `%LOCALAPPDATA%/PanoramaControl` | Inhalt |
|---|---|
| `Runs/captures.db`, gegebenenfalls `-wal` / `-shm` | Neue Laufablage, Checkpoints und Verknüpfungen |
| `Runs/*.panojournal` | Unveränderte historische Quellen |
| `Papywizard/Panorama_<Laufschlüssel>.xml` | Automatische vollständige Sicherung je zusammengehörigem Panorama |
| `settings-v2.xml` | Eingaben, Profile, Sprache, Qualitätsoption und Orientierung |
| `SupportLogs/` | Sitzungslogs mit Rotation |

Eine DB-Sicherung muss eine konsistente SQLite-Sicherung umfassen; bei laufender App gehört ein vorhandenes WAL zum aktuellen Stand. Am einfachsten die App schließen und anschließend die gesamte Datenablage sichern. DB-Commit und XML-Ersetzung sind getrennte Vorgänge; die XML-Anzeige meldet eine ausstehende Sicherung. Ein XML-Fehler allein löscht keine Capture-Daten. Vorübergehend fehlgeschlagene Archivschreibvorgänge werden in einem begrenzten, geordneten Speicherpuffer erneut versucht; der Fehler bleibt bis zum erfolgreichen Nachschreiben sichtbar. Noch nicht bestätigte DB-Commits sind bei einem Prozessabbruch während eines weiter bestehenden Speicherfehlers nicht dauerhaft gesichert.

Das bisherige Programmpaket und alte Journale bleiben als Rückfall erhalten. Neue SQLite-Läufe werden von 27.4.4 nicht gelesen; für sie dienen XML/CSV und Unterbrechungsexport als Austausch. Vor der ersten produktiven Nutzung persönliche Einstellungen und Laufdaten sichern.

## Prüfungen und Grenzen

Reproduzierbare Softwareprüfung:

```powershell
dotnet run --project Tests/ProtocolTests/ProtocolTests.csproj
pwsh -File Tests/build-windows.ps1
```

**Abschließender Paketlauf am 25.10.2026: 1.415 Host-/Protokoll-/Controllerprüfungen, 129 simulierte WinForms-Prüfungen und 16 Raster-/Skalierungs-/Transferprüfungen erfolgreich.** Release-Build und ZIP-Paket erfolgreich. Die vorhandenen Warnungen liegen in weiterhin eingebundenen Legacy-Klassen (`muControllerInterface`, `PanoramaAcquisitionClass`).

Die Hosttests prüfen Journalimport, Deduplizierung, unveränderte historische Quellen, Messdatenvorrang, sphärische Ganzzahlziele, Dateisperren, Neustart-Rebuild, überlappende Exporter, Restvorbereitung mit Abbruch nach CLEAR beziehungsweise LOAD, unpassende Gerätepläne, Boot-/Referenzwechsel, Stabilitätsrevisionen, Qualitätspause und blockierte Writes. Die WinForms-Prüfungen verwenden die tatsächliche .NET-4.8-Anwendung und denselben nativen SQLite-Provider; sie prüfen bestehende Bedienung, Sprachwechsel, Werteerhalt und Erreichbarkeit des Restauftrags über Start.

Die Prüfungen sind simuliert und führen keine realen Motorfahrten oder Kameraauslösungen aus. Die Auflösung einer echten Bluetooth-COM-Geräteadresse, das Verhalten des konkreten Bluetooth-Treibers, Dateisysteme außerhalb des geprüften lokalen Windows-Systems und Wiederaufnahme unter realer Last benötigen noch eine Hardwareabnahme. Bei nicht eindeutig auflösbarer Geräteadresse bleibt die Restauftragsfunktion gesperrt; Datenexport und normale Gerätebedienung bleiben verfügbar.

Capture-Zähler sind keine Bestätigung einer Kameradatei oder ihrer Schärfe. Die feste 1°-Qualitätspause garantiert keine bestimmte optische Überlappung. Ein Controller-Neustart oder eine geänderte Tilt-Referenz gibt keinen alten Restauftrag automatisch frei.
