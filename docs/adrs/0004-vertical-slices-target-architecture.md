# ADR 0004: Zielarchitektur Vertical Slices + Hexagonal-light

## Status
Accepted

**Reviewed by:** Core Team, GUI Team, Platform Team
**Approval date:** 2026-03-02
**Last updated:** 2026-03-02

## Kontext
Die Codebasis ist funktional stark, leidet jedoch unter zentralisierten Orchestrierungsdateien mit hoher Änderungs- und Regressionsgefahr. Besonders UI-Ereignislogik und Infrastrukturpfade sind eng gekoppelt.

## Entscheidung
Wir führen schrittweise eine Zielarchitektur ein:

1. **Domain Layer**
   - reine, deterministische Logik
   - keine UI/Dateisystem/Prozessaufrufe

2. **Application Layer (Use Cases)**
   - orchestriert Domain + Ports
   - kapselt Flows: `Run`, `Preflight`, `Convert`, `Rollback`, `Reporting`

3. **Adapter Layer**
   - WPF, CLI, API, Filesystem, Toolrunner, Plugin-Host
   - nur Adapter kennen Infrastrukturdetails

4. **Observability Layer (querliegend)**
   - strukturierte Fehlerverträge
   - CorrelationId durchgängig
   - standardisierte Security-/Audit-Events

## Technische Leitplanken
- Neue Features dürfen nicht direkt in `WpfEventHandlers.ps1` als Monolith wachsen.
- Jeder neue Flow wird als eigener UseCase mit Input/Output-Contract implementiert.
- Kein stilles `catch {}` in Domain/Application/API/IO (Ausnahmen nur UI-Kosmetik/Dispose mit Verbose).
- Plugin-Ausführung unterliegt Trust-Policy (`compat`, `trusted-only`, `signed-only`).

## Migrationsstrategie

### Phase A (kurzfristig)
- Sicherheits-Härtungen (API-Key fixed-time, CORS Profile, Plugin Trust Mode)
- Dead-Code-Kandidaten in produktiven Pfad integrieren oder entfernen
- Catch/Logging-Governance aktivieren

### Phase B (mittelfristig)
- `WpfEventHandlers.ps1` in vertikale Feature-Slices zerlegen
- Shared Runspace-Lifecycle helper für Dedupe/Convert
- UseCase-Contracts für Run/Preflight/Convert/Rollback

### Phase C (langfristig, Q3 2026)
- Vollständige Trennung Application↔Adapter
- Telemetrie-/Audit-Events vereinheitlichen
- PR-Gates für Architekturregeln und Komplexitätsgrenzen
- Zeitrahmen: 2026-06 bis 2026-08

## Migrationsfortschritt

| Phase | Status | Fortschritt | Referenz |
|-------|--------|-------------|----------|
| A | ✅ Done | 100% | API-Key, CORS, Plugin-Trust, Dead-Code-Cleanup |
| B | ✅ Done | 100% | 6/6 Slices done, Runspace-Helper done, UseCase-Contracts v1 implementiert, Dependency-Boundary-Tests aktiv, 76 Feature-Module mit 22 Service-Facades, Features-Tab (65 Buttons), ISS-001 Wizard |
| C | 🔄 In Progress | 25% | Governance-Gate implementiert, Architektur-Map dokumentiert, ModuleDependencyBoundary-Tests aktiv, CatchGuard-Compliance, Enforcement ab M3 |

## Konsequenzen

### Positiv
- Niedrigere Regressionsrate bei Feature-Ausbau
- Bessere Testbarkeit und schnellere Diagnose
- Klarere Ownership je Modul

### Negativ
- Anfangsinvestition für Refactoring und Umschulung
- Temporäre Doppelstrukturen während Migrationsphasen

## Verknüpfungen
- ADR 0002 (Ports/Services)
- ADR 0003 (Externalized WPF XAML)
- `docs/implementation/TECH_DEBT_REGISTER.md`
- `docs/implementation/REFACTORING_ROADMAP_2026Q2-Q3.md` (terminierter Umsetzungsplan)
- `docs/implementation/WPF_EVENTHANDLERS_SLICE_TICKETS.md` (Slice-Tickets 1–3)
- `dev/tools/Invoke-GovernanceGate.ps1` (Governance-Enforcement)
