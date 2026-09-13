# design-system — Index

Catalog of everything in this bounded context: tasks by status, ADRs scoped to this BC,
research touching this BC, and concept synthesis pages.

> Updated by: `model` (tasks), `work` (BC-scoped ADRs, concept page links), `research` (BC-scoped reports).

---

## Tasks by status

<!-- task-counts:start -->
- **Backlog:** 8
- **Todo:** 0
- **Doing:** 0
- **Done:** 11
<!-- task-counts:end -->

### Todo
<!-- todo-list:start -->
<!-- todo-list:end -->

### Doing
<!-- doing-list:start -->
<!-- doing-list:end -->

### Done (most recent first; older entries kept for prior-art search)
<!-- done-list:start -->
- **design-system-y6gkd** — Kategorienauswahl (BottomSheet.categoryPicker) im Styleguide und alle Styleguide-Sektionen im Claude-Design-Export (feature) — `done/design-system-y6gkd-kategorienauswahl-und-alle-sektionen-im-export.md`
- **design-system-f7k2p** — "Bug: App lädt ihre Schriften nicht (Google-Fonts-@import hinter @theme wird verworfen); Lösung: Schriften selbst ausliefern, auch im Claude-Design-Export" (bug) — `done/design-system-f7k2p-google-fonts-import-hinter-theme-block-wird-verworfen.md`
- **design-system-h58y4** — Erst-Sync des Design-Systems nach Claude Design ("BudgetBuddy Design System") und Sichtprüfung durch Roman (chore) — `done/design-system-h58y4-erst-sync-nach-claude-design.md`
- **design-system-t82sg** — Wiederholbarer Export des Design-Systems als Claude-Design-Bundle (Tokens + Kassenbuch-Komponenten aus der /styleguide-Route) (feature) — `done/design-system-t82sg-export-bundle-fuer-claude-design.md`
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
- **design-system-thtpp** — Wächter, der Klassennamen findet, die weder Tailwind-Utility noch in styles.css definiert sind (feature) — `backlog/design-system-thtpp-waechter-gegen-undefinierte-klassen.md`
- **design-system-bq4tz** — Badge.defaultProps bricht unter dotnet test (Modul-Initialisierung in View-Modulen mit Design-System-Import) (bug) — `backlog/design-system-bq4tz-badge-defaultprops-bricht-unter-dotnet-test.md`
- **design-system-fq8dn** — DaisyUI raus: sieben tote Klassen in drei Dateien ersetzen, ADR schreiben, CLAUDE.md korrigieren (refactor) — `backlog/design-system-fq8dn-daisyui-klassen-ohne-daisyui.md`
- **design-system-t3vqr** — Neue Money-Varianten in Styleguide-Dokument und /styleguide-Route nachziehen (chore) — `backlog/design-system-t3vqr-neue-money-varianten-in-styleguide-und-route.md`
- **design-system-wa6wz** — Skript für headless Screenshots beliebiger App-Routen (Werkzeug für das visuelle Gate) (feature) — `backlog/design-system-wa6wz-skript-fuer-seiten-screenshots.md`
- **design-system-an22r** — Export erzeugt selbst, was Claude Design beim Self-Check nachgezogen hat: @dsCard-Marker mit name/subtitle/viewport, thumbnail.html, @kind-Annotation (feature) — `backlog/design-system-an22r-export-uebernimmt-claude-design-manifest-ergaenzungen.md`
- **design-system-hadjy** — "Bug: Claude-Design-Export trägt @font-face mit absolutem url(/fonts/...) in styles.css, Schriften laden in Claude Design nicht" (bug) — `backlog/design-system-hadjy-export-font-face-absolute-url.md`
- **design-system-w4n9r** — E2E-/Playwright-Testinfrastruktur für die /styleguide-Route und den Export-Bundle-Build (chore) — `backlog/design-system-w4n9r-e2e-test-fuer-styleguide-export.md`
<!-- backlog-list:end -->

## ADRs scoped to this BC

<!-- adr-local:start -->
- **0027** — Kategorienauswahl als eine Karte mit beiden Zuständen; Sektions-Export-Umfang und -Ausnahmen; Portal-Karten schließen sich nacheinander — 2026-09-11 — `knowledge/decisions/0027-kategorienauswahl-und-vollstaendiger-sektions-export.md`
- **0026** — Schriften (Inter, JetBrains Mono, Space Grotesk) selbst ausliefern statt Google-Fonts-Import (löst Decision 3 von 0024 ab) — 2026-09-11 — `knowledge/decisions/0026-schriften-selbst-ausliefern-statt-google-fonts-import.md`
- **0024** — Export-Bundle rendert aus vite build und preview (nicht vite dev); Navigation bekommt additive nicht-fixe Preview-Varianten; Fonts per explizitem Link geladen (Decision 3 abgelöst durch 0026) — 2026-09-11 — `knowledge/decisions/0024-export-bundle-build-nicht-dev-und-nicht-fixe-navigation-preview.md`
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
