---
id: 0065
title: Budget-Zustand in Stufe 1 live aus YNAB, Budget-Bewegungen als append-only Aufzeichnung in BB
scope: budgeting
status: accepted
date: 2026-10-26
supersedes: []
superseded_by: []
related_tasks: [budgeting-k7wmd, budgeting-h2zfn]
related_adrs: [0013, 0050, 0055, 0063]
related_research: [ynab-api-targets-zuweisung-2026-10-25]
---

# ADR 0065: Budget-Zustand live aus YNAB, Budget-Bewegungen in BB

## Context
ADR 0063 macht das Budget zur nächsten Fähigkeit und setzt die Formel "BB führt, YNAB wird
nachgezogen". Stufe 1 (Budget-Seite, Targets anwenden) braucht zwei Antworten: woher die
Beträge kommen und wo festgehalten wird, was BB zugewiesen hat. Die Vision nennt als
Scheiter-Kriterium, dass BBs und YNABs Verfügbar sich unerklärt widersprechen.
Der Spike (budgeting-r4tkq) zeigt: GET /months/{month} liefert Zu verteilen, Zugewiesen,
Aktivität, Verfügbar und den Fehlbetrag je Kategorie. Zuweisen geht absolut per
PATCH /months/{month}/categories/{id}, eine Kategorie je Aufruf. YNAB liefert nicht, wer
wann was zugewiesen hat.

## Decision
1. **Beträge live aus YNAB.** Zu verteilen, Zugewiesen, Aktivität, Verfügbar und Fehlbetrag
   werden bei jedem Öffnen aus YNAB gelesen und nicht in BB gespeichert (wie "Available
   live", ADR 0055). Aktivität kommt in Stufe 1 ebenfalls von YNAB, nicht aus dem Kassenbuch.
   Wann BB selbst rechnet, bleibt offen.
2. **Budget-Bewegungen ab Stufe 1 in BB.** Targets anwenden hält je Kategorie eine
   unveränderliche Bewegung fest (Zeitpunkt, Herkunft, Monat, Kategorie, altes und neues
   Zugewiesen). Die Tabelle budget_movements ist getrennt vom Kassenbuch-Ereignis-Log
   (ADR 0013); SQLite-Trigger weisen UPDATE und DELETE ab. Rückschreib-Ergebnisse
   (geschrieben, fehlgeschlagen, in YNAB geändert) sind eigene angehängte Zeilen in
   budget_movement_results. Der Stand einer Bewegung ist ihr jüngstes Ergebnis.
3. **Reihenfolge wie ADR 0050:** erst alle Bewegungen festhalten (eine Transaktion), dann
   nacheinander senden. Ein Fehlschlag trägt als Baseline das alte Zugewiesen. Kein
   Rollback. Erneut senden ist idempotent, weil der Wert absolut gesetzt wird. Steht YNAB
   weder auf Baseline noch auf Ziel, gewinnt YNAB und die Zeile wird als "in YNAB geändert"
   geschlossen. Steht YNAB schon auf dem Ziel, wird nichts gesendet und die Zeile ist
   geschrieben.
4. **Betrag je Kategorie ist altes Zugewiesen plus Fehlbetrag von YNAB.** BB rechnet in
   Stufe 1 keine Targets. Pausierte Targets (goal_snoozed_at) werden übersprungen. Beträge
   müssen auf 10 Milliunits liegen, sonst lehnt BB ab (YNAB rundet 12345 still auf 12350).
5. **Rate-Limit:** sequentielle Einzel-PATCHes, Abbruch beim ersten 429, Rest als
   fehlgeschlagen mit Grund "Rate-Limit". Kein Zähler.
6. **Übernehmen vergleicht gegen die gezeigte Vorschau.** Der Client schickt die Vorschau
   mit; der Server liest den Monat neu und schreibt nur, wenn Zeilen (Kategorie, altes und
   neues Zugewiesen) und Zu verteilen übereinstimmen, sonst liefert er die neue Vorschau.
   Die Anwendung läuft unter einer Sperre je Monat (nicht unter der Kassenbuch-Sperre
   WriteGuard, weil sie keine Buchungen berührt); eine zweite Anwendung wartet und sieht
   dann ihre Vorschau als überholt.

## Consequences
### Positive
- BB kann YNAB in Stufe 1 nicht unerklärt widersprechen, weil BB keine eigenen Beträge hält.
- Die Budget-Historie (Stufe 2) beginnt nicht leer.

### Negative
- "BB führt" gilt in Stufe 1 nur für die Bewegungen. Die Beträge bleiben YNABs.
- Rund 100 PATCHes pro Lauf gegen etwa 200 Anfragen pro Stunde und Token. Das Limit kann
  in einer Stunde mit mehreren Läufen reißen.
- Zuweisungen, die Roman direkt in YNAB macht, tauchen in den Bewegungen nicht auf.

### Neutral
- Die Naht ledger zu budgeting (Aktivität) ist für Stufe 1 nicht gebaut.

## Alternatives considered
- **Beträge in BB persistieren und mit YNAB abgleichen.** Verworfen: schafft die
  Widerspruchsquelle, die die Vision als Scheiter-Kriterium nennt, und braucht einen
  Abgleich ohne Gegenwert in Stufe 1.
- **Bewegungen erst in Stufe 2 aufzeichnen.** Verworfen: die ersten Monate der Historie
  wären leer und nicht nachholbar.
- **Bewegungen im Kassenbuch-Ereignis-Log.** Verworfen: das Log hängt an Buchungen, das
  Budget hat eigene Sprache und einen eigenen Context.
- **Targets in BB berechnen.** Verworfen: YNAB liefert den Fehlbetrag, Targets bleiben bis
  Stufe 3 YNAB-geführt.

## References
- ADR 0013, 0050, 0055, 0063
- research/ynab-api-targets-zuweisung-2026-10-25.md
