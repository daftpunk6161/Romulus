# ROM Cleanup & Region Dedupe

> PowerShell 5.1+ WPF-Tool zum Aufräumen, Deduplizieren und Konvertieren von ROM-Sammlungen. Drei Entry Points: **GUI** (WPF/XAML), **CLI** (headless/CI), **REST API** (loopback).

---

## Kern-Features

| Feature | Beschreibung |
|---|---|
| **Region Dedupe** | Behält pro Spiel die beste ROM-Variante (Region, Version, Format, Größe). Priorität konfigurierbar (z.B. EU > US > JP). |
| **Junk-Entfernung** | Entfernt Demos, Betas, Protos, Software, Bad Dumps, Trainer, Hacks etc. automatisch. |
| **BIOS-Trennung** | Sortiert BIOS/Firmware-Dateien optional in separaten Ordner. |
| **Format-Konvertierung** | CUE/BIN → CHD, ISO → CHD/RVZ, CSO → ISO, PBP → CHD, NKit → ISO, ECM → BIN. Parallele Konvertierung mit Runspace-Pool und Pause/Resume-Queue. |
| **DAT-Matching** | Verifiziert ROMs gegen No-Intro, Redump, FBNEO, TOSEC DATs (SHA1/MD5/CRC32). Auto-Update mit Diff-Anzeige. |
| **1G1R-Modus** | One Game One ROM via Parent/Clone-Listen aus DAT-Dateien. |
| **Konsolen-Sortierung** | Sortiert Dateien automatisch nach 100+ Konsolen (Erkennung via Extension, Ordnername, Disc-Header, DAT). |
| **Audit & Rollback** | Signierte Audit-CSVs mit vollständigem Rollback-Wizard. |
| **DryRun** | Vorschau aller Aktionen ohne Dateien zu verschieben — CSV/HTML/JSON/PDF Reports. |
| **First-Start Wizard** | Geführter 3-Schritt-Wizard (ISS-001) für Ersteinrichtung mit Preflight-Check und Intent-Auswahl. |

## 76 Feature-Module (Phase 1–4)

Alle Features sind im **Features-Tab** der GUI über kategorisierte Buttons erreichbar:

| Kategorie | Features |
|---|---|
| **Analyse & Berichte** | Speicherplatz-Prognose, Junk-Report, ROM-Filter, Duplikat-Heatmap, Missing-ROM-Tracker, Cross-Root-Duplikat-Finder, Header-Analyse, Completeness-Tracker, DryRun-Vergleich, Trendanalyse, Emulator-Kompatibilitäts-Report |
| **Konvertierung & Hashing** | CSO/ZSO→CHD-Pipeline, NKit→ISO, Konvertierungs-Queue (Pause/Resume), Batch-Verify, Format-Prioritätsliste, Parallel-Hashing, GPU-Hashing |
| **DAT & Verifizierung** | DAT-Auto-Update, DAT-Diff-Viewer, TOSEC-Support, Custom-DAT-Editor, Hash-Datenbank-Export |
| **Sammlungsverwaltung** | Smart-Collections, Clone-List-Viewer, Cover-Scraping, Genre-Klassifikation, Spielzeit-Tracking, Sammlungs-Sharing, Virtuelle Ordner-Vorschau |
| **Sicherheit & Integrität** | Integritäts-Monitor, Backup-Manager, ROM-Quarantäne, Rule-Engine, Patch-Engine (IPS/BPS/UPS), Header-Reparatur |
| **Workflow & Automation** | Command-Palette (Ctrl+Shift+P), Split-Panel-Vorschau, Filter-Builder, Ordnerstruktur-Vorlagen, Pipeline-Engine, System-Tray, Scheduler, Rule-Pack-Sharing, Arcade Merge/Split |
| **Export & Integration** | PDF-Report, Emulator-Launcher-Integration (RetroArch/LaunchBox/EmulationStation), clrmamepro/RomVault-Import, M3U-Auto-Generierung, RetroArch-Playlist-Export, CSV-Export, CLI-Command-Export, Webhook (Discord/Slack) |
| **Infrastruktur** | Storage-Tiering, NAS-Optimierung, FTP/SFTP-Source, Cloud-Settings-Sync, Docker-Container, Mobile-Web-UI, Windows-Context-Menu, PSGallery-Modul, Winget/Scoop-Paket, Hardlink-Modus, USN-Journal-Scan, Multi-Instance-Sync, Telemetrie |
| **UI & Erscheinungsbild** | Barrierefreiheit (Screen-Reader, High-Contrast), Theme-Engine (Custom Themes), Dark/Light-Toggle, Keyboard-Shortcuts |

---

## Systemanforderungen

- **Windows** 10/11 (oder Windows Server 2016+)
- **PowerShell 5.1** (vorinstalliert auf Windows 10+)
- **.NET Framework 4.5+** (für WPF)
- **Optionale Tools** (für Konvertierung):
  - [chdman](https://www.mamedev.org/) — CHD-Konvertierung
  - [DolphinTool](https://dolphin-emu.org/) — RVZ-Konvertierung
  - [7-Zip](https://www.7-zip.org/) — Archiv-Handling
  - [psxtract](https://github.com/Starter-01/psxtract) — PBP → ISO
  - [ciso (Source)](https://github.com/jamie/ciso) — CSO → ISO (GitHub-Releases liefern i.d.R. nur Quellcode; für Windows eigenes ciso.exe bereitstellen/kompilieren)

---

## Quick Start

```powershell
# Rechtsklick → "Mit PowerShell ausführen" oder:
powershell -ExecutionPolicy Bypass -File .\simple_sort.ps1
```

Beim **ersten Start** öffnet sich automatisch der **First-Start Wizard** (ISS-001):
1. **Intent wählen** — Was möchtest du tun? (Aufräumen, Sortieren, Konvertieren)
2. **Grundeinstellungen** — Region-Priorität, DAT-Ordner, Tool-Pfade
3. **Preflight-Check** — Ampel zeigt ob Ordner lesbar, Tools verfügbar, DATs gefunden

Danach im Hauptfenster:
1. **ROM-Ordner hinzufügen** — Drag & Drop, `Ordner hinzufügen`-Button oder `Ctrl+V`.
2. **Modus wählen** — `Nur prüfen` (DryRun) für Vorschau, `Verschieben` für echte Änderungen.
3. **Dedupe starten** — Erster Lauf immer als DryRun empfohlen.
4. **Report prüfen** — HTML/CSV/PDF-Report zeigt alle geplanten/durchgeführten Aktionen.

> **Tipp:** Konvertierung, Sortierung, DAT-Matching und 76 weitere Features sind in separaten Tabs konfigurierbar. Der **Features-Tab** unter Konfiguration bietet Zugriff auf alle erweiterten Funktionen.

---

## CLI fuer Automation (JSON Output)

Das Headless-Skript `Invoke-RomCleanup.ps1` unterstuetzt jetzt maschinenlesbare Ergebnisse fuer externe Toolchains:

```powershell
pwsh -NoProfile -File .\Invoke-RomCleanup.ps1 `
  -Roots 'D:\ROMs\SNES' `
  -Mode DryRun `
  -EmitJsonSummary `
  -SummaryJsonPath .\reports\cli-summary.json
```

- `-EmitJsonSummary`: schreibt ein JSON-Ergebnis auf stdout.
- `-SummaryJsonPath`: schreibt dasselbe JSON in eine Datei.
- JSON-Schema: `romcleanup-cli-result-v1` mit Status, ExitCode, Preflight, RunErrors und Report-Pfaden.

---

## REST API (lokal, API-Key)

Der API-Server ist lokal auf Loopback begrenzt und nutzt Header-Auth via `X-Api-Key`.

```powershell
$env:ROM_CLEANUP_API_KEY = 'change-me'
pwsh -NoProfile -File .\Invoke-RomCleanupApi.ps1 -Port 7878 -CorsMode strict-local
```

### Endpunkte (MVP)

- `GET /health`
- `POST /runs`
- `GET /runs/{runId}`
- `GET /runs/{runId}/result`
- `POST /runs/{runId}/cancel`

### API-Sicherheitsoptionen

- `-CorsMode custom|local-dev|strict-local`
  - `custom`: verwendet `-CorsAllowOrigin`
  - `local-dev`: erlaubt `*`
  - `strict-local`: erzwingt `http://127.0.0.1`

### Plugin-Trust-Modus (Operation-Plugins)

- `ROMCLEANUP_PLUGIN_TRUST_MODE=compat|trusted-only|signed-only`
  - `compat`: rückwärtskompatibel (Standard)
  - `trusted-only`: nur Plugins mit `manifest.trusted=true`
  - `signed-only`: nur gültig signierte Plugins

### Beispiel: Run starten

```powershell
$headers = @{ 'X-Api-Key' = 'change-me' }
$body = @{
  mode = 'DryRun'
  roots = @('D:\ROMs\SNES')
  useDat = $false
  notifyAfterRun = $true
} | ConvertTo-Json

Invoke-RestMethod -Uri 'http://127.0.0.1:7878/runs' -Method Post -Headers $headers -Body $body -ContentType 'application/json'
```

### Beispiel: synchron warten

```powershell
Invoke-RestMethod -Uri 'http://127.0.0.1:7878/runs?wait=true' -Method Post -Headers $headers -Body $body -ContentType 'application/json'
```

---

## Projektstruktur

```
simple_sort.ps1                    # GUI Entry Point (WPF)
Invoke-RomCleanup.ps1              # CLI Entry Point (headless)
Invoke-RomCleanupApi.ps1           # REST API Entry Point (loopback)
dev/
  modules/
    # --- Domain / Engine ---
    Core.ps1                       # Regelwerk: Region, GameKey, Scoring, Winner
    Dedupe.ps1                     # Dedupe-Pipeline (1G1R, BIOS, Junk, DAT-Verify)
    Convert.ps1                    # Format-Konvertierung (CHD, RVZ, CSO, PBP, NKit, ECM)
    Classification.ps1             # Datei-/Konsolen-Klassifikation
    FormatScoring.ps1              # Format-Scoring/Prioritätslogik
    Dat.ps1                        # DAT-Parsing, XML, Hash-Matching, LRU-Cache
    DatSources.ps1                 # DAT-Katalog & Download (Redump, No-Intro, TOSEC)
    Sets.ps1 / SetParsing.ps1      # Set-Item-Konstruktoren (CUE, M3U, GDI, CCD)
    ConsoleSort.ps1                # Konsolen-Sortierung
    FolderDedupe.ps1               # Ordner-Deduplizierung (DOS/PC)
    # --- 76 Feature-Module (Phase 1–4) ---
    DatRename.ps1 ... Telemetry.ps1  # QW-01 bis XL-14 (siehe FEATURE_ROADMAP.md)
    # --- Application Layer ---
    ApplicationServices.ps1        # 22 Service-Fassaden (Invoke-Run*Service)
    OperationAdapters.ps1          # CLI/GUI Adapter → ApplicationServices
    PortInterfaces.ps1             # Port-Factories (FileSystem, ToolRunner, Dat, Audit, AppState)
    RunHelpers.ps1                 # Modularer Run-Helper-Hub
    RunHelpers.Execution.ps1       # Run-Ausführungslogik
    RunHelpers.Audit.ps1           # Audit-CSV/HMAC-Erzeugung
    RunHelpers.Insights.ps1        # Run-Analyse/Statistik
    # --- Adapter Layer (WPF GUI) ---
    wpf/MainWindow.xaml            # WPF-XAML-Layout (114 Buttons)
    WpfHost.ps1                    # WPF-Window-Host/Context-Aufbau
    WpfMainViewModel.ps1           # ViewModel mit Undo/Redo
    WpfEventHandlers.ps1           # WPF Event-Wiring (~1700 Zeilen)
    WpfSlice.Roots.ps1             # Slice 1: Root-Verwaltung
    WpfSlice.RunControl.ps1        # Slice 2: Start/Cancel/Progress
    WpfSlice.Settings.ps1          # Slice 3: Settings/Profile/Theme
    WpfSlice.DatMapping.ps1        # Slice 4: DAT-Grid/CRC-Verify
    WpfSlice.ReportPreview.ps1     # Slice 5: Reports/Export/Dashboards
    WpfSlice.AdvancedFeatures.ps1  # Slice 6: Plugins, Rollback, Watch + 65 Feature-Tab-Buttons
    WpfWizard.ps1                  # ISS-001 First-Start Wizard
    SimpleSort.WpfMain.ps1         # WPF-Startpunkt (Start-WpfGui)
    # --- Adapter Layer (API) ---
    ApiServer.ps1                  # HTTP-REST-Server, Auth, CORS, Rate-Limit
    # --- Infrastructure ---
    FileOps.ps1                    # Dateisystem-Operationen, Path-Safety
    Tools.ps1                      # Externe Tools (chdman, 7z, dolphintool)
    Settings.ps1                   # User-Settings Persistenz (JSON)
    Report.ps1 / ReportBuilder.ps1 # CSV/HTML/PDF Report-Generierung
    Logging.ps1                    # Structured JSONL Logging
    AppState.ps1                   # App-State (Get/Set/Watch/Undo/Redo)
    EventBus.ps1                   # Publish/Subscribe Event-System
    LruCache.ps1                   # Generischer LRU-Cache
    # --- Contracts & Observability ---
    DataContracts.ps1              # Schema-Validierung, DTOs
    ErrorContracts.ps1             # Strukturierte Fehlerobjekte
    CatchGuard.ps1                 # Silent-Catch-Governance
  tests/
    unit/                          # ~137 Unit-Testdateien (Pester 5)
    integration/                   # Integrations-Tests (WpfSmoke, Plugins)
    e2e/                           # End-to-End-Tests
    *.Tests.ps1                    # ~46 Root-Level-Testdateien
  tools/
    pipeline/                      # Test-Pipeline (Invoke-TestPipeline.ps1)
data/
  consoles.json                    # 100+ Konsolen-Definitionen
  rules.json                       # Regions-Patterns, Junk-Tags
  dat-catalog.json                 # DAT-Quellen (Redump, No-Intro, FBNEO)
  defaults.json                    # Standard-Einstellungen
  tool-hashes.json                 # SHA256-Allowlist für externe Tools
  i18n/de.json                     # Deutsche Lokalisierung
plugins/
  consoles/                        # Konsolen-Plugins (JSON)
  operations/                      # Operation-Plugins (PS1)
  reports/                         # Report-Plugins (PS1)
```

---

## Architektur

Hexagonal-light + Vertical Slices (ADR-0004). Schichten kommunizieren nur abwärts:

```
┌──────────────────────────────────────────────────────────┐
│  Entry Points                                            │
│  simple_sort.ps1 (GUI) │ Invoke-RomCleanup.ps1 (CLI)     │
│  Invoke-RomCleanupApi.ps1 (REST API)                     │
├──────────────────────────────────────────────────────────┤
│  Adapter Layer                                           │
│  WpfSlice.* (6 Slices) │ WpfWizard │ ApiServer           │
│  OperationAdapters │ WpfEventHandlers │ WpfMainViewModel  │
├──────────────────────────────────────────────────────────┤
│  Application Layer                                       │
│  ApplicationServices (22 Facades) │ RunHelpers.*          │
│  PortInterfaces │ SafetyToolsService                     │
├──────────────────────────────────────────────────────────┤
│  Domain / Engine (pure logic, keine UI/IO)                │
│  Core │ Dedupe │ Classification │ FormatScoring │ Convert │
│  Dat │ Sets │ ConsoleSort │ 76 Feature-Module (QW→XL)     │
├──────────────────────────────────────────────────────────┤
│  Infrastructure                                          │
│  FileOps │ Tools │ Settings │ Report │ Logging │ AppState │
│  EventBus │ LruCache │ MemoryGuard │ RunspaceLifecycle    │
├──────────────────────────────────────────────────────────┤
│  Contracts & Observability                               │
│  DataContracts │ ErrorContracts │ CatchGuard │ Telemetry  │
└──────────────────────────────────────────────────────────┘
```

**Sicherheitsfeatures:** Root-bound Moves, Reparse-Point-Blocking, Zip-Slip Pre/Post-Check, CSV-Injection-Neutralisierung, Verify-before-Delete, Audit-Signatur, Tool-Hash-Verifizierung (SHA256), Path-Traversal-Schutz, XXE-Schutz beim DAT-Parsing.

---

## Tests ausführen

```powershell
# Alle Tests
pwsh -NoProfile -File dev/tools/pipeline/Invoke-TestPipeline.ps1 -Stage all

# Nur Unit-Tests
pwsh -NoProfile -File dev/tools/pipeline/Invoke-TestPipeline.ps1 -Stage unit

# Nur Integration
pwsh -NoProfile -File dev/tools/pipeline/Invoke-TestPipeline.ps1 -Stage integration

# Nur E2E
pwsh -NoProfile -File dev/tools/pipeline/Invoke-TestPipeline.ps1 -Stage e2e
```

---

## GUI-Tabs

Die WPF-GUI ist in folgende Tabs organisiert:

| Tab | Inhalt |
|---|---|
| **Sortieren** | Root-Verwaltung, Modus-Wahl, Start/Cancel, Progress |
| **Konfiguration** | Einstellungen, Profile, Theme, DAT-Mapping, **Features-Tab** (65 Feature-Buttons in 9 Kategorien) |
| **Konvertierung** | Format-Konvertierung, Queue, Batch-Verify |
| **Log & Dashboard** | Live-Log, Statistiken, Diagramme |
| **Reports** | HTML/CSV/PDF-Reports, Export, Dashboards |

> **Features-Tab:** Unter Konfiguration → Features sind alle 76 Feature-Module über kategorisierte Expander-Sektionen erreichbar (Analyse, Konvertierung, DAT, Sammlungsverwaltung, Sicherheit, Workflow, Export, Infrastruktur, UI).

## Konfiguration

Settings werden automatisch gespeichert unter:  
`%APPDATA%\RomCleanupRegionDedupe\settings.json`

Portable Configs können über Config Export/Import als JSON-Datei geteilt werden. Alternativ: `--Portable` Flag für Settings/Logs relativ zum Programmordner.

---

## Lizenz

Privates Projekt — keine öffentliche Lizenz.
