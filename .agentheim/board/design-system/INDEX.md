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
- **Done:** 29
<!-- task-counts:end -->

### Todo
<!-- todo-list:start -->
<!-- todo-list:end -->

### Doing
<!-- doing-list:start -->
<!-- doing-list:end -->

### Done (most recent first; older entries kept for prior-art search)
<!-- done-list:start -->
- **design-system-bz0gn** — "Neue Filterleisten-Komponenten (ChipBar, FilterPopover, Kategorienauswahl mit Mehrfachauswahl) in die /styleguide-Route aufnehmen" (chore) — `done/design-system-bz0gn-neue-filterleisten-komponenten-chipbar-filterpopover-kategor.md`
- **design-system-an22r** — "Export erzeugt selbst, was Claude Design beim Self-Check nachgezogen hat: @dsCard-Marker mit name/subtitle/viewport und thumbnail.html" (feature) — `done/design-system-an22r-export-uebernimmt-claude-design-manifest-ergaenzungen.md`
- **design-system-tg2wn** — "TextGlows.orange/TextGlows.teal in Tokens.fs sind wie vormals TextGlows.green keine echten Tailwind-Klassen" (chore) — `done/design-system-tg2wn-textglows-orange-textglows-teal-in-tokens-fs-sind-wie-vormal.md`
- **design-system-w4n9r** — "Automatischer Test für den Design-System-Export: läuft in ein temporäres Verzeichnis und prüft das vollständige Bundle samt @dsCard-Markern" (chore) — `done/design-system-w4n9r-e2e-test-fuer-styleguide-export.md`
- **design-system-thtpp** — "Wächter, der Klassennamen findet, die weder Tailwind-Utility noch in styles.css definiert sind" (feature) — `done/design-system-thtpp-waechter-gegen-undefinierte-klassen.md`
- **design-system-j9xnd** — "Stats.fs: die Akzentstreifen-Entscheidung als reine Funktion herausziehen und testen" (chore) — `done/design-system-j9xnd-stats-fs-die-akzentstreifen-entscheidung-als-reine-funktion.md`
- **design-system-q2bmv** — "screenshot-route.mjs: die Aufnahme so testen, dass der BottomSheet-Fehler auch wirklich auffiele" (chore) — `done/design-system-q2bmv-screenshot-route-mjs-die-aufnahme-so-testen-dass-der-bottoms.md`
- **design-system-p3vqk** — "Stats.NoAccent im Styleguide/View.fs und standards/frontend/styleguide.md nachziehen" (chore) — `done/design-system-p3vqk-stats-noaccent-im-styleguide-view-fs-und-standards-frontend.md`
- **design-system-m4kp7** — "Präsentationale Routen ohne Backend aufnehmen: screenshot-route.mjs startet für /styleguide keinen Server" (feature) — `done/design-system-m4kp7-praesentationale-routen-ohne-backend-aufnehmen.md`
- **design-system-k4mtx** — "DaisyUI-Verweise in template/** und docs/MILESTONE-PLAN.md auf ADR 0031 nachziehen" (chore) — `done/design-system-k4mtx-template-daisyui-verweise-gegen-budgetbuddys-adr-0031-entsch.md`
- **design-system-k2vgz** — "Abgelöste Doku-Sätze aufräumen: docs/DESIGN-SYSTEM.md und docs/README.md, Farbpalette vorher sichern" (refactor) — `done/design-system-k2vgz-alte-doku-saetze-abloesen-oder-loeschen.md`
- **design-system-t3vqr** — "Neue Money-Varianten in Styleguide-Dokument und /styleguide-Route nachziehen" (chore) — `done/design-system-t3vqr-neue-money-varianten-in-styleguide-und-route.md`
- **design-system-e5vfc** — "Wächter-Abdeckung gegen den eigenen Verlust sichern: erfasste Startup-Typen gegen die Dateien unter src/Client/DesignSystem/ abgleichen" (chore) — `done/design-system-e5vfc-waechter-abdeckung-gegen-eigenen-verlust-sichern.md`
- **design-system-yxrxb** — "Wartepredikat von screenshot-route.mjs blockiert die /styleguide-Route, deren Demo-Inhalte dauerhaft Spinner und Skeletons zeigen" (bug) — `done/design-system-yxrxb-wartepredikat-blockiert-styleguide-route.md`
- **design-system-hadjy** — "Bug: Claude-Design-Export trägt @font-face mit absolutem url(/fonts/...) in styles.css, Schriften laden in Claude Design nicht" (bug) — `done/design-system-hadjy-export-font-face-absolute-url.md`
- **design-system-bq4tz** — "Eager ausgewertete Top-Level-Werte im Design-System zu Funktionen machen, damit View-Module unter dotnet test testbar werden" (bug) — `done/design-system-bq4tz-badge-defaultprops-bricht-unter-dotnet-test.md`
- **design-system-wa6wz** — "Skript für headless Screenshots beliebiger App-Routen (Werkzeug für das visuelle Gate)" (feature) — `done/design-system-wa6wz-skript-fuer-seiten-screenshots.md`
- **design-system-fq8dn** — "DaisyUI raus: sieben tote Klassen in drei Dateien ersetzen, ADR schreiben, CLAUDE.md korrigieren" (refactor) — `done/design-system-fq8dn-daisyui-klassen-ohne-daisyui.md`
- **design-system-y6gkd** — Kategorienauswahl (BottomSheet.categoryPicker) im Styleguide und alle Styleguide-Sektionen im Claude-Design-Export (feature) — `done/design-system-y6gkd-kategorienauswahl-und-alle-sektionen-im-export.md`
- **design-system-f7k2p** — "Bug: App lädt ihre Schriften nicht (Google-Fonts-@import hinter @theme wird verworfen); Lösung: Schriften selbst ausliefern, auch im Claude-Design-Export" (bug) — `done/design-system-f7k2p-google-fonts-import-hinter-theme-block-wird-verworfen.md`
- **design-system-h58y4** — Erst-Sync des Design-Systems nach Claude Design ("BudgetBuddy Design System") und Sichtprüfung durch Roman (chore) — `done/design-system-h58y4-erst-sync-nach-claude-design.md`
- **design-system-t82sg** — Wiederholbarer Export des Design-Systems als Claude-Design-Bundle (Tokens + Kassenbuch-Komponenten aus der /styleguide-Route) (feature) — `done/design-system-t82sg-export-bundle-fuer-claude-design.md`
- **design-system-008 · PWA-App-Icon — "B" im Sync-Ring, Signatur-Gradient** — 2026-06-19 — — (Master-SVG B-im-Sync-Ring + Gradient #00d4aa→#00ff88→#ff6b2c, vereinfachte solid-green favicon.svg, Raster-Set 192/512/maskable/apple-touch/16/32/.ico unter src/Client/public/; theme=bg #08081a für infra-002; Verifier PASS iter 1; **AC5 = Romans visuelles Gate offen**) — `done/design-system-008-pwa-app-icon.md`
- **design-system-007 · Konsolidierung roher Html.svg → Icons-DS-Komponente (SyncFlow-Views)** — 2026-06-19 — — (3 SVGs → Icons.plus/check + neues Icons.chevronLeft; Toggle-Check bewusst roh; DS-Normalisierung akzeptiert (Roman) — Farbe identisch, Größe/Stroke DS-Standard; Verifier PASS iter 2) — `done/design-system-007-svg-to-icons-consolidation.md`
- **design-system-002 · Drift-Audit + Konsolidierung des View-Codes auf den Styleguide** — 2026-06-18 — — (9e9526a; Token-Drift voll auf `Colors.onNeon`/`FontSizes.micro` gehoben (byte-identisch), Komponenten-Drift gesplittet → 006/007; ADR 0009; Verifier PASS iter 2) — `done/design-system-002-drift-audit-consolidation.md`
- **design-system-005 · Toast-Mobile-Platzierung — eingerückter Streifen statt Full-Bleed** — 2026-06-18 — — (895e56e; Folge-Bug zu 004; ADR 0007 amended; headless mobil verifiziert) — `done/design-system-005-toast-mobile-fit.md`
- **design-system-004 · Toast-Politur — sanfter Abgang, Positionierung & Styleguide-Motion** — 2026-06-16 — — (18cf474; Zwei-Phasen-Exit, ADR 0007, 8 Tests) — `done/design-system-004-toast-polish.md`
- **design-system-003 · Live In-App /styleguide-Route (visueller Styleguide)** — 2026-06-13 — — (build grün + verifiziert; offen: Romans visueller Check) — `done/design-system-003-live-styleguide-route.md`
- **design-system-001 · Styleguide retroaktiv kodifizieren (das Gate)** — 2026-06-13 — — (reviewt+akzeptiert als geschriebener Begleiter; visuelle Route → design-system-003) — `done/design-system-001-styleguide.md`
<!-- done-list:end -->

### Backlog
<!-- backlog-list:start -->
<!-- backlog-list:end -->


## Pointers

- Knowledge half (ADRs / research / concepts / BC README) for this BC: `../../knowledge/contexts/design-system/INDEX.md`
