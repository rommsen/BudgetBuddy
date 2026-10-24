---
id: ADR-0058
title: Split-Schreibweg nach YNAB: bestehende Split-Buchungen werden gelöscht und neu angelegt, weil die API das Ändern von subtransactions ausschließt
scope: ledger
status: proposed
date: 2026-10-24
related_tasks: [ledger-mas5g, ledger-kxme4, ledger-pafkp]
related_adrs: [0006, 0028, 0046, 0054, 0055]
---

# ADR-0058: Split-Schreibweg nach YNAB: Löschen und Neuanlegen

## Context

ADR 0055 ließ offen, ob YNAB die Splits einer bestehenden Buchung per API ändern lässt. Der
Spike ledger-mas5g hat die offizielle OpenAPI (https://api.ynab.com/papi/open_api_spec.yaml,
Version 1.87.0) gelesen. Zum Feld `subtransactions` (Schema `SaveTransactionWithOptionalFields`,
das PUT `/plans/{plan_id}/transactions/{transaction_id}` und PATCH `/plans/{plan_id}/transactions`
gemeinsam nutzen) steht dort wörtlich: "Updating `subtransactions` on an existing split
transaction is not supported and will return an error." Zu `category_id`: "If an existing
transaction is a split, the `category_id` cannot be changed." Zu Datum und Betrag: "Split
transaction dates cannot be changed and if a different date is supplied it will be ignored."
und "Split transaction amounts cannot be changed and if a different amount is supplied it will
be ignored." Eine bestehende Split-Buchung lässt sich per Update also weder in den Zeilen
ändern noch aufheben (aufheben hieße, eine Kategorie zu setzen, was ebenfalls ausgeschlossen
ist). Ob eine unaufgeteilte Buchung per Update aufgeteilt werden kann, sagt die Spec nicht.

## Decision (vorgeschlagen)

1. Für das Ändern und Aufheben bestehender Split-Buchungen gibt es genau einen API-Weg:
   `DELETE /plans/{plan_id}/transactions/{transaction_id}`, danach `POST
   /plans/{plan_id}/transactions` mit der gleichen `import_id`, dem gleichen Datum, Betrag,
   Konto, Payee, Memo, Flag, `cleared` und `approved` wie vorher und den neuen
   `subtransactions` (oder einer `category_id` beim Aufheben). Reihenfolge Delete zuerst,
   weil dieselbe `import_id` auf demselben Konto sonst mit 409 abgewiesen wird.
2. Das Aufteilen einer unaufgeteilten Buchung wird zuerst als PUT mit `category_id: null` und
   `subtransactions` versucht. Nimmt YNAB das an, ist es der Weg für diesen Fall. Lehnt es ab
   oder ignoriert es, gilt Punkt 1.
3. Der Schreibweg ist eine eigene benannte Funktion (`YnabClient.replaceSplitTransaction`),
   nicht `updateCategory`. Der Fehlschlag-Zustand aus ADR 0055 bekommt zusätzlich den Fall
   "in YNAB gelöscht, Neuanlage fehlgeschlagen" (BB hält die Daten, "Erneut senden" legt an).
4. Vor dem Bau läuft ein Probelauf gegen ein Wegwerf-Budget (Liste im Outcome von
   ledger-mas5g). Fällt eine der Annahmen (import_id nach Delete wiederverwendbar, `approved`
   und `cleared` übernehmbar, Gegenbuchungen verhalten sich wie beschrieben), wird dieser ADR
   überarbeitet, bevor ledger-kxme4 gebaut wird.
5. Nach einem Ersatz ändern sich die YNAB-Id der Buchung, die Subtransaction-Ids (ADR 0046)
   und die Ids der Übertrags-Gegenbuchungen. Der Spiegel muss die alte Buchung als gelöscht
   und die neue als zugehörig erkennen (über `import_id`), die Verknüpfung der Split-Überträge
   wird nach dem nächsten Abruf neu gezogen.

## Consequences

- Positiv: BB bleibt führend, YNABs Zahlen stimmen nach dem Schreiben, es entsteht keine
  Abweichung zwischen BB und YNAB.
- Negativ: Zwei Aufrufe mit einem Fenster, in dem die Buchung in YNAB fehlt. Reconciled
  Buchungen (`cleared: reconciled`) sind riskant, siehe Outcome. Ids ändern sich, und
  Übertrags-Gegenbuchungen werden von YNAB gelöscht und neu erzeugt (Annahme, nicht belegt).
- Verworfen: Splits nur in BB halten. Widerspricht ADR 0054 (YNAB nachziehen) und lässt
  die Kategoriezahlen in YNAB falsch.

## Note on ADR numbering

Minted provisionally as ADR-0059 in its worker worktree. A sibling task's ADR already claimed that number (or the guess overshot the true count) by the time this task's conductor finalized numbering at squash-merge integration (`lib/adr-allocation.mjs`'s `finalizeAdrNumbering`, ADR-0058) — this ADR was renumbered to **ADR-0058**, the true next-free number on `main` at that moment. No content besides this identity changed.
