---
id: design-system-f7k2p
title: "Bug: Google-Fonts-@import in styles.css steht hinter @theme und wird vom Build verworfen (App rendert in Ersatzschriften)"
status: backlog
type: bug
context: design-system
created: 2026-09-11
completed:
depends_on: []
blocks: []
tags: [design-system, css, fonts, bug]
related_adrs: [0024]
related_research: []
prior_art: [design-system-t82sg]
---

## Why
Bei der Iteration-1-Prüfung von `design-system-t82sg` (Export-Bundle für Claude Design)
fiel auf: das gebaute `dist/design-system/styles.css` enthält null `@import`- und null
`@font-face`-Regeln. Ursache liegt nicht im Export, sondern in
`src/Client/styles.css` selbst: der Google-Fonts-`@import` (Inter, Space Grotesk,
JetBrains Mono) steht in Zeile 50, **hinter** dem `@theme`-Block (Zeile 7 bis 43). CSS
verlangt, dass `@import` vor allen anderen Regeln außer `@charset`/`@layer` steht. Der
Tailwind-4-Build verwirft die Regel deshalb still (Build-Log: "@import rules must precede
all rules"). Damit lädt die gebaute BudgetBuddy-App die drei Marken-Fonts nicht: sie
rendert in Browser-Ersatzschriften statt Inter/Space Grotesk/JetBrains Mono.
`design-system-t82sg` hat den Bug nicht behoben (Umfang: nur der Export), sondern für die
Bundle-Vorschauen einen expliziten `<link>` ergänzt (s. ADR 0024).

## What
- Den Google-Fonts-`@import` in `src/Client/styles.css` vor den `@theme`-Block verschieben
  (oder auf einen `<link>` in `index.html` umstellen, was ebenfalls funktioniert und
  unabhängig von CSS-Import-Reihenfolge ist).
- Nach dem Fix: `npm run build` ausführen und im gebauten `dist/public/assets/*.css`
  prüfen, dass die Font-Regel enthalten ist (bzw. der `<link>` in `index.html` greift).
- Regressionstest: headless prüfen, dass `document.fonts` nach dem Laden der App die drei
  Familien enthält (Inter, Space Grotesk, JetBrains Mono).

## Acceptance criteria
- [ ] Die gebaute App lädt Inter, Space Grotesk und JetBrains Mono (verifiziert über
      `document.fonts` in einem headless-Check gegen den Produktions-Build, nicht nur
      `vite dev`).
- [ ] Regressionstest vorhanden, der den Bug reproduziert hätte (schlägt vor dem Fix fehl,
      besteht danach).

## Notes
Gefunden während der Verifikation von `design-system-t82sg` (Iteration 1); Detailanalyse
und die Bundle-seitige Übergangslösung stehen in ADR 0024
(`.agentheim/knowledge/decisions/0024-export-bundle-build-nicht-dev-und-nicht-fixe-navigation-preview.md`).
