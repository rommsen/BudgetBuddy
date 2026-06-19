---
id: design-system-008
title: PWA-App-Icon (neon-on-dark) — Master-Mark + Icon-Set + Theme-Farbe
status: backlog
type: feature
context: design-system
created: 2026-06-19
completed:
commit:
depends_on: [design-system-001]
blocks: [infra-002]
tags: [frontend, design-system, pwa, icon, branding]
related_adrs: []
related_research: []
prior_art: []
---

## Why
Roman will BudgetBuddy als PWA mit **eigenem Icon** ("mit eigenem Icon und allem, was
dazugehört", 2026-06-19). Die PWA-Mechanik (`infra-002`) braucht dafür ein vollständiges
App-Icon-Set plus eine definierte Theme-/Hintergrund-Farbe — aber das ist nicht nur
Plumbing: **die visuelle Identität gehört dem Design-System** und geht durchs
Styleguide-Gate (`design-system-001`). Bisher hat BB kein eigenes Home-Screen-/App-Icon;
ein installiertes BB würde mit dem Default-Favicon auf dem Homescreen liegen — das
unterläuft das Vision-Ziel "fühlt sich angenehmer an als YNAB-Web".

## What
Eine **Master-SVG-Mark** im neon-on-dark-Stil entwerfen (konsistent mit der
Farbsemantik: Orange = primär/CTA, Teal = sekundär, dunkler Grund) und daraus das volle
PWA-Icon-Set ableiten. Außerdem die `theme_color`/`background_color`-Werte als konkrete
Hex aus dem Token-Layer festlegen, damit `infra-002` sie im Manifest und in den
iOS-Meta-Tags nur noch referenzieren muss.

Scope-Entscheid (Roman 2026-06-19): **ich (Claude) entwerfe den Mark** im neon-on-dark-Stil
— kein geliefertes Fremdlogo, kein Platzhalter.

## Acceptance criteria
- [ ] Eine **Master-SVG-Mark** für BudgetBuddy existiert (im Client-Asset-Pfad), neon-on-dark,
      stimmig mit der Farbsemantik/Token-Palette des Design-Systems.
- [ ] Vollständiges **Icon-Set** generiert und im Build verfügbar: mind. `192×192`, `512×512`,
      ein **maskable** `512×512` (mit ≥20 % Safe-Zone-Padding, sonst beschneiden Android-Launcher),
      `apple-touch-icon` `180×180`, und ein Favicon (`.svg` und/oder `.ico`).
- [ ] **`theme_color` und `background_color`** als konkrete Hex-Werte definiert und dokumentiert,
      abgeleitet aus dem Token-Layer (dunkler Grund + Neon-Akzent) — bereit zur Übernahme durch
      `infra-002`.
- [ ] **Gate-Review:** Roman hat den Mark visuell abgenommen — klein (Home-Screen-Größe), auf
      hellem und dunklem Hintergrund, und im maskable-Squircle/Kreis (nichts Wichtiges in der
      Beschnitt-Zone).
- [ ] Entschieden, ob iOS-**Splash-Screens** Teil des Pakets sind (ja → Matrix/Generierung mit
      `infra-002` abstimmen; nein → bewusst weggelassen, notiert).

## Notes
- **Styleguide-Gate:** `depends_on: [design-system-001]` — der Styleguide ist die Source of
  Truth für visuelle Sprache, Farbsemantik und Voice; der App-Mark ist Marken-Identität und
  wird gegen das Gate reviewt.
- **Farb-Herkunft:** `theme_color`/`background_color` aus dem bestehenden Token-Layer
  (`src/Client/DesignSystem/Tokens.fs`, Farbsemantik; vgl. ADR 0009 `onNeon`-Palette) ableiten —
  **nicht** neu erfinden, damit das installierte App-Fenster und der Status-Bar-Look zur
  bestehenden Optik passen.
- **Tooling-Abstimmung mit `infra-002`:** `vite-plugin-pwa` + `@vite-pwa/assets-generator`
  können das gesamte Set (inkl. maskable + apple-touch) aus einer Master-SVG erzeugen — d. h.
  der eigentliche Liefergegenstand hier ist die **Master-SVG + die Farbwerte**, die Generierung
  selbst kann in `infra-002` mitlaufen.
- **Offene Refine-Frage (der eigentliche Design-Schritt):** Wie sieht der Mark aus? Monogramm
  ("BB"/"₿uddy"), abstraktes Sync-/Pfeil-Symbol, oder Geld-/Brücken-Motiv (Bank→YNAB)? Das
  braucht eine kurze REFINE-Runde mit Romans Input, bevor der Task `todo`-reif ist.
