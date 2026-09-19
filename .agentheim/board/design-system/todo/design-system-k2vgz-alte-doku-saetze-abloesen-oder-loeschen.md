---
id: design-system-k2vgz
title: "Abgelöste Doku-Sätze aufräumen: docs/DESIGN-SYSTEM.md und docs/README.md, Farbpalette vorher sichern"
status: todo
type: refactor
context: design-system
created: 2026-09-18
completed:
depends_on: []
blocks: []
tags: [doku, aufraeumen, styleguide]
related_adrs: [0031]
related_research: []
prior_art: [design-system-fq8dn]
---

## Why
Beim Nachziehen der DaisyUI-Angaben auf ADR 0031 (2026-09-18) sind zwei Dokumente
aufgefallen, die wie gültige Anweisung aussehen, es aber nicht mehr sind:

- **`docs/DESIGN-SYSTEM.md`** (letzter Commit 2025-11-30). Abschnitt 10 heißt „DaisyUI Theme
  Configuration" und enthält ein Installationsrezept samt `plugins: [require('daisyui')]`.
  DaisyUI ist mit ADR 0031 aus dem Projekt entfernt und steht nicht in `package.json`.
- **`docs/README.md`** (letzter Commit 2025-11-29). Ein „Development Guide" mit
  Einstiegskapitel „For New Projects" und einer Tabelle, die DaisyUI 4.12+ als
  Komponenten-Bibliothek führt.

Beide stammen aus der Zeit vor dem `standards/`-Satz und sind davon abgelöst: die
Dokumentations-Landkarte in der Wurzel-`CLAUDE.md` verweist ausschließlich auf
`standards/`, und der Styleguide (`standards/frontend/styleguide.md`) ist das Gate für
UI-Arbeit. Auf die beiden Dateien verweist nichts mehr, mit **einer** Ausnahme.

Das ist dieselbe Art Fehler wie in `design-system-fq8dn`: eine Doku-Aussage, die niemand
liest, bis ein Agent sie liest und für verbindlich hält.

## What
1. **Entscheiden, was mit den zwei Dateien passiert.** Zwei Wege:
   - Banner oben in beiden Dateien: „Abgelöst durch `standards/`, historischer Stand vom
     November 2025, nicht als Anweisung lesen." Billig, lässt den Inhalt als Zeitdokument
     stehen.
   - Löschen. Sauberer, kostet aber den Inhalt; Teile davon (Design-Philosophie,
     Farbpalette) sind im Styleguide vielleicht knapper gefasst als hier.
2. **Vorher die Farbpalette sichern.** `.claude/agents/design-quality.md:74` zitiert
   „Color Palette (from DESIGN-SYSTEM.md)". Das ist der einzige lebende Verweis. Vor dem
   Löschen prüfen, ob der Styleguide dieselben Werte trägt, und den Agenten auf
   `standards/frontend/styleguide.md` umstellen. Sonst verliert der Design-Quality-Agent
   seine Quelle und erfindet Farben.
3. **`template/**` ist eine eigene Frage, nicht Teil dieser Aufgabe.** Dort steht DaisyUI
   in `template/CLAUDE.md`, `template/README.md` und `template/docs/00-ARCHITECTURE.md`. Das
   Verzeichnis ist ein Gerüst für neue Projekte und beschreibt seinen eigenen Stack; ob es
   BudgetBuddys Entscheidung mittragen soll, entscheidet Roman getrennt.

**Nicht anfassen:** Diary, Blogposts, Protokoll, erledigte Tasks, ADR 0031 selbst und die
Code-Kommentare in `Table.fs`, `Settings/View.fs` und `Ledger/View.fs`, die erklären, dass
DaisyUI nicht geladen ist. Das ist Geschichte oder aktive Erklärung.

## Acceptance criteria
- [ ] `docs/DESIGN-SYSTEM.md` und `docs/README.md` sind gelöscht (Entscheidung Roman,
      2026-09-19: löschen statt bannern — eine zweite Quelle, die ein Agent für verbindlich
      hält, ist genau das Problem).
- [ ] **Vorher** ist die Farbpalette gesichert. Der Fund vom 2026-09-19 korrigiert die
      Annahme des Tasks: `standards/frontend/styleguide.md` enthält **keinen einzigen
      Hex-Wert**. Ein bloßes Umhängen des Verweises ginge also ins Leere. Zu wählen und im
      Ergebnis zu begründen: entweder die Werte wandern in den Styleguide, oder
      `.claude/agents/design-quality.md` verweist auf `src/Client/styles.css` (den
      `@theme`-Block, die tatsächliche Quelle der Tokens).
- [ ] `.claude/agents/design-quality.md` zitiert danach keine Palette mehr "from
      DESIGN-SYSTEM.md", sondern verweist auf die gewählte lebende Quelle. Die dort heute
      eingebetteten Werte (`--neon-green: #00ff88`, `--neon-orange: #ff6b2c`,
      `--neon-teal: #00d4aa`, `--neon-purple: #a855f7`, `--neon-pink: #ff2e97`,
      `--neon-red: #ff3b5c`, `--bg-void`, `--bg-dark`, `--bg-surface`) sind gegen die Quelle
      abgeglichen; Abweichungen sind benannt, nicht stillschweigend übernommen.
- [ ] Ein Suchlauf über das Repo (ohne Diary, Blogposts, Protokoll, erledigte Tasks und
      `template/**`) findet keinen lebenden Verweis mehr auf die beiden gelöschten Dateien.
- [ ] Unangetastet bleiben: Diary, Blogposts, Protokoll, erledigte Tasks, ADR 0031 und die
      Code-Kommentare in `Table.fs`, `Settings/View.fs`, `Ledger/View.fs`, die erklären,
      dass DaisyUI nicht geladen ist.
- [ ] `template/**` bleibt unangetastet — eigene Frage, eigener Task.

## Notes
Erfasst am 2026-09-18 nach Romans Freigabe, beim Nachziehen der DaisyUI-Angaben (Commit
`095dd71`, neun Stellen korrigiert). Die beiden hier genannten Dateien waren bewusst
ausgenommen, weil dort nicht ein Wort falsch ist, sondern die Frage offen ist, ob das
Dokument noch lebt.
