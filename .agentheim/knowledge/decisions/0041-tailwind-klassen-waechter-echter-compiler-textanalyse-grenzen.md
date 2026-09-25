---
id: 0041
title: Tailwind-Klassen-Wächter prüft gegen den echten Compiler, löst dynamische Klassen nur innerhalb reiner Textanalyse auf
scope: design-system
status: accepted
date: 2026-09-25
supersedes: []
superseded_by: []
related_tasks: [design-system-thtpp]
related_adrs: [0031]
related_research: []
---

# ADR-0041: Tailwind-Klassen-Wächter prüft gegen den echten Compiler, löst dynamische Klassen nur innerhalb reiner Textanalyse auf

## Context
ADR 0031 fand sieben DaisyUI-Klassen, die nie etwas taten (DaisyUI war nie installiert).
Der Fund blieb Jahre unbemerkt, weil eine fehlende Klasse keinen Fehler wirft, sie fehlt
einfach lautlos in der Ausgabe. `design-system-thtpp` macht daraus einen Wächter statt
eines einmaligen Aufräumens. Bei der Verfeinerung fanden sich bereits zwei weitere
Beispiele (`ErrorDisplay.warning` nutzte `bg-warning`/`text-warning`, ohne dass
`--color-warning` je in `styles.css` existierte; `qa-page`, ein totes Token ohne jeden
Selektor), und der erste echte Lauf des fertigen Wächters fand vier weitere:
`card-header`/`sheet-header-sub` (tote Klassen ohne jede Wirkung) und
`animate-neon-pulse`/`text-glow-green` (referenziert in `Tokens.fs` und an mehreren
Aufrufstellen, aber ohne je definierte CSS-Regel, derselbe Fund wie ADR 0031, nur
unbemerkt in anderen Komponenten).

## Decision
1. **Der echte Tailwind-4-Compiler ist die Autorität, kein F#-Nachbau.**
   `scripts/tailwind-class-guard.mjs` lädt `@tailwindcss/node`s
   `__unstable__loadDesignSystem(...).candidatesToCss(...)` gegen `src/Client/styles.css`
   und fragt jeden gefundenen Klassen-Token direkt ab. Eine zweite, in F# nachgebaute
   Kopie der Tailwind-Logik wäre genau das Drift-Risiko, das der Kopf-Kommentar in
   `src/Tests/FontFaceRegressionTests.fs` für einen analogen Fall vermeidet, und hätte den
   `bg-warning`-Fund vermutlich gar nicht erkannt (keine eigene Kenntnis, welche
   `@theme`-Farben real existieren).
2. **Node-Skript, aus `dotnet test` per Subprozess mitgeführt.** Damit der Wächter ohne
   Zutun bei jedem `dotnet test`-Lauf mitläuft, ruft `src/Tests/TailwindClassGuardTests.fs`
   das echte Skript per `ProcessStartInfo` auf (Muster aus
   `FontFaceRegressionTests.fs`/`BackendPortGuardTests.fs`), statt seine Logik ein zweites
   Mal in F# zu bauen.
3. **Auflösung dynamischer (`{...}`-interpolierter) Klassen-Tokens hat eine bewusste,
   dokumentierte Grenze: reine Textanalyse, keine F#-Symbolauswertung.** Ein Token wird
   zuerst gegen `Tokens.fs`-Literale aufgelöst (inkl. eigener Tokens.fs-Funktionen mit
   endlich vielen literalen Zweigen, z. B. `StaggerDelays.forIndex`), danach gegen
   `let`-Bindungen im selben Quelltext, deren Zweige (`match`/`function`/`if-then-else`,
   auch Tupel-destrukturiert, auch über mehrere reine Weiterleitungs-Hops) literal sind.
   Was sich so nicht auflösen lässt (Funktionsparameter wie `extraClass`/`animation`/
   `maxHeight`, ein `Option.map`-Pipeline-Ausdruck wie `alignClass`, ein per Match-Pattern
   gebundener Bezeichner wie `cls`, dessen Wert aus einem Aufruf mit einem literalen
   Argument an eine parametrisierte Funktion stammt) landet mit Begründung in
   `scripts/tailwind-class-guard-exceptions.json` (fünf Einträge, jeder bei jedem Lauf
   gegen den aktuellen Scan geprüft: ein Eintrag, den der Scan nicht mehr als unauflösbar
   findet, lässt den Lauf mit Nennung dieses Eintrags scheitern) statt den Lauf ungeprüft
   grün zu lassen.
4. **Vier weitere Funde aus dem ersten echten Lauf behoben, zusätzlich zu den beiden
   bereits in der Aufgabenverfeinerung gefundenen (`ErrorDisplay.warning`, `qa-page`):**
   `card-header`/`sheet-header-sub` (tote Klassen ohne jede CSS-Regel, entfernt) und
   `animate-neon-pulse`/`text-glow-green` (echte, seit ihrem Bau nie wirksame Effekte:
   `animate-neon-pulse` bekam eine neue `@keyframes`-Regel analog zum vorhandenen
   `animate-glow-pulse`-Muster; `text-glow-green` wurde auf die bereits im selben Modul
   vorhandene, funktionierende Arbitrary-Property-Syntax `[text-shadow:...]` umgestellt,
   wie sie der negative Glow-Fall daneben schon nutzte).

## Consequences
### Positive
- Eine Klasse ohne Wirkung wird ab jetzt laut gemeldet statt lautlos zu verpuffen, für
  jede künftige Änderung an `src/Client/**/*.fs`.
- Die Ausnahmeliste ist mechanisch durchgesetzt (kein veralteter Eintrag kann
  unbemerkt liegen bleiben) und bewusst kurz gehalten (fünf Einträge, jeder ein
  Funktionsparameter/Pipeline-Ergebnis, das prinzipiell laufzeitabhängig ist, keine
  feste Tailwind-Klasse).

### Negative
- Die Auflösung dynamischer Tokens ist ein bewusst begrenztes, mit der Zeit gewachsenes
  Geflecht aus Sonderfällen (Tokens.fs-Literale, Tokens.fs-Funktionen, Tupel-Bindungen,
  mehrstufige reine Weiterleitung) statt einer einzigen, sauberen Regel. Ein künftiger,
  bisher nicht gesehener F#-Ausdrucksstil (z. B. Argumentsubstitution durch eine
  parametrisierte Funktion, wie sie `cls` in `Stats.fs` bräuchte) landet vorerst in der
  Ausnahmeliste statt automatisch aufgelöst zu werden.
- `fable_modules/` (Fables lokaler, nicht in git getrackter Paket-Cache unter
  `src/Client/`) muss explizit vom Scan ausgeschlossen werden: sonst meldet der Wächter
  Funde in vendoriertem Bibliothekscode, den dieses Projekt nicht kontrolliert.
- Die Ausnahmeliste (`scripts/tailwind-class-guard-exceptions.json`) wird bei einem per
  `--client-dir` auf ein anderes Verzeichnis umgeleiteten Scan (z. B. den
  Falsifizierbarkeitstest gegen ein Scratch-Verzeichnis) bewusst NICHT geladen: gegen den
  umgeleiteten Baum wäre jeder reale Ausnahmeeintrag zwangsläufig „veraltet“, weil er dort
  naturgemäß nicht vorkommen kann. Ein `--client-dir`-Lauf prüft die Ausnahmeliste selbst
  also nicht mit; nur ein Lauf gegen den Standardpfad `src/Client` tut das.

## Alternatives considered
- **Prüfung gegen das gebaute CSS-Bundle** (ursprünglich in der Aufgabenbeschreibung
  skizziert): hätte `npm run build`/`vite build` vor jedem Lauf gebraucht, langsamer und
  ungenauer als eine direkte Abfrage gegen den Compiler (Risiko von
  Teilstring-Treffern im kompilierten CSS). Verworfen zugunsten von
  `__unstable__loadDesignSystem(...).candidatesToCss(...)`, das ohne Build in unter einer
  Sekunde antwortet.
- **Volle F#-Symbolauswertung für dynamische Tokens** (Typinferenz, cross-file
  Datenflussverfolgung): hätte auch `cls` (Stats.fs) und ähnliche Fälle lösen können,
  wäre aber der Umfang eines eigenen kleinen F#-Compilers innerhalb eines Wächter-Skripts
  gewesen, unverhältnismäßig für fünf verbleibende, gut begründete Ausnahmen.
