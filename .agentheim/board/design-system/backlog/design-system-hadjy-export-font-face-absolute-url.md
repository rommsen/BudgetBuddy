---
id: design-system-hadjy
title: "Bug: Claude-Design-Export trägt @font-face mit absolutem url(/fonts/...) in styles.css, Schriften laden in Claude Design nicht"
status: backlog
type: bug
context: design-system
created: 2026-09-11
completed:
depends_on: []
blocks: []
tags: [captured, claude-design, fonts]
related_adrs: []
related_research: []
prior_art: []
---

## Why
Beim Re-Sync in `ledger-xbrht` (2026-09-11) blieb in Claude Design die Warnung "Missing
brand fonts" stehen. Die Browser-Konsole zeigt: die Previews werden unter
`.../serve/preview/...` ausgeliefert, die Font-Anfragen gehen aber an die Origin-Wurzel
(`https://<projekt>.claudeusercontent.com/fonts/inter-latin-wght-normal.woff2`) und
liefern 404 ("downloadable font: download failed").

## What
`scripts/export-design-system.mjs` übernimmt die `@font-face`-Regeln der App aus dem
gebauten `styles.css` unverändert, also mit absolutem `src: url(/fonts/...)`. Weil
`styles.css` in jeder Preview nach `colors_and_type.css` eingebunden ist, gewinnt die
absolute Regel gegenüber der relativen aus `FONT_FACE_CSS` (`url('fonts/...')`), und die
Schriften laden im Claude-Design-Projekt nicht.

Fix: im Export `url(/fonts/` nach `url(fonts/` umschreiben (relativ zur Bundle-Wurzel,
wie `FONT_FACE_CSS` in `colors_and_type.css`), plus Regressionstest, der das Bundle
(`styles.css`, `colors_and_type.css`, Previews) auf absolute `url(/`-Pfade prüft.

## Acceptance criteria
- [ ] To be defined during refinement.

## Notes
Captured via `quick-capture` on 2026-09-11 — raw, unrefined. Needs a `modeling` refine pass before
it can be promoted. Workaround in der Session: `dist/design-system/styles.css` von Hand
gepatcht (`sed 's#url(/fonts/#url(fonts/#g'`) und nur diese Datei per DesignSync neu
hochgeladen. Der nächste `npm run export:design-system` erzeugt den Fehler wieder, bis das
Skript korrigiert ist.
