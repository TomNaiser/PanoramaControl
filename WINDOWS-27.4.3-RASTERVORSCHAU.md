# Windows 27.4.3 — automatische Rastervorschau und Endpunktanzeige

17. Oktober 2026. Nacharbeit zum Screenshot aus Windows 27.4.2. **Firmware, Geräteparameter und Achsenlimits wurden nicht geändert; es gab keine reale Motorfahrt.**

## Befund und Ursache

Der GUI-Test übernimmt die Winkel aus dem Screenshot: Start Pan +5,78° / Tilt +13,07°, Ende Pan −24,68° / Tilt +1,45°, 400 mm, Sensor 24 × 35,9 mm, Überlappung 40 %. Vor der Korrektur berechnete Windows zwar die Bildanzahl 16 × 5, erneuerte beim Setzen der Endpunkte aber nicht das für die Grafik verwendete Planobjekt. Der Test scheiterte reproduzierbar mit „Nach Start und Ende muss automatisch der neue Fahrplan gezeichnet werden, ohne Vorschau-Klick“.

Eine zweite Ursache betraf bereits geladene Aufträge: Die Grafik bevorzugte deren alten Plan vor einem geänderten lokalen Entwurf. „Plan einpassen“ verwendete dagegen den lokalen Entwurf. Damit konnten Grafikinhalt und Skalierungsgrundlage auseinanderlaufen.

Ein zusätzlicher Fehler im Winkel-/Pixeltransform ließ sich nicht bestätigen: Die Start-/Endkreuze des Screenshots passen zu den angezeigten Winkeln. Die grauen Linien sind die empfangene Bewegungshistorie, keine alten Endpunktmarkierungen. Tests prüfen nun ausdrücklich, dass Zoom und Fenstergröße keine Winkel ändern und keine Kreuzmarkierung an ihrer alten Pixelposition stehen bleibt.

## Korrigierter Ablauf

1. Im Diagramm anfahren und Start/Ende setzen. Das Raster wird dabei automatisch neu erzeugt und gezeichnet; ein Vorschau-Klick ist nicht erforderlich. Das Beispiel ergibt 80 Zielpunkte.
2. Die vorhandenen Eingaben für Brennweite, Sensor, Überlappung, Zeiten und Tempo aktualisieren den Entwurf ebenfalls. Kurze, noch ungültige Texteingaben zeigen kein altes Raster und sperren Übertragen. Die Grafikleiste nennt Größe bzw. Fehler des Entwurfs.
3. Im inaktiven Zustand zeigt die Grafik den lokalen Entwurf. „Plan einpassen“ benutzt exakt denselben Plan. Start und Ende sind beschriftet; alle Punkte und Marker werden aus ihren Winkeln in die aktuelle Ansicht projiziert. Die Tilt-Anzeige startet weiterhin bei ±25°; Softwarelimits gelten unabhängig davon.
4. „Übertragen“ lädt diesen Entwurf. Ein vorhandener geladener, gestoppter oder abgeschlossener Auftrag wird bei dieser ausdrücklichen Aktion kontrolliert gestoppt/gelöscht, IDLE bestätigt und danach durch den neuen Plan ersetzt. Eine Änderung allein löscht den Geräteplan nicht. Ein vorgelagertes „Neuer Plan“ ist für diesen Fall nicht mehr nötig. Planvalidierung und Referenzprüfung erfolgen vor bzw. während der Übernahme.
5. Eine Aufnahme wird dadurch nicht gestartet. Bei vom geladenen Plan abweichendem Entwurf ist Start gesperrt, bis neu übertragen wurde. Während laufender oder pausierter Aufnahme bleibt die Grafik beim tatsächlichen Geräteplan; Übertragen ist gesperrt. Nach Stop erscheint wieder der lokale Entwurf.

Gespeicherte Archivpläne verwenden ein eigenes Planobjekt und überschreiben den lokalen Entwurf nicht mehr. „Live“ bzw. das bewusste Ändern von Endpunkten kehrt zur aktuellen Planung zurück. „Neuer Plan“ erhält die vorhandene Vorschau zusammen mit den lokalen Eingaben.

Die Rastergeometrie wurde nicht verändert: Bildschritte folgen weiterhin Bildwinkel und Überlappung, Bildanzahl und Rasterzielwinkel entsprechen dem Android-Rezept. Ein gesetzter Endpunkt bezeichnet daher den gewünschten Bereich; je nach gerundeter Bildanzahl muss er nicht exakt mit dem letzten Rasterpunkt zusammenfallen.

## Prüfung und Paket

Build einschließlich aller Prüfungen: `pwsh -File Tests/build-windows.ps1`. Der neue reproduzierbare GUI-Fall ist auch einzeln aufrufbar: `Tests/WindowsSmoke/bin/WindowsSmoke.exe preview-repro Tests/WindowsSmoke/preview-repro`.

Ergebnis: **187 Hostprüfungen, 115 bisherige WinForms-Prüfungen und 16 zusätzliche Raster-/Skalierungs-/Transferprüfungen bestanden** (insgesamt 131 GUI-Prüfungen). Release-Build erfolgreich, sieben unveränderte Warnungen im Legacy-Code. Die Simulatorantwort auf LOAD_PLAN spiegelt jetzt das tatsächlich übertragene Rezept statt eines festen 2×2-Testplans. Der Test vergleicht übertragenes und gezeichnetes Rezept Feld für Feld.

Nachweis: `Tests/WindowsSmoke/build-results-27.4.3.txt`; Ansichten des Screenshot-Beispiels und des geänderten geladenen Plans unter `Tests/WindowsSmoke/preview-repro/`. Es wurde keine reale Geräteverbindung geöffnet. Physische Zielgenauigkeit und Bildqualität sind durch diese Softwareprüfungen nicht neu abgenommen; der frühere Tilt-Befund bleibt offen.

Paket: `releases/27.4.3/PanoramaControl.exe` bzw. `releases/PanoramaControl-27.4.3.zip`. Die bisherige [Bedienung](WINDOWS-27.4.2-BEDIENUNG.md) bleibt bis auf den hier ersetzten Plan-/Vorschauablauf bestehen.
