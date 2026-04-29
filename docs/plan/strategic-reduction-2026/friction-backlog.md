# Friktions-Backlog (Beta-Discovery)

> **Zweck:** Sammelpunkt für Reibungspunkte aus realen Beta-Smokes
> ([beta-smoke-protocol.md](beta-smoke-protocol.md)) und dem
> wöchentlichen Discovery-Loop ([discovery-loop-playbook.md](discovery-loop-playbook.md)).
>
> **Status heute (2026-04-29):** leer — keine reale Cohort, keine
> beobachteten Smokes. Datei existiert als Skelett, damit der
> Discovery-Loop-Owner sofort eintragen kann sobald T-W3-PHASE1-GATE
> aufgelöst ist.

## Severity-Skala (verbindlich)

- **P1:** Datenverlust, blockierende Fehlbedienung, Workflow-Stopp ohne
  Selbstheilung. → Owner-Zuweisung innerhalb 24h, Issue Pflicht.
- **P2:** schwere Verwirrung, Workflow nur mit Coach-Eingriff fortsetzbar,
  keine Datenintegrität verletzt. → Owner-Zuweisung innerhalb 1 Woche,
  Issue Pflicht.
- **P3:** Reibung, Verbesserungswunsch, kosmetisch. → monatliche Sichtung,
  optionales Issue.

P1 darf nie zu P2/P3 herabgestuft werden „weil ein Workaround existiert".

## Eintrags-Schema

```yaml
- id: F-001                     # fortlaufend, nicht wiederverwendet
  severity: P1|P2|P3
  observed_in:
    run_id: ""                  # aus beta-smoke-protocol Beobachtungsbogen
    beta_id: ""                 # aus beta-recruiting-playbook Cohort-Tracker
    smoke_step: 0               # 1..8 aus 8-Schritt-Skript
    date_utc: ""                # YYYY-MM-DD
  summary: ""                   # eine Zeile
  description: |
    Mehrzeilig: was passierte, was war erwartet, wo brach die Erwartung
  reproduction:
    deterministic: true|false
    steps: []
  affected_components: []       # z.B. ["RunPipeline", "DangerConfirmDialog"]
  user_impact: ""               # konkret: was konnte der Nutzer NICHT tun
  owner: "TBD"                  # Person (nicht Rolle), benannt
  status: open|in-progress|closed|wontfix-with-reason
  status_reason: ""             # bei wontfix Pflicht
  linked_issue: ""              # GitHub-Issue-URL falls eroeffnet
  closed_in_commit: ""          # falls status=closed
  follow_up_test: ""            # Pin-Test der die Regression verteidigt
```

## Backlog (chronologisch nach observed_in.date_utc)

```yaml
items: []
```

## Aggregations-Regeln

Owner Discovery-Loop trägt jeden Freitag im
[discovery-loop-tracker.md](discovery-loop-playbook.md#kpi-tracker-schema)
folgende Aggregate ein:

- `p1_open` / `p1_closed`: Zählung dieser Datei mit `severity: P1`
- `p2_open` / `p2_closed`: Zählung dieser Datei mit `severity: P2`

Kein offener P1 in der zur Stabilitäts-Bewertung herangezogenen Woche
ist eine harte Voraussetzung für T-W4-DISCOVERY-LOOP = `done` (siehe
Annahme-Kriterium dort).

## Anti-Patterns (verboten)

- P1-Eintrag still nach P3 verschieben, weil „kein Owner gefunden".
- `status: wontfix-with-reason` ohne ausgefüllten `status_reason`.
- Friktion ohne `owner` länger als 1 Woche stehen lassen (außer P3).
- Sammel-Eintrag „diverse Probleme bei Schritt 3" — jede Friktion
  bekommt ihre eigene F-Nummer.
- `closed`-Markierung ohne `closed_in_commit`-Verweis.
