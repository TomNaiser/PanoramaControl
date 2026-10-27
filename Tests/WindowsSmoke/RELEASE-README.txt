PanoramaControl 27.5.4 — Abgleich mit Android 0.6.27 / Firmware 2.4.75

Ziel: Windows 10/11 x64, .NET Framework 4.8, Systembibliothek winsqlite3.dll.
Firmware: V2; neue optionale Funktionen benötigen entsprechende Fähigkeiten
(z. B. Firmware 2.4.75). Firmware wird weder geändert noch geflasht.

Hauptfenster und Navigationsgrafik bleiben wie gewohnt bedienbar.
27.5.4: Vor dem Laden unter Navigation -> Zeiten ... die Android-Referenzwerte
auswaehlen und Uebernehmen: Tempo 1,7/1,7; Zeiten 0,5/0,03/0,3 s.
Die Werte bleiben vor dem Uebernehmen sichtbar aenderbar. Es wird kein
Motorprofil geschrieben. Grosse Diagnoseabrufe warten jetzt auf ihr Ende.
Der urspruengliche Pan-Vergleich mit Motorprofil Revision 39 ergab zuletzt
max. 0,348 Grad / Median 0,174 Grad. Das gesonderte Paket mit Kennung
pan-r40 enthaelt das anschliessend am Kopf korrigierte Pan-Profil;
Messwerte und Grenzen stehen dort in PAN-KORREKTUR.md. Die EXE ist identisch.
- Plan übertragen und anschließend ausdrücklich starten.
- Bei Fehlern quittieren / Gerätestand prüfen. Unter Läufe & Warnpunkte:
  Restauftrag ... öffnet die Auswahl der betroffenen/vorherigen Spalte oder
  des sphärischen Bands. Laden startet keine Bewegung; danach Start drücken.
- Die Wiederaufnahme bleibt bei geändertem Boot / Tilt-Nullpunkt, unklarem
  Geräteplan oder nicht eindeutig zuordenbarer Bluetooth-Adresse gesperrt.
- XML wird aus dem SQLite-Archiv automatisch vollständig im Hintergrund
  gesichert. Sicherungsstand und Fehler stehen unter Läufe & Warnpunkte.
  XML neu sichern wiederholt den Export; XML + CSV enthält verknüpfte Läufe
  sowie eine zusätzliche Unterbrechungsdatei.
- Antrieb & Grenzen / Fahrt, Referenz und Warnungen enthält Stabilität,
  optionale Qualitätspause über 1°, Rollwinkel und Deutsch / English.
- Gehaltene Pfeile, Mausnavigation, große Grafik und bestehende Profile
  bleiben erhalten. Loslassen / Fokusverlust beendet Handfahrt.

Daten: %LOCALAPPDATA%\PanoramaControl. Vorhandene Journale werden importiert
und bleiben unverändert. Alte Versionen lesen neue SQLite-Läufe nicht.
Vor erster produktiver Nutzung persönliche Einstellungen und Laufdaten sichern.
Zum Sichern der Daten die App schließen und den gesamten Datenordner kopieren.

Diese Version ist software-/simulatorgeprüft. Eine reale Hardwareabnahme mit
aktueller Firmware, Bluetooth-Treiber, Kameralast und Wiederaufnahme steht aus.
Keine Garantie von Kameradateierfolg, Bildschärfe oder Mindestüberlappung.
Details, Nachweise und Grenzen: ABNAHME.md.

27.5.1: Die deutsche Überlappungsbeschriftung verdeckt die Eingabe nicht mehr.
Pfeile berücksichtigen das bestätigte Geräte-Höchsttempo; gespeicherte höhere
Wünsche werden beim Fahren begrenzt. Supportlogs enthalten Pfeilgesten und
deren Stopgrund. Antrieb & Grenzen zeigt Firmware und bestätigte Lauftempi.

27.5.2: Die vier Pfeile behandeln Touchkontakte unmittelbar, einschließlich
Loslassen, Herausziehen und Captureverlust. Zweite Finger übernehmen keine
Fahrt. Maus und Tastatur bleiben bedienbar. Reale Surface-Abnahme steht aus.

27.5.3: Das Android-Praxisraster und pausierte Restauftraege sind mit
protokollstrenger STOP-/START-Pruefung abgesichert. Der Kopf bleibt auf
Firmware 2.4.75 und Motorprofil Revision 39. Der bereits am Kopf korrigierte
Start-Bremsvorhalt 0,110 s gilt fuer beide Clients; Windows schreibt ihn
beim Verbinden nicht um. Android und Firmware bleiben unveraendert.
Fuer gleiche Referenzbedingungen: Pan/Tilt-Tempo jeweils 1,7; Beruhigung
0,5 s; kontrollierter Triggerpuls 0,03 s; Belichtungswartezeit 0,3 s.
Vorhandene persoenliche Werte werden nicht automatisch ersetzt.
REFERENCE.json nennt die festgehaltene Basis. Ein beigelegtes vollstaendiges
Motorprofil wird nicht automatisch angewandt. Details: ABNAHME.md.
Für die weitere Diagnose: Läufe & Warnpunkte -> Support ...; alternativ
%LOCALAPPDATA%\PanoramaControl\SupportLogs\PanoramaSupport_*.log.
