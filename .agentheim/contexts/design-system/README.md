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

## Open questions
- Braucht der Styleguide eine *lebende* Komponenten-Galerie (Storybook-artig) oder reicht
  das Markdown-Dokument + CLAUDE.md-Referenz? (offen, erst bei Bedarf)
