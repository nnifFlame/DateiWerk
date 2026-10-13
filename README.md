# DateiWerk

Open-Source-Werkzeug für Windows x64: Dateien konvertieren, ZIP-Archive erstellen und entpacken, Dateien und ganze Ordner mit AES-256-GCM verschlüsseln.

## Download und Installation

**[Windows-Installer herunterladen](https://github.com/nnifFlame/DateiWerk/releases/latest/download/DateiWerk-Setup.exe)**

Alle Versionen und Hinweise: [GitHub-Releases](https://github.com/nnifFlame/DateiWerk/releases).

Installer starten, Lizenz und Nutzungshinweise lesen und zustimmen. Desktop-Verknüpfung, Explorer-Menü und automatische Updates sind auswählbar. DateiWerk wird für das aktuelle Benutzerkonto fest unter `%LOCALAPPDATA%\Programs\DateiWerk` installiert. Eine separate .NET- oder FFmpeg-Installation ist nicht erforderlich. Deinstallation ist über Windows' installierte Apps oder das Startmenü möglich.

**Der Installer und die Anwendung sind aktuell nicht codesigniert.** Windows kann eine Vertrauensmeldung anzeigen oder die Anwendung blockieren. Es werden keine Windows-Sicherheitseinstellungen geändert.

## Funktionen

- PDF → bearbeitbarer DOCX-Text, TXT, HTML oder Seitenbilder; bei mehreren Seiten werden sämtliche Bilder in einem ZIP gespeichert.
- PNG, JPG, WebP, BMP, GIF, TIFF und ICO → gängige Bildformate oder PDF.
- Textdateien → PDF und DOCX; CSV/TSV und JSON-Zeilentabellen → XLSX ohne Excel-Installation.
- Word-, Excel- und PowerPoint-Dateien → PDF und weitere Office-Formate mit der separat installierten Microsoft-Desktop-Anwendung.
- Audio/Video → MP3, WAV, FLAC, OGG, MP4, MKV und WebM; Medienkonverter integriert.
- ZIP für Dateien und vollständige Ordner; Schutz vor Überschreiben und Archivpfaden außerhalb des Zielordners.
- Passwortverschlüsselung für Dateien und Ordner. Ordner werden zuerst gezippt und als `.zip.dwenc` verschlüsselt. Nach dem Entschlüsseln das ZIP wieder entpacken.
- Rechtsklick auf eine Datei oder einen Ordner → DateiWerk. Unter Windows 11 gegebenenfalls unter „Weitere Optionen anzeigen“.

Die Formatliste passt sich an die analysierte Datei an. Nicht jede beliebige Formatkombination ist sinnvoll oder unterstützt. PDF-Textkonvertierung erhält nicht vollständig das Original-Layout; OCR ist noch nicht enthalten. Animationen und mehrseitige Bilder sind noch nicht konvertierbar. Weitere Einschränkungen und die Konvertierungsmatrix stehen in [LIESMICH.md](LIESMICH.md).

## Updates

Bei aktivierten automatischen Updates wird beim Start das neueste stabile GitHub-Release geprüft. Der Installer wird heruntergeladen, per SHA256 geprüft und installiert, wenn keine Dateiverarbeitung läuft. Ist bereits eine Datei ausgewählt, bleibt das Update zur späteren Installation bereit. Bei geänderten Bedingungen oder einer portablen Kopie wird der normale Installer geöffnet und fragt nach Zustimmung. Offline oder bei einem Fehler bleibt die vorhandene Version nutzbar. „Updates automatisch“ kann im Programm ausgeschaltet werden.

Die Update-Prüfung überträgt keine Dateiinhalte, ausgewählten Dateinamen oder Passwörter. GitHub erhält technisch IP-Adresse, Zeitpunkt und HTTP-Anfrage. Details: [NUTZUNGSBEDINGUNGEN.txt](NUTZUNGSBEDINGUNGEN.txt).

SHA256 dient der Downloadprüfung; sie ersetzt keine Codesignierung und schützt nicht vor einem kompromittierten Release-Konto.

## Build und neue Releases

Benötigt Windows x64, .NET 10 SDK, PowerShell 7 und `tar` (in aktuellen Windows-Versionen enthalten).

```powershell
./Build.ps1 -Version 0.5.0
```

Das Skript lädt den gepinnten FFmpeg-9.0.2-Build, prüft seine SHA256, erstellt das Medienpaket, baut die eigenständige EXE und kompiliert den Inno-Setup-Installer. `build/release/` enthält `DateiWerk-Setup.exe` und `update.json`; beide gehören in dasselbe stabile GitHub-Release mit Tag `vMAJOR.MINOR.PATCH`.

`Assets/Media.zip` und Build-Ausgaben werden nicht im Repository gespeichert. FFmpeg-Quellen, Build-Konfiguration und Lizenzen sind in `FFmpeg-README.txt` und `KOMPONENTEN.md` dokumentiert. Der Inno-Setup-Compiler unterliegt seinen eigenen Lizenzbedingungen; kommerzielle Build-Nutzung kann eine entsprechende Compilerlizenz erfordern. Das schränkt die GPL-Rechte am DateiWerk-Code nicht ein.

Der Workflow unter `.github/workflows/release.yml` baut und veröffentlicht neue Installer beim Push eines `v*`-Tags oder durch „Run workflow“ mit einer Versionsnummer. Für inhaltlich geänderte Nutzungshinweise müssen Zustimmungsversion und Updater-Konstante ebenfalls erhöht werden; solche Änderungen dürfen nicht stillschweigend akzeptiert werden.

## Prüfungen

Die Anwendung enthält `--self-test` mit Archiv-, Verschlüsselungs-, Tabellen-, Eingabe- und Update-Quellenprüfungen. Zusätzliche PDF-/WebP-, Medien- und Office-Integrationstests sind im Quellcode enthalten. Installer-Tests erfolgen mit einer separaten Test-App-ID. Komplexe Office-Dateien, alle Codecs und jede Windows-Version sind damit nicht vollständig getestet.

## Lizenz und Kontakt

Eigener Quellcode: **GPL-3.0-only**, siehe [LICENSE](LICENSE). Fremdkomponenten behalten ihre jeweiligen Lizenzen und Urheberrechtsvermerke. Nutzungshinweise schränken die Open-Source-Rechte nicht zusätzlich ein; gesetzlich zwingende Haftung und Verbraucherrechte bleiben unberührt. Die Hinweise sind ein projektspezifischer Entwurf, keine Bestätigung einer juristischen Prüfung.

Kontakt und Fehlerberichte: [GitHub-Issues](https://github.com/nnifFlame/DateiWerk/issues). Bitte keine vertraulichen Dateien oder Passwörter veröffentlichen.
