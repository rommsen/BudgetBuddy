---
id: budgeting-et7z8
title: "Budget-Seite: dritter Knopf Letzter Monat, Verschieben geht dort, Targets anwenden nicht"
status: done
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

## Outcome

Die Budget-Seite zeigt drei Monatsknöpfe in zeitlicher Folge: Letzter Monat, Dieser Monat, Nächster Monat. Vorgabe bleibt Dieser Monat. Bei 390 px passen alle drei in den Viewport, die Seite scrollt nicht seitlich (Browser-Wächter). `./verify.sh` zeigt `VERIFY: PASS` (dotnet 3168, node 412). `check:readme-delta` ist grün.

Wichtige Stellen:
- Shared: `src/Shared/Budgeting.fs` (`LastMonth`, Schlüssel "last", `targetsAllowed` als gemeinsame Regel für API-Grenze und Client), Kommentare in `src/Shared/Api.fs`.
- Server: `monthKey` rechnet den Vormonat auch über den Jahreswechsel (`src/Server/Budgeting/Domain.fs`). `validateTargetsMonth` in `src/Server/Validation.fs` lehnt "last" ab, `validateBudgetMonth` bleibt fürs Lesen.
- `src/Server/Api.fs`: die Logik von `previewTargets` und `applyTargets` steht jetzt in `previewTargetsWith` und `applyTargetsWith`, damit sie mit Fake-Port testbar ist. Live angeschlossen sind sie nur in `budgetApi` (`livePort`, `DateTime.Today`, `DateTime.UtcNow`). Diese Verdrahtung belegen die Zeilen der Negativtabelle: ohne Prüfung käme dort "Kein YNAB-Token" statt des Grunds.
- Client: `src/Client/Components/Budget/View.fs` (nur Umschalter und Knopf "Targets anwenden"), `src/Client/Components/Budget/Types.fs`.

Abweichungen und Einschränkungen:
- Die Negativtabelle verlangt genau eine Zeile je Endpoint. Die Zeilen von `previewTargets` und `applyTargets` prüfen deshalb jetzt "last" statt "kein-monat". Der ungültige Schlüssel ist nur noch im Unit-Test von `validateTargetsMonth` abgedeckt.
- Über die Aufgabe hinaus verweigert auch der reine Übergang `openTargets` im letzten Monat das Öffnen (Client-Test). So löst keine Nachricht eine Vorschau aus, die der Server ablehnen müsste.
- Das Lesen der Settings läuft bei `previewTargets` und `applyTargets` jetzt vor der Monatsprüfung, wie schon bei `moveMoney`. Beobachtbar ändert sich dadurch nichts.
- Rot gesehen habe ich die .NET-Tests. Die drei neuen Browser-Wächter liefen nur gegen den fertigen Code, ihre Gegenprobe steht aus.
- Keine ADR: die Entscheidung stammt von Roman, sie steht in der Task und im README-Delta.
