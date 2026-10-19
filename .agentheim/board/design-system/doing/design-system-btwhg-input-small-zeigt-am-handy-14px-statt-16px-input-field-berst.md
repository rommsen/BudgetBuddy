---
id: design-system-btwhg
title: Input.Small zeigt am Handy 14px statt 16px (.input-field überstimmt text-base)
status: doing
type: bug
context: design-system
created: 2026-10-19
depends_on: []
blocks: []
tags: [frontend, ios, input]
related_adrs: [0005]
related_research: []
prior_art: [design-system-zahv9, design-system-m7z9q]
---

## Why
Beim Messen des Datumsfelds (design-system-m7z9q) zeigte sich: `.input-field` in `src/Client/styles.css` (~678) setzt `font-size: 14px` ohne Layer. Die Tailwind-Klassen aus `Input.sizeToClass Small` (`text-base md:text-sm`) liegen im Utility-Layer und verlieren dagegen. Am Handy (390 px) misst ein `Input.Small` 14 px statt 16 px, iOS zoomt beim Fokus ein (Regel aus ADR 0005). Betroffen sind alle Aufrufer von `Input.text` mit `Size = Small` (Suchfelder der Auswahlen, Memo, Betrag). Das Datumsfeld umgeht es lokal mit `text-base! md:text-sm!`.

## What
- Ursache beheben (z. B. `font-size` aus `.input-field` entfernen oder `Input.Small` mit `!`-Varianten ausstatten) und prüfen, dass Medium/Large (Inline 16) und andere `.input-field`-Nutzer unverändert aussehen.
- Danach kann `DateField.textSizeClass` entfallen.

## Acceptance criteria
- [ ] Playwright misst bei 390x844 in einem `Input.Small`-Feld (Kategorie-Suche) 16px und bei 1440x900 14px.
- [ ] `Input.usesInlineFontSize Small` bleibt falsch.
- [ ] Medium und Large messen bei 390x844 und 1440x900 dieselbe Schriftgröße wie vor dem Fix (Playwright, je ein Feld).
- [ ] `DateField.textSizeClass` ist entfernt, das Datumsfeld misst bei 390x844 weiterhin 16px und bei 1440x900 14px.
- [ ] `npm run guard:classes`, `npm run build`, `npm run test:scripts`, `dotnet build`, `dotnet test` grün.