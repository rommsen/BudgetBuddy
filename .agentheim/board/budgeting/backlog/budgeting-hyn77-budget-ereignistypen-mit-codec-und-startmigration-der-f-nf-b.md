---
id: budgeting-hyn77
title: "Budget-Ereignistypen mit Codec und Startmigration der fünf Budget-Tabellen nach events, Schreibwege und Leser umgestellt, Verhalten unverändert"
status: backlog
type: feature
context: budgeting
created: 2026-11-05
completed:
depends_on: [ledger-k9tm2]
blocks: [budgeting-qm5x9, budgeting-dgrp7, budgeting-eh8xe]
tags: [event-sourcing, migration, fuehrung]
related_adrs: [0013, 0065, 0079, 0080, 0083, 0084]
related_research: []
prior_art: [budgeting-h2zfn, budgeting-afd8h, budgeting-c5w2n, budgeting-t6p8z, budgeting-k8wq3]
---

## Why
ADR 0084 legt die Budget-Ereignisse in die gemeinsame Tabelle `events` (ADR 0083). Bevor BB im
Budget führt, müssen die heutigen Aufzeichnungen dort liegen, mit englischen Typnamen, und alle
Schreibwege und Leser müssen aus `events` arbeiten. YNAB führt in diesem Schritt noch.

## What
- Ereignistypen aus ADR 0084 Punkt 2 mit Encoder und Decoder (`schema_version` 1, Payload-Felder
  englisch in camelCase), Streams `budgeting:month:<YYYY-MM>` und
  `budgeting:target:<categoryId>`, Stream-Name und Monats-Id an je einer Stelle gebaut.
- Altbestand-Typ für `target_changes` (etwa `YnabGoalChangedFromBb` mit den Schritt-Ergebnissen),
  weil alte Zeilen nur Teilwerte tragen. Neue Target-Änderungen schreiben bis budgeting-eh8xe
  ebenfalls diesen Typ, weil der Zwei-Schritt-Weg bis dahin bleibt.
- Ein Startschritt in einer Transaktion: `budget_movements`, `budget_movement_results`,
  `target_pause_events`, `target_changes`, `target_change_results` nach `events`, danach die
  Tabellen entfernen. Abbildung nach ADR 0084 Punkt 4: Bewegungen einer `application_id` werden
  ein Entscheidungsereignis nach Herkunft (`TargetsApplied`, `MoneyMoved`,
  `AssignmentChangedInBb`) mit `applicationId` im Payload; jede Ergebniszeile ein
  Ergebnis-Ereignis; Pausen `TargetPaused` oder `TargetPauseLifted` (Altwert "Fortgesetzt" als
  `TargetPauseLifted`); `target_changes` ohne `step` als Schritt 1.
- Reihenfolge im Stream: `occurred_at`, bei Gleichstand Entscheidung vor Ergebnis, dann `rowid`.
- Unbekannte Herkunft, Art oder ein unbekannter Status bricht die Transaktion ab, BB startet nicht.
- Schreibwege (Zuweisen, Verschieben, Targets anwenden, erneut senden, Pausieren, Aufheben, Target
  ändern) und Leser (Historie je Kategorie, allgemeine Historie, "Zuletzt verwendet", Pausen je
  Monat, letzte Target-Änderung für die Target-Prüfung, Bericht einer Anwendung) arbeiten aus
  `events`, immer mit Filter auf `context = 'budgeting'`.

## Acceptance criteria
- [ ] Je Ereignistyp ein Round-Trip-Test Encoder/Decoder; ein unbekannter `event_type` im Kontext
      `budgeting` wird beim Laden als Fehler gemeldet, nicht übersprungen.
- [ ] GIVEN eine Test-Datenbank mit Zeilen aller fünf Tabellen (jede Herkunft, jeder Status, eine
      Pause mit Altwert "Fortgesetzt", eine `target_changes`-Zeile ohne `step`, zwei Zeilen mit
      gleichem `occurred_at`), WHEN der Startschritt läuft, THEN stehen die Ereignisse in der
      Reihenfolge aus ADR 0084 Punkt 4 in `events`, die fünf Tabellen existieren nicht mehr, und
      ihre `position` liegt hinter den Kassenbuch-Ereignissen.
- [ ] Vorher-nachher-Vergleich im Test: "Zuletzt verwendet", die Historie je Kategorie, die
      allgemeine Historie, die Pausen je Monat, die letzte Target-Änderung (ADR 0082 Punkt 7)
      und der Bericht einer Anwendung liefern vor und nach der Migration dieselben Werte.
- [ ] Replay: Die Leser liefern aus den migrierten Ereignissen dasselbe wie aus neu über die
      Schreibwege angehängten Ereignissen gleichen Inhalts.
- [ ] GIVEN eine Zeile mit unbekannter Herkunft, WHEN der Startschritt läuft, THEN rollt die
      Transaktion zurück, die alten Tabellen sind unverändert da, und der Start bricht mit einer
      Meldung ab, die Tabelle und Wert nennt.
- [ ] Ein zweiter Start nach erfolgreicher Migration tut nichts (Tabellen fehlen, kein Fehler).
- [ ] Jeder Ladepfad filtert auf `context` oder `stream_id`; ein Test mit einem Kassenbuch-Ereignis
      in `events` zeigt, dass kein Budget-Leser es liest.
- [ ] Bestehende Budget-Tests bleiben grün; das Verhalten gegenüber YNAB und dem Client ändert
      sich nicht. Ändert sich ein Feld in `src/Shared`, werden die Fixtures in
      `scripts/*.test.mjs` (etwa `scripts/budget-history.test.mjs`) im selben Schritt nachgezogen.
- [ ] Probe gegen eine Kopie der Produktions-Datenbank außerhalb des Repos ist vor dem Deploy
      gelaufen und im Outcome vermerkt (ohne Zahlen oder Daten aus dem Budget).
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

## Notes
- Quelle: ADR 0084 Punkte 1, 2 und 4; Umschlag und generisches Anhängen aus ADR 0083 und
  infrastructure-e4vq7. Kein Budget-Ereignis in `events`, bevor ledger-k9tm2 erledigt ist.
- `target_changes` ohne `step`: `src/Server/Budgeting/Persistence.fs:569-570`.
- Im Altbestand ist ein ungesendet geschlossenes Ziel einer Verschiebung als `changed-in-ynab`
  gespeichert (`CommandHandlers.fs:559`). Prüfen, ob es sich von einem echten "in YNAB geändert"
  unterscheiden lässt; sonst als `AssignmentWriteBackSupersededInYnab` migrieren und im Outcome
  vermerken.
- Der Zustand (Soll, Regeln, Pausen) beginnt erst mit der Übernahme (budgeting-dgrp7,
  budgeting-eh8xe); migrierte Ereignisse sind Historie.