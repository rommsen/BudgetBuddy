---
id: ledger-w4vte
title: "Auswahl nach ADR 0053: Leertaste wählt die markierte Zeile, eine beginnende Auswahl schließt das Detail, Esc schließt Popover, dann Detail, dann Auswahl; während einer Auswahl ruhen Prüf-Schaltfläche, G und Wisch auf PC und Handy"
status: backlog
type: feature
context: ledger
created: 2026-10-20
completed:
depends_on: [design-system-001, ledger-q1hw2]
blocks: [ledger-m6zpd]
tags: [frontend, kassenbuch, auswahl, tastatur, bedienkonzept]
related_adrs: [0049, 0050, 0053]
related_research: []
prior_art: [ledger-r1d2f, ledger-rjb8w, ledger-nhwrt, ledger-th36h, ledger-mhe2s]
---

## Why
ADR 0053 trennt die Bedienung in "eine Buchung, das Detail" und "viele Buchungen, die
Leiste". Heute laufen Auswahl, Fokuszeile und Prüfwege mit verschiedenen Regeln
nebeneinander: am PC bleibt die Prüf-Schaltfläche bei laufender Auswahl aktiv, `G` nicht;
die Leertaste aus allen Handoffs ist nicht belegt; Esc tut in der Liste nichts; am Handy
reagiert das Kästchen im Auswahl-Modus laut Code nicht auf einen Tipp (Bestandsaufnahme
`docs/ux/kassenbuch/bedienkonzept-zeile-bestandsaufnahme.md`, Reibungen 6, 7, 9, 11, 13, 14).
Stück 2 von drei (nach `ledger-q1hw2`, vor `ledger-m6zpd`).

## What
Entscheidungen Roman, 2026-10-20 (ADR 0053 Punkte 2, 4, 5, 7):

1. **Leertaste** nimmt die markierte Zeile (`FocusedRow`) in die Auswahl oder heraus, wie
   ein Klick auf ihr Kästchen (Anker setzen wie `ClickRowCheckbox (id, false)`). Ohne
   markierte Zeile tut sie nichts. Sie wirkt nicht in Eingabefeldern und nicht mit
   Strg, Meta oder Alt (wie die übrigen Listentasten aus `ledger-rjb8w`).
2. **Eine beginnende Auswahl schließt das Detail.** Sobald die Auswahl von leer auf nicht
   leer wechselt (Kästchen, Umschalt-Klick, Leertaste, Kopfzeilen-Kästchen, am Handy
   langes Drücken), wird ein offenes Panel oder Sheet geschlossen. Solange die Auswahl
   nicht leer ist, öffnet ein Klick auf die Zeile am PC kein Detail, er markiert nur;
   am Handy wählt ein Tipp im Auswahl-Modus aus wie heute.
3. **Esc-Kette:** `Esc` schließt das zuletzt Geöffnete, in dieser Reihenfolge: ein offener
   Chip-Popover, dann das Detail, dann die Auswahl (`ClearSelection`). Ein `Esc` schließt
   genau eine Stufe.
4. **Sperren während einer Auswahl, auf PC und Handy gleich:** die Prüf-Schaltfläche an
   der Zeile ist nicht sichtbar und nicht bedienbar (am PC heute aktiv), `G` und
   `Umschalt+G` wirken nicht (wie heute), der Wisch nach rechts ist aus (wie heute). Der
   einzige Prüfweg ist "Geprüft" in der Leiste.
5. **Handy-Kästchen:** Ein Tipp auf das Kästchen einer Zeile im Auswahl-Modus wählt sie aus
   oder ab, genau wie ein Tipp auf die Zeile (Trefferfläche mindestens 44 px).
6. **Kein doppeltes Auslösen:** Nach einem Mausklick auf Kästchen oder Prüf-Schaltfläche
   liegt der Browserfokus auf dem Knopf. Enter und Leertaste dürfen dann nicht zugleich die
   native Knopf-Wirkung und die Listentaste auslösen; `keyCommandFor` behandelt einen
   fokussierten Knopf in der Liste so, dass genau eine Wirkung eintritt (die Listentaste;
   der Knopf behält beim Klick keinen Fokus oder seine native Aktivierung wird
   unterdrückt, Wahl beim Bau).
7. **Fußzeilen-Hinweis** nennt je Zustand alle belegten Tasten:
   - ohne Auswahl, ohne Panel: "↑ ↓ J K bewegen · Enter Detail · Leertaste auswählen ·
     G geprüft · Umschalt+G ungeprüft"
   - Panel offen: "↑ ↓ bewegen · G geprüft · Esc Panel schließen" (aus `ledger-q1hw2`)
   - mit Auswahl ersetzt die Leiste den Hinweis wie heute; die Leiste nennt zusätzlich
     "Esc aufheben".
   Genaue Schreibweise beim Bau nach dem Styleguide, die Belegungen sind fest.

## Acceptance criteria
- [ ] Reducer-Test: Leertaste auf der markierten Zeile nimmt sie in die Auswahl, ein zweites
      Mal heraus; ohne markierte Zeile ändert sie nichts; in einem Eingabefeld und mit
      Strg/Meta/Alt wirkt sie nicht.
- [ ] Reducer-Test: der Wechsel der Auswahl von leer auf nicht leer schließt ein offenes
      Detail, für jeden Weg (Kästchen, Umschalt-Klick, Leertaste, Kopfzeilen-Kästchen,
      `StartSelectionMode`).
- [ ] Reducer-Test: mit nicht leerer Auswahl setzt ein Klick auf eine PC-Zeile die
      Markierung und öffnet kein Detail.
- [ ] Reducer-Test der Esc-Kette: Popover offen, dann Detail, dann Auswahl; jedes `Esc`
      schließt genau eine Stufe; ohne offene Stufe ändert `Esc` nichts.
- [ ] Ansichts-Test: bei nicht leerer Auswahl rendert die PC-Zeile keine bedienbare
      Prüf-Schaltfläche; `G` und Wisch bleiben gesperrt (bestehende Tests grün).
- [ ] Ansichts- oder Playwright-Test (390 px): ein Tipp auf das Kästchen im Auswahl-Modus
      wählt die Zeile aus und wieder ab.
- [ ] Playwright-Wächter (PC 1440 px): Mausklick auf ein Kästchen, danach Leertaste: die
      Auswahl ändert sich genau einmal (kein doppeltes Kippen).
- [ ] Test des Hinweistexts je Zustand (ohne Auswahl, Panel offen).
- [ ] `dotnet build`, `dotnet test`, `npm run build`, `npm run guard:classes`,
      `npm run test:scripts` grün.
- [ ] Auswählen per Tastatur fühlt sich am PC flüssig an, ohne dass die Maus nötig ist.
      [human-eye]

## Notes
Entstanden aus der Konzeptrunde am 2026-10-20 (ADR 0053). Roman will große Umbauten statt
Zwischenschritte; diese Aufgabe räumt die Auswahl-Regeln in einem Zug auf.

Am PC gibt es weiter keinen eigenen Auswahl-Modus; "Auswahl läuft" heißt dort: die Auswahl
ist nicht leer. Am Handy bleibt `SelectionMode` aus `ledger-r1d2f`.

Ins Detail kommt man am Handy aus dem Auswahl-Modus nur über "Auswahl aufheben"
(ADR 0053 Punkt 7, Handoff Ablauf 3, offener Punkt 3).

Betroffene Stellen laut Bestandsaufnahme: `keyboardBlocked`/`reviewKeyBlocked` (State.fs),
`keyCommandFor` (Types.fs), `reviewMarkButton` und `entryTableRow` (View.fs),
`MultiSelect.box` und der Kästchen-Container in der Handy-Zeile, `resetEntries`.
