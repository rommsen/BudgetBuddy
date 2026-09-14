---
id: design-system-an22r
title: "Export erzeugt selbst, was Claude Design beim Self-Check nachgezogen hat: @dsCard-Marker mit name/subtitle/viewport, thumbnail.html, @kind-Annotation für Tailwind-Interna"
status: backlog
type: feature
context: design-system
created: 2026-09-11
completed:
depends_on: []
blocks: []
tags: [captured, claude-design, export]
related_adrs: []
related_research: []
prior_art: []
---

## Why
Beim Re-Sync in `ledger-xbrht` (2026-09-11) baute Claude Design das Manifest erst neu,
nachdem Roman im Projekt-Chat darum bat. Der dortige Agent hat dabei 26 Dateien im
Projekt geändert. Die Richtung bleibt Repo → Claude Design (design-system-README), der
nächste `npm run export:design-system` plus Re-Sync überschreibt diese Änderungen also.
Damit sie nicht bei jedem Sync verloren gehen und der Self-Check ohne Nacharbeit
durchläuft, soll der Export sie selbst erzeugen.

## What
Was Claude Design am 2026-09-11 nachgezogen hat (aus dem Projekt gelesen):
1. **`@dsCard`-Marker mit Attributen**: `<!-- @dsCard group="Components" viewport="880x280"
   name="Kategorienauswahl" subtitle="…" -->` statt nur `group`. Breite 880, Höhe je Karte
   gemessen. Die Namen sollten aus dem Repo kommen (Sektionstitel der Styleguide-Route),
   die Untertitel ebenso; die von Claude Design erfundenen sind teils falsch
   (Kategorienauswahl: "Raster aus Kategorie-Kacheln mit Aktivzustand", tatsächlich ein
   Sheet mit Chips und Gruppenliste).
2. **`thumbnail.html`** an der Bundle-Wurzel für die Projektkachel (Wortmarke in Space
   Grotesk auf App-Schwarz, rechts ein Streifen Grün, Teal, Orange, Violett). Manifest
   `hasThumbnailHtml: true`.
3. **`/* @kind other */`-Annotation** an Tailwind-internen Custom Properties in `styles.css`
   (`--tw-*`, `--animate-*`, `--ease-*`, `--duration-*`, Line-Heights), damit
   `check_design_system` sie nicht als Design-Tokens zählt; dazu Custom Properties aus
   Komponenten-Selektoren (`space-y-*`, `:hover`/`:focus`/`:disabled`/`:is()`) durch die
   direkten Werte ersetzt. Claude Design selbst schlug vor, "die Annotierung in den
   Export-Skript-Schritt zu ziehen".

Zu entscheiden bei der Verfeinerung: ob 3 wirklich in den Export gehört (Umschreiben des
Tailwind-Builds ist heikel) oder ob die Meldungen hingenommen werden, solange der
Self-Check trotzdem durchläuft.

## Acceptance criteria
- [ ] To be defined during refinement.

## Notes
Captured via `quick-capture` on 2026-09-11 — raw, unrefined. Needs a `modeling` refine pass before
it can be promoted. Zusammenhang: `design-system-hadjy` (absolute Font-URLs im Export)
ist der zweite Fund aus demselben Re-Sync. Lernpunkt für das README: der Self-Check läuft
nicht nach einem DesignSync-Upload, sondern erst, wenn im Projekt-Chat eine Anfrage
gestellt wird (Roman: "Bitte den Karten-Index aus den @dsCard-Markern in preview/ neu
aufbauen").
