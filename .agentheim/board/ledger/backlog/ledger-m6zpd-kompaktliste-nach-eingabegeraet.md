---
id: ledger-m6zpd
title: "Kompaktliste nach Eingabegerät (ADR 0053 Punkt 6): mit Maus bekommt die schmale PC-Liste Kästchen, Umschalt-Klick, Prüf-Schaltfläche bei Hover, eine sichtbare Markierung und alle Tasten; mit Finger bleibt sie wie heute"
status: backlog
type: feature
context: ledger
created: 2026-10-20
completed:
depends_on: [design-system-001, ledger-w4vte]
blocks: []
tags: [frontend, kassenbuch, kompaktliste, auswahl, bedienkonzept]
related_adrs: [0043, 0049, 0052, 0053]
related_research: []
prior_art: [ledger-qh9qx, ledger-r1d2f, ledger-rjb8w, ledger-th36h]
---

## Why
Seit ADR 0052 zeigt der PC in schmalen Fenstern die Handy-Liste. Mit ihr kommen die
Handy-Gesten (langes Drücken, Wisch), und die gehen nur per Touch: mit der Maus gibt es dort
keine Auswahl, die Markierung ist unsichtbar, `G` wirkt blind (Bestandsaufnahme
`docs/ux/kassenbuch/bedienkonzept-zeile-bestandsaufnahme.md`, Reibung 10). ADR 0053 Punkt 6:
die Breite wählt nur das Layout, die Gesten folgen dem Eingabegerät. Stück 3 von drei.

## What
1. **Eingabegerät erkennen** über `matchMedia("(pointer: fine)")` (Maus, Trackpad) gegen
   `(pointer: coarse)` (Finger). Eine reine Funktion entscheidet aus diesem Wert und dem
   Layout (`visibleColumns` aus ADR 0052), welche Bedienung die Zeile bekommt. Der Wert wird
   zur Renderzeit gelesen wie `isDesktopViewport()` (ADR 0043); ein Wechsel des Geräts im
   laufenden Betrieb muss nicht live nachgeführt werden.
2. **Kompaktliste mit Maus:**
   - Ein Kästchen steht links in jeder Zeile, sichtbar ohne Hover; Klick schaltet die
     Zeile, Umschalt-Klick nimmt die Spanne (wie `ClickRowCheckbox` in der Tabelle).
   - Die Prüf-Schaltfläche erscheint bei Hover und bei markierter Zeile (wie `ledger-mhe2s`
     in der Tabelle) und ruht während einer Auswahl (`ledger-w4vte`).
   - Die markierte Zeile ist sichtbar hervorgehoben (Fokus-Darstellung aus `ledger-rjb8w`,
     `data-keyboard-focus`, scrollt in den Blick).
   - Alle Tasten aus ADR 0053 wirken wie in der Tabelle; das Detail öffnet hier als Sheet
     (`ledger-q1hw2`).
   - Langes Drücken und Wisch sind mit der Maus nicht nötig und bleiben aus.
   - Es gibt keinen `SelectionMode` wie am Handy; die Auswahl läuft wie in der Tabelle
     (nicht leer heißt "Auswahl läuft"), die Leiste ist die PC-Leiste.
3. **Kompaktliste mit Finger** (Handy, Tablet): unverändert, langes Drücken, Wisch,
   Prüf-Schaltfläche immer sichtbar, `SelectionMode`.
4. **Fußzeilen-Hinweis:** erscheint in der Kompaktliste nur mit Maus (heute `hidden md:flex`
   unabhängig vom Gerät).

## Acceptance criteria
- [ ] Expecto-Test der reinen Entscheidungsfunktion: Tabelle oder Kompaktliste kreuz Maus
      oder Finger ergibt die erwartete Bedienung (Kästchen sichtbar, Hover-Schaltfläche,
      Markierung sichtbar, Gesten an oder aus).
- [ ] Reducer-Test: in der Kompaktliste mit Maus schaltet ein Klick auf das Kästchen die
      Zeile, Umschalt-Klick nimmt die Spanne, und es entsteht kein `SelectionMode`.
- [ ] Playwright-Wächter (Maus, etwa 900 px Breite, Kompaktliste): Kästchen sichtbar und
      klickbar, `↓` hebt eine Zeile sichtbar hervor (`data-keyboard-focus`), `G` prüft sie,
      Enter öffnet das Detail als Sheet, Hover zeigt die Prüf-Schaltfläche.
- [ ] Playwright-Wächter (Touch-Emulation, 390 px): langes Drücken startet den
      Auswahl-Modus wie bisher, kein Kästchen ohne Modus, kein Fußzeilen-Hinweis.
- [ ] `dotnet build`, `dotnet test`, `npm run build`, `npm run guard:classes`,
      `npm run test:scripts` grün.
- [ ] Die Kompaktliste mit Maus wirkt wie eine schmale Tabelle und nicht wie ein Handy auf
      dem Schreibtisch. [human-eye]

## Notes
Entstanden aus der Konzeptrunde am 2026-10-20 (ADR 0053). Ändert die Folge aus ADR 0052
"Die kompakte Liste hat keine Kästchen-Spalte und keine Tabellen-Interaktionen": sie gilt
nur noch für Touch (ADR 0053, Consequences).

Der Sortier-Chip aus `ledger-skpcx` erscheint in der Kompaktliste unabhängig vom
Eingabegerät; daran ändert diese Aufgabe nichts.

Geräte mit beidem (Laptop mit Touchscreen) melden in Chrome in der Regel `pointer: fine`
als primären Zeiger und bekommen damit die Maus-Bedienung. `any-pointer: coarse` wird
bewusst nicht ausgewertet.
