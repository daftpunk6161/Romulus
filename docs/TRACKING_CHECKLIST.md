# Tracking Checklist (RomCleanup)

## Release-Blocker
- [x] HealthScore-Formel als Single Source of Truth zentralisiert
- [x] API/CLI/GUI auf gemeinsame RunProjection-KPIs ausgerichtet
- [x] Tote KPI-Logik (`RunResultSummary`) entfernt

## GUI/UX (WPF/XAML)
- [x] IsValidTransition-Duplikat durch gemeinsame RunStateMachine ersetzt
- [x] Dashboard-KPIs in `ApplyRunResult` auf RunProjection umgestellt
- [x] Wizard/Flow: Roots -> Optionen -> Preview -> Confirm -> Run -> Report/Undo

## Core/Engine
- [x] `HealthScorer` in Core eingeführt
- [x] `RunProjectionFactory` nutzt zentrale Scoring-Logik
- [x] Determinismus über neue RunProjection-Tests abgesichert

## IO/Safety
- [x] Report-Summary weiter aus zentraler Projektion gespeist
- [ ] Vollständige OpenAPI-Dokumentation aller neuen API-KPI-Felder

## Performance
- [x] Keine zusätzlichen teuren Scans in GUI-KPI-Berechnung (Projection-Reuse)
- [ ] Weitere Hotspot-Prüfung Scan/Hashing

## Tests (keine Alibi-Tests)
- [x] `RunProjectionFactoryTests` hinzugefügt
- [x] `RunReportWriterTests` hinzugefügt
- [x] API-Integrationstest für neue Ergebnisfelder hinzugefügt
- [x] Kompletter `dotnet test src/RomCleanup.sln` Lauf lokal ausführen

## Backlog
- [ ] Report/CLI/API/OpenAPI-Dokumentationsparität vollständig automatisiert prüfen
- [ ] Weitere Channel-Parity-Tests (CLI vs API vs Report) für alle KPI-Felder