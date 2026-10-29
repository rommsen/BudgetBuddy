---
id: design-system-w3k5d
title: "Button.iconOnlyClass erkennt einen Text aus Leerzeichen als Icon-only"
status: backlog
type: bug
context: design-system
created: 2026-10-29
depends_on: []
blocks: []
tags: [button]
related_adrs: []
related_research: []
prior_art: [infrastructure-hdf7j]
---

## Why
`src/Client/DesignSystem/Button.fs` `iconOnlyClass` prüft mit `String.IsNullOrEmpty`. Ein Knopf mit Text `" "` gilt damit nicht als Icon-only und bekommt am Handy keine 48 px Mindestbreite (Audit C B19).

## What
`IsNullOrWhiteSpace` verwenden, mit Regressionstest in `src/Tests/ButtonSizeTests.fs`.

## Acceptance criteria
- [ ] Ein Test zeigt: Small mit Text `" "` liefert `min-w-[48px] md:min-w-0`; der Test ist vor dem Fix rot.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.