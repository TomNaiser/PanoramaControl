# Pan-Korrektur: Windows 27.5.4 mit Motorprofil Revision 40

27. Oktober 2026. Die verbleibende Pan-Abweichung wurde am selben Kopf mit
Windows 27.5.4 reproduziert und durch zwei gespeicherte Motorparameter
verringert. Android 0.6.27 und die reguläre Firmware 2.4.75 bleiben erhalten.
Das Profil wirkt im Kopf und damit auch bei Bedienung über Android oder
das Surface; ein zusätzlicher Windows-Regler ist nicht beteiligt.

## Ursache und konkrete Korrektur

Das gespeicherte Pan-Ankunftsfenster war zu groß. Der echte MotionController
akzeptiert mit 0,30° Toleranz eine ruhende Restabweichung von 0,282° ohne
Korrektur. Außerdem geht die halbe Toleranz in den Beginn des Bremsens ein.
Die unterschiedliche Anzeige der beiden Apps erklärt den Fehler nicht:
Die zuvor aufgezeichneten Messframes ergaben in beiden Parsern exakt
dieselben Positionen.

| Parameter | Revision 39 | Revision 40 |
|---|---:|---:|
| `pan.positionToleranceDeg` | 0,30° | 0,15° |
| `pan.holdExitToleranceDeg` | 0,40° | 0,25° |

Dies sind die bestehenden Firmware-Standardwerte dieser beiden Parameter.
Es wurde kein kompletter Werksreset durchgeführt. Die anderen **127 Werte**,
darunter der bereits korrigierte Tilt-Startbremsvorhalt von **0,110 s** und
die ausgeschaltete Feinannäherung, bleiben unverändert.

Die größeren Pan-Werte sind bereits in der Sicherung vom **22.10.2026,
Revision 29 / Firmware 2.4.71**, belegt. Sie wurden beim Windows-Update
27.5.3 nicht neu eingeführt. Der genaue frühere Schreibzeitpunkt lässt sich
aus den vorhandenen Sicherungen nicht bestimmen.

## Reale Überprüfung

Die Rezepte verwenden Tempo 1700/1700, Beruhigung/Puls/Belichtung
500/30/300 ms und volle Tilt-Spalten mit sechs Punkten. Gespeichert wurden
die tatsächlichen IMAGE_COMPLETED-Positionen über Produktionscontroller,
Bluetooth-COM und SQLite. Rekonstruierte Zielpositionen zählen nicht als
Messnachweis.

| Reihe | Aufnahmen | Pan maximal | Pan Median | Tilt maximal |
|---|---:|---:|---:|---:|
| Bisher, Revision 39, längerer Anlauf | 12 | 0,436° | 0,285° | 0,205° |
| Bisher, Revision 39, Wiederholung | 12 | 0,348° | 0,174° | 0,190° |
| Revision 40, erstes identisches 2×6-Rezept | 12 | 0,088° | 0,031° | 0,247° |
| Revision 40, 12×6 mit elf Spaltenrückfahrten | 72 | 0,236° | 0,106° | 0,315° |
| Revision 40, 2×6 in Gegenrichtung | 12 | 0,182° | 0,075° | 0,190° |

Der 72er-Lauf endete vollständig ohne Pause, Retry oder Neustart. Alle
Pan-Messwerte blieben unter 0,3°. **13 TARGET_MISSED-Warnungen** sind im
Archiv erhalten: Der engere interne Ankunftsmaßstab wurde nicht an jedem
Punkt eingehalten. Der größte rohe Pan-Aufnahmefehler lag dennoch bei
0,236°. Ein Tilt-Wert lag bei 0,315°; die Tilt-Abstimmung wurde nicht
verändert. Dies ist kein Nachweis allgemeiner Fehlerfreiheit oder optischer
Bildqualität für beliebige Lasten und Rezepte.

Insgesamt wurden **96 von 100 neu freigegebenen Auslösungen** verwendet,
alle gemessen und gespeichert; vier bleiben ungenutzt. Dies ist die neue
Freigabe für die Pan-Korrektur, zusätzlich zu den früheren Vergleichsreihen.
Die beiden 12er-Reihen meldeten keine Warnungen. Nach dem langen und dem
umgekehrten Lauf wurde **IDLE ohne Geräteauftrag** bestätigt. Beim ersten
12er-Lauf unterbrach das Testwerkzeug erst nach vollständigem Aufnahmeende
die automatische Diagnosesicherung mit seinem Abschluss-STOP; die
Verbindung wurde daraufhin getrennt. Der Prüfer wartet seitdem auf das
Übertragungsende und räumt abgeschlossene Aufträge ohne weiteren STOP auf.
Es gab keine zusätzliche Auslösung. Alle 129 Werte wurden abschließend
nochmals gelesen und gegen das vollständige Rückfallprofil geprüft:
genau die zwei aufgeführten Pan-Änderungen, Revision 40.

## Gesicherter Stand und Nutzung

Das Paket **PanoramaControl-27.5.4-pan-r40.zip** enthält dieselbe geprüfte
Windows-Anwendung 27.5.4 und das neue vollständige 129-Parameter-Profil
`MotionConfig.precise-pan-2.4.75-20261027.json`. `REFERENCE.json` nennt
Revision und Profil-Prüfsumme. Es wird beim App-Start nicht automatisch
geschrieben. Am geprüften Kopf ist Revision 40 bereits gespeichert.

Die vorherige Referenz Revision 39 und ihr Profil bleiben als Rückfallstand
erhalten, Hauptprojekt-Commit `665f0dc`. Die Anwendungsversion und die
Firmware-Binärdatei wurden für diese Parameterkorrektur nicht ersetzt.

Lokale Rohdaten und klar abgelegte Prüfwerkzeuge:
`analysis/2026-10-27-pan-fix/`. Die ursprüngliche Vergleichsmessung bleibt
im [vorherigen Bericht](WINDOWS-27.5.4-PAN-VERGLEICH.md) dokumentiert.
