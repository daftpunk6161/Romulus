# Romulus Product Roadmap Execution

Stand: 2026-04-01

Dieses Dokument ist der operative Einstiegspunkt fuer die Ausfuehrung der Produkt-Roadmap.
Die Detailarbeit wird in den verlinkten Release-Plaenen verfolgt.

## Release-Reihenfolge

- [x] R1 Foundation
- [x] R2 Productization
- [x] R3 Reach

## Abhaengigkeiten

- [x] R1 ist Grundlage fuer R2
- [x] R1 ist Grundlage fuer R3
- [x] R2 kann nur teilweise parallel zu R3 laufen
- [x] Conversion-Erweiterungen laufen erst nach gesichertem Review-, Verify- und Rollback-Pfad

## Release-Plaene

- [x] [R1 Foundation Execution](r1-foundation-execution.md)
- [x] [R2 Productization Execution](r2-productization-execution.md)
- [x] [R3 Reach Execution](r3-reach-execution.md)

## Aktuelle Prioritaet

- [x] R1-T01 Index-Vertrag und Datenmodell festziehen
- [x] R1-T02 Index-Adapter, Migration und Recovery umsetzen
- [x] R1-T03 Delta-Scan-Integration in Scanner und Orchestrierung anbinden
  Persistenter Hash-Cache, scannerseitige Dateimetadaten, Delta-Rehydration, Candidate-Persistenz und Stale-Entry-Cleanup laufen jetzt ueber denselben Collection-Index.
- [x] R1-T04 Run-Snapshot-Queries und Verlaufsabfragen abschliessen
  API, CLI und Trend-/Report-Konsumenten lesen jetzt dieselbe persistierte Snapshot-Historie.
- [x] R1-T05 Analyse, Completeness und Export auf zentrale Datenbasis umstellen
  Completeness, Export, WPF-Analyse und Standalone-Conversion nutzen jetzt gemeinsame Candidate-/Index-Resolver mit explizitem Fallback.
- [x] R1-T06 Gemeinsamen Review-Decision-Store einfuehren
  Persistierte Review-Approvals werden in GUI, CLI und API ueber dieselbe Infrastructure gelesen und geschrieben.
- [x] R1-T07 Watch- und Schedule-Services in gemeinsame Infrastructure ueberfuehren
  Debounce, Pending-Flush und Busy-Schutz liegen jetzt in gemeinsamen Infrastructure-Diensten.
- [x] R1-T08 Kanalintegration fuer Watch, Review und Run-Status abschliessen
  API, CLI und GUI sind auf dieselben Watch-, Review- und Statusmodelle verdrahtet.
- [x] R1-T09 Invarianten- und Regressionstest-Matrix fuer Foundation vervollstaendigen
  Vollsuite grün: `7101/7101` Tests auf Stand `2026-04-01`.
- [x] R2-T01 Szenario-Katalog und RunOptions-Mapping festziehen
  Workflow-Katalog, kanaluebergreifende Draft-/Explicitness-Modelle und gemeinsame Materialisierung liegen jetzt in Contracts/Infrastructure.
- [x] R2-T02 Wizard-State-Machine und UI-Integration umsetzen
  WPF-Wizard, Simple/Expert-Umschaltung und gefuehrte Auswahl verwenden denselben Materialisierungs- und Statuspfad.
- [x] R2-T03 Gemeinsame Workflow-Projection und Summary fuer Wizard und Expertenmodus
  Wizard, Expertenmodus und Folgefunktionen lesen dieselbe RunConfiguration-/Projection-Wahrheit.
- [x] R2-T04 Exportmodell und gemeinsamer Export-Query-Pfad definieren
  Frontend-Export basiert auf `ExportableGame` und index-/run-first Candidate-Reads statt lokaler UI-Ableitung.
- [x] R2-T05 RetroArch- und LaunchBox-Exporter produktisieren
  RetroArch und LaunchBox laufen ueber denselben Frontend-Export-Service in GUI, CLI und API.
- [x] R2-T06 EmulationStation- und Playnite-Exporter bereitstellen
  EmulationStation und Playnite sind ueber denselben Exportvertrag mit Pfad- und Escaping-Haertung angebunden.
- [x] R2-T07 Profilformat, Validierung und Built-in-Profile definieren
  Versionierte Profile, Built-ins und sichere Importvalidierung sind produktiv verdrahtet.
- [x] R2-T08 Profile in GUI, CLI und API ohne Schattenlogik verdrahten
  Profile und Workflows materialisieren ueber denselben Resolver in allen Kanaelen.
- [x] R2-T09 Run-Diff, Trend-Reports und Storage-Insights liefern
  Persistierte Run-Historie treibt Compare-, Trend- und Storage-Insights in GUI, CLI und API.
- [x] R2-T10 Regressionstest-Matrix fuer Wizard, Export und Profile erweitern
  Vollsuite grün: `7133/7133` Tests auf Stand `2026-04-01`.
- [x] R3-T01 Dashboard-Architektur und Sicherheitsrahmen festziehen
  Headless-Remote-Modus erzwingt HTTPS-`PublicBaseUrl`, API-Key und `AllowedRoots`; Dashboard bleibt API-only.
- [x] R3-T02 Embedded Dashboard Shell und Auth-Bootstrap umsetzen
  Statische Dashboard-Shell wird direkt aus `RomCleanup.Api` ausgeliefert und nutzt nur bestehende API-Pfade plus `bootstrap/summary`.
- [x] R3-T03 Run-Management und Live-Progress ueber API anbinden
  Browser-Run-Start, Status, Cancel und SSE-Live-Progress laufen ueber dieselbe Run-Wahrheit wie CLI und GUI.
- [x] R3-T04 DAT-Status, Review-Queue und Completeness im Dashboard bereitstellen
  Dashboard liest DAT-, Review- und Completeness-Daten aus denselben API-/Index-/Review-Modellen.
- [x] R3-T05 Headless-Paketierung und Deployment-Guides bereitstellen
  Dockerfile, Compose, Caddy-Beispiel und Headless-Guide sind im Repo dokumentiert.
- [x] R3-T06 Headless-spezifische API-Haertung abschliessen
  AllowedRoots, Remote-Startup-Validierung und Reach-OpenAPI-/API-Regressionen sind verdrahtet.
- [x] R3-T07 ECM- und NKit-Sicherheitsmodell sowie Tooling vorbereiten
  `EcmInvoker`/`NkitInvoker`, Compound-Extension-Erkennung und fail-closed Tool-Hash-Checks sind implementiert.
- [x] R3-T08 ECM- und NKit-Pipeline mit Review, Verify und Rollback integrieren
  Review-pflichtige Multi-Step-Conversion ist in Planner/Executor/Orchestrator integriert.
- [x] R3-T09 Integrations- und Negativtests fuer Headless und Conversion-Erweiterung vervollstaendigen
  Vollsuite grün: `7145/7145` Tests auf Stand `2026-04-01`.

## Parked

- [ ] Community-Kataloge erst nach stabiler Profil- und Indexbasis neu bewerten
- [ ] Erweiterte Collection Intelligence erst nach belastbarer Run-Historie planen

## Bewusst nicht verfolgen

- Kein Cross-Platform-Desktop-Rewrite vor Web-Frontend
- Keine neue Schattenlogik fuer KPIs, Status oder Export
- Keine aggressive Conversion-Ausweitung ohne Verifikation und Rollback
