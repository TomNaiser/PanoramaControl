# Windows 27.4.0 — Umsetzung und Abnahme

Stand: 17. Oktober 2026. Funktionsreferenz Android 0.6.19. **Firmware 2.4.70 wurde weder verändert noch geflasht.** Es wurden keine persistenten Motorparameter, Warnschwellen oder Nullpunkte am Gerät geändert. Geräte-Konfiguration vor und nach den Tests: Revision 24, 128 Parameter.

## Auslieferung

Startbares Paket: `releases/27.4.0/PanoramaControl.exe`, zusätzlich `releases/PanoramaControl-27.4.0.zip`. Windows x64 mit .NET Framework 4.8; WinForms und bestehende Fensterstruktur bleiben erhalten.

Reproduzierbarer Build einschließlich Software- und GUI-Prüfung: `pwsh -File Tests/build-windows.ps1`. Das Skript führt keine Hardwaretests aus. Die separat aufrufbare Hardwareprüfung verlangt einen beaufsichtigten, frei beweglichen Aufbau und einen freien Bluetooth-Kanal.

## Umgesetzter Umfang

| Bereich | Windows 27.4.0 |
|---|---|
| Gerätevertrag | Aktuelle Fähigkeitsmeldungen, Boot-/Sitzungskennung, manueller Betriebsstatus, Fahrzeitbudget, Lauftempo und Warnschwellen |
| Koordinaten | Getrennte Pan-/Tilt-Abbildung entsprechend aktuellem Android; positive Pan-Werte links, positive Tilt-Werte oben; konsistente Planung, Grafik und Exporte |
| Verlässliche Anzeige | ACK bedeutet Annahme; die Istposition stammt ausschließlich aus Gerätewerten. Fehlende/veraltete Statusdaten sperren neue Aktionen |
| Sicherheit und Wiederherstellung | Priorisierter Not-Stop, auch in großer Grafik; alte Aktionen bei Trennung verwerfen; ein Fehlerdialog pro Fall; Quittierung mit kontrollierter, bei fortbestehender Störung wiederholter Zustandsprüfung |
| Manuell | Ziel- und Schrittbewegung, getrennte Pan-/Tilt-Zielfahrtempi, gehaltene Pfeile mit Keepalive/Stop und Fokusverlustbehandlung, geräteabhängige Geschwindigkeiten, Rückfahrt aus Grenzüberschreitung |
| Ausrichtung | Grafisches/numerisches Ziel mit ausdrücklichem Anfahren; Start/Ende nach Beruhigung aus frischen Istwerten merken, erneut anfahren; numerische Endpunkte; Tilt-Referenzbefehle über explizite Bedienung |
| Plan/Lauf | Übertragen und Start trennen; bestätigter Planabbruch bis IDLE; inaktive Aufträge bei manueller Navigation erhalten; Raster, sphärisch, Bildanzahlmodus, Pause/Fortsetzen, Spaltennavigation |
| Tempo/Zeiten | Bis 2000 ‰ nur bei Gerätefreigabe; Zeitänderung während des Laufs, vorgemerkt gegenüber aktiv; separate Timelapse-Pulsdauer |
| Speicherung | Eingaben und editierbare Kameraprofile sofort dauerhaft speichern; Entwürfe nach Referenz-/Geräteänderung neu prüfen; lokale Dateien unter LocalAppData |
| Archiv/Export | Prüfsummengeschütztes Aufnahmejournal, Geräte-/Boot-/Lauftrennung, stabile Capture-Identität, Duplikatfilter und Rekonstruktion; konsistenter XML-/CSV-Snapshot mit Datenherkunft und Warnungen |
| Grafik/Warnungen | Plan, Fahrziel, Istposition und Aufnahmen getrennt; rote Warnpunkte, gespeicherte Warnliste, bewusstes erneutes Anfahren; Zoom, Verschieben, große Grafik, gespeicherte Läufe |

Kein vollständiger Editor für alle Motorparameter: Dieser war auch im vereinbarten Plan nicht Bestandteil der Android-Funktionsparität.

## Software- und GUI-Prüfung

- 166 Protokoll-/Modell-/Controller-/Speicherprüfungen im .NET-8-Hostprogramm; Ausgangsbasis waren 116 Prüfungen.
- 81 Windows-UI-Prüfungen gegen die wirkliche WinForms-App mit simuliertem Gerät: unter anderem ↑-Richtung, ACK/Ist-Trennung, bestätigte Endpunkte, Laden/Starten, Warnliste, Fokusverlust, Not-Stop im modalen Großbild sowie Profile/Entwurf nach Neustart.
- Release-Build erfolgreich. Sieben Warnungen im vorhandenen Legacy-Code betreffen unbenutzte Ausnahmevariablen und unerreichbare alte Pfade; neue Module verursachen keine Compilerwarnungen.
- Gerenderte Ansichten unter `Tests/WindowsSmoke/artifacts/`: Navigation, Warnliste und Einstellungen. Neue Controls wurden an die horizontale und vertikale Skalierung des bestehenden Formulars angepasst.

Auf dem realen Bluetooth-Kanal gefundene Windows-Probleme sind abgesichert: Nach `LOAD_PLAN` auf den bestätigten geladenen Plan warten; einen gemeldeten leeren Plan mit Laufkennung 0 nicht als Auftrag behandeln. Auch optionale Diagnose-Timeouts dürfen keine unklare Motoraktion vortäuschen.

## Reale Geräteprüfung

Der Benutzer hat den beaufsichtigten freien Aufbau bestätigt und Android vor dem Windows-Zugriff getrennt. Verbindung über COM4 / Bluetooth-SPP, unveränderte Firmware 2.4.70, Gerätestartkennung 132. Gemeldete Grenzen Pan ±180°, Tilt −45° bis +30°.

| Prüfung | Ergebnis |
|---|---|
| Handshake, aktuelle Position, Fähigkeiten | Bestätigt |
| Kleine manuelle Pan-/Tilt-Fahrten | Mehrere Zielankünfte bestätigt; eine Tilt-Rückfahrt fehlgeschlagen, siehe unten |
| Gehaltene Fahrt | Je eine Sekunde mit 2°/s angefordert; tatsächliche Pan-/Tilt-Bewegung und Abschluss `CANCELLED` nach Loslassen bestätigt |
| Fehlerquittierung nach manuellem Zielfehler | Neuer Gerätestand und IDLE bestätigt, keine zweite Quittierung für dieselbe wiederholte Fehlermeldung nötig |
| Einzeltrigger | 30-ms-Puls und `TRIGGERED` bestätigt |
| Raster mit Pause, Zeiten und Spaltenwiederholung | 6 gemeldete Aufnahmen; PAUSE_PENDING → PAUSED, UPDATE_TIMING, REWIND_COLUMNS und RESUME bestätigt; abgeschlossen |
| Sphärischer Auftrag mit zwei Bändern | 4 gemeldete Aufnahmen, abgeschlossen |
| Timelapse | 300-ms-Intervall, 30-ms-Puls; 3 Pulse und Stop bestätigt |
| Verbindungsverlust: autonom fortsetzen | Wiederanbindung übernimmt vorhandenen Auftrag ohne LOAD/START-Replay; 4 Aufnahmen abgeschlossen |
| Verbindungsverlust: stoppen | Nach Wiederanbindung STOPPED mit `CONNECTION_TIMEOUT`; kein Neustart des Auftrags |
| Aufräumen/Abschluss | Eigene Testaufträge entfernt; bestätigter Not-Stop, STOPPED und Laufkennung 0; Bluetooth anschließend getrennt |

Der erfolgreiche zusammenhängende Durchlauf umfasst 14 Panorama-Captures, einen Einzeltrigger und drei Timelapse-Pulse. Die Panorama-Captures dieses Durchlaufs meldeten keine `CAPTURE_WARNING`. Warnliste, Warnexport und erneute Zielauswahl wurden daher mit simulierten Warnereignissen geprüft, nicht durch absichtliches Verfehlen eines realen Aufnahmeziels.

Belege: `Tests/WindowsSmoke/acquisition-results.txt` und `Tests/WindowsSmoke/hardware/PanoramaSupport_20261017_140441_788.log`. Der Supportlog enthält die gesendeten normalen Bedienbefehle und den Gerätevertrag. Er enthält keine Firmware-Uploads oder Schreibbefehle für persistente Motorparameter.

## Offene Abnahmepunkte

**Manuelle Tilt-Positionsgenauigkeit ist nicht vollständig abgenommen.** In einem vorausgehenden Durchlauf verfehlte eine kleine Tilt-Rückfahrt ihr Ziel: Ziel 5,845°, gemeldeter Istwert etwa 5,273–5,295°, Ergebnis `TARGET_MISSED`. Windows hat den Fehler erkannt und der Test endete mit bestätigtem Not-Stop. Das entspricht ungefähr 0,55–0,57° Abweichung. Dieser Befund wurde nicht durch geänderte Toleranzen verdeckt. Die Firmware bleibt auf ausdrücklichen Benutzerwunsch unverändert.

Belege: `Tests/WindowsSmoke/hardware-results.txt` und `Tests/WindowsSmoke/hardware/PanoramaSupport_20261017_140203_581.log`. Ein erster Durchlauf vor der Statuswartezeit-Korrektur ist zusätzlich im Log `PanoramaSupport_20261017_135841_483.log` erhalten.

Weitere Grenzen der Prüfung:

- Bestätigte Auslöseereignisse sind kein Nachweis gespeicherter Kameradateien, Bildschärfe oder optischen Ausschwingens. PTGui-Import mit den realen Kameradateien bleibt gesondert zu prüfen.
- Keine reale Fahrt über eine mechanische Grenze und keine absichtliche Sensorstörung. Grenzrückfahrt, Statusverlust und Fehler-Replay sind simuliert geprüft.
- Kein erneuter realer Fahrtest mit 2000 ‰, keine große Tele-Serie und kein Langzeitlauf. Die Tempo-Freigabe und Ablehnung älterer Fähigkeiten sind softwareseitig geprüft.
- Die Rückübernahme des aktuellen Gerätestands durch Android nach Ende dieser Windows-Prüfung wurde nicht instrumentiert. Bluetooth ist wieder frei. Historische Android-Aufnahmedaten werden nicht automatisch in das lokale Windows-Archiv kopiert.
- Die Software ist implementiert und mit den genannten Fällen geprüft; eine uneingeschränkte Hardware-/Bildqualitätsfreigabe wird damit nicht behauptet.
