# DateiWerk 0.5 – Windows-Prototyp

`DateiWerk.exe` ist die neue eigenständige Anwendung für Windows x64. Keine separate .NET-, PDF- oder FFmpeg-Installation erforderlich. Die erste Ausführung entpackt interne Laufzeitkomponenten in den temporären .NET-Ordner. Bei der ersten Mediendatei werden die integrierten, anhand ihres SHA256 geprüften FFmpeg-Programme in einen temporären DateiWerk-Ordner entpackt. Der Installer installiert das Programm fest und bietet Desktop-Verknüpfung, Explorer-Menü und automatische Updates an. Eine ältere geöffnete Programmversion schließen und ausdrücklich die neue EXE starten.

**Prüfstatus:** Release-Build erfolgreich. Die gepackte EXE besteht 49 allgemeine Prüfungen, 26 PDF-/WebP-Prüfungen und 18 Medienprüfungen. Zwei weitere Integrationstests bestätigen, dass die selbst erzeugten DOCX- und XLSX-Dateien tatsächlich von Word und Excel gelesen werden. Die 15 Office-Exporttests der vorherigen Version bestanden ebenfalls; der Office-Konverter wurde nicht verändert. Die Oberfläche wurde mit ausgewählter PDF gerendert und visuell geprüft. Dies ist ein Prototyp, keine sicherheitsauditierte Veröffentlichung. Die Prüfungen decken einfache Beispieldokumente ab; komplexe Layouts, geschützte Dateien und sämtliche Mediencodecs sind nicht vollständig getestet. Die EXE ist nicht signiert. Bei aktivem Smart App Control kann sie weiterhin blockiert werden. Die Entwicklung hat keine Windows-Sicherheitseinstellungen verändert.

## Bedienung

### Explorer-Rechtsklick-Menü

DateiWerk 0.5 enthält den Eintrag **DateiWerk** für das Kontextmenü von Dateien und Ordnern. Auf diesem Rechner wurde er für die neue EXE eingerichtet. Rechtsklick → unter Windows 11 gegebenenfalls „Weitere Optionen anzeigen“ → DateiWerk. Das Programm lädt die angeklickte Datei bzw. den Ordner automatisch. Es werden weder Dateizuordnungen geändert noch andere Menüeinträge entfernt.

Über „Explorer-Menü einrichten“ lässt sich der Eintrag nach Verschieben der EXE auf den neuen Programmpfad aktualisieren. „Explorer-Menü entfernen“ löscht ausschließlich den DateiWerk-Eintrag. Die Registrierung erfolgt für den aktuellen Benutzer unter `HKCU\Software\Classes\*\shell\DateiWerk` und `HKCU\Software\Classes\Directory\shell\DateiWerk`; keine Administratorinstallation erforderlich. Das klassische Explorer-Menü wird ergänzt, die gesamte Windows-Menüdarstellung wird nicht verändert.

### Ordner verschlüsseln

„Verschlüsseln“ → „Ordner wählen“ → neues Ziel außerhalb des Quellordners → Passwort mit mindestens 12 Zeichen und Bestätigung → Start. Ausgabe: `Ordnername.zip.dwenc`. Unterordner und leere Ordner bleiben enthalten. ZIP-Stufe ist einstellbar; „Schnell“ ist voreingestellt. Verknüpfungen werden abgewiesen.

Das Programm legt für diesen Vorgang vorübergehend ein unverschlüsseltes ZIP im temporären Benutzerordner an. Dieses wird nach Erfolg, normalem Fehler oder Abbruch entfernt; nach einem Absturz kann es zurückbleiben. Die Originaldaten werden nicht gelöscht. Entschlüsseln erzeugt ein normales ZIP. Dieses anschließend mit „ZIP entpacken“ wieder als Ordner herstellen. Das separate Python-Entschlüsselungsskript funktioniert ebenfalls und liefert das ZIP zurück.

Die allgemeine Prüfsuite enthält nun 46 Prüfungen einschließlich Ordner-Verschlüsselung, Wiederherstellung, leeren Unterordnern, Abbruch, Quellordnerschutz und Explorer-Befehlsargumenten. Die zusätzlichen Konverter-Prüfergebnisse aus Version 0.3 gelten für die unveränderten Konverter.

1. Aufgabe auswählen.
2. Datei oder Ordner wählen; alternativ hineinziehen.
3. Die automatische Analyse abwarten: Dateityp, Größe, verfügbare Ausgabeformate und Einschränkungen werden angezeigt. Bei Bildern werden die Abmessungen geprüft, Office-Container und JSON/XML/CSV werden inhaltlich geprüft. Bei alten Office-Binärdateien wird der Container geprüft; die Zuordnung zu Word/Excel/PowerPoint kommt aus der Endung. Die vollständige Konvertierbarkeit kann erst der Konverter bestätigen.
4. Ein angebotenes Zielformat wählen. Das Programm schlägt einen neuen Ausgabepfad mit passender Endung vor. Mit „Ziel wählen“ lässt sich der Zielort ändern.
5. Den zur Aufgabe passenden Startknopf drücken. Anschließend kannst du die Ausgabe im Explorer anzeigen.

Die Oberfläche zeigt nur die zur Aufgabe passenden Einstellungen. Ordner, ZIPs und `.dwenc` werden bei der ersten Auswahl einer passenden Aufgabe zugeordnet, solange du noch keine Aufgabe selbst gewählt hast. Für nicht unterstützte Kombinationen ist der Konvertierungsstart deaktiviert; ZIP und Verschlüsselung stehen weiterhin zur Verfügung.

Originale bleiben erhalten. Vorhandene Ausgaben werden abgewiesen. Vorgänge laufen im Hintergrund und sind abbrechbar. Office-Aufrufe können nicht sicher mitten im Export beendet werden; Abbrechen greift nach dem laufenden Aufruf. Dateien werden zunächst in eine temporäre Datei neben der Ausgabe geschrieben. Bei normalem Fehler oder Abbruch wird diese entfernt. Nach Stromausfall oder Prozessabsturz können `.part`-Dateien oder Office-Zwischendateien zurückbleiben; beim Entschlüsseln enthalten diese Klartext.

## Unterstützte Konvertierungen

| Eingabe | Ausgabe | Hinweis |
| --- | --- | --- |
| PDF mit auslesbarem Text | DOCX, TXT, HTML | Integriert; alle Seiten. Word enthält bearbeitbaren Text mit Seitentrennung, übernimmt aber nicht das ursprüngliche Layout, Bilder oder Tabellenformatierung. |
| Einseitiges PDF, auch Scan | PNG, JPG, WebP | Integrierter PDF-Renderer, 150 DPI. |
| Mehrseitiges PDF, auch Scan | Alle Seiten als PNG, JPG oder WebP im ZIP | Keine Seite wird stillschweigend weggelassen. Ausgaben heißen z. B. `dokument-konvertiert.png.zip`. |
| DOC, DOCX, DOCM, RTF, ODT | PDF, DOCX, DOC, RTF, TXT, ODT | Benötigt Microsoft Word Desktop. TXT ist UTF-8 und verliert Layout. |
| XLS, XLSX, XLSM, XLSB, ODS | PDF, XLSX, XLS, CSV, ODS | Benötigt Microsoft Excel Desktop. CSV enthält das erste Arbeitsblatt als Werte; Trennzeichen nach Windows-Regionseinstellungen. |
| PPT, PPTX, PPTM, PPS, PPSX, ODP | PDF, PPTX, PPT, ODP | Benötigt Microsoft PowerPoint Desktop. |
| PNG, JPG/JPEG, WebP, BMP, GIF, TIFF/TIF, ICO | PNG, JPG, WebP, BMP, GIF, TIFF, PDF | Statische Einzelbilder; Animationen und mehrseitige Bilder werden abgewiesen. JPG verliert Transparenz (weißer Hintergrund) und ist verlustbehaftet. GIF reduziert Farben. PDF bettet das Bild auf einer A4-Seite ein. Metadaten können verloren gehen. |
| CSV, TSV | XLSX, JSON, CSV, TSV | CSV mit Komma; TSV mit Tabulator. XLSX und JSON übernehmen alle Zellen als Zeichenketten. XLSX-Konverter integriert, benötigt kein Excel. |
| JSON mit Zeichenketten-Zeilenarrays | XLSX, CSV, TSV | Keine beliebigen JSON-Objekte. |
| JSON | JSON | Prüfen und formatieren. |
| XML | XML | Prüfen und formatieren. |
| TXT, CSV, TSV, JSON, XML, MD, LOG | TXT | UTF-8-Textexport; keine inhaltliche Dokumentkonvertierung. |
| TXT, CSV, TSV, JSON, XML, MD, LOG | PDF, DOCX | Integrierter Dokumentexport; einfaches Textlayout. PDF erhält automatische Zeilenumbrüche und weitere Seiten bei längeren Texten. |
| TXT, CSV, TSV, JSON, XML | HTML | Text als sicher maskierte Vorformatierung. |
| Gängige Audio-/Videodateien | MP3, WAV, FLAC, OGG, MP4, MKV, WEBM | FFmpeg und FFprobe integriert. Videoformate nur bei vorhandener Videospur, Audioformate nur bei vorhandener Audiospur. Erste Video-/Audiospur; keine Untertitel oder Zusatzspuren. |

Office-Konvertierungen verwenden die installierten Microsoft-Desktop-Anwendungen über COM auf einem separaten STA-Thread. Die Registrierung der jeweiligen Anwendung wird geprüft; Aktivierung und tatsächliche Exportfähigkeit werden erst beim Export bestätigt. Microsoft Office wird nicht mitgeliefert. Word/Excel öffnen die Quelle schreibgeschützt; VBA-Makroausführung wird deaktiviert und Word-/Excel-Verknüpfungen werden nicht automatisch aktualisiert. Legacy-Makros, Schutzmechanismen oder Office-Dialoge können den Vorgang trotzdem verhindern. Makros können je nach Zielformat erhalten bleiben; dies ist keine Makrobereinigung. Formatwechsel können Layout, Funktionen und Metadaten verändern. Passwortgeschützte Office-Dateien benötigen vorheriges Entschlüsseln außerhalb dieses Programms. Enthält die verwendete Office-Instanz schon andere Dokumente, wird der Export abgewiesen, damit diese nicht geschlossen werden.

Textkonvertierung: maximal 50 MiB, UTF-8 mit optionalem BOM. Bilder: maximal 100 Megapixel. PDFs: maximal 2.000 Seiten, beim Rendern maximal 100 Megapixel pro Seite. Passwortgeschützte PDFs benötigen vorheriges Entschlüsseln außerhalb dieses Programms. Eine PDF ohne vorhandene Textebene bietet Bildexporte an, weil OCR noch nicht integriert ist. HEIC, SVG, RAR/7z-Archive und viele Spezialformate sind noch nicht integriert. Die Software unterstützt die hier aufgeführten Kombinationen; eine beliebige Umwandlung sämtlicher Formate ist nicht enthalten.

Audio-/Video-Eingaben werden anhand üblicher Dateiendungen erkannt und mit dem integrierten FFprobe auf vorhandene Ströme geprüft. Beschädigte Dateien werden bei der Analyse abgewiesen. Streamerkennung prüft nicht jedes Bild einer längeren Datei; spätere Decoderfehler sind trotzdem möglich. MP4/MKV verwenden H.264 und AAC, WebM VP9 und Opus, MP3 LAME, WAV PCM16, FLAC FLAC, OGG Vorbis. Diese Exporte sind keine Garantie verlustfreier Umwandlung.

Die integrierten Bibliotheken und Programme sind unter `Lizenzen/KOMPONENTEN.md` dokumentiert; ihre Lizenztexte liegen im Ordner `Lizenzen/`. FFmpeg 9.0.2 wurde als unveränderter, auf ffmpeg.org verlinkter Gyan-Windows-Build nach SHA256-Prüfung eingebettet. Die EXE enthält die benötigten PDF-/Bildbibliotheken und das komprimierte Medienpaket und ist deshalb größer als Version 0.2.

## ZIP

ZIP-Erstellung für einzelne Dateien und vollständige Ordner inklusive leerer Unterordner. Vier Stufen: Schnell (Standard), Ausgewogen, Kleinste Datei, Ohne Komprimierung. Streaming mit 1-MiB-Puffern, ohne gesamten Dateiinhalt im Arbeitsspeicher. Verknüpfungen innerhalb von Quellordnern werden abgewiesen. Das ZIP muss außerhalb des Quellordners liegen.

Entpacken normaler, unverschlüsselter ZIPs; höchstens 100.000 Einträge und 100 GiB deklarierte Gesamtausgabe. Pfade außerhalb des Zielordners werden abgewiesen. Es wird in einen neuen temporären Ordner entpackt und bei Erfolg umbenannt. Geschwindigkeit hängt von Datenträger, CPU und Komprimierungsgrad ab; kein Benchmark gegen WinRAR durchgeführt. Keine ZIP-Passwortverschlüsselung.

## AES-256-Verschlüsselung

AES-256-GCM mit 128-Bit-Authentifizierungstags, zufälligem 128-Bit-Salt und PBKDF2-HMAC-SHA-256 mit 600.000 Iterationen. Schlüsselgröße 256 Bit. Streaming in 1-MiB-Blöcken; Dateikopf und Blockposition sind authentifiziert. Auch leere Dateien erhalten einen Authentifizierungstag.

Mindestens 12 Passwortzeichen und Bestätigung sind erforderlich. Besser eine lange, zufällig erzeugte Passphrase wählen und sicher aufbewahren. Kein Passwort-Reset und kein Hintertürschlüssel. AES-256 bedeutet keine Garantie gegen schwache Passwörter, Malware oder kompromittierte Geräte. Die Originaldatei wird nicht gelöscht. Dateiname und unverschlüsselte Länge sind nicht geheim.

Die Ausgabe ist `.dwenc`, ein eigenes Dateiformat, **kein AES-ZIP / 7z und kein OpenSSL-enc-Format**. Zum Entschlüsseln gibt es die Oberfläche und das separate `entschluesseln.py`:

```powershell
py -m pip install cryptography
py entschluesseln.py beispiel.dwenc wiederhergestellt.pdf
```

Das Passwort wird interaktiv abgefragt, nicht als Kommandozeilenargument übergeben. Das ursprüngliche Format bleibt erhalten; beim Entschlüsseln die passende Dateiendung wählen.

### DWENC001-Format für andere Implementierungen

44-Byte-Header: ASCII `DWENC001` (8 Byte), Iterationen als Int32 Little Endian (4), Salt (16), zufälliges Nonce-Präfix (8), Klartextlänge als Int64 Little Endian (8). Akzeptierte Iterationen: exakt 600.000. Passwortkodierung UTF-8 ohne Unicode-Normalisierung.

Schlüssel: PBKDF2-SHA256(Passwort, Salt, 600000, 32 Byte). Je Block maximal 1.048.576 Byte Ciphertext, danach 16 Byte GCM-Tag. Letzter Block ergibt sich aus der Gesamtlänge. Für eine leere Datei gibt es einen leeren Block plus Tag. Nonce: 8-Byte-Präfix + UInt32 Big Endian Blockindex, ab 0. AAD: kompletter Header + derselbe 4-Byte-Blockindex. Zusätzliche Bytes und fehlende Blöcke werden abgewiesen. Maximal 2^32 Blöcke; die Implementierung weist das Erreichen des letzten Index konservativ ab.

## Quellcode / Weiterentwicklung

Quellcode unter `Quellcode/`. Benötigt .NET 10 SDK für Windows.

```powershell
dotnet publish Quellcode/Multitool.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o build
build/DateiWerk.exe --self-test
```

Der Selbsttest schreibt `Testergebnis.txt` neben die Anwendung. Er prüft Formatauswahl für JSON-Objekte und Zeilentabellen, ungültige Office-Container, Erkennung von Word-Containern und Office-Verfügbarkeit, ungültige Medien sowie AES-Roundtrips für leere und mehrblockige Dateien, falsche Passwörter, Manipulation, Abschneiden, ZIP-Roundtrip, leere Ordner, Pfadmanipulation, CSV, XLSX-Sonderzeichen, Bildkonvertierung, Überschreibschutz und Abbruch. Ergebnis: 49 Prüfungen bestanden.

`--office-test` erzeugt eigene einfache Testdokumente und prüft alle 15 angebotenen Office-Exporte; Ergebnis unter `Office-Testergebnis.txt`. Der Test verwendet keine bestehenden Nutzerdokumente. `--ui-test` rendert eine Oberfläche mit ausgewählter CSV-Beispieldatei als `Oberflaeche.png`; mit anschließendem Dateipfad kann stattdessen eine andere Datei analysiert werden. `--media-test <Testordner>` erzeugt synthetische Audio-/Videodateien und prüft alle sieben Ausgabeformate (18 Prüfungen). `--pdf-test <Testordner>` verwendet `reference.pdf` (zweiseitige Text-PDF) und `scan.pdf` (einseitig ohne Text), prüft alle PDF-Ausgabeformate, WebP → PDF und mehrseitigen Text → PDF (26 Prüfungen). `--document-test <Testordner>` prüft die DOCX-/XLSX-Exporte mit installiertem Word/Excel. Für eine spätere Veröffentlichung fehlen reguläre Codesignierung, Tests mit komplexen Dateien und ein Installer.
