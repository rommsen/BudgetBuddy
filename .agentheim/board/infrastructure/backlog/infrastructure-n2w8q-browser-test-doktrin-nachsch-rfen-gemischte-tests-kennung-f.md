---
id: infrastructure-n2w8q
title: "Browser-Test-Doktrin nachschärfen: gemischte Tests, Kennung für Verlauf und Routing, Spielarten als eigene Flows, Einzeiler-Verdrahtung im update"
status: backlog
type: decision
context: infrastructure
created: 2026-11-03
completed:
depends_on: [infrastructure-b3t9k]
blocks: []
tags: [tests, browser, doktrin, entscheidung]
related_adrs: []
related_research: []
prior_art: [infrastructure-b3t9k, infrastructure-t5m2b]
---

## Why
qa-milestone-reviewer hat am 2026-11-03 nach infrastructure-b3t9k Fragen gefunden, die die Doktrin (`.claude/rules/test-doctrine.md` Punkt 3 und 5) offenlässt. Sie sind Romans Festlegung.

## What
1. **Gemischte Tests:** Reicht eine Kennung für den ganzen Test, oder müssen Logik-Teilaussagen in gekennzeichneten Tests nach .NET? Beispiele: `scripts/category-detail.test.mjs` (`[layout]`, darin "Kontowechsel lädt nicht neu" und "Browser-Zurück öffnet die Buchung nicht erneut"), `ledger-sort-chip.test.mjs` ("Betrag aufsteigend sortiert flach"), `rules-page-mobile.test.mjs` ("Ändern speichert die neue Kategorie"), `budget-page.test.mjs` ("zeigt denselben Inhalt").
2. **Verlauf und Routing:** eigene Kennung (etwa `[verlauf]`) oder Teil eines Flows?
3. **aria-label unter `[tastatur]`:** `expectIconButtonsLabelled` in `rules-page-mobile.test.mjs` prüft ein DOM-Attribut, kein Tastatur-Ereignis (Regressionstest zu categorization-f955d).
4. **Spielarten als eigene Flows:** `lena-fragen` und `lena-fragen-mehrere`, `geld-verschieben` und `verschiebung-erneut-senden`, `kategorien-neu-laden`. Soll das Gate auch einen Flow in `FLOWS` ohne Test melden?
5. **Einzeiler-Verdrahtung:** Wird "Antwortfunktion plus Einzeiler im `update`" ohne Test der Verdrahtung hingenommen (Ledger `entriesPageAnswer`, `payeeSuggestionsAnswer`, Vorbild `totalsRefreshAnswer`), oder verlangt Punkt 3 einen Beleg je Anschlussstelle?
6. Zur Kenntnis: b3t9k hat `[layout]` um berechnete Farben und `[fable]` um Netzfehler des Remoting-Proxys erweitert (Verifier: passt zur Grundregel).

## Acceptance criteria
- [ ] Roman hat die Punkte 1 bis 5 entschieden, Punkt 6 bestätigt oder geändert.
- [ ] Doktrin Punkt 5 (und ggf. 3) und `scripts/browser-usage.mjs` folgen der Entscheidung, betroffene Tests sind angepasst.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.
