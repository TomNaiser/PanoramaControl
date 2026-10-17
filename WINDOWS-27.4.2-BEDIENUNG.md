# Windows 27.4.2 — Anzeige, klassische Navigation und Android-Pfeile

17. Oktober 2026. Die drei Benutzerpunkte wurden in dieser Reihenfolge implementiert und jeweils mit der wirklichen WinForms-Oberfläche an einem simulierten Gerät geprüft. **Die Nano-Firmware wurde weder geändert noch geflasht.** Es wurden keine Geräteparameter oder Achsenlimits geschrieben und keine realen Fahrten ausgeführt.

## 1. Tilt-Anzeige

Die erste Diagrammansicht zeigt Pan ±180° und Tilt ±25°. In der bestehenden Grafikleiste stehen „Tilt ±25°“, „Tilt ±45°“ und „Tilt ±90°“. Zoom, Verschieben und Plan einpassen bleiben verfügbar; „Pan ±180°“ stellt die Übersicht mit dem gewählten Tilt-Grundbereich wieder her. Die Einstellungen betreffen ausschließlich die Anzeige. Das aus Geräteposition und bestätigten Softwarelimits abgeleitete Fahrverbot bleibt unabhängig davon gültig, auch bei einem Klick in einen vergrößerten Anzeigebereich. Die große Grafik verwendet dieselbe Ansicht.

Zwischenprüfung nach diesem Punkt: 97 GUI-Prüfungen bestanden, darunter Ausgangsbereich, Erweiterung ohne Gerätebefehl und Zurückweisung eines außerhalb der Tilt-Limits liegenden Fahrziels.

## 2. Klassische Windows-Bedienung

Referenz war `Form1.panel1_MouseDown` sowie die beiden Endpunkttasten der alten, in Git enthaltenen Windows-Version: Position anklicken → dorthin fahren → erreichte Position mit Start-/Endpunkttaste übernehmen.

Ein Linksklick ins Diagramm fährt jetzt wieder unmittelbar an; ein zweiter Klick auf „Ziel anfahren“ ist dafür nicht erforderlich. „Start setzen“ und „Ende setzen“ übernehmen nach Beruhigung einen neu angeforderten Istwert. Sie übernehmen weder ein nur ausgewähltes Ziel noch eine ACK-bestätigte, noch laufende Fahrt. Rechtsziehen verschiebt ausschließlich die Grafik. Während aktiver Aufnahme, manueller Fahrt, veralteter Statusdaten oder außerhalb der bestätigten Softwarelimits startet der Diagrammklick keine Fahrt. Ein inaktiver Geräteplan bleibt bei Navigation erhalten. Numerische Zielwahl und Endpunktbearbeitung bleiben zusätzlich verfügbar.

Zwischenprüfung nach diesem Punkt: 101 GUI-Prüfungen bestanden, einschließlich direkter Linksklickfahrt, erhaltenem Geräteplan, unverfälschtem Istwert sowie Rechtsklick und ungültigem Ziel ohne Fahrt.

## 3. Pfeile wie Android

Referenz: Android `MainActivity.arrowButton`, `PanoramaService.beginJog/endJog` und die vorhandene Dokumentation der Haltefahrt. Beim Drücken wird genau ein `MANUAL_JOG_START_VELOCITY` mit Achse, Richtung und Höchsttempo gesendet, anschließend Keepalives und beim Loslassen `MANUAL_JOG_STOP`. Die bereits vorhandene Firmware baut die Geschwindigkeit von null über ihre Beschleunigungsrampe auf. Längeres Halten erlaubt damit höhere Geschwindigkeit bis zum eingestellten Höchsttempo; Windows erzeugt weder eine Serie kleiner Zielschritte noch eine neue hostseitige Motorregelung.

Vorgaben für die angeforderten Höchsttempi bleiben Pan 10°/s und Tilt 5°/s. Vorhandene lokale Geschwindigkeitswerte bleiben erhalten und werden gegen die gemeldeten Geräteobergrenzen geprüft. Einstellbar unter „Antrieb & Grenzen → Fahrt / Referenz / Warnungen“. Beschleunigungsparameter und Softwarelimits im Nano bleiben unverändert.

Die alte Umschaltung „Pfeile halten“ und die Schritt-Auswahl wurden aus der Hauptleiste entfernt: Die Pfeile verwenden unmittelbar die Android-Haltebedienung. Loslassen, Verlassen der gedrückten Taste, Capture-Verlust, Fenster-/Tastatur-Fokusverlust und Verbindungsverlust beenden die Geste. Loslassen vor dem Start-ACK verhindert verspätete Haltesignale. Der normale Windows-Click nach MouseUp erzeugt keine zweite Fahrt. Tastaturbedienung mit Leertaste/Enter am fokussierten Pfeil sowie Richtungstasten an einem fokussierten Pfeil unterstützt dieselbe Start-/Stop-Geste; Wiederholungsereignisse starten sie nicht erneut. Texteingaben behalten ihre normale Tastaturbedienung. Ein barrierefreier Einzelklick ohne Loslassereignis ist wie Android auf eine Sekunde begrenzt.

Zwischenprüfung nach diesem Punkt: 115 GUI-Prüfungen bestanden, einschließlich gehaltenem Start mit korrekter Achse/Richtung/Höchsttempo, genau einem Start und Keepalives, keinem zusätzlichen Klickschritt, Mausverlust, Tastaturwiederholung, Fokusverlust, frühem Loslassen und begrenztem Einzelklick.

## Paket und Prüfumfang

Start: `releases/27.4.2/PanoramaControl.exe`; vollständiges Paket `releases/PanoramaControl-27.4.2.zip`. Reproduzierbarer Build einschließlich Host- und UI-Prüfung: `pwsh -File Tests/build-windows.ps1`. Diese Prüfung öffnet keine reale Geräteverbindung.

Der abschließende Paketbuild prüft die bisher 187 Softwarefälle und alle 115 UI-Fälle. Nachweis: `Tests/WindowsSmoke/build-results-27.4.2.txt`. Gerenderte Ansichten unter `Tests/WindowsSmoke/artifacts/`. Die unveränderten Warnungen im Legacy-Code sind im Buildprotokoll enthalten.

Geprüft ist der Windows-Bedien- und Protokollablauf mit simuliertem Gerät. Die reale Beschleunigung und mechanische Zielgenauigkeit wurden in dieser Nacharbeit nicht erneut vermessen. Die offene Tilt-Abweichung der früheren Hardwareprüfung bleibt offen. Der bisherige [Funktionsabgleich 27.4.1](WINDOWS-27.4.1-ABGLEICH.md) und die [Geräteprüfung 27.4.0](WINDOWS-27.4.0-ABNAHME.md) bleiben als Hintergrund erhalten; die hier beschriebenen Bedienungsänderungen ersetzen die dortige ältere Windows-Pfeil-/Klickbedienung.
