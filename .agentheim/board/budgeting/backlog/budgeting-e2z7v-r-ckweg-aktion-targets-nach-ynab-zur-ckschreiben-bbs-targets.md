---
id: budgeting-e2z7v
title: "Rückweg-Aktion \"Targets nach YNAB zurückschreiben\": BBs Targets bewusst als Goals nach YNAB schreiben, soweit darstellbar, und listen, was YNAB nicht darstellen kann"
status: backlog
type: feature
context: budgeting
created: 2026-11-05
completed:
depends_on: [budgeting-eh8xe]
blocks: []
tags: [etappe-2, targets, rueckweg, ynab-spiegel]
related_adrs: [0072, 0084, 0088]
related_research: [ynab-reihenfolge-monate-target-schreiben-2026-11-02]
prior_art: [budgeting-k8wq3]
---

## Why
Nach ADR 0084 Punkt 6 sind die Goals in YNAB eingefroren. Kehrt Roman zu YNAB zurück, sollen
BBs Targets dort wieder stehen. Zuweisungen brauchen keinen Rückweg, YNAB hat sie laufend.

## What
- Eine bewusst auszulösende Aktion "Targets nach YNAB zurückschreiben".
- Je Kategorie mit BB-Target: als Goal nach YNAB schreiben, soweit YNABs API es darstellen kann
  (Grenzen aus ADR 0072 Punkte 2 bis 5, etwa zwei Aufrufe bei jährlich mit Datum, kein Wochentag).
- Eine Liste dessen, was YNAB nicht darstellen kann, mit Grund je Kategorie.
- Keine Wirkung auf BBs Targets und kein Ereignis, das BBs Regel ändert.

## Acceptance criteria
- [ ] Eine reine Funktion bildet eine BB-Regel auf YNAB-Patches ab oder liefert den Grund, warum
      sie nicht darstellbar ist; Tests je Art, auch für Wochentag und jährlich mit Datum.
- [ ] GIVEN BB-Targets mit darstellbaren und nicht darstellbaren Regeln, WHEN die Aktion läuft,
      THEN gehen die Patches für die darstellbaren hinaus (Port zählt sie), und die Antwort listet
      die anderen mit Grund (Handler-Test, kein Live-Aufruf).
- [ ] Bei 429 bricht die Aktion ab und meldet, welche Kategorien noch offen sind.
- [ ] Die Aktion läuft nur auf ausdrücklichen Auftrag (Knopf mit Bestätigung), nie automatisch.
- [ ] Die Faltung von BBs Targets ist vor und nach der Aktion gleich.
- [ ] Ändert sich `src/Shared` oder der Client, werden die Fixtures in `scripts/*.test.mjs` im
      selben Schritt nachgezogen; UI nach `standards/frontend/styleguide.md`.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

## Notes
- Quelle: ADR 0084 Punkt 6. Nutzt `setCategoryTarget` (`src/Server/YnabClient.fs:553-577`), falls
  er nach budgeting-eh8xe noch existiert; sonst neu aus der Historie.
- Nach Etappe 2 vorgezogen (Roman, 2026-11-08). Es bleibt die bewusste Aktion aus ADR 0084 Punkt 6,
  kein laufendes Spiegeln (dort verworfen).
- Kein Ereignis: die Aktion ändert weder Geld noch BBs Regeln, das Ergebnis steht nur in der Antwort
  (ADR 0083).
- Ort: Menü ⋯ der Budget-Seite, Eintrag "Targets nach YNAB zurückschreiben", davor `BottomSheet.confirm`
  am Handy bzw. `Modal.confirm` am PC mit der Zahl der Kategorien; danach eine Liste "Geschrieben" und
  "Nicht darstellbar" mit Grund je Kategorie. Der Styleguide gilt (design-system-001 ist erledigt).
- YNAB-API laut OpenAPI-Spezifikation (ADR 0088 Context): `PATCH .../categories/{id}` mit `goal_target`,
  `goal_target_date`, `goal_needs_whole_amount`, `goal_frequency` (`monthly`, `weekly`, `yearly`);
  `goal_frequency` lässt sich nicht mit `goal_target_date` kombinieren.