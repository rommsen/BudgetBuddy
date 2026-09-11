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
- **Conformist gegenüber** Tailwind 4 / DaisyUI (Token-Layer ordnet sich deren Klassen-
  Modell unter).
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
  `src/Client/Components/Styleguide/View.fs`, markiert eine Sektion als Export-Ziel
  (`components-card`, `-money`, `-badge`, `-table`, `-navigation`; `BottomSheet`
  portalt nach `document.body` und wird über `data-ds-card-trigger` + Klick gesondert
  eingesammelt).
- **`@dsCard`-Marker**: Zeile 1 jeder `preview/*.html` (`<!-- @dsCard group="…" -->`),
  das Format, über das Claude Design die Karten im Design-System-Bereich baut. Gruppen
  im Bundle: "Colors" (Neon-Palette, Flächen), "Type" (Font-Proben), "Components"
  (Card/Money/Badge/Table/BottomSheet/Navigation), "Brand" (App-Icon).
- **`colors_and_type.css`**: der `@theme`-Block aus `src/Client/styles.css` plus die
  Basis-Variablen, auf die er per `var()` verweist, vollständig aufgelöst (das Skript
  bricht ab, wenn eine referenzierte Variable fehlt). Die "Colors"-/"Type"-Preview-Karten
  werden aus genau diesen Werten generiert, nicht von Hand gepflegt.
- **Fonts laden explizit.** Jede Preview-Datei bindet die Google-Fonts-URL aus
  `src/Client/styles.css` per `<link>` ein (aus dem Quelltext extrahiert, nicht im
  Skript hart kodiert). Grund: der gebaute `dist/design-system/styles.css` enthält den
  `@import` selbst nicht mehr (App-Bug, s. u.) und ohne den expliziten `<link>` würden
  Card/Money/Navigation im Bundle in Ersatzschriften statt Inter/Space Grotesk/
  JetBrains Mono rendern. Details und Alternativen: ADR 0024.
- **Bekannter App-Bug (nicht Teil dieses Tasks):** der Google-Fonts-`@import` in
  `src/Client/styles.css` steht hinter dem `@theme`-Block, verstößt damit gegen die
  CSS-Regel, dass `@import` vor allen anderen Regeln außer `@charset`/`@layer` stehen
  muss, und wird vom Build deshalb verworfen. Die gebaute App selbst rendert dadurch in
  Ersatzschriften. Als Bug-Backlog-Item erfasst: `design-system-f7k2p`.
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

## Open questions
- Braucht der Styleguide eine *lebende* Komponenten-Galerie (Storybook-artig) oder reicht
  das Markdown-Dokument + CLAUDE.md-Referenz? (offen, erst bei Bedarf)
