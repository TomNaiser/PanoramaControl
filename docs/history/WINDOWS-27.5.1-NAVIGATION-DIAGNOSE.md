# Windows 27.5.1 — Layout, Haltefahrt und offene Bewegungsdiagnose

Stand: 26. Oktober 2026. Ergänzung zur technischen Android-Angleichung in
[27.5.0](WINDOWS-27.5.0-ANDROID-PARITAET.md). Das bestehende Layout bleibt erhalten.

## Reproduzierte und korrigierte Fehler

Die deutsche Beschriftung „Überlappung (%)“ überschneidet sich mit der Eingabe.
Der gezielte WinForms-Test meldete bei der lokalen Skalierung Label
`{X=338,Y=32,Width=85,Height=13}` gegen Eingabe `{X=405,Y=29,Width=88,Height=20}`:
18 Pixel Überdeckung. Die Beschriftung richtet sich nach Sprachwechsel rechtsbündig
links neben dem unverändert positionierten Eingabefeld aus.

Bei bestätigtem Geräte-Maximum unter den Windows-Vorgaben von Pan 10°/s bzw.
Tilt 5°/s bleiben die Pfeile freigegeben, aber `BeginJogAsync` lehnt die feste
Geschwindigkeit lokal ab. Der Test reproduziert das mit Pan 4°/s und Tilt 1°/s.
Die Oberfläche verwendet jetzt das kleinere Tempo aus Benutzerwunsch und
bestätigtem Gerätelimit. Die gespeicherte Präferenz bleibt erhalten. Auch der
Optionendialog zeigt diese wirksame Geschwindigkeit. Das Protokoll und die
firmwareseitigen Grenzen werden nicht aufgeweicht.

Pfeil-Drücken und der konkrete Stopanlass (Maus losgelassen, Maus verlassen,
Capture verloren, Fenster deaktiviert oder Tastaturfreigabe) erscheinen im
Supportlog. „Antrieb & Grenzen“ zeigt Firmware-Version, Konfigurationsrevision
und bestätigte Pan-/Tilt-Tempi des geladenen Auftrags.

## Langsame lange Aufnahmewege

Die gesicherte Android-Einstellung vom 24.10. aus
`analysis/2026-10-24-recovery/after-live-export.tar`, `shared_prefs/settings.xml`,
enthält Aufnahmegeschwindigkeit **1700 ‰**. Windows startet ohne eigene
Tempopräferenz mit **Faktor 1 = 1000 ‰**. Das ist eine belegte unterschiedliche
Einstellung, aber noch kein Vergleich der aktuell auf dem Surface und Kopf
geladenen Rezepte. Windows-Faktor **1,7 entspricht Android 1700 ‰**. Beide
Windows-Achsen besitzen getrennte Geschwindigkeitsfelder unter „Antrieb & Grenzen“.

Die Regression führt diese Eingabe durch echte UI-Planung und Übertragung:
Im gesendeten `LOAD_PLAN` müssen beide Felder `1700` sein. Es gibt dabei keine
Reduktion auf das separate manuelle Zielfahrtempo von 500 ‰. Das bestätigte
Aufnahmetempo wird angezeigt und die Zuordnung zu Android in beiden Tooltips
erklärt. Laufgeschwindigkeiten und persönliche Einstellungen wurden nicht
automatisch erhöht.

## Offene Ursachen und notwendiger Praxisnachweis

Der Benutzer bestätigt, dass Pan-Überschwingen mit Gegenkorrektur sowie langsame
lange Wege **während der Aufnahme** auftreten; betroffen sind auch Android und
die vier Windows-Schaltflächen auf einem **Surface Go**. Die Firmware-Version
dieses Aufbaus ist noch unbekannt. Aktuelle Logs dieses Surface stehen lokal
nicht zur Verfügung; ADB meldet kein angeschlossenes Android-Gerät.

Die vorhandenen Firmware-Hosttests reproduzieren die neue physische Störung
nicht. Firmware und Motor-/Bremsparameter wurden daher nicht verändert oder
geflasht. Die Korrektur des reproduzierten Pfeil-Limitfalls beweist noch nicht,
dass genau dieser Fall die Ursache auf dem Surface ist. Ein behobenes reales
Pan-Überschwingen oder eine abgeschlossene Hardwareabnahme wird nicht behauptet.

Für die Fortsetzung wird das aktuelle Surface-Supportlog mit Verbindungsaufbau,
Pfeilversuch und betroffener Aufnahmefahrt benötigt. Darin lassen sich
Firmware-Version, Gerätefreigaben, geladenes Tempo, Soll-/Ist-Verlauf,
Wiederholungen und Fehlercodes gemeinsam zuordnen. Export:
**Läufe & Warnpunkte → Support …**. Direkter Ordner:
`%LOCALAPPDATA%\PanoramaControl\SupportLogs`.

## Hardwarefreie Prüfung

Gezielte Regression (Windows-Mausnachrichten statt ausschließlich reflektierter
Eventaufrufe):

```powershell
Tests/WindowsSmoke/bin/WindowsSmoke.exe navigation-repro Tests/WindowsSmoke/artifacts/navigation
```

Geprüft werden alle vier Pfeile, Fahrt bis zum Loslassen, Stop, niedrigere
Gerätegrenzen einschließlich höher gespeicherter Benutzerwünsche, deutsche und
englische Beschriftung, 1700-‰-Planübertragung und Support-Gesten.

Die vorhandene Firmware-Hostsuite bestand unverändert mit **20.286 Assertions**.
Es wurden keine realen Motor- oder Kameraaktionen gestartet.

Abgeschlossen: **1.415 Host-/Protokoll-/Controllerprüfungen**, **129 simulierte
WinForms-Prüfungen**, **16 Raster-/Skalierungs-/Transferprüfungen** und **29
gezielte Navigationsprüfungen**. Navigations- und Antriebsansicht wurden nach
dem Fix gerendert und visuell kontrolliert. Der Paketbuild erzeugt Windows
27.5.1 für x64/.NET Framework 4.8. Die bestehenden Warnungen in den alten
V1-Kommunikations-/Acquisitionsklassen bleiben unverändert.
