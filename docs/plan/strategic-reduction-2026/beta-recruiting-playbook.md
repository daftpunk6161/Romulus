# Beta-Recruiting-Playbook (T-W3-BETA-USERS)

## Zweck

Operative Vorbereitung für T-W3-BETA-USERS. Diese Datei ist **kein Beleg
für erfolgte Akquise** — sie ist die Anleitung, mit der die Akquise vom
Maintainer durchgeführt wird. Der Plan-Status `T-W3-BETA-USERS` bleibt
`pending`, bis mindestens 5 reale Cohort-Einträge unten eingetragen sind
(davon mind. 3 extern, siehe Failure-Mode in plan.yaml).

## Status

| Feld | Wert |
| --- | --- |
| Owner Akquise | **TBD — vom Maintainer eintragen, bevor Akquise startet** |
| Owner Discovery-Loop (T-W4) | **TBD — bevor Welle 4 startet** |
| Cohort-Soll | 5 (mind. 3 extern) |
| Cohort-Ist | 0 |
| Erstkontakt-Datum | — |
| Onboarding-Abschluss | — |

## Definition „extern"

Ein Cohort-Mitglied gilt als extern, wenn **alle** zutreffen:

- Keine berufliche oder persönliche Beziehung zum Owner.
- Hat das Repo nicht aktiv mitentwickelt.
- Nutzt eine eigene, real gewachsene ROM-Sammlung (>= 500 Dateien,
  mindestens 3 Konsolen, gemischter Format-Stand).
- Stellt eigenes Windows-System zur Verfügung.

## Recruiting-Kanäle (Vorschlag, priorisiert)

1. **r/Roms / r/emulation / r/RetroArch** — kurze Vorstellung + Link zum
   ehrlichen README (T-W2-README-REFRESH). **Nicht** als Werbung framen,
   sondern als „Ich suche 5 Personen mit echten Sammlungen, die mir 90 min
   ihrer Zeit geben."
2. **EmuDeck / Batocera Discord-Server** — gleiche Ansprache.
3. **No-Intro / Redump Forum** — sehr selektiv, nur eine Anfrage, klar als
   Forschungsprojekt markieren.
4. **Twitter/Mastodon Retrogaming-Hashtag** — als Backup, niedrige
   Conversion erwartet.

Verboten: Mitarbeiter, Familie, langjährige Bekannte als „Beta" zählen.

## Ansprache-Template

```
Betreff: Tester gesucht — strukturiertes ROM-Cleanup-Tool, 90 min Feedback

Hi,

Romulus ist ein in C# / .NET 10 / WPF geschriebenes Tool für deterministisches,
auditierbares Aufräumen von ROM-Sammlungen (DAT-Verifizierung, regionale
Deduplikation, Konvertierung mit Rollback). Wir suchen 5 Personen mit echten,
gemischten Sammlungen, die einen kompletten Workflow durchlaufen und uns dabei
über die Schulter sehen lassen.

Was du investierst:
- 1x ca. 90 min für einen Workflow-Durchlauf (Add Library → Scan → Verify →
  Plan → Execute → Report → Rollback) auf deiner Maschine, deine Sammlung.
- 1x ca. 30 min strukturiertes Interview vorab (Bedarf, aktuelle Werkzeuge).
- Bereitschaft, Friktionspunkte ehrlich zu benennen.

Was du bekommst:
- Frühen Zugriff auf das Tool, bevor es in Phase 2 geht.
- Direkten Draht zum Maintainer für deine Wünsche.
- Keine Zahlung, keine NDA.

Datenschutz:
- Keine Telemetrie ohne deine Zustimmung.
- Screen-Recording nur mit deinem expliziten Opt-In.
- Wir bekommen nie deine ROMs zu sehen, nur deine Beobachtungen.

Wenn das passt: kurze Antwort mit deiner Sammlungsgröße + Konsolen + Format-Mix.

Danke,
[Maintainer-Name]
```

## Interview-Leitfaden (vor dem Smoke)

Strukturiertes Interview, ca. 30 min, halbstandardisiert. **Nicht** Frage
für Frage abarbeiten — als Gesprächsrahmen verwenden.

### Block 1: Sammlung & Status (5 min)

1. Wie viele ROMs hast du grob? Welche Konsolen?
2. Wie ist deine Sammlung aktuell organisiert? (Ordnerstruktur, Naming,
   Dubletten?)
3. Wann hast du sie zuletzt aufgeräumt — und wieso hast du aufgehört?

### Block 2: Aktuelle Werkzeuge (10 min)

4. Welche Tools nutzt du heute? (RomVault, ClrMamePro, RetroArch-DAT,
   Skripte, gar nichts?)
5. Was funktioniert für dich? Was nicht?
6. Hast du schon mal aus Versehen eine Datei verloren? Was hat dich
   damals geschützt — oder nicht geschützt?
7. Wie wichtig ist dir Determinismus (gleicher Input → gleicher Output)?
   Wie wichtig ist dir Rollback?

### Block 3: Erwartung & Risikobild (10 min)

8. Wenn du Romulus heute auf deine Sammlung loslassen würdest — was
   wäre dein größter Bedenken?
9. Welche Aktion würdest du auf gar keinen Fall ohne Bestätigung machen
   lassen? (Hinweis auf Move/Convert/Rollback achten.)
10. Wie viele Klicks wären für dich „zu viele" für eine sichere
    Standardaktion?

### Block 4: Logistik (5 min)

11. Welches Windows-Setup hast du? (Version, Plattenplatz frei?)
12. Hast du eine Test-Kopie der Sammlung, die du opfern kannst, oder
    arbeitest du auf der echten?  *(Wenn echte: stark zu Backup raten.)*
13. Wann hast du Zeit für den Smoke? Wie lange am Stück?

## Onboarding-Anleitung (kurz, ein-Seiter für den Tester)

1. Romulus-Release-Build aus `dist/` herunterladen (kein Source-Build
   verlangt).
2. Auf Test-Plattform entpacken. Erste-Start-Wizard läuft.
3. Library-Pfad eintragen — explizit empfohlen: **Test-Kopie** der
   Sammlung, nicht die Live-Sammlung.
4. README.md lesen (ist seit T-W2-README-REFRESH ehrlich und kurz).
5. Beim Smoke wartet der Maintainer auf einer abgesprochenen Plattform
   (Discord-Voice o.Ä.) zum Mitlauschen — aber **greift nicht ein**.

## Cohort-Tracker (vom Maintainer auszufüllen)

Format pro Eintrag:

```
- id: B-001
  name_or_handle: ""
  contact: ""             # E-Mail / Discord-Tag
  external: true|false    # mind. 3 von 5 müssen true sein
  collection_size: 0      # Anzahl ROMs grob
  consoles: []            # ["NES","SNES","PSX",...]
  format_mix: ""          # z.B. ".zip,.nes,.sfc,.bin/.cue"
  os: ""                  # "Windows 11 23H2"
  recruited_at: ""        # ISO-Datum
  onboarded_at: ""        # ISO-Datum oder null
  smoke_done: false
  smoke_link: ""          # Verweis auf beta-smoke-protocol.md Eintrag
  notes: ""
```

### Cohort

```yaml
cohort:
  []   # leer; T-W3-BETA-USERS bleibt pending bis hier 5 Einträge stehen
       # (davon mind. 3 mit external: true)
```

## Annahme-Kriterium für T-W3-BETA-USERS = done

Status darf erst auf `done` gesetzt werden, wenn **alle** zutreffen:

- [ ] Owner Akquise ist namentlich benannt.
- [ ] Owner Discovery-Loop ist namentlich benannt.
- [ ] Cohort hat 5 Einträge mit ausgefüllten Feldern.
- [ ] Mind. 3 Einträge haben `external: true`.
- [ ] Jeder Eintrag hat Recruited-Datum und Onboarding-Datum.
- [ ] Feedback-Kanal (Discord-Channel / E-Mail-Liste / GitHub-Discussion)
      ist eingerichtet und im Eintrag verlinkt.

Vor diesen Kriterien ist jedes „done" eine Selbstausstellung im Sinne der
AGENTS.md-Regel „keine halben Loesungen".
