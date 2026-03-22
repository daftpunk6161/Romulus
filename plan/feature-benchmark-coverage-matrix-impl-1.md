---
goal: "Coverage Matrix Implementation Plan — Verbindliche Umsetzung der Minimum Coverage Matrix als prüfbare Benchmark-Struktur im Repository"
version: "1.0"
date_created: 2026-03-20
last_updated: 2026-03-22
owner: "Romulus Team"
status: "Mostly Complete"
tags:
  - feature
  - testing
  - benchmark
  - coverage
  - architecture
---

# Coverage Matrix Implementation Plan – RomCleanup

![Status: Planned](https://img.shields.io/badge/status-Planned-blue)

Dieser Plan überführt die abstrakten Gate-Definitionen aus `docs/COVERAGE_GAP_AUDIT.md` §10 in eine **konkrete, versionierbare, CI-prüfbare Benchmark-Struktur**. Er definiert Dateistruktur, Sollmengen, Spezialmatrizen, Generatoren, Manifest-Erweiterungen und CI-Regeln so, dass jede Unterschreitung automatisch erkannt wird.

**Bezug:**
- `docs/COVERAGE_GAP_AUDIT.md` — Minimum Coverage Matrix (§10), Lückenanalyse
- `docs/GROUND_TRUTH_SCHEMA.md` — JSONL-Schema, Datenmodell
- `docs/TESTSET_DESIGN.md` — Dataset-Klassen, Pflichtfälle
- `plan/feature-benchmark-testset-1.md` — Basis-Plan (Infrastruktur + 70 golden-core)
- `plan/feature-benchmark-coverage-expansion-1.md` — Ausbauplan E1–E4 (56 Tasks)
- `data/consoles.json` — 69 Systeme (47 Cartridge, 22 Disc)

---

## 1. Executive Plan

### Hauptziel

Die in `COVERAGE_GAP_AUDIT.md` §10 definierte Minimum Coverage Matrix wird **nicht als Papierdokument**, sondern als maschinenlesbare Gate-Konfiguration (`benchmark/gates.json`), automatisierte Coverage-Validierung (`CoverageValidator.cs` + `CoverageGateTests.cs`) und vollständig befüllte Ground-Truth-Dateien (≥1.200 JSONL-Einträge) umgesetzt.

### Wichtigste Engpässe

| # | Engpass | Auswirkung | Blockiert |
|---|---------|-----------|-----------|
| 1 | **Arcade hat 0 Einträge, braucht ≥200** | 20 Subszenarien (Parent/Clone/BIOS/Split/Merged/Non-Merged/CHD/Device) ohne jegliche Testabdeckung | Metriken M4, M7 für komplexeste Plattform |
| 2 | **BIOS hat 0 Einträge, braucht ≥60** | BIOS-AS-GAME und GAME-AS-BIOS ungetestet; direkter Datenverlust-Vektor | FC-08, BIOS-Gate, Dedupe-Korrektheit |
| 3 | **17 Systeme fehlen komplett** | 25% des Scopes unsichtbar bei jedem Refactor | System-Coverage-Gate 69/69 |
| 4 | **Computer/PC 0 Einträge, braucht ≥150** | Folder-Only-Detection (höchste false-positive-Rate) ungetestet | FC-13, Computer-Gate |
| 5 | **Redump Multi-File 0 Einträge, braucht ≥80** | CUE+BIN/GDI/M3U Set-Integrität ungetestet; Disc-Datenverlust möglich | FC-10, FC-11, Disc-Gate |
| 6 | **PS1↔PS2↔PSP Disambiguation 0 Einträge, braucht ≥30** | Häufigster Produktionsfehler ungetestet | PS-Disambiguation-Gate |
| 7 | **Kein DAT-TOSEC-Eintrag, braucht ≥10** | Computer-Systeme nutzen TOSEC; 0 Testfälle | DAT-Ökosystem-Gate |

### Wichtigste Prioritäten

1. **P0**: 69/69 System-Coverage + BIOS ≥60 + Arcade ≥200 + PS-Disambiguation ≥30
2. **P1**: Computer ≥150 + Multi-File ≥80 + CHD-RAW-SHA1 ≥8 + Negative Controls ≥40
3. **P2**: Alle 20 Fallklassen über Gate + Tier-1 ≥20 pro System + Tier-2 ≥8 pro System
4. **P3**: Vollständige Manifest-Coverage-Metriken + Baseline-Snapshot + Regressions-Gate

### Kurzfazit

Das Schema und die Gate-Definitionen existieren. Die tatsächliche Befüllung ist bei null. Dieser Plan definiert exakt, welche Dateien mit welchen Mindestmengen zu befüllen sind, welche Generatoren dafür nötig sind, und wie CI bei Unterschreitung automatisch fehlschlägt. Ohne diese Umsetzung sind die Metriken M4 (Wrong Match Rate) und M7 (Unsafe Sort Rate) nicht belastbar berechenbar.

---

## 2. Datasets und Dateistruktur

### 2.1 Übersicht: 7 JSONL-Dateien + Manifest

Alle Dateien liegen in `benchmark/ground-truth/`. Jede Zeile ist ein eigenständiges JSON-Objekt (JSONL). Sortiert nach ID, UTF-8 ohne BOM, LF-Zeilenende.

### 2.2 golden-core.jsonl

| Attribut | Wert |
|----------|------|
| **Zweck** | Schnellster CI-Referenztest. Jede Detection-Methode × relevante Systeme. Reine Referenzfälle (FC-01), BIOS-Referenzen (FC-08), DAT-Referenzen (FC-06). |
| **Zielgrösse** | **250 Einträge** |
| **Plattformfamilien** | Alle 5: Cartridge (~100), Disc (~70), Arcade (~40), Computer (~25), Hybrid (~15) |
| **Fallklassen** | FC-01 (Referenz, ~150), FC-06 (DAT exact, ~30), FC-08 (BIOS, ~20), FC-16 (Negative Controls, ~20), FC-12 (Archive inner, ~15), FC-19 (Junk, ~15) |
| **ID-Präfix** | `gc-` |
| **Schwierigkeit** | 80% easy, 20% medium |
| **Must-Have** | Alle 69 Systeme mit ≥1 Eintrag; alle 9 Detection-Methoden vertreten; BIOS für ≥8 Systeme; 4 DAT-Ökosysteme je ≥3 Einträge |

### 2.3 golden-realworld.jsonl

| Attribut | Wert |
|----------|------|
| **Zweck** | Benchmark-Kerndatensatz. System-Tier-Tiefe aufbauen. Regions-, Revisions-, Container-Varianten pro System. |
| **Zielgrösse** | **350 Einträge** |
| **Plattformfamilien** | Cartridge (~140), Disc (~100), Arcade (~50), Computer (~40), Hybrid (~20) |
| **Fallklassen** | FC-01 (~200), FC-06 (~40), FC-09 (Parent/Clone, ~30), FC-10 (Multi-Disc, ~15), FC-11 (Multi-File, ~15), FC-08 (BIOS, ~15), FC-13 (Directory, ~10), FC-19 (Junk, ~10), FC-15 (Ambiguous, ~10), FC-04 (Extension-Conflict, ~5) |
| **ID-Präfix** | `gr-` |
| **Schwierigkeit** | 60% easy, 30% medium, 10% hard |
| **Must-Have** | Tier-1 (9 Systeme) je ≥12 Einträge (Rest kommt aus golden-core); Tier-2 (16 Systeme) je ≥4 Einträge |

### 2.4 edge-cases.jsonl

| Attribut | Wert |
|----------|------|
| **Zweck** | Alle bekannten Verwechslungspaare, Header-Konflikte, Disambiguierungsfälle, Confidence-Grenzen. |
| **Zielgrösse** | **150 Einträge** |
| **Plattformfamilien** | Cartridge (~35), Disc (~50), Arcade (~30), Computer (~20), Hybrid (~15) |
| **Fallklassen** | FC-03 (Header-Konflikt, ~25), FC-05 (Folder-vs-Header, ~20), FC-15 (Ambiguous, ~15), FC-18 (Cross-System, ~50), FC-08 (BIOS-Edge, ~15), FC-17 (Repair-unsafe, ~10), FC-04 (Extension-Conflict, ~15) |
| **ID-Präfix** | `ec-` |
| **Schwierigkeit** | 30% medium, 50% hard, 20% adversarial |
| **Must-Have** | PS1↔PS2↔PSP ≥30; GB↔GBC ≥12; MD↔32X ≥8; SAT↔SCD↔DC ≥12; GC↔Wii ≥8; Arcade Split/Merged/Non-Merged ≥30 |

### 2.5 chaos-mixed.jsonl

| Attribut | Wert |
|----------|------|
| **Zweck** | Realistische Wildsammlungs-Szenarien. Falsch benannt, kaputte Dateien, Headerless, gemischte Archive, Benennung mit Unicode/Sonderzeichen. |
| **Zielgrösse** | **200 Einträge** |
| **Plattformfamilien** | Cartridge (~50), Disc (~40), Arcade (~40), Computer (~40), Hybrid (~15), Cross-Platform (~15) |
| **Fallklassen** | FC-02 (Falsch benannt, ~40), FC-04 (Extension-Konflikt, ~15), FC-12 (Archive-Inner, ~25), FC-20 (Kaputte Sets, ~20), FC-07 (DAT weak/no, ~20), FC-19 (Junk/NonGame, ~15), Headerless (~25), Arcade-BIOS-Chaos (~15), Computer-Folder-Chaos (~15), Unicode/Sonderzeichen (~10) |
| **ID-Präfix** | `cm-` |
| **Schwierigkeit** | 20% medium, 50% hard, 30% adversarial |
| **Must-Have** | Headerless NES/SNES/MD/GB ≥10; Arcade-BIOS-Varianten ≥10; Kaputte Archive ≥10; Falsch benannt ≥30 |

### 2.6 negative-controls.jsonl

| Attribut | Wert |
|----------|------|
| **Zweck** | Dateien, die **nie** als ROM erkannt werden dürfen (FC-16), und Dateien die korrekt als UNKNOWN bleiben sollen (FC-14). |
| **Zielgrösse** | **80 Einträge** |
| **Plattformfamilien** | Systemübergreifend (alle 5 Familien referenziert) |
| **Fallklassen** | FC-14 (UNKNOWN expected, ~30), FC-16 (Negative Control, ~40), FC-17 (Sort-blocked Subset, ~10) |
| **ID-Präfix** | `nc-` |
| **Schwierigkeit** | 70% easy, 30% medium |
| **Must-Have** | 100% Pass-Rate Pflicht; je ≥5 Einträge pro Plattformfamilie; Nicht-ROM-Dateitypen (.doc, .pdf, .jpg, .mp3, .py, .exe, .dll, .html, .css, .js) je ≥1 |

### 2.7 repair-safety.jsonl

| Attribut | Wert |
|----------|------|
| **Zweck** | Confidence-Gating-Szenarien. Prüft ob das System korrekt zwischen Sort-Safe, Sort-Blocked und Review-Needed unterscheidet. |
| **Zielgrösse** | **70 Einträge** |
| **Plattformfamilien** | Cartridge (~20), Disc (~20), Arcade (~10), Computer (~10), Hybrid (~10) |
| **Fallklassen** | FC-17 (Repair-unsafe/blocked, ~30), FC-01 (Referenz mit hoher Confidence, ~15), FC-08 (BIOS-Sort-Block, ~10), FC-14 (UNKNOWN-Sort-Block, ~10), Multi-File-Repair (~5) |
| **ID-Präfix** | `rs-` |
| **Schwierigkeit** | 40% medium, 40% hard, 20% adversarial |
| **Must-Have** | 9 Confidence-Szenarien × ≥4 Fälle; hasConflict=true ≥5; Category≠Game ≥10 |

### 2.8 dat-coverage.jsonl

| Attribut | Wert |
|----------|------|
| **Zweck** | DAT-Matching-Szenarien über alle 4 Ökosysteme. SHA1, CRC32, MD5 Matching, Misses, Cross-DAT. |
| **Zielgrösse** | **100 Einträge** |
| **Plattformfamilien** | Cartridge (~30, No-Intro), Disc (~25, Redump), Arcade (~20, MAME), Computer (~15, TOSEC), Cross-Platform (~10) |
| **Fallklassen** | FC-06 (DAT exact, ~60), FC-07 (DAT weak/no/ambig, ~30), FC-12 (Archive-Inner-Hash, ~10) |
| **ID-Präfix** | `dc-` |
| **Schwierigkeit** | 50% easy, 30% medium, 20% hard |
| **Must-Have** | No-Intro ≥25; Redump ≥25; MAME ≥15; TOSEC ≥10; DAT-none/miss ≥15; CHD-RAW-SHA1 ≥8 |

### 2.9 Zusammenfassung Dateien → Gesamtverteilung

| JSONL-Datei | Ziel | Familien (C/D/A/Co/H) | Primäre Fallklassen |
|-------------|------|----------------------|---------------------|
| golden-core | 250 | 100/70/40/25/15 | FC-01, FC-06, FC-08, FC-16 |
| golden-realworld | 350 | 140/100/50/40/20 | FC-01, FC-06, FC-09, FC-10, FC-11 |
| edge-cases | 150 | 35/50/30/20/15 | FC-03, FC-05, FC-15, FC-18 |
| chaos-mixed | 200 | 50/40/40/40/15+15 | FC-02, FC-04, FC-12, FC-20 |
| negative-controls | 80 | Cross | FC-14, FC-16 |
| repair-safety | 70 | 20/20/10/10/10 | FC-17 |
| dat-coverage | 100 | 30/25/20/15/10 | FC-06, FC-07, FC-12 |
| **TOTAL** | **1.200** | **380/310/200/150/80+80** | **20/20 besetzt** |

---

## 3. Mindestmengen pro Plattformfamilie

### 3.1 Familie A: No-Intro / Cartridge

| Metrik | Sollwert | Hard-Fail | Begründung |
|--------|----------|-----------|-----------|
| **Systeme** | 35/35 (alle) | <35 | 100% Coverage Pflicht |
| **Samples gesamt** | ≥380 | <320 | 60–70% des Volumens realer Sammlungen |
| **Tier-1 (NES, SNES, N64, GBA, GB, GBC, MD, NDS, 3DS, SWITCH)** | ≥20 pro System | <15 pro System | Statistisch belastbare System-Metriken |
| **Tier-2 (32X, SMS, GG, PCE, LYNX, A78, A26, INTV, NGP, COLECO, VECTREX, SG1000, WS, WSC, VB, POKEMINI)** | ≥8 pro System | <5 pro System | Mindest-Variantenbreite |
| **Tier-3/4 (CHANNELF, SUPERVISION, NGPC, ODYSSEY2, A52)** | ≥3 pro System | <2 pro System | Smoke-Test-Level |
| **BIOS-Fälle** | ≥15 | <10 | GBA, NDS, 3DS BIOS |
| **Ambiguous/UNKNOWN/Negative** | ≥90 | <70 | GB↔GBC, MD↔32X, Headerless, Falsch benannt |
| **DAT (No-Intro)** | ≥30 | <20 | Primäres DAT-Ökosystem für Cartridge |
| **Safety/Repair-Block** | ≥50 | <35 | Extension-only, Folder-only, Confidence-Varianten |
| **CartridgeHeader-Detection** | ≥30 (10 Header-Systeme × 3) | <20 | Primärer Erkennungsweg |
| **UniqueExtension-Detection** | ≥40 | <25 | Grundlagen-Coverage |
| **Headerless** | ≥15 | <10 | NES, SNES, GB, GBC, MD ohne Header |

### 3.2 Familie B: Redump / Disc

| Metrik | Sollwert | Hard-Fail | Begründung |
|--------|----------|-----------|-----------|
| **Systeme** | 22/22 (alle) | <22 | Teuerste Fehlentscheidungen (GB-grosse Dateien) |
| **Samples gesamt** | ≥310 | <260 | Multi-File-Komplexität × System-Vielfalt |
| **Tier-1 (PS1, PS2, PSP, GC, WII, SAT, DC)** | ≥15 pro System | <10 pro System | PVD-Disambiguation braucht Tiefe |
| **Tier-2 (PS3, SCD, 3DO, PCECD, XBOX, X360, WIIU)** | ≥8 pro System | <5 pro System | Disc-Header-Varianten |
| **Tier-3 (PCFX, NEOCD, JAGCD, CD32, CDI, FMTOWNS)** | ≥3 pro System | <2 pro System | Seltene Systeme, aber Pflicht |
| **BIOS-Fälle** | ≥15 | <10 | PS1, PS2, SAT, DC, 3DO, XBOX |
| **Ambiguous/UNKNOWN/Negative** | ≥80 | <60 | PS1↔PS2↔PSP, GC↔Wii, SAT↔SCD↔DC |
| **Multi-File-Sets (CUE+BIN/GDI/M3U/CCD/MDS)** | ≥30 | <20 | Set-Integrität-Pflicht |
| **Multi-Disc** | ≥25 | <15 | 2–4 Disc Sets |
| **CHD-RAW-SHA1** | ≥8 | <5 | Embedded SHA1, nicht Container-Hash |
| **DAT (Redump)** | ≥25 | <15 | Primäres DAT-Ökosystem für Disc |
| **Cross-System-Disc-Disambiguation** | ≥20 | <12 | PS1↔PS2, PS2↔PSP, GC↔Wii, SAT↔SCD↔DC |
| **Serial-Number-Detection** | ≥15 | <10 | SLUS, SCUS, UCUS, GMSE etc. |

### 3.3 Familie C: Arcade

| Metrik | Sollwert | Hard-Fail | Begründung |
|--------|----------|-----------|-----------|
| **System-Keys** | 3 (ARCADE, NEOGEO, NEOCD) | <3 | Alle Keys Pflicht |
| **Logische Unterfamilien** | ≥8 als Subclass | <6 | MAME Generic, Neo Geo MVS/AES, Neo Geo CD, CPS1/2/3, Naomi, Atomiswave, System 16, PGM |
| **Samples gesamt** | ≥200 | <160 | 20 Subszenarien × 10 = Minimum |
| **Parent-Sets** | ≥20 | <15 | Basis-Erkennung |
| **Clone-Sets** | ≥15 | <10 | Clone→Parent Zuordnung |
| **BIOS-ZIPs** | ≥15 | <10 | neogeo.zip, pgm.zip, naomi.zip etc. |
| **Split-ROM-Sets** | ≥10 | <7 | Nur eigene ROMs |
| **Merged-ROM-Sets** | ≥10 | <7 | Parent+Clones in einem ZIP |
| **Non-Merged-ROM-Sets** | ≥10 | <7 | Komplett eigenständig |
| **CHD-Supplement** | ≥8 | <5 | Naomi/Atomiswave/MAME-HDD |
| **ARCADE↔NEOGEO Ambiguität** | ≥10 | <6 | acceptableAlternatives |
| **DAT (MAME CRC32)** | ≥15 | <10 | CRC32-basiertes Matching |
| **Neo Geo CD (Disc-basiert)** | ≥8 | <5 | Wechsel von ZIP zu Disc |
| **Device-ROMs (NonGame)** | ≥5 | <3 | Category=NonGame |
| **Kaputte/Unvollständige ROM-Sets** | ≥8 | <5 | FC-20 |
| **Junk-Arcade (Mahjong/Quiz/Gambling)** | ≥6 | <3 | NonGame-Klassifikation |
| **Negative Controls** | ≥5 | <3 | ZIP ohne Arcade-Bezug |

### 3.4 Familie D: Computer / PC

| Metrik | Sollwert | Hard-Fail | Begründung |
|--------|----------|-----------|-----------|
| **Systeme** | 10/10 (alle) | <10 | DOS, AMIGA, C64, ZX, MSX, ATARIST, A800, CPC, PC98, X68K |
| **Samples gesamt** | ≥150 | <120 | 10 Systeme × 15 Varianten = Minimum |
| **Systeme mit UniqueExt (AMIGA, C64, ZX, MSX, ATARIST, A800)** | ≥8 pro System | <5 | Extension-Varianten (ADF/HDF/DMS, D64/T64/G64, TZX/TAP) |
| **Systeme ohne UniqueExt (DOS, CPC, PC98, X68K)** | ≥10 pro System | <7 | FolderName/Keyword-Only = höchste Fehlerrate |
| **Folder-Only-Detection** | ≥12 | <8 | Einziger Erkennungsweg für CPC, PC98, X68K |
| **Directory-based Games** | ≥10 | <5 | DOS (Ordner+EXE), Amiga (WHDLoad) |
| **Extension-Overlap** | ≥8 | <5 | .dsk (CPC vs. MSX), .img (Amiga vs. generic) |
| **BIOS/Firmware** | ≥5 | <3 | Amiga Kickstart, C64 KERNAL |
| **DAT (TOSEC)** | ≥10 | <5 | Primäres DAT-Ökosystem für Computer |
| **Confidence < 80 (Sort-Block)** | ≥8 | <5 | CPC, PC98, X68K mit schwachem Signal |
| **Ambiguous/UNKNOWN/Negative** | ≥45 | <30 | Folder-Conflict, Extension-Conflict, Negative |
| **Disk-Image-Varianten** | ≥8 | <5 | ADF/HDF/DMS, D64/T64/G64, TZX/TAP/Z80 |

### 3.5 Familie E: Hybrid / Sonderfälle

| Metrik | Sollwert | Hard-Fail | Begründung |
|--------|----------|-----------|-----------|
| **Systeme** | 5 (PSP, VITA, 3DS, SWITCH, WIIU) | <5 | Alle Pflicht |
| **Samples gesamt** | ≥80 | <60 | Container-Vielfalt pro System |
| **Container-Formate** | ≥15 | <10 | CSO, ISO, VPK, CIA, 3DS, NSP, XCI, RPX, WUX |
| **Serial-Detection** | ≥8 | <5 | PSP (UCUS), Vita (PCSE), 3DS (CTR) |
| **Directory-based (WiiU)** | ≥8 | <5 | RPX in Ordner-Struktur |
| **Extension-Conflict (.iso)** | ≥5 | <3 | PSP-ISO vs. PS1/PS2-ISO |
| **BIOS** | ≥3 | <2 | 3DS, WiiU Firmware |
| **Update/DLC als NonGame** | ≥5 | <3 | Switch Updates, 3DS DLC |
| **DAT-Match** | ≥8 | <5 | No-Intro/Redump-DATs |
| **Ambiguous/UNKNOWN/Negative** | ≥25 | <15 | PSP↔PS1/PS2, Container-Verwirrung |

---

## 4. Spezialmatrizen

### 4.1 BIOS-Matrix

**Warum Release-kritisch:** BIOS-AS-GAME → falsche Dedupe → User verliert BIOS → Emulator startet nicht. GAME-AS-BIOS → Spiel wird nicht gefunden, weil als BIOS versteckt.

#### Pflicht-Unterkategorien

| # | Szenario | Mindestmenge | Systeme | Erwartetes Ergebnis | Warum kritisch |
|---|----------|-------------|---------|---------------------|---------------|
| B-01 | BIOS mit `[BIOS]` Tag im Dateinamen | 8 | PS1, PS2, GBA, NDS, DC, SAT, 3DO, XBOX | `category: "Bios"` | Häufigstes Pattern in No-Intro/Redump |
| B-02 | BIOS mit `(BIOS)` Tag | 5 | PS1, GC, Wii | `category: "Bios"` | Alternative Schreibweise |
| B-03 | BIOS mit Systemnamen ("PlayStation BIOS") | 5 | PS1, PS2, SAT | `category: "Bios"` | Beschreibende Benennung |
| B-04 | BIOS ohne explizites Tag (nur DAT-Kennung) | 5 | Diverse | `category: "Bios"` via DAT | DAT-Fallback-Erkennung |
| B-05 | Spiel mit BIOS-ähnlichem Namen | 5 | "BioShock", "BIOS Agent" | `category: "Game"` **nicht** Bios | Negativ-Test: kein falsches Match |
| B-06 | Arcade Shared BIOS (neogeo.zip, pgm.zip) | 5 | ARCADE, NEOGEO | `category: "Bios"`, `biosSystemKeys` | Arcade-BIOS-Detection |
| B-07 | Amiga Kickstart ROM | 2 | AMIGA | `category: "Bios"` | Computer-BIOS |
| B-08 | C64 KERNAL/BASIC ROM | 2 | C64 | `category: "Bios"` | Computer-BIOS |
| B-09 | 3DS/WiiU System-Firmware | 2 | 3DS, WIIU | `category: "Bios"` | Hybrid-BIOS |
| B-10 | BIOS neben Spielen im gleichen Ordner | 5 | PS1, SAT, DC | BIOS korrekt trotz Spiel-Nachbarn | Kontext-Robustheit |
| B-11 | BIOS in Archiv (ZIP mit nur BIOS) | 3 | PS1, NEOGEO | Archive-BIOS-Detection | Container-Variante |
| B-12 | Falsches BIOS (korrupte Datei) | 3 | Diverse | `datMatchLevel: "none"` oder UNKNOWN | Fehlerfall |
| **Total** | | **≥60** | **≥12 Systeme** | | |

#### Verteilung auf JSONL-Dateien

| JSONL | BIOS-Einträge | Beschreibung |
|-------|--------------|-------------|
| golden-core | 20 | B-01 bis B-04 Referenzen |
| golden-realworld | 15 | Arcade-BIOS (B-06, B-07), weitere B-01 |
| edge-cases | 15 | B-05 Falsch-Positive, B-10 Kontext, B-12 Korrupt |
| chaos-mixed | 10 | B-06 Varianten, B-11 Archive |
| **Total** | **≥60** | |

### 4.2 Arcade-Matrix

**Warum Release-kritisch:** 20 Subszenarien, höchste kombinatorische Komplexität. ROM-Set-Varianten (Split/Merged/Non-Merged) erzeugen radikal unterschiedliche ZIP-Inhalte für dasselbe Spiel.

#### Pflicht-Unterkategorien

| # | Szenario | Min. | Prüfziel |
|---|----------|------|---------|
| A-01 | MAME Parent-Set (Standard) | 10 | consoleKey=ARCADE, name=parent |
| A-02 | MAME Clone-Set | 10 | relationships.cloneOf korrekt |
| A-03 | Neo Geo MVS/AES Parent | 8 | consoleKey=NEOGEO |
| A-04 | Neo Geo Clone | 5 | Clone unter NEOGEO |
| A-05 | Neo Geo CD (Disc-basiert) | 8 | Erkennung wechselt von ZIP zu Disc |
| A-06 | Shared BIOS neogeo.zip | 5 | category=Bios, biosSystemKeys |
| A-07 | Shared BIOS pgm/naomi/etc. | 5 | Weitere BIOS-Systeme |
| A-08 | Split-ROM-Set | 8 | ZIP enthält nur eigene ROMs |
| A-09 | Merged-ROM-Set | 8 | ZIP enthält Parent+Clones |
| A-10 | Non-Merged-ROM-Set | 8 | ZIP ist eigenständig |
| A-11 | CHD-Supplement | 5 | ZIP + .chd nebeneinander |
| A-12 | Device-ROM (nicht spielbar) | 5 | category=NonGame |
| A-13 | MAME-Versionswechsel-Name | 5 | ROM-Name ändert sich |
| A-14 | Kaputte ROM-Sets | 8 | Fehlende ROMs → kein Crash |
| A-15 | ARCADE↔NEOGEO Ambiguität | 8 | acceptableAlternatives |
| A-16 | FolderName-Varianten | 8 | arcade/, mame/, fbneo/ |
| A-17 | Junk-Arcade (Mahjong/Quiz) | 5 | NonGame-Klassifikation |
| A-18 | DAT-Match MAME (CRC32) | 10 | CRC32-basiert |
| A-19 | DAT-Miss (Homebrew/Bootleg) | 5 | Kein DAT-Eintrag |
| A-20 | ZIP mit gemischtem Inhalt | 4 | Archiv-Robustheit |
| **Total** | **≥140 (+ ~60 aus anderen Sets = 200)** | |

#### Verteilung auf JSONL-Dateien

| JSONL | Arcade-Einträge | Schwerpunkt |
|-------|----------------|------------|
| golden-core | 40 | A-01, A-03, A-06, A-18 Referenzen |
| golden-realworld | 50 | A-01–A-04, A-06–A-07 |
| edge-cases | 30 | A-08–A-10 Split/Merged/Non-Merged, A-11, A-15 |
| chaos-mixed | 40 | A-12–A-14, A-17, A-19–A-20 |
| dat-coverage | 20 | A-18, A-19, DAT-Varianten |
| negative-controls | 5 | ZIP ohne Arcade |
| repair-safety | 10 | Confidence-Varianten Arcade |
| **Total** | **~200** (≥195 verteilt) | |

### 4.3 Redump-Matrix

**Warum Release-kritisch:** Grösste Dateien (4+ GB pro ISO), teuerstes Fehlverhalten pro Einzelfehler. Multi-File-Sets können bei unvollständiger Verarbeitung Discs unbrauchbar machen.

#### Pflicht-Unterkategorien

| # | Szenario | Min. | Systeme |
|---|----------|------|---------|
| R-01 | Single-ISO korrekt | 14 | PS1(2), PS2(2), PSP(2), GC(2), Wii(2), SAT(2), DC(2) |
| R-02 | CUE+BIN Single-Track | 8 | PS1, SAT, SCD |
| R-03 | CUE+BIN Multi-Track | 8 | PS1, SAT, DC |
| R-04 | GDI+Tracks | 5 | DC |
| R-05 | CHD (single disc) | 8 | PS1, PS2, SAT, DC |
| R-06 | CHD-RAW-SHA1 DAT-Match | 8 | PS1, PS2, PSP, SAT |
| R-07 | M3U-Playlist + CHD-Set | 6 | PS1, PS2 |
| R-08 | CCD+IMG+SUB | 4 | PS1, SAT |
| R-09 | MDS+MDF | 3 | PS1, PS2 |
| R-10 | Multi-Disc 2 Discs | 6 | PS1, SAT |
| R-11 | Multi-Disc 3–4 Discs | 6 | PS1, PS2 |
| R-12 | PS1↔PS2 PVD-Disambiguation | 8 | PS1, PS2 |
| R-13 | PS2↔PSP PVD-Disambiguation | 6 | PS2, PSP |
| R-14 | GC↔Wii Magic-Byte | 6 | GC, Wii |
| R-15 | SAT↔SCD↔DC Header | 6 | SAT, SCD, DC |
| R-16 | CSO-Container (PSP) | 3 | PSP |
| R-17 | WIA/RVZ/WBFS (Wii) | 4 | Wii |
| R-18 | Xbox/X360 ISO-Signatur | 4 | XBOX, X360 |
| R-19 | DAT-Miss seltene Discs | 5 | PCFX, JAGCD, CD32 |
| R-20 | Disc-BIOS | 5 | PS1, PS2, SAT, DC |
| **Total** | **≥143** | |

#### Verteilung auf JSONL-Dateien

| JSONL | Redump-Einträge | Schwerpunkt |
|-------|----------------|------------|
| golden-core | 70 | R-01, R-05, R-06 Referenzen |
| golden-realworld | 60 | R-02–R-04, R-07–R-09, R-10–R-11 |
| edge-cases | 50 | R-12–R-18 Disambiguation |
| chaos-mixed | 15 | R-19 seltene Discs, kaputte CUE/GDI |
| dat-coverage | 25 | R-06, R-19, Redump-DAT-Varianten |
| repair-safety | 15 | Multi-File-Repair (fehlende BINs, korrupte CHDs) |
| **Total** | **~235** (Disc-Gesamtfamilie 310) | |

### 4.4 Directory-based / PC / Computer

**Warum Release-kritisch:** Höchste False-Positive-Rate aller Familien. CPC, PC98, X68K haben weder UniqueExtension noch Header-Signatur — einziger Erkennungsweg ist FolderName/Keyword. Ein falscher Ordner-Alias-Eintrag reicht, um tausende Dateien falsch zu sortieren.

#### Pflicht-Unterkategorien

| # | Szenario | Min. | Systeme |
|---|----------|------|---------|
| D-01 | UniqueExtension korrekt | 18 | AMIGA(.adf), C64(.d64), ZX(.tzx), MSX(.mx1), ATARIST(.st), A800(.atr) |
| D-02 | FolderName-Only-Detection | 12 | DOS, CPC, PC98, X68K |
| D-03 | FilenameKeyword-Detection | 8 | DOS [DOS], CPC [CPC], PC98 [PC-98] |
| D-04 | Directory-based Game | 10 | DOS (Ordner+.EXE), Amiga (WHDLoad) |
| D-05 | Extension-Conflict (.dsk, .img, .bin) | 8 | CPC(.dsk) vs MSX(.dsk), Amiga(.adf vs .hdf) |
| D-06 | TOSEC-DAT-Match | 8 | AMIGA, C64, ZX, MSX, ATARIST |
| D-07 | Folder-vs-Keyword-Conflict | 5 | Ordner sagt X, Keyword sagt Y |
| D-08 | Disk-Image-Varianten | 8 | ADF/HDF/DMS, D64/T64/G64, TZX/TAP/Z80 |
| D-09 | BIOS/Firmware | 5 | Amiga Kickstart, C64 KERNAL |
| D-10 | Confidence < 80 (Sort-Block) | 8 | CPC, PC98, X68K |
| D-11 | Junk/Demo/PD-Software | 5 | Public-Domain als NonGame |
| D-12 | Negative Controls | 5 | .exe kein DOS-Spiel, .dsk unbekannt |
| **Total** | **≥100 (+ ~50 aus anderen Sets = 150)** | |

### 4.5 Ambiguous / Unknown / Negative Controls

**Warum Release-kritisch:** UNKNOWN ist kein Fehler — es ist eine korrekte Antwort auf uneindeutige Daten. Wenn das System UNKNOWN-Fälle fälschlich einem System zuordnet, entstehen False Positives. Wenn es Negative Controls akzeptiert, werden Nicht-ROM-Dateien verschoben.

#### Pflicht-Unterkategorien

| # | Szenario | Min. | Verteilung |
|---|----------|------|-----------|
| U-01 | UNKNOWN korrekt (Datei mit .rom-Extension, Zufallsinhalt) | 8 | negative-controls |
| U-02 | UNKNOWN korrekt (leerer Ordner mit Systemname) | 3 | negative-controls |
| U-03 | UNKNOWN korrekt (.bin mit PDF-Inhalt) | 3 | negative-controls |
| U-04 | UNKNOWN korrekt (.iso mit RAR-Magic) | 3 | negative-controls |
| U-05 | UNKNOWN korrekt (Datei ohne Extension) | 3 | negative-controls |
| U-06 | UNKNOWN korrekt (doppelte Extension .nes.bak) | 3 | negative-controls |
| U-07 | Negative: Nicht-ROM-Dateitypen (15 Typen × 1) | 15 | negative-controls |
| U-08 | Negative: ZIP ohne ROM-Inhalt | 3 | negative-controls |
| U-09 | Ambiguous: GB↔GBC (acceptableAlternatives) | 12 | edge-cases |
| U-10 | Ambiguous: NEOGEO↔ARCADE | 8 | edge-cases |
| U-11 | Ambiguous: Extension-only (.bin, .iso) | 8 | edge-cases, chaos-mixed |
| U-12 | Sort-blocked: Confidence < 80 | 10 | repair-safety |
| U-13 | Sort-blocked: hasConflict=true | 5 | repair-safety |
| U-14 | Sort-blocked: Category=Bios | 5 | repair-safety |
| **Total** | **≥90 (über negative-controls, edge-cases, repair-safety)** | |

---

## 5. Stub-Generatoren und Sample-Strategie

### 5.1 Stub-Generatoren (nach Plattformklasse)

#### Klasse A: Cartridge-Header-Generatoren

| Generator-Name | Erzeugt | Besonderheiten | Synthetisch machbar? |
|---------------|---------|----------------|---------------------|
| `nes-ines` | NES iNES-Header `4E45531A` | Standard, iNES 2.0 Variante | ✅ Ja |
| `n64-be` / `n64-bs` / `n64-le` | N64 Endian-Varianten | 3 Byte-Order-Varianten | ✅ Ja |
| `gba-logo` | GBA Logo @ 0x04 | `24FFAE51` | ✅ Ja |
| `gb-dmg` / `gbc-dual` / `gbc-only` | GB/GBC CGB-Flag-Varianten | 0x00, 0x80, 0xC0 | ✅ Ja |
| `snes-lorom` / `snes-hirom` | SNES LoROM/HiROM | Header @ 0x7FC0 / 0xFFC0 | ✅ Ja |
| `md-genesis` / `32x-sega` | MD/32X Header @ 0x100 | `SEGA MEGA DRIVE` / `SEGA 32X` | ✅ Ja |
| `lynx-header` | Lynx `4C594E58` | 4-Byte-Magic | ✅ Ja |
| `atari7800` | 7800 `ATARI7800` @ 0x01 | 8-Byte-Signatur | ✅ Ja |
| `headerless-padding` | ROM ohne Header (nur Bytes) | Für FC-20, Headerless-Tests | ✅ Ja |

#### Klasse B: Disc-Header-Generatoren

| Generator-Name | Erzeugt | Besonderheiten | Synthetisch machbar? |
|---------------|---------|----------------|---------------------|
| `ps1-pvd` | PS1 PVD @ 0x8000 | `PLAYSTATION`, kein BOOT2= | ✅ Ja |
| `ps2-pvd` | PS2 PVD @ 0x8000 | `BOOT2=cdrom0:\\SLUS_XXX` | ✅ Ja |
| `psp-pvd` | PSP PVD @ 0x8000 + `PSP_GAME` | PSP-Boot-Marker | ✅ Ja |
| `gc-magic` | GC Magic `C2339F3D` @ 0x1C | 4-Byte-Magic | ✅ Ja |
| `wii-magic` | Wii Magic `5D1C9EA3` @ 0x18 | 4-Byte-Magic | ✅ Ja |
| `sat-ipbin` | Saturn `SEGA SATURN` IP.BIN | Text @ 0x00 | ✅ Ja |
| `dc-ipbin` | DC `SEGA SEGAKATANA` IP.BIN | Text @ 0x00 | ✅ Ja |
| `scd-ipbin` | SCD `SEGADISCSYSTEM` | Text @ 0x00 | ✅ Ja |
| `3do-opera` | 3DO Opera FS | FS @ 0x00 | ✅ Ja |
| `xbox-media` | Xbox `MICROSOFT*XBOX*MEDIA` | ISO-Signatur | ✅ Ja |

#### Klasse C: Arcade-Generatoren

| Generator-Name | Erzeugt | Besonderheiten | Synthetisch machbar? |
|---------------|---------|----------------|---------------------|
| `arcade-zip-parent` | MAME Parent ZIP | ZIP mit CRC32-kontrollierten Inner-Files | ✅ Ja (CRC32 steuerbar) |
| `arcade-zip-clone` | MAME Clone ZIP | Delta-ROMs | ✅ Ja |
| `arcade-zip-merged` | Merged ROM-Set | Parent+Clones in einem ZIP | ✅ Ja |
| `arcade-zip-split` | Split ROM-Set | Nur eigene ROMs | ✅ Ja |
| `arcade-zip-nonmerged` | Non-Merged ROM-Set | Komplett eigenständig | ✅ Ja |

#### Klasse D: Computer-Generatoren

| Generator-Name | Erzeugt | Besonderheiten | Synthetisch machbar? |
|---------------|---------|----------------|---------------------|
| `adf-amiga` | Amiga ADF 880KB | Disk-Header | ✅ Ja |
| `d64-c64` | C64 D64 | BAM @ Track 18 | ✅ Ja |
| `tzx-zx` | ZX TZX `ZXTape!` | Header-Magic | ✅ Ja |
| `dsk-cpc` / `dsk-msx` | CPC/MSX EDSK | `EXTENDED` Header | ✅ Ja |
| `st-atarist` | Atari ST Disk-Image | ST-Format | ✅ Ja |
| `atr-atari800` | Atari 800 ATR `0x9602` | Header-Magic | ✅ Ja |

#### Klasse E: Container/Multi-File-Generatoren

| Generator-Name | Erzeugt | Besonderheiten | Synthetisch machbar? |
|---------------|---------|----------------|---------------------|
| `multi-file-cue-bin` | CUE + N BIN-Dateien | CUE-Text + Padding-BINs | ✅ Ja |
| `multi-file-gdi` | GDI + Track-Dateien | GDI-Textformat + Track-Padding | ✅ Ja |
| `multi-file-m3u-chd` | M3U + N CHD-Stubs | M3U-Text + CHD-v5-Headers | ✅ Ja |
| `chd-v5` | CHD v5 mit embedded SHA1 | SHA1 @ offset 0x40 | ✅ Ja |
| `chd-v4` | CHD v4 mit SHA1 | Älteres Format | ✅ Ja |
| `cso-container` | CSO-Header `CISO` | Compressed ISO | ✅ Ja |
| `directory-wiiu` | WiiU Ordner (code/app.rpx) | Directory-Container | ✅ Ja |

#### Klasse F: Hilfs-/Negativ-Generatoren

| Generator-Name | Erzeugt | Synthetisch machbar? |
|---------------|---------|---------------------|
| `empty-file` | 0-Byte-Datei | ✅ Ja |
| `random-bytes` | Deterministisch zufällig (Seed=ID.GetHashCode()) | ✅ Ja |
| `ext-only` | 1-Byte-Datei mit nur Extension | ✅ Ja |
| `non-rom-content` | JFIF/PDF/MZ-Header in ROM-Extension | ✅ Ja |
| `corrupt-zip` | ZIP mit kaputtem Central-Directory | ✅ Ja |
| `truncated-rom` | Korrekter Header, abgeschnittene Daten | ✅ Ja |

### 5.2 Fälle die echte donated Samples benötigen

| Bereich | Warum synthetisch unzureichend | Strategie |
|---------|-------------------------------|-----------|
| **Bestimmte CHD v4/v5 Edge-Cases** | CHD-Toolchain prüft interne Konsistenz; Minimal-Stubs könnten von `chdman verify` abgelehnt werden | Prüfen ob Detection ohne `chdman` auskommt; sonst donated CHD-Stub |
| **WIA/RVZ/WBFS Container** | Proprietäre Wii-Kompressionsformate mit internem Checksum | Prüfen ob Extension + Folder reicht; sonst 1 Sample pro Format donaten |
| **DMS-Archive (Amiga)** | Proprietäres Kompressionsformat | Extension .dms reicht für Detection; kein Inner-Content nötig |
| **Echte MAME-CRC32-Referenzen** | Wenn CRC32-Matching gegen echte DAT geprüft wird | Synthtetisch mit vordefiniertem CRC32 machbar; sonst 5 donated ROM-Sätze |

### 5.3 Besonders schwer zu erzeugende Fälle

| Fall | Schwierigkeit | Workaround |
|------|--------------|-----------|
| Cross-MAME-Version-Namenswechsel | Braucht Wissen über 2 MAME-Versionen | Hardcoded Namenspaar (z.B. `sf2u` → `sf2ua`) |
| PS1↔PS2 mit identischem PVD aber unterschiedlichem Boot-Marker | PVD-Bytes müssen bit-genau stimmen | Generator parametrisiert PVD-Content exakt |
| Merged-ROM-Set mit 50+ Inner-Files | ZIP-Erzeugung mit vielen Einträgen | Generator beschränkt auf 10 repräsentative Inner-Files |
| Directory-based DOS-Spiel (10 Dateien in Ordner) | Realistische Ordnerstruktur | Minimale Struktur: GAME.EXE + GAME.DAT + README.TXT |

---

## 6. Manifest-Erweiterungen

### 6.1 Neue Pflicht-Felder in `benchmark/manifest.json`

```jsonc
{
  "version": "2.0.0",
  "groundTruthVersion": "1.0.0",
  "lastModified": "2026-XX-XX",
  "totalEntries": 1200,

  // Bestehende Felder (Basis-Plan)
  "bySet": {
    "golden-core": 250,
    "golden-realworld": 350,
    "chaos-mixed": 200,
    "edge-cases": 150,
    "negative-controls": 80,
    "repair-safety": 70,
    "dat-coverage": 100
  },

  // NEU: Coverage-Block (Pflicht ab Phase E1)
  "coverage": {
    "systemsCovered": 69,
    "systemsTotal": 69,
    "systemCoveragePercent": 100.0,

    // 6.2 Plattformfamilien-Statistiken
    "byPlatformFamily": {
      "cartridge":  { "entries": 380, "systems": 35, "gate": 320, "status": "PASS" },
      "disc":       { "entries": 310, "systems": 22, "gate": 260, "status": "PASS" },
      "arcade":     { "entries": 200, "systems": 3,  "subclasses": 8, "gate": 160, "status": "PASS" },
      "computer":   { "entries": 150, "systems": 10, "gate": 120, "status": "PASS" },
      "hybrid":     { "entries": 80,  "systems": 5,  "gate": 60,  "status": "PASS" }
    },

    // 6.3 Tier-Tiefe-Statistiken
    "byTier": {
      "tier1": { "systems": 9,  "minPerSystem": 20, "actualMin": 20, "gate": 15, "status": "PASS" },
      "tier2": { "systems": 16, "minPerSystem": 8,  "actualMin": 8,  "gate": 5,  "status": "PASS" },
      "tier3": { "systems": 22, "minPerSystem": 3,  "actualMin": 3,  "gate": 2,  "status": "PASS" },
      "tier4": { "systems": 22, "minPerSystem": 2,  "actualMin": 2,  "gate": 1,  "status": "PASS" }
    },

    // 6.4 Fallklassen-Statistiken
    "byFallklasse": {
      "FC-01": { "entries": 120, "gate": 100, "status": "PASS" },
      "FC-02": { "entries": 40,  "gate": 30,  "status": "PASS" },
      "FC-03": { "entries": 25,  "gate": 20,  "status": "PASS" },
      "FC-04": { "entries": 30,  "gate": 20,  "status": "PASS" },
      "FC-05": { "entries": 20,  "gate": 15,  "status": "PASS" },
      "FC-06": { "entries": 60,  "gate": 40,  "status": "PASS" },
      "FC-07": { "entries": 40,  "gate": 30,  "status": "PASS" },
      "FC-08": { "entries": 60,  "gate": 40,  "status": "PASS" },
      "FC-09": { "entries": 30,  "gate": 20,  "status": "PASS" },
      "FC-10": { "entries": 25,  "gate": 15,  "status": "PASS" },
      "FC-11": { "entries": 30,  "gate": 20,  "status": "PASS" },
      "FC-12": { "entries": 20,  "gate": 15,  "status": "PASS" },
      "FC-13": { "entries": 15,  "gate": 10,  "status": "PASS" },
      "FC-14": { "entries": 30,  "gate": 20,  "status": "PASS" },
      "FC-15": { "entries": 25,  "gate": 15,  "status": "PASS" },
      "FC-16": { "entries": 40,  "gate": 30,  "status": "PASS" },
      "FC-17": { "entries": 30,  "gate": 20,  "status": "PASS" },
      "FC-18": { "entries": 25,  "gate": 15,  "status": "PASS" },
      "FC-19": { "entries": 25,  "gate": 15,  "status": "PASS" },
      "FC-20": { "entries": 20,  "gate": 10,  "status": "PASS" }
    },

    // 6.5 Spezialbereich-Statistiken
    "bySpecialArea": {
      "biosTotal":                     { "entries": 60,  "systems": 12, "gate": 35, "status": "PASS" },
      "arcadeParent":                  { "entries": 20,  "gate": 15, "status": "PASS" },
      "arcadeClone":                   { "entries": 15,  "gate": 10, "status": "PASS" },
      "arcadeSplitMergedNonMerged":    { "entries": 30,  "gate": 20, "status": "PASS" },
      "arcadeBios":                    { "entries": 15,  "gate": 10, "status": "PASS" },
      "arcadeChdSupplement":           { "entries": 8,   "gate": 5,  "status": "PASS" },
      "psDisambiguation":              { "entries": 30,  "gate": 20, "status": "PASS" },
      "gbGbcCgbVariants":              { "entries": 12,  "gate": 8,  "status": "PASS" },
      "md32xAmbiguity":                { "entries": 8,   "gate": 5,  "status": "PASS" },
      "multiFileSets":                 { "entries": 30,  "gate": 20, "status": "PASS" },
      "multiDisc":                     { "entries": 25,  "gate": 15, "status": "PASS" },
      "chdRawSha1":                    { "entries": 8,   "gate": 5,  "status": "PASS" },
      "datNoIntro":                    { "entries": 25,  "gate": 15, "status": "PASS" },
      "datRedump":                     { "entries": 25,  "gate": 15, "status": "PASS" },
      "datMame":                       { "entries": 15,  "gate": 10, "status": "PASS" },
      "datTosec":                      { "entries": 10,  "gate": 5,  "status": "PASS" },
      "directoryBasedGames":           { "entries": 10,  "gate": 5,  "status": "PASS" },
      "headerlessRoms":                { "entries": 20,  "gate": 10, "status": "PASS" },
      "crossSystemDiscDisambiguation": { "entries": 20,  "gate": 12, "status": "PASS" },
      "serialNumberDetection":         { "entries": 15,  "gate": 10, "status": "PASS" }
    },

    // 6.6 Schwierigkeitsverteilung
    "byDifficulty": {
      "easy": 500,
      "medium": 350,
      "hard": 250,
      "adversarial": 100
    }
  },

  // NEU: Gate-Zusammenfassung (generiert durch CoverageValidator)
  "gates": {
    "s1MinimumViableBenchmark": {
      "totalEntries":                { "required": 1200, "actual": 1200, "status": "PASS" },
      "systemsCovered":              { "required": 69,   "actual": 69,   "status": "PASS" },
      "fallklassenCovered":          { "required": 20,   "actual": 20,   "status": "PASS" },
      "platformFamiliesAboveGate":   { "required": 5,    "actual": 5,    "status": "PASS" },
      "biosEntries":                 { "required": 60,   "actual": 60,   "status": "PASS" },
      "arcadeEntries":               { "required": 200,  "actual": 200,  "status": "PASS" },
      "psDisambiguationEntries":     { "required": 30,   "actual": 30,   "status": "PASS" },
      "overallStatus": "PASS"
    }
  }
}
```

### 6.2 Statistik-Pflichtfelder (Zusammenfassung)

| Neues Manifest-Feld | Typ | Berechnet aus | Warum Pflicht |
|---------------------|-----|--------------|---------------|
| `coverage.systemsCovered` | int | Unique `expected.consoleKey` ≠ null | System-Coverage-Gate |
| `coverage.byPlatformFamily.*.entries` | int | `PlatformFamilyClassifier.Classify()` | Plattform-Gate |
| `coverage.byTier.*.actualMin` | int | Min. Einträge unter allen Systemen des Tiers | Tier-Tiefe-Gate |
| `coverage.byFallklasse.FC-XX.entries` | int | `FallklasseClassifier.Classify()` per Tags | Fallklassen-Gate |
| `coverage.bySpecialArea.*.entries` | int | Tag-basierte Zählung | Spezialbereich-Gate |
| `gates.s1MinimumViableBenchmark.overallStatus` | PASS/FAIL | Alle Sub-Gates | Release-Gate |

### 6.3 Automatische Manifest-Generierung

Das Manifest wird **nicht manuell gepflegt**, sondern von `ManifestGenerator.cs` aus den JSONL-Dateien automatisch erzeugt:

1. Lädt alle `.jsonl`-Dateien aus `benchmark/ground-truth/`
2. Klassifiziert jeden Eintrag (Plattformfamilie, Tier, Fallklasse, Spezialbereich)
3. Zählt alle Metriken
4. Prüft gegen Gate-Schwellen aus `benchmark/gates.json`
5. Schreibt `benchmark/manifest.json` mit allen Statistiken

---

## 7. Coverage-Gate / CI-Regeln

### 7.1 Gate-Konfigurationsdatei

Alle Gate-Schwellen werden in `benchmark/gates.json` maschinenlesbar definiert (keine Hardcodierung in Tests):

```jsonc
{
  "s1": {
    "totalEntries": { "target": 1200, "hardFail": 970 },
    "systemsCovered": { "target": 69, "hardFail": 69 },
    "fallklassenCovered": { "target": 20, "hardFail": 20 },

    "platformFamily": {
      "cartridge":  { "target": 380, "hardFail": 320 },
      "disc":       { "target": 310, "hardFail": 260 },
      "arcade":     { "target": 200, "hardFail": 160 },
      "computer":   { "target": 150, "hardFail": 120 },
      "hybrid":     { "target": 80,  "hardFail": 60 }
    },

    "tierDepth": {
      "tier1": { "minPerSystem": 20, "hardFail": 15 },
      "tier2": { "minPerSystem": 8,  "hardFail": 5 },
      "tier3": { "minPerSystem": 3,  "hardFail": 2 },
      "tier4": { "minPerSystem": 2,  "hardFail": 1 }
    },

    "caseClasses": {
      "FC-01": { "target": 120, "hardFail": 100 },
      "FC-02": { "target": 40,  "hardFail": 30 },
      "FC-03": { "target": 25,  "hardFail": 20 },
      "FC-04": { "target": 30,  "hardFail": 20 },
      "FC-05": { "target": 20,  "hardFail": 15 },
      "FC-06": { "target": 60,  "hardFail": 40 },
      "FC-07": { "target": 40,  "hardFail": 30 },
      "FC-08": { "target": 60,  "hardFail": 40 },
      "FC-09": { "target": 30,  "hardFail": 20 },
      "FC-10": { "target": 25,  "hardFail": 15 },
      "FC-11": { "target": 30,  "hardFail": 20 },
      "FC-12": { "target": 20,  "hardFail": 15 },
      "FC-13": { "target": 15,  "hardFail": 10 },
      "FC-14": { "target": 30,  "hardFail": 20 },
      "FC-15": { "target": 25,  "hardFail": 15 },
      "FC-16": { "target": 40,  "hardFail": 30 },
      "FC-17": { "target": 30,  "hardFail": 20 },
      "FC-18": { "target": 25,  "hardFail": 15 },
      "FC-19": { "target": 25,  "hardFail": 15 },
      "FC-20": { "target": 20,  "hardFail": 10 }
    },

    "specialAreas": {
      "biosTotal":                     { "target": 60, "hardFail": 35 },
      "biosSystems":                   { "target": 12, "hardFail": 8 },
      "arcadeParent":                  { "target": 20, "hardFail": 15 },
      "arcadeClone":                   { "target": 15, "hardFail": 10 },
      "arcadeSplitMergedNonMerged":    { "target": 30, "hardFail": 20 },
      "arcadeBios":                    { "target": 15, "hardFail": 10 },
      "psDisambiguation":              { "target": 30, "hardFail": 20 },
      "gbGbcCgb":                      { "target": 12, "hardFail": 8 },
      "md32x":                         { "target": 8,  "hardFail": 5 },
      "multiFileSets":                 { "target": 30, "hardFail": 20 },
      "multiDisc":                     { "target": 25, "hardFail": 15 },
      "chdRawSha1":                    { "target": 8,  "hardFail": 5 },
      "datNoIntro":                    { "target": 25, "hardFail": 15 },
      "datRedump":                     { "target": 25, "hardFail": 15 },
      "datMame":                       { "target": 15, "hardFail": 10 },
      "datTosec":                      { "target": 10, "hardFail": 5 },
      "directoryBased":                { "target": 10, "hardFail": 5 },
      "headerless":                    { "target": 20, "hardFail": 10 }
    }
  }
}
```

### 7.2 CI-Prüfregeln

#### Regel 1: Coverage-Gate (blockiert Build bei Unterschreitung)

```
dotnet test --filter Category=CoverageGate
```

Prüft:
- Alle Plattformfamilien über Hard-Fail-Schwelle
- Alle 20 Fallklassen besetzt über Hard-Fail-Schwelle
- Alle Tier-Systeme über Mindesttiefe
- Alle Spezialbereiche über Hard-Fail-Schwelle
- Gesamt ≥ Hard-Fail-Schwelle (970)

**Wann fehlschlägt:** Wenn _irgendein_ Gate-Wert unter die Hard-Fail-Schwelle fällt.

#### Regel 2: Manifest-Konsistenz (blockiert Build bei Drift)

```
dotnet test --filter "FullyQualifiedName~Manifest_IsConsistentWithGroundTruth"
```

Prüft:
- `manifest.totalEntries` == tatsächliche JSONL-Zeilenzahl
- `manifest.bySet.*` == tatsächliche Zeilen pro Datei
- `manifest.coverage.systemsCovered` == tatsächliche unique consoleKeys
- Alle IDs unique über alle JSONL-Dateien
- Alle IDs folgen Namenskonvention `{set-prefix}-{system}-{subclass}-{nr}`
- Alle JSONL-Zeilen valide gegen `ground-truth.schema.json`

**Wann fehlschlägt:** Wenn Manifest und tatsächliche Daten auseinanderlaufen.

#### Regel 3: Schema-Validierung (blockiert Build bei ungültigen Einträgen)

```
dotnet test --filter "FullyQualifiedName~AllEntries_ValidateAgainstSchema"
```

Prüft:
- Jede JSONL-Zeile ist gültiges JSON
- Jede Zeile hat alle Pflichtfelder
- `expected.consoleKey` ist entweder null oder in `data/consoles.json`
- `tags` Array enthält ≥1 klassifizierbaren Tag
- `stub.generator` referenziert einen registrierten Generator

#### Regel 4: Regressions-Gate (blockiert Release bei Qualitätsverschlechterung)

```
dotnet test --filter Category=Benchmark
```

Prüft:
- Wrong-Match-Rate ≤ Baseline + 0.1%
- Unsafe-Sort-Rate ≤ Baseline + 0.1%
- Kein System das vorher korrekt erkannt wurde ist jetzt Wrong

### 7.3 Gate-Eskalation

| Schwere | Reaktion | Wann |
|---------|----------|------|
| **HARD-FAIL** | Build blockiert, PR kann nicht gemergt werden | Ein Gate-Wert unter Hard-Fail-Schwelle |
| **WARNING** | CI gibt Warnung aus, PR kann gemergt werden | Ein Gate-Wert unter Target aber über Hard-Fail |
| **INFO** | CI gibt Coverage-Report aus | Normal, alle Gates bestanden |

### 7.4 Wie man erkennt, dass die Matrix wirklich umgesetzt ist

1. `dotnet test --filter Category=CoverageGate` → 0 Failures
2. `benchmark/manifest.json` → `gates.s1MinimumViableBenchmark.overallStatus: "PASS"`
3. Alle 69 Systeme in `manifest.coverage.systemsCovered`
4. Kein Spezialbereich unter Target
5. Regressions-Gate (`Category=Benchmark`) schlägt bei Detection-Verschlechterung fehl

---

## 8. Umsetzungsphasen

### Phase 1 — P0-Abdeckung: System-Coverage + BIOS + PS-Disambiguation

| Attribut | Wert |
|----------|------|
| **Ziel** | 69/69 Systeme, BIOS ≥60, PS1↔PS2↔PSP ≥30, golden-core = 250, Coverage-Validator in CI |
| **Betroffene Datasets** | golden-core (→250), edge-cases (→45), chaos-mixed (→15) |
| **Betroffene Familien** | Alle 5 (Schwerpunkt Cartridge + Disc) |
| **Benötigte Generatoren** | Alle Klasse-A + Klasse-B Generatoren, `multi-file-cue-bin`, `multi-file-gdi`, `multi-file-m3u-chd`, `cso-container`, `directory-wiiu` |
| **Neue CI-Dateien** | `CoverageValidator.cs`, `CoverageGateTests.cs`, `gates.json`, `ManifestGenerator.cs`, `FallklasseClassifier.cs`, `PlatformFamilyClassifier.cs` |
| **Exit-Kriterium** | System-Coverage 69/69; BIOS ≥50 über ≥12 Systeme; PS-Disambiguation ≥30; golden-core = 250; `CoverageGateTests` grün (Phase-1-Subset-Gates); Build grün |
| **Einträge neu** | ~310 |

### Phase 2 — Kritische Familien: Arcade + Computer + Multi-File + CHD

| Attribut | Wert |
|----------|------|
| **Ziel** | Arcade ≥200, Computer ≥150, Multi-File ≥80, CHD-RAW-SHA1 ≥25 |
| **Betroffene Datasets** | golden-realworld (→105), edge-cases (→90), chaos-mixed (→35), negative-controls (→10), repair-safety (→10), dat-coverage (→40) |
| **Betroffene Familien** | Arcade (Schwerpunkt), Computer, Disc (Multi-File) |
| **Benötigte Generatoren** | Alle Klasse-C (Arcade-ZIP-Varianten), Klasse-D (Computer), `chd-v5`, `chd-v4` |
| **Exit-Kriterium** | Arcade ≥160 (Hard-Fail); Computer ≥120 (Hard-Fail); Multi-File ≥20 (Hard-Fail); CHD ≥5 (Hard-Fail); Build grün |
| **Einträge neu** | ~230 |

### Phase 3 — Spezialfälle: Tiefe, Breite, alle Fallklassen

| Attribut | Wert |
|----------|------|
| **Ziel** | golden-realworld ≥350, chaos-mixed ≥200, edge-cases ≥150, negative-controls ≥80; Tier-1 ≥20 pro System; Tier-2 ≥8 pro System; Alle 20 Fallklassen über Gate |
| **Betroffene Datasets** | golden-realworld (→350), chaos-mixed (→200), edge-cases (→150), negative-controls (→80) |
| **Betroffene Familien** | Alle 5 (Auffüllung) |
| **Benötigte Generatoren** | `corrupt-zip`, `truncated-rom`, Headerless-Varianten |
| **Exit-Kriterium** | Alle Fallklassen über Hard-Fail; Tier-1 ≥15 pro System (Hard-Fail); Tier-2 ≥5 pro System (Hard-Fail); Build grün |
| **Einträge neu** | ~520 |

### Phase 4 — Langfristige Zielabdeckung: Metriken-Validierung + Baseline

| Attribut | Wert |
|----------|------|
| **Ziel** | repair-safety ≥70, dat-coverage ≥100, TOSEC ≥10, Headerless ≥20; Alle S1-Gates PASS; Manifest vollständig; Baseline-Snapshot |
| **Betroffene Datasets** | repair-safety (→70), dat-coverage (→100), Lücken in allen anderen |
| **Betroffene Familien** | Auffüllung wo Gaps (schwerpunktmässig Disc, Computer) |
| **Benötigte Generatoren** | Keine neuen; Verfeinerung bestehender |
| **Neue CI-Dateien** | `s1-baseline.json` (Metrik-Snapshot) |
| **Exit-Kriterium** | **ALLE S1-Gates PASS**: ≥1.200 Einträge, 69/69 Systeme, 20/20 Fallklassen, 5/5 Plattformfamilien über Gate, BIOS ≥35 (Hard-Fail), Arcade ≥160 (Hard-Fail), PS-Disambig ≥20 (Hard-Fail); Manifest-Konsistenz grün; Baseline geschrieben; `dotnet test --filter Category=CoverageGate` grün |
| **Einträge neu** | ~140 |

### Phasen-Verlauf (kumulativ)

| Phase | Kumulativ | System-Coverage | Fallklassen | Familien über Gate | S1-Status |
|-------|-----------|----------------|------------|-------------------|-----------|
| **Start** | 0 | 0/69 | 0/20 | 0/5 | ❌ |
| **Phase 1** | ~310 | 69/69 | ~12/20 | 2/5 (Cartridge, Disc) | ❌ |
| **Phase 2** | ~540 | 69/69 | ~16/20 | 4/5 (+Arcade, Computer) | ❌ |
| **Phase 3** | ~1.060 | 69/69 | 20/20 | 5/5 (+Hybrid) | ⚠️ (knapp) |
| **Phase 4** | **≥1.200** | **69/69** | **20/20** | **5/5** | **✅ PASS** |

---

## 9. Die 20 wichtigsten nächsten Umsetzungsschritte

| # | Schritt | Phase | Typ | P0? |
|---|---------|-------|-----|-----|
| 1 | `benchmark/gates.json` erstellen mit allen Gate-Schwellen aus §7.1 | 1 | Konfiguration | ✅ |
| 2 | `CoverageValidator.cs` implementieren: lädt Gates, klassifiziert Entries, prüft gegen Schwellen | 1 | C# Code | ✅ |
| 3 | `FallklasseClassifier.cs` implementieren: Tag→FC-XX Mapping (§4.5 der Expansion-Plan) | 1 | C# Code | ✅ |
| 4 | `PlatformFamilyClassifier.cs` implementieren: consoleKey→Familie (A–E) Mapping | 1 | C# Code | ✅ |
| 5 | `CoverageGateTests.cs` erstellen: 6 xUnit-Facts gegen Coverage-Validator | 1 | C# Test | ✅ |
| 6 | `golden-core.jsonl` auf 250 Einträge bringen: alle 69 Systeme + 9 Detection-Methoden | 1 | Ground Truth | ✅ |
| 7 | BIOS-Einträge erstellen: 60 Fälle verteilt über golden-core/edge-cases/chaos-mixed | 1 | Ground Truth | ✅ |
| 8 | PS1↔PS2↔PSP Disambiguation: 30 Einträge in edge-cases.jsonl | 1 | Ground Truth | ✅ |
| 9 | Neue Stub-Generatoren Phase 1: PSP-PVD, CSO, Directory-WiiU, Multi-File-CUE/GDI/M3U | 1 | C# Code | ✅ |
| 10 | `ManifestGenerator.cs` implementieren: automatische Manifest-Erzeugung aus JSONL | 1 | C# Code | ✅ |
| 11 | Arcade-Ausbau: 200 Einträge über 20 Subszenarien (golden-realworld + edge-cases + chaos) | 2 | Ground Truth | |
| 12 | Arcade-Stub-Generatoren: ZIP-Parent/Clone/Merged/Split/Non-Merged | 2 | C# Code | |
| 13 | Computer/PC-Ausbau: 150 Einträge für 10 Systeme inkl. Folder-Only + Directory-based | 2 | Ground Truth | |
| 14 | Computer-Stub-Generatoren: ADF, D64, TZX, DSK, ST, ATR | 2 | C# Code | |
| 15 | Multi-File/Multi-Disc: 80 Einträge (CUE+BIN, GDI, M3U, CCD, MDS, Multi-Disc 2–4) | 2 | Ground Truth | |
| 16 | CHD-RAW-SHA1: 25 Einträge mit CHD v5 embedded SHA1 | 2 | Ground Truth | |
| 17 | Tier-1-Auffüllung: je ≥20 pro Tier-1-System in golden-realworld | 3 | Ground Truth | |
| 18 | Chaos/Edge/Negative auffüllen: Headerless, Falsch benannt, Kaputte Sets, UNKNOWN-expected | 3 | Ground Truth | |
| 19 | Repair-safety und DAT-coverage finalisieren: 70 + 100 Einträge | 4 | Ground Truth | |
| 20 | Baseline-Snapshot `s1-baseline.json` schreiben + S1-Release-Gate aktivieren | 4 | C# Code + CI | |

---

## 10. Definition of Done

Nur messbare Kriterien. Jedes einzelne muss erfüllt sein, damit die Coverage-Matrix als "umgesetzt" gilt.

### System-Coverage

- [ ] **69/69 Systeme** aus `data/consoles.json` haben jeweils ≥1 Ground-Truth-Eintrag
- [ ] **Tier-1 (9 Systeme):** NES, SNES, N64, GBA, GB, GBC, MD, PS1, PS2 — je ≥20 Einträge
- [ ] **Tier-2 (16 Systeme):** 32X, PSP, SAT, DC, GC, WII, SMS, GG, PCE, LYNX, A78, A26, NDS, 3DS, SWITCH, AMIGA — je ≥8 Einträge
- [ ] **Tier-3 (22 Systeme):** je ≥3 Einträge
- [ ] **Tier-4 (22 Systeme):** je ≥2 Einträge

### Plattformfamilien

- [ ] **Cartridge:** ≥380 Einträge (Hard-Fail bei <320)
- [ ] **Disc:** ≥310 Einträge (Hard-Fail bei <260)
- [ ] **Arcade:** ≥200 Einträge über 20 Subszenarien (Hard-Fail bei <160)
- [ ] **Computer:** ≥150 Einträge für 10 Systeme (Hard-Fail bei <120)
- [ ] **Hybrid:** ≥80 Einträge für 5 Systeme (Hard-Fail bei <60)

### BIOS

- [ ] ≥60 BIOS-Einträge über ≥12 verschiedene Systeme
- [ ] BIOS-Szenarien B-01 bis B-12 alle repräsentiert
- [ ] Negativ-Test: "BioShock", "BIOS Agent" korrekt als Game erkannt

### Arcade

- [ ] ≥20 Parent-Sets, ≥15 Clone-Sets
- [ ] ≥15 BIOS-ZIPs (neogeo.zip, pgm.zip, naomi.zip etc.)
- [ ] ≥30 Split/Merged/Non-Merged ROM-Sets (≥10 pro Typ)
- [ ] ≥8 CHD-Supplement-Fälle
- [ ] ≥8 Neo Geo CD Disc-Fälle

### Redump / Disc

- [ ] PS1↔PS2↔PSP Disambiguation ≥30 Fälle
- [ ] GB↔GBC CGB-Varianten ≥12 Fälle
- [ ] MD↔32X Header-Ambiguität ≥8 Fälle
- [ ] SAT↔SCD↔DC Disc-Disambiguation ≥12 Fälle
- [ ] GC↔Wii Magic-Byte ≥8 Fälle
- [ ] Multi-File-Sets (CUE+BIN/GDI/M3U) ≥30 Fälle
- [ ] Multi-Disc ≥25 Fälle
- [ ] CHD-RAW-SHA1-Matching ≥8 Fälle

### Computer / PC

- [ ] Folder-Only-Detection ≥12 Fälle (DOS, CPC, PC98, X68K)
- [ ] Directory-based Games ≥10 Fälle
- [ ] TOSEC-DAT-Matching ≥10 Fälle

### Negative / UNKNOWN / Ambiguous

- [ ] Negative Controls ≥40 Fälle (100% Pass-Rate) — Nicht-ROM-Dateien nie als ROM erkannt
- [ ] UNKNOWN expected ≥30 Fälle — korrekte Unsicherheit
- [ ] Ambiguous acceptable ≥25 Fälle — acceptableAlternatives evaluiert

### Fallklassen

- [ ] Alle 20 Fallklassen (FC-01 bis FC-20) über Hard-Fail-Schwelle

### DAT-Ökosysteme

- [ ] No-Intro ≥25 Einträge
- [ ] Redump ≥25 Einträge
- [ ] MAME ≥15 Einträge
- [ ] TOSEC ≥10 Einträge

### CI / Automation

- [ ] `benchmark/gates.json` definiert alle Schwellen maschinenlesbar
- [ ] `CoverageValidator.cs` prüft gegen alle Gate-Schwellen
- [ ] `CoverageGateTests.cs` mit `[Trait("Category", "CoverageGate")]`
- [ ] `dotnet test --filter Category=CoverageGate` → 0 Failures
- [ ] `ManifestGenerator.cs` erzeugt Manifest automatisch aus JSONL
- [ ] `manifest.json` → `gates.s1MinimumViableBenchmark.overallStatus: "PASS"`
- [ ] Manifest-Konsistenz-Test prüft: Manifest-Zahlen == tatsächliche JSONL-Counts
- [ ] Schema-Validierung: alle JSONL-Zeilen valide gegen `ground-truth.schema.json`
- [ ] Baseline-Snapshot `s1-baseline.json` geschrieben
- [ ] Regressions-Gate aktiv: Wrong-Match-Rate ≤ Baseline + 0.1%

### Gesamtmetrik

- [ ] **≥1.200 Ground-Truth-Einträge** über 7 JSONL-Dateien
- [ ] **69/69 Systeme** abgedeckt
- [ ] **20/20 Fallklassen** über Gate
- [ ] **5/5 Plattformfamilien** über Gate
- [ ] **4/4 DAT-Ökosysteme** über Gate

---

## Requirements & Constraints (Referenz)

- **REQ-001**: 69/69 System-Coverage (100%).
- **REQ-002**: 20/20 Fallklassen (FC-01 bis FC-20) über Minimum.
- **REQ-003**: Tier-Tiefe: Tier-1 ≥20, Tier-2 ≥8, Tier-3 ≥3, Tier-4 ≥2.
- **REQ-004**: BIOS ≥60 über ≥12 Systeme.
- **REQ-005**: Arcade ≥200 über 20 Subszenarien.
- **REQ-006**: PS-Disambiguation ≥30.
- **REQ-007**: Multi-File-Sets ≥30.
- **REQ-008**: TOSEC-DAT ≥10.
- **REQ-009**: Negative Controls ≥40 mit 100% Pass-Rate.
- **REQ-010**: Headerless ROMs ≥20.
- **SEC-001**: Stub-Generatoren: Path-Traversal-Schutz (Root-Validierung).
- **SEC-002**: Keine echten ROM-Inhalte — nur synthetische Header + Padding.
- **CON-001**: Inkrementelle Phasen — Build zwischen Phasen grün.
- **CON-002**: JSONL: UTF-8 ohne BOM, LF, sortiert nach ID.
- **CON-003**: Gate-Schwellen in `gates.json`, nicht hardcodiert in Tests.
- **CON-004**: Manifest automatisch generiert, nicht manuell gepflegt.
- **GUD-001**: ID-Format: `{set-prefix}-{system}-{subclass}-{laufnummer}`.
- **PAT-001**: Fallklassen-Zuordnung per Tags (nicht per Datei).

---

## Alternatives

- **ALT-001**: Gate-Schwellen hardcodiert in C# statt `gates.json` → Verworfen: Nicht ohne Recompile änderbar; erschwert Phasenweise-Anpassung.
- **ALT-002**: Separate JSONL-Dateien pro System (69 Dateien) → Verworfen: Cross-System-Fälle können nicht sauber zugeordnet werden; 69 Dateien sind unhandlich.
- **ALT-003**: Separate JSONL für BIOS/Arcade → Verworfen: BIOS- und Arcade-Fälle verteilen sich natürlich über Referenz, Edge und Chaos; eigene Dateien würden Doppelzählung erzwingen.
- **ALT-004**: SQL-basierte Coverage-Validierung → Verworfen: Overkill für <5.000 Einträge.
- **ALT-005**: Manuelle Manifest-Pflege → Verworfen: Drift-Risiko zu hoch; automatische Generierung eliminiert Konsistenz-Probleme.

---

## Dependencies

- **DEP-001**: Basis-Plan `feature-benchmark-testset-1.md` Phasen 1–5 (Ordnerstruktur, Schema, 70 Einträge, StubGenerator, Evaluation-Runner).
- **DEP-002**: `data/consoles.json` mit 69 stabilen System-Keys (verifiziert: 65 Systeme in Datei, Schema passt).
- **DEP-003**: `ground-truth.schema.json` muss `tags`, `fileModel`, `relationships` unterstützen.
- **DEP-004**: StubGenerator erweiterbar per `stub.generator`-Dispatch.
- **DEP-005**: Expansion-Plan `feature-benchmark-coverage-expansion-1.md` definiert Task-Sequenz (E1–E4).

---

## Files

### Neue Dateien

| # | Datei | Beschreibung |
|---|-------|-------------|
| FILE-001 | `benchmark/gates.json` | Gate-Schwellen als maschinenlesbare Konfiguration |
| FILE-002 | `benchmark/generators/CoverageValidator.cs` | Prüft Coverage gegen alle Matrix-Gates |
| FILE-003 | `benchmark/generators/ManifestGenerator.cs` | Erzeugt manifest.json aus JSONL |
| FILE-004 | `benchmark/generators/FallklasseClassifier.cs` | Tag→Fallklasse Mapping |
| FILE-005 | `benchmark/generators/PlatformFamilyClassifier.cs` | consoleKey→Familie Mapping |
| FILE-006 | `src/RomCleanup.Tests/Benchmark/CoverageGateTests.cs` | xUnit Coverage-Gate-Tests |
| FILE-007 | `benchmark/baselines/s1-baseline.json` | Metrik-Snapshot nach S1 |

### Bestehende Dateien (erweitert)

| # | Datei | Änderung |
|---|-------|---------|
| FILE-008 | `benchmark/ground-truth/golden-core.jsonl` | 70 → 250 Einträge |
| FILE-009 | `benchmark/ground-truth/golden-realworld.jsonl` | 0 → 350 Einträge |
| FILE-010 | `benchmark/ground-truth/edge-cases.jsonl` | 0 → 150 Einträge |
| FILE-011 | `benchmark/ground-truth/chaos-mixed.jsonl` | 0 → 200 Einträge |
| FILE-012 | `benchmark/ground-truth/negative-controls.jsonl` | 0 → 80 Einträge |
| FILE-013 | `benchmark/ground-truth/repair-safety.jsonl` | 0 → 70 Einträge |
| FILE-014 | `benchmark/ground-truth/dat-coverage.jsonl` | 0 → 100 Einträge |
| FILE-015 | `benchmark/manifest.json` | Erweitert um `coverage`- und `gates`-Block |
| FILE-016 | `benchmark/generators/StubGenerator.cs` | +30 neue Generator-Methoden |

---

## Testing

| # | Test | Kategorie | Beschreibung |
|---|------|----------|-------------|
| TEST-001 | `AllPlatformFamilyGates_AreMet` | CoverageGate | 5 Familien + systemübergreifend über Hard-Fail |
| TEST-002 | `AllCaseClassGates_AreMet` | CoverageGate | 20 Fallklassen über Hard-Fail |
| TEST-003 | `AllTierDepthGates_AreMet` | CoverageGate | 4 Tiers × alle Systeme über Mindesttiefe |
| TEST-004 | `AllSpecialAreaGates_AreMet` | CoverageGate | BIOS, Arcade, PS-Disambig, DAT-Ökosysteme etc. |
| TEST-005 | `S1_AllGatesMet` | CoverageGate | Aggregiert alle Sub-Gates; Release-Gate |
| TEST-006 | `Manifest_IsConsistentWithGroundTruth` | CoverageGate | Manifest-Zahlen == JSONL-Counts |
| TEST-007 | `AllIds_AreUnique` | CoverageGate | Keine Duplikate über alle JSONL |
| TEST-008 | `AllEntries_ValidateAgainstSchema` | CoverageGate | Schema-Validierung |
| TEST-009 | `GatesJson_IsValid` | CoverageGate | gates.json syntaktisch korrekt, alle Felder vorhanden |

---

## Risks & Assumptions

- **RISK-001**: Handpflege von 1.200 JSONL-Einträgen ist fehleranfällig. Mitigation: Schema-Validierung + ID-Uniqueness + Manifest-Konsistenz als CI-Gate.
- **RISK-002**: Gate-Schwellen zu streng → blockiert Releases. Mitigation: Hard-Fail liegt 15–25% unter Target.
- **RISK-003**: Arcade-ZIP-Generatoren komplex (CRC32-Steuerung). Mitigation: Unit-Tests pro Generator; bei Fehlschlag als `known-limitation` taggen.
- **RISK-004**: Einige Fälle (CHD v4/v5, WIA/RVZ) könnten Detection-Pipeline-intern nicht über Minimal-Stubs erreichbar sein. Mitigation: Extension+Folder-Fallback prüfen; donated Samples als Ausweich.

- **ASSUMPTION-001**: 69 System-Keys in `data/consoles.json` bleiben stabil.
- **ASSUMPTION-002**: Detection-Pipeline akzeptiert Minimal-Stubs mit korrekten Header-Bytes.
- **ASSUMPTION-003**: Tags-basierte Fallklassen-Zuordnung ist eindeutig genug (Double-Counting akzeptabel).

---

## Related Specifications

- [docs/COVERAGE_GAP_AUDIT.md](../docs/COVERAGE_GAP_AUDIT.md) — Quell-Matrix (§10)
- [docs/GROUND_TRUTH_SCHEMA.md](../docs/GROUND_TRUTH_SCHEMA.md) — JSONL-Schema
- [docs/TESTSET_DESIGN.md](../docs/TESTSET_DESIGN.md) — Dataset-Klassen
- [docs/RECOGNITION_QUALITY_BENCHMARK.md](../docs/RECOGNITION_QUALITY_BENCHMARK.md) — Metriken M1–M16
- [plan/feature-benchmark-testset-1.md](feature-benchmark-testset-1.md) — Basis-Plan
- [plan/feature-benchmark-coverage-expansion-1.md](feature-benchmark-coverage-expansion-1.md) — Ausbauplan E1–E4
