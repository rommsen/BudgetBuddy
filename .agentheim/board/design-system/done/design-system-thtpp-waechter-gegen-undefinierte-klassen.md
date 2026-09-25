---
id: design-system-thtpp
title: "Wächter, der Klassennamen findet, die weder Tailwind-Utility noch in styles.css definiert sind"
status: done
type: feature
context: design-system
created: 2026-09-13
completed:
depends_on: []
blocks: []
tags: [test-infra, styleguide, sicherheitsnetz]
related_adrs: [0031, 0041]
related_research: []
prior_art: [infra-za7fq, design-system-fq8dn, design-system-t3vqr]
---

## Why
Die DaisyUI-Klassen im Design-System (`design-system-fq8dn`) standen seit dem Bau der
Table-Komponente im Code und haben nie etwas getan. Aufgefallen ist es erst, als jemand
eine Tabelle neben einen Entwurf gelegt hat. Eine Klasse, die es nicht gibt, wirft keinen
Fehler: sie fehlt einfach in der Ausgabe, und das Ergebnis sieht nicht kaputt aus, sondern
nur etwas anders als gedacht.

Genau diese Lautlosigkeit ist das Muster, das sich mehrfach gezeigt hat. Beim Testlauf,
der still in die echte Datenbank schrieb, half am Ende ein Wächter-Test (`infra-za7fq`).
Hier braucht es denselben Gedanken: aus einem stillen Ausfall eine laute Meldung machen.

Bei der Verfeinerung dieser Aufgabe (Probe gegen den echten, installierten Tailwind-4-
Compiler) fand sich sofort ein zweites, bisher unbemerktes Beispiel: `ErrorDisplay.warning`
(`src/Client/DesignSystem/ErrorDisplay.fs:314,328`) setzt `bg-warning/5`, `border-warning/20`,
`text-warning`, `hover:text-warning/80`, es gibt kein `--color-warning` in `styles.css`s
`@theme`-Block. Die Variante ist seit ihrem Bau unauffällig falsch gestylt, genau das
DaisyUI-Muster aus ADR 0031, nur in einer anderen Komponente. Ohne einen Wächter kehrt
dieses Muster zurück, sobald jemand aus Gewohnheit eine Klasse schreibt, die es hier nicht
gibt.

## What

### Die Prüfung selbst
Die maßgebliche Implementierung liegt als Node-Skript unter `scripts/`, nicht als
F#-Nachbau der Tailwind-Logik. Begründung: nur Tailwinds eigener Compiler kann
autoritativ beantworten, ob ein Klassenname eine echte Utility ist; eine zweite,
in F# nachgebaute Kopie dieser Logik wäre genau das Drift-Risiko, das der Kopf-Kommentar
in `src/Tests/FontFaceRegressionTests.fs` für einen analogen Fall ausdrücklich vermeidet.
Damit die Prüfung trotzdem **ohne Zutun** in jedem `dotnet test`-Lauf mitläuft (nicht nur
wenn jemand `npm run test:scripts` aufruft), wird sie zusätzlich per Node-Subprozess aus
einem Expecto-Test heraus aufgerufen, derselbe Aufbau wie
`FontFaceRegressionTests.fs`s `runRewriteAbsoluteAssetUrls` (CSS/Argumente über
stdin/CLI-Flags rein, Ergebnis über stdout/Exit-Code raus).

**Neue Dateien:**

1. **`scripts/tailwind-class-guard.mjs`**, Kern-Implementierung, exportiert reine,
   einzeln testbare Funktionen plus einen CLI-Einstiegspunkt:
   - `extractClassNameTokens(fsSource: string)`: findet jeden `prop.className`-Aufruf
     (Literal oder `$"..."`-Interpolation) in einem F#-Quelltext, zerlegt den String an
     Whitespace in einzelne Klassen-Tokens (nicht den ganzen Literal-String als ein
     Stück, ein Token wie `{Tokens.Borders.subtle}` neben `md:w-[268px] md:shrink-0` darf
     die drei statischen Nachbar-Tokens nicht mit ausnehmen). Ein Token, das `{` oder `}`
     enthält, wird als `dynamic` markiert statt direkt geprüft.
   - `extractTokensModuleLiterals(tokensFsSource: string)`: liest
     `src/Client/DesignSystem/Tokens.fs` und extrahiert jeden `let name = "..."`-String
     direkt. Fast jede `{Tokens.X.y}`-Interpolation an einer Aufrufstelle verweist auf
     genau so eine Bindung; ein `dynamic`-Token wird zuerst gegen diese Liste aufgelöst
     (Tailwind prüft dann den aufgelösten Literalwert), bevor es als "nicht auflösbar"
     gilt. Das schließt den Großteil der 70 im Client gefundenen interpolierten
     Aufrufstellen automatisch, ohne F#-Symbole auszuwerten.
   - `extractStylesheetClassNames(css: string)`: findet **jeden** `.bezeichner` in
     **jedem** Selektor von `styles.css`, auch in zusammengesetzten Selektoren
     (`.test-result.match`, `.proto-spinner.sm`, `.flow-label-icon.step1`),
     Nachfahren-Selektoren (`.filter-btn .count`, `.header-subtitle .dot`) und innerhalb
     von `@media`/`@supports`-Blöcken. Ein an Spalte 0 verankerter "nur führende
     Selektoren"-Parser reicht nicht: eine Probe damit erzeugte bei diesem Stylesheet
     neun falsch-positive "undefinierte" Klassen.
   - `isKnownMarkerClass(token: string)`: `true` für `group`, `peer`,
     `group/<name>`, `peer/<name>`, Tailwind erzeugt für diese bewusst keine eigene
     CSS-Regel (nur für `group-*:`/`peer-*:`-Varianten, die sie referenzieren); verifiziert
     gegen den echten Compiler (`candidatesToCss(["group"])` → `null`, obwohl korrekt).
     Ohne diese Ausnahme meldet der Wächter jedes `group`/`peer`-Vorkommen fälschlich als
     Fund.
   - `loadValidTailwindClasses({ stylesCssPath, base })`: lädt
     `@tailwindcss/node`s `__unstable__loadDesignSystem(css, { base })` gegen
     `src/Client/styles.css` und liefert eine Prüf-Funktion `isValidUtility(token)` über
     `.candidatesToCss([token])[0] !== null`. Kein `npm run build`/`vite build` nötig:
     diese API beantwortet Gültigkeit pro Kandidat direkt gegen den kompilierten
     Theme-Stand, ist in unter einer Sekunde einsatzbereit (gemessen) und exakter als ein
     Textabgleich gegen kompiliertes CSS (kein Risiko von Teilstring-Treffern).
   - `loadExceptions(path)` / `checkCodebase({ clientDir, stylesCssPath, tokensFsPath,
     exceptionsPath })`: orchestriert alle Bausteine, liefert
     `{ undefinedTokens, staleExceptions, unresolvedDynamic }`.
   - CLI (`node scripts/tailwind-class-guard.mjs [--client-dir <pfad>]`): druckt einen
     Bericht, Exit-Code `1` wenn `undefinedTokens` oder `staleExceptions` nicht leer sind.
     `--client-dir` (Default `src/Client`) macht den Scan-Wurzelpfad konfigurierbar,
     damit ein Test ihn gegen ein Scratch-Verzeichnis statt den echten Client-Baum laufen
     lassen kann (siehe AC „Falsifizierbarkeit" unten).

2. **`scripts/tailwind-class-guard-exceptions.json`**, die Ausnahmeliste. Format: Array
   von `{ "token": "<klasse-oder-roh-literal>", "reason": "<begründung>", "locations":
   ["<pfad:zeile>", ...] }`. Kurz halten (Ziel: einstellig). Jeder Eintrag muss zum
   Zeitpunkt des Laufs tatsächlich vom Scanner als unauflösbar gemeldet werden, sonst ist
   er veraltet und der Wächter scheitert darüber (siehe Akzeptanzkriterium).

3. **`scripts/tailwind-class-guard.test.mjs`**, `node --test`-Unit-Tests für die reinen
   Funktionen aus (1): Token-Zerlegung inkl. gemischt statisch/dynamischer Literale,
   zusammengesetzte und Nachfahren-Selektoren aus `styles.css`, Marker-Klassen-Erkennung,
   Auflösung über `Tokens.fs`-Literale, veraltete Ausnahmeliste-Einträge. Läuft über
   `npm run test:scripts`.

4. **`src/Tests/TailwindClassGuardTests.fs`**, Expecto-Test(s) nach dem Muster von
   `FontFaceRegressionTests.fs`/`BackendPortGuardTests.fs`:
   - Ruft `scripts/tailwind-class-guard.mjs` per `ProcessStartInfo` gegen den echten
     `src/Client`-Baum auf, erwartet Exit-Code 0.
   - Falsifizierbarkeits-Test (analog `infra-za7fq`/`BackendPortGuardTests.fs`): schreibt
     eine temporäre `.fs`-Datei mit einer erfundenen Phantom-Klasse
     (`prop.className "bb-phantom-nicht-real"`) in ein Scratch-Verzeichnis, ruft das
     Skript mit `--client-dir <scratch>` auf, erwartet Exit-Code ≠ 0 und die Phantom-
     Klasse wörtlich in der Ausgabe; löscht die Datei danach wieder und prüft erneut
     grün. Kein Eingriff in den echten `src/Client`-Baum nötig.

### Bestehende Dateien
5. **`package.json`**: `"guard:classes": "node scripts/tailwind-class-guard.mjs"` als
   neues Skript; `@tailwindcss/node` explizit als devDependency pinnen (heute nur
   transitiv über `@tailwindcss/vite` aufgelöst, `^4.1.17` passend zu `tailwindcss`/
   `@tailwindcss/vite`, ohne explizite Pin-Angabe könnte eine künftige
   Lockfile-Neuauflösung das Paket stillschweigend entfernen und den Wächter brechen).

### Sofortige Funde aus dieser Aufgabe selbst
6. **`ErrorDisplay.warning`** (`src/Client/DesignSystem/ErrorDisplay.fs:314,328`) und
   **`qa-page`** (`src/Client/Views/QuickAddPage.fs:121`) werden vom fertigen Wächter mit
   hoher Wahrscheinlichkeit als Funde gemeldet (siehe `## Why`). Für jeden Fund, den der
   erste echte Lauf meldet: trivial reparieren (z. B. `warning` auf eine bestehende
   Neon-Farbe der Farbsemantik ummünzen) **oder** mit Begründung in die Ausnahmeliste
   aufnehmen, nicht stillschweigend liegen lassen. Das Reparieren selbst ist kein
   eigenständiger Styling-Auftrag dieser Aufgabe, sondern eine Bedingung dafür, dass die
   Aufgabe grün wird.

   Am Code nachgesehen (2026-09-25), damit der Worker nicht raten muss:
   - `qa-page` kommt im ganzen Repo nur an dieser einen Stelle vor (kein Selektor in
     `styles.css`, kein Test, kein Skript): das Token wird ersatzlos entfernt.
   - Die Warn-Farbrolle des Design-Systems ist Hot Pink (`src/Client/DesignSystem/Tokens.fs`,
     Zeile 28, "Warning: Hot Pink"; `Icons.warning MD Warning` in derselben Funktion nutzt
     sie bereits). `ErrorDisplay.warning` wird auf diese Rolle umgestellt, mit den Klassen,
     die die Nachbarn `Badge.warning` und `Toast.warning'` dafür benutzen; keine neue
     Farbe in `@theme`. Keine Ausnahmeliste für diese beiden Fälle.

## Acceptance criteria
- [ ] `scripts/tailwind-class-guard.mjs` prüft jeden `prop.className`-Klassen-Token in
      `src/Client/**/*.fs` gegen (a) `@tailwindcss/node`s
      `__unstable__loadDesignSystem(...).candidatesToCss(...)` über
      `src/Client/styles.css` und (b) jeden Klassennamen, den `styles.css` selbst über
      irgendeinen Selektor definiert (nicht nur an Spalte 0 verankerte Top-Level-Selektoren
     , zusammengesetzte und Nachfahren-Selektoren zählen mit).
- [ ] `group`, `peer`, `group/<name>`, `peer/<name>` gelten ohne eigene Tailwind-CSS-Regel
      als gültig (dokumentierter Sonderfall, kein Fund).
- [ ] Ein Klassen-Token, das `{`/`}` enthält, wird zuerst gegen die `let`-Literale aus
      `src/Client/DesignSystem/Tokens.fs` aufgelöst; erst danach gilt es als "nicht
      auflösbar" und muss entweder in `scripts/tailwind-class-guard-exceptions.json`
      stehen oder den Lauf scheitern lassen.
- [ ] `npm run guard:classes` läuft grün auf dem aktuellen `main`-Stand, nachdem die unter
      „Sofortige Funde" genannten Fälle (und jeder weitere, den der erste echte Lauf
      meldet) entweder repariert oder mit Begründung ausgenommen sind.
- [ ] **Falsifizierbarkeit**: eine testweise eingebaute Phantom-Klasse (weder Tailwind-
      Utility noch in `styles.css` definiert) lässt den Wächter mit Exit-Code ≠ 0 und der
      Klasse wörtlich in der Meldung scheitern; nach Entfernen ist er wieder grün. Belegt
      im Outcome (Text + Exit-Codes), analog `infra-za7fq`/`BackendPortGuardTests.fs`.
- [ ] `scripts/tailwind-class-guard-exceptions.json`: jeder Eintrag wird bei jedem Lauf
      dagegen geprüft, ob sein Token tatsächlich noch als unauflösbar erkannt wird; ein
      Eintrag, den der aktuelle Scan nicht mehr findet, lässt den Wächter mit Nennung
      dieses Eintrags scheitern (mechanisierte Durchsetzung der Ausnahmeliste, nicht nur
      Konvention, ADR-0059 dieses Harness: mechanize-or-drop).
- [ ] `src/Tests/TailwindClassGuardTests.fs` ruft das echte Skript per Node-Subprozess auf
      (keine F#-Nachbildung der Tailwind-Prüflogik) und läuft als Teil von `dotnet test`.
- [ ] `scripts/tailwind-class-guard.test.mjs` deckt die reinen Extraktions-/
      Klassifizierungsfunktionen ab und läuft grün unter `npm run test:scripts`.
- [ ] `dotnet build`, `dotnet test`, `npm run build`, `npm run test:scripts` sind grün.

## Notes
Verfeinert am 2026-09-25 mit einer Probe gegen den echten, bereits installierten
Tailwind-4-Compiler (`@tailwindcss/node@4.1.17`, liegt unter `node_modules/@tailwindcss/node`,
aber noch nicht explizit in `package.json` gepinnt, siehe `## What`, Punkt 5). Die Probe
zeigt: `__unstable__loadDesignSystem(css, {base}).candidatesToCss(kandidaten)` beantwortet
Gültigkeit pro Klassen-String direkt und braucht keinen vorherigen `npm run build`, schneller
und genauer als der ursprünglich in der Aufgabe skizzierte Weg über das gebaute CSS. Das
weicht von der ursprünglichen Formulierung „Der einfachste verlässliche Weg dürfte das
gebaute CSS sein" bewusst ab; Grund und Beleg stehen oben.

**Zwei echte Funde aus der Probe** (siehe `## Why`, `## What` Punkt 6):
- `ErrorDisplay.warning` (`src/Client/DesignSystem/ErrorDisplay.fs:314,328`): `bg-warning/5`,
  `border-warning/20`, `text-warning`, `hover:text-warning/80`, kein `--color-warning` in
  `styles.css`s `@theme`. Silent-Fail wie bei den DaisyUI-Klassen aus ADR 0031, nur in einer
  anderen Komponente.
- `qa-page` (`src/Client/Views/QuickAddPage.fs:121`): weder Tailwind-Utility noch in
  `styles.css` definiert.

**Stolperstein 1:** eine naive Selektor-Extraktion aus `styles.css`, die nur an Spalte 0
verankerte, einzelne `.klasse { ... }`-Regeln erkennt, erzeugt falsch-positive Funde bei
zusammengesetzten Selektoren (`.test-result.match`) und Nachfahren-Selektoren
(`.filter-btn .count`). Eine erste Probe-Implementierung mit genau diesem naiven Ansatz
erzeugte neun solche Fehlalarme in diesem Stylesheet. Die Extraktion muss jeden
`.bezeichner` in jedem Selektor finden, unabhängig von Kombinator/Verschachtelung.

**Stolperstein 2:** `group`/`peer` erzeugen absichtlich keine eigene CSS-Regel; ohne
Sonderbehandlung meldet der Wächter jedes Vorkommen (mehrere in
`src/Client/DesignSystem/Card.fs`, `Input.fs`) als Fund, obwohl beide Klassen korrekt sind.

**Stolperstein 3:** ein Token wie `{categoryColorClassName}` (ein lokaler Bezeichner, keine
`Tokens.*`-Referenz) lässt sich durch reine Textanalyse nicht auflösen. Die Aufgabe
verlangt hier keine vollständige F#-Symbolauswertung; solche Fälle landen entweder in der
Ausnahmeliste (mit Begründung) oder werden, falls die Definitionsstelle selbst nur eine
endliche Menge von `Tokens.*`-Werten zurückgibt, an ihrer Definition statt an der
Aufrufstelle aufgelöst. Diese Entscheidung liegt beim Worker, Fall für Fall.

Verwandt: `infra-za7fq` (derselbe Wächter-Gedanke gegen einen anderen stillen Ausfall),
`design-system-fq8dn` (der ADR-0031-Fund, der diese Aufgabe motiviert hat),
`design-system-t3vqr` (nennt dieselbe allgemeine Sorge für Design-System-Funktionen
gegen die `/styleguide`-Route, verwandt, aber eine andere Prüfung: dort geht es um
fehlende Styleguide-Einträge für neue Komponenten, hier um undefinierte CSS-Klassen).

## Verifier note (iteration 1)

REASONS:
- Writing rule (user CLAUDE.md: "Keine Gedankenstriche, nimm stattdessen Punkt, Komma, Doppelpunkt oder Klammern"). The new code comments and JSON notes use a spaced hyphen " - " as a Gedankenstrich throughout. Occurrences: all four "reason" strings in scripts/tailwind-class-guard-exceptions.json (e.g. "...selbst - reine Textanalyse..."); about 25 comment lines in scripts/tailwind-class-guard.mjs; scripts/tailwind-class-guard.test.mjs (two places); src/Client/DesignSystem/Money.fs comment ("ADR 0031) - anders als..."); src/Client/DesignSystem/Tokens.fs comment ("(nicht in styles.css definiert) - Arbitrary-Property-Syntax"); src/Client/styles.css comment ("...Regel dafür stand - derselbe stille Ausfall"); src/Tests/TailwindClassGuardTests.fs ("ADR 0031 - die", "src/Client - der Wächter"). The ADR and OUTCOME prose are clean.
- Lockfile drift: package.json adds "@tailwindcss/node": "^4.1.17" to devDependencies, but package-lock.json's root `packages[""].devDependencies` still lacks it. It resolves transitively today (Dockerfile uses `npm install`, no `npm ci`), but the manifests are out of sync: `npm ci` would refuse, and the next `npm install` leaves an unrelated lock change behind. `npm install --package-lock-only` rewrites only package-lock.json and leaves the shared node_modules alone.
- Minor: ADR 0041's counts are inconsistent. The Context says the first real run "fand vier weitere" and lists five items (qa-page was already known from refinement); Decision 4 repeats "Vier weitere Funde". The OUTCOME says "drei aus der Aufgabenverfeinerung", but refinement named two (ErrorDisplay.warning, qa-page). The OUTCOME also says the skipped exception list under --client-dir is covered in "ADR 0041 Consequences/Negative", but the ADR never mentions it.
- Everything else passes: all nine ACs covered, falsifiability independently confirmed against the pre-change tree, visual fixes match the styleguide (reduced-motion covered globally), README_DELTA fits, scope clean.

SUGGESTED_FIX: Rewrite every spaced-hyphen dash in the new comments and JSON reason strings using a period, comma, colon or parentheses. Run `npm install --package-lock-only` so package-lock.json lists @tailwindcss/node, and include that lock change. Correct the finding counts and the "Consequences/Negative" reference in ADR 0041 and the OUTCOME.

ITERATION_HINT: likely-fixable

## Outcome

Neuer Wächter `scripts/tailwind-class-guard.mjs` (CLI: `npm run guard:classes` /
`node scripts/tailwind-class-guard.mjs [--client-dir <pfad>]`) prüft jeden
`prop.className`-Klassen-Token in `src/Client/**/*.fs` gegen (a) den echten
Tailwind-4-Compiler (`@tailwindcss/node`s `__unstable__loadDesignSystem(...)
.candidatesToCss(...)` gegen `src/Client/styles.css`) und (b) jeden Klassennamen, den
`styles.css` selbst über irgendeinen Selektor definiert (Top-Level, zusammengesetzt,
Nachfahren, innerhalb `@media`/`@supports`). `group`/`peer`(`/name`) und sprintf-
Formatplatzhalter (`%s` u. ä., aus `sprintf "... %s" ...`-Konstrukten) gelten als
Sonderfälle ohne eigenen Fund.

**Gefundene und behobene Stellen** (zwei aus der Aufgabenverfeinerung selbst genannt,
vier weitere, die der erste echte Lauf meldete):
- `src/Client/DesignSystem/ErrorDisplay.fs` (`ErrorDisplay.warning`): `bg-warning`/
  `border-warning`/`text-warning` (kein `--color-warning` in `styles.css`) durch die
  echte Warn-Farbrolle Hot Pink (`neon-pink`, wie `Badge.warning`) ersetzt.
- `src/Client/Views/QuickAddPage.fs`: totes Token `qa-page` entfernt (im ganzen Repo nur
  an dieser Stelle, kein Selektor, kein Test).
- `src/Client/Components/Settings/View.fs`: totes Token `card-header` entfernt.
- `src/Client/DesignSystem/BottomSheet.fs`: totes Token `sheet-header-sub` entfernt.
- `src/Client/styles.css`: `.animate-neon-pulse`/`@keyframes neonPulse` ergänzt (analog
  zum vorhandenen `.animate-glow-pulse`-Muster). Die Klasse stand in `Tokens.fs`
  (`Animations.neonPulse`) und an drei Aufrufstellen (`StatusViews.fs` ×2, `Loading.fs`),
  ohne dass je eine CSS-Regel existierte.
- `src/Client/DesignSystem/Money.fs` (`getGlowClass`) und `src/Client/DesignSystem/
  Tokens.fs` (`TextGlows.green`, `Presets.moneyPositive`): `text-glow-green` (keine
  Tailwind-Utility, keine `styles.css`-Regel) durch dieselbe Arbitrary-Property-Syntax
  ersetzt, die der negative Glow-Fall in derselben Funktion bereits nutzte
  (`[text-shadow:0_0_10px_theme(colors.neon-green),0_0_20px_rgba(0,255,136,0.5)]`, mit
  denselben rgba-Werten wie `--shadow-glow-green`). `TextGlows.orange`/`TextGlows.teal`
  sind vom selben Muster betroffen, aber unbenutzt (kein Aufrufer, vom Wächter also nicht
  gemeldet), als Backlog-Item erfasst, nicht in dieser Aufgabe repariert.

**Ausnahmeliste** (`scripts/tailwind-class-guard-exceptions.json`, 5 Einträge, jeder mit
Begründung, jeder bei jedem Lauf gegen den aktuellen Scan geprüft): `{extraClass}`
(Durchreiche-Parameter an acht DS-Komponenten), `{animation}` (Loading.fs'
`staggeredList`-Parameter), `{maxHeight}` (Primitives' `ScrollContainer.vertical`-
Parameter), `cls` (Stats.fs, aus einem Aufruf mit literalem Argument an eine
parametrisierte Funktion gebunden) und `{alignClass}` (Table.fs, `Option.map`-Pipeline
über einen vom Aufrufer frei wählbaren String). Alle fünf sind echte, zur Laufzeit vom
Aufrufer bestimmte Werte, keine feste Tailwind-Klasse (siehe ADR 0041 für die
Auflösungsgrenze, die das begründet).

**Dynamische Auflösung** (`resolveIdentifierToLiterals` in `tailwind-class-guard.mjs`):
zweistufig wie in der Aufgabe skizziert (zuerst `Tokens.fs`-Literale inkl. eigener
Tokens.fs-Funktionen mit endlich vielen Zweigen wie `StaggerDelays.forIndex`, danach
`let`-Bindungen im selben Quelltext), erweitert um Tupel-Destrukturierung (`let a, b, c =
...` und `let (a, b, c) = ...`, mit und ohne Klammern) und mehrstufige reine
Weiterleitung (ein Arm, dessen Wert selbst nur ein Aufruf einer anderen, ebenfalls
endlich verzweigenden Funktion ist). Während der Entwicklung mehrfach gegen den echten
`src/Client`-Baum verifiziert und dabei drei eigene Bugs gefunden und behoben: `\s*`
zwischen `->`/`then`/`else` und dem Wert las über Zeilengrenzen hinweg und fing dadurch
fälschlich den Kopf eines verschachtelten `match`s als Arm-Wert ein (behoben mit
`[ \t]*` plus expliziter, durch `isControlFlowKeywordStart` geschützter
Nächste-Zeile-Behandlung); `|>` (Pipe-Operator) wurde als Match-Arm-Anfang gelesen, weil
beides mit `|` beginnt; ein erklärender Kommentar direkt nach einem wertlosen `| Pattern
->` wurde fälschlich als dessen Wert gelesen (`nextNonBlankLine` überspringt jetzt
`//`-Kommentarzeilen).

**Falsifizierbarkeit** (`src/Tests/TailwindClassGuardTests.fs`): eine testweise in ein
Scratch-Verzeichnis geschriebene Phantom-Klasse (`bb-phantom-nicht-real`, per
`--client-dir` isoliert vom echten `src/Client`-Baum) lässt den Wächter mit Exit-Code ≠ 0
und der Klasse wörtlich in der Meldung scheitern; nach Entfernen der Datei ist er wieder
grün (Exit-Code 0). Belegt per `dotnet test` (siehe Testausführung unten). Die reale
Ausnahmeliste wird bei einem per `--client-dir` umgeleiteten Scan bewusst NICHT geladen
(sonst wären alle fünf realen Einträge zwangsläufig "veraltet", weil sie im
Scratch-Verzeichnis naturgemäß nicht vorkommen können, siehe ADR 0041 Consequences/Negative).

**Unit-Tests** (`scripts/tailwind-class-guard.test.mjs`, 26 Fälle, `npm run
test:scripts`): Token-Zerlegung (einfaches Literal, gemischt statisch/dynamisch,
Interpolationsloch mit eigenem Leerzeichen, zwei aneinandergrenzende Löcher ohne
Leerzeichen, geklammerte if/then/else-Ausdrücke, nackter Bezeichner), Stylesheet-
Selektor-Extraktion (Top-Level, zusammengesetzt, Nachfahren, `@media`, Kommentare,
Dezimalzahl-Fehlalarm-Regression), `isKnownMarkerClass`, `isPrintfFormatSpecifier`,
`extractTokensModuleLiterals` (inkl. Funktionsbindungen, die NICHT als Literal gelten),
`loadExceptions` sowie drei `checkCodebase`-Integrationsfälle (veralteter
Ausnahmeliste-Eintrag, per Ausnahmeliste erlaubte Klasse, echte unentdeckte
Phantom-Klasse).

**`package.json`**: `"guard:classes": "node scripts/tailwind-class-guard.mjs"` ergänzt;
`@tailwindcss/node` explizit als devDependency gepinnt (`^4.1.17`, wie
`@tailwindcss/vite`/`tailwindcss`, vorher nur transitiv aufgelöst). **`package-lock.json`
(Iteration 2):** `npm install --package-lock-only` ausgeführt, damit der Root-
`devDependencies`-Eintrag den neuen Direct-Dependency-Pin widerspiegelt (vorher stand er
nur in `package.json`, ein `npm ci` hätte das abgelehnt). `node_modules` ist in diesem
Worktree ein Symlink auf den geteilten Haupt-Tree; `--package-lock-only` schreibt
ausschließlich die Lock-Datei, `node_modules` blieb vor und nach dem Lauf byte-identisch
derselbe unveränderte Symlink (`readlink node_modules` vor/nach geprüft). Der Lock-Diff
ist klein und ausschließlich additiv (61 Zeilen, 0 Löschungen): der neue Root-
`devDependencies`-Eintrag plus `@tailwindcss/oxide-wasm32-wasi`s optionale,
plattformspezifische Transitiv-Abhängigkeiten, die erst durch den direkten statt nur
transitiven Pin in den Lock-Baum aufgenommen wurden, keine unabhängigen Änderungen an
anderer Stelle der Datei.

**`src/Client/**/*.fs`-Scan schließt `fable_modules/`/`bin`/`obj` aus**: `fable_modules`
ist Fables lokaler, nicht in git getrackter Paket-Cache (vendorierte Kopien von
Feliz/Elmish-Quelltext); ohne den Ausschluss meldete der Wächter Funde in Bibliothekscode,
den dieses Projekt nicht kontrolliert (während der Entwicklung entdeckt, nachdem `npm run
build` diesen Cache befüllt hatte).

**Iteration 2** behebt ausschließlich die von der Verifikation der ersten Iteration
gemeldeten Hygiene-Verstöße, die funktionale Umsetzung (Wächter, Ausnahmeliste, Tests,
behobene Stellen) blieb unverändert:
- Jeder mit einem Leerzeichen umgebene Bindestrich als Gedankenstrich in den neuen
  Kommentaren und JSON-Begründungen dieser Aufgabe (`scripts/tailwind-class-guard.mjs`,
  `scripts/tailwind-class-guard-exceptions.json`, `scripts/tailwind-class-guard.test.mjs`,
  `src/Client/DesignSystem/Money.fs`, `src/Client/DesignSystem/Tokens.fs`,
  `src/Client/styles.css`, `src/Tests/TailwindClassGuardTests.fs`) durch Punkt, Komma
  oder Doppelpunkt ersetzt. Verbliebene Treffer sind ausschließlich Arithmetik im
  JS-Code (`depth - 1`, `line.length - line.trimStart().length`) oder ein
  Markdown-Listenpunkt am Zeilenanfang (` * - Ein Literal ...`), beide laut
  Verifikations-Hinweis zulässige Ausnahmen.
- `package-lock.json` per `npm install --package-lock-only` synchronisiert (siehe oben),
  `node_modules` dabei nachweislich unverändert.
- Die Fund-Zählung in ADR 0041 (Context, Decision 4) und in diesem Outcome korrigiert:
  die Aufgabenverfeinerung selbst nannte zwei Funde (`ErrorDisplay.warning`, `qa-page`),
  nicht drei; der erste echte Lauf des fertigen Wächters fand die verbleibenden vier
  (`card-header`, `sheet-header-sub`, `animate-neon-pulse`, `text-glow-green`). ADR 0041
  nennt den `--client-dir`-Ausnahmeliste-Sonderfall jetzt explizit unter
  Consequences/Negative, worauf dieses Outcome bereits verwies.

### Build/Tests
- `dotnet build`: 0 Fehler, 0 Warnungen.
- `dotnet test`: 994 erfolgreich, 0 fehlgeschlagen, 7 übersprungen (Integrationstests ohne
  `.env`, wie üblich), 1001 gesamt.
- `npm run test:scripts`: 77 bestanden (26 neu für den Wächter, Rest vorbestehend), 0
  fehlgeschlagen.
- `npm run build`: erfolgreich; das gebaute CSS-Bundle enthält `.animate-neon-pulse`
  (`animation:1.5s ease-in-out infinite neonPulse`) und die neue
  `[text-shadow:...neon-green...]`-Utility, geprüft per `grep` gegen
  `dist/public/assets/*.css`.
- `npm run guard:classes`: Exit-Code 0 gegen den aktuellen `src/Client`-Baum.
- Iteration 2 erneut gegen den korrigierten Stand ausgeführt: `dotnet build` 0/0,
  `dotnet test` 994/0/7/1001, `npm run test:scripts` 77/0, `npm run build` erfolgreich,
  `npm run guard:classes` Exit-Code 0. `node_modules` vor/nach `npm install
  --package-lock-only` als unveränderter Symlink auf den Haupt-Tree bestätigt.

### Diary-Notiz
Was: Wächter gegen undefinierte `prop.className`-Klassen gebaut (Node-Skript + Expecto-
Subprozess-Test + Unit-Tests), sechs echte stille Funde behoben (ErrorDisplay.warning,
qa-page, card-header, sheet-header-sub, animate-neon-pulse, text-glow-green), fünf
irreduzible Durchreiche-Parameter begründet ausgenommen. Iteration 2: Gedankenstriche in
neuen Kommentaren/JSON-Begründungen entfernt, `package-lock.json` synchronisiert,
Fund-Zählung in ADR 0041/Outcome korrigiert.
Dateien: siehe FILE_LIST oben.
Warum: ADR 0031 fand sieben DaisyUI-Klassen ohne Wirkung; ohne Wächter kehrt das Muster
zurück. Die Aufgabenverfeinerung selbst fand bereits zwei Beispiele (ErrorDisplay.warning,
qa-page); der erste echte Lauf des fertigen Wächters fand vier weitere.
Ergebnis: Build grün (0 Fehler/Warnungen), dotnet test 994/1001 grün (7 übersprungen wie
immer), npm run test:scripts 77/77 grün, npm run build grün, npm run guard:classes
Exit-Code 0.
