# Beta-Smoke-Protocol (T-W3-RUN-SMOKE-WITH-USERS)

## Zweck

Operative Vorbereitung für T-W3-RUN-SMOKE-WITH-USERS. Diese Datei
beschreibt **wie** der Smoke abläuft und liefert die Vorlagen für
Beobachtung und Auswertung. Sie ist kein Beleg für erfolgte Durchläufe.
Plan-Status `T-W3-RUN-SMOKE-WITH-USERS` bleibt `pending`, bis unten
mindestens 5 reale Run-Einträge mit Friktionspunkten + Severity + Owner
stehen.

## Vorbedingungen

- T-W3-BETA-USERS Cohort hat mind. 5 Einträge (siehe
  `beta-recruiting-playbook.md`).
- Tester hat Test-Kopie seiner Sammlung (oder Backup nachweislich).
- Romulus-Build läuft auf Tester-Maschine; Erst-Start-Wizard war
  erfolgreich.
- Tester hat dem Beobachtungs-Modus zugestimmt (Voice-Channel zum
  Mitlauschen; Screen-Recording **nur** mit explizitem Opt-In).

## Kernregeln für den Beobachter (Maintainer)

1. **Kein Coaching.** Wenn der Tester hängt, nicht eingreifen. Notieren,
   warten. Eingreifen erst, wenn der Tester ausdrücklich um Hilfe bittet
   ODER eine destruktive Aktion droht (z.B. Live-Sammlung statt Test-Kopie).
2. **Think-Aloud aktiv halten.** Wenn der Tester verstummt: "Was denkst du
   gerade?" — keine inhaltliche Frage stellen.
3. **Friktion sofort notieren** mit Zeitstempel + Schritt + Severity.
4. **Keine Korrektur der eigenen Software live.** Bug = Notiz, nicht Hotfix.
5. Maximal 90 min am Stück. Bei Überschreitung Pause oder Abbruch.

## Severity-Skala

| Stufe | Bedeutung | Reaktion |
| --- | --- | --- |
| **P1** | Workflow blockiert oder Datenverlust droht | sofort als Issue, blockiert weitere Smokes bis Fix |
| **P2** | Schwere Verwirrung, Workflow geht weiter, Tester bleibt unsicher | Issue im Backlog, vor Wave-4-Code adressieren |
| **P3** | Reibung, kosmetische Klarheit, Workflow läuft | Issue als „polish"-Marker, nicht release-blockend |

P1 ist immer ein Fund — wenn ein Smoke ohne P1 endet, ist das **nicht**
automatisch Erfolg, sondern muss aktiv begründet werden ("kein P1, weil
Workflow nachweislich von Anfang bis Ende ohne Coach-Eingriff lief").

## Smoke-Skript (8 Schritte, je Schritt Beobachtung)

### Schritt 1: Add Library

- Tester öffnet Romulus, fügt Library-Pfad hinzu.
- Beobachtungspunkte:
  - Findet er den Einstiegspunkt ohne Suchen?
  - Versteht er den Unterschied Library-Pfad vs. Output-Pfad?
  - Bemerkt er den Hinweis auf Test-Kopie / Backup?

### Schritt 2: Scan

- Tester startet den Scan.
- Beobachtungspunkte:
  - Ist der Fortschritt nachvollziehbar?
  - Versteht er die Tier-Markierung (Top-30 vs. Best-Effort)?
  - Hat er nach 5 min Geduld verloren?

### Schritt 3: Verify (DAT)

- Tester löst DAT-Verifikation aus.
- Beobachtungspunkte:
  - Versteht er den Unterschied DAT-Match / Heuristik?
  - Erkennt er den Best-Effort-Marker (ADR-0023)?
  - Wie reagiert er auf "Unmatched"-Treffer?

### Schritt 4: Plan (Preview)

- Tester sieht den Run-Plan.
- Beobachtungspunkte:
  - Versteht er, was Move / Convert / Skip bedeutet?
  - Sieht er die Winner-Reasons (T-W2-SCORING-REASON-TRACE)?
  - Versteht er den Lossy-Block, falls vorhanden?
  - Findet er den Audit-Hinweis "kein Schreibvorgang in dieser Phase"?

### Schritt 5: Confirm (Token)

- Tester bestätigt mit getipptem Token (MOVE / CONVERT) und ggf.
  AcceptDataLossToken.
- Beobachtungspunkte:
  - Empfindet er den Tipp-Schutz als sinnvoll oder nervig?
  - Hat er die Lossy-Warnung ernst genommen?
  - Hat er versucht, den Token zu umgehen?

### Schritt 6: Execute

- Tester startet die echte Ausführung.
- Beobachtungspunkte:
  - Ist der Fortschritt zwischen Move / Convert / Rename klar getrennt?
  - Versteht er den Audit-Sidecar-Hinweis?
  - Bricht er ab? Wenn ja: an welcher Stelle und warum?

### Schritt 7: Report

- Tester sieht den Report.
- Beobachtungspunkte:
  - Versteht er KPI-Block (Winner / Loser / Skipped / Failed)?
  - Findet er den Pfad zum Audit-Sidecar?
  - Welchen Wert hat der Report aus seiner Sicht?

### Schritt 8: Rollback-Test

- Tester rollt **mindestens eine** Aktion zurück.
- Beobachtungspunkte:
  - Findet er den Rollback-Einstieg?
  - Vertraut er der Rollback-Garantie?
  - Validiert er das Ergebnis selbstständig (z.B. Datei wieder am
    Ursprungspfad)?

## Beobachtungsbogen pro Run

```yaml
- run_id: R-001
  beta_id: B-001          # Verweis auf Cohort
  date_utc: ""            # ISO-8601
  duration_minutes: 0
  collection_size_at_start: 0
  consoles_in_scope: []
  test_copy_used: true|false
  recording_consent: false
  steps:
    - step: 1
      name: Add Library
      completed_unaided: true|false
      time_minutes: 0
      friction:
        - { severity: P1|P2|P3, summary: "", owner: "" }
    - step: 2
      name: Scan
      completed_unaided: true|false
      time_minutes: 0
      friction: []
    - step: 3
      name: Verify
      completed_unaided: true|false
      time_minutes: 0
      friction: []
    - step: 4
      name: Plan
      completed_unaided: true|false
      time_minutes: 0
      friction: []
    - step: 5
      name: Confirm
      completed_unaided: true|false
      time_minutes: 0
      friction: []
    - step: 6
      name: Execute
      completed_unaided: true|false
      time_minutes: 0
      friction: []
    - step: 7
      name: Report
      completed_unaided: true|false
      time_minutes: 0
      friction: []
    - step: 8
      name: Rollback
      completed_unaided: true|false
      time_minutes: 0
      friction: []
  overall_completed_unaided: true|false
  coach_interventions: []
  data_loss_or_unintended_change: false
  audit_entry_findable_by_user: true|false
  user_self_assessment: ""    # 1-2 Sätze, Zitat
  observer_summary: ""        # Beobachter-Fazit, max. 5 Sätze
```

## Aggregations-Pflicht (vom Maintainer nach jedem Run)

1. Friktionspunkte werden in den globalen Backlog gespiegelt
   (`docs/plan/strategic-reduction-2026/beta-friction-backlog.md` —
   anzulegen, sobald erster echter Run vorliegt).
2. Jede P1 hat innerhalb von 24 h einen Owner.
3. Wave-4-Code-Tasks werden gegen den Friktionsbacklog priorisiert,
   nicht gegen Vermutungen des Maintainers.

## Run-Ergebnisse

```yaml
runs:
  []   # leer; T-W3-RUN-SMOKE-WITH-USERS bleibt pending bis hier
       # mindestens 5 Einträge stehen, alle mit ausgefüllten Friktionsblöcken
       # (auch wenn leer — explizit "[]" mit Begründung im observer_summary).
```

## Annahme-Kriterium für T-W3-RUN-SMOKE-WITH-USERS = done

- [ ] 5 Run-Einträge vorhanden.
- [ ] Jeder Run hat alle 8 Schritte beobachtet (kein Schritt ausgelassen).
- [ ] Friktion mit Severity + Owner.
- [ ] Mind. 4 von 5 Runs schließen `overall_completed_unaided: true` ab,
      sonst muss T-W1-UI-REDUCTION reopened werden.
- [ ] Friktionsbacklog existiert und ist verlinkt.
- [ ] Validation-Matrix (siehe plan.yaml) ist abgeglichen — jeder
      `expected_result` ist pro Run mit `met` / `not met` markiert.

Erst dann darf gem-critic in T-W3-PHASE1-GATE die Welle 3 als grün
durchwinken.
