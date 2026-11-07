---
id: design-system-dak16
title: "Palette 2 im Token-Layer: ein Akzent für Aktion und Auswahl, Gelb für braucht dich, keine Seitenausnahmen, Kontrast-Wächter"
status: backlog
type: feature
context: design-system
created: 2026-11-07
completed:
depends_on: [design-system-001, infrastructure-jc1y1]
blocks: []
tags: [etappe-1, frontend, tokens, farben, kontrast, styleguide]
related_adrs: [0087, 0075, 0077, 0049, 0086]
related_research: [farbschema-neon-dark-2026-11-07]
prior_art: [design-system-zahv9]
---

## Why
Etappe 1 des Fahrplans (ADR 0086). Farben haben heute keine feste Bedeutung: Orange ist mal
Hauptaktion, mal Warnung, Targets prüfen hat vier Seitenausnahmen, Pink gilt nur im Kassenbuch,
Gelb nur im Budget. Dazu fallen Kontraste durch: Weiß auf `neon-orange` 2,84:1, `neon-purple`
4,06:1 und `neon-pink` 4,47:1 auf `bg-hover`, `border` 1,35:1 (Recherche
`farbschema-neon-dark-2026-11-07`, nachgerechnet). Roman hat Palette 2 am Prototyp gewählt
(ADR 0087 Punkte 1 bis 8).

## What
Die Farbrollen aus ADR 0087 werden Tokens; jede bestehende Seite zieht über die Tokens mit. Layout,
Orte und Abläufe bleiben, wie sie sind (Etappe 3).

| Rolle | Wert | ersetzt |
|---|---|---|
| Akzent: Hauptaktion und Auswahl/Navigation | `#28d6df`, Text darauf `bg-app` | `neon-orange` als Aktion, `neon-teal` |
| Braucht dich, kein Fehler | `#cdb27a` (heutiges `calm-yellow`) | `neon-pink`, Orange als Warnung |
| Fehler und Minus | `#fd4f75` | `neon-red` |
| Gelungen | `#5be479` | `neon-green` |
| Hinweis | `#7ab8ff` (heutiges `calm-blue`) | |
| Rand, der ein Bedienelement erkennbar macht, Fokusring | `border-strong` `#6a6c86` | `border` an Eingabe- und Auswahlfeldern |

Flächen (`bg-*`), Textstufen (`text-*`), `border` und `border-subtle` bleiben.

Konkret:
- `src/Client/styles.css` und `src/Client/DesignSystem/Tokens.fs`: semantische Tokens nach der
  Tabelle. `neon-orange`, `neon-pink`, `neon-purple` und `calm-violet` verschwinden aus Tokens und
  Client.
- `Button.primary` und `Button.hero` in Akzent mit dunklem Text; `Button.heroTeal` geht in
  `Button.hero` auf. Glow nur an Hauptaktion und Rückmeldung.
- Kassenbuch: „zu prüfen“ als 3-px-Kante in Gelb mit dem Wort; „ohne Kategorie“ in Gelb.
- Targets prüfen: Gruppenkopf „Gibst du mehr aus?“ Gelb, „weniger“ Blau, „nie“ und „Kein Target“
  neutral, „Passt“ Grün; Monate über dem Target in Gelb. Die Ausnahmen aus dem Nachtrag zu ADR 0075
  entfallen.
- Lena: Die Flag-Farbe aus `lena_flag_color` erscheint nur im Flag-Symbol, immer mit dem Namen
  „Lena“. Kein lila Badge mehr.
- Kontrast-Wächter `scripts/contrast-guard.test.mjs`: liest die Farbwerte aus `src/Client/styles.css`
  und prüft nach WCAG 2: jede Text- und Rollenfarbe mindestens 4,5:1 gegen `bg-card`, `bg-elevated`,
  `bg-hover`; `border-strong` mindestens 3:1 gegen dieselben; `bg-app` auf Akzent mindestens 4,5:1.
- Styleguide `standards/frontend/styleguide.md` §2 neu: eine Tabelle Farbe, Bedeutung, Token, ohne
  Seitenausnahmen. Nur §2 und §3; die Muster schreibt `design-system-bcdqy` (§4, §5, §7).
- `/styleguide` zeigt die Farbrollen mit Beispiel (Knopf, Auswahl, Zeile zu prüfen, Minus, Rückmeldung).
- ADR 0075 und ADR 0049 bekommen einen Nachtrag, dass die Farbregeln durch ADR 0087 ersetzt sind.

## Acceptance criteria
- [ ] `grep -rE "neon-orange|neonOrange|neon-pink|neonPink|neon-purple|neonPurple|calm-violet|calmViolet" src/Client` findet nichts.
- [ ] Die Tokens haben die Werte der Tabelle; `npm run guard:tokens` und `npm run guard:classes` sind grün, ohne neue Einträge in den Ausnahmelisten.
- [ ] `scripts/contrast-guard.test.mjs` prüft die genannten Paare und ist grün. Gegenprobe: ein Testfall mit einem absichtlich zu dunklen Wert (etwa `#6a6c86` als Textfarbe gegen `bg-hover` mit 4,5:1) schlägt fehl.
- [ ] `Button.primary` und `Button.hero` rendern Akzent mit Text in `bg-app`; ein Test prüft die Klassen über die Tokens, nicht über Hex-Werte.
- [ ] Im Kassenbuch trägt eine ungeprüfte Zeile die gelbe Kante und das Wort „zu prüfen“; eine Buchung mit Lena-Flag zeigt Flag-Symbol und „Lena“ ohne farbiges Badge (Test auf Rolle und Text).
- [ ] Styleguide §2 enthält keine Zeile mit „Ausnahme“ oder einem Seitennamen als Bedingung für eine Farbe.
- [ ] ADR 0075 und ADR 0049 verweisen in einem Nachtrag auf ADR 0087.
- [ ] Seiten neben dem Prototyp verglichen, Abweichungen nur die genannten: Übersicht (heute Sync), Kassenbuch, Budget, Targets prüfen, je 390 × 844 und 1440 × 900, Screenshots im Outcome. Verglichen werden nur Farben; Layout und Orte weichen ab, weil sie zu Etappe 3 gehören. [human-eye]
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

## Notes
- Vorlage: Prototyp https://claude.ai/artifact/CE3WJZHhPX6NABosJaviRg (Version 14, Palette 2 in
  `:root`), Skizze https://claude.ai/artifact/HPpe24Y1owgpSZLWvtdGWd (Version 2, Abschnitt 1 und 2).
  Worker und Verifier können Artifacts nicht öffnen; der Conductor legt die Dateien und je Ansicht
  einen Screenshot in den Scratchpad (CLAUDE.md, Ablauf).
- Abhängig von `infrastructure-jc1y1`: Erst wenn die Designwert-Tests weg sind, schlägt ein
  Farbwechsel nicht in Dutzenden `[layout]`-Tests an.
- Umfang heute: außerhalb von `src/Client/DesignSystem/` 13 Stellen `neon-orange`, 9 `neonOrange`,
  4 Pink, 3 Purple, 1 `calmViolet` (Stand 2026-11-07).
- Keine Änderung an `src/Shared`, daher keine Fixtures nachzuziehen.
- Kontrastwerte der Palette (WCAG 2, gegen bg-card / bg-elevated / bg-hover): Akzent 10,38 / 9,46 /
  9,01; Gelb 9,03 / 8,23 / 7,84; Rot 5,77 / 5,26 / 5,02; Grün 11,30 / 10,30 / 9,81; Blau 8,93 / 8,14 /
  7,75; `border-strong` 3,62 / 3,29 / 3,14; `bg-app` auf Akzent 11,1.
