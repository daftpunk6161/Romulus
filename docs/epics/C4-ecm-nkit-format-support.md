# C4: ECM/NKit Format-Support

Status: geliefert am 2026-04-01

## Problem

ECM (Error Code Modeler) und NKit (Nintendo Kit) sind verbreitete Kompressionsformate
in der ROM-Community. Romulus kann diese Formate weder lesen noch konvertieren.

## Loesungsansatz

Neue Tool-Invoker fuer `ecm` und `nkit`, Integration in die bestehende
Conversion-Pipeline via Registry-Erweiterung.

### Umgesetzte Komponenten

1. **EcmInvoker**
   - `unecm.exe`: ECM → BIN Dekompression
   - Verifikation: non-zero Output + zentraler Tool-Hash-Check
   - fail-closed ohne erwarteten Hash

2. **NkitInvoker**
   - `NKitProcessingApp.exe`: NKit → ISO Expansion fuer GameCube/Wii
   - Timeout, Exit-Code und Output-Pruefung
   - erwarteter SHA256 ist in der Registry gepinnt

### Registry-Erweiterung (`data/conversion-registry.json`)

- `.ecm -> .bin` ueber `unecm`
- `.nkit.iso -> .iso` ueber `nkit`
- `.nkit.gcz -> .iso` ueber `nkit`
- `.iso -> .rvz` fuer `GC`/`WII` bleibt der bestehende Folgeschritt
- NKit-Expansionsschritte sind als `lossless: true` modelliert, obwohl die Quellintegritaet `Lossy` bleibt; dadurch ist der Review-pflichtige Multi-Step-Plan zulaessig, ohne einen weiteren lossy Schritt zu kaschieren

### Tool-Hashes (`data/tool-hashes.json`)

- `NKitProcessingApp.exe` ist mit SHA256 gepinnt
- `unecm.exe` bleibt absichtlich fail-closed, bis ein kontrollierter Hash hinterlegt wurde

### Conversion-Graph

```
.ecm → .bin → .chd (via ecm + chdman)
.nkit.iso → .iso → .rvz (via nkit + dolphintool)
.nkit.gcz → .iso → .rvz (via nkit + dolphintool)
```

Multi-Step-Konvertierung laeuft ueber den bestehenden `IConversionPlanner` und `IConversionExecutor`.
Review-pflichtige Plaene bleiben ohne explizites `ApproveConversionReview` blockiert.

## Abhaengigkeiten

- Bestehende Conversion-Pipeline (A4 Decomposition)
- `IConversionPlanner` und `IConversionExecutor` Interfaces
- Tool-Hash-Verifikation (ToolRunnerAdapter)

## Risiken

- ECM/NKit Tools sind Community-Builds, keine offiziellen signierten Distributionen
- NKit-Konvertierung kann sehr langsam sein (Wii: 4-8 GB)
- Multi-Step-Fehlerbehandlung: Cleanup bei Abbruch zwischen Steps

## Teststand

- `ReachInvokerTests`
- `ReachConversionTests`
- `SourceIntegrityClassifierTests`
- bestehende Conversion-, Executor- und Invariantensuites
