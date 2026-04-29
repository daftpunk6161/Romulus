<!--
PR-Template Romulus.
Bitte alle Abschnitte ausfuellen oder begruendet streichen.
-->

## Was aendert sich?

<!-- Eine bis drei Saetze, was diese PR fachlich aendert. -->

## Warum?

<!-- Bezug zu Issue, Plan-Task (z. B. T-W2-XXX) oder Bugfix. -->

## Audit-Moratorium-Check

> Im Geltungszeitraum **2026-04-28 bis 2026-05-28** (Wellen 1-3 der strategischen Reduktion) gilt ein hartes Moratorium gegen neue Audit-Dokumente, Findings-Tracker und Repo-wide Deep-Dives. Details siehe Abschnitt "Audit-Moratorium" in [AGENTS.md](../AGENTS.md).

- [ ] Diese PR fuegt **keine** neuen Audit-/Findings-/Tracker-/Deep-Dive-Dokumente hinzu.
- [ ] Falls doch: Die PR adressiert einen konkreten **P1-Sicherheits- oder Datenintegritaets-Befund** und liefert direkt den Fix (kein Sammeldokument). Begruendung:

<!-- Falls die zweite Box angekreuzt ist, hier kurz begruenden. Sonst leer lassen. -->

## Architektur-Check

- [ ] Aenderung respektiert die Schichten-Richtung Entry Points -> Infrastructure -> Core -> Contracts.
- [ ] Keine Businesslogik im WPF Code-Behind, keine I/O in `Romulus.Core`.
- [ ] Keine konkurrierenden Wahrheiten (Status / KPIs / Results / Reports) eingefuehrt.

## Test-Nachweis

<!-- Welche Tests sichern die Aenderung ab? Pin-Tests, Regressionstests, Invarianten? -->

## Risiken / Hinweise

<!-- Bekannte Risiken, Folgearbeiten, Migrationsschritte. -->
