---
id: design-system-f7k2p
title: "Bug: App lädt ihre Schriften nicht (Google-Fonts-@import hinter @theme wird verworfen); Lösung: Schriften selbst ausliefern, auch im Claude-Design-Export"
status: backlog
type: bug
context: design-system
created: 2026-09-11
completed:
depends_on: [design-system-001]
blocks: [ledger-xbrht]
tags: [design-system, css, fonts, bug, claude-design]
related_adrs: [0024]
related_research: []
prior_art: [design-system-t82sg, design-system-h58y4]
---

## Why
Bei der Prüfung von `design-system-t82sg` fiel auf: das gebaute CSS enthält keine
`@import`- und keine `@font-face`-Regeln. Der Google-Fonts-`@import` (Inter, Space
Grotesk, JetBrains Mono) steht in `src/Client/styles.css` (Zeile 50) **hinter** dem
`@theme`-Block. CSS verlangt `@import` vor allen anderen Regeln außer `@charset`/`@layer`;
der Tailwind-4-Build verwirft ihn deshalb (Build-Log: "@import rules must precede all
rules"). Die gebaute App rendert seitdem in Browser-Ersatzschriften.

Beim Erst-Sync nach Claude Design (`design-system-h58y4`, 2026-09-11) meldete Claude
Design zusätzlich "Missing brand fonts: Inter (--font-sans), JetBrains Mono
(--font-mono), Space Grotesk (--font-display), rendering with substitutes until the font
files are uploaded". Claude Design erwartet die Schriften als Dateien; der Google-Fonts-Link
im Bundle (ADR 0024) reicht dort nicht.

Beides löst dieselbe Maßnahme (Entscheidung Roman, 2026-09-11): BudgetBuddy liefert die
drei Schriften selbst aus. Nebeneffekt: die PWA hat ihre Schriften auch offline, und die App
hängt beim Laden nicht mehr an Google.

## What
1. **Schriften ins Repo:** woff2-Dateien der verwendeten Schnitte (Inter 300 bis 800,
   JetBrains Mono 400 bis 600, Space Grotesk 400 bis 700; Variable Fonts, wenn verfügbar
   und kleiner) unter `src/Client/public/fonts/`, mit einer kurzen Lizenz-Notiz (alle drei
   stehen unter der SIL Open Font License 1.1; Lizenztext beilegen). Bezugsquelle beim
   Implementieren wählen (z. B. die offiziellen Releases oder die Google-Fonts-Dateien),
   im Diary festhalten.
2. **`@font-face` statt Google-Import** in `src/Client/styles.css`; den
   Google-Fonts-`@import` entfernen. `font-display: swap` beibehalten.
3. **PWA:** prüfen, dass die Font-Dateien im Precache landen (vite-plugin-pwa
   `globPatterns` o. ä.).
4. **Export:** `scripts/export-design-system.mjs` legt die Font-Dateien ins Bundle
   (`fonts/`, wie beim Vorbild-Projekt "Smolder Design System") und bindet sie per
   `@font-face` in `colors_and_type.css` ein; der Google-Fonts-`<link>` in den Vorschauen
   und die URL-Extraktion aus `styles.css` entfallen. ADR 0024 entsprechend nachziehen
   (neue ADR, wenn die Entscheidung sich grundlegend ändert).
5. **Regressionstest** nach dem Bug-Protokoll in CLAUDE.md: ein Test, der vor dem Fix
   fehlschlägt, z. B. ein Expecto-Test, der `src/Client/styles.css` liest und prüft, dass
   für alle drei Familien `@font-face` mit lokalem `url(...)` existiert und kein
   `fonts.googleapis.com`-Import mehr steht; dazu der headless-Check `document.fonts`
   gegen den Produktions-Build.

## Acceptance criteria
- [ ] Die gebaute App lädt Inter, Space Grotesk und JetBrains Mono aus eigenen Dateien
      (headless `document.fonts` gegen den Produktions-Build, nicht nur `vite dev`); keine
      Anfrage an fonts.googleapis.com oder fonts.gstatic.com.
- [ ] Regressionstest vorhanden, der vor dem Fix fehlschlägt und danach besteht, mit
      Kommentar, welchen Bug er verhindert.
- [ ] Die Font-Dateien sind im PWA-Precache.
- [ ] Das Export-Bundle enthält die Font-Dateien und bindet sie per `@font-face` ein; die
      Vorschauen rendern damit auch ohne Netz; zwei Export-Läufe bleiben byte-identisch.
- [ ] Lizenz-Notiz (OFL 1.1) liegt bei den Fonts im Repo und im Bundle.
- [ ] `dotnet build`, `dotnet test`, `npm run build` grün.

## Notes
- Gefunden in der Verifikation von `design-system-t82sg` (Iteration 1), bestätigt durch
  die Meldung in Claude Design beim Erst-Sync (`design-system-h58y4`). Übergangslösung und
  Analyse: ADR 0024.
- Kann parallel zu `design-system-y6gkd` laufen; beide ändern
  `scripts/export-design-system.mjs`, aber an verschiedenen Stellen (Fonts vs. Karten).
- Nach beiden Tasks folgt ein Re-Sync nach dem README-Ablauf (Hauptsession mit Roman).
