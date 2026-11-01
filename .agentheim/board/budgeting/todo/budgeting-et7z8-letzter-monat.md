---
id: budgeting-et7z8
title: "Budget-Seite: dritter Knopf Letzter Monat, Verschieben geht dort, Targets anwenden nicht"
status: todo
type: feature
context: budgeting
created: 2026-11-01
completed:
depends_on: [design-system-001]
blocks: []
tags: [budgeting, ui, monat]
related_adrs: [0065, 0069]
related_research: []
prior_art: [budgeting-k7wmd, budgeting-h2zfn, budgeting-afd8h]
---

## Why
Am Monatsersten will Roman vor dem Verteilen auf den abgelaufenen Monat schauen und dort bei Bedarf noch korrigieren. Die Budget-Seite kennt nur diesen und den nächsten Monat.

## What
**Entscheidung von Roman (2026-11-01):** im letzten Monat geht Verschieben, Targets anwenden nicht.

- Die Monatsauswahl zeigt drei Knöpfe in zeitlicher Reihenfolge: Letzter Monat, Dieser Monat, Nächster Monat. Vorgabe bleibt Dieser Monat.
- `BudgetMonthChoice` bekommt `LastMonth`, Schlüssel auf dem Draht `"last"`. `monthKey` rechnet den Vormonat, auch über den Jahreswechsel.
- `getBudgetMonth` nimmt `"last"` an. `previewTargets` und `applyTargets` lehnen `"last"` an der API-Grenze ab, ohne YNAB-Anfrage (eigene Validierung, `validateBudgetMonth` bleibt für das Lesen).
- Im letzten Monat fehlt der Knopf "Targets anwenden". Verfügbar-Betrag (Verschieben) und Kategorie-Detail funktionieren wie in den anderen Monaten. `moveMoney` bekommt den Monat schon heute vom Client und braucht keine Änderung.

## Acceptance criteria
- [ ] `monthKey` liefert für `LastMonth` den Vormonat; am 2026-01-15 ist das "2025-12-01" (Unit-Test).
- [ ] `parseMonthChoice` liest "last"; die Fehlermeldung bei ungültigem Schlüssel nennt alle drei Werte (Unit-Test).
- [ ] GIVEN Schlüssel "last" WHEN `previewTargets` oder `applyTargets` aufgerufen wird THEN kommt ein Fehler und YNAB wird nicht angefragt (Test auf API-Ebene mit Fake-Port, Zeile in `RemotingEndpointNegativeTests.fs`, falls die Tabelle die Eingabe abdeckt).
- [ ] GIVEN Schlüssel "last" WHEN `getBudgetMonth` aufgerufen wird THEN liefert der Fake-YNAB-Abruf den Vormonat (Test).
- [ ] `select LastMonth` lädt den Monat wie die anderen Knöpfe, Monatswechsel lassen die Suche stehen (Client-Test).
- [ ] Browser-Wächter in `scripts/budget-page.test.mjs`: Knopf "Letzter Monat" fordert den Schlüssel "last" an, "Targets anwenden" ist dort nicht sichtbar und beim Zurückwechseln auf "Dieser Monat" wieder da.
- [ ] Browser-Wächter in `scripts/move-money.test.mjs`: im letzten Monat geht `moveMoney` mit dem Vormonat hinaus.
- [ ] Die drei Knöpfe passen bei 390 px ohne horizontales Scrollen der Seite (Browser-Wächter).
- [ ] README budgeting: Bullet Budget-Seite nennt die drei Monate und die Einschränkung; Kommentare in `src/Shared/Api.fs` und `src/Shared/Budgeting.fs` nennen "last".
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

## Notes
- Der Server rechnet "heute" in `Api.fs` mit `DateTime.Today`; das bleibt.
- `ChipBar.bar` ist die vorhandene Komponente, keine neue bauen. Bricht sie am Handy um, ist das in Ordnung, solange die Seite nicht seitlich scrollt.
- Offene Verschiebung und Erneut senden tragen den Monat in den Bewegungen und brauchen keine Änderung.
