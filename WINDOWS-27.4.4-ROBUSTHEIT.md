# Windows 27.4.4: Bedienung bei verzögertem Bluetooth-Schreiben

Serielle Schreibvorgänge laufen außerhalb des UI-Threads und außerhalb der Sperre für ACK-Verarbeitung und Abbruch. Ein blockierender Transport hält dadurch nicht mehr den Aufruf eines asynchronen Kommandos oder die Protokoll-Abbruchroutine fest. Ausgaben bleiben serialisiert; vor dem tatsächlichen Schreiben werden überholte, durch Not-Stop verworfene Befehle erneut abgefangen. Der COM-Transport behält seinen vorhandenen 500-ms-Schreibtimeout.

Der neue Test `dotnet run --project Tests/ProtocolTests/ProtocolTests.csproj -- --blocked-transport` war zuvor nach 300 ms rot. Die vollständige Protokoll-/Controller-Suite besteht mit 192 Prüfungen. Der simulierte WinForms-Test besteht mit 115 Prüfungen; Tests asynchroner UI-Aktionen warten jetzt auf deren tatsächlichen Abschluss. Die Windows-App lässt sich für .NET Framework 4.8 bauen. Vorhandene Warnungen im Legacy-Code bleiben bestehen.

Diese Windows-Prüfungen verwenden einen Simulator. Die realen Tilt-Messungen wurden über einen separaten Bluetooth-Test durchgeführt und belegen keine Windows-Dauermessung. Bericht mit physischer Statistik und Grenzen: `../analysis/2026-10-22-robustness/ERGEBNIS.md` im Gesamtprojekt.

Build und Paket: `pwsh -File Tests/build-windows.ps1`; das neue Paket verwendet Version 27.4.4. Die Bedienung der vorherigen Version 27.4.3 bleibt erhalten.
