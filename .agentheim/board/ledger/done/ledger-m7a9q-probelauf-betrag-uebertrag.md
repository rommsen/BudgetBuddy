---
id: ledger-m7a9q
title: "Probelauf: Betrag eines Übertrags per Update in YNAB ändern"
status: done
type: spike
context: ledger
created: 2026-11-09
completed:
depends_on: []
blocks: []
tags: [ynab, betrag, uebertrag]
related_adrs: [0092, 0058]
related_research: []
prior_art: [ledger-m7mye]
---

## Why
ADR 0092 Punkt 4 sperrt den Betrag von Überträgen (ganz oder als Split-Zeile), weil nicht geprüft ist, ob YNAB die Gegenbuchung beim Update des Betrags mitzieht. Die Folgen-Zeile des ADR nennt einen Probelauf gegen das Test-Budget; danach kann die Sperre fallen.

## What
Probelauf gegen das Wegwerf-Budget "Test": PUT `amount` auf einen Übertrag zwischen zwei Budget-Konten und auf eine Split-Buchung mit Übertrags-Zeile (Ersatz). Ergebnis als Nachtrag zu ADR 0092.

## Acceptance criteria
- [ ] Probelauf dokumentiert (Aufruf, Antwort, Zustand der Gegenbuchung danach).
- [ ] Nachtrag ADR 0092 mit der Entscheidung, ob `amountTransferReason` und `amountSplitTransferReason` fallen.

## Notes
Stop-loss: Ist mitten im Probelauf die Antwort schon klar und die Folge billig (etwa YNAB zieht die Gegenbuchung
sichtbar mit oder lehnt das Update eindeutig ab), das Ergebnis festhalten und aufhören.
Nur gegen das Wegwerf-Budget "Test", nie gegen Romans Budget; Aufruf hinter `RUN_INTEGRATION_TESTS`.

## Outcome

Probelauf ausgeführt am 2026-11-10 gegen das Wegwerf-Budget "Test" (zwei offene Budget-Konten A und B, eine Kategorie), API v1, dreimal nacheinander mit gleichem Ergebnis. Der Probelauf steht als Integrationstest in `src/Tests/YnabIntegrationTests.fs` ("Probelauf: YNAB zieht die Gegenbuchung beim Betrag eines Übertrags mit (Budget Test)"), läuft nur mit `RUN_INTEGRATION_TESTS=true` in `.env` und nutzt BBs echte Schreibwege (`createReplacement`, `updateAmount`, `replaceSplitTransaction`, `deleteTransaction`). Gelesen wird roh per `GET /budgets/{id}/transactions/{tid}`. Alles Angelegte wird im `finally` gelöscht; eine Abfrage der Buchungen der letzten drei Tage im Budget "Test" danach war leer. Sperre im Code nicht angefasst: sie aufzuheben heißt, in BB auch den Eintrag der Gegenbuchung nachzuziehen, das ist mehr als eine kleine Änderung (Follow-up ledger-t4rk8).

Ergebnisse (Beträge in Milliunits):

| # | Aufruf | Antwort | Zustand danach |
|---|---|---|---|
| 1 | POST Übertrag A nach B, -12340 (`payee_id` = Übertrags-Payee von B) | 201 | A -12340, Gegenbuchung auf B +12340, verknüpft über `transfer_transaction_id` |
| 2 | PUT `{"transaction":{"amount":-20000}}` auf A (`updateAmount`) | 200 | A -20000, **Gegenbuchung +20000**, gleiche Id, Verknüpfung bleibt |
| 3 | PUT `amount` 7500 auf die Gegenbuchung (Seite B) | 200 | Gegenbuchung +7500, **A -7500**, Ids bleiben |
| 4 | POST Split auf A, -15000: Kategorie -10000, Übertrags-Zeile nach B -5000 | 201 | Gegenbuchung der Zeile auf B +5000 (ihr `transfer_transaction_id` ist `null`, sie zeigt nicht auf die Subtransaction) |
| 5 | Ersatz (DELETE und POST, ADR 0058) mit -25000: Kategorie -10000, Übertrag -15000 | 200, 201 | neue Buchungs-Id, alte Gegenbuchung gelöscht, **neue Gegenbuchung +15000** mit neuer Id |
| 6 | PUT `amount` 3000 auf die Gegenbuchung der Split-Zeile | **400** `"the amount or date cannot be changed because the payee or category is not valid"` | nichts geändert: Split-Zeilen und Gegenbuchung (+15000) wie vorher |

Nachtrag für ADR 0092 (vom Conductor als `## Nachtrag 2026-11-10` an `.agentheim/knowledge/decisions/0092-buchung-loeschen-und-betrag-aendern-in-bb.md` anzuhängen, die Rückfrage an Roman wie beim Rest des ADR):

```
## Nachtrag 2026-11-10, Probelauf Betrag eines Übertrags (ledger-m7a9q)

Probelauf gegen das Budget "Test" (Integrationstest "Probelauf: YNAB zieht die Gegenbuchung beim
Betrag eines Übertrags mit (Budget Test)" in `src/Tests/YnabIntegrationTests.fs`, hinter
`RUN_INTEGRATION_TESTS`):

- PUT `amount` auf einen Übertrag zwischen zwei Budget-Konten: 200, YNAB zieht die Gegenbuchung mit
  (Betrag mit umgekehrtem Vorzeichen, gleiche Id, Verknüpfung bleibt). Das gilt von beiden Seiten.
- Ersatz (DELETE und POST, ADR 0058 Punkt 2) einer Split-Buchung mit Übertrags-Zeile und neuem
  Betrag: die alte Gegenbuchung ist gelöscht, die neue trägt den neuen Zeilenbetrag und eine neue Id
  (wie ADR 0058 Probelauf 5).
- PUT `amount` auf die Gegenbuchung einer Split-Übertrags-Zeile: 400 "the amount or date cannot be
  changed because the payee or category is not valid"; nichts ändert sich.

Entscheidung: Punkt 4 fällt für den Betrag. `amountTransferReason` fällt für einen ganzen Übertrag
(BB schreibt den Betrag per Update und zieht den Eintrag der Gegenbuchung in BB mit), außer für die
Gegenbuchung einer Split-Übertrags-Zeile: deren Betrag ändert sich nur über die Aufteilung der
Ursprungsbuchung, die Sperre bleibt dort mit eigenem Grund. `amountSplitTransferReason` fällt: der
Betrag einer Split-Buchung mit Übertrags-Zeile geht wie jeder Split als Ersatz, die Verknüpfung der
Split-Überträge (ledger-h6mer) läuft danach neu. Umsetzung in ledger-t4rk8.
```
