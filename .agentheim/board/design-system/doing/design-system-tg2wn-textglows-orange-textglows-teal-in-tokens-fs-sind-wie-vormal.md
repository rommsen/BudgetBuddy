---
id: design-system-tg2wn
title: "TextGlows.orange/TextGlows.teal in Tokens.fs sind wie vormals TextGlows.green keine echten Tailwind-Klassen"
status: doing
type: chore
context: design-system
created: 2026-09-25
depends_on: []
blocks: []
tags: [tech-debt, styleguide]
related_adrs: [0031, 0041]
related_research: []
prior_art: [design-system-thtpp]
---

## Why
`design-system-thtpp` fand und behob, dass `Tokens.fs`s `TextGlows.green` (Literal
`"text-glow-green"`) weder eine echte Tailwind-Utility noch in `styles.css` definiert
war, derselbe stille Ausfall wie die DaisyUI-Klassen aus ADR 0031, nur in einer anderen
Komponente. Der Fix ersetzte den Literal-Wert durch die Arbitrary-Property-Syntax
`[text-shadow:...]`, die `Money.fs`s `getGlowClass` für den negativen Fall bereits
funktionierend nutzte (siehe ADR 0041).

`TextGlows.orange = "text-glow-orange"` und `TextGlows.teal = "text-glow-teal"`
(`src/Client/DesignSystem/Tokens.fs`, direkt neben `green`) tragen exakt dasselbe Muster:
weder in `styles.css` definiert noch eine echte Tailwind-Utility. Der
Tailwind-Klassen-Wächter (`scripts/tailwind-class-guard.mjs`, `design-system-thtpp`)
meldet sie NICHT, weil `git grep` zum Zeitpunkt dieses Fundes keinen Aufrufer außerhalb
von `Tokens.fs` selbst zeigt: der Wächter prüft nur tatsächlich benutzte
`prop.className`-Aufrufstellen, nicht jedes `Tokens.fs`-Literal isoliert. Bleiben also
unbemerkt, bis ein künftiger Aufrufer sie tatsächlich verwendet und denselben stillen
Ausfall erlebt, den ADR 0031 und ADR 0041 schon zweimal beheben mussten.

## What
- `Tokens.fs`s `TextGlows.orange`/`TextGlows.teal` (Zeilen 104-105, direkt neben `green`
  in Zeile 103) auf dieselbe Arbitrary-Property-Syntax umstellen wie `TextGlows.green`
  (ADR 0041). Geprüfte rgba-Werte aus `src/Client/styles.css` (Zeilen 34/35):
  `--shadow-glow-orange: 0 0 20px rgba(255, 107, 44, 0.5)`,
  `--shadow-glow-teal: 0 0 20px rgba(0, 212, 170, 0.5)`. Analog zu `green`s
  `"[text-shadow:0_0_10px_theme(colors.neon-green),0_0_20px_rgba(0,255,136,0.5)]"` also:
  - `orange = "[text-shadow:0_0_10px_theme(colors.neon-orange),0_0_20px_rgba(255,107,44,0.5)]"`
  - `teal = "[text-shadow:0_0_10px_theme(colors.neon-teal),0_0_20px_rgba(0,212,170,0.5)]"`
- Geprüft: `git grep -n "TextGlows\.\(orange\|teal\)" -- src/` findet aktuell (2026-10-02)
  keinen Aufrufer außerhalb von `Tokens.fs` selbst. Bei der Umsetzung erneut prüfen, ob
  zwischenzeitlich einer hinzugekommen ist; falls ja, dessen Rendering vor/nach dem Fix
  visuell vergleichen (analog dem `/styleguide`-Vorher/Nachher-Vergleich in
  `design-system-fq8dn`).
- Geprüft: `npm run guard:classes` ruft `scripts/tailwind-class-guard.mjs` auf
  (`package.json`, Zeile 14), `npm run test:scripts` (`node --test scripts/*.test.mjs`)
  schließt `scripts/tailwind-class-guard.test.mjs` mit ein. Pfade/Skriptnamen im Task
  stimmen, keine Korrektur nötig.
- Falls dabei ein allgemeineres Muster auffällt (mehr `Tokens.fs`-Literale ohne
  Aufrufer, die dasselbe Risiko tragen): das gehört in eine eigene, breiter angelegte
  Aufgabe, nicht in diese.

## Acceptance criteria
- [ ] `TextGlows.orange`/`TextGlows.teal` sind entweder eine echte Tailwind-Utility oder
      über die Arbitrary-Property-Syntax `[text-shadow:...]` mit denselben rgba-Werten
      wie die jeweilige `--shadow-glow-*`-Theme-Variable definiert.
- [ ] `npm run guard:classes`/`dotnet test`/`npm run build` bleiben grün.
- [ ] Falls ein Aufrufer gefunden wurde: visuelle Gleichheit vor/nach dem Fix belegt
      (Screenshot-Vergleich oder Code-Äquivalenz-Argument, analog `design-system-fq8dn`).