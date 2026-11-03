# Idee: Von Actual Budget abschauen

**Status:** Ideen, keine Tasks. Nichts davon ist modelliert oder beschlossen.
**Erfasst:** 2026-11-02
**Kontext:** Vergleich von BudgetBuddy und Actual (Version 26.10.0, Code gelesen, nicht gestartet). Ergebnis: BB wird nicht durch Actual ersetzt. Pfade unten beziehen sich auf das Actual-Repo.

---

## Ideen

### 1. Schedule-Abgleich für die erwartete Einnahme

Actual verknüpft eine importierte Buchung über eine Regel (`link-schedule`) mit dem Schedule, statt sie doppelt zu führen. Das Datum darf 2 Tage abweichen, ein "ungefährer" Betrag 7,5 %. Das passt zum Problem aus der Vision, dass geplante Transaktionen in YNAB doppelt auftauchen. Das Einlösen der **erwarteten Einnahme** beim Comdirect-Import könnte dieselbe Logik nutzen.

Fundstellen: `packages/loot-core/src/server/schedules/app.ts`, `packages/docs/docs/schedules.md`.

### 2. Template-Grammatik als Vorlage für Targets in BB

Die Notiz-Syntax lohnt sich nicht, die Bedeutung schon: Deckel (`up to`), Priorität, Probelauf vor dem Anwenden. Das spart Modellierung, sobald BB Targets selbst führt (Stufe 3). Das kleine Goal-Modell aus `docs/idea-ynab-replacement.md` bleibt der Maßstab, übernommen wird nur, was Roman braucht.

Fundstellen: `packages/loot-core/src/server/budget/goal-template.pegjs`, `budget/dry-run-category-template` in `packages/loot-core/src/server/budget/app.ts`.

### 3. Regeln aus Verhalten

Actual legt Regeln automatisch an, wenn man Payees umbenennt oder Buchungen kategorisiert. Regeln können außerdem Splits setzen (`set-split-amount`) und in Stufen laufen. Berührt `ledger-hg5m5` (Regel-Angebot nach Kategorie-Änderung) und `banking-import-wp7js` (Split im Import).

Fundstellen: `packages/loot-core/src/server/rules/action.ts`, `packages/docs/docs/budgeting/rules/index.md`.

### 4. Undo für Budget-Bewegungen

Altes und neues Zugewiesen stehen schon in `budget_movements`, das Rückgängigmachen wäre also eine neue Bewegung mit vertauschten Werten. Die Historie bleibt dabei append-only. Bei Actual hängt Undo als Wrapper an den Budget-Aktionen, ist aber auf 20 Schritte im Speicher begrenzt.

Fundstellen: `packages/loot-core/src/server/undo.ts`, `packages/loot-core/src/server/budget/app.ts`.

### 5. Actual als Rechenprobe

Einen YNAB-Export in eine Actual-Instanz importieren. Das liefert unabhängige Vergleichswerte für BBs eigene Verfügbar-Rechnung und prüft nebenbei den Notausgang. Der Importer übernimmt Budgetbeträge, Schedules und Flags (als Tags), aber keine Targets.

Fundstelle: `packages/loot-core/src/server/importers/ynab5.ts`.

---

## Bewusst nicht

CRDT und Local-first: das löst ein Problem, das BB ohne Konfliktmodell nicht hat.

---

## Wann die Ersetzen-Frage wieder aufmachen

- Stufe 3 bleibt liegen, obwohl die Budget-Grundlage steht.
- Die ING-Frage und die eigene Rechnung werden zusammen größer als der Umstieg auf BB als Importer vor Actual.
