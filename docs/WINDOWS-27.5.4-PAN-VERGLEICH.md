# Windows 27.5.4 — realer Pan-Vergleich und Aufnahmevorgaben

**Fortsetzung desselben Tages:** Die anschließend tatsächlich korrigierte
Pan-Abstimmung mit Revision 40 und längerer Fahrprüfung steht im
[Pan-Korrekturbericht](WINDOWS-27.5.4-PAN-KORREKTUR.md).
Die folgenden Messungen beschreiben den vorherigen Stand mit Revision 39.

27.10.2026. Android **0.6.27**, reguläre Firmware **2.4.75** und Motorprofil
**Revision 39** bleiben unverändert. Referenz: Hauptprojekt-Commit `665f0dc`.

## Was tatsächlich verglichen wurde

Der vom Benutzer beanstandete neue Surface-Auftrag war im Kopf erhalten:
16×6 Ziele, Pan/Tilt 1000/1000 ‰, Beruhigung/Puls/Belichtung 1000/30/500 ms.
Die 129 ausgelesenen Motorparameter stimmen mit dem bestätigten Android-
Profil überein. Der Kopf liefert nach Neuverbindung Plan und Fortschritt,
aber keine alten gemessenen Bildpositionen. Die 39 erhaltenen Aufnahmen
wurden deshalb lokal als rekonstruiert gesichert; ihre gemessenen Originale
bleiben im SQLite-Archiv des Surface. Diese Rekonstruktion ist kein
Genauigkeitsnachweis des beanstandeten Laufs.

Auf ausdrücklichen Benutzerwunsch wurden am Entwicklungsrechner zwei echte
Windows-Aufnahmereihen über den veröffentlichten 27.5.3-Controller, seinen
Bluetooth-COM-Transport und SQLite ausgeführt. Sie verwendeten exakt das
12-Bilder-Rezept der Android-Referenz, nur mit neuer Laufnummer: 2×6 Ziele,
Pan 127,375→129,437°, Tilt 4,614→−10,801°, Schritte 2,062°/−3,083°,
Tempo 1700/1700 ‰, Zeiten 500/30/300 ms, Heartbeat-Stopp nach 5000 ms.

| Gemessene Bildabschlüsse | Bilder | Pan max. | Pan Median | Tilt max. |
|---|---:|---:|---:|---:|
| Android-Referenz | 12 | 0,242° | 0,152° | 0,291° |
| Windows, Anlauf aus ca. 88° Pan | 12 | 0,436° | 0,285° | 0,205° |
| Windows, Wiederholung aus ca. 129° wie Android | 12 | 0,348° | 0,174° | 0,190° |

Beide Windows-Läufe endeten vollständig mit 12 gemessenen Datensätzen,
danach bestätigtem Stopp und IDLE ohne Geräteauftrag. Insgesamt sind jetzt
**81 von 100** Diagnoseauslösungen reserviert und bestätigt; **19 bleiben**.
Die vom Benutzer separat aufgenommenen Surface-Bilder zählen nicht zu den
automatisierten Vergleichsreihen.

Während der ersten Windows-Fahrt wurden ausschließlich Heartbeats gesendet,
keine zusätzlichen Ziel-, Timing- oder Motorbefehle. Die tatsächlichen
IMAGE_COMPLETED-Frames wurden anschließend durch den unveränderten Android-
Plotparser abgespielt. Alle zwölf Soll-/Istpositionen stimmen exakt mit der
Windows-Auswertung überein, einschließlich des 0,436°-Fehlers. Eine falsche
Windows-Pan-Konvertierung oder ein zusätzlicher Windows-Regler ist damit
für diese Daten ausgeschlossen. Der Pan-Median der Wiederholung liegt nur
0,022° — einen Encoderschritt — über der Android-Referenz.

**Die verbleibende Pan-Abweichung ist real und nicht als behoben belegt.**
Der unveränderte Regler erlaubt im Profil Pan 0,3° Positionstoleranz und
0,4° Haltehysterese; STEP_TARGET_MISSED kann nach passiver Beruhigung trotzdem
zu einer Aufnahme führen. Maxima aus diesen wenigen Läufen beweisen keinen
allgemeinen Genauigkeitsunterschied zwischen den Clients. Die frühere gute
Tilt-Korrektur ist in beiden Windows-Reihen erhalten geblieben.

## Konkrete Windows-Korrekturen

Unter **Navigation → Zeiten …** lassen sich vor dem Laden jetzt Zeiten und
beide Lauftempi für den lokalen Entwurf übernehmen. Bisher versuchte dieser
Dialog auch ohne Geräteplan ein UPDATE_TIMING für Lauf 0 und übernahm die
Eingaben bei dessen Ablehnung nicht.

**Android-Referenzwerte → Übernehmen** setzt ausdrücklich 1,7/1,7 sowie
0,5/0,03/0,3 Sekunden. Die sichtbaren Werte können vor dem Übernehmen geändert
werden. Diese Aktion sendet vor dem Laden keinen Gerätebefehl und verändert
keine Motorparameter, Grenzen, Endpunkte oder Kamerageometrie. Für geladene
Aufträge bleibt nur die bestehende bestätigte Änderung der Aufnahmezeiten
verfügbar; deren Tempo wird nicht heimlich ersetzt.

Ein weiterer real beobachteter Fehler betraf die Diagnose: Ein zusätzlicher
Supportabruf überlappte mit der automatischen Diagnoseübertragung und löste
TX_OVERFLOW aus, ohne Bewegung oder Auslösung. Nach Sicherung des Restzustands
wurde dieser Kommunikationsfehler mit der Produktions-Recovery zurückgesetzt.
Windows wartet jetzt bei GET_DEBUG_LOG und GET_SUPPORT_BUNDLE auf das echte
DEBUG_LOG_END statt nur auf ACK. Große Übertragungen werden serialisiert;
Bewegungsstart wartet auf ihr Ende. Not-Stop bleibt unabhängig. Timeout oder
Abbruch beendet eine unvollständige Sitzung, damit ein verspätetes Ende nicht
die nächste Übertragung freigibt. Dies entspricht Androids bestehender
Behandlung ausstehender Diagnosedaten. Es ist kein nachgewiesener Fix der
verbleibenden Pan-Restabweichung während der eigentlichen Aufnahme.

Der gezielte Übertragungs-Regressionsfall schlug vor der Korrektur fehl und
bestand danach. UI-Prüfungen decken Referenzwerte und die lokale Übernahme
ohne UPDATE_TIMING für Lauf 0 ab. Der Paketbuild prüft Hostcontroller,
WinForms, Vorschau, Navigation und native Surface-Touch-Nachrichten.

Abschließend bestanden **1.432 Host-/Controller-, 132 WinForms-,
16 Vorschau-, 34 Navigations- und 19 Touchprüfungen**: zusammen **1.633**.
Der neue Dialog wurde als Rendering geprüft. Am echten Kopf bestätigte der
korrigierte Client zusätzlich zwei gleichzeitig angeforderte Leseabrufe:
Die CRC-geprüfte Protokollspur zeigt jeweils DEBUG_LOG_END vor dem nächsten
Bulk-Befehl. Ergebnis **IDLE/NONE, Revision 39**, keine Bewegung oder Auslösung.
Dieser Leseversuch erfolgte in Boot 162; die Aufnahmevergleiche liefen in Boot 161.

## Paket und Grenzen

`releases/PanoramaControl-27.5.4.zip` enthält Anwendung, Hinweise und
`REFERENCE.json`; beim Build mit `-MotionProfile` außerdem das vollständige
unveränderte Referenzprofil. Es wird nicht automatisch geschrieben.
Vorhandene Benutzereinstellungen bleiben erhalten, bis der Benutzer die
Referenzwerte ausdrücklich übernimmt.

Die neuen Messdaten, CRC-geprüften Frames, Android-Parserwiedergabe,
129-Parameter-Sicherung und temporären Prüfer liegen lokal unter
`analysis/2026-10-27-windows-pan/`. Ein pauschaler Pan-Fix oder eine
allgemeine Genauigkeitsgarantie wird aus den Softwarekorrekturen nicht
abgeleitet. Firmware und das bestätigte Motorprofil wurden nicht nachgetunt.
