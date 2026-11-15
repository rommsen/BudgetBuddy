---
id: ledger-fp4wn
title: "Auswahl außerhalb des geladenen Fensters: Leiste und Panel zählen gewählte, nicht mehr geladene Buchungen mit"
status: backlog
type: bug
context: ledger
created: 2026-11-15
completed:
depends_on: []
blocks: []
tags: [frontend, auswahl, daten]
related_adrs: [0096]
related_research: []
prior_art: [ledger-ct3cx, ledger-cnaz3]
---

## Why
Eine gewählte Buchung (`Picked`), die beim Nachladen aus dem geladenen Fenster rutscht, bleibt gewählt. `selectionTotals`
zählt unter `Picked` nur geladene Zeilen, die Leiste zeigt sie also nicht mehr. „Geprüft“ und „Kategorie setzen“ schicken
aber alle Ids aus `Picked` und treffen sie. Man ändert Buchungen, die man nicht sieht und die nicht gezählt sind.
Entschieden (Roman, 2026-11-15, BB Prüfliste q-fenster): mitzählen, nicht aus der Auswahl werfen.

## What
Unter `Picked` zählen Leiste, Summenzeile und Panel jede gewählte Id, auch eine nicht mehr geladene, mit Betrag. Das Panel
sagt, wie viele davon gerade nicht in der Liste stehen. Sammelaktionen treffen genau, was gezählt ist.

## Acceptance criteria
- [ ] GIVEN zwei Buchungen gewählt, eine rutscht beim Nachladen aus dem Fenster, WHEN die Leiste erscheint, THEN zeigt sie
      „2 ausgewählt“ und die Summe beider Beträge (kaputt-Szenario, heute „1“; Regressionstest vor dem Fix rot).
- [ ] Der Betrag einer nicht mehr geladenen gewählten Buchung kommt aus dem Stand beim Wählen (etwa eine Map Id → Betrag
      im Modell, gepflegt bei Wählen, Abwählen, Leeren und bei Treffern aus dem Spiegel-Lauf, wenn die Zeile wieder geladen
      ist); eine in YNAB gelöschte zählt wie heute nicht in die Summe.
- [ ] Die Zusammenfassung im Panel (ADR 0096) listet die geladenen und nennt den Rest als Zeile „und n weitere, gerade nicht
      in der Liste“.
- [ ] `singleSelectedEntry` und die Leiste `SingleSelectionBar` bleiben bei genau einer gewählten, nicht geladenen Buchung
      konsistent: kein Detail, die Leiste zeigt „1 ausgewählt“.
- [ ] `AllExcept` bleibt unverändert (zählt schon gegen `TotalCount`).
- [ ] Sammelaktionen („Geprüft“, „Kategorie setzen“) schicken dieselben Ids, die gezählt sind; ein Test vergleicht Zahl der
      Leiste und Ids der Anfrage im Zustand nach dem Nachladen.
- [ ] Filterwechsel und „Auswahl aufheben“ leeren die Auswahl wie heute samt der gemerkten Beträge.

## Notes
Fundstellen: `selectionTotals`, `selectedEntries`, `singleSelectedEntry` in `src/Client/Components/Ledger/Types.fs`
(um Zeile 1800). Frage aus dem Review 2026-11-15 zu ledger-ct3cx (Liste behält Auswahl nach Spiegel-Lauf).
