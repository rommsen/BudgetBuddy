---
id: 0018
title: Konten holt der Spiegel je Lauf vollständig, das server_knowledge gilt nur für Transaktionen
scope: ynab-sync
status: accepted
date: 2026-09-06
supersedes: []
superseded_by: []
related_tasks: [ledger-m8r2v]
related_adrs: [0014, 0015]
related_research: []
---

# ADR 0018: Konten holt der Spiegel je Lauf vollständig, das server_knowledge gilt nur für Transaktionen

## Context
ADR 0014 beschreibt einen Spiegel-Lauf als zwei Requests: Transaktions-Delta über
`last_knowledge_of_server` plus ein Accounts-Delta, und "der letzte `server_knowledge` wird
pro Budget persistiert".

Beim Bauen zeigt sich, dass beide Endpoints ihre **eigene** Delta-Marke führen. Eine
gemeinsame Marke für beide ist falsch. Führt man zwei Marken, entsteht ein zweites Problem:
ein Konten-Delta lässt unveränderte Konten weg, eine Transaktions-Beobachtung kann aber auf
genau so ein Konto zeigen (Buchung geändert, Konto nicht). Der Lauf müsste dann entweder
scheitern oder ein Konto mit erfundenem Namen anlegen.

## Decision
- Der Spiegel holt die Konten **je Lauf vollständig**, ohne `last_knowledge_of_server`.
- Persistiert wird genau **eine** Marke je Budget: das `server_knowledge` des
  **Transaktions**-Abrufs (`ledger_mirror_server_knowledge_<budgetId>` in den Settings).
- `YnabClient.getAccountsDelta` nimmt den Knowledge-Parameter trotzdem entgegen; der Lauf
  übergibt bewusst `None`. Der Parameter bleibt, damit ein späterer Nutzer der Funktion
  nicht die Signatur ändern muss.
- Die Auflösung YNAB-Konto → Kassenbuch-Konto ist damit **total**: zeigt eine Beobachtung
  trotzdem auf ein unbekanntes Konto, bricht der Lauf mit einer benennenden Fehlermeldung
  ab, statt ein Konto zu erfinden.

## Consequences
### Neutral
- Die Rate-Limit-Rechnung aus ADR 0014 bleibt unverändert: ein Lauf = zwei Requests. Ein
  Vollabruf der Konten kostet genauso viel wie ein Konten-Delta.
- Ein in YNAB gelöschtes Konto (`deleted: true`) gilt im Kassenbuch als **geschlossen**;
  gelöscht wird auch hier nichts.

### Negative
- Die Konten-Antwort ist etwas größer als ein Delta. Bei der Größenordnung eines privaten
  YNAB-Budgets (einstellige bis niedrige zweistellige Kontenzahl) ist das ohne Belang.

## Alternatives considered
- **Zwei Marken (Konten und Transaktionen getrennt) persistieren:** korrekt gegenüber der
  API, löst aber das Kernproblem nicht — die Beobachtung auf ein nicht mitgeliefertes Konto
  bliebe möglich, und das Kassenbuch müsste einen Platzhalter-Kontonamen erfinden.
- **Konto bei Bedarf aus der Beobachtung anlegen:** die Beobachtung trägt nur die
  `account_id`, keinen Kontonamen. Ein Konto namens `"YNAB-Konto <guid>"` im Kassenbuch
  widerspricht dem Anspruch, dass die Historie ohne Nachschlagen lesbar ist (ADR 0014).
