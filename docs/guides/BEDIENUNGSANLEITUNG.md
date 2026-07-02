# Romulus — Bedienungsanleitung für alle

**Zielgruppe:** Einsteiger, technisch nicht versierte Personen, Sammler ohne
Programmierkenntnisse.
**Sprache:** Deutsch, ohne Fachjargon (Fachbegriffe werden erklärt).
**Stand:** 2026-05-18

Dieses Dokument erklärt Romulus von Grund auf: Was es ist, was es tut, was es
ausdrücklich **nicht** tut, wie man es installiert, einrichtet, benutzt — und
wie man im Fehlerfall sicher wieder zum ursprünglichen Zustand zurückkehrt.

> **Wichtigste Botschaft vorab:** Romulus löscht **nie** direkt. Jede Aktion
> ist umkehrbar (Rückgängig), solange Sie den Audit-Trail behalten.
> Sie können also nichts „kaputt machen", wenn Sie Schritt für Schritt
> vorgehen wie in dieser Anleitung beschrieben.

---

## Inhaltsverzeichnis

1. [Was ist Romulus?](#1-was-ist-romulus)
2. [Wofür ist Romulus **nicht** gedacht?](#2-wofür-ist-romulus-nicht-gedacht)
3. [Glossar — Begriffe einfach erklärt](#3-glossar--begriffe-einfach-erklärt)
4. [Systemvoraussetzungen](#4-systemvoraussetzungen)
5. [Installation](#5-installation)
6. [Der goldene Ablauf in fünf Schritten](#6-der-goldene-ablauf-in-fünf-schritten)
7. [Die Benutzeroberfläche (GUI) im Detail](#7-die-benutzeroberfläche-gui-im-detail)
8. [Die sechs Hauptaktionen](#8-die-sechs-hauptaktionen)
9. [Einstellungen — was sollte ich konfigurieren?](#9-einstellungen--was-sollte-ich-konfigurieren)
10. [Externe Werkzeuge einrichten (optional)](#10-externe-werkzeuge-einrichten-optional)
11. [DAT-Dateien — Verifikation einrichten (optional)](#11-dat-dateien--verifikation-einrichten-optional)
12. [Reports lesen und verstehen](#12-reports-lesen-und-verstehen)
13. [Rückgängig machen (Rollback)](#13-rückgängig-machen-rollback)
14. [Sicherheit — was Romulus für Sie schützt](#14-sicherheit--was-romulus-für-sie-schützt)
15. [Häufige Fragen (FAQ)](#15-häufige-fragen-faq)
16. [Fehlermeldungen verstehen und lösen](#16-fehlermeldungen-verstehen-und-lösen)
17. [Erste-Hilfe-Checkliste](#17-erste-hilfe-checkliste)
18. [Kommandozeile (CLI) — nur für Fortgeschrittene](#18-kommandozeile-cli--nur-für-fortgeschrittene)
19. [Lokale REST-API — nur für Entwickler](#19-lokale-rest-api--nur-für-entwickler)
20. [Wo finde ich was?](#20-wo-finde-ich-was)

---

## 1. Was ist Romulus?

Romulus ist ein **Aufräum- und Prüfwerkzeug für ROM-Sammlungen** unter Windows.

Konkret hilft Romulus dabei, eine über Jahre gewachsene, unsortierte Sammlung
von Spieldateien (ROMs, ISOs, CHDs, BIN/CUE …) in einen **sauberen,
nachprüfbaren Zustand** zu bringen:

- **Erkennen**, zu welcher Konsole jede Datei gehört (NES, SNES, PS1, …).
- **Sortieren** in Ordner pro Konsole.
- **Doubletten** finden (z. B. dieselbe Region in mehreren Formaten) und das
  beste Exemplar behalten.
- **Junk-Dateien** finden (Bilder, Anleitungen, Werbung, kaputte Archive).
- **Hashes** (Prüfsummen) gegen anerkannte Datenbanken wie No-Intro oder
  Redump abgleichen, um Echtheit und Vollständigkeit zu prüfen.
- **Optional umwandeln** (z. B. PS1 BIN/CUE → CHD).
- **Alles protokollieren** und **rückgängig machbar** halten.

Romulus arbeitet **deterministisch**. Das heißt: Gleiche Eingaben erzeugen
immer dieselben Entscheidungen. Es gibt keine zufälligen Ergebnisse.

---

## 2. Wofür ist Romulus **nicht** gedacht?

Romulus ist **kein**:

- **Frontend / Launcher** (kein Ersatz für LaunchBox, RetroArch, ES-DE, Playnite …).
- **Scraper** für Cover, Beschreibungen oder Metadaten.
- **Patch-Werkzeug** (kein IPS-/BPS-/xdelta-Patching).
- **MAME-Set-Builder** (split/merge/non-merged).
- **Emulator** — Romulus spielt keine Spiele ab.
- **Cloud-Dienst** — alles läuft ausschließlich lokal auf Ihrem PC.
- **Mehrbenutzer-Software** — Romulus ist für eine Person an einem PC gedacht.

Wenn Sie eines dieser Themen brauchen, sind dedizierte Werkzeuge besser
geeignet (z. B. RomM, Igir, LaunchBox).

---

## 3. Glossar — Begriffe einfach erklärt

| Begriff | Erklärung |
|---|---|
| **ROM** | Eine Spieldatei (z. B. `Super Mario.nes`). Ursprünglich von Modulen oder Discs eingelesen. |
| **ISO / BIN / CUE / CHD / RVZ** | Verschiedene Dateiformate für CD-/DVD-basierte Spiele. CHD und RVZ sind komprimiert. |
| **Konsole** | Die Spielhardware, zu der eine Datei gehört (NES, SNES, PS1, GameCube, …). |
| **Region** | Aus welchem Markt das Spiel stammt: EU (Europa), US (USA), JP (Japan). Steht meist im Dateinamen in Klammern. |
| **Hash / Prüfsumme** | Ein eindeutiger digitaler „Fingerabdruck" einer Datei. Zwei identische Dateien haben denselben Hash. Verfahren: SHA-1, SHA-256, MD5. |
| **DAT-Datei** | Eine Liste von offiziellen Spielen mit ihren Hashes (z. B. von No-Intro oder Redump). Damit kann Romulus prüfen, ob Ihre Datei „echt" und unverändert ist. |
| **Junk** | Datei, die nicht in eine saubere Sammlung gehört (Cover-Bilder, Anleitungen, leere Archive, Werbung). |
| **Doublette / Duplikat** | Dasselbe Spiel mehrfach vorhanden (z. B. EU- und US-Version, oder als ZIP **und** als 7z). |
| **Winner-Selection** | Romulus' Entscheidung, welches Exemplar bei Doubletten behalten wird. |
| **Trash** | Ein „Papierkorb"-Ordner. Romulus verschiebt Junk und Verlierer dorthin, **statt zu löschen**. |
| **DryRun (Vorschau)** | Probelauf — Romulus zeigt, was passieren **würde**, ohne etwas anzufassen. |
| **Move (Ausführen)** | Echter Lauf — Dateien werden tatsächlich verschoben. |
| **Audit-Trail** | Lückenlose Protokoll-Datei (CSV) jeder Bewegung, kryptografisch signiert. Grundlage für Rückgängig. |
| **Rollback** | Vergangenen Lauf rückgängig machen. Setzt alle Dateien an ihre Ursprungsorte zurück. |
| **Preview / Plan** | Vorschau der geplanten Aktionen ohne Ausführung. |
| **Root / Roots** | Die Hauptordner, in denen Ihre ROM-Sammlung liegt. Romulus arbeitet **nur** innerhalb dieser Roots. |
| **CLI** | Kommandozeile — Bedienung über Texteingaben statt Mausklicks. Optional. |
| **API** | Programmierschnittstelle für eigene Skripte. Optional, nur für Entwickler. |

---

## 4. Systemvoraussetzungen

- **Betriebssystem:** Windows 10 oder Windows 11 (64 Bit).
- **.NET 10 SDK** (kostenlos von Microsoft).
  → Download: <https://dotnet.microsoft.com/>
- **Festplattenplatz:** Mindestens so viel freier Platz wie Ihre ROM-Sammlung
  groß ist (für Trash-Ordner und Reports).
- **Optional, aber empfohlen:**
  - **chdman** — für CHD-Konvertierung (PS1, PS2, Saturn, Dreamcast).
  - **DolphinTool** — für RVZ-Konvertierung (GameCube, Wii).
  - **7-Zip** — für Archive (ZIP, 7z, RAR).
- **Optional:** DAT-Dateien (z. B. von No-Intro oder Redump) für die
  Hash-Verifizierung.

> Romulus läuft komplett offline. Eine Internetverbindung wird nur einmalig
> für die Installation und für optionale DAT-Updates benötigt.

---

## 5. Installation

### 5.1 .NET 10 SDK installieren

1. Öffnen Sie <https://dotnet.microsoft.com/> in Ihrem Browser.
2. Laden Sie das **.NET 10 SDK** für Windows herunter und installieren Sie es.
3. Öffnen Sie danach die Eingabeaufforderung (Windows-Taste → `cmd`) und tippen:
   ```bash
   dotnet --version
   ```
   Wenn eine Versionsnummer (z. B. `10.0.100`) erscheint, war die Installation
   erfolgreich.

### 5.2 Romulus starten (GUI)

Wechseln Sie im Terminal in den Romulus-Ordner und starten Sie die grafische
Oberfläche:

```bash
dotnet run --project src/Romulus.UI.Wpf
```

Nach kurzer Zeit öffnet sich das Hauptfenster.

> **Tipp:** Wenn Sie eine fertige `.exe` haben, reicht ein Doppelklick.

---

## 6. Der goldene Ablauf in fünf Schritten

So gehen Sie immer vor, egal wie groß Ihre Sammlung ist:

```
1. Ordner auswählen   →  2. Vorschau (DryRun)   →  3. Bericht prüfen
                                                          │
                                                          ▼
5. Falls nötig: Rückgängig  ←  4. Ausführen (Move) + Bestätigen
```

1. **Ordner auswählen** — Sagen Sie Romulus, **wo** Ihre ROMs liegen.
2. **Vorschau (DryRun)** — Romulus rechnet durch, was es **tun würde**, ohne
   etwas anzufassen. Dauer: je nach Sammlungsgröße Sekunden bis Stunden.
3. **Bericht prüfen** — Romulus öffnet einen HTML-Bericht im Browser. Sie
   sehen genau: Was wäre Gewinner, was wäre Junk, was wäre Doublette.
4. **Ausführen (Move)** — Erst wenn Sie zufrieden sind, starten Sie den
   echten Lauf. Sie müssen vorher in einem Dialog **bestätigen**.
5. **Rückgängig (Rollback)** — Sollte das Ergebnis nicht passen, machen Sie
   den gesamten Lauf mit einem Klick rückgängig.

> **Regel Nummer 1:** Machen Sie **immer zuerst eine Vorschau (DryRun)**.
> Auch bei kleinen Sammlungen. Auch wenn Sie meinen, alles im Griff zu haben.

---

## 7. Die Benutzeroberfläche (GUI) im Detail

Die grafische Oberfläche ist in Tabs (Reiter) unterteilt. Jeder Tab hat einen
klaren Zweck.

### 7.1 Tab „Sortieren" (Start)

Das ist Ihr Haupt-Arbeitsplatz.

| Element | Bedeutung |
|---|---|
| **Roots** (Liste der Quell-Ordner) | Ordner, in denen Ihre ROMs liegen. Per Drag & Drop oder Button hinzufügen. |
| **Trash-Pfad** | Ordner, in den Junk und Verlierer verschoben werden. Wird automatisch erstellt. |
| **Modus** | **DryRun** (Vorschau, nichts wird angefasst) oder **Move** (echte Aktion). |
| **Sortierung starten** | Hauptknopf. Startet den ausgewählten Lauf. |
| **Cancel** | Bricht einen laufenden Lauf jederzeit sauber ab. |
| **Fortschrittsbalken + ETA** | Zeigt aktuelle Phase und geschätzte Restzeit. |

### 7.2 Tab „Konfiguration"

Hier stellen Sie einmalig Ihre Vorlieben ein:

- **Bevorzugte Regionen** (z. B. EU, US, JP — in dieser Reihenfolge).
- **DAT-Verzeichnis** (falls Sie No-Intro/Redump-DATs nutzen).
- **Pfade zu externen Werkzeugen** (chdman, dolphintool, 7z).
- **Theme** (helles oder dunkles Erscheinungsbild).
- **Profile** (Voreinstellungs-Sets für unterschiedliche Anwendungsfälle).

### 7.3 Tab „Log & Dashboard"

Live-Ansicht des aktuellen Laufs: Zeitstrahl, Statistiken,
Konsolen-Verteilung.

### 7.4 Tab „Reports"

Alle bisherigen HTML-/CSV-Berichte. Doppelklick öffnet den Bericht im Browser.

### 7.5 Tab „Audit"

Liste aller vergangenen Läufe. Hier starten Sie auch das **Rollback**.

### 7.6 Tab „Wizard" (Einsteiger-Assistent)

Schritt-für-Schritt-Assistent für den allerersten Lauf. Empfohlen für neue
Nutzer.

### 7.7 Zwei Bedienmodi

In den Einstellungen können Sie zwischen zwei Modi wechseln:

- **Einfach** — nur die wichtigsten Optionen. Empfohlen für den Anfang.
- **Experte** — alle Optionen sichtbar. Für erfahrene Nutzer.

---

## 8. Die sechs Hauptaktionen

Egal ob GUI oder Kommandozeile — Romulus bietet genau diese sechs Aktionen:

### 8.1 Scan — Einlesen und klassifizieren

Liest Ihre Ordner ein, erkennt Dateitypen, ordnet jede Datei einer Konsole zu.
**Ändert nichts.**

### 8.2 Verify — Prüfsummen berechnen und gegen DAT abgleichen

Berechnet Hashes (Prüfsummen) und vergleicht sie mit den DAT-Dateien.
Ergebnis: Echtheit und Vollständigkeit jeder Datei. **Ändert nichts.**

### 8.3 Plan — Doubletten und Junk finden (Vorschau)

Romulus entscheidet:
- Welche Doubletten gibt es?
- Welches Exemplar pro Spiel ist der „Winner"?
- Welche Dateien sind Junk?
- Welche Dateien sind unbekannt?

**Ändert nichts.** Das Ergebnis sehen Sie im HTML-Bericht.

### 8.4 Move — Plan ausführen

Verschiebt:
- Junk in den Trash-Ordner.
- Doubletten-Verlierer in den Trash-Ordner.
- Gewinner in saubere Konsolen-Unterordner.

Schreibt **gleichzeitig** das Audit-Protokoll (CSV + Signatur).
Dateien werden **nie direkt gelöscht** — nur verschoben.

### 8.5 Convert — Format-Konvertierung (optional)

Konvertiert in platzsparende Formate:

| Quelle | Ziel | Werkzeug |
|---|---|---|
| PS1 BIN/CUE, Saturn, Dreamcast | CHD | chdman |
| PS2 ISO | CHD | chdman |
| GameCube/Wii ISO | RVZ | DolphinTool |

**Wichtig:** Manche Konvertierungen sind **verlustbehaftet** (lossy). Romulus
markiert diese Pfade explizit und fragt vor der Ausführung noch einmal nach.

### 8.6 Rollback — Vergangenen Lauf rückgängig machen

Liest die Audit-CSV eines früheren Laufs und stellt **alle** Verschiebungen
in genau der umgekehrten Reihenfolge wieder her.

---

## 9. Einstellungen — was sollte ich konfigurieren?

Die Einstellungen liegen unter:

```
%APPDATA%\Romulus\settings.json
```

(Das öffnet sich mit Windows-Taste + R → `%APPDATA%\Romulus` eingeben.)

Sie können fast alles direkt in der GUI im Tab **Konfiguration** ändern. Die
Datei wird automatisch aktualisiert.

### 9.1 Bevorzugte Regionen

Beispiel: `["EU", "US", "JP"]` — Romulus bevorzugt erst die EU-Version, dann
die US-Version, dann die japanische.

### 9.2 Aggressive Junk-Erkennung

- **Aus** (Standard): Romulus markiert nur eindeutigen Müll als Junk.
- **An**: Romulus markiert mehr Grenzfälle als Junk. Achtung — bitte erst im
  DryRun prüfen, ob die Auswahl Ihren Vorstellungen entspricht.

### 9.3 Log-Level

- **Info** (Standard): Normale Meldungen.
- **Debug**: Sehr ausführlich, nur bei Fehlersuche aktivieren.

---

## 10. Externe Werkzeuge einrichten (optional)

Romulus braucht externe Programme nur, wenn Sie **konvertieren** oder
**Archive verarbeiten** wollen.

| Werkzeug | Brauche ich es? | Download |
|---|---|---|
| **chdman** | Nur für CHD-Konvertierung. | MAME-Distribution |
| **DolphinTool** | Nur für RVZ (GameCube/Wii). | Dolphin Emulator |
| **7-Zip** | Wenn Sie ZIP/7z/RAR-Archive haben. | <https://www.7-zip.org/> |

**So tragen Sie die Pfade ein:**

1. Tab **Konfiguration** öffnen.
2. Bei jedem Werkzeug auf **Durchsuchen** klicken.
3. Die `.exe`-Datei auswählen.
4. Speichern.

> **Sicherheits-Hinweis:** Romulus prüft jedes externe Werkzeug vor der
> Ausführung gegen eine SHA-256-Liste bekannter, sicherer Versionen
> (`data/tool-hashes.json`). Sind Sie nicht die offizielle Version, wird das
> Werkzeug abgelehnt. Das schützt Sie vor manipulierten Programmen.

---

## 11. DAT-Dateien — Verifikation einrichten (optional)

DAT-Dateien sind XML-Listen, die für jedes offizielle Spiel den
korrekten Hash enthalten. Mit ihnen kann Romulus prüfen, ob Ihre Dateien
**echt und unverändert** sind.

### 11.1 Wo bekomme ich DATs?

- **No-Intro** — für Modul-basierte Konsolen (NES, SNES, GBA, NDS …).
- **Redump** — für Disc-basierte Konsolen (PS1, PS2, GameCube …).

Beide Anbieter stellen DAT-Dateien kostenlos zur Verfügung. Laden Sie die
gewünschten Konsolen herunter und legen Sie alle DAT-Dateien in **einen**
Ordner.

### 11.2 In Romulus konfigurieren

1. Tab **Konfiguration** öffnen.
2. Bei **DAT-Verzeichnis** den Ordner auswählen.
3. **Hash-Typ** wählen (`SHA1` ist der Standard und passt für die meisten DATs).
4. **DAT-Verifikation aktivieren** ankreuzen.

Beim nächsten Lauf prüft Romulus jede Datei und meldet:
- ✅ **Verified** — Datei stimmt mit DAT überein.
- ⚠️ **Mismatch** — Datei kennt der DAT, aber Hash stimmt nicht (verändert / kaputt).
- ❓ **Unknown** — Datei ist nicht im DAT.

---

## 12. Reports lesen und verstehen

Nach jedem Lauf öffnet Romulus automatisch einen HTML-Bericht im Browser.

### 12.1 Was steht drin?

- **Zusammenfassung oben:** Anzahl gefundener Dateien, erkannte Konsolen,
  Doubletten, Junk, Unbekannte.
- **Konsolen-Verteilung:** Wie viele Spiele pro Konsole.
- **Aktions-Liste:** Was Romulus tun würde / getan hat.
- **Junk-Liste:** Welche Dateien als Müll markiert sind und **warum**.
- **Unbekannt-Liste:** Welche Dateien nicht zugeordnet werden konnten.

### 12.2 Wo liegt der Bericht?

```
reports/rom-cleanup-report-JJJJMMTT-HHMMSS.html
reports/rom-cleanup-report-JJJJMMTT-HHMMSS.csv
reports/rom-cleanup-report-JJJJMMTT-HHMMSS.json
```

Sie können den Bericht auch später jederzeit über den Tab **Reports** öffnen.

### 12.3 Audit-CSV — das Sicherheitsnetz

Zusätzlich entsteht eine **signierte** Audit-CSV. Diese ist die Grundlage für
das Rückgängig. **Bitte nicht löschen, nicht verändern.**

```
audit-logs/run-JJJJMMTT-HHMMSS/audit.csv
audit-logs/run-JJJJMMTT-HHMMSS/audit.csv.sig   ← Signatur
```

---

## 13. Rückgängig machen (Rollback)

Sie haben einen Lauf ausgeführt und das Ergebnis gefällt Ihnen nicht? Kein
Problem.

### 13.1 In der GUI

1. Tab **Audit** öffnen.
2. Den gewünschten Lauf auswählen (Datum und Uhrzeit hilft).
3. Auf **Rollback** klicken.
4. Im Bestätigungsdialog auf **Ja** klicken.
5. Romulus stellt alle Dateien an ihren ursprünglichen Ort zurück.

### 13.2 Voraussetzungen

- Audit-CSV und Signatur müssen noch vorhanden sein (`audit-logs/…`).
- Die verschobenen Dateien müssen am Zielort noch existieren (also: nicht
  manuell aus dem Trash gelöscht).

### 13.3 Was Rollback **nicht** macht

- **Keine Konvertierung rückgängig**: Wenn Sie BIN/CUE → CHD konvertiert haben
  und die Quell-Dateien danach manuell gelöscht haben, kann Romulus die
  ursprünglichen BIN/CUE nicht zurückzaubern. Deshalb fasst Romulus
  Quell-Dateien nach Konvertierung **nicht** an, bis Sie ausdrücklich
  zustimmen.

---

## 14. Sicherheit — was Romulus für Sie schützt

Romulus ist defensiv gebaut. Folgende Schutzmaßnahmen sind **immer** aktiv
und können nicht abgeschaltet werden:

| Schutz | Bedeutung |
|---|---|
| **Kein direktes Löschen** | Dateien gehen immer in den Trash, nie ins Nirgendwo. |
| **Root-Bindung** | Romulus verschiebt **nur** innerhalb der von Ihnen definierten Roots. Pfade außerhalb sind blockiert. |
| **Reparse-Point-Blocker** | Symlinks/Junctions werden nicht blind verfolgt. |
| **Zip-Slip-Schutz** | Beim Entpacken von Archiven werden manipulierte Pfade blockiert. |
| **CSV-Injection-Schutz** | Reports erzeugen keine schädlichen Formeln in Excel. |
| **Tool-Hash-Allowlist** | Externe Werkzeuge werden vor Ausführung verifiziert. |
| **XXE-Schutz** | XML-Parser für DAT-Dateien ist gegen Angriffe gehärtet. |
| **HTML-Encoding** | Berichte können keine eingeschleusten Skripte ausführen. |
| **Signierter Audit-Trail** | Manipulation am Protokoll wird beim Rollback erkannt. |
| **API nur 127.0.0.1** | Die REST-API ist nur lokal erreichbar, niemals im Netzwerk. |

---

## 15. Häufige Fragen (FAQ)

**F: Kann ich Romulus auf meinen Live-Ordner loslassen?**
A: Ja — aber **immer zuerst** mit DryRun. Erst nach Sichtung des Berichts mit
Move. Solange der Audit erhalten bleibt, ist alles umkehrbar.

**F: Was passiert mit Dateien, die Romulus nicht erkennt?**
A: Sie bleiben unangetastet und landen in der „Unknown"-Liste des Berichts.
Es gibt ein eigenes Dokument dazu: [UNKNOWN_FAQ.md](UNKNOWN_FAQ.md).

**F: Romulus erkennt eine Konsole falsch. Was tun?**
A: Notieren Sie den Dateinamen und die erkannte Konsole, prüfen Sie, ob die
Datei in einem irreführenden Ordner liegt (z. B. Datei vom NES im
„SNES"-Ordner). Bei systematischen Fehlern kann die Erkennung über die
Datendateien unter `data/` (z. B. `console-maps.json`) angepasst werden.

**F: Wo werden Junk-Dateien hingelegt?**
A: In den Trash-Ordner, den Sie in den Einstellungen definiert haben.
Standard: `Trash` unterhalb des Roots. Die Dateien sind dort **nicht
gelöscht** — Sie können sie jederzeit anschauen oder zurückholen.

**F: Wie viel Platz brauche ich zusätzlich?**
A: Im schlimmsten Fall so viel wie die Sammlung selbst (wenn alles in den
Trash müsste). In der Praxis viel weniger. Berichte und Audit-Logs sind
klein (wenige MB).

**F: Funktioniert Romulus mit RetroArch / LaunchBox / ES-DE?**
A: Indirekt ja — Romulus sortiert Ihre Sammlung. Ob Sie sie danach in einem
Frontend benutzen, ist Ihre Entscheidung. Romulus selbst exportiert **keine**
Frontend-Konfigurationen.

**F: Sind meine Daten sicher? Wird etwas hochgeladen?**
A: Nein. Romulus arbeitet vollständig offline. Es gibt keine Telemetrie,
keine Cloud, keine externen Aufrufe (außer den Werkzeugen, die Sie selbst
konfigurieren).

**F: Warum gibt es eine „experimental"-API?**
A: Die REST-API ist eine optionale Schnittstelle für Entwickler. Sie ist als
„experimental" markiert, weil sich Details noch ändern können. Für normale
Nutzung über die GUI ist das irrelevant.

---

## 16. Fehlermeldungen verstehen und lösen

| Fehler | Bedeutung | Lösung |
|---|---|---|
| **„Tool not found"** | Externes Werkzeug-Pfad fehlt oder ist falsch. | Tab **Konfiguration** → Pfad korrigieren. |
| **„Tool hash mismatch"** | Werkzeug stimmt nicht mit Allowlist überein. | Offizielle Version verwenden oder Tool nicht nutzen. |
| **„DAT not found"** | DAT-Verzeichnis ist leer oder falsch konfiguriert. | DAT-Pfad in Konfiguration prüfen. |
| **„Path outside root"** | Versuch, außerhalb der Roots zu verschieben. | Roots korrekt setzen; keine Symlinks im Root. |
| **„Reparse point blocked"** | Symlink/Junction im Pfad gefunden. | Symlink auflösen oder echten Pfad verwenden. |
| **„Preflight failed"** | Vor dem Lauf wurde eine Voraussetzung nicht erfüllt. | Bericht im Terminal/GUI lesen — meist fehlende Pfade. |
| **„Cancelled"** | Sie haben den Lauf manuell abgebrochen. | Kein Fehler — Audit ist trotzdem konsistent. |
| **„Rollback verification failed"** | Audit-Signatur stimmt nicht. | Audit-CSV wurde verändert. Keine automatische Wiederherstellung mehr möglich. |

### CLI-Exit-Codes (für Skripte)

| Code | Bedeutung |
|---|---|
| `0` | Erfolg |
| `1` | Fehler |
| `2` | Abgebrochen |
| `3` | Preflight fehlgeschlagen |

---

## 17. Erste-Hilfe-Checkliste

Bevor Sie Romulus zum allerersten Mal benutzen:

- [ ] .NET 10 SDK installiert (`dotnet --version` zeigt eine Version).
- [ ] Romulus startet ohne Fehler (`dotnet run --project src/Romulus.UI.Wpf`).
- [ ] Sammlung **gesichert** (externes Backup auf zweite Festplatte).
- [ ] Roots in der GUI hinzugefügt.
- [ ] Trash-Pfad gesetzt und Platz dafür frei.
- [ ] Bevorzugte Regionen eingestellt.
- [ ] **Erster Lauf im DryRun** — Bericht angeschaut.
- [ ] **Erst dann** Move ausgeführt.
- [ ] Audit-Ordner gesichert (für späteres Rollback).

> **Backup-Empfehlung:** Auch wenn Romulus nichts löscht — vor dem allerersten
> echten Lauf bitte eine vollständige Sicherheitskopie Ihrer Sammlung anlegen.
> Das ist gute Praxis bei jeder Massen-Operation.

---

## 18. Kommandozeile (CLI) — nur für Fortgeschrittene

Wenn Sie automatisieren möchten (z. B. nächtliche Aufräum-Läufe), bietet
Romulus eine vollständige Kommandozeile.

### Beispiele

```bash
# Reine Vorschau
dotnet run --project src/Romulus.CLI -- --roots "D:\Roms" --mode DryRun

# Echter Lauf mit Region-Bevorzugung
dotnet run --project src/Romulus.CLI -- --roots "D:\Roms" --mode Move --regions EU,US

# Mehrere Roots (mit Semikolon trennen)
dotnet run --project src/Romulus.CLI -- --roots "D:\Roms;E:\Backups" --mode DryRun
```

### Wichtige Schalter

| Schalter | Bedeutung |
|---|---|
| `--roots <pfade>` | Quell-Ordner (mehrere mit `;`). |
| `--mode DryRun\|Move` | Vorschau oder Ausführung. |
| `--regions EU,US,JP` | Bevorzugte Regionen (Komma-getrennt). |
| `--trashroot <pfad>` | Trash-Ordner. |
| `--gamesonly` | Nur Spiele behalten, BIOS/Sonst aussortieren. |
| `--keepunknown` / `--dropunknown` | Unbekannte behalten oder verwerfen. |
| `--aggressivejunk` | Strengere Junk-Erkennung. |
| `--sortconsole` | In Konsolen-Unterordner sortieren. |
| `--conflictpolicy Rename\|Skip\|Overwrite` | Verhalten bei Namens-Konflikten. |
| `--convertonly` | Nur Konvertierung, keine Sortierung. |

Eine vollständige Liste erhalten Sie mit:

```bash
dotnet run --project src/Romulus.CLI -- --help
```

### Subkommandos

Neben dem Standard-Lauf gibt es spezialisierte Subkommandos: `analyze`,
`simulate`, `explain`, `provenance`, `validate-policy`, `dat-diff`, `dat-fix`,
`junk-report`, `completeness` u. a. Details siehe `--help` und das
technische Handbuch [USER_HANDBOOK.md](USER_HANDBOOK.md).

---

## 19. Lokale REST-API — nur für Entwickler

Romulus bietet eine lokale REST-API für eigene Skripte und Integrationen.
**Sie ist niemals im Netzwerk erreichbar**, sondern bindet ausschließlich an
`127.0.0.1:7878`.

### Starten

```bash
set ROM_CLEANUP_API_KEY=mein-geheimer-key
dotnet run --project src/Romulus.Api
```

### Authentifizierung

Jede Anfrage braucht den Header `X-Api-Key` mit dem oben gesetzten Wert.

### Wichtige Endpunkte

Alle Routen liegen unter `/v1-experimental/`. Vollständige Liste siehe
[README.md](../../README.md#rest-api-lokal) oder
[USER_HANDBOOK.md](USER_HANDBOOK.md).

| Methode | Pfad | Zweck |
|---|---|---|
| `GET` | `/v1-experimental/health` | Lebt der Dienst? |
| `POST` | `/v1-experimental/runs` | Neuen Lauf starten |
| `GET` | `/v1-experimental/runs/{id}` | Status abfragen |
| `GET` | `/v1-experimental/runs/{id}/stream` | Live-Fortschritt (SSE) |
| `POST` | `/v1-experimental/runs/{id}/cancel` | Abbrechen |

Schutzmaßnahmen: Rate Limit (120 Anfragen/Minute), Body-Limit 1 MB,
strikte CORS-Regel.

---

## 20. Wo finde ich was?

| Was | Wo |
|---|---|
| **Einstellungen** | `%APPDATA%\Romulus\settings.json` |
| **Berichte (HTML/CSV/JSON)** | `reports/` |
| **Audit-Logs (für Rollback)** | `audit-logs/` |
| **Datenquellen (Konsolen-Liste, Regeln)** | `data/` |
| **Diese Anleitung** | `docs/guides/BEDIENUNGSANLEITUNG.md` |
| **Technisches Handbuch** | `docs/guides/USER_HANDBOOK.md` |
| **FAQ zu unerkannten Dateien** | `docs/guides/UNKNOWN_FAQ.md` |
| **README (Projektüberblick)** | `README.md` |

---

## Letzte Worte

Romulus ist mit einem klaren Versprechen gebaut: **Sicher aufräumen,
nachvollziehbar prüfen, jederzeit rückgängig machen.** Wenn Sie sich an den
goldenen Ablauf halten — Vorschau, prüfen, ausführen, sichern — kann nichts
verloren gehen.

Bei Unklarheiten:

1. **Lesen Sie zuerst den HTML-Bericht** — er erklärt, was Romulus tun
   wollte oder getan hat.
2. **Brechen Sie im Zweifel ab** — Cancel ist immer sicher.
3. **Im Fehlerfall: Rollback** — solange der Audit-Ordner intakt ist.

Viel Erfolg beim Aufräumen.
