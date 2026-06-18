---
id: design-system-007
title: Konsolidierung roher Html.svg → Icons-DS-Komponente (SyncFlow-Views)
status: backlog
type: refactor
context: design-system
created: 2026-06-18
completed:
commit:
depends_on: [design-system-001]
blocks: []
tags: [frontend, design-system, refactor, drift, icons, consolidation]
related_adrs: []
related_research: []
prior_art: [design-system-002]
---

## Why
Split aus dem Drift-Audit `design-system-002`. Das Audit fand 4 rohe `Html.svg`/inline-SVG
in den View-Schichten:

- `SyncFlow/View.fs` (1), `SyncFlow/Views/QuickAdd.fs` (1), `InlineRuleForm.fs` (1),
  `TransactionRow.fs` (1).

Der Styleguide (§5, Icons-Zeile) definiert eigene rohe `<svg>` explizit als Drift:
„fehlt ein Icon, hier ergänzen" — also im `Icons`-DS-Modul. Diese vier Stellen wurden in
002 ausgegliedert, weil jede einzeln geprüft werden muss: deckt ein bestehendes
`Icons.*`-Glyph das SVG ab, oder fehlt das Icon und muss erst in `Icons.fs` ergänzt werden
(dann ist es kein reiner Lift mehr, sondern eine DS-Erweiterung).

## What
1. Jedes inline-SVG identifizieren und gegen das `Icons`-Inventar matchen.
2. Wo ein passendes `Icons.*`-Glyph existiert: Call-Site darauf umstellen (Size/Color-Token).
3. Wo kein Glyph existiert: Icon in `Icons.fs` ergänzen (Pfad 1:1 übernehmen), dann nutzen.
4. Tests grün; mobil + Desktop stichprobenhaft visuell bestätigen.

## Acceptance criteria
- [ ] Liste (Datei → SVG → vorhandenes Icon | neu in Icons.fs ergänzt) im Task oder Diary.
- [ ] Keine rohen `Html.svg` mehr an diesen Call-Sites (Grep-Check).
- [ ] Kein Look-Regress (gleiches Glyph/Größe/Farbe); `dotnet build` + `dotnet test` grün; Diary aktualisiert.

## Notes
Abgrenzung wie in 002: gleiche Optik, nur Anheben aufs DS bzw. minimale Icons.fs-Ergänzung.
Kein neues Icon-Design.
