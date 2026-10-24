---
id: design-system-wfhxj
title: "Token-Wächter: test:scripts scheitert, sobald Feature-Code Hex- oder rgb-Farben in Klassen, Standard-Tailwind-Farben, inline Farbe/Schriftgröße oder text-[Npx] benutzt; das Design-System selbst läuft über eine begründete Ausnahmeliste"
status: done
type: chore
context: design-system
created: 2026-10-24
completed:
depends_on: []
blocks: []
tags: [frontend, design-system, waechter, tokens, styleguide]
related_adrs: [0031, 0049]
related_research: []
prior_art: [design-system-thtpp]
---

## Why
Die Design-Tafeln in `docs/ux/kassenbuch/tafeln/` zeigen den Stand vom September 2026 mit
festen Hex-Werten. Seitdem ist die Optik weitergegangen (ruhige Listen, ADR 0049; ruhige
Auswahl; gebautes Detail) und wurde bewusst nicht ins Design-Projekt zurückgespiegelt. Die
nächsten Frontend-Tasks (Kategorie ändern, Split-Editor, Lena fragen) beziehen sich auf
diese Tafeln. Ein Worker, der eine Tafel abmalt, käme heute durch alle Prüfungen: die
Token-Regel (`.claude/rules/design-tokens.md`, Styleguide §2/§3) ist nur Prosa, und
`npm run guard:classes` (`design-system-thtpp`) prüft nur, ob eine Klasse existiert;
`bg-[#16162e]` ist für Tailwind eine gültige Klasse. Entscheidung Roman, 2026-10-24: die
Regel "Optik nur aus Styleguide und Design-System" mechanisch absichern.

## What
Ein Node-Wächter `scripts/token-guard.mjs` mit Test `scripts/token-guard.test.mjs`, der in
`npm run test:scripts` läuft (dazu ein Skript `npm run guard:tokens`).

1. **Geprüfte Dateien:** alle `.fs` unter `src/Client/` außer `obj/` und `bin/`.
2. **Verbotene Muster** (in String-Literalen, also auch in `prop.className`, `$"..."` und
   Token-Konstanten):
   - Arbitrary-Werte mit Farbe: ein `-[` … `]`-Block, der `#` plus 3 bis 8 Hex-Zeichen oder
     `rgb(`/`rgba(`/`hsl(` enthält (z. B. `bg-[#0a0a0f]/80`, `to-[#e55a1f]`,
     `shadow-[0_0_25px_rgba(255,107,44,0.5)]`).
   - Standard-Tailwind-Paletten: `(text|bg|border|ring|from|to|via|fill|stroke|shadow|
     outline|divide|placeholder|accent|caret|decoration)-(red|green|blue|gray|slate|zinc|
     neutral|stone|orange|amber|yellow|lime|emerald|teal|cyan|sky|indigo|violet|purple|
     fuchsia|pink|rose)-<Stufe>`.
   - Inline-Stile: `style.color`, `style.backgroundColor`, `style.borderColor`,
     `style.fontSize`.
   - Arbitrary-Schriftgrößen: `text-[<Zahl>px]`, `text-[<Zahl>rem]`.
3. **Geltungsbereich:** Außerhalb von `src/Client/DesignSystem/` gilt null Toleranz.
   Innerhalb von `src/Client/DesignSystem/` (die Definitionsschicht) sind Treffer nur
   erlaubt, wenn sie in `scripts/token-guard-exceptions.json` mit Datei, Muster und Grund
   stehen. Stand 2026-10-24 sind das: `Tokens.fs` (Definitionsstelle, 9 Hex-Arbitrary und 8
   `text-[Npx]`), `Button.fs:70` (Orange-Verlauf und Glow des Primärknopfs), `Loading.fs:383`
   (Overlay-Fläche), `Input.fs` (drei `style.fontSize 16`, iOS-Zoom-Schutz, siehe Kommentar
   dort). Der Worker zählt beim Bau selbst nach und trägt den echten Stand ein.
4. **Feature-Code heute:** einziger bekannter Treffer außerhalb des Design-Systems ist ein
   `text-[Npx]` in `src/Client/Components/Styleguide/View.fs`. Er wird durch einen Token
   ersetzt (ein vorhandener aus `Tokens.fs`/`FontSizes` oder ein neuer im Design-System);
   eine Ausnahme für Feature-Code gibt es nicht.
5. **Meldung:** je Treffer Datei, Zeile, Muster und der Satz "Optik kommt aus
   standards/frontend/styleguide.md und src/Client/DesignSystem/, nicht aus Tafel-Werten".
   Exit 1 bei Treffern, Exit 2 wenn der Wächter selbst nicht urteilen kann (z. B.
   Ausnahmeliste ungültig), wie bei `readme-delta-check`.
6. **Ausnahmeliste veraltet:** steht in der Liste ein Eintrag, der keinen Treffer mehr hat,
   scheitert der Lauf ebenfalls (sonst wächst die Liste still).

Konvention, die dieser Task festschreibt: "Feature-Code nutzt keine Farb- oder
Schriftwerte außerhalb der Tokens". Durchsetzung: dieser Wächter in `npm run test:scripts`
(mechanisch, ADR-0059 erfüllt).

## Acceptance criteria
- [ ] `scripts/token-guard.test.mjs` belegt mit Fixture-Dateien (nicht gegen den echten
      Baum): je ein Treffer für Hex-Arbitrary, rgba-Arbitrary im Schatten, Paletten-Farbe,
      `style.color`, `style.fontSize`, `text-[13px]` außerhalb von `DesignSystem/` lässt den
      Wächter mit Exit 1 scheitern und nennt Datei und Zeile.
- [ ] Derselbe Treffer in einer Datei unter `DesignSystem/` scheitert ohne Eintrag in der
      Ausnahmeliste und besteht mit Eintrag.
- [ ] Ein Eintrag in der Ausnahmeliste ohne passenden Treffer lässt den Lauf scheitern
      (veraltete Ausnahme).
- [ ] Token-Klassen wie `text-neon-teal`, `bg-surface`, `from-neon-orange`, `shadow-glow-orange`
      und nicht-farbige Arbitrary-Werte wie `min-h-[36px]` oder `w-[420px]` lösen keinen
      Treffer aus (Test).
- [ ] Ein Live-Test im selben Testfile lässt den Wächter gegen den echten `src/Client/`-Baum
      laufen und erwartet Exit 0.
- [ ] `src/Client/Components/Styleguide/View.fs` enthält kein `text-[Npx]` mehr; die Seite
      `/styleguide` sieht an der Stelle unverändert aus (gleiche berechnete Schriftgröße,
      per Playwright `getComputedStyle` vorher und nachher verglichen).
- [ ] `package.json` hat `guard:tokens`; `npm run test:scripts` enthält den Test.
- [ ] `standards/frontend/styleguide.md` §3 ("Harte Regel") nennt den Wächter als
      Durchsetzung; `.claude/rules/design-tokens.md` verweist auf `npm run guard:tokens`
      statt nur auf Grep-Checks.
- [ ] `dotnet build`, `dotnet test`, `npm run build`, `npm run guard:classes`,
      `npm run test:scripts` grün.

## Notes
Angelegt am 2026-10-24 auf Romans Wunsch, Schritt 3 von 4 der Absicherung "neue
Frontend-Tasks benutzen den heutigen Stil, nicht die September-Tafeln". Die anderen drei
Schritte: Vorrangregel in Tafel-README, Handoffs und Styleguide; Zuordnung "Tafel-Element
zu heutigem Baustein" in `docs/ux/kassenbuch/handoff-ablauf-4-kategorie-und-split.md`;
Styleguide-Kriterien in den Frontend-Tasks.

Vorbild für Aufbau, Exit-Codes und Ausnahmeliste: `scripts/tailwind-class-guard.mjs`
(`design-system-thtpp`) und `scripts/readme-delta-check.mjs`. Gehört in die erste Welle,
weil die übrigen Frontend-Tasks in ihren Kriterien `npm run test:scripts` verlangen und der
Wächter dann für sie mitläuft.

`npm run test:scripts` seriell laufen lassen (`node --test --test-concurrency=1
scripts/*.test.mjs`), parallel hängt es auf Romans Rechner.

## Verifier note (iteration 1)

**REASONS:**
- Check 4/5: der Diff führt "Token-Wächter" als neuen BC-Begriff ein (Ausgaben und Dateikommentar von `scripts/token-guard.mjs`, Konvention "Feature-Code nutzt keine Farb- oder Schriftwerte außerhalb der Tokens" mit gezählter Ausnahmeliste), das `readmeDelta` ist aber `[]`. Die BC-README listet den Geschwister-Mechanismus **Tailwind-Klassen-Wächter** als eigenen Bullet in `## Ubiquitous language`; ohne Delta beschreibt sie nur einen der beiden Wächter.
- Checks 1 bis 3 bestanden: alle Kriterien belegt, `dotnet build` 0 Warnungen, `dotnet test` 1666/7 übersprungen/0 Fehler, `npm run build`, `guard:classes`, `guard:tokens` grün, `test:scripts` 176/176; Ausnahmeliste stimmt mit den echten Zeilen überein; Schreibregel-grep ohne Treffer.

**SUGGESTED_FIX:** README_DELTA-Op, die einen Bullet "**Token-Wächter**" in `## Ubiquitous language` direkt nach "Tailwind-Klassen-Wächter" einfügt: `scripts/token-guard.mjs`, `npm run guard:tokens`/`test:scripts`, die fünf Muster, null Toleranz außerhalb `src/Client/DesignSystem/`, gezählte `scripts/token-guard-exceptions.json` mit Regel für veraltete Einträge. Code unverändert.

**ITERATION_HINT:** likely-fixable

## Outcome

`scripts/token-guard.mjs` prüft alle `.fs` unter `src/Client/` (ohne obj/bin) auf fünf Muster: `hex-arbitrary`, `rgb-arbitrary`, `palette-farbe`, `inline-style` (style.color/backgroundColor/borderColor/fontSize) und `text-arbitrary-size`. Kommentarzeilen zählen nicht. Außerhalb von `src/Client/DesignSystem/` gilt null Toleranz. Innerhalb braucht jedes Muster je Datei einen Eintrag mit exakter Anzahl und Grund in `scripts/token-guard-exceptions.json`; ein Eintrag ohne Treffer oder mit abweichender Anzahl ist veraltet und lässt den Lauf scheitern. Exit 0 sauber, 1 Treffer oder veraltete Ausnahme, 2 Wächter kann nicht urteilen (Liste ungültig, Verzeichnis fehlt). Meldung je Treffer: Datei:Zeile, Muster, Treffer, plus der Satz "Optik kommt aus standards/frontend/styleguide.md und src/Client/DesignSystem/, nicht aus Tafel-Werten".

Echter Ausnahmestand (gezählt, weicht von der Task-Schätzung ab): Tokens.fs 8 Hex-Arbitrary (nicht 9), 6 rgb-Arbitrary, 7 `text-[Npx]` (nicht 8); Button.fs 1 Hex + 1 rgb; Loading.fs 1 Hex; Input.fs 3 `style.fontSize` und zusätzlich 2 rgb-Arbitrary (Fokusring Fehler/Erfolg); Card.fs 3 rgb-Arbitrary (in der Task nicht genannt). Alle mit Grund eingetragen.

Feature-Code: einziger Treffer war `src/Client/Components/Styleguide/View.fs:162` (Beschriftungstext "Body — text-[15px] md:text-base"). Ersetzt durch `prop.text ("Body: " + Tokens.FontSizes.body)`; der Beschriftungstext zeigt damit weiter dieselbe Klasse an, nur der Gedankenstrich ist einem Doppelpunkt gewichen. Es gibt keine Ausnahme für Feature-Code.

Dokumentation: `standards/frontend/styleguide.md` (Block "Harte Regel", steht in der Datei am Ende von §2 vor §3) nennt den Wächter als Durchsetzung; `.claude/rules/design-tokens.md` hat den neuen Abschnitt "Mechanische Prüfung" vor den Grep-Checks. `package.json`: `guard:tokens`. `test:scripts` nimmt den Test über den Glob `scripts/*.test.mjs` mit.

Tests (`scripts/token-guard.test.mjs`, 19): für alle sechs Fälle (Hex, rgba-Schatten, Paletten-Farbe, style.color, style.fontSize, text-[13px]) je ein Fixture-Test außerhalb von DesignSystem (Exit 1, Datei und Zeile, Hinweissatz) und einer unter DesignSystem (ohne Eintrag Exit 1, mit Eintrag 0); veraltete Ausnahme; zu kleine Anzahl; Token-Klassen und `min-h-[36px]`/`w-[420px]` ohne Treffer; Kommentare; ungültige Liste Exit 2; obj/bin ignoriert; Live-Test gegen den echten Baum mit Exit 0.

Gelaufen aus dem Worktree: `dotnet build` 0 Warnungen/0 Fehler; `dotnet test` 1666 bestanden, 7 übersprungen, 0 Fehler; `npm run build` ok; `npm run guard:classes` sauber; `node --test --test-concurrency=1 scripts/*.test.mjs` 176 von 176 bestanden; `npm run guard:tokens` Exit 0. Selbstcheck gegen 480a62c ohne Treffer.
