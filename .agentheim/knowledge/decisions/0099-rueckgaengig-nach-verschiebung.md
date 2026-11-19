---
id: 0099
title: Rückgängig nach einer Verschiebung als Verschiebung in die Gegenrichtung
scope: budgeting
status: proposed
date: 2026-11-19
supersedes: []
superseded_by: []
related_tasks: [budgeting-y1kkr, budgeting-gtrgk, ledger-vf5h6]
related_adrs: [0055, 0069, 0083, 0084]
related_research: []
---

# ADR 0099: Rückgängig nach einer Verschiebung als Verschiebung in die Gegenrichtung

## Context

Mit Regel R3 (design-system-ytgnc) fällt am Handy der Toast weg; das Ergebnis einer Verschiebung steht im Sheet „Geld“ in
einer Zeile über dem Knopf (budgeting-gtrgk). Roman hat bei der Freigabe der Skizze ledger-vf5h6 (Version 9, 2026-11-19)
entschieden, dass es dort „Rückgängig“ gibt, und zwar als Verschiebung in die Gegenrichtung.

Geld ist in BB ein Ereignis (ADR 0083). Eine Verschiebung ist ein Entscheidungsereignis (`MoneyMoved`, bei Zu verteilen
als Gegenseite `AssignmentChangedInBb`) mit Rückschreib-Ergebnissen danach. Verschieben rechnet relativ (ADR 0069), YNAB
gewinnt bei Abweichung (ADR 0055), jeder Befehl beobachtet den Monat vorher (ADR 0084).

## Decision

Von Roman entschieden (2026-11-19):

1. **Rückgängig ist eine neue Verschiebung in die Gegenrichtung**, ein neues Entscheidungsereignis. Das ursprüngliche
   Ereignis wird weder gelöscht noch zurückgezogen.
2. **Ort:** die Ergebniszeile über dem Hauptknopf im Sheet „Geld“.

Vorgeschlagen beim Verfeinern (tactical-modeler, budgeting-y1kkr), bis zur Antwort von Roman offen:

3. **Relativ:** derselbe Betrag zurück, über den bestehenden Befehl Verschieben mit vertauschter Quelle und vertauschtem
   Ziel, an den Monat der ursprünglichen Verschiebung, mit frischer Befehls-Id. Kein neuer Ereignistyp, kein neues Feld,
   Server unverändert.
4. **Wege:** Holen, Abgeben und der Fehlbetrag-Knopf. Nicht das Feld Zugewiesen, nicht Targets anwenden.
5. **Nur wenn jede Zeile geschrieben ist.** Bei fehlgeschlagener Zeile gilt „Erneut senden“, bei „In YNAB geändert“ oder
   „in BB zurückgezogen“ gibt es kein Rückgängig.
6. **Lebensdauer:** nur die letzte Verschiebung; das Angebot endet mit dem nächsten Senden, Kategorie- oder Monatswechsel,
   Schließen des Sheets, Verlassen der Seite und Neuladen. Es lebt nur im Client.

## Offene Fragen an Roman

- Zeigt die Historie ein Rückgängig als solches (Verweis `undoes` im Ereignis), oder reichen zwei normale Bewegungen?
  Empfehlung: zwei normale Bewegungen, Verweis später als optionales Feld.
- Endet das Angebot schon beim Moduswechsel (Zuweisen, Holen, Abgeben)? Empfehlung: nein, erst beim nächsten Senden.
- Kein Rückgängig beim Feld Zugewiesen? Empfehlung: ja, keins.
- Relativ statt absolut? Empfehlung: relativ.

## Consequences

- Kein Eingriff in den Ereignisstrom und keine Rücknahme-Semantik; Rückschreiben, Beobachten und Erneut senden gelten
  unverändert auch für das Rückgängig.
- Hat sich zwischen Verschiebung und Rückgängig in YNAB oder BB etwas geändert, bleibt diese Änderung erhalten; der
  Endstand ist dann nicht der alte Stand. Bei der kurzen Lebensdauer des Angebots ist das selten.
- Die Historie zeigt Hin- und Rückweg als zwei Bewegungen, solange Frage 1 nicht anders entschieden ist.

## Alternatives considered

- **Zurückziehen des ursprünglichen Ereignisses:** verworfen von Roman; Geld-Ereignisse werden nicht geändert (ADR 0083),
  und eine schon nach YNAB geschriebene Zeile ist keine offene Rückschreibung, die man zurückziehen könnte.
- **Absolut den alten Stand herstellen:** überschriebe Änderungen aus YNAB dazwischen (gegen ADR 0055) und bräuchte eine
  eigene Prüfung „Stand hat sich geändert“.
- **Herkunft „Rückgängig“ mit Verweis im Ereignis:** möglich, aber im ersten Schnitt nicht nötig; offene Frage oben.
