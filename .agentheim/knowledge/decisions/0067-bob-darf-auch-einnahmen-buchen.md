---
id: 0067
title: Bob darf auf dem Quick-Add-Konto auch Einnahmen buchen
scope: ledger
status: accepted
date: 2026-10-28
supersedes: []
superseded_by: []
related_tasks: [ledger-b0b7q]
related_adrs: [0004, 0066]
related_research: []
---

# ADR 0067: Bob darf auf dem Quick-Add-Konto auch Einnahmen buchen

## Context
ADR 0066, Entscheidung 5, nennt "nur Ausgaben aufs Quick-Add-Konto", und die Folgen sprechen
von "höchstens Barausgaben bis zur Obergrenze". Die Task ledger-b0b7q und Romans Verfeinerung
vom 2026-10-27 führen dagegen `direction` mit den Werten `outflow` und `inflow` auf. Quick Add
selbst bucht beide Richtungen (`IsOutflow` im Formular), und `addCashExpense` trägt den Betrag
vorzeichenbehaftet ein.

## Decision
Bob darf über `POST /bot/transactions/preview` und den Commit auch Einnahmen buchen
(`direction: "inflow"`, positiver Betrag). Das ergänzt ADR 0066, Entscheidung 5.

Unverändert eng bleibt:
1. Das Konto ist immer das Quick-Add-Konto (ADR 0004), die Eingabe hat kein Konto-Feld.
2. Dieselbe Betragsobergrenze (`BOT_MAX_AMOUNT_EUR`, Vorgabe 500 EUR) gilt für beide Richtungen.
3. Es gibt keine Lese-Route für Buchungen oder Salden.
4. Jede Buchung trägt die Herkunft `Bob`, Einnahmen wie Ausgaben.

## Consequences
- Ein kompromittierter Bob kann Barausgaben und Bareinnahmen bis zur Obergrenze anlegen, beides
  sichtbar an der Herkunft `Bob`.
- Der Satz "höchstens Barausgaben" in den Folgen von ADR 0066 gilt nicht mehr.
