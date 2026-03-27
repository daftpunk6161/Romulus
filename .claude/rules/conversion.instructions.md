# Romulus – Conversion-Regeln

## Grundsatz
Conversion darf niemals auf Kosten von Datenintegritaet oder Nachvollziehbarkeit aggressiv werden.

## Pflichtregeln
- Source-Dateien nie vor erfolgreicher Verifikation entfernen
- partielle Outputs bei Fehlern sauber behandeln
- externe Tools absichern:
  - Tool-Hash-Verifizierung
  - korrektes Argument-Quoting
  - Exit-Code-Pruefung
  - Timeout / Retry / Cleanup
- Tool-Ausgabe nicht blind vertrauen
- Output validieren, wenn Folgeentscheidungen davon abhaengen
- keine riskanten stillen Auto-Konvertierungen
- Set-Integritaet respektieren
- Preview und Execute muessen dieselbe fachliche Entscheidung zeigen

## Architektur
- Conversion-Regeln nicht parallel in GUI, CLI und API modellieren
- Policies, Prioritaeten und Verify-Regeln zentral halten
- keine lokalen Sonderpfade, wenn es bereits eine zentrale Conversion-Logik gibt

## Tests
Aenderungen an Conversion brauchen:
- Unit-Tests
- Regressionstests
- Negative / Edge-Tests
- Invarianten fuer Verify / Cleanup / deterministisches Verhalten
