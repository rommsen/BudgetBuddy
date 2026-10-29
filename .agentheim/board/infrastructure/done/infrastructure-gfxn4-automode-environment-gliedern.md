---
id: infrastructure-gfxn4
title: "autoMode in ~/.claude/settings.json gliedern: neutrale Org-Zeilen plus Blöcke je Projekt"
status: done
type: chore
context: infrastructure
created: 2026-10-29
completed:
depends_on: []
blocks: []
tags: [harness]
related_adrs: []
related_research: []
prior_art: []
---

## Why
`autoMode.environment` beschreibt nur BudgetBuddy (Trusted repo, Deploy-Remote, .env-Secrets) und gilt für alle Projekte (Audit A, abgleich-souls Abschnitt 3). Owner 2026-10-29: teilweise global.

## What
Vorher in der Claude-Code-Doku prüfen, ob projektlokale `autoMode`-Einträge zusammengeführt werden. Danach gliedern: projektneutrale Zeilen global, BudgetBuddy- und Souls-Blöcke mit Pfadbezug (oder projektlokal, falls unterstützt).

## Acceptance criteria
- [ ] Kein globaler Satz nennt BudgetBuddy als einziges trusted repo.
- [ ] BudgetBuddy-spezifische soft_deny-Einträge (`git push deploy`, `deploy-rules.sh --clear`) wirken weiter.

## Notes
Migrationsschritt G. Zielbild: `.agentheim/knowledge/research/harness-audit/zielbild.md`. Promote und work erst nach ausdrücklichem Ja des Owners.
Conductor-Task: bleibt im Backlog, kein Worker. Der Owner begleitet den Schritt. Änderung außerhalb des Repos, nur durch oder mit dem Owner.

## Outcome

Doku geprüft (code.claude.com/docs/en/auto-mode-config.md): Der Klassifikator liest `autoMode` nicht aus `.claude/settings.json` oder `.claude/settings.local.json`, nur aus User- und Managed-Settings oder `--settings`. Einträge mehrerer Ebenen werden zusammengeführt. Ein Pfad-Scoping gibt es nicht; projektspezifische Regeln gehören laut Doku in die CLAUDE.md, die der Klassifikator mitliest.

Umgesetzt in `~/.claude/settings.json` (Owner-Freigabe 2026-10-29, Backup im Scratchpad der Session): `environment` gegliedert in "Org-wide (gilt für alle Projekte)" mit projektneutralen Zeilen plus neuer allgemeiner Secrets-Regel und "Projekt BudgetBuddy (/Users/romansachse/src/BudgetBuddy)" mit Repo, Deploy-Remote, Secrets und Routine. Kein Satz nennt BudgetBuddy mehr als einziges trusted repo. Die beiden `soft_deny`-Einträge (`git push deploy`, `deploy-rules.sh --clear`) bleiben, mit Pfadbezug im Text. `allow` (homelab) unverändert. Entfallen: `legacy/.env` (gelöscht in M9), Vermutung zu supabase/hcloud.

Für M12: die BudgetBuddy-spezifischen Verbote (kein Push auf deploy ohne Auftrag, `deploy-rules.sh --clear` nur nach Rückfrage) zusätzlich in die neue CLAUDE.md, weil der Klassifikator sie dort mitliest. Ein Souls-Block fehlt noch; ihn ergänzt die Souls-Session bei Bedarf.
