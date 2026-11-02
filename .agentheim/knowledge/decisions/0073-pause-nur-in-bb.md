---
id: 0073
title: Die Pause eines Targets liegt in BB, je Kategorie und Monat, und wird nicht nach YNAB gezogen
scope: budgeting
status: accepted
date: 2026-11-02
supersedes: []
superseded_by: []
related_tasks: [budgeting-v4ht9, budgeting-t6p8z, budgeting-c5w2n, budgeting-m3x7f]
related_adrs: [0063, 0065, 0072]
related_research: [ynab-api-targets-zuweisung-2026-10-25]
---

# ADR 0073: Pause nur in BB

## Context
Roman will Targets für einen Monat aussetzen. ADR 0065 Punkt 4 überspringt pausierte Targets
bisher nur, wenn YNAB sie meldet (`goal_snoozed_at`). Dieses Feld ist über die API nicht
schreibbar (budgeting-h2zfn). Die Regel "BB entscheidet, YNAB wird nachgezogen" lässt sich
hier also nicht erfüllen.

## Decision
1. **Die Pause liegt in BB, je Kategorie und Monat.**
2. **Sie gilt, wenn sie auf einer der beiden Seiten steht:** in BB gesetzt oder in YNAB über
   `goal_snoozed_at`. Eine Pause aus YNAB beachtet BB weiter und hebt sie nicht auf.
3. **Sie endet mit dem Monat.** Für den Folgemonat gilt sie nicht mehr, und Pausen werden nicht
   übertragen.
4. **Pausierte Targets zählen nicht** für `fehlt`, die Vorschau und Targets anwenden. Sie
   sperren auch nichts beim freien Betrag (Verfügbar minus offener Fehlbetrag).
5. **Bewusste Ausnahme von "BB entscheidet, YNAB wird nachgezogen".** BB schreibt für die
   Pause nichts nach YNAB. Pausieren geht im aktuellen und in künftigen Monaten (ADR 0074).

## Consequences
### Positive
- Monatsausnahmen sind in BB möglich, obwohl die API keinen Schreibweg hat.
- Punkt 2 hält das Verhalten für Pausen aus YNAB gleich, ADR 0065 Punkt 4 wird erweitert.

### Negative
- YNAB zeigt das Target weiter als offen, einschließlich `goal_under_funded`. Wer YNAB öffnet,
  sieht dort einen anderen Eindruck als in BB. Das ist eine bewusste und erklärte Abweichung,
  weil YNAB weiter die Quelle des Fehlbetrags bleibt.
- BB muss den YNAB-Fehlbetrag bei pausierten Targets verwerfen und hält erstmals einen
  Target-Zustand, den YNAB nicht kennt.
- Eine Pause aus YNAB lässt sich nur in YNAB aufheben.

### Neutral
- Ob und wie die Pause aufgezeichnet wird, klärt budgeting-t6p8z. Der Prototyp schreibt sie in
  die Historie.
- Beim Wechsel zu BB als führendem System muss die Pause mitgehen.

## Alternatives considered
- **Pause nach YNAB ziehen.** Nicht möglich: `goal_snoozed_at` ist nicht schreibbar.
- **Target für den Monat entfernen und danach neu anlegen.** Verworfen: Tag und Datum gingen
  verloren (ADR 0072).
- **Pause über den Monat hinaus, bis zum Aufheben.** Verworfen: Das Konzept will eine
  Monatsausnahme, die von selbst endet.

## References
- ADR 0063, 0065, 0072
- docs/ux/budget/konzept.md, Abschnitt Targets
- research/ynab-api-targets-zuweisung-2026-10-25.md
