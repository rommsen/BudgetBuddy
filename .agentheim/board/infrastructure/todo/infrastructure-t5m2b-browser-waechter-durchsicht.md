---
id: infrastructure-t5m2b
title: "Browser-Tests auf das reduzieren, was nur der Browser prüfen kann, Logik wandert in MVU-Tests"
status: todo
type: chore
context: infrastructure
created: 2026-11-02
completed:
depends_on: []
blocks: []
tags: [test, waechter, gate]
related_adrs: []
related_research: []
prior_art: []
---

## Why
Roman: "500 sind mir eindeutig zu viele. Die Doktrin ist, dass nur Dinge, die wirklich nur im Frontend getestet werden können, auch darin laufen. Alles andere nicht. Wenn man Frontend-Logik testet, sollte das mit der MVU-Architektur auch anders gehen. Ich will nicht ständig flaky Tests."

Stand 2026-11-03: 437 Tests in `scripts/*.test.mjs`, davon 341 in 42 Dateien mit Browser oder gestartetem Frontend. 35-mal `waitForTimeout`, obwohl Doktrin Punkt 5 es verbietet. Die Node-Tests kosten rund 248 von rund 420 Sekunden Gate und sind die Quelle des Flackerns.

## What
Elmish trennt Zustand und Darstellung: `update` läuft unter .NET ohne Browser. Logik gehört dorthin, in reine Funktionen oder in Server-Tests. Im Browser bleibt, was nur dort messbar ist.

**Bleibt im Browser:**
- Layout und Maße (Breiten, Abschneiden, sticky, Viewport, Trefferflächen)
- Fokus, Tastatur-Ereignisse im echten DOM, Scrollen
- was nur im Fable-Kompilat gilt (Dezimalrechnung, Datumsformate)
- je Nutzerfluss genau ein Happy Path

**Wandert oder fällt weg:**
- welcher Text, welche Zahl, welcher Zustand nach einem Klick erscheint: Client-`update` unter .NET
- Sichtbarkeits- und Sperrlogik: reine Funktion aus dem View ziehen und unter .NET testen (Vorbild `assignKeyAction` und `amountKeyAction` in `Components/Budget/Moves.fs`), die View ruft sie nur auf
- Doppelungen zu vorhandenen .NET-Tests: löschen
- Tests für Hilfswerkzeuge (`screenshot-route`, `export-design-system`) auf das Nötigste kürzen, der Screenshot-Pfad ist kein Produkt

Dateien ohne Browser (`tailwind-class-guard`, `readme-delta-check`, `verify-lib`, `token-guard` und ähnliche) sind nicht Gegenstand dieser Task.

## Acceptance criteria
- [ ] Höchstens 100 Tests laufen im Browser. Die Zahl ist die Vorgabe des Owners im Sinn von "deutlich weniger". Liegt eine ehrliche Durchsicht darüber, begründet das Outcome je Datei, warum der Rest nur im Browser geht.
- [ ] Kein `waitForTimeout` in `scripts/*.test.mjs`.
- [ ] Ein Wächter im Gate (Vorschlag: `scripts/run-guards.test.mjs` oder eigener Test ohne Browser) zählt die Browser-Tests und `waitForTimeout` und wird rot, sobald die Obergrenze überschritten ist. Die Obergrenze steht als Konstante mit Kommentar im Wächter. Gegenbeweis: ein zusätzlicher Test oder ein `waitForTimeout` macht ihn rot.
- [ ] Doktrin geschärft in `.claude/rules/test-doctrine.md` Punkt 5 und Punkt 1: Browser-Wächter nur für das, was unter .NET nicht prüfbar ist, neue Logik-Tests im Browser sind verboten, die Obergrenze ist genannt. CLAUDE.md verweist weiter nur auf die Doktrin.
- [ ] Liste im Outcome je Datei: Tests vorher und nachher, Laufzeit vorher und nachher, was gezogen und was gelöscht wurde.
- [ ] Kein Verhalten verliert seine Absicherung: Je gezogener oder gelöschter Verhaltensgruppe nennt das Outcome die Mutante im Produktionscode, die den verbleibenden Test rot macht. Eine Gruppe ist ein fachliches Verhalten, nicht ein einzelner Test. Reine Lesart reicht nicht, gemessen wird stichprobenartig mindestens je Datei eine Mutante, bei `budget-page`, `move-money`, `ledger-filter-bar` und `rules-page-mobile` je Verhaltensgruppe.
- [ ] Produktionscode ändert nur seine Form, damit Logik testbar wird (reine Funktionen). Kein sichtbares Verhalten ändert sich.
- [ ] Der Test-Gesamtlauf ist dreimal hintereinander grün, ohne Wiederholung einzelner Tests.
- [ ] `TEST_CONCURRENCY` bleibt unverändert (die Messung ist eine eigene Aufgabe auf Romans Auftrag).
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

## Notes
Löschen ist von Roman freigegeben (2026-11-03, "setze das selbständig um"), die alte Bedingung "Löschen nur nach Freigabe" entfällt damit. Die frühere Abhängigkeit von `budgeting-m3x7f` ist aufgehoben: Die Budget-Seite wird jetzt mit reduziert, die Tasks 3 bis 5 folgen der geschärften Doktrin.

`RetryTests.fs`: Das dortige Inventar liest Quelltext und ist nach infrastructure-h4q8v die einzige Absicherung der Aufrufstellen. Es ist kein Kandidat zum Aufräumen.

Client-`update` unter .NET geht nur für Nachrichten, deren Cmd nicht `Client.Api` berührt (Doktrin Punkt 1). Nach budgeting-c5w2n liegen auf der Budget-Seite die Lese-Cmds hinter einem Lambda. Andere Seiten, vor allem das Kassenbuch, brauchen dieselbe Form, bevor ihre Logik unter .NET prüfbar ist. Das ist Teil dieser Task und gehört in denselben Schritt wie das Ziehen.

Vorgehen in dieser Reihenfolge: erst die Doktrin und den Wächter mit der heutigen Zahl als Obergrenze, dann Datei für Datei von groß nach klein, zuletzt die Obergrenze absenken. Nach jeder großen Datei grün belegen.

Auffällig: `budget-page` 45, `screenshot-route` 32, `export-design-system` 19, `ledger-filter-bar` 19, `rules-page-mobile` 18. Prüfe, ob `static-serve` und `export-design-system` wirklich einen Browser brauchen.
