---
id: design-system-m65pw
title: "Styleguide verweist auf den entfallenen CLAUDE.md-Abschnitt Design System Components"
status: backlog
type: chore
context: design-system
created: 2026-10-30
completed:
depends_on: []
blocks: []
tags: [harness]
related_adrs: []
related_research: []
prior_art: [infrastructure-a2pcg]
---

## Why
Die neue CLAUDE.md (infrastructure-a2pcg) hat keine Design-System-API-Referenz mehr. `standards/frontend/styleguide.md` verweist an drei Stellen darauf (Zeilen 13 bis 14, 216 bis 217, 384), ein Agent sucht dort also vergeblich.

## What
Die drei Verweise auf `src/Client/DesignSystem/` (Komponenten und Doc-Kommentare) und die Route `/styleguide` umstellen. Der Satz in Zeile 216 bis 217 ("konsistent mit CLAUDE.md") entfällt.

## Acceptance criteria
- [ ] `grep -n 'Design System Components' standards/` findet nichts mehr.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.
