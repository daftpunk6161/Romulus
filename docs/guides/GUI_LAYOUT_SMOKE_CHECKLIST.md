# GUI Layout Smoke Checklist

Ziel: den WPF-Layout-Pass nach dem Commit `b96a91f` manuell auf realen Fensterbreiten und kritischen Nutzerflüssen verifizieren.

## Vorbereitung

- [ ] App im aktuellen Stand starten
- [ ] Dark/Synthwave aktiv prüfen
- [ ] mindestens eine kleine Test-Collection mit echten Roots verfügbar haben
- [ ] mindestens ein Preview-Run und ein vorhandener Report verfügbar haben

## Breakpoints

### 960 px

- [ ] Fenster auf ca. `960px` Breite setzen
- [ ] Navigation bleibt benutzbar
- [ ] Command Bar clippt nicht
- [ ] Subtabs bleiben horizontal nutzbar
- [ ] Inspector ist standardmäßig ausgeblendet
- [ ] Mission Control CTA bleibt vollständig sichtbar
- [ ] Progress-Screen bleibt ohne horizontales Abschneiden lesbar
- [ ] Result-Screen zeigt KPI-Karten und Move-Panel ohne Überlappung

### 1200 px

- [ ] Fenster auf ca. `1200px` Breite setzen
- [ ] Shell wirkt ausgeglichen, nicht gedrängt
- [ ] Command Bar zeigt Breadcrumb, Status-Chips und Aktionen stabil
- [ ] Mission-Control-Dashboard zeigt beide Hauptkarten sauber nebeneinander
- [ ] Result-Charts und Expander wirken visuell klar getrennt

### 1440 px

- [ ] Fenster auf ca. `1440px` Breite setzen
- [ ] Inspector lässt sich sinnvoll zuschalten
- [ ] Layout nutzt zusätzliche Breite ohne leere Wüsten
- [ ] KPI-Karten, Charts und TreeViews wirken nicht verloren oder überdehnt

## Kernflüsse

### Mission Control

- [ ] neue Root per Button hinzufügen
- [ ] Root per Drag & Drop hinzufügen
- [ ] Intent zwischen Clean / Sort / Convert wechseln
- [ ] Workflow- und Profilhinweise sind verständlich
- [ ] primäre CTA ist klar erkennbar

### Preview / Progress

- [ ] Preview mit `F5` starten
- [ ] Busy-Screen zeigt aktuelle Phase klar
- [ ] Fortschritt, Datei und Log sind gleichzeitig verständlich
- [ ] minimieren/weiterarbeiten wirkt nicht irritierend
- [ ] Cancel mit `Esc` ist auffindbar und verständlich

### Results / Apply

- [ ] KPI-Zusammenfassung ist auf den ersten Blick verständlich
- [ ] Move-/Apply-Bereich ist klar als kritische Aktion erkennbar
- [ ] Inline-Bestätigung für Move ist eindeutig
- [ ] Report öffnen und Rollback finden ist sofort möglich
- [ ] Console Distribution und Dedupe Decisions sind erreichbar, aber nicht dominant

### Navigation / Shell

- [ ] Wechsel zwischen Mission Control, Library, Config, Tools, System ist klar
- [ ] Subtabs wirken als sekundäre Navigation, nicht als Konkurrenz zur Hauptnavigation
- [ ] Inspector-Toggle in der Command Bar ist verständlich
- [ ] Smart Action Bar erscheint nur dort, wo sie sinnvoll ist

## Accessibility Smoke

- [ ] kompletter Grundfluss per Tastatur navigierbar
- [ ] Fokus-Reihenfolge bleibt logisch
- [ ] sichtbarer Fokus ist jederzeit erkennbar
- [ ] Shortcut-Overlay per `F1` funktioniert
- [ ] Inspector per `Ctrl+I` toggelbar
- [ ] Theme per `Ctrl+T` toggelbar

## Abnahme

- [ ] keine sichtbaren Clipping-/Overlap-Fehler
- [ ] keine unleserlichen Textgrößen mehr in Navigation und Header
- [ ] keine Layoutsprünge bei Wechsel zwischen Screens
- [ ] keine regressiven UX-Probleme gegenüber dem vorherigen Hauptfluss
- [ ] Findings dokumentiert und in Folgetickets überführt
