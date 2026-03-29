# ADR-0019: Console Detection Data Integrity Audit & Fixes

## Status
Accepted

## Date
2026-06-26

## Context
Die Konsolen-Erkennung (ConsoleDetector) ist eine Kernfunktion von Romulus. Nach der massiven Daten-Expansion (65→162 Konsolen, ~157 DAT-Einträge) wurde ein systematischer Audit durchgeführt, um die Datenintegrität in `consoles.json`, `console-maps.json` und dem Benchmark-System zu verifizieren.

## Decision Drivers
- `_uniqueExtMap` in ConsoleDetector ist **first-write-wins** — die erste Konsole in der JSON, die eine Extension als `uniqueExt` beansprucht, gewinnt
- `_folderMap` ist **last-write-wins** — die letzte Konsole mit einem Alias überschreibt die vorherige
- Diese gegensätzlichen Semantiken erfordern, dass Konflikte in den Daten präventiv eliminiert werden

## Findings

### Kritisch: UniqueExt-Konflikte (behoben)
| Extension | Konsolen | Auswirkung |
|-----------|----------|------------|
| `.3ds` | 3DS, NEW3DS | NEW3DS per Extension unerreichbar |
| `.cia` | 3DS, NEW3DS | NEW3DS per Extension unerreichbar |
| `.nds` | NDS, NDSI | NDSI per Extension unerreichbar |

**Fix**: NEW3DS `.3ds`/`.cia` → `ambigExts`. NDSI `.nds` → `ambigExts`, `.dsi` bleibt `uniqueExt`.

**Begründung**: NEW3DS ist abwärtskompatibel zu 3DS (gleiche Formate). DSi ist abwärtskompatibel zu NDS. Die Original-Konsole (3DS/NDS) soll die Extension besitzen. Backward-kompatible Konsolen nutzen Folder/DAT/Keyword-Erkennung.

### Kritisch: Folder-Alias-Konflikte (behoben)
| Alias | Konsolen (vorher) | Gewinner (last-write) | Fix |
|-------|-------------------|----------------------|-----|
| `mame` | ARCADE, MAME | MAME | Entfernt aus ARCADE |
| `e-amusement` | KSITE, EAMUSE | EAMUSE | Entfernt aus KSITE |
| `videopac` | ODYSSEY2, VIDEOPAC | VIDEOPAC | Entfernt aus ODYSSEY2 |

### Mittel: console-maps.json Duplikat-Schlüssel (behoben)
| Key | Mapping 1 | Mapping 2 | Fix |
|-----|-----------|-----------|-----|
| `msx2` | MSX (Zeile 151) | MSX2 (Zeile 323) | Zeile 151 entfernt |
| `e-amusement` | KSITE (Zeile 240) | EAMUSE (Zeile 321) | Zeile 240 entfernt |

Zusätzlich: `mame` → MAME (statt ARCADE), `videopac` → VIDEOPAC (statt ODYSSEY2).

### Info: console-maps.json ist Legacy-Only
Bestätigt per grep: `console-maps.json` wird nur von Legacy-PowerShell-Code referenziert, nicht vom C#-Core.
Der C#-ConsoleDetector liest ausschließlich `consoles.json`.

### Info: Übrige Unique-vs-Ambig Overlaps
| Extension | Unique | Ambig | Risiko |
|-----------|--------|-------|--------|
| `.d64` | C64 | PLUS4 | Akzeptabel — C64 besitzt, PLUS4 als Ambig-Kandidat |

### Info: Benchmark-Abdeckung
NEW3DS und NDSI sind **nicht** im DatasetExpander.BuildSystemCatalog() enthalten: Kein Ground-Truth-Test.
Akzeptiert im Moment, da beide nur per Folder/DAT erkannt werden und keine uniqueExts ohne Konflikt haben.

## Architectural Notes

### ConsoleDetector Map-Semantiken
```
_uniqueExtMap  → first-write-wins  (ContainsKey guard)
_folderMap     → last-write-wins   (direct assignment)
_ambigExtMap   → all-writers-win   (List<string> collection)
```

Diese Semantiken sind beabsichtigt:
- **UniqueExt first-win**: Die "kanonische" Konsole für ein Format soll stabil bei JSON-Reihenfolge bleiben
- **FolderMap last-win**: Neuere/spezifischere Konsolen-Definitionen können generische Alias-Besitzer übersteuern
- **AmbigExt all-wins**: Alle Kandidaten werden gesammelt, HypothesisResolver entscheidet

### Confidence Pipeline
```
DatHash=100 > UniqueExt=95 > DiscHeader=92 > CartridgeHeader=90 > 
SerialNumber=88 > FolderName=85 > ArchiveContent=80 > Keyword=75 > AmbigExt=40
```

Multi-Source-Bonus: +15. Soft-only Cap: 75. Conflict-Penalty: -20 bei starkem Runner-up (≥80).

## Consequences
- **Alle uniqueExts sind eindeutig** — keine stille Übersteuerung durch JSON-Reihenfolge
- **Alle folderAliases sind eindeutig** — keine stille Übersteuerung durch last-write-wins
- **console-maps.json bereinigt** — keine Duplikat-JSON-Keys mehr
- **6663/6663 Tests bestehen** — keine Regressionen
- **Baseline-Metriken unverändert** — WrongMatchRate=0%, UnsafeSortRate=0%

## Verification
```
Build:     0 Fehler, 0 Warnungen
Tests:     6663 bestanden, 0 fehlgeschlagen
UniqueExt: Keine Konflikte
Aliases:   Keine Konflikte
Benchmark: Alle Quality Gates bestanden
```
