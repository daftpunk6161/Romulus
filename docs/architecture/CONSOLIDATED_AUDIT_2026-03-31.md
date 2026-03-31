# Consolidated Audit

Stand: `2026-03-31`

Diese Zusammenfassung konsolidiert das externe Audit mit einer Code-Pruefung des aktuellen Repos. Sie trennt bewusst zwischen:

- `validiert`: im aktuellen Code direkt belegt
- `korrigiert`: im Audit veraltet, ungenau oder nur als Hypothese belastbar
- `umgesetzt`: in diesem Arbeitsblock bereits geaendert

## Validierte Findings

### P1

- `Simple/Expert-Modus inkonsistent`
  `IsSimpleMode` existiert, ist aber in der aktiven Shell nicht konsequent umgesetzt. Die relevante Alt-View `SortView` bindet daran, die Shell in `MainWindow` aber nicht.

- `OpenAPI-Spec hatte Drift`
  Der eingebettete Spec-Stand lag hinter der realen API. Insbesondere fehlte `POST /runs/{runId}/rollback`, und zentrale Run-/Rollback-Schemas waren nicht explizit dokumentiert.

### P2

- `Review-Queue ohne Pagination`
  `GET /runs/{runId}/reviews` lieferte bisher unpaginiert die komplette Queue.

- `Fehlende Response-Hardening-Header`
  `X-Content-Type-Options: nosniff` und `X-Frame-Options: DENY` waren in der API-Antwort nicht gesetzt.

- `Enrichment-Pipeline sequentiell`
  Die aktuelle Pipeline verarbeitet Kandidaten in `EnrichmentPipelinePhase` sequentiell, obwohl ein `ParallelHasher` vorhanden ist. Das ist ein echter Skalierungshebel, aber noch kein in diesem Block implementierter Fix.

- `Thread-Safety-Risiko bei spaeterer Parallelisierung`
  `folderConsoleCache` ist aktuell ein `Dictionary<string, string>` und damit nicht parallelisierungssicher.

- `UI-Dichte in der Shell`
  CommandBar und ResultView enthalten harte Layoutentscheidungen, die auf kleinen Breiten unguenstig sind, unter anderem 7pt-Phasenlabels und ein fest dimensionierter Result-Chart.

### P3

- `Health ist hinter API-Key`
  Das ist im aktuellen Verhalten korrekt beobachtet. Ob das geaendert werden soll, ist eine Produktentscheidung und in diesem Block bewusst nicht angefasst.

- `Competitive Claims nicht repo-intern verifizierbar`
  Aussagen zu Marktposition, Konkurrenz und Alleinstellungsmerkmalen sind nicht aus dem Code beweisbar und muessen extern belegt werden.

## Korrigierte Audit-Punkte

- `API hat nicht 10 Endpoints`
  Der aktuelle Stand mappt 15 Endpoints, darunter `dats/status`, `dats/update`, `dats/import`, `convert` und `runs/{runId}/completeness`.

- `Kein DAT-Update-Endpoint` ist falsch
  `POST /dats/update` existiert bereits.

- `Performance-Zahlen sind Hypothesen, keine Messwerte`
  Aussagen wie `2-10x`, konkrete Stundenwerte oder genaue Throughput-Schaetzungen sind plausible Einschaetzungen, aber ohne Benchmark-Lauf nicht als Ist-Messung zu behandeln.

- `Verwaiste Views`
  Der Befund ist wahrscheinlich richtig, sollte aber praeziser als `aktuell nicht aktiv referenziert` formuliert werden, solange keine Laufzeitnavigation dagegen belegt ist.

## Bereits umgesetzt

### Batch 1: API-Konsistenz und Härtung

- `OpenAPI-Spec aktualisiert`
  Ergaenzt wurden:
  - `POST /runs/{runId}/rollback`
  - Response-Schemas fuer Run-Status, Run-Result, Cancel und Rollback
  - Pagination-Parameter fuer `GET /runs/{runId}/reviews`
  - Pagination-Metadaten in `ApiReviewQueue`

- `Review-Pagination implementiert`
  `GET /runs/{runId}/reviews` unterstuetzt jetzt optionale Query-Parameter:
  - `offset`
  - `limit` mit Bereich `1..1000`

- `Security-Header gesetzt`
  Die API setzt jetzt:
  - `X-Content-Type-Options: nosniff`
  - `X-Frame-Options: DENY`

- `Regressionstests ergaenzt`
  Neue bzw. erweiterte Tests decken ab:
  - Review-Pagination
  - Security-Header
  - OpenAPI-Rollback- und Pagination-Deklaration

## Naechste sinnvolle Umsetzungsbloecke

1. `Enrichment parallelisieren`
   Beste ROI-Stelle auf der Backend-Seite. Vorher muss `folderConsoleCache` parallelisierungssicher gemacht werden.

2. `Simple/Expert-Modus entscheiden`
   Entweder konsequent in der aktiven Shell umsetzen oder die Alt-Pfade entfernen.

3. `UI-Hotspots entschlacken`
   CommandBar-Informationsdichte und Result-Chart auf responsive Layouts umstellen.

4. `Health/Auth-Policy bewusst entscheiden`
   Entweder bewusst intern-only belassen oder einen abgespeckten unauthentifizierten Health-Endpoint einfuehren.
