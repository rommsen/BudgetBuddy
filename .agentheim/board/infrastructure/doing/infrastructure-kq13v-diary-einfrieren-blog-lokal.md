---
id: infrastructure-kq13v
title: "Diary einfrieren, Pages-Deploy abschalten, blogpost auf protocol.md und git log umstellen"
status: doing
type: chore
context: infrastructure
created: 2026-10-29
completed:
depends_on: [infrastructure-psdcb]
blocks: []
tags: [harness]
related_adrs: []
related_research: []
prior_art: []
---

## Why
Owner-Entscheidung 2026-10-29 (Abweichung E9): Blog-Möglichkeit bleibt, nichts Externes mehr. `development.md` (434 KB) wird pro Task gepflegt und über `.github/workflows/deploy-diary.yml` veröffentlicht.

## What
`deploy-diary.yml` entfernen oder deaktivieren. `diary/development.md` bleibt als Archiv, keine neuen Einträge. `.claude/commands/blogpost.md` liest `.agentheim/board/protocol.md` und git log und schreibt lokal nach `diary/_posts/`.

## Acceptance criteria
- [ ] Kein Workflow deployt `diary/**` mehr.
- [ ] `blogpost` verweist nicht mehr auf `diary/development.md`.
- [ ] Die Diary-Pflicht steht nirgends mehr in Rules oder Agents (grep); CLAUDE.md folgt in M12.

## Notes
Migrationsschritt M13. Zielbild: `.agentheim/knowledge/research/harness-audit/zielbild.md`. Promote und work erst nach ausdrücklichem Ja des Owners.

