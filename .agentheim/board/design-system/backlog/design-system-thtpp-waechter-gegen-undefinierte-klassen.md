---
id: design-system-thtpp
title: "Wächter, der Klassennamen findet, die weder Tailwind-Utility noch in styles.css definiert sind"
status: backlog
type: feature
context: design-system
created: 2026-09-13
completed:
depends_on: []
blocks: []
tags: [test-infra, styleguide, sicherheitsnetz]
related_adrs: [0031]
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
