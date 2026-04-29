# ADR-0023: DAT-First-Policy als verbindliches Default-Verhalten

## Status
Accepted

## Datum
2026-04-29

## Owner
daftpunk6161 (Repo-Maintainer)

## Bezug
- Plan: `docs/plan/strategic-reduction-2026/plan.yaml` (Task `T-W2-DAT-FIRST-ADR`)
- Architektur-Vorlauf: ADR-0021 "DAT-First Conservative Recognition Architecture" (Status: Proposed)
- AGENTS.md: Determinismus + "Eine fachliche Wahrheit"

ADR-0021 beschreibt die Architektur einer DAT-getriebenen Erkennungs-Pipeline. Diese ADR-0023 setzt die **Policy** dazu: Welcher Modus ist Default, wann darf Heuristik einspringen, wie wird die Unsicherheit nach aussen sichtbar.

## Kontext

Romulus erkennt Konsolen ueber zwei prinzipielle Wege:
1. **DAT-Lookup** — Hash-/Strukturmatch gegen kuratierte DAT-Kataloge (No-Intro / Redump / TOSEC). Ergebnis hat harte Beweisstaerke (Tier 0/1) und ist deterministisch reproduzierbar.
2. **Heuristik** — Folder-, Extension-, Filename-Keyword-, Cartridge-/Disc-Header-Inferenz. Ergebnis hat weiche Beweisstaerke (Tier 2/3), kann false-positives produzieren und ist abhaengig von Eingabe-Konventionen.

In bisherigen Releases wurde die Heuristik in mehreren Pfaden implizit als gleichwertiger Fallback eingesetzt. Das fuehrte zu False-Positives, die vom Nutzer schwer von echten DAT-Treffern unterscheidbar waren.

## Entscheidung

**1. Default ist DAT-first.**
Die Pipeline akzeptiert ein Erkennungs-Ergebnis fuer Sort-Entscheidungen (Move/Convert/Rename) standardmaessig nur dann, wenn mindestens eine harte Evidenz-Quelle (Tier 0 oder Tier 1: `DatHash`, `DiscHeader`, `CartridgeHeader`, `SerialNumber`) zum Konsolen-Schluss beigetragen hat.

**2. Heuristik nur als opt-in Best-Effort-Modus.**
Heuristische Erkennung (`FolderName`, `UniqueExtension`, `AmbiguousExtension`, `ArchiveContent`, `FilenameKeyword`) darf eine Konsolen-Zuordnung fuer Move/Convert/Rename nur tragen, wenn der Aufrufer das explizit anfordert. Vertrags-Kante:

- `Romulus.Contracts.Models.RunOptions.AllowHeuristicFallback` (bool, Default `false`).
- Wird der Flag nicht gesetzt, landen rein heuristisch erkannte Items im `_REVIEW`-Lane und nicht in der finalen Konsolen-Sortierung.

**3. Sichtbare Markierung des Best-Effort-Status.**
`Romulus.Core.Classification.ConsoleDetectionResult` exponiert die Computed-Property `IsBestEffort`. Sie ist `true` genau dann, wenn das Resultat ohne harte Evidenz zustande kam (`HasHardEvidence == false`). GUI, CLI, API und Reports muessen Best-Effort-Resultate sichtbar als solche markieren.

**4. Single Source of Truth bleibt der Detektor.**
GUI/CLI/API leiten den Best-Effort-Status nicht selbst ab und berechnen ihn nicht parallel neu. Sie lesen `ConsoleDetectionResult.IsBestEffort`. Konkurrierende Wahrheiten sind verboten (siehe AGENTS.md).

## Konsequenzen

### Positiv
- False-Positive-Konsolen-Zuordnung ohne harte Evidenz wird per Default verhindert.
- Nutzer mit DAT-Sammlung bekommen den konservativen Modus geschenkt.
- Nutzer ohne DAT-Sammlung (Best-Effort-Workflow) bekommen ein klares, dokumentiertes Opt-In, statt heimlich anders zu laufen.
- GUI/CLI/API/Reports koennen Best-Effort-Items einheitlich kennzeichnen, weil der Status aus einer Quelle kommt.

### Negativ / Aufwand
- Bestandskonsumenten, die bisher implizit auf heuristische Erkennung gesetzt haben, muessen `AllowHeuristicFallback = true` explizit setzen.
- UI muss Best-Effort-Flag sichtbar abbilden (kommt im Decision-Drawer in Welle 4).

### Folge-Tasks
- T-W4-DECISION-EXPLAINER: zeigt `IsBestEffort` im Decision-Drawer.
- T-W2-CONSOLE-CATALOG-UI (deferred aus Welle 1): visualisiert `tier=core` vs `best-effort` Konsolen.

## Verbote (wegen "keine konkurrierende Wahrheit")

- Keine zweite Property in einem ViewModel, die "ist heuristisch" parallel berechnet.
- Kein heimlicher Fallback in Service-X, der `AllowHeuristicFallback` umgeht.
- Keine Heuristik-Aktivierung in Defaults (`data/defaults.json`) ohne Aenderung dieser ADR.

## Reaktivierungs-Bedingung

Diese ADR wird nur dann neu verhandelt, wenn:
- die DAT-Coverage signifikant steigt und Best-Effort-Modus dadurch obsolet wird, **oder**
- ein neuer harter Evidenz-Tier (z. B. eingebettete kryptografische Header) eingefuehrt wird, der die Tier-Hierarchie veraendert.

Bis dahin ist die Policy bindend.

## Pin-Test-Absicherung

`Wave2DatFirstPolicyTests` (in `src/Romulus.Tests/`) sichert dauerhaft:
- `RunOptions.AllowHeuristicFallback` existiert mit Default `false`.
- `ConsoleDetectionResult.IsBestEffort` ist `true`, wenn `HasHardEvidence == false`.
- Diese ADR-Datei existiert mit `Status: Accepted` und benennt die DAT-first Default-Regel sowie den Opt-In-Flag.
