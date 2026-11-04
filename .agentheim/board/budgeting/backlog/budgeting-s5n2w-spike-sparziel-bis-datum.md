---
id: budgeting-s5n2w
title: "Spike: Lässt sich über die YNAB-API ein einmaliges Sparziel bis zu einem Datum anlegen und ändern?"
status: backlog
type: spike
context: budgeting
created: 2026-11-04
completed:
depends_on: []
blocks: []
tags: [budget, target, sparziel, ynab, spike]
related_adrs: [0072]
related_research: [ynab-api-targets-zuweisung-2026-10-25, ynab-reihenfolge-monate-target-schreiben-2026-11-02]
prior_art: [budgeting-r4tkq, budgeting-q7m2c, budgeting-xp46a]
---

## Why
Roman (2026-11-04): "Ich möchte es schaffen, 18.000 Euro zu sparen bis zum 01.01.2030." BB kann heute nur NEED wöchentlich, monatlich und jährlich anlegen (ADR 0072 Punkt 2), weil `goal_frequency` der einzige belegte Weg zum Anlegen ist. Ein einmaliges Sparziel ist in YNAB "Guthaben bis zu einem Datum" (TBD) oder ein NEED ohne Wiederholung (cadence 0 mit Datum). TBD ist laut Spec nicht schreibbar. Ob ein NEED ohne Wiederholung entsteht, wenn auf einer Kategorie ohne Target nur `goal_target` und `goal_target_date` ohne `goal_frequency` gesendet werden, hat niemand geprüft.

## What
Nur gegen das Budget "Test", abgesichert durch einen Wächter, der jede Schreibung mit einer anderen Budget-Id verweigert. Romans Budget wird nicht berührt. Kein Produktionscode.

1. Kategorie ohne Target: `PATCH` mit `goal_target` und `goal_target_date` (Datum mehrere Jahre voraus), ohne `goal_frequency`. Was entsteht (`goal_type`, `goal_cadence`, `goal_cadence_frequency`, `goal_needs_whole_amount`, `goal_under_funded`, `goal_months_to_budget`)?
2. Dasselbe in zwei Aufrufen: erst nur `goal_target`, dann nur `goal_target_date`.
3. Auf dem entstandenen Ziel: Betrag ändern, Datum ändern, Datum entfernen. Bleibt die Form?
4. Ein vorhandenes TB oder TBD im Budget Test: nur `goal_target` und nur `goal_target_date` senden. Bleibt die Art?
5. Zum Vergleich: "jährlich mit Datum 2030" als Ersatz. Welche Monatsrate rechnet YNAB?

**Stop-loss:** Wenn sich mitten im Spike zeigt, dass die Antwort schon bekannt und billig ist, wird sie festgehalten und der Spike endet dort.

## Acceptance criteria
- [ ] Bericht unter `.agentheim/knowledge/research/` beantwortet die Fragen mit den beobachteten Antworten, ohne Tokens, Kontodaten oder echte Kategorienamen aus Romans Budget.
- [ ] Der Bericht empfiehlt in einem Satz, ob BB ein einmaliges Sparziel anlegen kann und mit welchem Aufruf.
- [ ] Das Budget Test ist nach dem Spike dokumentiert (was geändert blieb).

## Notes
Folge je nach Ergebnis: eine Feature-Task "Sparziel anlegen" im Tab Target, oder die Frage aus Stufe 3, ob BB Targets selbst führt. Hängt mit budgeting-d7r4k (Datum je Rhythmus) zusammen.
