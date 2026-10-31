---
id: ynab-sync-r7m2c
title: "Import prüfen: eine alte Antwort auf die Lena-Farbe darf die neuere nicht ersetzen"
status: backlog
type: bug
context: ynab-sync
created: 2026-10-31
completed:
depends_on: [ynab-sync-k3vq8]
blocks: []
tags: [import, lena, retry]
related_adrs: [0056]
related_research: []
prior_art: [ynab-sync-k3vq8, ynab-sync-d9e51]
---

## Why

`LoadLenaFlagColor` läuft über `Retry.reading` und startet je `LoadTransactions`. Scheitert eine ältere Abfrage nach den Wiederholungen, nachdem eine neuere schon geantwortet hat, setzt `LenaFlagColorLoaded None` die Farbe zurück und sperrt "Lena fragen", bis die Buchungen neu laden.

## What

Antworten tragen wie bei Sitzung, Buchungen und Konten den Stand ihres Abrufs, nur der aktuelle zählt.

## Acceptance criteria

- [ ] Kaputt-Szenario: GIVEN eine neuere Antwort lieferte die Farbe, WHEN die ältere Abfrage scheitert, THEN bleibt die Farbe gesetzt (Test auf Client-`update`, vor dem Fix rot).
- [ ] Intakt-Szenario: Scheitert die aktuelle Abfrage, bleibt die Farbe unbekannt und die Aktion gesperrt, ohne Toast.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.