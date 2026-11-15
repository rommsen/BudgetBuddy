---
id: design-system-x1abn
title: "Logo und Icon: Buddy aus standards/frontend/brand/ als Marke und PWA-Icon"
status: backlog
type: feature
context: design-system
created: 2026-11-11
completed:
depends_on: [design-system-001-styleguide]
blocks: []
tags: [etappe-3, frontend, marke]
related_adrs: [0087]
related_research: []
prior_art: []
---

## Why
BB hat noch kein eigenes Logo. Ein Entwurf liegt seit 2026-11-07 vor (Palette 2).

## What
Buddy aus `standards/frontend/brand/` wird Marke in der Navigation und Icon der PWA.

## Acceptance criteria
Entwurf (2026-11-15, Conductor); offen sind die Fragen F1 bis F3 unten, beantwortet in der BB Prüfliste.

Vorlage
- [ ] Saubere Vektorfassung von Kopf und ganzer Figur als SVG unter `src/Client/public/` (F1), in Palette 2 aus
      `standards/frontend/brand/README.md`: Grund `#08081a`, Körper Petrol `#1e5d6b`, Visier, Augen, Lächeln und Kanten
      Cyan `#28d6df`, Ohren und Münze Gold `#cdb27a`. Kein Orange, kein Verlauf, kein Glühen. Münze mit dem Zeichen aus F2,
      Düse nach F3.
- [ ] Die Farben kommen aus den Tokens (`Tokens.fs`, `--accent` usw.), wo die Marke in der App steht; in den PWA-Dateien
      als feste Werte, weil das Manifest keine Tokens kennt.

In der App
- [ ] Seitenleiste ab 1024 px (`Navigation.brand`): der Kopf ersetzt das Zeichen „B“ im 30-px-Feld, daneben der
      Schriftzug „Budget“ in `text-primary` und „Buddy“ im Akzent (README, Regeln). Am Handy und in der Leiste
      768 bis 1023 px dort, wo heute die Marke steht.
- [ ] Unter 64 px nur der Kopf, nie die ganze Figur (README).

PWA und Browser
- [ ] App-Symbol aus der ganzen Figur: `icon-192.png`, `icon-512.png`, `apple-touch-icon.png` (180 px, deckend),
      `maskable-512.png` (Figur in der sicheren Zone, 76 %, Grund `#08081a`).
- [ ] Favicon aus dem Kopf: `favicon.svg`, `favicon-16.png`, `favicon-32.png`, `favicon.ico`; bei 16 px vereinfacht
      (ohne Glanz und Kanten), erkennbar wie die „Micro-Icon-Variante“ im Entwurf.
- [ ] `icon-master.svg` und `icon-maskable.svg` ersetzt, `src/Client/public/icons/README.md` auf das neue Zeichen
      umgeschrieben (Herkunft, Farben, wie die Größen entstehen); das alte „B im Sync-Ring“ entfällt.
- [ ] Manifest unverändert in Name, `theme_color` und `background_color` (`#08081a`).

Tests und Abgleich
- [ ] Dateinamen und Größen der Icons wie heute (`vite.config` `includeAssets`, `manifest.icons`), `npm run build`
      grün; kein eigener Test für Bilddateien (Test-Doktrin Punkt 6), die Größen prüft der Abgleich im Outcome.
- [ ] `/styleguide` zeigt Marke und Kopf in 24, 30 und 64 px.
- [ ] Neben dem Entwurf verglichen: Screenshots der Seitenleiste bei 1440 px, des Handy-Kopfs bei 412 px und der Icons
      (16, 32, 192, 512, maskable) im Outcome; Abweichungen nur die genannten (F1 bis F3, kein Schriftzug-Grün und -Gold).

## Offene Fragen (für Roman, in der BB Prüfliste)
- F1 Vektorfassung: (a) Claude zeichnet Kopf und Figur als flaches SVG nach der Vorlage nach und legt eine Vorschau in
  die Prüfliste, bevor gebaut wird (Empfehlung); (b) Roman liefert ein SVG; (c) die PNG-Vorlage hochrechnen (316 px
  reichen für 512 px nicht, unscharf).
- F2 Münze: „€“ statt „$“ (Empfehlung, BB rechnet in Euro) oder „$“ wie im Entwurf, oder ohne Zeichen.
- F3 Düse: weglassen (Empfehlung, ruhiger und bei 192 px lesbarer) oder in Gold.

## Notes
Schnitt aus Etappe 3 (Fahrplan, Roman 2026-11-11). Marke im Akzent (Roman, 2026-11-08).
Vorlage: Prototyp https://claude.ai/artifact/CE3WJZHhPX6NABosJaviRg (Version 17). Baut der Schnitt eine Seite um, fliegen ihre `[flow]`-, `[tastatur]`-, `[fokus]`- und `[scroll]`-Tests raus und werden gegen die Muster aus dem Styleguide neu geschrieben, je Flow einer.
