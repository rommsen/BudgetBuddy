---
id: 0024
title: Export-Bundle rendert aus `vite build`+`preview` (nicht `vite dev`); Navigation bekommt additive nicht-fixe Preview-Varianten; Fonts per explizitem Link geladen
scope: design-system
status: accepted
date: 2026-09-11
supersedes: []
superseded_by: []
related_tasks: [design-system-t82sg]
related_adrs: []
related_research: []
---

# ADR 0024: Export-Bundle rendert aus `vite build`+`preview`; Navigation bekommt additive nicht-fixe Preview-Varianten; Fonts per explizitem Link geladen

## Context
`design-system-t82sg` baut ein wiederholbares Export-Kommando
(`npm run export:design-system`), das die `/#/styleguide`-Route headless rendert
(Playwright) und je Sektion das gerenderte HTML nach `dist/design-system/preview/*.html`
schneidet. Drei Design-Fragen kamen dabei ohne bestehende Antwort im Repo:

1. **Woher kommt das CSS, das die Preview-Dateien standalone einbinden
   (`../styles.css`)?** `vite dev` injiziert CSS zur Laufzeit per JS/HMR. In diesem
   Modus gibt es keine stabile, statische CSS-Datei, die man kopieren könnte.
2. **Wie zeigt man die `Navigation`-Komponente (Desktop-Navbar, Mobile-Bottom-Nav) in
   einer begrenzten Galerie-Sektion, ohne dass sie sich als `fixed`-Overlay über die
   ganze Seite legt?** `Navigation.desktopNav`/`mobileNav` sind absichtlich `fixed`
   (App-Shell-Kontrakt).
3. **Wie laden die drei Marken-Fonts (Inter, Space Grotesk, JetBrains Mono) in den
   standalone geöffneten Preview-Dateien?** Bei der Iteration-1-Prüfung stellte sich
   heraus, dass das gebaute `dist/design-system/styles.css` weder eine `@import`- noch
   eine `@font-face`-Regel enthält: der Google-Fonts-`@import url(...)` in
   `src/Client/styles.css` (Zeile 50) steht **hinter** dem `@theme`-Block, was gegen
   die CSS-Regel verstößt, dass `@import` allen anderen Regeln außer `@charset`/`@layer`
   vorausgehen muss. Der Tailwind-4-Build verwirft die Regel deshalb still (Build-Log:
   "@import rules must precede all rules"). Das ist ein **bestehender App-Bug**, nicht
   erst durch den Export entstanden: die gebaute App selbst rendert schon in
   Ersatzschriften statt Inter/Space Grotesk/JetBrains Mono. Für das Bundle reicht es
   nicht, den App-Bug zu erben, denn Claude Design liest die Preview-Dateien als
   Vorbild für die Typografie.

## Decision
1. **Export-Skript nutzt `vite build` (in ein temporäres Verzeichnis) + `vite preview`,
   nicht `vite dev`.** `vite build` erzeugt eine echte, gehashte CSS-Datei
   (`assets/*.css`), die 1:1 als `dist/design-system/styles.css` kopiert wird: die
   Preview-Dateien binden dasselbe CSS ein wie die gebaute App. `vite preview` dient
   nur als Playwright-Ziel (auf einem vorab reservierten freien Port,
   `--strictPort`), läuft komplett ohne Backend und wird nach dem Export gestoppt
   (auch bei Fehlern), das temporäre Build-Verzeichnis danach gelöscht.
2. **`Navigation.fs` bekommt zwei neue, additive Funktionen**
   `desktopNavPreview`/`mobileNavPreview`: dieselbe Chrome (Klassen, Icons, Items) wie
   `desktopNav`/`mobileNav`, aber mit `relative`/`w-full` statt `fixed …`-Klassen.
   Die Styleguide-Galerie (und damit der Export) rendert diese Preview-Varianten, die
   echten `desktopNav`/`mobileNav` bleiben unverändert und weiter `fixed`.
3. **Jede Preview-Datei bekommt einen expliziten `<link rel="stylesheet">` auf die
   Google-Fonts-URL**, die das Export-Skript per Regex aus `src/Client/styles.css`
   extrahiert (nicht hart im Skript kodiert). Damit laden die Fonts im Bundle korrekt,
   unabhängig vom App-Bug. Der App-Bug selbst wird in diesem Task **nicht** behoben
   (Umfang: nur der Export); er ist als eigenes Bug-Backlog-Item erfasst
   (`design-system-f7k2p`).

## Consequences
### Positive
- Preview-Dateien sind wirklich standalone (Doppelklick im Browser öffnet sie korrekt,
  Farben/Layout stimmen mit der App überein, Fonts laden unabhängig vom App-Bug):
  verifiziert per Screenshot-Vergleich und per headless `document.fonts`-Check
  (Task-Diary).
- Zwei aufeinanderfolgende Läufe erzeugen ein byte-identisches Bundle (verifiziert).
- Kein Risiko, dass die echte App-Navigation durch einen Containment-Trick
  (`transform` auf einem Ahnen-Container) kaputtgeht: genau das verbietet
  `.claude/rules/css-animation-safety.md` für `fixed`-Kinder wie Modal/BottomSheet/
  Toast, und der Trick hätte exakt diese Klasse von Bug provoziert.
- Der App-Bug (Fonts laden in der gebauten App nicht) ist jetzt sichtbar dokumentiert
  statt still im Bundle mitgeschleppt zu werden.

### Negative
- Ein `vite build`-Lauf (rund 30s) ist Teil jedes Exports, spürbar langsamer als ein
  `vite dev`-Start. Für ein manuell angestoßenes Re-Sync-Kommando akzeptabel.
- Zwei zusätzliche Funktionen in `Navigation.fs`, die nur für Galerie/Export existieren
  und nicht Teil der echten App-Navigation sind: geringe, bewusste Duplikation statt
  eines Positionierungs-Parameters auf den bestehenden Funktionen (der auch die
  Hauptnutzung anfassen würde).
- Das Bundle lädt Fonts über einen zusätzlichen, expliziten `<link>`, den die echte App
  (wegen des Bugs) so nicht hat. Bis der App-Bug behoben ist, driftet das Bundle in
  diesem einen Punkt bewusst positiv von der aktuell gebauten App ab (Ziel:
  Fonts wie in der *beabsichtigten* Typografie, nicht wie im aktuell kaputten Build).

## Alternatives considered
- **`vite dev` + Zusammenbauen des CSS aus dem HMR-Client:** kein stabiler Pfad zu
  einer einzigen, ladbaren CSS-Datei; hätte das Vite-Interna nachbauen müssen.
- **`transform`/`contain: layout` auf dem Galerie-Container, um die echte
  `desktopNav`/`mobileNav` (`fixed`) einzufangen:** genau das Muster, das
  `.claude/rules/css-animation-safety.md` für Container mit `fixed`-Kindern verbietet
  (verändert den Containing Block, bricht potenziell andere `fixed`-Elemente in
  Reichweite). Verworfen zugunsten der additiven Preview-Funktionen.
- **`desktopNav`/`mobileNav` um einen `positionClass`-Parameter erweitern:** hätte auch
  die Hauptnutzung (`src/Client/View.fs`) anfassen müssen für einen reinen
  Galerie-/Export-Bedarf: mehr Risiko für weniger Nutzen als zwei neue Funktionen.
- **Fonts im Bundle einfach so lassen, wie der App-Build sie liefert (kaputt):** hätte
  den App-Bug unsichtbar ins Bundle vererbt; Claude Design hätte dann Ersatzschriften
  als Vorbild bekommen. Verworfen zugunsten des expliziten `<link>` plus separatem
  Bug-Backlog-Item für den App-Fehler selbst.
