---
id: design-system-m65pw
title: "Styleguide verweist auf den entfallenen CLAUDE.md-Abschnitt Design System Components"
status: done
type: chore
context: design-system
created: 2026-10-30
completed: 2026-10-30
depends_on: []
blocks: []
tags: [harness]
related_adrs: []
related_research: []
prior_art: [infrastructure-a2pcg]
---

## Why
Die neue CLAUDE.md (infrastructure-a2pcg) hat keine Design-System-API-Referenz mehr. `standards/frontend/styleguide.md` verweist an drei Stellen darauf (Zeilen 13 bis 14, 221 bis 222, 389), ein Agent sucht dort also vergeblich.

## What
Die drei Verweise auf `src/Client/DesignSystem/` (Komponenten und Doc-Kommentare) und die Route `/styleguide` umstellen. Der Einschub in Zeile 222 ("konsistent mit CLAUDE.md") entfällt.

## Acceptance criteria
- [ ] `grep -rn 'Design System Components' standards/` findet nichts mehr.
- [ ] `grep -n 'CLAUDE.md' standards/frontend/styleguide.md` findet nichts mehr.
- [ ] Die drei Stellen nennen stattdessen `src/Client/DesignSystem/` oder die Route `/styleguide`.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

## Notes
Refine am 2026-10-30: Zeilennummern auf den heutigen Stand gezogen. Es sind die einzigen Treffer für `CLAUDE.md` unter `standards/`.

## Outcome

`standards/frontend/styleguide.md` nennt an den drei Stellen (Kopf, Abschnitt 5, Quellenliste) statt des entfallenen CLAUDE.md-Abschnitts `src/Client/DesignSystem/` und die Route `/styleguide`. Der Einschub "konsistent mit CLAUDE.md" ist entfallen. Beide grep-Prüfungen aus den Akzeptanzkriterien finden nichts mehr, `./verify.sh` zeigt `VERIFY: PASS` (dotnet 2695 grün, node 329 grün).
