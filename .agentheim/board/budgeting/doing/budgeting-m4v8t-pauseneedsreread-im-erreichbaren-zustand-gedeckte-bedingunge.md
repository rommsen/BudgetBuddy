---
id: budgeting-m4v8t
title: "pauseNeedsReread: im erreichbaren Zustand gedeckte Bedingungen streichen oder benennen"
status: doing
type: chore
context: budgeting
created: 2026-11-04
completed:
depends_on: [budgeting-n7h3x]
blocks: []
tags: [budget, pause, test]
related_adrs: []
related_research: []
prior_art: [budgeting-c6vn4, budgeting-e6p4r]
---

## Why
Aus budgeting-e6p4r: die Gegenprobe "`|| monthChanging model` in `pauseNeedsReread` streichen" bleibt grün, auch im Walk. Jeder Monatswechsel hebt die Generation seit dem Start des Aufrufs, `generationMoved` deckt den Fall. c6vn4 hatte dasselbe schon für `generationMoved` und `notLoaded` gegenüber `MonthStale` festgestellt (QA-Review c6vn4, Kann-Punkt). Bedingungen ohne Test, der sie trägt, sind nach Doktrin Punkt 6 ungeprüft.

## What
In `src/Client/Components/Budget/Pause.fs` entscheiden: die vier Teilbedingungen von `pauseNeedsReread` auf die nötigen kürzen oder jede mit einem Kommentar versehen, warum sie als Absicherung bleibt. Der Walk in `src/Tests/TargetPauseClientTests.fs` muss grün bleiben.

## Acceptance criteria
- [ ] Jede verbliebene Teilbedingung hat einen Test, der sie gestrichen rot macht, oder einen Kommentar, der sie als Absicherung benennt.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.