# Top 20 strategic recommendations (Romulus / Strategic Reduction 2026)

This file is the canonical machine-readable mirror of the Top-20 strategic
recommendations from the executive review. It lives alongside `plan.yaml`
and is used by **T-W2-COVERAGE-GAP-CHECK** to verify, on every CI run, that
every recommendation is covered by at least one task and that every
`covers:` reference in `plan.yaml` points to an existing TOP-N here.

Format contract (do not break without updating
`Wave2CoverageGapTests.cs`):

* Each item is introduced by a `TOP-N` token where `N` is the strategic
  rank from the executive review.
* `N` runs `1..20` and is mandatory: missing one will fail CI.

---

## TOP-1: GUI-Plattform fixieren (Avalonia vs WPF)
Avalonia archivieren, WPF behalten. Kein paralleler Cross-Plattform-Pfad
ohne harten Bedarf. Eindeutig dokumentieren.

## TOP-2: User-Trust und Release-Reife durch echte Nutzer
Echte Beta-Nutzer (kein Selbstmarketing) plus harte Regression-Invarianten
fuer Move/Convert; jede neue Welle muss durch Paritaets-Tests verteidigt
sein.

## TOP-3: README/Positionierung ehrlich machen
Marketing-Floskeln raus, tatsaechliche Identitaet ("safer ROM cleanup
helper") rein.

## TOP-4: GUI-Komplexitaet hart reduzieren
Tabs/Tools, die niemand misst und die Fehlbedienung ermoeglichen, fliegen
oder werden hinter Expert-Mode verlegt.

## TOP-5: Feature-Wildwuchs eindaemmen
i18n-Orphans, FeatureCommandService-Leichen, ToolCards ohne Owner und
ungenutzte Befehle entfernen.

## TOP-6: First-Class Audit-Viewer
Sidecars + Ledger werden read-only sichtbar (GUI/CLI/API), nicht nur
Dateien auf Platte.

## TOP-7: Review-Inbox als zentrale Triage
Alle Blocked/Review/Unknown-Lanes laufen in eine konsolidierte Inbox
zusammen, statt verstreut in Tabs.

## TOP-8: Entscheidungs-Erklaerbarkeit (Decision Explainer)
"Warum hat Variante X gewonnen?" sichtbar pro Group; basiert auf
WinnerReasonTrace.

## TOP-9: DAT-first als Default-Policy
Hash-/DAT-Treffer schlagen Heuristik. Heuristik nur als opt-in
Best-Effort-Modus, sichtbar markiert.

## TOP-10: Konvertierungs-Sicherheit (Lossy-Schutz)
Lossy-Pfade nur mit explizitem AcceptDataLossToken, kein Datenverlust
durch Default.

## TOP-11: Before/After-Simulator
Plan vs. Status vergleichen, bevor Move/Convert ausgefuehrt wird.

## TOP-12: GUI-Konsolidierung der Aktionen
Move/Convert-Aktionen sind nur ueber definierte Bestaetigungs-Pfade
erreichbar, keine Schattenpfade.

## TOP-13: FeatureCommandService-Konsolidierung
Eine Quelle der Wahrheit pro Befehl, keine doppelten Registrierungen,
keine ToolCards ohne Command.

## TOP-14: Konsolen-Coverage und Telemetrie (opt-in)
Reale Konsolenliste pflegen; Telemetrie strikt opt-in, anonymisiert,
nicht release-blockierend.

## TOP-15: Report-Vereinheitlichung
HTML-, JSON- und CSV-Reports bilden dieselbe fachliche Wahrheit ab.

## TOP-16: HealthScore als zentrale Sammlungs-KPI
Stabile, deterministische KPI ueber Zustand der Sammlung; basiert auf
Audit-/Run-Daten.

## TOP-17: Provenance-Trail (per ROM)
Jedes ROM hat eine append-only Historie ueber Run-Grenzen hinweg;
abgegrenzt vom Audit-Sidecar.

## TOP-18: Multi-DAT-Aufloesung
Mehrere DAT-Quellen pro Konsole konsistent aufloesen; klare
Konflikt-Strategie.

## TOP-19: Policy-Governance
Regelwerk-Aenderungen sind nachvollziehbar (Aenderung, Owner, Datum,
Begruendung). Kein wildes Drift in `data/rules.json`.

## TOP-20: Cross-Cutting Verification + Identity Guardrail
Jede neue Welle wird gegen Schattenlogik, Determinismus, Safety,
Audit-Atomicity gepruef; Identitaets-Frage als PR-Pflichtpruefung.
