---
id: budgeting-e2z7v
title: "Targets beim Speichern direkt nach YNAB schreiben (Nachtrag ADR 0084), mit Hinweis bei nicht Darstellbarem und Erneut senden"
status: doing
type: feature
context: budgeting
created: 2026-11-05
completed:
depends_on: [budgeting-eh8xe]
blocks: []
tags: [etappe-2, targets, ynab-spiegel]
related_adrs: [0072, 0084, 0088]
related_research: [ynab-reihenfolge-monate-target-schreiben-2026-11-02]
prior_art: [budgeting-k8wq3]
---

## Why
Roman will Targets wie die Kategorien (ADR 0088) beim Speichern direkt nach YNAB schreiben, damit YNAB als
Fallback dieselben Ziele zeigt (Nachtrag ADR 0084 vom 2026-11-08, ersetzt Punkt 6 "Goals einfrieren" und die
Rückweg-Aktion).

## What
- **Beim Speichern:** Anlegen, Ändern und Entfernen eines Targets in BB (Tab Target, Targets prüfen, Targets
  anwenden ändert nur Zuweisungen und bleibt außen vor) schreibt nach dem BB-Ereignis das Goal per
  `PATCH .../categories/{id}`: `goal_target`, `goal_frequency` (`monthly`, `weekly`, `yearly`), `goal_target_date`,
  `goal_needs_whole_amount`. Entfernen sendet `goal_target: null`. "Jährlich mit Datum" geht in zwei Aufrufen
  (ADR 0072 Punkt 5).
- **BB führt:** Das BB-Target gilt sofort. Scheitert das Schreiben ganz oder nach dem ersten von zwei Aufrufen,
  steht am Target gelb "Nicht in YNAB" mit "Erneut senden" (ADR 0087 Nachtrag); ein späterer Erfolg löst es auf.
- **Nicht Darstellbares:** Eine reine Funktion bildet eine BB-Regel auf die YNAB-Felder ab und liefert dazu, was
  verloren geht (etwa der Wochentag, ADR 0072 Punkt 4). BB schreibt das nächste Passende und zeigt am Target "In
  YNAB ohne Wochentag" (Wortlaut je Fall).
- **Pause:** fachlich YNABs Snooze, per API nicht schreibbar (`goal_snoozed_at` nur lesend). Das Goal in YNAB bleibt
  unverändert; am pausierten Target steht "In YNAB nicht gesnoozt".
- **Einmaliger Lauf:** Nach dem Einbau bringt eine Aktion alle bestehenden BB-Targets nach YNAB (Budget ⋯, mit
  Bestätigung), mit Liste "Geschrieben", "Mit Abweichung" und "Fehlgeschlagen". Bei 429 bricht sie ab und nennt die
  offenen Kategorien.
- Ob Erfolg und Fehlschlag des Schreibens Ereignisse oder Zustand sind, folgt ADR 0083 (Push-Ergebnis zu einem
  Ereignis über Geld-Entscheidungen; Vorbild `EntryPushToYnabFailed`). Der Worker klärt das mit dem
  tactical-modeler und nennt die Wahl im Outcome.

## Acceptance criteria
- [ ] Reine Funktion: BB-Regel je Art (monatlich, wöchentlich mit und ohne Wochentag, jährlich, jährlich mit Datum,
      einmalig mit Datum, ohne Target) ergibt die YNAB-Felder und die Liste des Verlorenen; ein Test je Art.
- [ ] Handler-Test: Target speichern schreibt das BB-Ereignis und ruft danach den Port mit den Feldern; bei "jährlich
      mit Datum" zwei Aufrufe in der Reihenfolge aus ADR 0072 Punkt 5.
- [ ] Handler-Test: Port scheitert beim ersten oder zweiten Aufruf: das BB-Target ist gespeichert, der Fehlschlag
      steht mit Grund fest; "Erneut senden" schreibt erneut und löst ihn bei Erfolg auf.
- [ ] Handler-Test: Target entfernen sendet `goal_target: null`; Pausieren und Aufheben rufen den Port nicht.
- [ ] Handler-Test: der einmalige Lauf schreibt alle Targets, meldet Abweichungen und bricht bei 429 mit den offenen
      Kategorien ab; die Faltung von BBs Targets ist vor und nach dem Lauf gleich.
- [ ] Encoder-Test für den JSON-Rumpf (Beträge als JSON-Zahl in Milliunits, `Encode.int64`).
- [ ] Client-`update` unter .NET: Hinweise "Nicht in YNAB" mit "Erneut senden", "In YNAB ohne Wochentag" und "In
      YNAB nicht gesnoozt" erscheinen aus dem Zustand der Kategorie; Erneut senden ist gesperrt, solange es läuft.
- [ ] Je neuem Endpoint eine Zeile in `src/Tests/RemotingEndpointNegativeTests.fs`; ein neues Pflichtfeld in
      `src/Shared/` zieht die Fixtures in `scripts/*.test.mjs` mit.
- [ ] Live-Aufrufe gegen YNAB nur hinter `RUN_INTEGRATION_TESTS`.

## Notes
- Nachtrag ADR 0084 vom 2026-11-08 (Entscheidung), ADR 0072 (Grenzen des Goal-Schreibens: Wochentag, zwei Aufrufe,
  kein Löschen und Neuanlegen), ADR 0080 (Target-Änderungen nur im laufenden Monat), ADR 0088 (Kategorien,
  OpenAPI-Felder), ADR 0087 Nachtrag (Gelb mit "Erneut senden").
- Den alten Schreibweg (`setCategoryTarget`) gibt es nach budgeting-eh8xe nicht mehr; neu bauen, Probe
  budgeting-q7m2c und Forschung `ynab-reihenfolge-monate-target-schreiben-2026-11-02` lesen.
- Kein Bezug mehr auf die Rückweg-Aktion; der einmalige Lauf ersetzt sie.
