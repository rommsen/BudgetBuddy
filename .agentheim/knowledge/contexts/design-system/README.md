# design-system

## Purpose
Stellt die **visuelle Sprache und das Komponenten-Inventar** bereit, mit dem alle
fachlichen BCs ihre UI bauen. Der Code existiert bereits vollständig unter
`src/Client/DesignSystem/` (20 Komponenten + Token-Layer); dieser BC kodifiziert ihn
*retroaktiv* zu einem reviewbaren **Styleguide** und hält ihn als Gate für künftige
UI-Arbeit konsistent.

## Classification
**supporting / frontend-infrastructure** — kein fachlicher Differenzierer für sich, aber
dient direkt dem Vision-Ziel "Roman fühlt den Workflow als angenehmer als YNAB-Web":
das neon-on-dark-Look-and-Feel und die mobile-festen Muster sind Teil dieses Gefühls.
First-class BC für Frontend-Infrastruktur (analog `infrastructure` für Backend/Runtime);
**nicht** ein konkurrierender UI-Infra-BC daneben.

## Actors
- **Roman** — reviewt den Styleguide (das Gate), baut UI gegen ihn.
- **Alle fachlichen BCs** (banking-import, categorization, ynab-sync) — Konsumenten:
  ihre View-Schichten verwenden die DS-Komponenten und -Tokens.

## Ubiquitous language
- **Styleguide** — das reviewbare Dokument (`standards/frontend/styleguide.md`), das die
  visuelle Sprache, Farbsemantik, das Komponenten-Inventar, Muster, Motion und Voice
  festhält. Die **Single Source of Truth** für UI-Entscheidungen. *Das Gate* (s.u.).
- **Token** — benannte Tailwind/DaisyUI-Klasse in `Tokens.fs` (Farben, Glow-Shadows,
  Spacing, Fonts, Presets). UI referenziert Tokens statt hartkodierter Klassen.
- **Visuelle Sprache: neon-on-dark** — dunkler Grund, neon-Akzente
  (`neon-green/teal/orange/purple/pink/red`) mit Glow-Shadows, Mono-Font für Zahlen/Daten.
- **Farbsemantik** — feste Bedeutungen: Orange = primär/CTA, Teal = sekundär/aktiv,
  Green = Erfolg/positiv, Red = Fehler/Gefahr, Purple/Pink = Akzent/Kategorie.
- **DS-Komponente** — eine der 20 Feliz-Komponenten unter `src/Client/DesignSystem/`
  (Button, Card, Badge, Input, Modal, Toast, Stats, Money, Table, Loading, ErrorDisplay,
  Icons, Navigation, PageHeader, Primitives, Tokens, Form, BottomSheet, Swipe, Viewport).
  Inline-Feliz für etwas, das eine DS-Komponente abdeckt, gilt als **Drift**.
- **Muster (Pattern)** — projektweite Interaktions-Konvention oberhalb einzelner
  Komponenten: **visual-viewport-Sheet + Click-Commit** (ADR 0005, mobile-keyboard-fest),
  **Konto-/Picker-Auswahl** (ADR 0004), Spring-Easing/Skeleton (Mobile-Polish),
  **Toast-Platzierung + sanfter Zwei-Phasen-Abgang** (ADR 0007: Desktop unten-rechts /
  Mobile oben über der Bottom-Nav; *exiting*-markieren → nach Exit-Animation entfernen).
- **Drift** — Abweichung des realen View-Codes vom Styleguide: hartkodierte Farben statt
  Tokens, inline-Feliz statt DS-Komponente, abweichende Muster.

## The styleguide gate
Der Styleguide (`design-system-001`) ist das **Gate** für UI-Arbeit projektweit:
- Jeder neu erfasste Frontend-/UI-Task in **jedem** BC muss `design-system-001` in
  `depends_on` führen.
- Kein UI-Task wird nach `todo/` promotet, bevor der Styleguide done **und mit Roman
  reviewt** ist.
- Der Styleguide-Task selbst ist vom Gate ausgenommen (er *ist* das Gate).

Bestehende UI-Arbeit (Quick Add, Mobile-Overhaul) ist bereits done und wird nicht
rückwirkend blockiert; der einzige offene UI-Task `ynab-002` (ynab-sync) hängt ab jetzt
am Gate (Hard-Enforcement, gewählt 2026-06-13).

## Relationships with other contexts
- **Supplier für** banking-import, categorization, ynab-sync — liefert die UI-Bausteine,
  gegen die deren View-Schichten bauen (Customer-Supplier; DS ist upstream der UI).
- **Conformist gegenüber** Tailwind 4 (Token-Layer ordnet sich dessen Klassen-Modell
  unter). **Kein DaisyUI:** trotz `CLAUDE.md`s früherer Tech-Stack-Zeile nie installiert
  (nicht in `package.json`/`node_modules`); sieben Stellen setzten trotzdem inerte
  DaisyUI-Klassen (`table-sm`/`-lg`/`-zebra`, `range`/`range-sm`), unbemerkt seit dem Bau
  der `Table`-Komponente. Entscheidung Roman: nachrüsten verworfen, ein zweites
  Gestaltungssystem neben dem eigenen Token-Layer würde die Frage aufwerfen, welches
  gewinnt (`design-system-fq8dn`, ADR 0031).
- Voller Kontext-Überblick: `../../context-map.md`.

## Drift-Audit-Ergebnis (design-system-002, 2026-06-18)
Erstes Audit des View-Codes gegen den Styleguide-Gate. **Befund: wenig Token-/viel
Komponenten-Drift.**
- *Sauber:* Standard-Tailwind-Palette-Farben (0), inline `style.color/fontSize` (0) — der
  Token-Layer hält an den Farb-/Spacing-Call-Sites.
- *Konsolidiert (Token-Drift, risikoarm):* rohe Hex `text-[#0a0a0f]` (dunkle Schrift auf
  Neon) → neues `Colors.onNeon`/`onNeonMuted`; arbitrary `text-[10px]/[11px]` → neues
  `FontSizes.micro`/`microPlus` (**ADR 0009**) — sowohl in den fachlichen Views als auch
  DS-komponenten-intern (`Badge.fs`, `Navigation.fs`, `Stats.fs`). Re-Audit: **0**
  token-gedeckte Residuen. Gerenderter CSS-String byte-identisch, kein visueller Change.
- *Gesplittet (Komponenten-Drift, per-BC-Risiko):* ~34 rohe `Html.button` (teils bewusst
  custom: Click-Commit/ADR 0005, Swipe, `action-chip`) → `design-system-006`; 4 rohe
  `Html.svg` → `Icons` → `design-system-007`. Bewusst NICHT gemega-refactored.

## App-Mark / Branding (design-system-008, 2026-06-19)
Der **App-Mark** ("B im Sync-Ring") ist die Marken-Identität von BudgetBuddy fürs
Home-Screen-/PWA-Icon — gehört dem Design-System, geht durchs Styleguide-Gate.
- **Konzept:** fettes "B" (Identität) zentriert in einem **Sync-Ring** mit zwei
  Pfeilköpfen (Funktion: Bank→YNAB; echot `Icons.sync`).
- **Farbe:** Signatur-Gradient `135deg #00d4aa→#00ff88→#ff6b2c` (= `.gradient-text` /
  `Presets.gradientText`) + dezenter Neon-Glow. **Klein-Variante ≤32px:** vereinfacht zu
  solid `#00ff88` "B" ohne Ring/Gradient (Lesbarkeit bei 16/32px).
- **`theme_color` = `background_color` = `#08081a`** (`--bg-app`, echte App-Surface). Löst
  das generische `#0f172a`-Slate-Drift in `index.html` ab — die Verdrahtung (Manifest +
  Meta-Tags + Generator-Config) macht `infra-002`.
- **Quell-SVGs (zwei, weil Generatoren uniform skalieren):**
  `src/Client/public/icon-master.svg` (voller Hybrid), `…/favicon.svg` (vereinfacht),
  `…/icon-maskable.svg` (Hybrid auf opakem `#08081a`, ≥20 % Safe-Zone).
- **Raster-Set:** `src/Client/public/icons/` (192/512 transparent, maskable-512,
  apple-touch-180 opak, favicon-16/32 + `favicon.ico`).

## Export-Bundle für Claude Design (design-system-t82sg, 2026-09-11)
`npm run export:design-system` (`scripts/export-design-system.mjs`) erzeugt unter
`dist/design-system/` (git-ignoriert) ein wiederholbares Bundle im Format, das Claude
Design für Design-System-Projekte erwartet: Tokens plus die Kassenbuch-relevanten
Komponenten, **nur in diese Richtung** (Repo → Claude Design, nie zurück).
- **Rendern statt nachbauen.** Das Skript baut die App (`vite build`), startet sie
  headless (`vite preview` + Playwright) und schneidet je Sektion das gerenderte HTML
  aus `/#/styleguide` heraus (kein von Hand geschriebenes Komponenten-Markup im
  Skript). Läuft ohne Backend (rein präsentationale Route).
- **`data-ds-card`**: stabiles Attribut in
  `src/Client/Components/Styleguide/View.fs`, markiert eine Sektion als Export-Ziel.
  Deckt inzwischen fast jede Sektion der Route ab (Liste und Ausnahmen:
  `design-system-y6gkd`, ADR 0027); Komponenten, die per `createPortal` nach
  `document.body` rendern (`BottomSheet`, Kategorienauswahl, `Modal`), werden über
  `data-ds-card-trigger` + Klick gesondert eingesammelt und danach wieder geschlossen,
  bevor die nächste Portal-Karte öffnet.
- **`@dsCard`-Marker**: Zeile 1 jeder `preview/*.html` (`<!-- @dsCard group="…" -->`),
  das Format, über das Claude Design die Karten im Design-System-Bereich baut. Gruppen
  im Bundle: "Colors" (Neon-Palette, Flächen), "Type" (Font-Proben), "Components"
  (Card, Money, Badge, Table, Navigation, Button, Stats, Icons, PageHeader,
  Primitives, Swipe, BottomSheet, Kategorienauswahl, Modal), "Feedback" (Loading,
  ErrorDisplay, Toast), "Forms" (Input, Form), "Brand" (App-Icon). Vollständige Liste
  und Ausnahmen: `design-system-y6gkd`, ADR 0027.
- **`colors_and_type.css`**: der `@theme`-Block aus `src/Client/styles.css` plus die
  Basis-Variablen, auf die er per `var()` verweist, vollständig aufgelöst (das Skript
  bricht ab, wenn eine referenzierte Variable fehlt), plus drei `@font-face`-Regeln
  (Inter, JetBrains Mono, Space Grotesk) auf die mitkopierten Font-Dateien. Die
  "Colors"-/"Type"-Preview-Karten werden aus genau diesen Werten generiert, nicht von
  Hand gepflegt.
- **Fonts laden lokal.** `fonts/` im Bundle enthält die drei Variable-Font-Dateien
  (Subset "latin", SIL Open Font License 1.1) plus `LICENSE.md`, kopiert aus
  `src/Client/public/fonts/`. `colors_and_type.css` bindet sie per `@font-face` ein;
  die Preview-Dateien binden ohnehin schon `colors_and_type.css` und laden die Fonts
  damit ohne Netzzugriff, genau wie die App selbst. Details: ADR 0026 (löst ADR 0024
  Decision 3 ab).
- **Bekannter App-Bug: behoben durch design-system-f7k2p.** Der frühere
  Google-Fonts-`@import` in `src/Client/styles.css` stand hinter dem `@theme`-Block,
  verstieß damit gegen die CSS-Regel, dass `@import` vor allen anderen Regeln außer
  `@charset`/`@layer` stehen muss, und wurde vom Build deshalb verworfen. Die App
  liefert die drei Schriften jetzt selbst aus (`@font-face`, `public/fonts/`), kein
  Google-Fonts-Import mehr.
- **Navigation-Preview-Varianten** (`Navigation.desktopNavPreview`/`mobileNavPreview`,
  `src/Client/DesignSystem/Navigation.fs`): dieselbe Chrome wie
  `desktopNav`/`mobileNav`, aber ohne `fixed`-Positionierung, zum Einbetten in
  begrenzte Container (Styleguide-Galerie, Export-Bundle). Bewusst additive Funktionen
  statt eines `transform`-Containment-Tricks auf den echten (fixen)
  Navigationskomponenten (siehe `.claude/rules/css-animation-safety.md`).
- Verifikation ist manuell (kein E2E-Harness im Repo): zweifacher Lauf, byte-identisches
  Bundle (Idempotenz); Screenshot-Vergleich Live-Route vs. exportierte Preview-Dateien;
  headless `document.fonts`-Check. Nachrüstung einer automatisierten Absicherung:
  `design-system-w4n9r` (Backlog).
- Hochladen nach Claude Design ist **kein Worker-Task** (kein `DesignSync`-Zugriff, jeder
  Schreibplan wird von Roman freigegeben): das macht `design-system-h58y4`.

## Claude-Design-Projekt und Re-Sync (design-system-h58y4, 2026-09-11)
Das Bundle liegt in Romans Account im Design-System-Projekt **"BudgetBuddy Design
System"**, Projekt-Id `3a764a23-efa6-4d3a-8a66-e0325666d0c3` (Typ
`PROJECT_TYPE_DESIGN_SYSTEM`). Das Projekt "Smolder Design System" im selben Account hat
mit BudgetBuddy nichts zu tun und wird nie beschrieben.

**Re-Sync nach einer DS-Änderung** (Hauptsession mit Roman, kein Worker):
1. `npm run export:design-system` erzeugt `dist/design-system/` neu.
2. `DesignSync get_project` mit der Projekt-Id oben prüft Typ und Schreibrecht.
3. `DesignSync list_files` gegen `find dist/design-system -type f` vergleichen: neue und
   geänderte Pfade werden geschrieben, Pfade, die es lokal nicht mehr gibt, gelöscht.
4. `DesignSync finalize_plan` mit `localDir` = `dist/design-system`, genau diesen
   Schreib- und Löschpfaden; Roman gibt den Plan frei.
5. `DesignSync write_files` mit `localPath` je Datei (Inhalte gehen nicht durch den
   Kontext), danach `delete_files` für entfallene Pfade.
6. `list_files` erneut gegen das lokale Bundle prüfen; Roman sieht sich die Karten in
   claude.ai/design an.

Die Richtung bleibt Repo → Claude Design. Was in Claude Design am System geändert wird,
fließt nicht zurück; Änderungen am Design-System entstehen im Repo und gehen durch das
Styleguide-Gate.

## Styleguide-Sektionen im Export (design-system-y6gkd, 2026-09-11)
Fund aus Romans Sichtprüfung beim Erst-Sync (`design-system-h58y4`): die
Kategorienauswahl (`BottomSheet.categoryPicker`/`categoryPickerLayered`) fehlte im
Bundle, dazu Romans Entscheidung, alle Sektionen der Route zu exportieren statt nur
sechs. Beides erledigt.

- **Kategorienauswahl** (`/styleguide` Sektion 19, `CategoryPickerDemo`): zeigt den
  echten Picker mit erfundenen Beispieldaten (fünf Kategorien in zwei Gruppen,
  Available positiv/negativ/null; Vorschläge + Zuletzt verwendet). Eine Karte
  (`components-categorypicker`) mit beiden Zuständen gleichzeitig (Chips oben,
  Gruppenliste mit Suchfeld darunter), da der echte Picker sie so gleichzeitig zeigt.
  Begründung/Alternativen: ADR 0027.
- **Vollständiger Sektions-Export.** Neu als eigene `data-ds-card`-Karte, aus der DOM
  gerendert (kein von Hand nachgebautes Markup): Kategorienauswahl, Button, Stats,
  Icons, PageHeader, Primitives, Swipe, Loading, ErrorDisplay, Input, Form, Toast,
  Modal. Zusammen mit den bereits vorhandenen (Card, Money, Badge, Table, Navigation,
  BottomSheet) sind das 19 Komponenten-Karten, gruppiert als "Components" (Card,
  Money, Badge, Table, Navigation, Button, Stats, Icons, PageHeader, Primitives,
  Swipe, BottomSheet, Kategorienauswahl, Modal), "Feedback" (Loading, ErrorDisplay,
  Toast) und "Forms" (Input, Form).
- **Bewusst ausgelassen** (keine eigene DOM-Karte, mit Begründung): Farbsemantik- und
  Typografie-Sektion, weil sie bereits über die generierten "Colors"-/"Type"-Karten
  abgedeckt sind (aus denselben Tokens, kein zweites, drift-anfälliges Set); die
  Voice-Sektion, weil sie reiner Fließtext ist und im Bundle-README/SKILL steht statt
  in einer Karte.
- **Portal-Komponenten** (BottomSheet, Kategorienauswahl, Modal) öffnen im Export
  nacheinander, nicht parallel: jede wird über ihren Trigger geöffnet, eingesammelt und
  wieder geschlossen, bevor die nächste öffnet (neue Helferfunktion
  `extractPortalCard` in `scripts/export-design-system.mjs`). Grund und Alternativen:
  ADR 0027.
- Verifikation wie beim Erst-Export: zweifacher Lauf byte-identisch, jede
  `preview/*.html` beginnt in Zeile 1 mit dem `@dsCard`-Marker, headless
  Screenshot-Stichprobe der neuen Karten (Kategorienauswahl, Modal, Toast, Button)
  bestätigt korrektes Rendering. Details und Testzahlen: Diary der Task-Datei.

## Mockups gehören ins Repo, und Frontend-Arbeit hat ein visuelles Gate (2026-09-12)

**Fund aus `ledger-b1xwe`.** Die erste Kassenbuch-Scheibe erfüllte alle neun
Akzeptanzkriterien, bestand die Verifikation und sah trotzdem nichts wie das Zielbild aus:
englisches Zahlenformat, weiße statt rot-grüner Beträge, ungekürzter Bank-Rohtext in der
Memo-Spalte, verschmolzene Spalten, ein Tabellenkopf hinter dem Seitenkopf. Roman sah es
als Erster, nach dem Commit.

Zwei Ursachen, beide im Ablauf, nicht beim Worker:

1. **Das Mockup lag nur in Claude Design.** Der Worker hat es nie gesehen, er hat den
   Fließtext des UX-Konzepts gelesen. Aus "am PC eine Tabelle mit Sticky-Kopf" folgt kein
   Spaltenlayout, aus "Money-Komponente" kein deutsches Zahlenformat. Text ist für
   Gestaltung ein verlustbehafteter Kanal.
2. **Kein Schritt im Ablauf hat das Bild angesehen.** Akzeptanzkriterien, die sich sauber
   in Zusicherungen übersetzen lassen, beschreiben Verhalten, nicht Aussehen. Der Verifier
   prüft Diff und Tests. Niemand prüft das Ergebnis.

**Daraus zwei verbindliche Regeln für jede Frontend-Arbeit:**

- **Das Zielbild liegt im Repo**, unter `docs/ux/<bereich>/mockups/`, als PNG mit
  sprechendem Namen. Ein Screen, den nur Claude Design kennt, existiert für die Umsetzung
  nicht. Der Task verweist im Abschnitt `## What` auf den Pfad; der Conductor gibt ihn dem
  Worker im Spawn-Prompt mit. Worker können Bilder lesen und vergleichen.
- **Vor dem Integrieren wird die gebaute Seite aufgenommen und neben das Mockup gelegt.**
  Headless, in beiden Viewports (390 × 844 und 1440 × 900). Weicht das Bild sichtbar vom
  Mockup ab, ist das ein Verifikations-Fehlschlag wie jeder andere, auch wenn alle
  Akzeptanzkriterien erfüllt sind. Werkzeug dafür: Playwright liegt bereits im Projekt,
  `scripts/export-design-system.mjs` zeigt das Muster (bauen, Preview-Server starten,
  aufnehmen, aufräumen). Ein eigenes Skript für beliebige Routen: `design-system-wa6wz`.

Die Richtung bleibt unverändert Repo → Claude Design. Ein Mockup-Bild im Repo ist kein
Rückfluss von Gestaltung in das Design-System, sondern die Übergabe eines Entwurfs an die
Umsetzung. Was am Design-System selbst geändert wird, entsteht weiterhin im Code und geht
durch das Styleguide-Gate.

**Warum Claude nicht selbst in das Mockup-Projekt schaut:** `DesignSync` liest und schreibt
zwar in beide Richtungen, sieht aber ausschließlich Projekte vom Typ
`PROJECT_TYPE_DESIGN_SYSTEM`. Mockup-Projekte tauchen in `list_projects` nicht auf. Deshalb
der Umweg über das Bild im Repo, der ohnehin robuster ist: er überlebt Chats, Sessions und
Werkzeugwechsel.

## Open questions
- Braucht der Styleguide eine *lebende* Komponenten-Galerie (Storybook-artig) oder reicht
  das Markdown-Dokument + CLAUDE.md-Referenz? (offen, erst bei Bedarf)
