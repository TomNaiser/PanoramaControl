# Windows 27.4.1 — Android-Funktionsabgleich

17. Oktober 2026. Verbindliche Referenz: Quellcode der Android-App 0.6.19. Windows Forms, Hauptfenster, Reiter und bestehende Eingabefelder bleiben erhalten. Neue Abläufe liegen in den bereits ergänzten Windows-Dialogen. Die Firmware wurde weder bearbeitet noch geflasht; auch Geräteparameter und Toleranzen wurden in dieser Nacharbeit nicht verändert. Es gab keine weiteren realen Fahrten.

## Korrektur der gemeldeten manuellen Fahrt

Androids `PanoramaService.updateManualAction` unterscheidet ausdrücklich zwischen einem beendeten, ungenauen Ziel (`TARGET_MISSED`) und einer unterbrochenen Aktion. Windows 27.4.0 behandelte diese Fälle gemeinsam als quittierungspflichtige Störung und wertete auch einen alten, mit `GET_STATUS` wiederholten Abschluss aus.

27.4.1 übernimmt den Android-Ablauf: Erst eine Statusmeldung nach der Befehlsbestätigung mit passender Sequenz, Aktionsart und bei Zielfahrten passendem Ziel beendet die Aktion. `TARGET_MISSED` zeigt eine Warnung in der vorhandenen Statusanzeige und im Protokoll, optional mit Signalton. Die Motoren sind laut Geräteergebnis gestoppt; erneutes Anfahren ist möglich, ohne den Geräteauftrag zu löschen. Das alte Schrittziel und die grafische Fahrzielauswahl werden verworfen. Wiederholte Statusmeldungen erzeugen keine weiteren Warnungen. Zeitüberschreitung, Abbruch und Gerätefehler verlangen weiterhin Zustandsprüfung. Ein fehlender Abschluss hat eine endliche Wartezeit.

Das beseitigt keine physische Zielabweichung: Die zuvor gemessenen rund 0,55–0,57° Tilt-Abweichung bleiben ein offener Hardwarebefund. Keine größere Toleranz und kein automatischer Wiederholungsfahrbefehl verdecken ihn. Die letzte Benutzermeldung allein belegt nicht, ob sie einen neuen Fahrversuch oder einen gespeicherten Abschluss betrifft; beide Windows-Auswertungswege sind jetzt abgesichert.

## Zuordnung der Android-Funktionen

Die Tabelle bezeichnet fachlich entsprechende Funktionen, keinen Austausch der Windows-Oberfläche gegen Android-Seiten. Android-spezifische Betriebssystemfunktionen (Berechtigungen, Foreground-Service, Android-Dateiauswahl und Import einer Windows-Konfiguration) werden durch vorhandene Windows-Verbindung, Desktop-Laufzeit und lokale Konfigurationsdateien abgedeckt.

| Android-Referenz | Entsprechung in Windows |
|---|---|
| `DeviceState`, `DeviceSession`, `V2Client`, `ReconnectPolicy` | `PanoramaSessionController`, V2-Client: Fähigkeiten, Bootkennung, Statusalter, Wiederanbindung und Übernahme des Geräteauftrags ohne automatischen Neustart |
| `CommandEffects`, `IncidentTracker`, `RecoveryFlow` | Getrennte Annahme und Wirkung, priorisierter Not-Stop, Verwerfen alter Aktionen, ein Fehlerdialog, Quittierung und wiederholte Zustandsprüfung |
| `ManualActionStatus`, `ManualActionProgress` | Abschluss nur für den bestätigten Befehl, keine Erledigung durch alte Statusdaten oder reine Zielnähe; Warnung und Unterbrechung getrennt |
| `ManualTargets` | Feinschritte ab dem zuletzt angenommenen Ziel, Überschwingen berücksichtigt; nach Warnung, Handfahrt, Zustandswechsel oder Verbindungstrennung wieder ab Istposition |
| `ManualMotionOptions`, `JogHold` | Getrennte Zielfahrtempi, geräteabhängige Haltegeschwindigkeiten, Lease/Keepalive, Loslassen/Fokusverlust; vom Gerät beendete Haltefahrt stoppt auch den Client-Timer |
| `DeviceState.validateManualTarget` | Getrennte Pan-/Tilt-Koordinaten, Grenzen und kontrollierte Rückfahrt aus Grenzüberschreitung |
| `EndpointSelection` | Start/Ende nach Beruhigung aus neu angefordertem Istwert merken; beide Punkte wieder anfahren; numerische Eingabe und grafisch gewählten Punkt ausdrücklich in Start/Ende übernehmen |
| Entwurfsreferenz in `PanoramaService` | Nach Geräte-/Tilt-Referenzwechsel Endpunkte behalten, Übertragen sperren und bewusste Bestätigung unter „Winkel / Endpunkte“ verlangen |
| `PanoramaPlan`, `PlanAbort` | Raster und sphärische Bänder, Vorschau, Übertragen getrennt von Start, bestätigtes Zurücksetzen vor anderem Plan oder Timelapse; geladener Auftrag beim Start ausdrücklich benannt |
| `runCommand`, `columns` | Start, Pause/Fortsetzen, Stop, Rasterspalte zurück/vor mit Prüfung von aktuellem Lauf und Zustand; manuelle Navigation erhält inaktive Aufträge |
| `updateTiming`, `timingSummary` | Beruhigung 0–600 s, Puls 0,001–60 s, Belichtungswartezeit 0–3600 s; aktive gegenüber vorgemerkten Zeiten und Fahrzeitbudget; Lauftempo bis 2000 ‰ bei Gerätefreigabe |
| `trigger`, `timelapse` | Einzelpuls mit Abschlussbestätigung; separates Intervall und Pulsdauer für autonomen Timelapse, expliziter Stop |
| `CameraProfile`, Eingabespeicherung | Vorhandene Windows-Kameraauswahl und XML-Konfiguration; editierbare Profile und unmittelbare dauerhafte Eingabespeicherung |
| `setTiltZero`, Tilt-Grenzen | Explizite Nullpunktbefehle und bestätigte Gerätegrenzen in vorhandenen Einstellungsdialogen; keine Ausführung durch diesen Build/Test |
| `CaptureWarning`, Warnliste | Getrennte geräteseitige Warnschwellen, Warnursachen, optionale Töne, rote Aufnahmepunkte, gespeicherte Liste und ausdrückliches erneutes Anfahren |
| `RunPlotModel`, `PlotViewport`, `PlotWorkspace` | Plan, Istwert, Fahrziel und Aufnahmen getrennt, Zoom, Verschieben, Einpassen und große Grafik; letzte 512 Statuspunkte mit Unterbrechung bei Verbindungslücken, ohne erfundene Verbindungslinien |
| `CaptureStore` | Lokales prüfsummengeschütztes Journal, Geräte-/Boot-/Lauftrennung, Duplikatfilter, Rekonstruktion und gespeicherte Läufe |
| XML/CSV- und Supportexport | Gemeinsamer Aufnahmesnapshot, Warnmetadaten und Quellenkennzeichnung, fehlende historische Istwerte ausdrücklich unbekannt; dauerhafter Supportlog und Geräteabfrage |

Windows behält zusätzlich den vorhandenen Bildanzahlmodus und getrennte Pan-/Tilt-Lauftempi. Die Motorregelung, Fahrprofile, Zeitautomatik und physische Zielgenauigkeit verbleiben in der unveränderten Gerätefirmware. Ein vollständiger Motorparametereditor ist auch kein Bedienfenster der Android-App; deren separate Diagnoseskripte sind kein Bestandteil der GUI-Parität.

## Prüfung und Paket

Reproduzierbarer Build: `pwsh -File Tests/build-windows.ps1`. Enthält ausschließlich Software-/simulierte Gerätetests, keine reale Verbindung und keine Motorfahrt. Ausgabe: `releases/27.4.1/PanoramaControl.exe` und `releases/PanoramaControl-27.4.1.zip`.

Der vor der Korrektur fehlschlagende Status-Replay-Test und die zusätzlichen Controller-/WinForms-Prüfungen prüfen insbesondere Warnung statt Sperrdialog, alte Ergebnisse, falsche Ziele/Aktionsarten, Sequenzwiederverwendung vor ACK, erneutes Anfahren ab Istwert, fehlenden Abschluss, beendete Haltefahrt, erhaltenen Geräteplan, Referenzbestätigung und Android-Zeitbereiche. Ergebnisse und Ansichten liegen unter `Tests/WindowsSmoke/`.

Die dokumentierten realen Prüfungen und offenen Hardware-/Bildqualitätsfragen der [Abnahme 27.4.0](WINDOWS-27.4.0-ABNAHME.md) bleiben gültig; sie sind kein neuer Hardware-Nachweis für 27.4.1. Die Firmware bleibt auf Benutzeranweisung unverändert.

Ergebnis: **187 Softwareprüfungen und 93 WinForms-Prüfungen bestanden**. Release-Build erfolgreich; sieben unveränderte Warnungen in altem Code. Ansichten einschließlich Zielwarnung und Endpunktdialog unter `Tests/WindowsSmoke/artifacts/`; vollständiger Buildnachweis `Tests/WindowsSmoke/build-results-27.4.1.txt`.
