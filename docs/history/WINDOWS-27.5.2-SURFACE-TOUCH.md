# Windows 27.5.2 — Surface-Touch und Aufnahmefahrt

Stand: 26. Oktober 2026. Ergänzt die [27.5.1-Diagnose](WINDOWS-27.5.1-NAVIGATION-DIAGNOSE.md)
mit dem jetzt vorliegenden Surface-Supportlog und der bestätigten Touchbedienung.
Die Überlappungsbeschriftung und Begrenzung des Pfeiltempos aus 27.5.1 bleiben erhalten.

## Pfeile auf dem Touchscreen

Das Surface-Log enthält keinen einzigen `MANUAL_JOG_START`, `MANUAL_JOG_STOP` oder
Keepalive-Befehl und auch keinen entsprechenden NACK. Die Firmware bestätigt
Pan 10°/s und Tilt 5°/s; der in 27.5.1 behobene Fall niedrigerer Gerätegrenzen
erklärt diesen Versuch daher nicht. Der Benutzer bedient die vier Pfeile direkt
auf dem Touchscreen.

Die Pfeile behandeln jetzt den vollständigen nativen Pointer-Kontaktstrom,
statt auf eine in Mausereignisse umgesetzte Haltegeste zu warten. Der erste
Primärkontakt startet sofort. Loslassen, Verlassen der Schaltfläche, Abbruch,
Capture- oder Fokusverlust stoppen. Ein zweiter Finger übernimmt die Fahrt
nicht; Zurückziehen nach Verlassen startet sie nicht neu. Loslassen vor dem
Start-ACK führt ebenfalls zum Stop. Gedrückthalten als Rechtsklick/Flick ist
auf den Pfeilen deaktiviert. Maus- und Tastaturbedienung bleiben vorhanden;
aus einem Pointerkontakt erzeugte Mausereignisse beenden ihn nicht vorzeitig.
Physische Bildschirmkoordinaten werden vor der Bereichsprüfung in die
DPI-Koordinaten des Fensters umgerechnet.

Microsoft beschreibt [Pointer-Down und implizite Capture](https://learn.microsoft.com/en-us/windows/win32/inputmsg/wm-pointerdown),
[Capture-Verlust ohne garantiertes Up](https://learn.microsoft.com/en-us/windows/win32/inputmsg/wm-pointercapturechanged),
[die DPI-Umrechnung](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-physicaltologicalpointforpermonitordpi)
und [die Gestensteuerung pro Fenster](https://learn.microsoft.com/en-us/windows/win32/tablet/wm-tablet-querysystemgesturestatus-message).

Der native Nachrichten-Test prüft 19 Fälle über die echte Fensterprozedur,
darunter alle vier Pfeile, Mehrfachkontakt, Capture-Verlust und spätes ACK.
Das ersetzt die abschließende Fingerprüfung auf dem Surface nicht.

## Was das Aufnahmeprotokoll tatsächlich zeigt

Quelle: `PanoramaSupport_20261026_182751_606_e10566.log`, lokal unverändert unter
`analysis/2026-10-26-surface/surface-support.log` im übergeordneten Arbeitsbereich.
SHA-256: `827DAAD01E13543D800CFD3F1782B05355C446267F0B553E00E67D988327AE8C`.
Die Auswertung prüft 8.188 gültige Protokollframes; 33 ACKs haben einen Median
von 88 ms und ein Maximum von 149 ms. Die größte Statuslücke während der
Aufnahme beträgt 555 ms. Mehrsekündige Verzögerungen durch Befehlswiederholungen
sind in diesen Fahrten nicht belegt.

Der Kopf verwendet Firmware **2.4.75**, Konfigurationsrevision **33**. Bei Lauf
2520474873 bleibt das Pan-Ziel über mehrere Bilder **100,121°**. Die Istwerte
wechseln von **99,360°** auf **100,679°** und danach **100,107°**, ohne neue
Pan-Zielvorgabe des Clients. Damit ist die Gegenkorrektur am unveränderten Ziel
belegt. Die auf 0,1° gerundeten Debugtexte dürfen nicht als neue Sollbefehle
interpretiert werden. Beide Apps verwenden dieselbe geräteseitige Regelung.

Der erste Surface-Auftrag überträgt **Pan 2000 ‰ / Tilt 1000 ‰**, der zweite
**1000 / 1000 ‰**. Die frühere Vermutung „Windows immer 1000 statt Android
1700“ erklärt den ersten Auftrag ausdrücklich nicht. Der gesicherte Android-
Stand vom 24.10. enthält 1700 ‰, 500 ms Beruhigung und 300 ms Belichtung.
Surface verwendet zunächst 2000, später 1000 ms Beruhigung sowie 500 ms
Belichtung. Das sind unterschiedliche Rezepte. Lange Spaltenrückfahrten besitzen
zusätzlich eigene Firmwareparameter. Ein vollständiger Geschwindigkeitsvergleich
oder eine behobene physische Pan-Störung wird damit nicht behauptet.

## Hardwarestand und weitere Messung

Alle 129 Motorparameter (Revision 33) und der erhaltene Auftrag mit 39 erfassten
Bildern wurden vor dem Neustart nur ausgelesen und gesichert. Der gespeicherte
Fehler lautete `FAULT_TILT_SENSOR`. Nach dem vom Benutzer ausgeführten Aus- und
Einschalten meldet Boot 149 **IDLE/NONE**, beide Winkelwerte werden aktualisiert.
Die elektrische Ursache des früheren Sensorfehlers ist dadurch nicht geklärt.

Ein isoliertes Reibungsmodell reproduziert zu große kurze Pan-Korrekturen.
Versuchte Regelungsänderungen verschlechterten andere Fälle und wurden vollständig
verworfen. Das Modell ist nicht auf diesen Kopf eingemessen. Produktionsfirmware
und Motorparameter bleiben unverändert; die unveränderte Firmware-Hostsuite
bestand mit 20.286 Assertions.

Vorbereitet ist eine vorhandene Diagnosevariante derselben Firmware mit gepufferter
USB-Messspur. Der konkrete Messplan bewegt Pan um +2,062° und zurück bei unverändertem
Tilt-Ziel, ohne Kameraauslösung, mit Stop und Auslesen nach jeder Fahrt. Standardmäßig
erstellt `--pan-measure` nur den Plan. Flashen und reale Fahrten benötigen gesonderte
Freigabe und einen freien, beaufsichtigten Aufbau.

Der anschließend freigegebene Messversuch wurde nach 1,5 s mit `MOTION_STALLED`
gestoppt; eine Rückfahrt und Kameraauslösung fanden nicht statt. Die reguläre
2.4.75 wurde wiederhergestellt und ist per SHA-256 bytegleich mit dem archivierten
Build vom 23.10. Alle 129 Motorparameter sind weiterhin identisch. Ein späterer
Rohstatus zeigte im selben Boot 151 einen Tilt-Sprung von etwa −1,7° auf −54,3°
bei Benutzer-Nullpunkt 0. Der feste Sensorversatz 2396 entspricht 52,646°; seine
Unabhängigkeit von einem Sensor-Neustart wird als separater Firmwarekandidat
2.4.76 nur lokal geprüft. Er wurde noch nicht übertragen und ist kein behobenes
Pan-Überschwingen. Die Spannungseinstellung „88“ der Minigorilla ist laut Benutzer
unverändert auch zuvor verwendet worden.

Nach einem vollständigen Neustart beider Versorgungen meldet die unveränderte
2.4.75 bei Boot 152 wieder Tilt −1,714° innerhalb der unveränderten Grenzen.
Der Benutzer bestätigt anschließend funktionierende Android-Pan-Pfeile bei
plausiblem Tilt. Der anschließend direkt vom Handy gesicherte Android-Lauf
819366978 enthält 28 eindeutige Bildabschlüsse. Pan verfehlt das Ziel dabei
maximal um 0,286°, Tilt maximal um 0,761°. Vier Tilt-Ziele werden mit
`STEP_TARGET_MISSED` abgeschlossen. Am Anfang der zweiten und vierten Spalte
meldet die Firmware `MOTION_STALLED` mit automatischer Wiederholung; an der
vierten Spalte pausiert der zweite Fehlversuch nach 18 Bildern. Die angebotene
Spaltenwahl gehört zu dieser Erholung, nicht zur Android-Qualitätspause.
Die auslösende Achse ist aus den 500-ms-Statusproben noch nicht sicher bestimmbar.
Auswertung: `analysis/2026-10-26-surface/ANDROID-AUSWERTUNG-20261027.md` im
übergeordneten Arbeitsbereich. Ein kontrollierter Vergleich gleicher Rezepte
und ein nachgewiesener Fix der physischen Aufnahmebewegung bleiben offen.

## Softwareprüfung

Der Paketbuild führt Host-/Controller-, WinForms-, Vorschau-, Navigations- und
Touchprüfungen aus. Er enthält keine Hardwarefahrt. Die reale Surface-Bedienung
und die physische Pan-Korrektur bleiben getrennte, offene Nachweise.

Bestanden: 1.415 Host-/Controller-, 129 UI-, 16 Vorschau-, 30 Navigations- und
19 Touchprüfungen. Eine zusätzliche Prüfung durch den vollständigen UI-Start
und Verbindungsaufbau erlaubt ausschließlich `HELLO`, `GET_*` und `HEARTBEAT`,
keine Schreibbefehle für Motorparameter, Grenzen oder Nullpunkt. Das bereitgestellte
Surface-Log enthält ebenfalls keine solchen Schreibbefehle; andere Sitzungen
des Tages oder ein Auslöser über die Bewegungsfolge sind damit nicht ausgeschlossen.

```powershell
Tests/WindowsSmoke/bin/WindowsSmoke.exe touch-repro Tests/WindowsSmoke/artifacts/touch
Tests/build-windows.ps1
```
