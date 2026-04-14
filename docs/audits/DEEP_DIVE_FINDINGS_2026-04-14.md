# Deep Dive Findings - 2026-04-14

Ziel dieses Dokuments:
- offene Repo-weite Bugs, Sicherheitsluecken und technische Schulden trackbar machen
- Release-Risiken priorisiert sichtbar halten
- Fixes, Tests und Nachverfolgung an einer Stelle sammeln

Legende:
- `[ ]` offen
- `[x]` erledigt
- Prioritaet: `P0` Blocker, `P1` hoch, `P2` relevant, `P3` nachgelagert

## Release-Blocker und hohe Risiken

- [ ] `P1` DAT-Mehrdeutigkeit wird im Enrichment zu `NoMatch` statt `Ambiguous`
  - Tags: `Release-Blocker`, `Parity Risk`, `False Confidence Risk`
  - Dateien:
    - `src/Romulus.Infrastructure/Orchestration/EnrichmentPipelinePhase.cs:720`
    - `src/Romulus.Infrastructure/Orchestration/EnrichmentPipelinePhase.cs:738`
    - `src/Romulus.Core/Audit/DatAuditClassifier.cs:101`
    - `src/Romulus.Core/Audit/DatAuditClassifier.cs:102`
  - Problem:
    - Exakte Hash-Treffer ueber mehrere Konsolen werden im Enrichment ohne Detector-Hypothesen als `NoMatch` verworfen.
    - Dieselbe Lage wird im DAT-Audit als `Ambiguous` klassifiziert.
    - Damit liefern Erkennung und Audit fuer dieselbe Datei unterschiedliche fachliche Wahrheiten.
  - Wirkung:
    - `UNKNOWN` / `No detection hypotheses available` trotz vorhandenem exaktem DAT-Treffer.
    - Safety-Review, Decisions und DAT-Audit koennen sich widersprechen.
  - Fix:
    - eine gemeinsame Ambiguitaetsregel fuer Enrichment und DatAuditClassifier erzwingen
    - `ResolveUnknownDatMatch` darf exakte Mehrfachtreffer nicht zu `NoMatch` degradieren
  - Testbedarf:
    - Invariantentest: gleicher Hash, mehrere DAT-Matches, keine Hypothesen -> beide Pfade liefern dieselbe Statusfamilie
    - Cross-surface Test fuer GUI/CLI/API/Report

- [ ] `P1` Rollback liest unvalidierte Root-Metadaten vor der Signaturpruefung
  - Tags: `Data-Integrity Risk`, `Security Risk`
  - Dateien:
    - `src/Romulus.Infrastructure/Audit/RollbackService.cs:70`
    - `src/Romulus.Infrastructure/Audit/RollbackService.cs:77`
    - `src/Romulus.Infrastructure/Audit/AuditRollbackRootResolver.cs:61`
    - `src/Romulus.Infrastructure/Audit/AuditRollbackRootResolver.cs:84`
  - Problem:
    - `RollbackService.Execute` und `VerifyTrashIntegrity` loesen Root-Metadaten vor `VerifyMetadataSidecar` auf.
    - Dabei beeinflusst unvalidiertes `.meta.json` bereits den Kontrollfluss und kann mit kaputten Pfaden vorher scheitern.
  - Wirkung:
    - manipulierte oder kaputte Sidecars koennen Rollback-Preflight und Rollback selbst abbrechen
    - Signaturpruefung kommt zu spaet
  - Fix:
    - zuerst Integritaet der Sidecar-Datei pruefen
    - Root-Aufloesung erst nach erfolgreicher Verifikation
    - beim Preflight dieselbe Reihenfolge erzwingen
  - Testbedarf:
    - Regressionstest mit manipuliertem `.meta.json`
    - Test fuer ungueltige Root-Pfade vor Signaturpruefung

- [ ] `P1` Collection Index wird vor Rename/Sort/Convert/DAT-Audit persistiert
  - Tags: `Parity Risk`, `False Confidence Risk`, `Architecture Debt Hotspot`
  - Dateien:
    - `src/Romulus.Infrastructure/Orchestration/RunOrchestrator.PreviewAndPipelineHelpers.cs:418`
    - `src/Romulus.Infrastructure/Orchestration/RunOrchestrator.PreviewAndPipelineHelpers.cs:502`
    - `src/Romulus.Infrastructure/Orchestration/RunOrchestrator.PreviewAndPipelineHelpers.cs:533`
    - `src/Romulus.Infrastructure/Analysis/CollectionCompareService.cs:59`
    - `src/Romulus.Infrastructure/Analysis/CollectionCompareService.cs:65`
  - Problem:
    - Kandidaten werden direkt nach Scan/Enrichment in den Collection Index geschrieben.
    - Spaetere fachliche Wahrheit aus DAT-Audit, Rename, ConsoleSort und Conversion wird nicht mehr in denselben Entries nachgezogen.
  - Wirkung:
    - nach jeder mutierenden Run-Phase ist der Index sofort teilweise veraltet
    - `CollectionCompareService` blockiert dann selbst wegen `collection index scope does not match filesystem`
    - delta-reuse kann spaeter auf einem Zustand basieren, der nie die finale Ausfuehrungswahrheit war
  - Fix:
    - Index erst nach finaler Pfadmutation und finalen Statuswerten aktualisieren
    - alternativ zweiten Persist-Schritt nach Execute-Phasen einfuehren
  - Testbedarf:
    - End-to-End-Test: Move/Sort/Convert-Run -> CollectionIndex passt sofort zum Filesystem
    - Test: finale DAT-/Sort-/Path-Werte sind im Index sichtbar

- [ ] `P1` 7z-DAT-Extraktion prueft Containment erst nach dem Entpacken
  - Tags: `Security Risk`
  - Dateien:
    - `src/Romulus.Infrastructure/Dat/DatRepositoryAdapter.cs:454`
    - `src/Romulus.Infrastructure/Dat/DatRepositoryAdapter.cs:466`
    - `src/Romulus.Infrastructure/Dat/DatRepositoryAdapter.cs:473`
  - Problem:
    - bei 7z-komprimierten DATs wird das Archiv mit externem `7z x` erst entpackt und die Pfade erst danach gefiltert.
    - Falls ein boesartiges Archiv Traversal oder problematische Pfade enthaelt, ist die Validierung zu spaet.
  - Wirkung:
    - potenzielles Schreiben ausserhalb des Temp-Verzeichnisses waehrend der Analysephase
  - Fix:
    - 7z-Inhalt vor der Extraktion listen und validieren
    - nur sichere Eintraege extrahieren
    - gleiches Sicherheitsniveau wie `ChdmanToolConverter.ExtractZipSafe`
  - Testbedarf:
    - Security-Test mit traversal-artigem 7z-Inhalt
    - Test fuer Abbruch ohne Dateisystemnebenwirkungen ausserhalb des Temp-Roots

- [ ] `P1` Junk-Removal verschiebt Descriptoren, aber laesst Set-Member zurueck
  - Tags: `Data-Integrity Risk`, `Parity Risk`
  - Dateien:
    - `src/Romulus.Infrastructure/Orchestration/JunkRemovalPipelinePhase.cs:18`
    - `src/Romulus.Infrastructure/Orchestration/JunkRemovalPipelinePhase.cs:33`
    - `src/Romulus.Infrastructure/Orchestration/JunkRemovalPipelinePhase.cs:74`
  - Problem:
    - Der Junk-Pfad schuetzt referenzierte Set-Member nur davor, selbst als Einzeldatei geloescht zu werden.
    - Wenn aber ein `.cue`-, `.gdi`-, `.ccd`- oder aehnlicher Descriptor selbst als Junk-Winner entfernt wird, werden seine referenzierten `.bin`/Track-Dateien nicht mitverschoben.
  - Wirkung:
    - orphaned Set-Member bleiben in der Sammlung liegen
    - Preview/Execute/Report weichen von der fachlichen Erwartung "das Junk-Set ist entfernt" ab
    - spaetere Runs sehen kaputte oder unvollstaendige Disc-Sets
  - Fix:
    - JunkRemoval auf dieselbe Set-Member-Strategie wie `MovePipelinePhase` heben
    - Descriptor und referenzierte Member atomar planen, preflighten, auditen und verschieben
  - Testbedarf:
    - Regressionstest: Junk-`.cue` mit `.bin`-Membern -> alle Dateien landen gemeinsam im Junk-Trash
    - DryRun/Execute-Paritaetstest fuer Set-Descriptoren

- [ ] `P1` Move-Freespace-Preflight unterschaetzt Disc-Sets ohne ihre Set-Member
  - Tags: `Data-Integrity Risk`, `False Confidence Risk`
  - Dateien:
    - `src/Romulus.Infrastructure/Orchestration/MovePipelinePhase.cs:26`
    - `src/Romulus.Infrastructure/Orchestration/MovePipelinePhase.cs:137`
    - `src/Romulus.Infrastructure/Orchestration/MovePipelinePhase.cs:350`
  - Problem:
    - Der Out-of-space-Preflight summiert nur `loser.SizeBytes`.
    - Die spaeter tatsaechlich mitverschobenen Set-Member aus `plannedMemberMoves` sind darin nicht enthalten.
  - Wirkung:
    - auf ein anderes Trash-Volume kann der Run starten, obwohl fuer `.cue`/`.gdi`-Sets real nicht genug Platz vorhanden ist
    - der Fehler tritt dann erst mitten im Move mit Rollback-Versuch auf
  - Fix:
    - Preflight auf dieselbe Set-Aufloesung wie die eigentliche Move-Phase heben
    - benoetigte Bytes fuer Descriptor plus Member deterministisch vorab summieren
  - Testbedarf:
    - Regressionstest mit kleinem Ziel-Volume und mehrteiligem Disc-Set
    - Test: Preflight blockiert vor dem ersten Move, wenn nur die Member den Platzbedarf sprengen

- [ ] `P1` 7z-Archiv-zu-CHD validiert Traversal erst nach dem Entpacken
  - Tags: `Security Risk`
  - Dateien:
    - `src/Romulus.Infrastructure/Conversion/ChdmanToolConverter.cs:120`
    - `src/Romulus.Infrastructure/Conversion/ChdmanToolConverter.cs:127`
    - `src/Romulus.Infrastructure/Conversion/ChdmanToolConverter.cs:130`
  - Problem:
    - Bei `.7z`-Quellen entpackt `ConvertArchiveToChdman` erst per externem `7z x` und prueft Pfad-Containment/Reparse-Points erst danach.
    - Ein boesartiges Archiv kann damit vor der Validierung schon ausserhalb des Extraktionsverzeichnisses schreiben.
  - Wirkung:
    - Sicherheitsluecke im produktiven Conversion-Pfad fuer benutzergelieferte Archive
    - Validierung kommt zu spaet, um die Seiteneffekte zu verhindern
  - Fix:
    - 7z-Inhalt vor der Extraktion listen und validieren
    - unsichere Eintraege hart blockieren, bevor `7z x` ausgefuehrt wird
  - Testbedarf:
    - Security-Test mit traversal-artigem 7z-ROM-Archiv
    - Test: keine Dateien ausserhalb des Temp-/Extract-Roots nach Fehlerfall

## Relevante Bugs und Paritaetsfehler

- [ ] `P2` DAT-Status wird in Exporten und WPF-Feldern auf Bool reduziert
  - Tags: `Parity Risk`, `False Confidence Risk`
  - Dateien:
    - `src/Romulus.Infrastructure/Analysis/CollectionExportService.cs:124`
    - `src/Romulus.Infrastructure/Analysis/CollectionExportService.cs:157`
    - `src/Romulus.UI.Wpf/Services/FeatureService.Collection.cs:41`
  - Problem:
    - `Have`, `HaveWrongName`, `Miss`, `Unknown`, `Ambiguous` werden zu `Verified` / `Unverified` kollabiert.
  - Wirkung:
    - UI-Filter, CSV und Excel verschweigen die eigentliche DAT-Wahrheit
    - Benutzer sehen keine Trennung zwischen `Unknown`, `Miss` und `WrongName`
  - Fix:
    - Exporte und Feldauflosung auf `DatAuditStatus` heben
    - Bool `DatMatch` nur noch als abgeleitete Kurzinfo verwenden
  - Testbedarf:
    - Export-Paritaetstest fuer alle DAT-Statuswerte
    - UI-Filtertest fuer `WrongName`, `Ambiguous` und `Miss`

- [ ] `P2` Delta-Rehydration verliert `DetectionConflictType`
  - Tags: `Parity Risk`, `Determinism Risk`
  - Dateien:
    - `src/Romulus.Contracts/Models/RomCandidate.cs:37`
    - `src/Romulus.Infrastructure/Index/CollectionIndexCandidateMapper.cs:49`
    - `src/Romulus.Infrastructure/Sorting/ConsoleSorter.cs:814`
    - `src/Romulus.Infrastructure/Sorting/ConsoleSorter.cs:817`
  - Problem:
    - `RomCandidate.DetectionConflictType` wird weder im Collection Index gespeichert noch beim Rehydratisieren gesetzt.
    - `ConsoleSorter.BuildSortReasonTag` haengt aber direkt davon ab.
  - Wirkung:
    - gleicher Input erzeugt unterschiedliche Reason-Tags je nachdem, ob ein Kandidat frisch gescannt oder aus dem Index wiederverwendet wurde
    - Audit-/Reason-Projektionen verlieren Konfliktpraezision
  - Fix:
    - `DetectionConflictType` in `CollectionIndexEntry` aufnehmen und mappen
  - Testbedarf:
    - Determinismus-Test: frischer Lauf vs. delta-reuse -> identische Sort-Reason-Tags

- [ ] `P2` `DatAuditStatus` ist im Modell vorhanden, wird aber nicht end-to-end getragen
  - Tags: `False Confidence Risk`, `Architecture Debt Hotspot`
  - Dateien:
    - `src/Romulus.Contracts/Models/RomCandidate.cs:25`
    - `src/Romulus.Core/Classification/CandidateFactory.cs:11`
    - `src/Romulus.Infrastructure/Orchestration/DatAuditPipelinePhase.cs:36`
    - `src/Romulus.Infrastructure/Orchestration/DatAuditPipelinePhase.cs:67`
    - `src/Romulus.Infrastructure/Index/CollectionIndexCandidateMapper.cs:67`
    - `src/Romulus.Infrastructure/Index/CollectionIndexCandidateMapper.cs:128`
  - Problem:
    - das Domain-Modell kennt `DatAuditStatus`, aber der Hauptpfad setzt ihn auf Kandidaten nie konsistent.
    - Der Index kann den Wert zwar speichern, bekommt aber in der Produktion meist nur `Unknown`.
  - Wirkung:
    - DAT-Audit-Tab, Export, Collection Index und Folgefeatures arbeiten nicht auf derselben Statusquelle
  - Fix:
    - `DatAuditPipelinePhase` muss Ergebnisse in Kandidaten bzw. in die spaetere Persist-Wahrheit zurueckschreiben
    - Single Source of Truth fuer DAT-Status festlegen
  - Testbedarf:
    - End-to-End-Test: DAT-Audit-Lauf -> Kandidat, Index, UI und Export zeigen denselben Status

- [ ] `P2` RunHistory-Snapshots speichern Vorher-Groesse statt finaler Collection-Groesse
  - Tags: `False Confidence Risk`, `Parity Risk`
  - Dateien:
    - `src/Romulus.Infrastructure/Index/CollectionRunSnapshotWriter.cs:29`
    - `src/Romulus.Infrastructure/Index/CollectionRunSnapshotWriter.cs:70`
    - `src/Romulus.Infrastructure/Analysis/RunHistoryTrendService.cs:32`
    - `src/Romulus.Infrastructure/Analysis/RunHistoryInsightsService.cs:33`
    - `src/Romulus.Infrastructure/Analysis/RunHistoryInsightsService.cs:69`
  - Problem:
    - `CollectionSizeBytes` wird aus `result.AllCandidates` summiert, also aus dem gescannten Vorher-Zustand.
    - Trend- und Insight-Services behandeln diesen Wert spaeter als finalen Collection-Stand.
  - Wirkung:
    - Storage-Trends, Growth-Werte und Run-Vergleiche sind nach Conversion/Move fachlich falsch
  - Fix:
    - Snapshot-Groesse aus `RunArtifactProjection.Project(result)` oder aus finalen Pfad-/Conversion-Artefakten berechnen
  - Testbedarf:
    - Regressionstest: Conversion-Run mit kleinerem Ziel -> Snapshot-Groesse sinkt
    - Test fuer ConsoleSort/DatRename-Pfadmutationen

- [ ] `P2` RunEnvironmentBuilder macht DAT-I/O auch bei deaktiviertem DAT und kann dabei schon scheitern
  - Tags: `Parity Risk`, `Performance Risk`
  - Dateien:
    - `src/Romulus.Infrastructure/Orchestration/RunEnvironmentBuilder.cs:528`
    - `src/Romulus.Infrastructure/Orchestration/RunEnvironmentBuilder.cs:592`
    - `src/Romulus.Infrastructure/Orchestration/RunEnvironmentBuilder.cs:1169`
    - `src/Romulus.Infrastructure/Dat/DatCatalogStateService.cs:115`
  - Problem:
    - effektiver DAT-Root und rekursive DAT-Dateizaehlung werden vor sauberer `EnableDat`-Guard ausgefuehrt.
    - Dabei werden rohe `Directory.GetFiles(... AllDirectories)`-Scans genutzt.
  - Wirkung:
    - nicht-DAT-Workflows zahlen unnoetige I/O-Kosten
    - reine Diagnosezaehlung kann Environment-Build abbrechen
  - Fix:
    - DAT-Aufloesung hinter `EnableDat`
    - alle DAT-Enumerationen auf `EnumerateLocalDatFilesSafe` zentralisieren
  - Testbedarf:
    - Test: `EnableDat = false` -> kein DAT-Root-Scan
    - Test: ACL-fehlerhafter DAT-Tree blockiert RunEnvironment nicht

- [ ] `P2` Unsichere rekursive Dateiscans umgehen vorhandene Safe-Enumeration
  - Tags: `Parity Risk`, `Reliability Risk`
  - Dateien:
    - `src/Romulus.CLI/Program.cs:568`
    - `src/Romulus.UI.Wpf/ViewModels/MainViewModel.Productization.cs:729`
    - `src/Romulus.UI.Wpf/ViewModels/MainViewModel.Settings.cs:1280`
    - `src/Romulus.UI.Wpf/Services/FeatureCommandService.Dat.cs:41`
    - `src/Romulus.Infrastructure/Analysis/DatAnalysisService.cs:193`
    - `src/Romulus.Infrastructure/Analysis/CompletenessReportService.cs:147`
    - `src/Romulus.Infrastructure/Dat/DatCatalogStateService.cs:115`
  - Problem:
    - sichere Scan-Helfer existieren, werden aber an mehreren produktiven Stellen umgangen.
    - Ein Teil des Codes faengt Ausnahmen nur beim Enumerator-Aufbau, nicht waehrend der Iteration.
  - Wirkung:
    - ACL-denied Unterordner, kaputte Reparse Points oder Race Conditions brechen Analysepfade inkonsistent ab
    - gleiches Produkt verhaelt sich je nach Entry Point unterschiedlich robust
  - Fix:
    - gemeinsame, fault-tolerante Enumeration fuer alle rekursiven Produktpfade
  - Testbedarf:
    - Matrix-Tests fuer CLI/API/WPF mit inaccessible subdirectory und reparse point

- [ ] `P2` Wizard-Analyse faengt Enumerationsfehler an der falschen Stelle
  - Tags: `False Confidence Risk`
  - Dateien:
    - `src/Romulus.UI.Wpf/ViewModels/MainViewModel.Productization.cs:707`
    - `src/Romulus.UI.Wpf/ViewModels/MainViewModel.Productization.cs:729`
  - Problem:
    - `BuildWizardScanData` umschliesst nur `Directory.EnumerateFiles(...)`, nicht die nachfolgende Iteration.
    - Viele I/O-Fehler werden bei `foreach` geworfen, nicht beim Erzeugen des Enumerables.
  - Wirkung:
    - ein einzelner problematischer Unterordner kann die gesamte Wizard-Analyse als fehlgeschlagen markieren
  - Fix:
    - fault-tolerante Enumeration oder Iterations-try/catch pro Root
  - Testbedarf:
    - Regressionstest mit Root, der waehrend Iteration `UnauthorizedAccessException` wirft

- [ ] `P2` WPF-DAT-Befehle scannen grosse Trees synchron und teilweise doppelt
  - Tags: `Architecture Debt Hotspot`, `Reliability Risk`
  - Dateien:
    - `src/Romulus.UI.Wpf/Services/FeatureCommandService.Dat.cs:20`
    - `src/Romulus.UI.Wpf/Services/FeatureCommandService.Dat.cs:41`
    - `src/Romulus.UI.Wpf/Services/FeatureCommandService.Dat.cs:174`
    - `src/Romulus.UI.Wpf/ViewModels/MainViewModel.Settings.cs:1259`
    - `src/Romulus.UI.Wpf/ViewModels/MainViewModel.Settings.cs:1280`
    - `src/Romulus.UI.Wpf/ViewModels/MainViewModel.Settings.cs:1298`
  - Problem:
    - `DatAutoUpdateAsync` macht vor dem ersten `await` einen vollen rekursiven Scan.
    - `OnAutoDetectDatMappings` scannt `DatRoot` selbst und ruft danach `BuildConsoleMap`, das erneut rekursiv scannt.
  - Wirkung:
    - UI-Haenger bei grossen DAT-Verzeichnissen
    - doppelte I/O-Last genau in den Workflows, die fuer DAT-first entscheidend sind
  - Fix:
    - Scans in Background-Task mit cancelbarer Safe-Enumeration verschieben
    - Scan-Ergebnis zwischen Auto-Detect und ConsoleMap teilen
  - Testbedarf:
    - UI-responsiveness Test / command test mit grossem fake DAT-Baum
    - Cancellation-Test

- [ ] `P2` Run-History und Storage-Insights mischen verschiedene Collections in einer Kurve
  - Tags: `False Confidence Risk`, `Parity Risk`
  - Dateien:
    - `src/Romulus.Infrastructure/Index/CollectionIndexPaths.cs:24`
    - `src/Romulus.Contracts/Models/CollectionIndexModels.cs:190`
    - `src/Romulus.Infrastructure/Analysis/RunHistoryTrendService.cs:22`
    - `src/Romulus.Infrastructure/Analysis/RunHistoryInsightsService.cs:19`
    - `src/Romulus.Infrastructure/Analysis/RunHistoryInsightsService.cs:54`
    - `src/Romulus.CLI/Program.cs:602`
  - Problem:
    - Snapshots speichern `Roots` und `RootFingerprint`, der Default-Index liegt aber global in `%APPDATA%`.
    - Trend-, History- und Insight-Services laden anschliessend rohe Snapshots ohne irgendeine Scope-Filterung nach Collection oder Root-Fingerprint.
  - Wirkung:
    - Runs unterschiedlicher Sammlungen oder Test-/Produktiv-Roots werden in einer Historie zusammengeworfen
    - Growth-, Storage- und Delta-Aussagen werden fachlich falsch, obwohl sie praezise wirken
  - Fix:
    - History/Insights standardmaessig auf aktuellen `RootFingerprint` oder explizite Scope-Parameter filtern
    - globale History nur noch als bewusst separaten Modus anbieten
  - Testbedarf:
    - Regressionstest mit zwei verschiedenen Root-Sets im selben `collection.db`
    - Test: Trend/Insights zeigen nur Snapshots der angeforderten Collection

- [ ] `P2` Legacy-Conversion-Fallback ignoriert Run-Cancellation waehrend Tool-Lauf
  - Tags: `Parity Risk`, `Reliability Risk`
  - Dateien:
    - `src/Romulus.Infrastructure/Conversion/FormatConverterAdapter.cs:199`
    - `src/Romulus.Infrastructure/Conversion/FormatConverterAdapter.cs:252`
    - `src/Romulus.Infrastructure/Conversion/FormatConverterAdapter.cs:382`
    - `src/Romulus.Infrastructure/Conversion/ChdmanToolConverter.cs:64`
    - `src/Romulus.Infrastructure/Conversion/ChdmanToolConverter.cs:122`
    - `src/Romulus.Infrastructure/Conversion/SevenZipToolConverter.cs:22`
    - `src/Romulus.Infrastructure/Conversion/DolphinToolConverter.cs:28`
    - `src/Romulus.Infrastructure/Conversion/PsxtractToolConverter.cs:22`
  - Problem:
    - Der plannerlose Legacy-Pfad und der explizite Archive-Fallback rufen alte Tool-Converter auf.
    - Diese starten externe Prozesse ohne den aktuellen `CancellationToken`.
  - Wirkung:
    - bei Cancel stoppt der Run nicht sauber, solange der Legacy-Toolprozess noch laeuft
    - Preview/Execute-/Cancel-Semantik unterscheidet sich zwischen Executor-Pfad und Fallback-Pfad
  - Fix:
    - CancellationToken durch alle Legacy-Converter bis `IToolRunner.InvokeProcess` durchreichen
    - Legacy- und Executor-Pfad auf dieselbe Cancel-Semantik bringen
  - Testbedarf:
    - Regressionstest: Cancel waehrend Archive-Fallback-Konvertierung terminiert den Toolprozess
    - Test fuer plannerlosen Legacy-Pfad mit langlaufendem Fake-Invoker

## Repo-Hygiene und technische Schulden

- [ ] `P3` Legacy-`ProfileService` loescht noch immer die komplette `settings.json`
  - Tags: `Architecture Debt Hotspot`, `False Confidence Risk`
  - Dateien:
    - `src/Romulus.UI.Wpf/Services/ProfileService.cs:18`
    - `src/Romulus.UI.Wpf/Services/ProfileService.cs:21`
    - `src/Romulus.UI.Wpf/Services/ProfileService.cs:31`
    - `src/Romulus.UI.Wpf/Services/FeatureCommandService.cs:434`
    - `src/Romulus.Tests/GuiViewModelTests.cs:2095`
  - Problem:
    - die Legacy-Serviceklasse arbeitet auf der globalen `settings.json`, obwohl das Profilsystem heute ueber `RunProfileService` laeuft.
    - ein gefaehrlicher Delete-Pfad lebt weiter, obwohl er fachlich nichts mit einem einzelnen Run-Profil zu tun hat.
  - Wirkung:
    - scharfe Altlast im Codebestand
    - irrefuehrender Name und potentiell gefaehrliche Wiederverwendung
  - Fix:
    - Legacy-Service entkoppeln, klar deprecaten oder entfernen
    - verbleibende Import/Export-Funktionen auf das neue Profilmodell umstellen
  - Testbedarf:
    - keine Alibi-Tests; stattdessen gezielte Migration/compatibility-Tests

- [ ] `P3` Architektur-Hotspots erschweren Korrektheit und Paritaet
  - Tags: `Architecture Debt Hotspot`
  - Dateien:
    - `src/Romulus.UI.Wpf/ViewModels/MainViewModel.RunPipeline.cs` (~69 KB)
    - `src/Romulus.UI.Wpf/ViewModels/MainViewModel.Settings.cs` (~49 KB)
    - `src/Romulus.UI.Wpf/ViewModels/MainViewModel.cs` (~47 KB)
    - `src/Romulus.Infrastructure/Orchestration/RunEnvironmentBuilder.cs` (~56 KB)
    - `src/Romulus.Infrastructure/Orchestration/EnrichmentPipelinePhase.cs` (~43 KB)
    - `src/Romulus.Infrastructure/Orchestration/RunOrchestrator.PreviewAndPipelineHelpers.cs` (~34 KB)
  - Problem:
    - zu viele Verantwortlichkeiten pro Klasse; dadurch entstehen lokale Sonderpfade, doppelte I/O-Strategien und Statusduplikate.
  - Wirkung:
    - Regressionen verstecken sich in Querabhaengigkeiten
    - Review- und Testaufwand steigt unnoetig
  - Fix:
    - Refactor entlang fachlicher Truth-Module statt entlang UI-Komfortmethoden
    - Dateisystem- und DAT-Enumeration zentralisieren
  - Testbedarf:
    - vor jedem Split Invariantentests fuer Preview/Execute/Report und GUI/CLI/API-Paritaet

## Tests und QA-Luecken

- [ ] `P2` Es fehlen Invariantentests fuer DAT-Status ueber alle Surfaces
  - Tags: `False Confidence Risk`
  - Betroffene Bereiche:
    - Enrichment
    - DAT Audit
    - Collection Index
    - Exporte
    - WPF-Filter
    - Reports
  - Problem:
    - Es gibt viele issue-getriebene Red-Tests, aber kein hartes Invariantenset fuer "eine DAT-Wahrheit ueber alle Schichten".
  - Fix:
    - ein gemeinsames Matrix-Set fuer `Have`, `HaveWrongName`, `Miss`, `Unknown`, `Ambiguous`
  - Testbedarf:
    - Cross-channel parity suite
    - delta-reuse vs full-scan suite

- [ ] `P2` Es fehlen Tests fuer sofortige CollectionIndex-Frische nach mutierenden Runs
  - Tags: `False Confidence Risk`
  - Problem:
    - der kritische Pfad "Run aendert Dateisystem -> Index muss sofort wieder verwendbar sein" ist nicht abgesichert.
  - Fix:
    - End-to-End-Test fuer Move, DAT Rename, ConsoleSort und Conversion
  - Testbedarf:
    - `TryMaterializeSourceAsync` direkt nach mutierendem Run
    - Compare/History Features direkt im Anschluss

- [ ] `P3` Einzelne Tests geben nur Scheinsicherheit
  - Tags: `False Confidence Risk`
  - Dateien:
    - `src/Romulus.Tests/GuiViewModelTests.cs:2095`
  - Problem:
    - `ProfileService_Delete_NoFile_ReturnsFalse` prueft nur `Assert.IsType<bool>(...)` statt das erwartete Verhalten.
  - Wirkung:
    - Test gruen, auch wenn das Verhalten fachlich falsch bleibt
  - Fix:
    - Test auf echte Semantik umstellen oder Altservice entfernen

## Empfohlene Bearbeitungsreihenfolge

1. [ ] DAT-Ambiguitaet zwischen Enrichment und DAT-Audit vereinheitlichen
2. [ ] Rollback-Root-Aufloesung hinter Signaturpruefung ziehen
3. [ ] CollectionIndex-Persistierung auf finalen Run-Zustand umstellen
4. [ ] 7z-DAT-Extraktion hart absichern
5. [ ] Junk-Removal fuer Set-Descriptoren atomar machen
6. [ ] Move-Freespace-Preflight fuer Set-Member korrigieren
7. [ ] alle rekursiven DAT-/File-Scans auf eine Safe-Enumeration umstellen
8. [ ] DAT-Status als echten Enum end-to-end propagieren
9. [ ] Snapshot-/Trend-Groessen und Scope auf finale Collection-Wahrheit umstellen
10. [ ] Legacy-/Fallback-Conversion cancelbar machen
11. [ ] WPF-DAT-Workflows entkoppeln und background-faehig machen
12. [ ] DetectionConflictType im Collection Index persistieren
13. [ ] Legacy `ProfileService` entschaerfen oder entfernen
