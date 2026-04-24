# Full Repo Audit – Romulus `src/` (2026-04-24)

> **Status:** Tracking-Dokument fuer alle Audit-Findings aus zwei Tiefen-Audit-Runden.
> Jedes Finding hat eine Checkbox, die beim Umsetzen abgehakt werden muss.
> Quelle: parallele Audits durch `gem-reviewer`, `SE: Architect`, `gem-critic`, `SE: Security`.
> Scope: ausschliesslich `src/` (alle Projekte). `archive/powershell/` ignoriert.

---

## Executive Verdict

**Repo ist NICHT release-tauglich.** 8 P0-Funde (Datenverlust + Audit-Integritaet + Broken Access Control), 22 P1-Funde, 25+ P2/P3-Funde. Wichtigste systemische Wurzeln:

1. **Verify-Vertrag ist fail-open** statt fail-closed (Conversion, HMAC, Sidecar).
2. **„Eine fachliche Wahrheit" verletzt** an >10 Stellen (CSV-Sanitizer, DAT-Update, RunOrchestrator-Komposition, Status-Strings, Settings-Loader).
3. **Statisch-Mutable State in Core** untergraebt Determinismus (Dedup-Ranks, ClassificationIo).
4. **Halbfertige Refactors** als Schattenlogik (Avalonia, FeatureService, MainViewModel).
5. **Tests zementieren Bug-Verhalten** (z. B. Verify-Bypass-Test, Alibi-Determinismus-Test).
6. **Audit-Kette ohne kryptografische Anker** (kein Hash-Chain, kein KeyId, additionalMetadata unsigniert).

---

## Priorisierung & Fortschritt

| Severity | Round 1+2 | Round 3 | Round 4 | Gesamt | Erledigt |
|----------|----------:|--------:|--------:|-------:|---------:|
| **P0** (Release-Blocker)         |  8 |  1 |  9 |  18 | 0 |
| **P1** (Hohe Risiken)            | 22 | 15 | 17 |  54 | 0 |
| **P2** (Mittlere Risiken)        | 26 | 22 | 22 |  70 | 0 |
| **P3** (Wartbarkeit / niedrig)   | 12 |  6 | 13 |  31 | 0 |
| **Gesamt**                       | 68 | 44 | 61 | 173 | 0 |

> Bitte beim Abhaken die Tabelle hier oben mit aktualisieren.
> Round-3-Funde: `R3-A-*` (DAT/Hash/Tools), `R3-B-*` (Settings/Loc/UI), `R3-C-*` (API/CLI/Reports/Logging).
> Round-4-Funde: `R4-A-*` (Avalonia), `R4-B-*` (Tests/Benchmark), `R4-C-*` (Deploy/Safety/Logging/Audit).

---

## Inhalt

- [P0 – Release-Blocker](#p0--release-blocker)
- [P1 – Hohe Risiken](#p1--hohe-risiken)
- [P2 – Mittlere Risiken](#p2--mittlere-risiken)
- [P3 – Wartbarkeit / Niedrige Risiken](#p3--wartbarkeit--niedrige-risiken)
- [Round 3 – Neue Funde](#round-3--neue-funde)
- [Round 4 – Neue Funde](#round-4--neue-funde)
- [Test- & Verifikationsplan](#test---verifikationsplan)
- [Sanierungsstrategie](#sanierungsstrategie)

---

## P0 – Release-Blocker

> Diese Funde MUESSEN vor jedem weiteren Feature-Commit oder Release behoben werden.

### P0-01 — Conversion-Source wandert in Trash ohne echte Verifikation
**Tags:** Release-Blocker · Data-Integrity Risk · Bug
- [ ] **Fix umsetzen**
- **Files:** [src/Romulus.Infrastructure/Orchestration/ConversionVerificationHelpers.cs](../../src/Romulus.Infrastructure/Orchestration/ConversionVerificationHelpers.cs#L21-L46), [ConversionPhaseHelper.cs](../../src/Romulus.Infrastructure/Orchestration/ConversionPhaseHelper.cs#L196), [ChdmanToolConverter.cs](../../src/Romulus.Infrastructure/Conversion/ChdmanToolConverter.cs#L64), [SevenZipToolConverter.cs](../../src/Romulus.Infrastructure/Conversion/SevenZipToolConverter.cs#L38), [FormatConverterAdapter.cs](../../src/Romulus.Infrastructure/Conversion/FormatConverterAdapter.cs#L393)
- **Problem:** `IsVerificationSuccessful` liefert `true` bei `(NotAttempted, target=null, plan ohne Capability)`. Source landet anschliessend in `_TRASH_PostConversion`, ohne dass `chdman verify`/`7z t` jemals lief.
- **Reproduktion:** Conversion-Registry nicht ladbar -> `ConvertLegacy`-Pfad -> `ConversionResult.NotAttempted` -> Source weg.
- **Fix:** `IsVerificationSuccessful` bei `NotAttempted` immer `false`. Pflicht-Verify in `ChdmanToolConverter.Convert` und `SevenZipToolConverter.Convert`. `ConvertLegacy`-Pfad loeschen.
- **Tests fehlen:**
  - [ ] Regression `(NotAttempted, target=null) -> false`
  - [ ] Integration mit Stub-Konverter ohne Verify -> Source bleibt erhalten
  - [ ] Negative-Test: Mock-chdman exit 0 + zero-byte Output -> kein Source-Move

### P0-02 — Test zementiert Verify-Bug-Verhalten
**Tags:** Release-Blocker · False Confidence Risk
- [ ] **Test invertieren oder loeschen**
- **File:** `src/Romulus.Tests/...IsVerificationSuccessful_NotAttempted_NullTarget_ReturnsTrueForLegacyPath`
- **Problem:** Bestehender Test fixiert das Bug-Verhalten als „erwartet" und blockiert P0-01.
- **Fix:** Test umkehren: `NotAttempted_NullTarget_ReturnsFalse` ODER Test entfernen, sobald P0-01 umgesetzt ist.

### P0-03 — GameKey „__empty_key_null" kollidiert fuer alle Whitespace-Namen
**Tags:** Release-Blocker · Data-Integrity Risk · Determinism Risk
- [ ] **Fix umsetzen**
- **File:** [src/Romulus.Core/GameKeys/GameKeyNormalizer.cs](../../src/Romulus.Core/GameKeys/GameKeyNormalizer.cs#L240-L283)
- **Problem:** Frueh-Return Z. 241 weist allen Whitespace-Namen denselben Key zu -> unverwandte Files werden gruppiert -> Loser landet in `_TRASH_REGION_DEDUPE`.
- **Reproduktion:** `   .iso` und `\u3000.chd` -> beide Key `__empty_key_null` -> ein File wird geloescht.
- **Fix:** Frueh-Pfad ebenfalls hash-suffigieren (analog Spaet-Fallback Z. 280-283) ODER Kandidat aus Gruppierung ausschliessen.
- **Tests fehlen:**
  - [ ] Property-Test: `Normalize(a) != Normalize(b)` fuer 2 verschiedene Whitespace-Inputs

### P0-04 — Cross-Volume-Move nicht atomar (Cancel/IO-Fehler hinterlaesst Partials)
**Tags:** Release-Blocker · Data-Integrity Risk
- [ ] **Fix umsetzen**
- **File:** [src/Romulus.Infrastructure/FileSystem/FileSystemAdapter.cs](../../src/Romulus.Infrastructure/FileSystem/FileSystemAdapter.cs#L393-L465)
- **Problem:** .NET `File.Move` zwischen Volumes = Copy+Delete. Bei Cancel/IOException bleibt halbe Zieldatei + ganze Source -> Trash-Inflation, korrupte Files, Audit verweist auf abgeschnittene Datei.
- **Fix:** Stage-File `{dest}.tmpmv` schreiben -> atomarer `File.Move` innerhalb Zielvolume -> Source loeschen. `try/catch/finally` mit Tempfile-Cleanup.
- **Tests fehlen:**
  - [ ] Mock-IFS wirft `OperationCanceledException` -> keine Restdatei am Ziel
  - [ ] Mock-IFS wirft IOException nach Teil-Copy -> Tempfile geloescht

### P0-05 — Audit-Sidecar `meta.json` nicht atomar geschrieben
**Tags:** Release-Blocker · Data-Integrity Risk · Audit Integrity Risk
- [ ] **Fix umsetzen**
- **File:** [src/Romulus.Infrastructure/Audit/AuditSigningService.cs](../../src/Romulus.Infrastructure/Audit/AuditSigningService.cs#L177-L181)
- **Problem:** `File.WriteAllText(metaPath, json, Encoding.UTF8)` direkt auf Zielpfad. Crash mitten im Schreiben -> Sidecar halb-geschrieben/leer -> `VerifyMetadataSidecar` wirft `JsonException` -> Rollback dauerhaft blockiert. Inkonsistent zur Key-Datei (die ist atomar via tmp+Move).
- **Fix:** Schreiben ueber `metaPath + ".tmp"` mit `File.Move(tmp, metaPath, overwrite:true)`.
- **Tests fehlen:**
  - [ ] Halben JSON nach `meta.json` schreiben -> Rollback liefert klares `INTEGRITY_BROKEN`-Result, NICHT alle Zeilen als Failed

### P0-06 — HMAC-Signing-Key wird aus leerer / korrupter Datei stillschweigend regeneriert
**Tags:** Release-Blocker · Security Risk · Critical (OWASP A08)
- [ ] **Fix umsetzen**
- **File:** [src/Romulus.Infrastructure/Audit/AuditSigningService.cs](../../src/Romulus.Infrastructure/Audit/AuditSigningService.cs#L42-L60)
- **Problem:** Bei leerer Datei (`Convert.FromHexString("") == byte[0]`) oder `FormatException` wird neuer Key generiert ohne Laengenpruefung. Alle bestehenden Sidecars werden ab sofort als „tampered" abgelehnt -> Denial-of-Rollback. Angreifer mit Schreibrecht kann durch Byte-Flip jeden Rollback blockieren.
- **Fix:** Nach `Convert.FromHexString` validieren `Length == 32`, sonst fail-closed (`InvalidOperationException`). Korrupten Key in `quarantine/<utc>.bad` verschieben statt ueberschreiben. Niemals stillschweigend rotieren.
- **Tests fehlen:**
  - [ ] Negative: leere/Whitespace/zu kurze Key-Datei -> Konstruktor wirft, alte Sidecars bleiben verifizierbar
  - [ ] Korrupte Hex-Datei -> wirft, Datei bleibt unveraendert

### P0-07 — Audit-Rollback liefert leeres Ergebnis bei geloeschter CSV
**Tags:** Release-Blocker · Audit Integrity Risk
- [ ] **Fix umsetzen**
- **File:** [src/Romulus.Infrastructure/Audit/AuditSigningService.cs](../../src/Romulus.Infrastructure/Audit/AuditSigningService.cs#L222)
- **Problem:** Frueher Exit-Branch behandelt fehlende CSV als „nichts zu tun". Sidecar-Existenz wird nicht gegengeprueft. UI zeigt „Rollback erfolgreich, 0 Dateien" obwohl Tampering vorliegt.
- **Fix:** Wenn `File.Exists(metaPath) && !File.Exists(auditCsvPath)` -> `Failed = metadata.RowCount, Tampered=true`. Result-Felder `Tampered` ergaenzen.
- **Tests fehlen:**
  - [ ] `Rollback_WithMissingCsvAndPresentSidecar_ReportsTampered`
  - [ ] `Rollback_WithBothMissing_ReportsFailedNotZero`

### P0-08 — Run/Snapshot mit leerem `OwnerClientId` ist fuer alle API-Keys offen
**Tags:** Release-Blocker · Security Risk · Broken Access Control (OWASP A01)
- [ ] **Fix umsetzen**
- **File:** [src/Romulus.Api/ProgramHelpers.cs](../../src/Romulus.Api/ProgramHelpers.cs#L181-L190)
- **Problem:** `CanAccessRun`/`CanAccessSnapshot` geben `true` zurueck, sobald `OwnerClientId` `null/whitespace` ist. Legacy-/importierte Records ohne Owner sind fuer jeden gueltigen Key sichtbar UND steuerbar (inkl. `/runs/{id}/rollback`, `/export/frontend`, `/collections/merge/apply`).
- **Fix:** `OwnerClientId` zur Pflicht. Records ohne Owner als „system-locked" -> eigener Admin-Scope. `CanAccessRun` defaultet auf `false`.
- **Tests fehlen:**
  - [ ] Run mit `OwnerClientId=""` -> fremder API-Key bekommt 403, nicht 200

---

## P1 – Hohe Risiken

### P1-01 — Avalonia ist Shadow-UI ohne RunOrchestrator-Anbindung
**Tags:** Shadow Logic · Parity Risk
- [ ] **Entscheidung treffen + umsetzen** (entweder Stub markieren ODER echte Anbindung)
- **Files:** [MainWindowViewModel.cs](../../src/Romulus.UI.Avalonia/ViewModels/MainWindowViewModel.cs#L113), [ProgressViewModel.cs](../../src/Romulus.UI.Avalonia/ViewModels/ProgressViewModel.cs#L51-L75), [StartViewModel.cs](../../src/Romulus.UI.Avalonia/ViewModels/StartViewModel.cs), [ResultViewModel.cs](../../src/Romulus.UI.Avalonia/ViewModels/ResultViewModel.cs)
- **Problem:** `StartPreview()` ruft Progress an und setzt `Progress=100%`. Kein Aufruf von RunOrchestrator/RunService. Verstoesst „Keine halben Loesungen".
- **Fix:** Avalonia per Feature-Flag aus Build-Default rausnehmen ODER an `RunService` anschliessen.

### P1-02 — `RunOrchestrator`-Komposition dreifach dupliziert
**Tags:** Parity Risk · Duplicate Logic
- [ ] **Fix umsetzen**
- **Files:** [RunManager.cs](../../src/Romulus.Api/RunManager.cs#L113-L130), [RunService.cs](../../src/Romulus.UI.Wpf/Services/RunService.cs#L106-L120), [Program.cs](../../src/Romulus.CLI/Program.cs#L243-L283), [Program.Subcommands.AnalysisAndDat.cs](../../src/Romulus.CLI/Program.Subcommands.AnalysisAndDat.cs#L34)
- **Problem:** 17-Parameter-`new RunOrchestrator(...)` wortgleich an 3+ Stellen. Jeder neue Parameter muss ueberall nachgezogen werden.
- **Fix:** `env.CreateOrchestrator(onProgress, reviewDecisionService)` in `RunEnvironment`/Factory.

### P1-03 — DAT-Update-Pipeline dreifach implementiert
**Tags:** Shadow Logic · Duplicate Logic
- [ ] **Fix umsetzen**
- **Files:** [Program.cs API](../../src/Romulus.Api/Program.cs#L546-L650), [Program.cs CLI](../../src/Romulus.CLI/Program.cs#L370-L420), [DatCatalogViewModel.cs](../../src/Romulus.UI.Wpf/ViewModels/DatCatalogViewModel.cs#L145)
- **Fix:** `DatSourceService.UpdateCatalogAsync(catalog, force, ct) -> DatBatchUpdateResult`.

### P1-04 — Doppelte CSV-Sanitizer (DAT-Audit fehlt UNC-Schutz)
**Tags:** Security Risk · Parity Risk · Duplicate Logic
- [ ] **Fix umsetzen**
- **Files:** [AuditCsvParser.cs](../../src/Romulus.Infrastructure/Audit/AuditCsvParser.cs#L119-L138), [DatAuditViewModel.cs](../../src/Romulus.UI.Wpf/ViewModels/DatAuditViewModel.cs#L188-L251)
- **Problem:** `SanitizeDatAuditCsvField` neutralisiert nicht `\\`-Prefix (UNC). Excel oeffnet UNC-Verbindung beim CSV-Open. R5-011-Fix existiert nur in `SanitizeSpreadsheetCsvField`.
- **Fix:** Auf gemeinsamen Helper konsolidieren.
- **Tests fehlen:**
  - [ ] `SanitizeDatAuditCsvField("\\\\evil\\share")` muss prefixed/escaped sein
  - [ ] Parity-Test DAT-CSV vs. Spreadsheet-CSV

### P1-05 — `MoveSetAtomically` wirft `AggregateException` durch
**Tags:** Data-Integrity Risk · False Confidence Risk
- [ ] **Fix umsetzen**
- **File:** [ConsoleSorter.cs](../../src/Romulus.Infrastructure/Sorting/ConsoleSorter.cs#L497-L520) (Throw), Call-Sites Z. 183, 238, 298, 376
- **Problem:** Sort-Phase bricht mitten in Iteration ab; KPIs/Counter inkonsistent. Mischt zwei Fehlerklassen.
- **Fix:** Immer `(false, 0, [])` zurueckgeben + `criticalRollbackFailures: IReadOnlyList<string>` in `ConsoleSortResult`. Reason-Tag `set-rollback-failed`.
- **Tests fehlen:**
  - [ ] Mock-IFS Move-Failure + Rollback-Failure -> Tupel statt Exception

### P1-06 — Set-Rollback `MoveItemSafely` ohne `overwrite` -> silent DUP-Suffix
**Tags:** Data-Integrity Risk
- [ ] **Fix umsetzen**
- **File:** [ConsoleSorter.cs](../../src/Romulus.Infrastructure/Sorting/ConsoleSorter.cs#L495-L515)
- **Problem:** Rollback nimmt DUP-Suffix `original__DUP1.cue` ohne Auditspur. Verletzt Audit/Undo-Garantie.
- **Fix:** Rollback mit `overwrite: true`. Bei belegter Source-Position WERFEN.
- **Tests fehlen:**
  - [ ] Race-Test mit zwischenzeitlich auftauchender Source -> Rollback wirft

### P1-07 — `EnrichmentPipelinePhase` Parallel.For ohne dokumentierte Thread-Safety
**Tags:** Determinism Risk · Concurrency
- [ ] **Fix umsetzen**
- **File:** [EnrichmentPipelinePhase.cs](../../src/Romulus.Infrastructure/Orchestration/EnrichmentPipelinePhase.cs#L56-L82)
- **Problem:** DatIndex/HashService aus mehreren Threads -> potenzielle Race-Cache-Variation -> unterschiedliche DAT-Hits je Lauf.
- **Fix:** Locks/Concurrent-Collections in DatIndex bestaetigen oder einbauen.
- **Tests fehlen:**
  - [ ] Stress-Test 200x parallel -> bit-identische Candidate-Liste

### P1-08 — `DeduplicationEngine` mit globalem mutable static state
**Tags:** Architecture Debt Hotspot · Determinism Risk
- [ ] **Fix umsetzen**
- **File:** [DeduplicationEngine.cs](../../src/Romulus.Core/Deduplication/DeduplicationEngine.cs#L10-L57)
- **Problem:** `RegisterCategoryRanks()`/`RegisterCategoryRankFactory()` setzen prozessweite Felder. `ResetForTesting()` ist Eingestaendnis. In API+WPF im selben Prozess: letzter Registrant gewinnt.
- **Fix:** Ranks per ctor-Parameter durchreichen.

### P1-09 — Reparse-Point-Check ignoriert NTFS-Hardlinks
**Tags:** Data-Integrity Risk · KPI-Luege
- [ ] **Fix umsetzen**
- **File:** `MoveItemSafely`/`EnsureNoReparsePointInExistingAncestry` in `FileSystemAdapter.cs`
- **Problem:** Hardlink-Loser geht in Trash, Winner-Pfad ist identisch -> KPI „Saved bytes" lügt.
- **Fix:** `BY_HANDLE_FILE_INFORMATION.NumberOfLinks > 1` per `GetFileInformationByHandle` pruefen, Audit ehrlich markieren.
- **Tests fehlen:**
  - [ ] Hardlink-Set-Test

### P1-10 — `ConversionExecutor.BuildOutputPath` zwingt Output ins Source-Directory
**Tags:** Architecture Debt · Data-Integrity Risk
- [ ] **Fix umsetzen**
- **File:** [ConversionExecutor.cs](../../src/Romulus.Infrastructure/Conversion/ConversionExecutor.cs#L298-L313)
- **Problem:** Read-only Source (NAS, ISO-Mount) -> Multi-Step bricht ab. Long-Path > 248 Zeichen -> externe Tools schlagen fehl.
- **Fix:** Konfigurierbares Output-Verzeichnis pro Run (Default = SourceDir, Override = WorkRoot). `\\?\`-Praefix fuer externe Tools bei langen Pfaden.

### P1-11 — `MainViewModel` Gott-Klasse (~4.811 LoC ueber 5 Partials)
**Tags:** Architecture Debt Hotspot
- [ ] **Refactor umsetzen** (Gross-Aufwand)
- **Files:** [MainViewModel.cs](../../src/Romulus.UI.Wpf/ViewModels/MainViewModel.cs) + 4 Partials (Settings, RunPipeline, WatchAndProgress, Productization)
- **Fix:** RunPipeline-Partials -> `RunPipelineViewModel`. Settings-Partial -> in `SetupViewModel` integrieren. Productization -> `ProductizationViewModel`.

### P1-12 — Audit-CSV bricht bei Newlines im `Reason`-Feld
**Tags:** Data-Integrity Risk
- [ ] **Fix umsetzen**
- **Files:** [DatRenamePipelinePhase.cs](../../src/Romulus.Infrastructure/Orchestration/DatRenamePipelinePhase.cs#L107-L116), [AuditSigningService.cs](../../src/Romulus.Infrastructure/Audit/AuditSigningService.cs#L645-L688)
- **Problem:** DAT-XML kann mehrzeilige Werte enthalten. `ReadAuditRowsReverse` liest zeilenweise -> Felder mit `\n` zerlegt -> Eintrag wird `skippedUnsafe` -> Rollback ueberspringt.
- **Fix:** In `WriteAuditRowCore` `\r\n` aus Werten entfernen vor Quoting; ODER echten CSV-Reader (TextFieldParser/CsvHelper) nutzen.
- **Tests fehlen:**
  - [ ] Round-trip: AppendAuditRow mit `reason="line1\nline2"` -> ReadAuditRowsReverse liefert genau eine Zeile

### P1-13 — `DatRenamePipelinePhase` ignoriert `ConflictPolicy` ausser "Skip"
**Tags:** Data-Integrity Risk · Bug
- [ ] **Fix umsetzen**
- **File:** [DatRenamePipelinePhase.cs](../../src/Romulus.Infrastructure/Orchestration/DatRenamePipelinePhase.cs#L26-L120)
- **Problem:** Bei `Rename`/`Overwrite` keine Kollisionsbehandlung -> Move-Phase respektiert Policy, DatRename nicht -> Preview/Execute-Divergenz.
- **Fix:** `ResolveDestinationByPolicy(targetPath, ConflictPolicy)` analog Move-Phase.
- **Tests fehlen:**
  - [ ] 2 Files mit kollidierendem DAT-TargetName + `ConflictPolicy=Rename` -> beide existieren mit Suffixen

### P1-14 — `LiteDbCollectionIndex` faellt nach Recovery-Race still in In-Memory-Modus
**Tags:** Data-Integrity Risk · Concurrency
- [ ] **Fix umsetzen**
- **File:** [LiteDbCollectionIndex.cs](../../src/Romulus.Infrastructure/Index/LiteDbCollectionIndex.cs#L491-L515)
- **Problem:** Bei zwei Prozessen auf korrupter DB landet einer im in-memory degraded mode -> Snapshots werden geschrieben aber NIE persistiert. UI/CLI erfaehrt nichts.
- **Fix:** Im `catch` echten Fehler hochmelden ODER `IsDegraded`-Property exponieren und in API/GUI sichtbar machen.
- **Tests fehlen:**
  - [ ] Zwei parallel oeffnende Indizes auf korrupter DB -> beide bekommen Persistenz oder einer wirft sichtbar

### P1-15 — SSRF ueber DAT-Katalog (keine Host-Allowlist)
**Tags:** Security Risk · OWASP A10
- [ ] **Fix umsetzen**
- **Files:** [DatSourceService.cs](../../src/Romulus.Infrastructure/Dat/DatSourceService.cs#L62-L72), [DatSourceService.cs](../../src/Romulus.Infrastructure/Dat/DatSourceService.cs#L116-L124)
- **Problem:** `IsSecureUrl` prueft nur HTTPS. Keine Host-Allowlist, kein Private-IP/Loopback-Block, AutoRedirect ohne Re-Validation. Praeparierter Katalog kann interne HTTPS-Dienste probaen.
- **Fix:** Statische Allowlist (github.com, raw.githubusercontent.com, datomatic.no-intro.org, redump.org). Redirects manuell folgen + jeden Hop validieren. Loopback/RFC1918/Link-Local IPs hart blocken.
- **Tests fehlen:**
  - [ ] Praeparierter Katalog mit `https://127.0.0.1`, `https://10.x`, Redirect zu `169.254.169.254` -> alle ohne Connect/Read fehlschlagen

### P1-16 — HMAC-Tempfile ohne ACL beim Schreiben (Race)
**Tags:** Security Risk · OWASP A02
- [ ] **Fix umsetzen**
- **File:** [AuditSigningService.cs](../../src/Romulus.Infrastructure/Audit/AuditSigningService.cs#L67-L97)
- **Problem:** Tempfile mit Default-ACL geschrieben, ACL-Haertung erst nach `File.Move`. Race-Fenster fuer fremden Prozess.
- **Fix:** Tempfile direkt mit restriktiver ACL erzeugen (Windows: `FileSecurity` ueber `FileSystemAclExtensions.Create`. Unix: `File.Create` ohne Inhalt + `File.SetUnixFileMode(0600)` + Schluessel schreiben).
- **Tests fehlen:**
  - [ ] Linux mit `umask 022`: Schluessel-Datei nie `OtherRead`

### P1-17 — Kein Hash-Chain / Replay-Schutz zwischen Audit-Sidecars
**Tags:** Audit Integrity Risk · Security · OWASP A04/A08
- [ ] **Fix umsetzen**
- **Files:** [AuditSigningService.cs](../../src/Romulus.Infrastructure/Audit/AuditSigningService.cs#L131-L138), [AuditCsvStore.cs](../../src/Romulus.Infrastructure/Audit/AuditCsvStore.cs#L19)
- **Problem:** Payload `v1|<file>|<csv-sha>|<rows>|<utc>` enthaelt weder `keyId` noch Verweis auf vorherige Datei. Aelteres Sidecar+CSV kann an neuen Pfad kopiert werden -> akzeptiert.
- **Fix:** Payload um `previousSidecarHmac` und `keyId` erweitern. Append-only `audit-ledger`.
- **Tests fehlen:**
  - [ ] Aelteres Sidecar an neuen Pfad kopiert -> `VerifyMetadataSidecar` lehnt ab

### P1-18 — Audit-Rows zwischen Checkpoints sind ungeschuetzt
**Tags:** Audit Integrity Risk
- [ ] **Fix umsetzen**
- **File:** [AuditCsvStore.cs](../../src/Romulus.Infrastructure/Audit/AuditCsvStore.cs#L82)
- **Problem:** Sidecar deckt nur Stand zum letzten Checkpoint. Tail-Rows sind unverankert -> Angreifer kann sie modifizieren bevor naechster Checkpoint laeuft.
- **Fix:** Spalten `Seq` (monoton) und `PrevRowSha256`. Verify prueft Luecken und Hash-Kette.
- **Tests fehlen:**
  - [ ] `AppendingRowsWithoutCheckpoint_ThenTampering_IsDetectedByVerify`

### P1-19 — Kein HSTS / HTTPS-Erzwingung im Remote-Modus
**Tags:** Security Risk · OWASP A02/A05
- [ ] **Fix umsetzen**
- **Files:** [Program.cs](../../src/Romulus.Api/Program.cs#L37-L42), [HeadlessApiOptions.cs](../../src/Romulus.Api/HeadlessApiOptions.cs#L53-L66)
- **Problem:** `WebHost.UseUrls($"http://...")` startet ausschliesslich Klartext. Kein `UseHttpsRedirection`, kein HSTS. Operator vergisst Reverse-Proxy -> X-Api-Key + Payload im Klartext im LAN.
- **Fix:** Im Remote-Modus Kestrel mit `ListenAnyIP(port, opts => opts.UseHttps(certPath))` ODER Start hart abbrechen ohne TLS-Konfig. HSTS-Header bei `AllowRemoteClients=true`.
- **Tests fehlen:**
  - [ ] `AllowRemoteClients=true` ohne TLS-Konfig -> Programmstart wirft `InvalidOperationException`

### P1-20 — CLI `convert`/`header` umgehen Allowed-Roots-Policy
**Tags:** Parity Risk · Security
- [ ] **Fix umsetzen**
- **Files:** [Program.cs](../../src/Romulus.CLI/Program.cs#L994), [Program.cs](../../src/Romulus.CLI/Program.cs#L1034)
- **Problem:** `romulus convert --input "C:\Windows\System32\drivers\etc\hosts"` ohne Path-Policy-Check. API-Pfade pruefen `AllowedRootPathPolicy`, CLI nicht.
- **Fix:** `AllowedRootPathPolicy` aus Settings auch in CLI erzwingen.
- **Tests fehlen:**
  - [ ] `CliConvert_WithPathOutsideAllowedRoots_Fails`

### P1-21 — CLI Exit-Code 4 ("Completed with errors") wird nirgendwo emittiert
**Tags:** Parity Risk · CI-Killer
- [ ] **Fix umsetzen**
- **File:** [Program.cs](../../src/Romulus.CLI/Program.cs#L33)
- **Problem:** Doc deklariert `4=Completed with errors`. `grep "return 4"` = 0 Treffer. Run mit Failures schliesst mit Exit 0 -> CI sieht nichts.
- **Fix:** `result.Failures > 0 ? 4 : 0` in `ExecuteRunCoreAsync`. `convert` analog.
- **Tests fehlen:**
  - [ ] `RunWithPartialFailures_ReturnsExitCode4`

### P1-22 — Tool-Hash-Cache umgehbar via Timestomping (in user-writable Tool-Roots)
**Tags:** Security Risk · OWASP A02
- [ ] **Fix umsetzen**
- **File:** [ToolRunnerAdapter.cs](../../src/Romulus.Infrastructure/Tools/ToolRunnerAdapter.cs#L745-L774)
- **Problem:** Cache-Eintrag nur `(LastWriteTimeUtc, Length)`. Modifizierte Binary mit gleicher Groesse + zurueckgesetztem `LastWriteTime` matcht Cache. Betrifft `ROMULUS_CONVERSION_TOOLS_ROOT` und `LocalApplicationData` (psxtract, ciso, flips, xdelta3).
- **Fix:** Cheap-Preflight (erste/letzte 4 KB SHA256) zusaetzlich; ODER Cache nur pro Process-Lebenszyklus mit Re-Verify im ersten Call. Cache fuer user-writable Roots deaktivieren.
- **Tests fehlen:**
  - [ ] Bytes ueberschreiben + LastWriteTime resetten -> Re-Verify schlaegt fehl

---

## P2 – Mittlere Risiken

### P2-01 — `SafeRegex` Fail-Open: Junk-Klassifikation kann verschluckt werden
**Tags:** Determinism Risk
- [ ] **Fix umsetzen**
- **File:** [SafeRegex.cs](../../src/Romulus.Core/SafeRegex.cs#L23-L50)
- **Problem:** Bei `RegexMatchTimeoutException` liefert `IsMatch` `false`/`Match.Empty`. Pathological Input unter Last -> File gilt als „kein Junk" -> falsche Sortierung.
- **Fix:** Klassifikatorische Regexes bei Timeout in `Review` mit Reason `regex-timeout` schieben.
- **Tests fehlen:**
  - [ ] Synthetischer Pattern + Eingabe mit garantiertem Timeout -> deterministischer Endzustand

### P2-02 — `SafetyValidator` rejected `\\?\` Long-Path-Praefix komplett
**Tags:** Filesystem Edge Case
- [ ] **Fix umsetzen**
- **File:** [SafetyValidator.cs](../../src/Romulus.Infrastructure/Safety/SafetyValidator.cs#L101-L105)
- **Fix:** Praefix erkennen, abschaelen, intern ohne Praefix fuehren ODER klare Fehlermeldung.

### P2-03 — `IsSafeExtension` erlaubt mehrfach-Punkte (`.chd.exe`)
**Tags:** Security Edge Case
- [ ] **Fix umsetzen**
- **File:** [ConversionExecutor.cs](../../src/Romulus.Infrastructure/Conversion/ConversionExecutor.cs#L364-L383)
- **Fix:** Whitelist konkreter Extensions pro Tool aus Registry.

### P2-04 — `RunEnvironmentBuilder` 1.372 LoC (Builder + Resolver + RegEx + Settings-Loader)
**Tags:** Architecture Debt Hotspot
- [ ] **Refactor umsetzen**
- **File:** [RunEnvironmentBuilder.cs](../../src/Romulus.Infrastructure/Orchestration/RunEnvironmentBuilder.cs)
- **Fix:** Aufteilung `IDataDirectoryResolver`, `ISettingsLoader`, `IDatRootResolver`. DI-Registrierung in `AddRomulusCore()`/CLI-ServiceProvider.

### P2-05 — `EnrichmentPipelinePhase` 1.110 LoC mit 5 DAT-Hash-Stages + BIOS + Family + Streaming
**Tags:** Architecture Debt Hotspot
- [ ] **Refactor umsetzen**
- **File:** [EnrichmentPipelinePhase.cs](../../src/Romulus.Infrastructure/Orchestration/EnrichmentPipelinePhase.cs)
- **Fix:** `IDatLookupService`, `IBiosResolver`, `ICrossConsoleDatPolicy` extrahieren.

### P2-06 — `ClassificationIoResolver` ist statischer Service Locator
**Tags:** Architecture Debt Hotspot
- [ ] **Fix umsetzen**
- **File:** [ClassificationIoResolver.cs](../../src/Romulus.Core/Classification/ClassificationIoResolver.cs#L7-L31)
- **Fix:** `IClassificationIo` per ctor in `ConsoleDetector`/`ContentSignatureClassifier`/`DiscHeaderDetector` injizieren.

### P2-07 — `MainWindow.xaml.cs` startet API per `Process.Start("dotnet run")`
**Tags:** Architecture Debt
- [ ] **Fix umsetzen**
- **File:** [MainWindow.xaml.cs](../../src/Romulus.UI.Wpf/MainWindow.xaml.cs#L317-L321)
- **Fix:** `IRomulusApiHost`-Service in Infrastructure (Kestrel oder Pfad zu `Romulus.Api.exe`); andernfalls Feature deaktivieren.

### P2-08 — Bootstrap-Idiom `ResolveDataDir + LoadSettings` 10+ mal kopiert
**Tags:** Duplicate Logic
- [ ] **Fix umsetzen**
- **Files:** API (4x), CLI (3x), WPF (3x)
- **Fix:** `IRomulusEnvironmentContext` als Singleton in DI.

### P2-09 — Konkurrierende Status-Modelle fuer Run/Job
**Tags:** Shadow Logic · Parity Risk
- [ ] **Fix umsetzen**
- **Files:** [RunState.cs](../../src/Romulus.UI.Wpf/Models/RunState.cs), [RunResultBuilder.cs](../../src/Romulus.Infrastructure/Orchestration/RunResultBuilder.cs#L13), [RunManager.cs](../../src/Romulus.Api/RunManager.cs#L323), [LiteDbCollectionIndex.cs](../../src/Romulus.Infrastructure/Index/LiteDbCollectionIndex.cs#L915), [AdvancedModels.cs](../../src/Romulus.Contracts/Models/AdvancedModels.cs#L12)
- **Fix:** `enum RunLifecycleStatus` in Contracts. WPF `RunState` als reine UI-Projektion.

### P2-10 — `FeatureService` (static) und `FeatureCommandService` (instance) parallel
**Tags:** Halbfertiger Refactor
- [ ] **Fix umsetzen**
- **Files:** `FeatureService.cs` + 8 Partials, `FeatureCommandService.cs` + 7 Partials
- **Fix:** Einen Stack waehlen, anderen aufloesen.

### P2-11 — Inline-Geschaeftslogik in API-Endpoints
**Tags:** Shadow Logic
- [ ] **Fix umsetzen**
- **Files:** [Program.cs](../../src/Romulus.Api/Program.cs#L546-L741), [Program.RunWatchEndpoints.cs](../../src/Romulus.Api/Program.RunWatchEndpoints.cs#L150-L410)
- **Fix:** `DatUpdateService.UpdateAsync()`, `RunCreateService.CreateFromRequestAsync()`, `ConvertEndpointService.RunAsync()`.

### P2-12 — `NormalizeConsoleKey` (Core) vs. `RxValidConsoleKey` (Infrastructure) — konkurrierende Wahrheit
**Tags:** Determinism Risk · Duplicate Logic
- [ ] **Fix umsetzen**
- **File:** [DeduplicationEngine.cs](../../src/Romulus.Core/Deduplication/DeduplicationEngine.cs#L184-L201)
- **Problem:** Core akzeptiert Unicode-Letter/Digit (arabisch-indische Ziffern, kyrillisch). Sorting whitelistet `[A-Z0-9_-]+`.
- **Fix:** Eine Validierung in Contracts.

### P2-13 — `FormatConverterAdapter.ConvertLegacy`-Pfad ist Refactor-Ueberbleibsel
**Tags:** Hygiene · Konsequenz von P0-01
- [ ] **Loeschen**
- **File:** [FormatConverterAdapter.cs](../../src/Romulus.Infrastructure/Conversion/FormatConverterAdapter.cs#L393)

### P2-14 — CSP `style-src 'nonce-...'` blockiert Inline-`style="..."`-Attribute
**Tags:** Bug · Security (Style-Injection-Risiko)
- [ ] **Fix umsetzen**
- **File:** [ReportGenerator.cs](../../src/Romulus.Infrastructure/Reporting/ReportGenerator.cs#L107)
- **Problem:** Inline-Styles brauchen `'unsafe-inline'` oder `style-src-attr 'unsafe-hashes'`. Bar-Chart kollabiert auf 0px in CSP3-strikten Browsern.
- **Fix:** Inline-Styles in `<style nonce="...">` mit eindeutigen Klassen pro Bar-Breite verschieben. CSP zusaetzlich `base-uri 'none'`, `form-action 'none'`, `frame-ancestors 'none'`.
- **Tests fehlen:**
  - [ ] Snapshot-Test: kein `style="` Substring im Output

### P2-15 — `FileHashService.FlushPersistentCache` nicht cross-process-atomar
**Tags:** Concurrency · Data-Integrity
- [ ] **Fix umsetzen**
- **File:** [FileHashService.cs](../../src/Romulus.Infrastructure/Hashing/FileHashService.cs#L161-L183)
- **Problem:** Zwei Prozesse auf Default-Cache -> lost updates. Keine Mutex-Bracketing wie `AuditCsvStore`.
- **Fix:** Cross-Process-Mutex einbauen; vor Schreiben Datei neu laden + mergen.
- **Tests fehlen:**
  - [ ] Stress: 2 Tasks schreiben disjunkte Hashes -> Vereinigung im JSON

### P2-16 — `HeaderlessHasher.ComputeN64CanonicalHash` doppelte Allokation, faengt OOM nicht
**Tags:** Resource · Bug
- [ ] **Fix umsetzen**
- **File:** [HeaderlessHasher.cs](../../src/Romulus.Infrastructure/Hashing/HeaderlessHasher.cs#L83-L101)
- **Fix:** `var normalized = bytes;` (Kopie weg). Groessenlimit z. B. 256 MB als harte Obergrenze + Log statt OOM.
- **Tests fehlen:**
  - [ ] Mock-FileStream 200 MB N64 -> Hash korrekt, Speicher < 1.5x

### P2-17 — `ScreenScraperMetadataProvider` HttpClient ohne Timeout
**Tags:** Resource · Concurrency
- [ ] **Fix umsetzen**
- **File:** [Program.cs](../../src/Romulus.Api/Program.cs#L46-L54)
- **Fix:** `IHttpClientFactory` mit `Timeout=TimeSpan.FromSeconds(30)` und `SocketsHttpHandler { PooledConnectionLifetime = 5min }`.
- **Tests fehlen:**
  - [ ] Mock-Handler `await Task.Delay(60_000)` -> wirft `TaskCanceledException` < 35s

### P2-18 — `RunManager.Dispose()` (synchron) cancelt aktive Runs nicht
**Tags:** Concurrency · Resource Leak
- [ ] **Fix umsetzen**
- **File:** [RunManager.cs](../../src/Romulus.Api/RunManager.cs#L98-L106)
- **Fix:** `_lifecycle.ShutdownAsync().GetAwaiter().GetResult()` mit Timeout ODER `IAsyncDisposable`-only.
- **Tests fehlen:**
  - [ ] TestServer mit aktivem DryRun -> `Dispose` (sync) -> CancellationToken `IsCancellationRequested`

### P2-19 — `SettingsService.SaveFrom` Cross-Process-Race korrumpiert `settings.json`
**Tags:** Concurrency
- [ ] **Fix umsetzen**
- **File:** [SettingsService.cs](../../src/Romulus.UI.Wpf/Services/SettingsService.cs#L389-L406)
- **Fix:** Tmp-Pfad mit `ProcessId + Guid`. Cross-Process-Mutex `Global\Romulus.Settings`. Backup `settings.json.bak` vor Migration.
- **Tests fehlen:**
  - [ ] 4 Tasks `SaveFrom` parallel -> finale Datei immer valides JSON

### P2-20 — `MovePipelinePhase` zaehlt `failCount` ohne Audit-Zeile bei `root-not-found`
**Tags:** Bug · Audit-Luecke
- [ ] **Fix umsetzen**
- **File:** [MovePipelinePhase.cs](../../src/Romulus.Infrastructure/Orchestration/MovePipelinePhase.cs#L60-L102)
- **Fix:** In Fail-Branches `AuditStore.AppendAuditRow(action="MOVE_FAILED", reason="root-not-found"|"path-resolve-failed")`.
- **Tests fehlen:**
  - [ ] Roots = `["C:\A"]`, Loser-Pfad `D:\foo.rom` -> exakt 1 Audit-Zeile

### P2-21 — Hardcoded deutsche Strings in WPF-Startup/Crash-Dialogen (i18n umgangen)
**Tags:** i18n Risk
- [ ] **Fix umsetzen**
- **Files:** [App.xaml.cs](../../src/Romulus.UI.Wpf/App.xaml.cs#L31-L37), [App.xaml.cs](../../src/Romulus.UI.Wpf/App.xaml.cs#L59-L64), [App.xaml.cs](../../src/Romulus.UI.Wpf/App.xaml.cs#L120-L125), [MainWindow.xaml.cs](../../src/Romulus.UI.Wpf/MainWindow.xaml.cs#L94-L100)
- **Fix:** Eingebetteter Mini-Resource mit `de`/`en`/`fr`-Tabelle ODER englischer Fallback.
- **Tests fehlen:**
  - [ ] Reflection-Scan auf MessageBox-Aufrufe

### P2-22 — Datenschemas ohne `schemaVersion`
**Tags:** False Confidence Risk
- [ ] **Fix umsetzen**
- **Files:** `data/consoles.json`, `data/console-maps.json`, `data/defaults.json`, `data/dat-catalog.json`, `data/format-scores.json`, `data/rules.json`, `data/ui-lookups.json`
- **Fix:** `schemaVersion` Pflichtfeld + Loader-Versionsgate + sichtbare Warnung bei unbekannter Version.
- **Tests fehlen:**
  - [ ] `LoadConsoles_WithMissingSchemaVersion_LogsWarning`
  - [ ] `LoadFormatScores_WithFutureSchemaVersion_FailsExplicitly`

### P2-23 — API-Request-Logging anfaellig fuer Log-Injection via URL-Pfad
**Tags:** Audit Integrity Risk · Log Injection · OWASP A09/A03
- [ ] **Fix umsetzen**
- **File:** [Program.cs](../../src/Romulus.Api/Program.cs#L233)
- **Problem:** `GET /healthz%0a%5bAUDIT%5d...` -> Logger schreibt zwei Zeilen. `correlationId` ist sanitized, `path` nicht.
- **Fix:** `SafeConsoleWriteLine` `\r\n\t` ersetzen ODER JSONL-Logging zentral.
- **Tests fehlen:**
  - [ ] `RequestLog_WithEncodedNewlinesInPath_DoesNotEmitMultilineEntry`

### P2-24 — `LocalizationService`-Fallback inkonsistent (DE-only Keys verschwinden fuer EN)
**Tags:** i18n Risk
- [ ] **Fix umsetzen**
- **File:** [LocalizationService.cs](../../src/Romulus.UI.Wpf/Services/LocalizationService.cs#L57-L80)
- **Problem:** Code-Comment sagt „DE base + overlay", Code nutzt EN als Base.
- **Fix:** Entscheiden + dokumentieren. Build-Time-Lint, dass alle Sprachdateien identische Key-Sets haben.
- **Tests fehlen:**
  - [ ] `Localization_AllLocalesHaveIdenticalKeySet`

### P2-25 — `JsonlLogWriter` keine Groessenbegrenzung pro Aufruf, kein Auto-Rotate
**Tags:** False Confidence Risk · Resource
- [ ] **Fix umsetzen**
- **File:** [JsonlLogWriter.cs](../../src/Romulus.Infrastructure/Logging/JsonlLogWriter.cs#L97-L127)
- **Fix:** In `Write` periodisch `RotateIfNeeded` automatisch triggern. Message auf 16 KB cappen mit `… [truncated]`-Suffix.
- **Tests fehlen:**
  - [ ] `JsonlLog_AfterMaxBytes_AutoRotates`
  - [ ] `JsonlLog_VeryLongMessage_IsTruncated`

### P2-26 — `MainWindow.OnClosing` ist `async void` -> unbeobachtete Exceptions killen App
**Tags:** False Confidence Risk · Concurrency
- [ ] **Fix umsetzen**
- **File:** [MainWindow.xaml.cs](../../src/Romulus.UI.Wpf/MainWindow.xaml.cs#L148)
- **Fix:** Methodenrumpf in try/catch wrappen mit Notfall-`crash.log`-Pfad. Settings vor Close immer flushen.
- **Tests fehlen:**
  - [ ] `OnClosing_WhenSaveSettingsThrows_StillReleasesResources`

### P2-27 — `ProcessStartInfo` erbt komplettes Eltern-Environment (DLL-Hijack / `LD_PRELOAD`)
**Tags:** Security Risk · OWASP A04/A08
- [ ] **Fix umsetzen**
- **File:** [ToolRunnerAdapter.cs](../../src/Romulus.Infrastructure/Tools/ToolRunnerAdapter.cs#L375-L388)
- **Fix:** `psi.EnvironmentVariables.Clear()`. Nur explizit `SystemRoot, windir, ComSpec, TEMP, PATH` durchreichen. `WorkingDirectory` auf frischen Tempordner. Windows: `SetDefaultDllDirectories(LOAD_LIBRARY_SEARCH_SYSTEM32)`.
- **Tests fehlen:**
  - [ ] `LD_PRELOAD=/tmp/evil.so` -> Child-Env enthaelt es nicht

### P2-28 — Sensible Pfade landen ueber `_log` in Audit-/Konsolen-Logs
**Tags:** Security Risk · OWASP A09/A03
- [ ] **Fix umsetzen**
- **Files:** [AuditSigningService.cs](../../src/Romulus.Infrastructure/Audit/AuditSigningService.cs#L388-L399), [Program.cs](../../src/Romulus.Api/Program.cs#L221-L225)
- **Fix:** Zentrale `LogPathRedactor.Redact(path)` (analog `RedactAbsolutePaths` in `ToolRunnerAdapter`). `\r\n\t` aus User-Strings entfernen.
- **Tests fehlen:**
  - [ ] Audit-Zeile mit `OldPath="C:\\Users\\bob\\\nFAKE"` -> Logsenke erhaelt eine Zeile, redigiert

### P2-29 — `JsonDocument.Parse` fuer Request-Bodies ohne explizites `MaxDepth`
**Tags:** Security · OWASP A03/A05
- [ ] **Fix umsetzen**
- **Files:** [Program.cs](../../src/Romulus.Api/Program.cs#L450-L465), [Program.cs](../../src/Romulus.Api/Program.cs#L519-L533)
- **Fix:** `new JsonDocumentOptions { MaxDepth = 8, AllowTrailingCommas = false }` ODER auf source-generated `ApiJsonSerializerOptions` migrieren.
- **Tests fehlen:**
  - [ ] Body mit 100 verschachtelten `[`-Objekten -> 400, kein 500

### P2-30 — Key-Datei wird ohne Berechtigungspruefung gelesen
**Tags:** Security · OWASP A02
- [ ] **Fix umsetzen**
- **File:** [AuditSigningService.cs](../../src/Romulus.Infrastructure/Audit/AuditSigningService.cs#L42-L52)
- **Problem:** Aeltere Versionen / `xcopy /O` ohne `/X` koennen `Romulus.hmac` mit `0644` hinterlassen -> kommentarlose Verwendung.
- **Fix:** Vor Verwendung Linux: `File.GetUnixFileMode(path)` pruefen. Windows: `FileInfo.GetAccessControl()` enumerieren. Bei Verstoss fail-closed.
- **Tests fehlen:**
  - [ ] Test mit „lockerer" Key-Datei -> Service muss laden ablehnen

---

## P3 – Wartbarkeit / Niedrige Risiken

### P3-01 — Mojibake `�` Literal im HTML-Report-Header
**Tags:** Encoding-Bug · Cosmetic
- [ ] **Fix umsetzen**
- **File:** [ReportGenerator.cs](../../src/Romulus.Infrastructure/Reporting/ReportGenerator.cs#L36)
- **Fix:** UTF-8 mit BOM speichern, Zeichen ersetzen. Snapshot-Test des Headers ergaenzen.

### P3-02 — Default-`MainViewModel`-ctor erzeugt Services manuell
**Tags:** Architecture Debt
- [ ] **Fix umsetzen**
- **File:** [MainViewModel.cs](../../src/Romulus.UI.Wpf/ViewModels/MainViewModel.cs#L70)
- **Fix:** WPF auf `Microsoft.Extensions.DependencyInjection` umstellen.

### P3-03 — `RunOrchestrator.Dispose` setzt nur Flag, kein async-Shutdown
**Tags:** Architecture Debt
- [ ] **Fix umsetzen**
- **File:** [RunManager.cs](../../src/Romulus.Api/RunManager.cs#L102-L111)

### P3-04 — Magic-Status-Strings statt Konstanten/Enum
**Tags:** Hygiene
- [ ] **Fix umsetzen**
- **Files:** [AdvancedModels.cs](../../src/Romulus.Contracts/Models/AdvancedModels.cs#L12), [MainViewModel.cs](../../src/Romulus.UI.Wpf/ViewModels/MainViewModel.cs#L51-L54)

### P3-05 — `DatCatalogViewModel` laedt Catalog dreifach in einer Klasse
**Tags:** Hygiene · zusammen mit P1-03
- [ ] **Fix umsetzen** (mit P1-03)
- **File:** [DatCatalogViewModel.cs](../../src/Romulus.UI.Wpf/ViewModels/DatCatalogViewModel.cs#L145)

### P3-06 — Reparse-Point `MaxAncestryDepth = 256` nur symbolisch
**Tags:** Hygiene
- [ ] **Fix umsetzen**
- **File:** [SafetyValidator.cs](../../src/Romulus.Infrastructure/Safety/SafetyValidator.cs#L11)
- **Fix:** Visited-Set ueber bereits besuchte Full-Paths.

### P3-07 — Test `SelectWinner_IsDeterministic` ist Alibi-Test
**Tags:** False Confidence Risk · Test-Hygiene
- [ ] **Fix umsetzen**
- **File:** [DeduplicationEngineTests.cs](../../src/Romulus.Tests/DeduplicationEngineTests.cs#L127-L143)
- **Fix:** Echte Permutation einbauen ODER umbenennen `_AreIdempotent`.

### P3-08 — Test `IsValidTransition_SkipPhases_Valid` erlaubt unrealistische Spruenge
**Tags:** False Confidence Risk · Test-Hygiene
- [ ] **Fix umsetzen**
- **File:** [WpfNewTests.cs](../../src/Romulus.Tests/WpfNewTests.cs#L632-L639)

### P3-09 — `RunManager.Cancel` weckt `WaitForCompletion` nicht (Latenz im Hashing)
**Tags:** Concurrency Edge Case
- [ ] **Fix umsetzen**
- **File:** [RunLifecycleManager.cs](../../src/Romulus.Api/RunLifecycleManager.cs#L181-L201)
- **Fix:** Stream-Hashing in 64 KB-Chunks mit Token-Check.
- **Tests fehlen:**
  - [ ] Cancel waehrend simulierter 30-s-Hash-Operation -> WaitForCompletion < 2s

### P3-10 — `FormatScorer.GetRegionScore`: leere/lange `preferOrder` -> Score-Inversion
**Tags:** False Confidence
- [ ] **Fix umsetzen**
- **File:** [FormatScorer.cs](../../src/Romulus.Core/Scoring/FormatScorer.cs#L246)
- **Fix:** `Math.Max(1, 1000 - idx)`. Default-Reihenfolge bei leerem `preferOrder`.
- **Tests fehlen:**
  - [ ] `RegionScore_WithEmptyPreferOrder_ReturnsDocumentedDefault`
  - [ ] `RegionScore_WithMoreThan1000Preferences_NeverGoesNegative`

### P3-11 — `/dashboard/bootstrap` anonym (Information Disclosure)
**Tags:** Security · OWASP A05
- [ ] **Fix umsetzen**
- **Files:** [ProgramHelpers.cs](../../src/Romulus.Api/ProgramHelpers.cs#L124-L128), [DashboardDataBuilder.cs](../../src/Romulus.Api/DashboardDataBuilder.cs#L15-L32)
- **Fix:** Hinter API-Key ziehen ODER Response auf `{ "version": "x.y" }` reduzieren bei `AllowRemoteClients=true`.
- **Tests fehlen:**
  - [ ] GET /dashboard/bootstrap ohne API-Key im Remote-Modus -> 401

### P3-12 — `force`-Flag in `/dats/update` wirft `InvalidOperationException` bei Nicht-Bool
**Tags:** Security · OWASP A05
- [ ] **Fix umsetzen**
- **File:** [Program.cs](../../src/Romulus.Api/Program.cs#L460-L469)
- **Fix:** `forceProp.ValueKind == JsonValueKind.True` ODER `TryGetBoolean`. Catch erweitern.
- **Tests fehlen:**
  - [ ] Body `{"force":"yes"}` -> 400, nicht 500

---

## Round 3 – Neue Funde

> Quellen: parallele Audits 3a (DAT/Hash/Tools – `R3-A-*`), 3b (Settings/Localization/UI – `R3-B-*`), 3c (API/CLI/Reports/Logging – `R3-C-*`).
> Insgesamt 44 zusaetzliche Funde.

### Block A – DAT / Hash / Tools / Conversion (`R3-A-*`)

#### R3-A-01 — ZIP CRC32 Fast-Path vertraut zentralem Verzeichnis ungeprueft (P0)
**Tags:** Release-Blocker · Verify Fail-Open · Determinism
- [ ] **Fix umsetzen** (gleiche Klasse wie P0-01)
- **File:** [ArchiveHashService.cs](../../src/Romulus.Infrastructure/Hashing/ArchiveHashService.cs#L200-L210) (`HashZipEntries`, `useZipCrc32FastPath`)
- **Problem:** Bei `hashType == "CRC"|"CRC32"` wird `entry.Crc32` direkt aus dem ZIP-Central-Directory als „Hash" zurueckgegeben, ohne den entpackten Stream je gegen die CRC zu pruefen. Das CRC-Feld ist Metadaten und vom Angreifer frei waehlbar. Gerade MAME/FBNeo-DATs sind CRC32-only -> jede DAT-Match-Entscheidung kann erzwungen werden.
- **Fix:** Fast-Path entfernen ODER entpackten Stream zusaetzlich gegen `entry.Crc32` validieren.
- **Tests fehlen:**
  - [ ] ZIP mit gefaelschter CRC32 im Central-Directory -> `HashZipEntries` darf nicht den Header-Wert zurueckgeben
  - [ ] Property: `centralDirCrc == StreamCrc`, sonst Reject

#### R3-A-02 — `ContentSignatureClassifier` markiert ROMs faelschlich als BMP/MP3 (P1)
**Tags:** Junk-Misclassification · Determinism
- [ ] **Fix umsetzen**
- **File:** [ContentSignatureClassifier.cs](../../src/Romulus.Core/Classification/ContentSignatureClassifier.cs#L46-L53)
- **Problem:** BMP-Erkennung nur 2 Byte (`0x42 0x4D` = "BM"). MP3 nur Frame-Sync `0xFF (b1 & 0xE0)==0xE0` -> matcht ROMs mit `0xFF`-Padding (typisch SNES/MD).
- **Fix:** BMP zusaetzlich `BinaryPrimitives.ReadUInt32LE(header[2..6]) == fileSize`. MP3-Sync um Bitrate-/Samplerate-Index validieren.
- **Tests fehlen:**
  - [ ] SNES-ROM `0xFF 0xE2 ...` -> nicht MP3
  - [ ] Property 1000 zufaellige Header -> kein BMP/MP3-Hit

#### R3-A-03 — DatIndex mischt heterogene Hash-Typen ohne Tag (P1)
**Tags:** Determinism · Cross-Console False-Match · Architecture Debt
- [ ] **Fix umsetzen**
- **Files:** [DatRepositoryAdapter.cs](../../src/Romulus.Infrastructure/Dat/DatRepositoryAdapter.cs#L266-L307), [DatIndex.cs](../../src/Romulus.Contracts/Models/DatIndex.cs#L100-L115)
- **Problem:** Pro ROM-Zeile wird je nach Verfuegbarkeit SHA1 -> MD5 -> CRC32 in dieselbe Map indiziert. `LookupEntry` weiss nicht, was es speichert -> Type-Pollution.
- **Fix:** `DatIndexEntry.HashType`-Feld + `Lookup(consoleKey, hashType, hash)` filtert auf gleichen Typ.
- **Tests fehlen:**
  - [ ] DAT mit MD5(X), andere DAT mit gleichem 32-Hex-String als CRC32 -> getrennte Lookups

#### R3-A-04 — `HeaderRepairService` ueberschreibt Backup-Datei mit korruptem Inhalt (P1)
**Tags:** Data-Integrity · Audit-Luecke
- [ ] **Fix umsetzen**
- **File:** [HeaderRepairService.cs](../../src/Romulus.Infrastructure/Hashing/HeaderRepairService.cs#L48)
- **Problem:** `CopyFile(path, path+".bak", overwrite:true)` ueberschreibt vorhandenes Backup. Sequenz: erster Repair crasht halb-geschrieben -> zweiter Repair ersetzt Original-`.bak` mit Korruption.
- **Fix:** Vor Ueberschreiben SHA256-Vergleich; sonst `.bak.{utc}.bak`. Pflicht-Verify nach Schreiben.
- **Tests fehlen:**
  - [ ] Zweimal Repair -> `.bak` enthaelt Original

#### R3-A-05 — `ChdmanToolConverter.ConvertMultiCueArchive` ignoriert effektives chdman-Subcommand pro Disc (P1)
**Tags:** Conversion-Bug · Determinism · Parity
- [ ] **Fix umsetzen**
- **File:** [ChdmanToolConverter.cs](../../src/Romulus.Infrastructure/Conversion/ChdmanToolConverter.cs#L210-L235)
- **Problem:** Single-CUE-Pfad nutzt `ResolveEffectiveChdmanCommand`, Multi-CUE-Pfad nicht -> Mischarchive (PSX-CD + PS2-DVD) werden mit falschem Subcommand verarbeitet.
- **Fix:** `effectiveCommand` in der `for`-Schleife pro CUE neu aufloesen.
- **Tests fehlen:**
  - [ ] Multi-CUE mit gemischtem Disc-Typ

#### R3-A-06 — `ConversionOutputValidator.ValidateMagicHeader` nie aufgerufen (P1)
**Tags:** Verify Fail-Open · Hygiene
- [ ] **Fix umsetzen**
- **File:** [ConversionOutputValidator.cs](../../src/Romulus.Infrastructure/Conversion/ConversionOutputValidator.cs#L88-L112)
- **Problem:** Magic-Tabelle existiert, wird aber von `TryValidateCreatedOutput` nie aufgerufen -> 6-Byte-Output mit Muell-Inhalt gilt als „valid".
- **Fix:** In `TryValidateCreatedOutput` Magic-Check ergaenzen, Mindest-Header-Bytes hochziehen.
- **Tests fehlen:**
  - [ ] 6-Byte `00`-Output bei `.zip` -> false

#### R3-A-07 — DatRepositoryAdapter 7z-Extraktion folgt Junctions in Sub-Verzeichnissen (P1)
**Tags:** Security · Path Traversal · OWASP A01
- [ ] **Fix umsetzen**
- **File:** [DatRepositoryAdapter.cs](../../src/Romulus.Infrastructure/Dat/DatRepositoryAdapter.cs#L539-L560) (`TryParse7zDat`)
- **Problem:** `Directory.GetFiles(tempDir, ..., AllDirectories)` ohne Reparse-Point-Pre-Check, anders als `ArchiveHashService`. Junction-Eintrag im 7z laesst Walk nach `C:\` rekursieren.
- **Fix:** Pre-Walk auf Reparse-Points; ODER `7z -snl-`.
- **Tests fehlen:**
  - [ ] 7z mit Verzeichnis-Junction -> Parse leer

#### R3-A-08 — `ExtractZipSafe` Zip-Bomb-Pruefung trustet `entry.Length` (P1)
**Tags:** Security · DoS · OWASP A05
- [ ] **Fix umsetzen**
- **File:** [ChdmanToolConverter.cs](../../src/Romulus.Infrastructure/Conversion/ChdmanToolConverter.cs#L264-L295) (`ExtractZipSafe`)
- **Problem:** Compression-Ratio-Check + `totalUncompressed` basieren auf Central-Directory-Werten (Angreifer-kontrolliert). Bombs mit gefaelschter `Length` umgehen Cap.
- **Fix:** Stream-Extraktion mit Live-Bytecounter + harter Cap.
- **Tests fehlen:**
  - [ ] Bomb mit `centralDir.uncompressed_size = 1024` aber 1 GB Output -> Abbruch

#### R3-A-09 — `HeaderlessHasher` mappt CRC32 still auf SHA1 (P1)
**Tags:** Determinism · Verify Fail-Open · DAT-Mismatch
- [ ] **Fix umsetzen**
- **File:** [HeaderlessHasher.cs](../../src/Romulus.Infrastructure/Hashing/HeaderlessHasher.cs#L116-L122)
- **Problem:** Switch hat keinen `CRC`/`CRC32`-Branch -> `_ => SHA1.Create()`. Headerless-Anforderung mit CRC32 liefert SHA1-Hex, kein DAT-Match moeglich.
- **Fix:** `if (NormalizeHashType(hashType) is "CRC" or "CRC32") return Crc32.HashStream(fs);` vor Branch.
- **Tests fehlen:**
  - [ ] `ComputeHeaderlessHash(nesFile, "NES", "CRC32")` -> 8-Hex-String

#### R3-A-10 — `ToolRunnerAdapter.ReadToEndWithByteBudget` liest weiter nach Truncation, ignoriert CT (P2)
**Tags:** DoS · Resource Leak · Concurrency
- [ ] **Fix umsetzen**
- **File:** [ToolRunnerAdapter.cs](../../src/Romulus.Infrastructure/Tools/ToolRunnerAdapter.cs#L535-L580)
- **Problem:** Nach Truncation `continue;` ohne `ct.IsCancellationRequested`-Check -> Reader-Task verschlingt unbegrenzt Bytes; blockiert `Process.Dispose`.
- **Fix:** CancellationToken in Methodensignatur, im Inner-Loop pruefen, bei `2*maxBytes` `Process.Kill(entireProcessTree:true)`.
- **Tests fehlen:**
  - [ ] Tool emittiert 1 GB stdout -> Reader < 5s nach Cancel

#### R3-A-11 — DatRepositoryAdapter Fallback-Chain falsch verschachtelt (P2)
**Tags:** Bug · DAT-Lookup · Determinism
- [ ] **Fix umsetzen**
- **File:** [DatRepositoryAdapter.cs](../../src/Romulus.Infrastructure/Dat/DatRepositoryAdapter.cs#L283-L307)
- **Problem:** `is not` verschachtelte Bedingungen: SHA1-Request bekommt MD5-Eintrag mit `selectedHashType="SHA1"` -> Index semantisch falsch.
- **Fix:** Klare Tabelle pro `requestedHashType` mit Fallback-Liste; `selectedHashType` korrekt setzen + Warning.

#### R3-A-12 — `ToolRunnerAdapter.RunProcess` ignoriert `WaitForExit(10s)`-Returnwert (P2)
**Tags:** Bug · Concurrency
- [ ] **Fix umsetzen**
- **File:** [ToolRunnerAdapter.cs](../../src/Romulus.Infrastructure/Tools/ToolRunnerAdapter.cs#L468-L483)
- **Problem:** Bool-Return ignoriert -> `process.ExitCode` wirft `InvalidOperationException` wenn Child detacht.
- **Fix:** `if (!process.WaitForExit(10_000)) { TryTerminate(...); return BuildFailureOutput(...); }`.

#### R3-A-13 — `GetDatGameKey` Trim-Reihenfolge bricht Whitespace-Variationen (P3)
**Tags:** Determinism · Edge Case
- [ ] **Fix umsetzen**
- **File:** [DatRepositoryAdapter.cs](../../src/Romulus.Infrastructure/Dat/DatRepositoryAdapter.cs#L88-L92)
- **Fix:** `$"{console.Trim()}|{gameName.Trim()}".ToLowerInvariant()`.

#### R3-A-14 — SNES-Copier-Detektion via `% 1024 == 512` ohne Header-Validierung (P2)
**Tags:** Determinism · DAT-Mismatch · Bug
- [ ] **Fix umsetzen**
- **File:** [HeaderSizeMap.cs](../../src/Romulus.Core/Classification/HeaderSizeMap.cs#L29)
- **Problem:** Heuristik laesst legitime SNES-ROMs ohne Copier-Header faelschlich Headerless-Skip bekommen -> No-Intro-CRC matcht nicht.
- **Fix:** SNES-Copier-Check an LoROM/HiROM-Internal-Header-Validierung knuepfen.

#### R3-A-15 — GBA/GB-Detektion auf Basis nur 4 Logo-Bytes (P2)
**Tags:** False-Classification · Determinism
- [ ] **Fix umsetzen**
- **File:** [CartridgeHeaderDetector.cs](../../src/Romulus.Core/Classification/CartridgeHeaderDetector.cs#L107-L135)
- **Problem:** 4-Byte-Match `24 FF AE 51` (GBA) bzw. `CE ED 66 66` (GB) erzeugt False-Positives bei beliebigen .bin-Dateien.
- **Fix:** Vollstaendige 156-Byte-/48-Byte-Logo-Validierung ODER Nintendo-Pruefsumme `0x134..0x14C`.

#### R3-A-16 — DatRepositoryAdapter waehlt nichtdeterministisch bei mehreren inneren `.dat`/`.xml` (P3)
**Tags:** Determinism · Bug
- [ ] **Fix umsetzen**
- **File:** [DatRepositoryAdapter.cs](../../src/Romulus.Infrastructure/Dat/DatRepositoryAdapter.cs#L548-L555)
- **Fix:** Multi-DAT mergen ODER explizit warnen.

---

### Block B – Settings / Localization / UI / Schemas (`R3-B-*`)

#### R3-B-01 — `SettingsService.Load` ueberschreibt gemergte Defaults mit hardcoded Fallbacks (P1)
**Tags:** Settings · Single-Source-of-Truth · Defaults Regression
- [ ] **Fix umsetzen**
- **File:** [SettingsService.cs](../../src/Romulus.UI.Wpf/Services/SettingsService.cs#L60-L185)
- **Problem:** Doppelter Lade-Pfad: erst sauberer `SettingsLoader.LoadWithExplicitUserPath`, dann erneuter `JsonDocument.Parse`-Walk mit hardcoded Defaults (`"Info"`, `false`, `Models.ConflictPolicy.Rename`). User-`general:{}` wirft alle defaults.json-Werte um.
- **Fix:** Zweiten JSON-Walk entfernen; ausschliesslich aus `mergedCoreSettings` mappen.
- **Tests fehlen:**
  - [ ] Defaults mit non-default Werten + `general:{}` -> Defaults bleiben
  - [ ] Property-Test pro `SettingsDto`-Property: Save->Load Roundtrip

#### R3-B-02 — `App.OnStartup` faengt `AbandonedMutexException` nicht (P2)
**Tags:** Startup · Single-Instance · Recovery
- [ ] **Fix umsetzen**
- **File:** [App.xaml.cs](../../src/Romulus.UI.Wpf/App.xaml.cs#L29-L43)
- **Problem:** Nach Hard-Kill der Vorgaenger-Instanz wirft Mutex-Konstruktor `AbandonedMutexException` *vor* dem `try`. App stuerzt mit Generic-Crash-Dialog.
- **Fix:** `try { new Mutex(true,name,out cn); } catch (AbandonedMutexException) { /* retry */ }`.

#### R3-B-03 — `LocalizationService.LoadStrings` synchrone Disk-IO bei jedem `SetLocale` (P2)
**Tags:** UI Thread · Performance
- [ ] **Fix umsetzen**
- **File:** [LocalizationService.cs](../../src/Romulus.UI.Wpf/Services/LocalizationService.cs#L57-L78)
- **Fix:** In-Memory-Cache; async Load.

#### R3-B-04 — `LocalizationService` Default-Locale-Drift (DE Code, EN Base) (P2)
**Tags:** Doc Drift · i18n
- [ ] **Fix umsetzen**
- **File:** [LocalizationService.cs](../../src/Romulus.UI.Wpf/Services/LocalizationService.cs#L1-L22)
- **Fix:** Doc + Code synchronisieren; Base-Locale klar definieren.

#### R3-B-05 — Overlay vernichtet leere/Whitespace-Translationen statt Fallback (P2)
**Tags:** i18n · Fallback Robustness
- [ ] **Fix umsetzen**
- **File:** [LocalizationService.cs](../../src/Romulus.UI.Wpf/Services/LocalizationService.cs#L73-L77)
- **Fix:** `if (!string.IsNullOrWhiteSpace(kv.Value)) baseDict[kv.Key] = kv.Value;`.

#### R3-B-06 — `LibraryReportView` async-void Lambda als Click-Handler (P2)
**Tags:** WPF · async void · Crash Risk
- [ ] **Fix umsetzen**
- **File:** [LibraryReportView.xaml.cs](../../src/Romulus.UI.Wpf/Views/LibraryReportView.xaml.cs#L18-L20)
- **Fix:** Wrapper-Methode mit try/catch; ODER Command-Binding.

#### R3-B-07 — `IValueConverter.ConvertBack` werfen `NotSupportedException` (P2)
**Tags:** WPF · Converters · Binding Errors
- [ ] **Fix umsetzen**
- **File:** [Converters.cs](../../src/Romulus.UI.Wpf/Converters/Converters.cs#L62-L189)
- **Fix:** `ConvertBack => Binding.DoNothing` ODER alle Bindings auf `Mode=OneWay` zwingen.

#### R3-B-08 — Hardcoded Brushes in 12+ Views umgehen Theme-/HighContrast-Switch (P2)
**Tags:** Theming · Accessibility · ResourceDictionary Bypass
- [ ] **Fix umsetzen**
- **Files:** DatAuditView, DatCatalogView, ToolsView, ResultView, WizardView, ExternalToolsView, CommandBar
- **Fix:** Alle Hex-Literale auf `{DynamicResource Brush*}` umstellen.

#### R3-B-09 — `SettingsService.version` ohne Migrations-Pfad (P2)
**Tags:** Settings · Schema Versioning · Data Loss
- [ ] **Fix umsetzen**
- **File:** [SettingsService.cs](../../src/Romulus.UI.Wpf/Services/SettingsService.cs#L19-L88)
- **Fix:** Migrations-Tabelle 0->1->2 sequentiell. Bei `fileVersion > Current`: Backup `.v{N}.bak`, Save deaktivieren.

#### R3-B-10 — `SettingsService.Load` aktualisiert `LastAuditPath`/`LastTheme` im Outer-Catch nicht (P2)
**Tags:** Settings · Restart Recovery
- [ ] **Fix umsetzen**
- **File:** [SettingsService.cs](../../src/Romulus.UI.Wpf/Services/SettingsService.cs#L185-L192)
- **Fix:** Properties direkt aus `dto` lesen ODER im Catch-Pfad zusaetzlich setzen.

#### R3-B-11 — `data/format-scores.json` mehrdeutige/kollidierende Extensions (P2)
**Tags:** Data Schema · Classification Drift · Junk Detection
- [ ] **Fix umsetzen**
- **File:** [data/format-scores.json](../../data/format-scores.json#L41-L100)
- **Problem:** `.app`, `.dmg`, `.img`, `.zip`, `.7z`, `.rar`, `.bs` als ROM gescort -> macOS-Bundles/Disk-Images werden als ROM erkannt.
- **Fix:** Schema um `family`/`requiresHashVerification` erweitern. Container-Extensions raus aus `formatScores`.

#### R3-B-12 — `LocalizationService.SetLocale` raised PropertyChanged nicht fuer `AvailableLocales` (P3)
**Tags:** WPF · INotifyPropertyChanged Coverage
- [ ] **Fix umsetzen**
- **File:** [LocalizationService.cs](../../src/Romulus.UI.Wpf/Services/LocalizationService.cs#L36-L46)

#### R3-B-13 — `data/builtin-profiles.json` Schema-Versionierung auf falscher Ebene (P3)
**Tags:** Data Schema · Versioning
- [ ] **Fix umsetzen**
- **File:** `data/builtin-profiles.json`
- **Fix:** Top-Level `{ "schemaVersion": 1, "profiles": [...] }`; Per-Item-`version` umbenennen oder entfernen.

---

### Block C – API / CLI / Reports / Logging (`R3-C-*`)

#### R3-C-01 — `RateLimiter` Bucket-Dictionary unbeschraenkt (DoS via X-Client-Id Spam) (P1)
**Tags:** Security · DoS · API
- [ ] **Fix umsetzen**
- **File:** [RateLimiter.cs](../../src/Romulus.Api/RateLimiter.cs#L30-L80)
- **Problem:** Eviction nur alle 5min. Rotierende `X-Client-Id` blaeht Dictionary auf Millionen Eintraege.
- **Fix:** `MaxBuckets` Cap (z. B. 10_000) + LRU-Eviction synchron beim Add.

#### R3-C-02 — `JsonlLogRotation` 1-Sekunden-Stamp-Kollision verliert Logzeilen (P1)
**Tags:** Logging · Data-Loss · Race
- [ ] **Fix umsetzen**
- **File:** [JsonlLogWriter.cs](../../src/Romulus.Infrastructure/Logging/JsonlLogWriter.cs#L195-L215)
- **Fix:** `yyyyMMdd-HHmmssfff` + Counter-Suffix; Logging-Fehler ueber `IDiagnosticSink`.

#### R3-C-03 — `ReportGenerator.WriteHtmlToFile` / `WriteJsonToFile` nicht atomar (P1)
**Tags:** Reporting · Data-Integrity · Atomicity
- [ ] **Fix umsetzen**
- **File:** [ReportGenerator.cs](../../src/Romulus.Infrastructure/Reporting/ReportGenerator.cs#L188-L214), [RunReportWriter.cs](../../src/Romulus.Infrastructure/Reporting/RunReportWriter.cs#L155-L165)
- **Fix:** Tmp-Write + atomic-Rename Muster wie bei P0-05.

#### R3-C-04 — CLI `--log` Pfad bypasst `AllowedRoots`-Politik (P1)
**Tags:** CLI · Security · Path-Traversal · Parity
- [ ] **Fix umsetzen**
- **Files:** [Program.cs](../../src/Romulus.CLI/Program.cs#L240-L260), [JsonlLogWriter.cs](../../src/Romulus.Infrastructure/Logging/JsonlLogWriter.cs#L1-L60)
- **Problem:** `--log` UNC/Systempfad ungeprueft.
- **Fix:** `SafetyValidator.EnsureSafeOutputPath` im CLI-Parser; UNC/Reparse/Sys-Verzeichnis ablehnen.

#### R3-C-05 — SSE-Writer ohne Per-Write-Timeout -> Slow-Consumer DoS (P1)
**Tags:** API · DoS · SSE · Backpressure
- [ ] **Fix umsetzen**
- **Files:** [ProgramHelpers.cs](../../src/Romulus.Api/ProgramHelpers.cs#L88-L100), [Program.RunWatchEndpoints.cs](../../src/Romulus.Api/Program.RunWatchEndpoints.cs#L876-L955)
- **Fix:** `WriteSseEvent` mit linked CTS (5s Timeout + RequestAborted).

#### R3-C-06 — `/runs` POST: Case-Insensitive + Duplicate-Property Last-Wins-Bypass (P1)
**Tags:** API · Validation · Injection · JSON
- [ ] **Fix umsetzen**
- **Files:** [Program.RunWatchEndpoints.cs](../../src/Romulus.Api/Program.RunWatchEndpoints.cs#L171-L195)
- **Problem:** `roots` + `Roots` doppelt -> last-property-wins bypassed Proxy/WAF-Filter.
- **Fix:** Source-gen `JsonSerializerContext`; custom converter rejected Duplicate-Property mit 400.

#### R3-C-07 — `AuditCsvStore.CountAuditRows` zaehlt physische Zeilen (False-Positive TAMPERED) (P2)
**Tags:** Audit · Data-Integrity · CSV-Parsing
- [ ] **Fix umsetzen**
- **File:** [AuditCsvStore.cs](../../src/Romulus.Infrastructure/Audit/AuditCsvStore.cs#L170-L195)
- **Problem:** `File.ReadLines.Count() - 1` zaehlt physische Zeilen. Quoted multi-line Field -> Sidecar-Count != Reader-Count -> falscher TAMPERED-Alarm.
- **Fix:** Ueber `AuditCsvParser.ParseCsvLine` mit Quote-State zaehlen.

#### R3-C-08 — `ReportGenerator.LoadReportLocale` Single-Slot-Cache + kein Size-Limit (P2)
**Tags:** Reporting · Performance · DoS
- [ ] **Fix umsetzen**
- **File:** [ReportGenerator.cs](../../src/Romulus.Infrastructure/Reporting/ReportGenerator.cs#L413-L462)
- **Fix:** LRU-Cache (Cap 8); `FileInfo.Length > 256KB` ablehnen; `JsonDocumentOptions { MaxDepth = 8 }`.

#### R3-C-09 — CLI `Enum.Parse<LogLevel>` wirft auf invalid -> Exit 1 statt 3 (P2)
**Tags:** CLI · UX · Error-Codes
- [ ] **Fix umsetzen**
- **File:** [Program.cs](../../src/Romulus.CLI/Program.cs#L240-L255)
- **Problem:** Profile-Datei mit `"logLevel": "Trace"` -> Exit 1, nicht 3.
- **Fix:** Zentrale Validierung nach Profile-Merge mit `TryParse` + Exit 3.

#### R3-C-10 — CLI `Console.ReadLine()` auf geschlossenem Stdin -> "Aborted by user" Fehl-Diagnose (P2)
**Tags:** CLI · UX · Headless · CI/CD
- [ ] **Fix umsetzen**
- **File:** [Program.cs](../../src/Romulus.CLI/Program.cs#L184-L195)
- **Fix:** `Console.IsInputRedirected || ReadLine() is null` -> Exit 3 mit Hinweis auf `--yes`.

#### R3-C-11 — `CliOutputWriter` nutzt Reflection-`JsonSerializer` (AOT-Trim-Drift) (P2)
**Tags:** CLI · AOT · Maintainability
- [ ] **Fix umsetzen**
- **File:** [CliOutputWriter.cs](../../src/Romulus.CLI/CliOutputWriter.cs#L11-L30)
- **Fix:** `internal partial class CliJsonSerializerContext : JsonSerializerContext` mit allen Output-Typen.

#### R3-C-12 — Report-JSON ohne `schemaVersion` -> Konsumenten erkennen Breaking Changes nicht (P2)
**Tags:** Reporting · Contracts · API-Stability
- [ ] **Fix umsetzen**
- **Files:** [ReportGenerator.cs](../../src/Romulus.Infrastructure/Reporting/ReportGenerator.cs#L172-L185), [RunReportWriter.cs](../../src/Romulus.Infrastructure/Reporting/RunReportWriter.cs#L155-L165)
- **Fix:** `SchemaVersion`-Property in `JsonReport`/`ReportSummary`; ADR fuer SemVer-Politik.

#### R3-C-13 — `SanitizeCorrelationId` -> `null` -> stille Ersetzung bricht Trace (P2)
**Tags:** API · Observability · Tracing
- [ ] **Fix umsetzen**
- **File:** [ProgramHelpers.cs](../../src/Romulus.Api/ProgramHelpers.cs#L101-L113)
- **Fix:** Bei Sanitize-Fail Warning loggen + Response-Header `X-Correlation-Id-Replaced: 1`.

#### R3-C-14 — `RunRequest.Roots` ohne MaxItems -> POST-DoS (P2)
**Tags:** API · DoS · Validation
- [ ] **Fix umsetzen**
- **Files:** [RunManager.cs](../../src/Romulus.Api/RunManager.cs#L273-L295), [Program.RunWatchEndpoints.cs](../../src/Romulus.Api/Program.RunWatchEndpoints.cs#L219-L260)
- **Fix:** `MaxRootsPerRequest=64`; analog `Extensions`/`PreferRegions` Cap 32.

#### R3-C-15 — Dedup `SelectWinner`: doppelter `MainPath`-ThenBy maskiert Upstream-Bug (P3)
**Tags:** Core · Determinism · Dead-Code
- [ ] **Fix umsetzen**
- **File:** [DeduplicationEngine.cs](../../src/Romulus.Core/Deduplication/DeduplicationEngine.cs#L97-L108)
- **Fix:** Zweiten `ThenBy` entfernen + Upstream-Validierung (Throw bei case-only-Duplikat). `CategoryRank` mit `Dictionary<FileCategory,int>`.

---

## Round 4 – Neue Funde

> Quellen: Audit 4a (Avalonia UI – `R4-A-*`), 4b (Tests/Benchmark – `R4-B-*`), 4c (Deploy/Safety/Audit/Logging – `R4-C-*`).
> Insgesamt 61 zusaetzliche Funde. Gesamtbestand: 173.

---

### Block A – Avalonia UI (`R4-A-*`)

#### R4-A-01 — Fake KPI-Berechnung in `ApplyFromPreview` — keine echte Pipeline-Anbindung (P0)
**Tags:** Release-Blocker · Preview/Execute-Parität · Fake-KPIs · Avalonia
- [ ] **Fix umsetzen**
- **File:** [ResultViewModel.cs](../../src/Romulus.UI.Avalonia/ViewModels/ResultViewModel.cs#L90)
- **Problem:** `ApplyFromPreview` berechnet alle KPIs (`games`, `dupes`, `junk`, `health`) mit hardcoded Fake-Arithmetik (`rootCount * 120`, `rootCount * 14` etc.). Die GUI zeigt erfundene Zahlen, die keinerlei Bezug zur tatsaechlichen Analyse-Pipeline haben. Preview/Execute/Report-Paritaet ist fundamental gebrochen.
- **Fix:** `ApplyFromPreview` muss ein echtes Result-Modell aus Core entgegennehmen (`RunResult`, `SortProjection`). Alle KPIs aus dem tatsaechlichen Ergebnis der Pipeline. Kein `HasRunData=true` ohne echten Run.
- **Tests fehlen:**
  - [ ] Preview/Execute/Report-Paritaet: gleiche Roots → GUI, CLI, API zeigen identische KPIs
  - [ ] `HasRunData` erst nach echtem Run true

#### R4-A-02 — Path-Traversal-Risiko in `ImportRootsAsync`: importierte Pfade ohne Validierung (P0)
**Tags:** Release-Blocker · Security · Path Traversal · OWASP A01 · Avalonia
- [ ] **Fix umsetzen**
- **File:** [StartViewModel.cs](../../src/Romulus.UI.Avalonia/ViewModels/StartViewModel.cs#L87)
- **Problem:** Aus Textdatei importierte Pfade werden nach `Trim()` ohne Validierung direkt in `Roots` uebernommen. Kein Check auf `../../`, keine Pruefung ob Verzeichnis existiert, keine Laengenpruefung, keine Pruefung auf System-Pfade (`C:\Windows\System32`). Verletzt explizit die Projektregel "Path Traversal blockieren".
- **Fix:** `Path.GetFullPath()` + Canonical-Path-Vergleich; Pfad muss innerhalb eines erlaubten `AllowedRootsBase` liegen; `Directory.Exists()` pruefen; relative Pfade und System-Pfade ablehnen.
- **Tests fehlen:**
  - [ ] Input `../../Windows/System32` → abgelehnt
  - [ ] Relativer Pfad → abgelehnt
  - [ ] TOCTOU: Datei zwischen Exists-Check und ReadAllLines geloescht → kein Crash

#### R4-A-03 — `SafeDialogBackend` vollstaendig non-funktional in Production-DI (P1)
**Tags:** Architecture · Dialog · No-Op · UX-Risk
- [ ] **Fix umsetzen**
- **Files:** [App.axaml.cs](../../src/Romulus.UI.Avalonia/App.axaml.cs#L49), [SafeDialogBackend.cs](../../src/Romulus.UI.Avalonia/Services/SafeDialogBackend.cs#L1)
- **Problem:** `IAvaloniaDialogBackend` ist in Production-DI mit `SafeDialogBackend` registriert. `SafeDialogBackend` ist ein reines No-Op: `BrowseFolder→null`, `Confirm→false`, `DangerConfirm→false`, `ShowInputBox→defaultValue`. Error-Dialoge werden still verschluckt.
- **Fix:** Echte `IAvaloniaDialogBackend`-Implementierung mit Avalonia `MessageBox`-API erstellen. `SafeDialogBackend` nur fuer Tests/Design verwenden.
- **Tests fehlen:**
  - [ ] `IDialogService.Error()` im Avalonia-Kontext zeigt sichtbaren Dialog
  - [ ] `DangerConfirm=false` bricht destructive Operationen tatsaechlich ab

#### R4-A-04 — Synchrones `IDialogService`-Contract strukturell inkompatibel mit Avalonia (P1)
**Tags:** Architecture · Deadlock-Risk · async · Contracts
- [ ] **Fix umsetzen**
- **File:** [AvaloniaDialogService.cs](../../src/Romulus.UI.Avalonia/Services/AvaloniaDialogService.cs#L30)
- **Problem:** `IDialogService` aus `Romulus.Contracts` definiert synchrone Methoden. Avalonia's gesamte Dialog- und File-Picker-API ist ausschliesslich `async Task<T>`. Eine echte Implementierung muesste Avalonia-async blockierend wrappen → garantierter Deadlock auf UI-Thread.
- **Fix:** `IDialogService` um async-Varianten erweitern (`Task<bool> ConfirmAsync()`) oder separaten `IAsyncDialogService` in Contracts einfuehren. Niemals `.GetAwaiter().GetResult()` auf dem UI-Thread.
- **Tests fehlen:**
  - [ ] Integrations-Test: synchrone Variante vom UI-Thread → kein Deadlock

#### R4-A-05 — Navigation State Machine: `NavigateStartCommand` ohne State-Reset waehrend IsRunning (P1)
**Tags:** Navigation · Race-Condition · State Machine
- [ ] **Fix umsetzen**
- **File:** [MainWindowViewModel.cs](../../src/Romulus.UI.Avalonia/ViewModels/MainWindowViewModel.cs#L39)
- **Problem:** `NavigateStartCommand` setzt nur `CurrentScreen = WorkspaceScreen.Start` ohne `Progress.IsRunning` zu pruefen. Laufender Run bleibt aktiv; `CompleteRunCommand.CanExecute` bleibt true; nachfolgender Preview-Start wirft den laufenden Run wortlos weg. `ReturnToStartCommand` macht es korrekt (mit `Reset()`), `NavigateStartCommand` nicht.
- **Fix:** `NavigateStartCommand` bei `IsRunning==true` deaktivieren (CanExecute) oder `Progress.Reset()` + `Result.Reset()` erzwingen, analog `ReturnToStartCommand`.
- **Tests fehlen:**
  - [ ] Navigate-Start waehrend IsRunning → Progress.IsRunning danach false
  - [ ] `ReturnToStartCommand` vs `NavigateStartCommand` identischer End-State

#### R4-A-06 — `StartPreviewCommand` ohne `!Progress.IsRunning`-Guard: Double-Start moeglich (P1)
**Tags:** Navigation · Race-Condition · Command Guard
- [ ] **Fix umsetzen**
- **File:** [MainWindowViewModel.cs](../../src/Romulus.UI.Avalonia/ViewModels/MainWindowViewModel.cs#L34)
- **Problem:** `StartPreviewCommand` prueft nur `HasRoots`. Zweiter Klick waehrend laufendem Preview wirft den Run wortlos weg. Kein Koordination zwischen `StartPreviewCommand` (MainWindow) und `RequestPreviewCommand` (StartView).
- **Fix:** `StartPreviewCommand = new RelayCommand(StartPreview, () => Start.HasRoots && !Progress.IsRunning)`. `NotifyCanExecuteChanged` bei `Progress.IsRunning`-Wechsel.
- **Tests fehlen:**
  - [ ] Zweiter StartPreview waehrend IsRunning → Command nicht ausfuehrbar

#### R4-A-07 — Hardcoded Entwicklungs-Roots in `StartViewModel` Production-Code (P1)
**Tags:** Release-Blocker · Hardcoded Dev-Data · Bug
- [ ] **Fix umsetzen**
- **File:** [StartViewModel.cs](../../src/Romulus.UI.Avalonia/ViewModels/StartViewModel.cs#L29)
- **Problem:** `Roots` wird mit `@"C:\\ROMS\\Arcade"` und `@"C:\\ROMS\\Nintendo"` initialisiert. Existieren auf den meisten Systemen nicht. `HasRoots` ist initial `true` wegen dieser Stubs. Darf niemals ausgeliefert werden.
- **Fix:** `Roots = []`. Entwicklungs-/Design-Daten in separatem Design-Time-ViewModel oder Feature-Flag.
- **Tests fehlen:**
  - [ ] Frisch initialisierter `StartViewModel` hat `Roots.Count == 0` und `HasRoots == false`

#### R4-A-08 — `ImportRootsAsync`: kein try/catch um `File.ReadAllLinesAsync` (TOCTOU + IO) (P2)
**Tags:** Robustness · Error Handling
- [ ] **Fix umsetzen**
- **File:** [StartViewModel.cs](../../src/Romulus.UI.Avalonia/ViewModels/StartViewModel.cs#L88)
- **Fix:** `try/catch(Exception ex)` um `File.ReadAllLinesAsync`; bei Fehler Status-Property setzen oder `IDialogService.Error()` aufrufen.

#### R4-A-09 — `AddRootAsync` / `ImportRootsAsync`: kein try/catch um Picker-Aufruf (P2)
**Tags:** Robustness · Error Handling
- [ ] **Fix umsetzen**
- **Files:** [StartViewModel.cs](../../src/Romulus.UI.Avalonia/ViewModels/StartViewModel.cs#L68)
- **Fix:** try/catch um `BrowseFolderAsync()` und `BrowseFileAsync()` mit sichtbarem Fehler-Feedback.

#### R4-A-10 — `ProgressViewModel.LiveLog.Add` nicht thread-safe fuer kuenftige async-Operationen (P2)
**Tags:** Concurrency · Thread Safety · ObservableCollection
- [ ] **Fix umsetzen**
- **File:** [ProgressViewModel.cs](../../src/Romulus.UI.Avalonia/ViewModels/ProgressViewModel.cs#L83)
- **Problem:** `LiveLog` ist eine `ObservableCollection<string>` ohne Dispatcher-Schutz. Sobald echter async-Code (Run-Orchestrator) `AppendLog` vom Background-Thread aufruft → `InvalidOperationException`-Crash.
- **Fix:** `Dispatcher.UIThread.InvokeAsync(() => LiveLog.Add(line))` in `AppendLog`.
- **Tests fehlen:**
  - [ ] `AppendLog` von Background-Thread → kein Crash, Eintrag erscheint

#### R4-A-11 — Hardcoded Hex-Farben in allen AXAML-Dateien: Theme-Toggle wirkungslos (P2)
**Tags:** Theming · Accessibility · ResourceDictionary Bypass
- [ ] **Fix umsetzen**
- **Files:** [MainWindow.axaml](../../src/Romulus.UI.Avalonia/MainWindow.axaml#L43), [StartView.axaml](../../src/Romulus.UI.Avalonia/Views/StartView.axaml#L11), [ProgressView.axaml](../../src/Romulus.UI.Avalonia/Views/ProgressView.axaml#L9), [ResultView.axaml](../../src/Romulus.UI.Avalonia/Views/ResultView.axaml#L9)
- **Problem:** `BorderBrush="#D0D7DE"` und `Background="#FCFCFD"` in allen Views hardcoded. Dark-Mode-Toggle hat keine Wirkung auf diese Farben.
- **Fix:** `DynamicResource`-Referenzen statt Hex-Literale. `ResourceDictionary` mit Semantic-Color-Keys fuer Hell/Dunkel.

#### R4-A-12 — Picker-Services: Lifetime-Auflosung via `Application.Current` statt DI (P2)
**Tags:** Testability · DI · Application.Current
- [ ] **Fix umsetzen**
- **Files:** [AvaloniaStorageFilePickerService.cs](../../src/Romulus.UI.Avalonia/Services/AvaloniaStorageFilePickerService.cs#L17), [AvaloniaStorageFolderPickerService.cs](../../src/Romulus.UI.Avalonia/Services/AvaloniaStorageFolderPickerService.cs#L15)
- **Fix:** `IClassicDesktopStyleApplicationLifetime` in DI registrieren und per Constructor-Injection uebergeben.

#### R4-A-13 — `MainWindow`: parameterloser Konstruktor erstellt `new MainWindowViewModel()` ohne DI (P2)
**Tags:** DI · Design-Time · Code Clarity
- [ ] **Fix umsetzen**
- **File:** [MainWindow.axaml.cs](../../src/Romulus.UI.Avalonia/MainWindow.axaml.cs#L8)
- **Fix:** Parameterlose Konstruktor mit `[EditorBrowsable(Never)]` und klarem Kommentar "Nur fuer XAML-Designer" kennzeichnen.

#### R4-A-14 — Fehlende `AutomationProperties` auf allen interaktiven Elementen (P3)
**Tags:** Accessibility · UI Automation
- [ ] **Fix umsetzen**
- **Files:** [MainWindow.axaml](../../src/Romulus.UI.Avalonia/MainWindow.axaml), [StartView.axaml](../../src/Romulus.UI.Avalonia/Views/StartView.axaml), [ProgressView.axaml](../../src/Romulus.UI.Avalonia/Views/ProgressView.axaml), [ResultView.axaml](../../src/Romulus.UI.Avalonia/Views/ResultView.axaml)
- **Fix:** `AutomationProperties.Name` auf allen Buttons, ListBox, ProgressBar.

#### R4-A-15 — Event-Subscriptions ohne Unsubscribe in `MainWindowViewModel` (P3)
**Tags:** Memory · Lifetime · Events
- [ ] **Fix umsetzen**
- **File:** [MainWindowViewModel.cs](../../src/Romulus.UI.Avalonia/ViewModels/MainWindowViewModel.cs#L27)
- **Fix:** `IDisposable` implementieren, Unsubscribe in `Dispose()`. Oder `WeakEventManager`.

#### R4-A-16 — `Tmds.DBus.Protocol`-Paket ohne Begruendung im Windows-Deployment (P3)
**Tags:** Hygiene · Supply-Chain · Unused Dependency
- [ ] **Fix umsetzen**
- **File:** [Romulus.UI.Avalonia.csproj](../../src/Romulus.UI.Avalonia/Romulus.UI.Avalonia.csproj#L22)
- **Fix:** Paket entfernen oder Condition `'$(OS)' == 'Unix'` hinzufuegen und dokumentieren.

#### R4-A-17 — `NavigateProgressCommand` ohne Guard: freie Navigation zur leeren Progress-View (P3)
**Tags:** Navigation · UX · Guard
- [ ] **Fix umsetzen**
- **File:** [MainWindowViewModel.cs](../../src/Romulus.UI.Avalonia/ViewModels/MainWindowViewModel.cs#L41)
- **Fix:** CanExecute: `() => Progress.IsRunning || Progress.CurrentPhase != "Idle"`.

---

### Block B – Tests & Benchmark (`R4-B-*`)

#### R4-B-01 — Benchmark-DAT-Pipeline komplett blind: `DatIndex: null` (P0)
**Tags:** Release-Blocker · Benchmark · DAT-Pipeline-Blindspot
- [ ] **Fix umsetzen**
- **File:** [BenchmarkEvaluationRunner.cs](../../src/Romulus.Tests/Benchmark/BenchmarkEvaluationRunner.cs#L117)
- **Problem:** `TryEvaluateEnrichmentCandidate` uebergibt `DatIndex: null` an `EnrichmentPhase.Execute()`. Die gesamte DAT-Hash-Lookup-Pipeline laeuft in keinem einzigen Benchmark-Sample. DAT-Regressionsfehler bleiben unsichtbar.
- **Fix:** `BenchmarkFixture` muss einen `DatIndex` aus `benchmark/dats/` laden und in `TryEvaluateEnrichmentCandidate` uebergeben.
- **Tests fehlen:**
  - [ ] Benchmark-Sample mit bekanntem DAT-Treffer → `DatVerified`-Verdict

#### R4-B-02 — Silent-Catch in Benchmark-Evaluation maskiert Pipeline-Fehler (P0)
**Tags:** Release-Blocker · Benchmark · Exception-Masking
- [ ] **Fix umsetzen**
- **File:** [BenchmarkEvaluationRunner.cs](../../src/Romulus.Tests/Benchmark/BenchmarkEvaluationRunner.cs#L120-L125)
- **Problem:** Nacktes `catch { return null; }` schluckt alle Enrichment-Pipeline-Exceptions. Sample gilt als "korrekt nach Fallback", waehrend der echte Fehler verschwindet.
- **Fix:** Exception fangen, als `BenchmarkVerdict.Error` zaehlen, in `_output.WriteLine` loggen.

#### R4-B-03 — Alibi-Test: `EdgeCaseBenchmarkTests` prueft keine Korrektheit (P0)
**Tags:** Alibi-Test · Benchmark · Edge Cases
- [ ] **Fix umsetzen**
- **File:** [EdgeCaseBenchmarkTests.cs](../../src/Romulus.Tests/Benchmark/EdgeCaseBenchmarkTests.cs#L22-L30)
- **Problem:** Assertiert nur `confidence >= 0 && confidence <= 100` und dass die Sample-Datei existiert. Alle Edge Cases koennen falsch erkannt werden, Test bleibt gruen.
- **Fix:** Verdict-Assertion analog zu `GoldenCoreBenchmarkTests` ergaenzen.

#### R4-B-04 — 4 Benchmark-Sets ohne Verdict-Assertion (Chaos/Realworld/Repair/DatCoverage) (P0)
**Tags:** Alibi-Tests · Benchmark Coverage · Silent Pass
- [ ] **Fix umsetzen**
- **Files:** [ChaosMixedBenchmarkTests.cs](../../src/Romulus.Tests/Benchmark/ChaosMixedBenchmarkTests.cs#L22-L28), [GoldenRealworldBenchmarkTests.cs](../../src/Romulus.Tests/Benchmark/GoldenRealworldBenchmarkTests.cs#L18-L24), [RepairSafetyBenchmarkTests.cs](../../src/Romulus.Tests/Benchmark/RepairSafetyBenchmarkTests.cs#L21-L27), [DatCoverageBenchmarkTests.cs](../../src/Romulus.Tests/Benchmark/DatCoverageBenchmarkTests.cs#L40-L46)
- **Problem:** Alle vier prueft nur `confidence in [0,100]` und "Sample existiert". Keine Korrektheitspruefung. Jeder Produktionsfehler in diesen Bereichen ist unsichtbar.
- **Fix:** Jeweils Verdict-Assertion; fuer `RepairSafety` zusaetzlich `expected.RepairSafe` gegen `actual.SortDecision` pruefen.

#### R4-B-05 — `SafetyIoSecurityRedPhaseTests` ohne Trait-Isolation: unbekannter CI-Zustand (P0)
**Tags:** Test-Hygiene · Security Tests · CI-State
- [ ] **Fix umsetzen**
- **File:** [SafetyIoSecurityRedPhaseTests.cs](../../src/Romulus.Tests/SafetyIoSecurityRedPhaseTests.cs#L1-L20)
- **Problem:** Klasse kommentiert als "TDD RED PHASE ONLY — absichtlich ROT", aber kein `[Trait("Category", "RedPhase")]` und `xunit.runner.json` schließt keinen Trait aus. Sicherheitskritische Pfad-Guard-Tests in unklarem CI-Zustand (gruen/rot unklar).
- **Fix:** Wenn gruen: RED-Kommentar entfernen. Wenn rot: `[Trait]` + CI-Filter isolieren oder Implementierung nachziehen.

#### R4-B-06 — Alle Quality-Gates standardmaessig informational: kein CI-Schutz (P1)
**Tags:** Benchmark · Gates · CI-Wirkungslosigkeit
- [ ] **Fix umsetzen**
- **Files:** [QualityGateTests.cs](../../src/Romulus.Tests/Benchmark/QualityGateTests.cs#L23-L57), [HoldoutGateTests.cs](../../src/Romulus.Tests/Benchmark/HoldoutGateTests.cs#L40-L79), [AntiGamingGateTests.cs](../../src/Romulus.Tests/Benchmark/AntiGamingGateTests.cs#L43), [SystemTierGateTests.cs](../../src/Romulus.Tests/Benchmark/SystemTierGateTests.cs#L50)
- **Problem:** Alle Gates gehen ohne `ROMULUS_ENFORCE_QUALITY_GATES=true` mit `return` durch ohne Assertion. M4-wrongMatchRate kann auf 50% steigen, alle Tests bleiben gruen.
- **Fix:** Default auf `enforce=true`. Opt-out via `ROMULUS_SKIP_QUALITY_GATES=true`.

#### R4-B-07 — `GroundTruthComparator` prueft `SortDecision` nicht (P1)
**Tags:** Benchmark · GroundTruth · SortDecision Blindspot
- [ ] **Fix umsetzen**
- **File:** [GroundTruthComparator.cs](../../src/Romulus.Tests/Benchmark/GroundTruthComparator.cs#L23-L155)
- **Problem:** `ExpectedResult.SortDecision` wird nie mit `actual.SortDecision` verglichen. Bug im `DecisionResolver` (Sort statt Blocked) ist im Benchmark unsichtbar.
- **Fix:** `SortDecision`-Vergleich in `Compare()` + `sortDecisionMismatchRate` in `MetricsAggregator`.

#### R4-B-08 — `GroundTruthComparator` prueft `DatMatchLevel`/`DatEcosystem` nicht (P1)
**Tags:** Benchmark · GroundTruth · DAT-Match Blindspot
- [ ] **Fix umsetzen**
- **File:** [GroundTruthComparator.cs](../../src/Romulus.Tests/Benchmark/GroundTruthComparator.cs#L23)
- **Problem:** `ExpectedResult.DatMatchLevel` und `DatEcosystem` werden ignoriert. In Kombination mit R4-B-01 (DatIndex=null) sind DAT-Match-Regressionsfehler doppelt unsichtbar.
- **Fix:** `DatMatchLevel`/`DatEcosystem` in Verdict-Vergleich einbeziehen + eigene Metrik.

#### R4-B-09 — Benchmark statische Service-Instanzen: Test-Isolation verletzt (P1)
**Tags:** Test-Isolation · Static State · Benchmark
- [ ] **Fix umsetzen**
- **File:** [BenchmarkEvaluationRunner.cs](../../src/Romulus.Tests/Benchmark/BenchmarkEvaluationRunner.cs#L15-L17)
- **Problem:** `EnrichmentPhase`, `HashService`, `ArchiveHashService` als `private static readonly`. Bei internem State-Caching koennen aufeinanderfolgende Tests Zustandsreste erben.
- **Fix:** Services als stateless bestaetigen oder instanzbasiert per `BenchmarkFixture` erzeugen.

#### R4-B-10 — `PreviewExecuteParityTests` ohne Report/API-Paritaet (P1)
**Tags:** Test-Coverage · Parity · CLI/API/Report
- [ ] **Fix umsetzen**
- **File:** [PreviewExecuteParityTests.cs](../../src/Romulus.Tests/PreviewExecuteParityTests.cs#L36-L75)
- **Problem:** Prueft nur 4 Felder in GUI→DryRun vs GUI→Execute. CLI-Output, API-Endpunkt, Report-KPIs nicht geprueft. "Preview N Duplikate, Report M" ist nicht testbar.
- **Fix:** Test `RunResult.LoserCount == CLI-exit-summary == API /run/status == Report`.

#### R4-B-11 — Holdout-Gate: 20%-Mindestschwelle bietet keinen effektiven Schutz (P1)
**Tags:** Benchmark · Gates · Threshold
- [ ] **Fix umsetzen**
- **File:** [HoldoutGateTests.cs](../../src/Romulus.Tests/Benchmark/HoldoutGateTests.cs#L76)
- **Problem:** `Holdout_DetectionRate_AboveMinimum` prueft `rate >= 20.0`. Ein System das 80% falsch erkennt besteht diesen Gate.
- **Fix:** Schwelle auf ≥75% anheben. Gate standardmaessig enforced.

#### R4-B-12 — `gates.json` `minDatVerifiedRate=0.00` fuer alle Familien (P1)
**Tags:** Benchmark · Gates · DAT Coverage
- [ ] **Fix umsetzen**
- **File:** [benchmark/gates.json](../../benchmark/gates.json#L78-L89)
- **Problem:** 0% DAT-Verified gilt fuer alle Familien als erfuellt. Mit R4-B-01 kumuliert sich blind-DAT zur keiner Messung.
- **Fix:** Fuer `NoIntroCartridge` ≥30%, `RedumpDisc` ≥20% als realistische Schwellen setzen.

#### R4-B-13 — Fehlende Rollback/Undo-Tests fuer Mid-Run-Cancel (P1)
**Tags:** Test-Coverage · Rollback · Data-Integrity
- [ ] **Fix umsetzen**
- **File:** [MovePhaseAuditInvariantTests.cs](../../src/Romulus.Tests/MovePhaseAuditInvariantTests.cs)
- **Problem:** Kein Test fuer: (a) halbfertiger Move + Cancel → Dateisystem konsistent rollbackbar; (b) Undo nach vollstaendigem Move → exakt originale Pfade; (c) Undo nach fehlgeschlagenem Move → keine Exception.
- **Fix:** Integrations-Test mit Mid-Run-Cancellation-Simulation + anschliessendem Undo.

#### R4-B-14 — `gates.json` `FolderBased.maxUnknownRate=0.95` — kein effektiver Gate (P2)
**Tags:** Benchmark · Gates · Threshold
- [ ] **Fix umsetzen**
- **File:** [benchmark/gates.json](../../benchmark/gates.json#L88)
- **Fix:** Auf ≤75% setzen. Gate-Kommentar mit Begruendung ergaenzen.

#### R4-B-15 — `GroundTruthComparatorTests` — nur 4 Basis-Cases (P2)
**Tags:** Test-Coverage · GroundTruth
- [ ] **Fix umsetzen**
- **File:** [GroundTruthComparatorTests.cs](../../src/Romulus.Tests/Benchmark/GroundTruthComparatorTests.cs#L1-L80)
- **Fix:** Mindestens 8 weitere Cases: AcceptableConsoleKeys, SortDecision-Mismatch, Ambiguous, DatMatchLevel.

#### R4-B-16 — Keine Unicode-Pfad-Tests fuer `FileSystemAdapter.MoveItemSafely` (P2)
**Tags:** Test-Coverage · Unicode · Path Safety
- [ ] **Fix umsetzen**
- **File:** [FileSystemAllowedRootTests.cs](../../src/Romulus.Tests/FileSystemAllowedRootTests.cs)
- **Fix:** Parametrisierte Theory mit Unicode-Ordnernamen (`ゲーム`, `Спорт`, `Üntersuchung`).

#### R4-B-17 — `DecisionResolver` Tier1+datAvailable=false→Sort-Invariante fehlt (P2)
**Tags:** Test-Coverage · DAT-Gate · DecisionResolver
- [ ] **Fix umsetzen**
- **File:** [DecisionResolverTests.cs](../../src/Romulus.Tests/DecisionResolverTests.cs)
- **Fix:** `Tier1_WithDatAvailableButNoMatch_CapsAtReview` und `Tier1_WithoutDat_CanReachSort` als benannte Invarianten-Tests.

#### R4-B-18 — Benchmark-Scope-Luecke: kein End-to-End-Sortierweg (P2)
**Tags:** Benchmark · Scope · Sorting Blindspot
- [ ] **Fix umsetzen**
- **File:** [BenchmarkEvaluationRunner.cs](../../src/Romulus.Tests/Benchmark/BenchmarkEvaluationRunner.cs)
- **Problem:** Dedup, Sorter, MovePipeline, ConversionPhase laufen nie in einem Benchmark-Sample. Falsch sortierte erkannte ROMs sind unsichtbar.
- **Fix:** `SortingPathBenchmarkTests` mit ≥20 Samples durch vollstaendigen `RunOrchestrator` + `actualTargetFolder` vs `expectedTargetFolder`.

#### R4-B-19 — `NegativeControlBenchmarkTests` uebermassige Skips ohne Assertion (P2)
**Tags:** Alibi-Test · Benchmark
- [ ] **Fix umsetzen**
- **File:** [NegativeControlBenchmarkTests.cs](../../src/Romulus.Tests/Benchmark/NegativeControlBenchmarkTests.cs#L28-L55)
- **Fix:** Jeder early-return-Pfad benoetigt eigene Assert-Aussage.

#### R4-B-20 — Fehlende Tests fuer Reparse-Point-Erkennung (SEC-CONV-08) (P2)
**Tags:** Security · Reparse Points · Conversion Safety
- [ ] **Fix umsetzen**
- **File:** [SetMemberRootContainmentSecurityTests.cs](../../src/Romulus.Tests/SetMemberRootContainmentSecurityTests.cs)
- **Fix:** `Directory.CreateSymbolicLink` am Konvertierungsziel, dann `ConversionVerification` → `VerificationStatus.Failed` assertieren.

#### R4-B-21 — `*CoverageBoost*`-Dateien: strukturelles Coverage-Gaming (P3)
**Tags:** Test-Hygiene · Coverage Gaming
- [ ] **Fix umsetzen**
- **Files:** [ApiCoverageBoostTests.cs](../../src/Romulus.Tests/ApiCoverageBoostTests.cs), [EnrichmentPipelinePhaseCoverageBoostTests2.cs](../../src/Romulus.Tests/EnrichmentPipelinePhaseCoverageBoostTests2.cs), mind. 12 weitere `*CoverageBoost*`-Dateien
- **Fix:** Audit aller `*CoverageBoost*`-Tests; reine `Assert.NotNull`-Tests ohne fachliche Aussage loeschen oder mit echten Assertions ersetzen.

#### R4-B-22 — Phasenbasierte Testnamen ohne fachliche Aussage (P3)
**Tags:** Test-Hygiene · Naming
- [ ] **Fix umsetzen**
- **Files:** [Phase1ReleaseBlockerTests.cs](../../src/Romulus.Tests/Phase1ReleaseBlockerTests.cs) bis Phase15
- **Fix:** Umbenennen in sprechende Domaenennamen (als Hygiene-Schuld dokumentieren).

#### R4-B-23 — `BaselineRegressionGateTests` schreibt Artefakte als Test-Seiteneffekt (P3)
**Tags:** Test-Hygiene · Side Effects · Test Isolation
- [ ] **Fix umsetzen**
- **File:** [BaselineRegressionGateTests.cs](../../src/Romulus.Tests/Benchmark/BaselineRegressionGateTests.cs#L40-L65)
- **Fix:** Artefakt-Schreiben und Baseline-Update in separaten CLI-Befehl auslagern, nicht als Testlauf-Seiteneffekt.

#### R4-B-24 — `AuditEFOpenTests` tautologische `Assert.NotNull` ohne Feldpruefung (P3)
**Tags:** Alibi-Tests · Test-Hygiene
- [ ] **Fix umsetzen**
- **File:** [AuditEFOpenTests.cs](../../src/Romulus.Tests/AuditEFOpenTests.cs#L298-L319)
- **Fix:** Mindestens ein spezifisches Feld pro Test pruefen (Status, Count, konkrete Ausgabewerte).

---

### Block C – Deploy / Safety / Audit / Logging (`R4-C-*`)

#### R4-C-01 — HMAC-Schluessel ephemer: Rollback nach Neustart permanent blockiert (P0)
**Tags:** Release-Blocker · Audit · Rollback Blocked · Data-Integrity
- [ ] **Fix umsetzen**
- **File:** [AuditSigningService.cs](../../src/Romulus.Infrastructure/Audit/AuditSigningService.cs#L36)
- **Problem:** HMAC-Signing-Key wird nur im Speicher gehalten wenn kein `keyFilePath` konfiguriert. Nach Neustart: neuer Key, alle bestehenden `.meta.json`-Sidecars koennen nicht mehr verifiziert werden → `Rollback()` dauerhaft blockiert.
- **Fix:** `keyFilePath` in allen produktiven Entry Points zwingend konfigurieren. Im Konstruktor bei `keyFilePath==null` mindestens Warning loggen und idealerweise Exception werfen.
- **Tests fehlen:**
  - [ ] `AuditCsvStore` ohne `keyFilePath`, neu instanziiert → `Rollback()` schlaegt sauber fehl (nicht stillen 0-Eintraege)

#### R4-C-02 — Docker: Profil-Volume unter `/root/.config` nicht erreichbar fuer `app`-User (P0)
**Tags:** Release-Blocker · Docker · Permissions · Data-Loss
- [ ] **Fix umsetzen**
- **Files:** [docker-compose.headless.yml](../../deploy/docker/docker-compose.headless.yml#L26), [Dockerfile](../../deploy/docker/api/Dockerfile#L31)
- **Problem:** `USER app` gesetzt, aber Volume mount auf `/root/.config/Romulus`. App-User hat keinen Schreibzugriff auf `/root/.config/`. Profil-Schreiboperationen schlagen lautlos fehl.
- **Fix:** Volume-Pfad auf Home des `app`-Users aendern (`/home/app/.config/Romulus`) oder `ENV APPDATA=/app/config` setzen.
- **Tests fehlen:**
  - [ ] Container-Smoke: Profil-Write nach API-Start, dann Read → round-trip korrekt

#### R4-C-03 — CI: GitHub Actions nicht auf Commit-SHA gepinnt (Supply-Chain-Angriff) (P1)
**Tags:** Security · CI/CD · Supply Chain · OWASP A08
- [ ] **Fix umsetzen**
- **Files:** [test-pipeline.yml](../../.github/workflows/test-pipeline.yml#L24), [release.yml](../../.github/workflows/release.yml#L17), [benchmark-gate.yml](../../.github/workflows/benchmark-gate.yml#L44)
- **Problem:** Alle Actions (`actions/checkout@v4`, `setup-dotnet@v4`, etc.) auf floating Tags. Tag-Neuzuweisung erlaubt beliebige Code-Ausfuehrung im CI-Runner.
- **Fix:** Alle Actions auf unveraenderliche Commit-SHAs pinnen. `pinact` oder Dependabot-Actions einsetzen.

#### R4-C-04 — `AllowedRootPathPolicy`: keine Extended-Path/ADS-Abwehr (P1)
**Tags:** Security · Path Traversal · OWASP A01 · Safety Divergenz
- [ ] **Fix umsetzen**
- **File:** [AllowedRootPathPolicy.cs](../../src/Romulus.Infrastructure/Safety/AllowedRootPathPolicy.cs#L30)
- **Problem:** `IsPathAllowed()` nutzt `Path.GetFullPath()` direkt ohne `SafetyValidator.NormalizePath()`-Guards. `\\?\`/`\\.\`/ADS-Pfade werden von `AllowedRootPathPolicy` anders behandelt als von `SafetyValidator` → divergierende Sicherheitspfade.
- **Fix:** `AllowedRootPathPolicy.IsPathAllowed()` muss `SafetyValidator.NormalizePath(path)` aufrufen; bei `null`-Rueckgabe sofort `false`.
- **Tests fehlen:**
  - [ ] `\\?\`-Pfade, Device-Pfade, ADS-Pfade, Trailing-Dot-Segmente → jeweils `false`

#### R4-C-05 — `WriteMetadataSidecar` schreibt nicht-atomar (Sidecar korrupt bei Crash) (P1)
**Tags:** Audit · Atomicity · Data-Integrity
- [ ] **Fix umsetzen**
- **File:** [AuditSigningService.cs](../../src/Romulus.Infrastructure/Audit/AuditSigningService.cs#L171)
- **Problem:** `File.WriteAllText(metaPath, ...)` nicht-atomar. Crash erzeugt leeres/korruptes `.meta.json` → `Rollback()` dauerhaft blockiert. Schluesseldatei-Schreiben ist korrekt atomar (Zeile 74) — Sidecar nicht.
- **Fix:** `tmpPath = metaPath + ".tmp"` + `File.WriteAllText(tmp)` + `File.Move(tmp, meta, overwrite: true)`.
- **Tests fehlen:**
  - [ ] Simulierter Write-Crash (Truncate vor Move) → `VerifyMetadataSidecar` schlaegt kontrolliert fehl

#### R4-C-06 — `AbandonedMutexException` in `AcquireCrossProcessMutex` lautlos geschluckt (P1)
**Tags:** Audit · Data-Integrity · Mutex · Recovery
- [ ] **Fix umsetzen**
- **File:** [AuditCsvStore.cs](../../src/Romulus.Infrastructure/Audit/AuditCsvStore.cs#L237)
- **Problem:** `AbandonedMutexException` still gefangen → kein Log, kein Integritaets-Check. Halbgeschriebene CSV-Zeile moeglich. Naechster Write koennte korrupte Zeile anhaengen.
- **Fix:** Exception loggen; letzte Zeile der CSV auf Vollstaendigkeit pruefen (endet mit `\n`?).
- **Tests fehlen:**
  - [ ] Abandoned-Mutex-Szenario → Warn-Log wird emittiert

#### R4-C-07 — Dockerfile: floating Image-Tags ohne Digest-Pinning (P2)
**Tags:** Docker · Supply-Chain · Reproducible Builds
- [ ] **Fix umsetzen**
- **File:** [Dockerfile](../../deploy/docker/api/Dockerfile#L1)
- **Fix:** `FROM mcr.microsoft.com/dotnet/sdk:10.0@sha256:<digest>` und `FROM mcr.microsoft.com/dotnet/aspnet:10.0@sha256:<digest>`.

#### R4-C-08 — Caddyfile: Security Headers unvollstaendig (P2)
**Tags:** Security · HTTP Headers · XSS · Clickjacking
- [ ] **Fix umsetzen**
- **File:** [Caddyfile](../../deploy/docker/caddy/Caddyfile#L1)
- **Problem:** Nur `Strict-Transport-Security` gesetzt. Fehlend: `X-Content-Type-Options`, `X-Frame-Options`, `Content-Security-Policy`, `Referrer-Policy`, `Permissions-Policy`.
- **Fix:** Alle 5 fehlenden Header ergaenzen.

#### R4-C-09 — Smoke-Test: kein positiver AllowedRoots-Akzeptanz-Test (P2)
**Tags:** Smoke-Test · Test Coverage · AllowedRoots
- [ ] **Fix umsetzen**
- **File:** [Invoke-HeadlessSmoke.ps1](../../deploy/smoke/Invoke-HeadlessSmoke.ps1#L172)
- **Problem:** Smoke prueft nur Blocked-Root-Ablehnung (HTTP 400), nicht dass erlaubte Root akzeptiert wird (HTTP 202). Zu-aggressives AllowedRoots-Enforcement unsichtbar.
- **Fix:** Positiver POST `/runs` mit `$allowedRomRoot` → Assert HTTP 202.

#### R4-C-10 — `settings.schema.json` / `rules.schema.json`: zu liberale `additionalProperties` (P2)
**Tags:** Schema · Validation · Config Drift
- [ ] **Fix umsetzen**
- **Files:** [settings.schema.json](../../data/schemas/settings.schema.json#L12), [rules.schema.json](../../data/schemas/rules.schema.json#L43)
- **Fix:** `"additionalProperties": false` setzen. `tool-hashes.schema.json` SHA-Pattern `^[a-fA-F0-9]{64}$` als Required ergaenzen.

#### R4-C-11 — `AuditCsvStore`: UNC-Pfade ohne Spreadsheet-Schutz (CSV-Injection via SMB) (P2)
**Tags:** Security · CSV-Injection · Audit · OWASP A03
- [ ] **Fix umsetzen**
- **File:** [AuditCsvStore.cs](../../src/Romulus.Infrastructure/Audit/AuditCsvStore.cs#L204)
- **Problem:** `WriteAuditRowCore` nutzt `SanitizeCsvField` statt `SanitizeSpreadsheetCsvField`. UNC-Pfade (`\\NAS\ROMs\...`) koennen in Excel SMB-Auto-Resolution ausloesen.
- **Fix:** `SanitizeSpreadsheetCsvField` in `WriteAuditRowCore` verwenden.
- **Tests fehlen:**
  - [ ] UNC-Pfad im `oldPath`-Feld → Output enthaelt Quote-Escaping oder `'`-Praefix

#### R4-C-12 — `AppendAuditRow`: Einzelzeilen-Write nicht atomar (P2)
**Tags:** Audit · Atomicity · Data-Integrity
- [ ] **Fix umsetzen**
- **File:** [AuditCsvStore.cs](../../src/Romulus.Infrastructure/Audit/AuditCsvStore.cs#L96)
- **Problem:** `AppendAuditRow` oeffnet mit `FileMode.Append` + direktem Write. `AppendAuditRows` nutzt korrekt Temp+Rename. Crash kann unvollstaendige Zeile hinterlassen.
- **Fix:** `AppendAuditRow` auf `AppendAuditRows([row])` delegieren.
- **Tests fehlen:**
  - [ ] Write-Abbruch nach `WriteLine` → CSV danach noch parsebar

#### R4-C-13 — `ConsoleSorter.MoveSetAtomically`: Primaerdatei ohne expliziten Root-Check (P2)
**Tags:** Safety · Path Containment · Sorting
- [ ] **Fix umsetzen**
- **File:** [ConsoleSorter.cs](../../src/Romulus.Infrastructure/Sorting/ConsoleSorter.cs#L457)
- **Problem:** Set-Mitglieder bekommen `IsPathWithinRoot`-Check (Z. 470), Primaerdatei selbst nicht. Bei extern bereitgestellten `candidatePaths` keine Containment-Garantie.
- **Fix:** `if (!IsPathWithinRoot(primaryPath, root)) return (false, 0, []);` vor erstem Move.
- **Tests fehlen:**
  - [ ] `MoveSetAtomically` mit `primaryPath` ausserhalb des Roots → `(false, 0, [])`

#### R4-C-14 — CI: kein `retention-days` auf Test- und Release-Artifacts (P2)
**Tags:** CI · Data Retention · Privacy
- [ ] **Fix umsetzen**
- **Files:** [test-pipeline.yml](../../.github/workflows/test-pipeline.yml#L79), [release.yml](../../.github/workflows/release.yml#L68)
- **Fix:** `retention-days: 14` fuer Test-Artifacts, `retention-days: 30` fuer Release-ZIPs.

#### R4-C-15 — `JsonlLogWriter`: Dateisystem-Pfade im `root`-Feld aller Log-Eintraege (P2)
**Tags:** Security · Information Disclosure · OWASP A09 · Logging
- [ ] **Fix umsetzen**
- **File:** [JsonlLogWriter.cs](../../src/Romulus.Infrastructure/Logging/JsonlLogWriter.cs#L34)
- **Problem:** Vollstaendiger `root`-Pfad in jedem Log-Eintrag. Bei Remote-Logging koennen interne UNC-Pfade (`\\NAS\ROMs\SNES`) Netzwerktopologie preisgeben.
- **Fix:** `root`-Feld auf letztes Pfad-Segment reduzieren oder abstrahierte `rootId` (Hash-Praefix) loggen.
- **Tests fehlen:**
  - [ ] `JsonlLogWriter.Write()` ohne explizites Root-Argument → kein Pfad im Output

#### R4-C-16 — Docker `romulus-cli`: `read_only` fehlt (P3)
**Tags:** Docker · Security · Hardening
- [ ] **Fix umsetzen**
- **File:** [docker-compose.headless.yml](../../deploy/docker/docker-compose.headless.yml#L44)
- **Fix:** `read_only: true` und `tmpfs: [/tmp]` auch fuer `romulus-cli` ergaenzen.

#### R4-C-17 — Docker Healthcheck: `dotnet`-Prozess als Health-Probe teuer (P3)
**Tags:** Docker · Performance · Healthcheck
- [ ] **Fix umsetzen**
- **File:** [docker-compose.headless.yml](../../deploy/docker/docker-compose.headless.yml#L36)
- **Fix:** `test: ["CMD-SHELL", "curl -sf http://localhost:7878/healthz || exit 1"]`.

#### R4-C-18 — `tools/` Diagnose-Skripte: hardcodierte Pfade, kein Fehler-Handling (P3)
**Tags:** Hygiene · Scripts · Error Handling
- [ ] **Fix umsetzen**
- **Files:** [tools/dat-diag.ps1](../../tools/dat-diag.ps1#L1), [tools/dat-map-diag.ps1](../../tools/dat-map-diag.ps1#L1), [tools/DatDiag.csx](../../tools/DatDiag.csx#L3)
- **Fix:** Pfade als Parameter; `$ErrorActionPreference = 'Stop'`; Existenz-Check vor Verwendung.

#### R4-C-19 — `dat-catalog.schema.json`: `ConsoleKey` nullable aber im Code als Pflicht behandelt (P3)
**Tags:** Schema · Contracts · Data Drift
- [ ] **Fix umsetzen**
- **File:** [dat-catalog.schema.json](../../data/schemas/dat-catalog.schema.json#L13)
- **Fix:** Schema auf `"type": "string", "minLength": 1` aendern oder null-Fall mit Warn-Log im Loader explizit behandeln.

#### R4-C-20 — `RotateIfNeeded`: Rotations-Fehler lautlos ignoriert (P3)
**Tags:** Logging · Error Handling · Resilience
- [ ] **Fix umsetzen**
- **File:** [JsonlLogWriter.cs](../../src/Romulus.Infrastructure/Logging/JsonlLogWriter.cs#L105)
- **Fix:** try/catch um Rotations-Block; im catch: `Console.Error.WriteLine(...)` als Fallback-Strategie.

---

## Test- & Verifikationsplan

> Pflicht-Invarianten, die als Unit-/Integration-/Property-Tests ergaenzt werden muessen.

- [ ] **INV-01** Verify-Vertrag: Source nie in Trash bei `VerificationResult != Verified`
- [ ] **INV-02** GameKey-Eindeutigkeit unter Whitespace/Sonderzeichen (Property-Test)
- [ ] **INV-03** Cross-Volume-Move-Atomicity (kein Restdatei bei Cancel)
- [ ] **INV-04** Set-Move-Rollback-Vertrag (kein Throw, dokumentierte Result-Felder)
- [ ] **INV-05** Determinismus unter Permutation (`SelectWinner` 100x)
- [ ] **INV-06** Determinismus unter Parallelitaet (`EnrichmentPipelinePhase` 200x parallel)
- [ ] **INV-07** CSV-Sanitizer-Parity (DAT-Audit vs. Spreadsheet)
- [ ] **INV-08** Tool-Hash-Verify nach Timestomping
- [ ] **INV-09** Hardlink-KPI-Ehrlichkeit (`freed bytes` = 0)
- [ ] **INV-10** GUI/CLI/API-Paritaet (gleicher Input -> gleicher Status, gleiche DAT-Match-Counter, gleiche Decision-Class-Counts)
- [ ] **INV-11** Audit-Hash-Chain (Tampering bei Sidecar-Swap erkennt)
- [ ] **INV-12** Audit-CSV-Atomicity (Crash mid-write -> kein blockierter Rollback)
- [ ] **INV-13** SSRF-Block (Loopback/RFC1918 Hosts werden vor Connect abgelehnt)
- [ ] **INV-14** OwnerClientId-Enforcement (kein Cross-Tenant-Zugriff)
- [ ] **INV-15** HSTS/HTTPS-Pflicht im Remote-Modus

---

## Sanierungsstrategie

### Sofort (vor jedem weiteren Feature-Commit)
- [ ] P0-01 Conversion-Verify fail-closed
- [ ] P0-02 Test invertieren
- [ ] P0-03 GameKey-Whitespace-Hash
- [ ] P0-04 Cross-Volume-Move atomar
- [ ] P0-05 Sidecar atomar
- [ ] P0-06 HMAC-Key fail-closed
- [ ] P0-07 Rollback-Tampering-Detection
- [ ] P0-08 OwnerClientId-Enforcement

### Vor Release
- [ ] Alle P1-Funde abarbeiten
- [ ] INV-01 bis INV-15 implementiert

### Nachgelagert (nach Release)
- [ ] P2-Architektur-Debt (P2-04, P2-05, P2-06, P2-07, P2-10, P2-11)
- [ ] P3-Hygiene

### Bewusst verschieben (mit Begruendung dokumentieren)
- [ ] P3-06 Reparse-Point-Tiefenlimit (symbolisch, niedriger Schaden)
- [ ] P3-08 State-Transition-Test (niedriger Hebel)

---

## Anhang: Systemische Hauptursachen

1. **Verify-Vertrag fail-open statt fail-closed** -> Symptom in Conversion (P0-01), HMAC (P0-06), Sidecar (P0-05).
2. **„Eine fachliche Wahrheit" strukturell verletzt** -> Symptom in CSV-Sanitizer (P1-04), DAT-Update (P1-03), RunOrchestrator-Komposition (P1-02), Status-Strings (P2-09), Settings (P2-08).
3. **Static Mutable State in Core** -> Determinismus-Verletzung (P1-08, P2-06).
4. **Halbfertige Refactors** -> Schattenlogik (P1-01 Avalonia, P2-10 FeatureService, P1-11 MainViewModel).
5. **Tests betonieren Bug-Verhalten** (P0-02, P3-07).
6. **Audit-Kette ohne kryptografische Anker** (P1-17, P1-18, P0-06).
7. **Filesystem-Annahmen luckenhaft** (Cross-Volume P0-04, Hardlinks P1-09, Long-Path P2-02, Free-Space).

---

**Letzte Aktualisierung:** 2026-04-24
**Naechste Review:** nach Abschluss aller P0-Funde
