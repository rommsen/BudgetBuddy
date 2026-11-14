---
id: categorization-m47g8
title: "Neue Regeln kommen ans Ende der Liste (niedrigste Priorität), aus der Buchung wie im Tab Regeln"
status: todo
type: bug
context: categorization
created: 2026-11-14
completed:
depends_on: [design-system-001-styleguide]
blocks: []
tags: [regeln, prioritaet, frontend]
related_adrs: [0094]
related_research: []
prior_art: [categorization-msep5, categorization-n3sxw]
---

## Why
Romans Blick nach dem Deploy vom 2026-11-14: „Regel anlegen“ legt die Regel oben an, sie soll wieder unten stehen.
Seit categorization-msep5 (342217df) vergibt der Server die Priorität so, dass jede neue Regel vorn steht. Regeln
laufen von oben nach unten, die erste passende gewinnt; eine neue Regel sticht damit alle älteren aus.

## What
`Rules.CommandHandlers.createRule` (`src/Server/Rules/CommandHandlers.fs:48`) holt die Priorität über
`Persistence.Rules.nextPriority` (`src/Server/Persistence.fs:483`, `MAX(priority) + 1`); Liste und Engine sortieren
`priority DESC, rowid ASC`. Entschieden (Roman, 2026-11-14): jede neue Regel bekommt die niedrigste Priorität und steht
am Ende, für beide Wege, die über denselben Befehl laufen: „Regel anlegen“ aus Buchungs-Detail und Karte des
Durchgehens (ledger-hg5m5) und „Neue Regel“ im Tab Regeln (categorization-n3sxw). Der Client zeigt sie nach dem
Anlegen unten (`src/Client/Components/Rules/State.fs:35` und die Stelle, die sie heute vorn einfügt).

## Acceptance criteria
- [ ] GIVEN Regeln mit den Prioritäten 3, 2, 1 WHEN eine neue Regel angelegt wird THEN hat sie eine Priorität kleiner
      als jede vorhandene und steht in `getAllRules` an letzter Stelle. Regressionstest auf Handler-Ebene, vor dem Fix
      rot (Doktrin Punkt 8, Kommentar nennt den Bug).
- [ ] Intakt: Ohne vorhandene Regel bekommt die erste eine gültige Priorität; Umordnen, Bearbeiten (Priorität bleibt)
      und `restoreRule` (ADR 0094, Regel kommt an ihre alte Stelle) verhalten sich wie vorher, belegt durch die
      vorhandenen Tests oder je einen Fall.
- [ ] Die Engine (`RulesEngine.classify`, Import und `ruleCategoryFor`) wertet eine neue Regel nach allen älteren aus:
      passt eine ältere Regel auf denselben Payee, gewinnt sie.
- [ ] Im Tab Regeln steht die neue Regel nach dem Anlegen unten in der Liste, aus „Neue Regel“ wie aus „Regel
      anlegen“; der Toast „Regel angelegt“ bleibt. Client-`update`-Test; der Kommentar an `State.fs:35` ist angepasst.
- [ ] Wo Doku oder Tests „steht vorn“ sagen (README categorization und ledger, `RulesListTests`, ledger-hg5m5-Bezug),
      ist es auf „steht am Ende“ nachgezogen.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

## Notes
Erfasst und verfeinert 2026-11-14 aus Romans Blick (Entscheidung: beide Wege unten). Die Priorität vergibt weiter der
Server; ob das über `MIN(priority) - 1` oder ein Neunummerieren geht, entscheidet der Worker nach dem, was Umordnen
und `restoreRule` erwarten (ganze Zahlen, auch 0 oder negativ, sofern nichts dagegen spricht).
