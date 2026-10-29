---
id: infrastructure-a2pcg
title: "Projekt-CLAUDE.md neu schreiben, Ziel 100 bis 130 Zeilen"
status: backlog
type: chore
context: infrastructure
created: 2026-10-29
completed:
depends_on: [infrastructure-jy5ca, infrastructure-cbr0r, infrastructure-gwvw1, infrastructure-kq13v]
blocks: []
tags: [harness]
related_adrs: []
related_research: []
prior_art: []
---

## Why
29 KB, etwa 7,3k Tokens always-on, plus doppeltes Laden in Worktrees; Serena-Block, Milestone-, Backlog-, QA- und Diary-Pflichten werden nicht gelebt; Design-System-API-Referenz ist aus Code und Styleguide ablesbar (Audit A). Souls Abschnitt 2.

## What
Inhalt: Domäne (Kassenbuch, YNAB, Comdirect, Bob), Security-Fallen (Bot-Grenzen, Secrets, Live-Dienste), Laufzeitfallen (Fable.Remoting-Pflichtfelder, Giraffe `skipPipeline`, CLIMutable), Dev-Kommandos, Gate, Ablauf, Lernen, README-Deltas. Ohne CRITICAL/MUST-Nachdruck.

## Acceptance criteria
- [ ] CLAUDE.md hat höchstens 130 Zeilen.
- [ ] Der Abschnitt README-Deltas bleibt so, dass `npm run test:scripts` (Kopplung Skriptpfad/Text) grün ist.

## Notes
Migrationsschritt M12. Zielbild: `.agentheim/knowledge/research/harness-audit/zielbild.md`. Promote und work erst nach ausdrücklichem Ja des Owners.
Conductor-Task: bleibt im Backlog, kein Worker. Der Owner begleitet den Schritt. Nach diesem Schritt messen.
