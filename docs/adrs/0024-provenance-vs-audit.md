# ADR-0024: Provenance-Trail vs Audit-Sidecar — Verantwortungs-Trennung

## Status
Accepted

## Datum
2026-04-29

## Owner
daftpunk6161 (Repo-Maintainer)

## Bezug
- Plan: `docs/plan/strategic-reduction-2026/plan.yaml` (Task `T-W2-PROVENANCE-AUDIT-ADR`)
- Folge-Implementierung: `T-W7-PROVENANCE-TRAIL` (Wave 7) — darf ohne diese ADR **nicht** starten.
- AGENTS.md: "Eine fachliche Wahrheit", "Kein Datenverlust", "Keine doppelte Logik".
- Repo-Memory: `AuditSigningService` Sidecar+Ledger-Atomicity (April 2026 Deep-Dive Fix).
- Bestehende Implementierung: `src/Romulus.Infrastructure/Audit/AuditSigningService.cs` (Run-Sidecar `.meta.json` + Append-Only-Ledger `.ledger.jsonl`, atomar synchronisiert).

## Kontext

Romulus hat bereits einen verifizierten **Audit-Sidecar-Pfad**: jeder Run schreibt eine `.meta.json` neben den Run-Output, signiert per HMAC, und hängt den Sidecar-Hash zusätzlich in eine Append-Only-Ledger-Datei `.ledger.jsonl`. Der atomare Schreibpfad (Sidecar + Ledger als Einheit, mit Restore bei Append-Failure) ist im April-2026-Deep-Dive korrigiert worden und wird durch `AuditSidecarLedgerAtomicityTests` geschützt.

Ab Wave 7 wird zusätzlich ein **Provenance-Trail je ROM** verlangt (TOP-17): pro ROM-Fingerprint soll lebenslang nachvollziehbar sein, in welchen Runs der ROM gesehen, verschoben, konvertiert oder verworfen wurde. Ohne klare Architektur-Vorgabe würden zwei Wahrheiten entstehen:

- ein zweiter Sidecar-Schreibpfad neben dem Audit-Sidecar,
- eine zweite Ledger/Signing-Logik, die die Garantien des Audit-Pfads dupliziert,
- gemischtes Format (Run-Sidecar enthält Provenance-Trail-Einträge), was den Lese-Pfad pro ROM unbezahlbar macht.

Diese ADR legt **vor jeglichem Code** fest, wie Audit und Provenance koexistieren.

## Entscheidung

### 1. Verantwortungs-Trennung

| Pfad | Scope | Speicherort | Schreibmoment |
| --- | --- | --- | --- |
| **Audit-Sidecar + Ledger** | Run als Ganzes | neben dem Run-Output (`<run>.meta.json` + `.ledger.jsonl`) | Genau einmal pro Run, am Run-Ende |
| **Provenance-Trail** | Einzelner ROM über seine Lebenszeit | je ROM-Fingerprint in eigener Store-Datei (Layout: siehe §3) | Append-Only, eine Zeile pro Run, in dem der ROM betroffen war |

**Audit beantwortet:** "Was hat *dieser eine Run* getan, ist es manipulationsfrei, ist es rollback-fähig?"

**Provenance beantwortet:** "Was ist *diesem ROM* in seiner Geschichte passiert, durch welche Runs ist er gewandert?"

Die beiden Datenströme dürfen sich niemals gegenseitig ersetzen oder ineinander gemischt werden.

### 2. Gemeinsame Helper — keine Duplikation

Provenance erbt sämtliche Integritäts-Garantien von der Audit-Infrastruktur. Folgende Bausteine **müssen** wiederverwendet, **dürfen nicht** dupliziert werden:

- **Hashing:** `Romulus.Infrastructure.Hashing` (existierende SHA-256-Helfer).
- **Signing:** `AuditSigningService.HMAC*` mit derselben Key-Quelle. Kein zweiter HMAC-Schlüssel, kein anderes Verfahren.
- **Atomic-Write:** der Restore-on-Failure-Pattern aus `AuditSigningService.WriteMetadataSidecar` (Snapshot vorhandener Bytes → Schreiben → bei Folge-Failure Restore/Delete) wird in einen reusable Helper `Romulus.Infrastructure.Audit.AtomicSidecarWriter` extrahiert und sowohl von Audit als auch Provenance verwendet.
- **Ledger-Format:** Provenance verwendet das gleiche JSONL-Schema und die gleiche Append-Only-Semantik; nur der Dateiname unterscheidet sich (siehe §3).

Wenn die Wave-7-Implementierung versucht, einen eigenen Signing-Pfad, einen eigenen Atomic-Write oder ein eigenes Ledger-Format einzuführen, ist das ein Review-Block.

### 3. Speicherort und Layout

- Pro ROM-Fingerprint genau **eine** Provenance-Datei:
  `<provenance-root>/<fingerprint[0..1]>/<fingerprint[2..3]>/<fingerprint>.provenance.jsonl`
  (Sharding via Hash-Prefix, um Verzeichnis-Größen handhabbar zu halten.)
- Datei-Inhalt: Append-Only JSONL. Eine Zeile pro Provenance-Eintrag.
- Verbot: kein gemeinsames Provenance-Sammel-File über mehrere ROMs (würde Lese-Kosten pro ROM auf O(N) heben).
- Verbot: keine Provenance-Einträge im Audit-Sidecar oder im Audit-Ledger.

### 4. Cross-Reference-Pflicht

Jeder Provenance-Eintrag **muss** das Feld `audit_run_id` enthalten (= UUID des Runs, in dem der Eintrag entstand). Damit ist die Brücke zum Audit-Sidecar gegeben:

- Aus einem Provenance-Eintrag heraus ist die zugehörige `.meta.json` auffindbar.
- Verifikation eines Provenance-Eintrags lädt den referenzierten Audit-Sidecar mit, prüft dessen HMAC und dessen Ledger-Eintrag, und akzeptiert nur dann.
- Ohne `audit_run_id` ist ein Provenance-Eintrag ungültig und wird beim Lesen verworfen.

### 5. Verbote (zusammenfassend)

- **Kein zweiter Sidecar-Schreibpfad.** Audit-`.meta.json` bleibt einzige Run-Sidecar-Quelle.
- **Keine zweite Ledger-Datei für Audit.** `.ledger.jsonl` bleibt einzig.
- **Kein paralleles Signing-Verfahren.** Ein HMAC, ein Schlüssel-Pfad.
- **Keine Provenance-Einträge im Audit-Sidecar.**
- **Keine Audit-Run-Daten im Provenance-Trail.**
- **Kein In-Memory-only Provenance-Pfad** (würde Persistenz-Garantie brechen).
- **Kein Provenance-Schreibvorgang vor Audit-Sidecar-Commit.** Reihenfolge: Run-Engine → Audit-Sidecar+Ledger atomar → erst danach Provenance-Append (mit `audit_run_id` aus dem committeten Sidecar).

## Konsequenzen

- T-W7-PROVENANCE-TRAIL kann nun mit klaren Vertrags-Kanten starten (Store-Layout, gemeinsame Helper, Cross-Reference, Verbote).
- Refactor-Vorbereitung: vor dem Wave-7-Start muss `AtomicSidecarWriter` aus `AuditSigningService` extrahiert werden (kleiner reiner Refactor; Bestands-Tests in `AuditSidecarLedgerAtomicityTests` müssen weiter grün bleiben).
- gem-reviewer prüft jede Wave-7-PR explizit gegen die Verbots-Liste in §5; Verstoß = Block.
- Provenance-Lesepfad pro ROM bleibt billig (O(1) Datei-Lookup über Fingerprint-Sharding).
- Audit-Pfad bleibt unverändert; bestehende Garantien und Tests gelten weiter.

## Alternativen (verworfen)

1. **Provenance-Einträge im Audit-Sidecar mitführen.** Verworfen: Lese-Kosten pro ROM würden auf O(Anzahl Runs) explodieren; Sidecar-Größe wäre unbeschränkt.
2. **Eigenes Provenance-Signing.** Verworfen: doppelter Schlüssel-Pfad, doppelte Bug-Surface, widerspricht "Eine fachliche Wahrheit".
3. **Globales Single-File-Provenance-Ledger über alle ROMs.** Verworfen: Append-Only-Schreibe wird zum globalen Lock-Punkt; Verifikation eines einzelnen ROM müsste über die gesamte Datei scannen.

## Test-Pflicht (bei Wave 7)

- Provenance-Append nach gescheitertem Audit-Sidecar-Commit darf nicht stattfinden.
- Provenance-Eintrag ohne gültige `audit_run_id` wird beim Lesen verworfen.
- Kein Schreibpfad in Wave-7-Code referenziert direkt `WriteMetadataSidecar` oder `AppendLedgerEntry`; alle Schreibvorgänge gehen über den extrahierten `AtomicSidecarWriter`.
- Keine zweite HMAC-Initialisierung im Repo (grep-Guard im CI).
