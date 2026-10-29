---
id: infrastructure-kq13v
title: "Diary einfrieren, Pages-Deploy abschalten, blogpost auf protocol.md und git log umstellen"
status: done
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


## Outcome

Migrationsschritt M13 umgesetzt (Owner-Abweichung E9):

- `.github/workflows/deploy-diary.yml` gelöscht. Kein anderer Workflow deployt `diary/**`.
- `.claude/commands/blogpost.md` neu ausgerichtet: Quellen sind die Einträge in `.agentheim/board/protocol.md` und `git log --since/--until` für ein Zeitfenster (vom User genannt, sonst seit dem letzten Post), ergänzt um den Context Window. Ausgabe nur lokal unter `diary/_posts/`, ausdrücklich ohne Commit, Push oder Veröffentlichung. Der Befehl erwähnt `diary/development.md` nicht mehr.
- Diary-Pflicht entfernt in `.claude/rules/testing.md` (Schritt 8 im Bug Fix Protocol) und `.claude/rules/feature-workflow.md` (Abschnitt "Development Diary" samt Archivierungsregel am Dateiende). In `.claude/agents/*.md` gab es keine Diary-Erwähnung.
- `diary/development.md` bleibt unverändert als Archiv.

Bewusst nicht angefasst, mit verbleibender Diary-Erwähnung:
- `.claude/rules/localization.md`: nennt `diary/**` als Geltungspfad und im Umlaut-grep. Das ist keine Diary-Pflicht, sondern die Umlautregel für Dateien dort (gilt weiter für `_posts/`).
- `.claude/skills/README.md` und `.claude/skills/fsharp-feature/SKILL.md`: werden vom parallelen Worker M9 (infrastructure-vm8zz) gelöscht.
- `.claude/settings.local.json`: Owner-Datei.
- `CLAUDE.md`: Abschnitt "Development Diary" und Qualitätskriterien folgen in M12.

Reste in `diary/`, die nur dem Jekyll-Deploy dienten: `diary/_config.yml` (Jekyll-Konfiguration, Theme minima) und `diary/index.md` (Jekyll-Startseite mit `layout: home`). Beide liegen gelassen; sie stören lokal nicht, können aber bei Gelegenheit entfernt werden. `diary/development_archive.md` und die Posts in `diary/_posts/` sind Inhalt und bleiben.

Gate grün: `dotnet build`, `dotnet test` (2502 bestanden, 6 übersprungen), `npm run build`, `guard:classes`, `guard:tokens`, `test:scripts` (0 Fehler). Kein Skripttest bindet den Deploy-Workflow oder die Diary-Pflicht.
