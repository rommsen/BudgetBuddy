---
id: design-system-hadjy
title: "Bug: Claude-Design-Export trägt @font-face mit absolutem url(/fonts/...) in styles.css, Schriften laden in Claude Design nicht"
status: doing
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
- [ ] Nach `npm run export:design-system` enthält keine Datei unter `dist/design-system/`
      einen absoluten Asset-Pfad der Form `url(/…)`. Geprüft über das ganze Bundle
      (`styles.css`, `colors_and_type.css`, jede `preview/*.html`), nicht nur über die
      Datei, in der der Fehler gefunden wurde.
- [ ] `dist/design-system/styles.css` bindet die drei Schriften relativ zur Bundle-Wurzel
      ein (`url(fonts/…)`), wie `FONT_FACE_CSS` in `colors_and_type.css` es bereits tut.
- [ ] Das Umschreiben geschieht beim Erzeugen des Bundles, nicht von Hand danach: die
      Stelle, die das gebaute App-CSS ins Bundle kopiert (heute ein reines `copyFile` in
      `scripts/export-design-system.mjs`), liest, schreibt um und schreibt.
- [ ] Ein Regressionstest scheitert, wenn ein absoluter `url(/…)`-Pfad ins Bundle
      zurückkehrt. Andockpunkt ist `src/Tests/FontFaceRegressionTests.fs`, das bereits
      `src/Client/styles.css` auf lokale `@font-face`-Regeln prüft — erweitern statt eine
      zweite Prüfstelle daneben zu stellen.
- [ ] Zwei Läufe hintereinander erzeugen ein byte-identisches Bundle (Idempotenz-Kriterium
      aus `design-system-t82sg` bleibt erfüllt).
- [ ] `dotnet test` ist grün.

## Notes
Captured via `quick-capture` on 2026-09-11 — raw, unrefined. Needs a `modeling` refine pass before
it can be promoted. Workaround in der Session: `dist/design-system/styles.css` von Hand
gepatcht (`sed 's#url(/fonts/#url(fonts/#g'`) und nur diese Datei per DesignSync neu
hochgeladen. Der nächste `npm run export:design-system` erzeugt den Fehler wieder, bis das
Skript korrigiert ist.
