---
id: design-system-dak16
title: "Palette 2 im Token-Layer: ein Akzent für Aktion und Auswahl, Gelb für braucht dich, keine Seitenausnahmen, Kontrast-Wächter"
status: done
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

## Verifier note (iteration 1)

**VERDICT:** FAIL (likely-fixable). Gate selbst gelaufen: `./verify.sh` PASS (dotnet 4289, node 295), contrast-guard 6/6, Sweep 22/22, README-Delta-Gate Exit 0. Kriterien 1 bis 5 und 7 erfüllt; Pink-Regel (5850313) per Rückdrehung belegt.

**REASONS**
- Orange als Warnung bleibt: `src/Client/styles.css` 2019 bis 2026 (`.info-banner.warning`, `.info-banner-icon.warning`, `rgba(255, 138, 36, …)`) und 2046 (`.info-banner-action:hover`), genutzt in `SyncFlow/Views/TransactionList.fs:155`. Ohne Aufrufer: `.gradient-bg` (367) mit Lila-Hex. `public/offline.html` 78 und 80: alte Glows in Grün und Teal. Kein Wächter liest Farbliterale in CSS.
- Kriterium 6: §2 „Bewegungslisten der Budget-Historie“ macht einen Seitennamen zur Bedingung einer Farbe (Betrag neutral, sonst „positiv im Akzent“).
- Eingänge im Akzent (offene Abweichung 1) widersprechen ADR 0087 Punkt 1 und 2 und dem Prototyp (Eingänge neutral); die Task nennt keine Abweichung, also gilt die Vorlage (CLAUDE.md, Ablauf).
- Fokusring im Akzent widerspricht der Tabelle der Task und ADR 0087 Punkt 5 (`border-strong`).
- Ohne Test: `Badge.Warning` und `ErrorDisplay.warning` könnten wieder eine andere Rolle zeichnen.
- Klein: Assert-Texte „Orange“ in `SyncFlowViewTests.fs:166/197`, Akzent-Glow an `Card.fs:51`, README-Abschnitt App-Mark nennt Orange.

**Vorgabe des Conductors für Iteration 2:** Eingänge neutral (Vorlage), Fokusring `border-strong` (Task und ADR); beides wird Roman danach als Frage vorgelegt. Abweichung 3 (Lena-Farbe) ist zulässig, keine Kann-Zeile nötig.

## Outcome

Palette 2 (ADR 0087 Punkte 1 bis 8) ist im Token-Layer umgesetzt; alle Seiten ziehen über die Tokens mit. `./verify.sh` nach Iteration 2: VERIFY: PASS (dotnet 4293 grün, node 298 grün inklusive Layout-Sweep, dotnet build, npm run build, guard:classes, guard:tokens).

**Tokens** (`src/Client/styles.css`, `@theme`): `--color-accent #28d6df`, `--color-on-accent` (= `var(--bg-app)`), `--color-attention #cdb27a`, `--color-error #fd4f75`, `--color-success #5be479`, `--color-info #7ab8ff`, `--color-border-strong` (= `--border-strong #6a6c86` in `:root`), dazu `--color-flag-red/orange/yellow/green/blue/purple` nur für das Flag-Symbol. Glow-Schatten nur noch `glow-accent/success/error`, über `color-mix` aus dem Token statt eigener rgba-Werte. Alle Farbliterale in `styles.css` außerhalb von `@theme` und `:root` laufen jetzt über `var(--…)` oder `color-mix` auf Tokens (Iteration 2 dazu: `.info-banner.warning`, `.info-banner-icon.warning` und `.info-banner-action:hover` vom Orange `rgba(255, 138, 36, …)` auf `attention`, die Aktion im gelben Hinweis trägt ebenfalls Gelb; `.glass-card`, Scrollbar, Aktionsleiste, Overlays, `.badge-duplicate`, `.info-banner` über `bg-card`, `text-primary`, `bg-app`, `border-strong`; `.gradient-bg` samt `@keyframes gradientShift` und `.gradient-text` ohne Aufrufer entfernt; Hex-Fallbacks in `var(…, #…)` entfernt). Übrig bleibt nur Schwarz mit Deckkraft für Schatten und Scrims. `src/Client/public/offline.html`: Retry-Knopf als Hauptaktion im Akzent, Glow über `color-mix` statt der alten Grün/Teal-rgba. `neon-teal`, `neon-green`, `neon-red`, `calm-yellow`, `calm-blue` sind in `accent`, `success`, `error`, `attention`, `info` umbenannt (Client, Tests, Skripte, Styleguide). `src/Client/DesignSystem/Tokens.fs`: `Colors.accent/attention/error/success/info`, `Colors.onAccent`, `Backgrounds.accent`, `Borders.strong` (vorher `border-white/15`), `Borders.accent/attention/success/error`, `Glows.accent/accentLg/success/error`, `TextGlows.success/accent`. `grep -rE "neon-orange|neonOrange|neon-pink|neonPink|neon-purple|neonPurple|calm-violet|calmViolet" src/Client` findet nichts. In `scripts/token-guard-exceptions.json` sind sieben Einträge entfallen (Hex/rgb in Tokens, Button, Card, Loading, Input), kein neuer.

**Bausteine:** `Button.primary` und `Button.hero` (neu `Button.heroClass`) Akzent gefüllt, Text `on-accent`, Symbole im Knopf dunkel (`[&_svg]:text-on-accent`); `Button.heroTeal`/`heroTealWithIcon` entfallen (nur der Styleguide nutzte sie). `Button.secondary` und Eingabe-/Auswahlfelder (`.input-field`, `.select-field`, Kästchen `.proto-toggle`, Suchfeld im `searchableSelect`) tragen `border-strong`. **Fokusring** (Iteration 2): `*:focus-visible`, `.btn`, Links, Karten, Nav und alle `:focus`-Regeln der Felder in `styles.css` (19 Stellen) sowie `Table` (Sortierkopf) und das Suchfeld in `Input.fs` zeichnen ihn in `border-strong` statt im Akzent; Breite und Abstand unverändert. `Button.danger` Rand in Rot. Badge: `Warning` ist Gelb (vorher Pink; Klassen jetzt öffentlich über `Badge.variantClass`), `Notice`, `Orange`, `Purple` entfallen (Aufrufer auf `Warning`), `Info` ist Blau, `Badge.count` Gelb wie im Prototyp, gefülltes `Error` mit dunklem Text. `Toast.Warning` (`Toast.variantClass`), `ErrorDisplay.warning`/`notice` (gemeinsam `ErrorDisplay.attentionBoxClass` und `attentionIconColor`), `Form`-Hinweise, `Detail.notice` (`NoticeOrange` entfällt, `NoticeYellow`) sind Gelb. `Icons.IconColor`: `Accent/Success/Warning(=Gelb)/Error/Info/LenaFlag`, die `Neon*`-Fälle entfallen. `Loading.SpinnerColor` `Default/Accent/Success`, `Stats.StatAccent` `Accent/Success/Error/Gradient/NoAccent`, `Stats.SparkOver` Gelb. Zeitleiste im Detail: `Detail.DotColor` `DotInfo/DotGrey/DotGreen/DotRed` (Import und Regel Blau, Spiegel, Quick Add und Bob Grau). Logo in der Navigation: Akzent-Rand statt Orange-Verlauf; der Schriftzug wie im Prototyp „Budget“ hell, „Buddy“ im Akzent (vorher Verlauf). `Card.Glow` ohne Glow (ADR 0087 Punkt 6). Fehler-Kopf von `ErrorDisplay.hero` und Sync-Startkarte ohne Orange/Pink-Verlauf.

**Beträge in Listen** (Iteration 2, ADR 0087 Punkt 2 und Prototyp): Eingänge sind neutral in `text-primary` wie jeder Listenbetrag (`Money.listAmountClass`, damit `Table.amountCellClass`, `Money.signedListAmountClass`), ebenso der Betrag im Detail-Kopf (`Detail.amount`) und die Summe der Mehrfachauswahl (`MultiSelect.amountClassName`). Das Vorzeichen „+“ bleibt.

**Kassenbuch:** „zu prüfen“ ist die 3-px-Kante in Gelb (`ListRoles.statusEdge` über `var(--color-attention)`) mit dem Wort in Gelb, „ohne Kategorie“ und die „Erneut senden“-Hinweise in Gelb, „Nicht in YNAB“ Gelb, *Markiert* neutral. **Lena:** neuer Baustein `Badge.lenaMark` (Flag-Symbol mit `role="img"`, Name „Flag“, und Text mit „Lena“, neutral, kein Badge). Die Farbe aus `lena_flag_color` setzt die Seite als `data-lena-flag` (Kassenbuch über einen `contents`-Rahmen um die Seite, Import prüfen am Review-Container), `styles.css` bildet sie auf `--color-flag-*` ab; ohne Farbe ist das Symbol gedämpft. In der PC-Statusspalte steht „Lena“ statt „Lena?“, damit die 128-px-Zelle passt.

**Targets prüfen** (`TargetCheckView.fs`): „Gibst du mehr aus?“ Gelb, „weniger“ Blau, „nie“ und „Kein Target“ neutral, „Passt“ Grün; Monate über dem Target Gelb, Legende „gelb: über dem Target“; Zähler Hinweise Gelb, Passt Grün; der Zusatz unter „Passt“ neutral.

**Tests:**
- `scripts/contrast-guard.test.mjs` (neu, 6 Tests): liest `styles.css`, prüft Text- und Rollenfarben 4,5:1, `border-strong` und Flag-Farben 3:1 gegen `bg-card/elevated/hover`, `on-accent` auf Akzent 4,5:1. Gegenproben im Test (`#6a6c86` als Textfarbe auf `bg-hover` wird gemeldet; Weiß auf altem Orange 2,84:1) und von Hand: `--color-error` auf `#6a6c86` gesetzt, der Wächter meldet „--color-error auf --bg-card: 3.62:1“, danach zurückgesetzt.
- `src/Tests/FarbrollenTests.fs` (neu, 7 Tests): `Button.primary` und `Button.heroClass` enthalten `Backgrounds.accent` und `Colors.onAccent` (über die Tokens), kein `text-white`/Verlauf; `Button.secondary` trägt `Borders.strong`. Iteration 2: `Badge.Warning` trägt in jedem Stil Gelb und keine andere Rollenfarbe, `ErrorDisplay.attentionBoxClass` Gelb und `Icons.colorToClass ErrorDisplay.attentionIconColor = Colors.attention`, `Toast.Warning` Gelb; die Rolle wird aus `Colors.attention` abgeleitet. Gegenprobe: `Badge.Warning` (Soft) lokal auf `bg-error/10 text-error border-error/30` gesetzt, der Test wurde rot („Soft: bg-error/10 … Actual value was false“), danach zurückgesetzt und grün.
- `scripts/token-guard.mjs` neues Muster `css-farbliteral` (Iteration 2): meldet Hex, rgb/rgba, hsl/hsla in `src/Client/styles.css` außerhalb von `@theme` und `:root` (auch `:root` in `@media`), Kommentare und Daten-URLs zählen nicht, erlaubt ist nur `rgba(0, 0, 0, a)` für Schatten und Scrims, keine Ausnahmeliste. 3 Tests in `scripts/token-guard.test.mjs` (Orange in einer Klasse gemeldet mit Zeile; Tokens, color-mix, Schwarz, Kommentar, Daten-URL erlaubt; Lauf scheitert mit Datei und Zeile). Doktrin Punkt 8: den Fund aus Iteration 1 lokal wiederhergestellt (`.info-banner.warning { background: rgba(255, 138, 36, 0.08) }`), `node scripts/token-guard.mjs` meldete „src/Client/styles.css:2008 [css-farbliteral] rgba(255, 138, 36, 0.08)“, danach zurückgesetzt und grün.
- `scripts/ledger-lena.test.mjs` `[flow:lena-fragen]` erweitert: Zeile zeigt `lena-mark` mit Rolle `img` „Flag“ und „Lena“, das Symbol hat die berechnete Farbe von `--color-flag-purple`, der Text nicht, kein `.rounded-full` drumherum. `ledger-lena-batch.test.mjs` liest das Kennzeichen über `lena-mark`.
- Angepasst: `LedgerViewTests` („ohne Kategorie“ Gelb, Bindung an `--color-attention`; Iteration 2: positiver Listenbetrag `text-primary` ohne Akzent), `RuhigeListenTests` (Kante über `var(--color-attention)`; Iteration 2: positiv neutral in `listAmountClass` und `Table.amountCellClass`), `SyncFlowViewTests` (Assert-Texte Gelb statt Orange), `StatsAccentTests`, `TextGlowTokenTests` (Token statt rgba), `TargetCheckClientTests`, `TargetsApplyClientTests`, `LedgerDetailTests` (Punktfarben), `ImportAskLenaClientTests`, `export-design-system.test.mjs`.

**Pink-Regel (Fix 5850313) umgezogen:** `budget-ohne-pink` in `scripts/token-guard.mjs` und `measurePink`/`PINK_RULE` samt Gegenbeweis-Test im Layout-Sweep (`scripts/layout-invariants.mjs`, `.test.mjs`) sind entfernt; es gibt keinen Pink-Token mehr, der die CSS-Variable liefern könnte. Neu ist das Muster `entfallene-farbe` im Token-Wächter: gilt überall unter `src/Client/`, auch im DesignSystem, und lässt sich nicht per Ausnahmeliste erlauben (8 Fälle × 3 Orte plus Ausnahmelisten-Test in `scripts/token-guard.test.mjs`). Doktrin Punkt 8: den Bug-Zustand lokal wiederhergestellt (in `src/Client/DesignSystem/Badge.fs` `Warning -> "bg-neon-pink/10 text-neon-pink border-neon-pink/30"` wie vor 5850313; die Budget-Seite nutzt heute `Badge.warningSmall`/`Badge.Warning`), `node scripts/token-guard.mjs` wurde rot („src/Client/DesignSystem/Badge.fs:58 [entfallene-farbe] neon-pink“), danach zurückgesetzt und grün.

**Styleguide** (`standards/frontend/styleguide.md`): §2 neu als Tabelle Farbe, Bedeutung, Token ohne Seitenausnahmen, mit Kontrastregel und praktischen Konsequenzen; in §2 steht kein „Ausnahme“ mehr (die Budget-Besonderheiten sind als Aufbau ohne Farbe benannt, Rot und Gelb in einer Zeile als allgemeine Regel „zwei Zustände, beide mit Farbe“). Iteration 2: Fokusring `border-strong` in Tabelle und Konsequenzen, Betrag in „Ruhige Listen“ neutral auch positiv, der Abschnitt „Bewegungslisten der Budget-Historie“ entfällt (kein Seitenname als Bedingung einer Farbe). §3 nennt die Rollen-Tokens. Satz in der Einleitung von §7 auf den neuen Stand gebracht. `/styleguide` Abschnitt 1 zeigt die Farbrollen mit Beispielen (Knopf, Auswahl-Tab, Zeile zu prüfen, Minus, Rückmeldung, Eingabefeld). `scripts/export-design-system.mjs` exportiert die Rollen statt der Neon-Farben (und überspringt im @theme selbst deklarierte Variablen beim Auflösen).

**Vergleich mit dem Prototyp [human-eye]:** Screenshots von BB mit den Mocks des Layout-Sweeps (keine echten Daten), statischer Build:
- /private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/11e99da9-912c-40fc-980f-5dc7a3c80a6d/scratchpad/dak16-bb/home-390x844.png, /private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/11e99da9-912c-40fc-980f-5dc7a3c80a6d/scratchpad/dak16-bb/home-1440x900.png (Übersicht = heute Sync)
- /private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/11e99da9-912c-40fc-980f-5dc7a3c80a6d/scratchpad/dak16-bb/ledger-390x844.png, /private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/11e99da9-912c-40fc-980f-5dc7a3c80a6d/scratchpad/dak16-bb/ledger-1440x900.png
- /private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/11e99da9-912c-40fc-980f-5dc7a3c80a6d/scratchpad/dak16-bb/budget-390x844.png, /private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/11e99da9-912c-40fc-980f-5dc7a3c80a6d/scratchpad/dak16-bb/budget-1440x900.png
- /private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/11e99da9-912c-40fc-980f-5dc7a3c80a6d/scratchpad/dak16-bb/tcheck-390x844.png, /private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/11e99da9-912c-40fc-980f-5dc7a3c80a6d/scratchpad/dak16-bb/tcheck-1440x900.png
- /private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/11e99da9-912c-40fc-980f-5dc7a3c80a6d/scratchpad/dak16-bb/styleguide-390x844.png, /private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/11e99da9-912c-40fc-980f-5dc7a3c80a6d/scratchpad/dak16-bb/styleguide-1440x900.png (Farbrollen-Beispiele)
Prototyp daneben: /private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/11e99da9-912c-40fc-980f-5dc7a3c80a6d/scratchpad/dak16-prototyp/shots/. Ich habe Kassenbuch, Targets prüfen, Budget und Übersicht bei 1440 und die Übersicht bei 390 angesehen (Screenshots nach Iteration 2 erneuert): Akzent an Hauptknopf, aktivem Nav-Punkt, Filter-Chip und „Buddy“ im Schriftzug, Gelb an Kante, „zu prüfen“, „ohne Kategorie“, „fehlt“ und Balken über dem Target, Blau bei „weniger“, Grün bei „Passt“, Rot bei Minus, Beträge neutral stimmen mit dem Prototyp überein. Layout und Orte weichen ab (Etappe 3). Ein Blick von Roman steht noch aus. Farbabweichungen zum Prototyp: keine offenen mehr. Die Abweichungen 1 (Eingänge im Akzent) und 2 (Fokusring im Akzent) aus Iteration 1 sind behoben: Eingänge neutral wie im Prototyp, Fokusring `border-strong` wie in Task und ADR 0087 Punkt 5 (der Prototyp zeigt den Fokus im Akzent; das kann Roman entscheiden). Abweichung 3 (Lena-Farbe nur im Kassenbuch und in Import prüfen) ist nach Verifier zulässig.

**ADR-Nachträge (vom Conductor einzutragen):**

ADR 0075 (`0075-farbrollen-gelb-und-neutrale-knoepfe.md`), am Ende vor „## Consequences“ anfügen:

> ## Nachtrag 2026-11-08: ersetzt durch ADR 0087 (design-system-dak16)
> Die Farbrollen dieses ADR und der Nachtrag „Ausnahme Seite Targets prüfen“ vom 2026-11-06 sind durch ADR 0087 (Palette 2) ersetzt. Es gibt keine Seitenausnahmen mehr: Gelb (`attention`) heißt auf jeder Seite „braucht dich, kein Fehler“, der Akzent (`accent`, `#28d6df`) steht für Hauptaktion und Auswahl/Navigation, Orange und Pink entfallen. Auf Targets prüfen ist „Gibst du mehr aus?“ Gelb, „weniger“ Blau, „nie“ und „Kein Target“ neutral, „Passt“ Grün, Monate über dem Target Gelb. Punkt 2 (Standard-Knöpfe neutral) gilt weiter, ihr Rand ist jetzt `border-strong`. Punkt 4 gilt allgemein: trägt eine Zeile zwei Zustände (Minus und `fehlt`), zeigen beide ihre Farbe. Token-Namen: `calm-yellow` heißt `attention`, `calm-blue` heißt `info`.

ADR 0049 (`0049-ruhige-listen-farbe-als-signal.md`), am Ende anfügen:

> ## Nachtrag 2026-11-08: Farben durch ADR 0087 ersetzt (design-system-dak16)
> Die Farbangaben dieses ADR folgen jetzt ADR 0087 (Palette 2). Punkt 4: „Keine Kategorie“ ist Gelb (`attention`), nicht Orange. Punkt 7: „Zu prüfen“ ist eine 3-px-Kante in Gelb mit dem Wort, nicht Pink; Pink gibt es nicht mehr. Punkt 2: Eingänge sind neutral in `text-primary` wie jeder Listenbetrag, nicht mehr Teal; der Akzent gehört nach ADR 0087 Punkt 2 der Hauptaktion und der Auswahl. Struktur, Größen und „ein Akzent pro Zeile“ gelten weiter.

**Kann-Sammlung design-system (Zeilen):**
- PC-Statusspalte im Kassenbuch: „zu prüfen“ plus ein zweites Kennzeichen steht bündig am Betrag (Zeilen „Rückfrage und Prüfung“, „Markiert und zu prüfen“ im Sweep-Zustand `kassenbuch` bei 1440); der Sweep meldet es nicht. Schon vor dieser Task so eng (das alte Badge „Lena?“ war breiter).
- `Loading.neonPulse` und `Loading.progressBar` glühen noch; nach ADR 0087 Punkt 6 nur Hauptaktion und Rückmeldung.
- §1 des Styleguides („Glow … positive Beträge, fokussierte Karten“, „Neon-Akzente“) auf ADR 0087 bringen; §1 war nicht Teil dieser Task.
- Die App-Icons (`src/Client/public/icon-master.svg`, `favicon.svg`, Raster unter `public/icons/`) tragen noch den alten Verlauf Teal→Grün→Orange.
