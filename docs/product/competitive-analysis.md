# Romulus – Competitive Analysis & Positioning

**Stand:** 2026-04-18
**Confidence:** high (basiert auf öffentlichen Quellen, GitHub, Webseiten)

---

## Executive Summary

Romulus ist das einzige ROM-Management-Tool, das **GUI + CLI + REST API + signierte Audit-Trails + deterministische Conversion Pipeline** in einem Paket vereint. Kein Wettbewerber deckt alle diese Dimensionen gleichzeitig ab.

**Top-5 USPs:**
1. **Drei Entry Points** (GUI/CLI/API) — kein anderes Tool bietet alle drei
2. **Signierte Audit-Trails mit Rollback** — Enterprise-Grade Datenintegrität, einzigartig
3. **Deterministische Pipeline** — Preview ≡ Execute ≡ Report, mathematisch konsistent
4. **163 Konsolen mit differenzierten Policies** — pro-Konsole Conversion/DAT/Scoring Regeln
5. **Collection Health Monitor** — kontinuierliche Integritätsprüfung, kein Wettbewerber hat das

---

## Competitive Landscape

### Übersicht der Tools

| Tool | Typ | Sprache | Lizenz | Plattform | Aktiv | Stars/Community |
|------|-----|---------|--------|-----------|-------|-----------------|
| **Romulus** | Full Suite | C# .NET 10 | GPLv3 | Windows (Docker cross-plat) | ✅ | Neu |
| **RomVault** | DAT Manager | C# .NET | Closed Source | Windows (+Linux CLI) | ✅ | Discord ~2000+ |
| **Igir** | CLI Manager | TypeScript | GPLv3 | Cross-Platform | ✅ | ~800 Stars |
| **Retool** | DAT Filter | Python | BSD-3 | Cross-Platform | ❌ Eingestellt | ~460 Stars |
| **RomM** | Web Manager | Python/Vue | AGPL-3 | Docker/Self-Hosted | ✅ | ~8600 Stars |
| **clrmamepro** | DAT Manager | C++ | Closed | Windows | ✅ | Legacy |
| **SabreTools** | DAT Toolkit | C# .NET | MIT | Cross-Platform | ✅ | ~135 Stars |

---

## Feature-Matrix

| Feature | Romulus | RomVault | Igir | Retool | RomM | clrmamepro | SabreTools |
|---------|--------|----------|------|--------|------|------------|------------|
| **GUI** | ✅ WPF + Avalonia (staged) | ✅ WinForms | ❌ | ✅ Qt | ✅ Web | ✅ Win32 | ❌ |
| **CLI** | ✅ | ✅ (separat) | ✅ | ✅ | ❌ | ❌ | ✅ |
| **REST API** | ✅ mit SSE | ❌ | ❌ | ❌ | ✅ partial | ❌ | ❌ |
| **DAT-Matching** | ✅ 6 Quellen, Multi-Hash | ✅✅ DATVault | ✅ | ✅ Filter only | ❌ | ✅✅ | ✅✅ |
| **Deduplication** | ✅ Region-scored | ✅ | ✅ 1G1R | ✅ 1G1R | ❌ | ✅ | ❌ |
| **Conversion** | ✅ Multi-Step, Verify | ❌ | ✅ basic | ❌ | ❌ | ❌ | ❌ |
| **ROM Patching** | ✅ IPS/BPS/UPS | ❌ | ✅ 11 Formate | ❌ | ❌ | ❌ | ❌ |
| **Audit Trail** | ✅ SHA256-signiert | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ |
| **Rollback/Undo** | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ |
| **Metadata/Artwork** | ✅ ScreenScraper | ❌ | ❌ | ❌ | ✅ IGDB+SS+MobyGames | ❌ | ❌ |
| **Frontend Export** | ✅ 11 Frontends | ❌ | ✅ 15+ device dirs | ❌ | ✅ Playnite Plugin | ❌ | ❌ |
| **Health Monitor** | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ |
| **RA Compliance** | ✅ | ❌ | ❌ | ❌ | ✅ Hash-Check | ❌ | ❌ |
| **Docker** | ✅ CLI+API | ❌ | ✅ npm | ❌ | ✅ Primär | ❌ | ❌ |
| **Cross-Platform** | ⚠️ Docker+Avalonia staged | ⚠️ CLI Linux | ✅ | ✅ | ✅ | ❌ | ✅ |
| **Built-in Profiles** | ✅ 4 Presets + Custom | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ |
| **Watch/Schedule** | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ |
| **Collection Diff** | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ |
| **In-Browser Play** | ❌ | ❌ | ❌ | ❌ | ✅ EmulatorJS | ❌ | ❌ |
| **Tests** | 7100+ | ? | ~500+ | ~200 | ~100+ | ? | ~50 |
| **Konsolen** | 163 | DAT-abhängig | DAT-abhängig | DAT-abhängig | 400+ (IGDB) | DAT-abhängig | DAT-abhängig |

---

## Detailanalyse pro Wettbewerber

### RomVault (romvault.com)

**Stärken:**
- Etablierteste DAT-Matching Engine (seit 2010, 15+ Jahre Entwicklung)
- DATVault-Integration (automatische DAT-Updates, Patreon $5/Monat)
- CHD-Support nativ (seit v3.6.0, kein chdman.exe nötig)
- ZSTD + TorrentZip + TDCZip Structured Archive Formate (v3.7.0)
- MIA-Status-Tracking (Missing in Action ROMs)
- Community: aktiver Discord, dediziertes Wiki
- Closed Source, aber kostenlos nutzbar
- Level 1/2/3 Scanning (CRC-only bis Full-Hash)

**Schwächen:**
- Kein CLI (RVCmd existiert, aber limitiert)
- Keine API
- Keine Conversion Pipeline
- Kein Audit-Trail / Rollback
- Kein Metadata/Artwork
- Keine Frontend-Integration
- Keine Profiles/Presets
- Windows-GUI only (CLI cross-platform)
- Closed Source → kein Community-Beitrag zum Kerncode

**Romulus-Positionierung vs RomVault:**
> "RomVault ist der Goldstandard für DAT-Matching. Romulus bietet vergleichbares DAT-Matching plus Conversion, Audit-Trail, API, Frontend-Export und Health-Monitoring — als Open-Source-Paket."

---

### Igir (igir.io)

**Stärken:**
- Cross-Platform (TypeScript/Node.js), läuft überall
- Sehr aktive Entwicklung (v4.3.2, 103 Releases, 20 Contributors)
- 11 Patch-Formate (.aps, .bps, .dps, .ebp, .ips, .ips32, .ppf, .rup, .ups, .vcdiff, .xdelta)
- 15+ Device-Output-Directories (Adam, Batocera, ES, MiSTer, Pocket, OnionOS, etc.)
- TorrentZip + RVZSTD Archive-Support
- MAME Set-Building (split/merge/fullnonmerged)
- Header-Erkennung und -Entfernung
- Playlist-Generierung
- GPLv3 Open Source
- Comprehensive CLI mit 100+ Optionen
- Multi-Threading (DAT/Reader/Writer parallel)

**Schwächen:**
- Kein GUI
- Keine API
- Kein Audit-Trail / Rollback
- Kein Health-Monitoring
- Kein Metadata/Artwork
- Keine Built-in Profiles
- Keine Scoring/Winner-Selection — nur Filter/Prefer-Optionen
- Keine Collection Diff/Merge

**Romulus-Positionierung vs Igir:**
> "Igir ist das schweizer Taschenmesser der CLI-ROM-Verwaltung. Romulus bietet zusätzlich GUI, API, deterministische Scoring-Engine, Audit-Trail und Health-Monitoring für User, die mehr als CLI-Automation brauchen."

---

### Retool (unexpectedpanda/retool)

**Stärken:**
- Beste 1G1R-Filterung (superiore Clone-Listen und Metadata)
- Community-contributed Clone-Listen (separates Repository)
- Lokale Titel-Support (japanische/lokale Namen)
- GUI + CLI Versionen
- Cross-Platform (Python)
- Eigene DAT-Ausgabe (gefilterte DATs als Input für andere Tools)

**Schwächen:**
- ❌ **Eingestellt** (März 2026, "no longer maintained")
- Nur DAT-Filter, kein ROM-Manager
- Kein File-Handling (kein Copy/Move/Verify)
- Kein Audit-Trail
- Keine Conversion
- Benötigt nachgelagertes Tool (RomVault/Igir/clrmamepro)

**Romulus-Positionierung vs Retool:**
> "Retool war der beste 1G1R-Filter, ist aber eingestellt. Romulus integriert Region-basierte Deduplizierung direkt in die Pipeline — kein separates Tool nötig."

---

### RomM (rommapp/romm)

**Stärken:**
- Schönste UI (moderne Web-App, Vue.js Frontend)
- Stärkste Metadata-Integration (IGDB + ScreenScraper + MobyGames + SteamGridDB)
- In-Browser Gaming (EmulatorJS, RuffleRS)
- 400+ Plattformen (via IGDB)
- RetroAchievements-Integration
- Multi-User mit Berechtigungen
- Official Apps: Playnite Plugin, Android (Argosy), CFW Clients
- Größte Community: 8.6k Stars, 114 Contributors, aktiver Discord
- Docker-first (Self-Hosted)
- AGPL-3 Open Source

**Schwächen:**
- Kein DAT-Matching / Verification
- Keine Deduplication
- Keine Conversion
- Kein Audit-Trail
- Kein CLI (nur Web-API)
- Keine deterministische Pipeline
- Kein Offline-Betrieb (braucht Server)
- Kein File-Move/-Sort (zeigt Dateien an, managt sie nicht wirklich)

**Romulus-Positionierung vs RomM:**
> "RomM ist das Netflix für ROM-Sammlungen — wunderschön, aber ohne echtes Collection-Management. Romulus ist das DevOps-Tool: DAT-Verify, Deduplizierung, Conversion, Audit-Trail. Komplementär nutzbar."

---

### clrmamepro

**Stärken:**
- Ältestes und bewährtestes DAT-Tool (seit 1999, 25+ Jahre)
- De-facto Standard in der MAME-Community
- Exzellente DAT-Parsing und Rebuild-Engine
- Batch-Profil-Management
- Header-Support

**Schwächen:**
- Windows-only, veraltete Win32-UI
- Closed Source
- Keine API, kein CLI
- Keine Conversion
- Keine modernen Features (Metadata, Frontend-Export, etc.)
- Steile Lernkurve
- Keine aktive Community-Plattform

**Romulus-Positionierung vs clrmamepro:**
> "clrmamepro ist das vim der ROM-Verwaltung — mächtig, aber nicht mehr zeitgemäß. Romulus bietet modernere UX, API-Integration und Automation."

---

### SabreTools

**Stärken:**
- Umfangreichste DAT-Manipulation (Split, Merge, Convert, Statistics)
- Cross-Platform (.NET)
- MIT-Lizenz
- Header-Erkennung (8 Systeme)
- Dir2DAT, DAT-Conversion, DAT-Splitting, DAT-Statistics
- SHA-512 Hashing Support

**Schwächen:**
- Kein GUI (nur CLI)
- Kein ROM-Management (nur DAT-Manipulation)
- Keine Deduplication
- Keine Conversion
- Sehr kleine Community (3 Contributors, 135 Stars)
- Nischen-Tool für DAT-Autoren

**Romulus-Positionierung vs SabreTools:**
> "SabreTools ist das Experten-Werkzeug für DAT-Autoren. Romulus ist das End-User-Tool für Collection-Management, das DAT-Matching als Feature integriert."

---

## Positioning Matrix

```
                    ┌─────────────────────────────────────────────┐
                    │          FEATURE COMPLETENESS               │
                    │   Low ◄──────────────────────────► High     │
                    │                                             │
               High │  RomM          ┌─────────┐                 │
                    │  (Web UX)      │ ROMULUS  │                 │
   EASE OF USE      │                └─────────┘                 │
                    │                                             │
                    │  Retool†                    RomVault        │
                    │                                             │
               Low  │  SabreTools     Igir        clrmamepro     │
                    │                                             │
                    └─────────────────────────────────────────────┘
                                                    † eingestellt
```

---

## Community-Einführungswinkel pro Plattform

| Plattform | Zielgruppe | Angle | Key Message |
|-----------|-----------|-------|-------------|
| **r/Roms** | Casual Collectors | Ease of Use | "One-click ROM sorting with undo — no more manual folder management" |
| **r/DataHoarder** | Power Users | Data Integrity | "SHA256-signed audit trails, deterministic dedup, Health Monitor for your ROM archive" |
| **r/RetroArch** | Frontend Users | Integration | "Export to RetroArch, ES-DE, LaunchBox, Playnite, MiSTer in one click" |
| **r/SteamDeck** | Handheld Users | Docker/Headless | "Run Romulus headless on Deck via Docker — CLI + API, no desktop needed" |
| **r/emulation** | Enthusiasts | Technical | "Open-source ROM manager with 163 consoles, 200+ DATs, deterministic pipeline" |
| **RomVault Discord** | RV Users | Respectful Alt | "Complementary tool: Romulus adds Conversion, Audit, API where RV stops" |

---

## Competitive Advantages Summary

### Einzigartig bei Romulus (kein Wettbewerber hat das)
1. **Signierter Audit-Trail mit Rollback** — Enterprise-Grade Nachvollziehbarkeit
2. **Collection Health Monitor** — Bit-Rot Detection, Integrity Score, Alerts
3. **Drei Entry Points in einem Tool** — GUI + CLI + REST API mit SSE
4. **Deterministische Pipeline** — mathematische Garantie: Preview = Execute = Report
5. **Built-in Profiles** — Presets (default, retro-purist, space-saver, quick-scan)
6. **Collection Diff/Merge** — Side-by-Side Vergleich zweier Collections

### Stärker als Wettbewerb
7. **Conversion Pipeline** — Multi-Step mit Verify, Source-Backup, Tool-Hash-Verification (vs Igir: basic)
8. **163 Konsolen mit differenzierten Policies** — nicht "one size fits all"
9. **7100+ Tests** — 10-50× mehr als jeder Wettbewerber
10. **Security** — Path-Traversal, Zip-Slip, CSV-Injection, HTML-Encoding, Tool-Hash-Verification

### Aufholen nötig
11. **Cross-Platform GUI** — Avalonia staged, Docker als Brücke
12. **Metadata-Tiefe** — nur ScreenScraper (vs RomM: IGDB+SS+MobyGames+SteamGridDB)
13. **Community-Größe** — Neues Projekt, noch keine Sichtbarkeit
14. **In-Browser Play** — nicht geplant (unterschiedlicher Fokus)

---

## Risiken & Mitigierung

| Risiko | Wahrscheinlichkeit | Impact | Mitigierung |
|--------|-------------------|--------|-------------|
| Community ignoriert "yet another tool" | mittel | hoch | Klare USP-Kommunikation, respektvolle Positionierung |
| RomVault implementiert API/Conversion | niedrig | mittel | 12+ Monate Architektur-Vorsprung, Open-Source-Vorteil |
| Igir überholt bei Feature-Parität | niedrig | mittel | GUI + API + Audit als dauerhafte Differenzierung |
| RomM besetzt "modern ROM manager" Nische | mittel | mittel | Unterschiedlicher Fokus: Management vs Browsing/Playing |
| Retool-User suchen Ersatz | hoch | positiv | Direkte Migration möglich, Region-Dedup integriert |
