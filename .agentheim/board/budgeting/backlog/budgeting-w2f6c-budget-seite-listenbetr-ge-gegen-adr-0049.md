---
id: budgeting-w2f6c
title: "Budget-Seite: Listenbeträge gegen ADR 0049"
status: backlog
type: bug
context: budgeting
created: 2026-11-02
completed:
depends_on: [budgeting-p4n6r]
blocks: []
tags: [budget, design]
related_adrs: [0049, 0075]
related_research: []
prior_art: [budgeting-p4n6r]
---

## Why
`plainAmount` in `Components/Budget/View.fs:27-31` setzt `text-secondary` statt `text-primary`, und eine positive Aktivität ist nicht Teal. Das weicht von ADR 0049 ab. Die Reviews zu budgeting-g8s3w haben es gefunden, budgeting-p4n6r schloss es aus.

## What
Die Beträge an ADR 0049 angleichen oder die Abweichung per ADR als Ausnahme für die Budget-Seite festhalten.

## Acceptance criteria
- [ ] (Browser) Zugewiesen, Aktivität und Target folgen den Farbregeln aus ADR 0049, oder ein ADR nennt die Ausnahme und ihren Grund.
- [ ] Styleguide und `/styleguide` stimmen mit der Entscheidung überein.