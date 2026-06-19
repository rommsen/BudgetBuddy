# design-system — Index

Catalog of everything in this bounded context: tasks by status, ADRs scoped to this BC,
research touching this BC, and concept synthesis pages.

> Updated by: `model` (tasks), `work` (BC-scoped ADRs, concept page links), `research` (BC-scoped reports).

---

## Tasks by status

<!-- task-counts:start -->
- **Backlog:** 0
- **Todo:** 0
- **Doing:** 0
- **Done:** 7
<!-- task-counts:end -->

### Todo
<!-- todo-list:start -->
<!-- todo-list:end -->

### Doing
<!-- doing-list:start -->
<!-- doing-list:end -->

### Done (most recent first; older entries kept for prior-art search)
<!-- done-list:start -->
- **design-system-008 · PWA-App-Icon — "B" im Sync-Ring, Signatur-Gradient** — 2026-06-19 — `done/design-system-008-pwa-app-icon.md` (Master-SVG B-im-Sync-Ring + Gradient #00d4aa→#00ff88→#ff6b2c, vereinfachte solid-green favicon.svg, Raster-Set 192/512/maskable/apple-touch/16/32/.ico unter src/Client/public/; theme=bg #08081a für infra-002; Verifier PASS iter 1; **AC5 = Romans visuelles Gate offen**)
- **design-system-007 · Konsolidierung roher Html.svg → Icons-DS-Komponente (SyncFlow-Views)** — 2026-06-19 — `done/design-system-007-svg-to-icons-consolidation.md` (3 SVGs → Icons.plus/check + neues Icons.chevronLeft; Toggle-Check bewusst roh; DS-Normalisierung akzeptiert (Roman) — Farbe identisch, Größe/Stroke DS-Standard; Verifier PASS iter 2)
- **design-system-002 · Drift-Audit + Konsolidierung des View-Codes auf den Styleguide** — 2026-06-18 — `done/design-system-002-drift-audit-consolidation.md` (9e9526a; Token-Drift voll auf `Colors.onNeon`/`FontSizes.micro` gehoben (byte-identisch), Komponenten-Drift gesplittet → 006/007; ADR 0009; Verifier PASS iter 2)
- **design-system-005 · Toast-Mobile-Platzierung — eingerückter Streifen statt Full-Bleed** — 2026-06-18 — `done/design-system-005-toast-mobile-fit.md` (895e56e; Folge-Bug zu 004; ADR 0007 amended; headless mobil verifiziert)
- **design-system-004 · Toast-Politur — sanfter Abgang, Positionierung & Styleguide-Motion** — 2026-06-16 — `done/design-system-004-toast-polish.md` (18cf474; Zwei-Phasen-Exit, ADR 0007, 8 Tests)
- **design-system-003 · Live In-App /styleguide-Route (visueller Styleguide)** — 2026-06-13 — `done/design-system-003-live-styleguide-route.md` (build grün + verifiziert; offen: Romans visueller Check)
- **design-system-001 · Styleguide retroaktiv kodifizieren (das Gate)** — 2026-06-13 — `done/design-system-001-styleguide.md` (reviewt+akzeptiert als geschriebener Begleiter; visuelle Route → design-system-003)
<!-- done-list:end -->

### Backlog
<!-- backlog-list:start -->
<!-- backlog-list:end -->

## ADRs scoped to this BC

<!-- adr-local:start -->
- **0009** — Token-Namen für „dunkle Schrift auf Neon" (`onNeon`) + Mikro-Schriftgrößen (`micro`/`microPlus`) — 2026-06-18 — `../../knowledge/decisions/0009-onneon-foreground-and-micro-font-tokens.md`
- **0007** — Toast-Platzierung (Desktop unten-rechts / Mobile oben) und Zwei-Phasen-Abgang — 2026-06-16 — `knowledge/decisions/0007-toast-placement-and-soft-exit.md`
<!-- adr-local:end -->

## Research touching this BC

<!-- research-local:start -->
<!-- research-local:end -->

## Concepts (opt-in synthesis pages)

<!-- concepts:start -->
<!-- concepts:end -->

## Pointers

- BC README (visuelle Sprache, Tokens, Muster, das Gate): `README.md`
- Styleguide-Artefakt (entsteht in design-system-001): `../../../standards/frontend/styleguide.md`
