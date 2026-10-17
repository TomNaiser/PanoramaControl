PanoramaControl 27.4.3 für Windows
17. Oktober 2026

Start: PanoramaControl.exe. Benötigt Windows x64 und .NET Framework 4.8.
Alle Dateien des Pakets zusammen entpacken. Die Beispielkonfiguration nutzt COM4.
Eine vorhandene C:\PanoControlConfig.xml hat weiterhin Vorrang für Kamera-/Altwerte.
Bei Bedarf den richtigen ausgehenden Bluetooth-COM-Port im Fenster auswählen.
Android vor einer Windows-Verbindung vom Kopf trennen.

Wichtigste Bedienung:
- Nach Start und Ende erscheint das neue Zielpunktraster automatisch; auch
  Brennweite, Sensorgröße und Überlappung aktualisieren die Vorschau.
- Übertragen lädt den sichtbaren Entwurf ohne Fahrt. Ein alter inaktiver
  Geräteplan wird dabei zurückgesetzt und ersetzt. Start erfolgt separat.
- NOT-STOP ist im Hauptfenster und in der großen Grafik erreichbar.
- Tilt-Anzeige startet bei ±25°. Auswahl Tilt ±25°/±45°/±90° erweitert nur
  die Ansicht; gesetzte Achsenlimits bleiben unverändert verbindlich.
- Wie alte Windows-GUI: Linksklick im Diagramm fährt direkt an. Danach
  Start setzen oder Ende setzen: übernommen wird die erreichte Istposition.
  Rechte Maustaste verschiebt nur die Ansicht. Numerische Ziele bleiben möglich.
- Pfeile wie Android: Drücken startet eine kontinuierliche Fahrt. Längeres
  Halten beschleunigt über die vorhandene Geräterampe zum eingestellten
  Höchsttempo. Loslassen, Verlassen der Taste oder Fokusverlust stoppt.
  Kein Umschalten in einen Schrittmodus erforderlich; keine Klick-Nachfahrt.
- Bei fokussiertem Pfeil funktionieren auch Leertaste, Enter und Richtungstasten.
  Ein barrierefreier Einzelklick ohne Loslassereignis ist auf 1 s begrenzt.
- Start/Ende setzen verwendet nach Beruhigung frische Gerätepositionen.
- Start und Ende sind in der Grafik beschriftet und werden beim Zoomen aus
  ihren Winkeln neu gezeichnet. Graue Linien zeigen frühere Fahrbewegungen.
- Winkel / Endpunkte übernimmt den Grafikpunkt ausdrücklich als Start/Ende.
  Nach Referenzwechsel bleiben Endpunkte erhalten und müssen geprüft werden.
- TARGET_MISSED ist wie unter Android eine Zielwarnung: Istposition prüfen,
  erneutes Anfahren möglich. Die Zielabweichung wird nicht automatisch korrigiert.
- Vor Timelapse vorhandenen Auftrag mit Neuer Plan ausdrücklich zurücksetzen.
  Start benennt den geladenen Geräteauftrag; bei geändertem Entwurf zuerst
  Übertragen drücken. Während laufender/pausierter Aufnahme ist dies gesperrt.
- Zeiten ... ändert Beruhigung, Triggerpuls und Belichtungswartezeit im Lauf.
- Kamera-Label anklicken, um die angezeigten Werte als Profil zu speichern.
- Fahrt / Referenz / Warnungen ... enthält getrennte Tempi, Warnschwellen,
  Verbindungsverlust-Policy und Tilt-Referenz. Änderungen am Gerät sind explizit.
- Bei Störungen Hinweis anklicken bzw. den einmaligen Fehlerdialog quittieren.
  Die Wiederherstellung prüft den Gerätestand und wartet nötigenfalls auf die
  Behebung der Störung. Ein Aufnahmelauf wird dadurch nicht neu gestartet.
- Läufe & Warnpunkte zeigt gespeicherte Captures und exportiert XML plus CSV
  aus demselben Snapshot. Unbekannte Istpositionen bleiben im CSV leer.
- Grafik: Mausrad zoomt; rechte Maustaste verschiebt; Pan ±180° setzt zurück.

Einstellungen und Laufarchiv liegen unter %LOCALAPPDATA%\PanoramaControl.
Bestehende XML-Konfigurationen werden nicht überschrieben.

Firmware wurde nicht verändert oder geflasht. Getestete Referenz: 2.4.70,
Motor-Konfiguration Revision 24 mit 128 Parametern unverändert.

Offener Hardwarebefund: Eine manuelle Tilt-Rückfahrt meldete TARGET_MISSED
bei etwa 0,55 Grad Abweichung. Dies wird als Fehler angezeigt; eine vollständige
Abnahme der manuellen Positionsgenauigkeit steht deshalb aus. Bildschärfe und
Nachschwingen sind nicht durch bestätigte Auslösezähler nachgewiesen.

Prüfungen und Grenzen: ABNAHME.md.
