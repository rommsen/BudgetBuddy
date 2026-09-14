---
id: design-system-f7k2p
title: "Bug: App lädt ihre Schriften nicht (Google-Fonts-@import hinter @theme wird verworfen); Lösung: Schriften selbst ausliefern, auch im Claude-Design-Export"
status: done
type: bug
context: design-system
created: 2026-09-11
completed: 2026-09-11
depends_on: [design-system-001]
blocks: [ledger-xbrht]
tags: [design-system, css, fonts, bug, claude-design]
related_adrs: [0024, 0026]
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
- [x] Die gebaute App lädt Inter, Space Grotesk und JetBrains Mono aus eigenen Dateien
      (headless `document.fonts` gegen den Produktions-Build, nicht nur `vite dev`); keine
      Anfrage an fonts.googleapis.com oder fonts.gstatic.com.
- [x] Regressionstest vorhanden, der vor dem Fix fehlschlägt und danach besteht, mit
      Kommentar, welchen Bug er verhindert.
- [x] Die Font-Dateien sind im PWA-Precache.
- [x] Das Export-Bundle enthält die Font-Dateien und bindet sie per `@font-face` ein; die
      Vorschauen rendern damit auch ohne Netz; zwei Export-Läufe bleiben byte-identisch.
- [x] Lizenz-Notiz (OFL 1.1) liegt bei den Fonts im Repo und im Bundle.
- [x] `dotnet build`, `dotnet test`, `npm run build` grün.

## Notes
- Gefunden in der Verifikation von `design-system-t82sg` (Iteration 1), bestätigt durch
  die Meldung in Claude Design beim Erst-Sync (`design-system-h58y4`). Übergangslösung und
  Analyse: ADR 0024.
- Kann parallel zu `design-system-y6gkd` laufen; beide ändern
  `scripts/export-design-system.mjs`, aber an verschiedenen Stellen (Fonts vs. Karten).
- Nach beiden Tasks folgt ein Re-Sync nach dem README-Ablauf (Hauptsession mit Roman).
- Entscheidung dokumentiert in ADR 0026 (löst ADR 0024 Decision 3 ab, Decisions 1+2 aus
  ADR 0024 bleiben gültig).

## Outcome
Die App liefert Inter, JetBrains Mono und Space Grotesk jetzt selbst aus statt über
einen Google-Fonts-`@import`. `src/Client/styles.css` bindet drei lokale
`@font-face`-Regeln ein (Variable Fonts, Subset "latin", je eine Datei pro Familie
unter `src/Client/public/fonts/`, Lizenz-Notiz `LICENSE.md` dort, OFL 1.1). Der
Google-Fonts-`@import`, der hinter dem `@theme`-Block stand und deshalb vom
Tailwind-4-Build verworfen wurde, ist ersatzlos entfernt.

`scripts/export-design-system.mjs` kopiert dieselben Font-Dateien plus `LICENSE.md`
nach `dist/design-system/fonts/` und bindet sie per `@font-face` in
`colors_and_type.css` ein; der frühere Google-Fonts-`<link>` in den Vorschau-Dateien
und die URL-Extraktion aus `styles.css` sind entfallen. Zwei Export-Läufe hintereinander
bleiben byte-identisch (`diff -rq`).

Regressionstest `src/Tests/FontFaceRegressionTests.fs` (registriert in
`src/Tests/Tests.fsproj` direkt nach `ToastLifecycleTests.fs`) liest `styles.css` als
Text und prüft: kein `fonts.googleapis.com`/`fonts.gstatic.com`-Verweis mehr, für alle
drei Familien ein `@font-face` mit lokalem `url(...)`, referenzierte Dateien existieren
unter `public/fonts/`. Test schlug vor dem Fix mit 5 von 6 Fällen fehl, ist jetzt grün.

Wichtige Dateien:
- `src/Client/styles.css` (Fix: `@font-face` statt `@import`)
- `src/Client/public/fonts/{inter,jetbrains-mono,space-grotesk}-latin-wght-normal.woff2`,
  `src/Client/public/fonts/LICENSE.md`
- `scripts/export-design-system.mjs` (Fonts ins Bundle, `@font-face` in
  `colors_and_type.css`, Google-Fonts-Teile entfernt)
- `src/Tests/FontFaceRegressionTests.fs`, `src/Tests/Tests.fsproj`
- `.agentheim/knowledge/decisions/0026-schriften-selbst-ausliefern-statt-google-fonts-import.md`
  (neue ADR), `.agentheim/knowledge/decisions/0024-...md` (Hinweis auf Ablösung von
  Decision 3 ergänzt)
- `.agentheim/contexts/design-system/README.md` (drei Bullets im
  Export-Bundle-Abschnitt aktualisiert)

## Diary entry

## 2026-09-11 21:35 - Schriften selbst ausliefern statt Google-Fonts-Import

**What I did:**
Google-Fonts-`@import` in `src/Client/styles.css` entfernt (stand hinter dem
`@theme`-Block, wurde vom Tailwind-4-Build verworfen, App rendert seitdem in
Ersatzschriften). Ersetzt durch drei lokale `@font-face`-Regeln (Inter, JetBrains
Mono, Space Grotesk), Font-Dateien liegen jetzt im Repo. Export-Bundle
(`scripts/export-design-system.mjs`) zieht nach: kopiert dieselben Dateien ins
Bundle und bindet sie per `@font-face` in `colors_and_type.css` ein, statt den
Google-Fonts-Link in die Vorschau-Dateien zu schreiben.

**Bezugsquelle der Fonts (im Diary festgehalten, wie im Task gefordert):** die
Variable-Font-Pakete von Fontsource (`@fontsource-variable/inter`,
`@fontsource-variable/jetbrains-mono`, `@fontsource-variable/space-grotesk`,
jeweils Version 5.3.0), bezogen per `curl` von der jsDelivr-CDN-Spiegelung der
npm-Pakete (kein `npm install`, wie im Task-Setup vorgeschrieben):
- `https://cdn.jsdelivr.net/npm/@fontsource-variable/inter@5.3.0/files/inter-latin-wght-normal.woff2`
  (48.256 Byte, Gewicht 100-900)
- `https://cdn.jsdelivr.net/npm/@fontsource-variable/jetbrains-mono@5.3.0/files/jetbrains-mono-latin-wght-normal.woff2`
  (40.404 Byte, Gewicht 100-800)
- `https://cdn.jsdelivr.net/npm/@fontsource-variable/space-grotesk@5.3.0/files/space-grotesk-latin-wght-normal.woff2`
  (22.288 Byte, Gewicht 300-700)
- Lizenztexte von denselben Paketen (`.../LICENSE`), alle drei OFL 1.1, nur die
  Copyright-Zeile unterscheidet sich je Schrift; zu einer gemeinsamen
  `LICENSE.md` mit Tabelle plus dem einen gemeinsamen OFL-1.1-Volltext
  zusammengeführt.

Je eine Variable-Font-Datei pro Familie deckt den gesamten benötigten
Gewichtsbereich ab (kein Satz einzelner statischer Schnitte); Subset "latin"
deckt deutsche Umlaute (ä, ö, ü, ß) ab (Unicode-Range U+0000-00FF u. a.), Stil
"normal" (keine Kursivschnitte nötig).

**Files Added:**
- `src/Client/public/fonts/inter-latin-wght-normal.woff2`
- `src/Client/public/fonts/jetbrains-mono-latin-wght-normal.woff2`
- `src/Client/public/fonts/space-grotesk-latin-wght-normal.woff2`
- `src/Client/public/fonts/LICENSE.md` - OFL-1.1-Notiz für alle drei Schriften
- `src/Tests/FontFaceRegressionTests.fs` - Regressionstest für den Font-Bug
- `.agentheim/knowledge/decisions/0026-schriften-selbst-ausliefern-statt-google-fonts-import.md`

**Files Modified:**
- `src/Client/styles.css` - Google-Fonts-`@import` entfernt, drei `@font-face`-Regeln
  eingefügt
- `scripts/export-design-system.mjs` - `extractGoogleFontsUrl` entfernt, neue
  `FONT_FILES`/`FONT_FACE_CSS`/`copyFontFiles`, `writePreview`/`extractPreviews`/
  `writeTokenAndBrandPreviews`/`main` ohne `fontsUrl`-Parameter, `@font-face` in
  `colors_and_type.css`, README/SKILL-Text im Bundle angepasst
- `src/Tests/Tests.fsproj` - `FontFaceRegressionTests.fs` registriert
- `.agentheim/contexts/design-system/README.md` - drei Bullets im
  Export-Bundle-Abschnitt aktualisiert (Fonts jetzt lokal, App-Bug als behoben markiert)
- `.agentheim/knowledge/decisions/0024-export-bundle-build-nicht-dev-und-nicht-fixe-navigation-preview.md`
  - Hinweis ergänzt, dass Decision 3 durch ADR 0026 ersetzt wird, Decisions 1+2
    bleiben gültig

**Files Deleted:**
- keine

**Rationale:**
Zwei unabhängige Funde (Build verwirft den `@import`; Claude Design meldet fehlende
Font-Dateien beim Erst-Sync) haben dieselbe Ursache: die App bezieht ihre Schriften
nicht aus dem eigenen Bundle. Ein reines Verschieben des `@import` vor den
`@theme`-Block hätte nur den ersten Fund behoben, die Abhängigkeit von einem
externen Font-Host aber bestehen lassen (kein Offline-Fallback für die PWA, keine
Dateien für Claude Design). Deshalb: Schriften als Dateien ins Repo, `@font-face`
statt `@import`, sowohl in der App als auch im Export-Bundle.

**Outcomes:**
- Build: dotnet build - OK; npm run build - OK
- Tests: dotnet test 784 bestanden, 7 übersprungen (brauchen `.env`, unverändert
  gegenüber vorherigem Stand); davon 6 neue Fälle in `FontFaceRegressionTests.fs`
  (vor dem Fix: 5 von 6 fehlgeschlagen, danach: alle 6 bestanden)
- Headless-Check (`vite build` + `vite preview --port 5191 --strictPort`,
  Playwright, Navigation zu `/#/styleguide`): `document.fonts` zeigt Inter,
  JetBrains Mono und Space Grotesk als `loaded`; keine Netzwerk-Anfrage an
  fonts.googleapis.com oder fonts.gstatic.com beobachtet
- PWA-Precache: generierter `dist/public/sw.js` listet alle drei
  `fonts/*.woff2`-Dateien im Workbox-Manifest
- Export-Bundle: `npm run export:design-system` zweimal hintereinander,
  `diff -rq` zwischen den beiden Läufen leer (byte-identisch); `dist/design-system/fonts/`
  enthält die drei Font-Dateien plus `LICENSE.md`; `colors_and_type.css` enthält die
  drei `@font-face`-Regeln; keine `googleapis`/`gstatic`-Vorkommen mehr in
  `dist/design-system/`
- Issues: keine

---

## 2026-09-11 22:10 - Iteration 2: Schreibstil und ADR-Frontmatter korrigiert

**What I did:**
Nach dem Verifier-Fund aus Iteration 1 alle transliterierten Umlaute in
`src/Client/styles.css` (Kommentar über dem `@font-face`-Block) und in
`src/Tests/FontFaceRegressionTests.fs` (Kommentare und Assertion-Messages)
durch echte Umlaute und ß ersetzt; dabei auch "durfte" zu "darf" korrigiert
(Präsens statt fälschlich Präteritum). In ADR 0026 `supersedes: [0024]`
gesetzt; in ADR 0024 `superseded_by: [0026]` und `related_adrs: [0026]`
ergänzt (Status bleibt `accepted`, die bestehende Amendment-Notiz macht
bereits klar, dass nur Decision 3 ersetzt wird, Decisions 1 und 2 bleiben
gültig). Alle geänderten Dateien plus Export-Skript, README und Fonts-Lizenz
auf verbliebene Transliterationen geprüft; keine gefunden.

**Files Modified:**
- `src/Client/styles.css` - Kommentar korrigiert (echte Umlaute)
- `src/Tests/FontFaceRegressionTests.fs` - Kommentare und Assertion-Messages
  korrigiert (echte Umlaute, ß, "durfte" zu "darf")
- `.agentheim/knowledge/decisions/0026-schriften-selbst-ausliefern-statt-google-fonts-import.md`
  - `supersedes: [0024]` gesetzt
- `.agentheim/knowledge/decisions/0024-export-bundle-build-nicht-dev-und-nicht-fixe-navigation-preview.md`
  - `superseded_by: [0026]`, `related_adrs: [0026]` ergänzt

**Rationale:**
Die Implementierung selbst war funktional korrekt (Build, 784 Tests,
@font-face im gebauten CSS, Fonts im PWA-Precache); nur Schreibstil und
ADR-Verknüpfung mussten nachgezogen werden.

**Outcomes:**
- Build: dotnet build - OK
- Tests: dotnet test - siehe unten
- Issues: keine

---

## Verifier note (iteration 1)

**REASONS:**
- Writing style (conductor check: German prose in added lines must use real umlauts). `src/Client/styles.css:49` (the new comment above the `@font-face` block) writes "Inter fuer UI, JetBrains Mono fuer Zahlen, Space Grotesk fuer". Two lines further down the same comment spells "frühere" correctly.
- Writing style, same rule. `src/Tests/FontFaceRegressionTests.fs` uses transliterations in comments and assertion messages: "fuer" (lines 7, 16, 20, 80, 87, 94), "ausser" (10, should be "außer"), "enthaelt" (13, 34), "prueft"/"Prueft" (20, 33), "faellt" (21), "Bloecke" (23), "Anfuehrungs-zeichen" (33), "aufgeloest" (112). Line 108 has "Blocke" (should be "Blöcke"). Lines 66 and 73 say "durfte" (past tense) where "darf" or "dürfte" is meant.
- Check 6b (honored related ADRs). ADR 0026 deliberately replaces ADR 0024 Decision 3: the 0024 amendment says "Ersetzt durch ADR 0026", and the 0026 heading for Decision 4 says "ADR 0024 Decision 3 ersetzt". Yet the frontmatter in `.agentheim/knowledge/decisions/0026-schriften-selbst-ausliefern-statt-google-fonts-import.md` still reads `supersedes: []`. The rule requires the replaced ADR's id in the new ADR's `supersedes` field. There is precedent in the repo: 0012 has `supersedes: [0001]`.
- Otherwise the change checks out: `dotnet build` 0 warnings, `dotnet test` 784 passed / 0 failed / 7 skipped, `npm run build` green; built CSS has all three `@font-face` rules and no googleapis/gstatic references; `dist/public/sw.js` precaches all three woff2 files; 5 of 6 regression tests would fail against the pre-fix `styles.css`; scope clean; no "—" or "–" in any added line.

**SUGGESTED_FIX:** Replace the transliterations with real umlauts and ß in the `styles.css` comment and in `FontFaceRegressionTests.fs` (also "Blocke" to "Blöcke" and "durfte" to "darf"). Set `supersedes: [0024]` in ADR 0026's frontmatter and keep the body note that only Decision 3 is replaced and Decisions 1 and 2 stay valid.

**ITERATION_HINT:** likely-fixable
