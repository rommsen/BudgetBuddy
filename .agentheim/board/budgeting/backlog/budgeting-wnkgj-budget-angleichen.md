---
id: budgeting-wnkgj
title: "Budget an die Bedeutungen angleichen: Targets prüfen mit weniger Orange, Wochentag als Auswahlfeld, Historie mit „Zeigen“, Leerflächen am PC"
status: backlog
type: feature
context: budgeting
created: 2026-11-11
completed:
depends_on: [design-system-001-styleguide]
blocks: []
tags: [etappe-3, frontend, budget]
related_adrs: [0087]
related_research: []
prior_art: []
---

## Why
Das Budget weicht an einigen Stellen von der Bedeutungsschicht ab (ADR 0087).

## What
Targets prüfen nutzt Orange nur nach §2. Wochentag eines Targets als Auswahlfeld statt sieben Knöpfe (§7.5). Budget-Historie mit Auswahlfeld „Zeigen“ statt `ChipBar` (§7.11). Große Leerflächen am PC nach §7.15.

## Acceptance criteria
- [ ] To be defined during refinement.

## Notes
Vorab entschieden (Roman, 2026-11-15, für die Verfeinerung):
- „noch nicht in YNAB“ an einer Kategorie mit ausstehendem Push in Gelb wie im Kassenbuch (heute `Badge.neutralSmall`,
  `MonthView.fs` mit Text aus `Month.fs`), Styleguide §2.
- Die Budget-Tabelle steht neben dem Panel erst ab rund 1340 px (G11 aus design-system-gcpws); hier nochmal ansehen.
Schnitt aus Etappe 3 (Fahrplan, Roman 2026-11-11). Prototyp Version 15 bis 17: Budget-Tabs mit fester Höhe, Monatsbalken mit Beträgen.
Vorlage: Prototyp https://claude.ai/artifact/CE3WJZHhPX6NABosJaviRg (Version 17). Baut der Schnitt eine Seite um, fliegen ihre `[flow]`-, `[tastatur]`-, `[fokus]`- und `[scroll]`-Tests raus und werden gegen die Muster aus dem Styleguide neu geschrieben, je Flow einer.
