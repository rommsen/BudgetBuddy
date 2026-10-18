---
id: design-system-an22r
title: "Export erzeugt selbst, was Claude Design beim Self-Check nachgezogen hat: @dsCard-Marker mit name/subtitle/viewport und thumbnail.html"
status: doing
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

**Entscheidung Roman (Refine-Durchlauf):** nur Punkt 1 (`@dsCard`-Marker mit
`name`/`subtitle`/`viewport`) und Punkt 2 (`thumbnail.html`) umsetzen. Punkt 3
(`@kind`-Annotation, Ersetzen von Custom Properties in Komponenten-Selektoren) entfällt:
das Umschreiben des Tailwind-Builds ist heikel, die Meldungen des Self-Checks dazu werden
hingenommen, solange der Self-Check insgesamt durchläuft (kein hartes Scheitern). Das ist
hier bewusst dokumentiert, keine offene Lücke.

`design-system-hadjy` (absolute Font-URLs im Export, zweiter Fund aus demselben Re-Sync)
ist bereits `done`.

## What

### 1. `@dsCard`-Marker mit `name`/`subtitle`/`viewport`

Heute schreibt `writePreview(id, group, title, innerHtml)`
(`scripts/export-design-system.mjs`) nur `<!-- @dsCard group="…" -->` als Zeile 1 jeder
`preview/*.html`; `title` fließt nur in den `<title>`-Tag. Ziel-Format:
`<!-- @dsCard group="Components" name="Kategorienauswahl" subtitle="…"
viewport="880x280" -->`.

**Eine Quelle für `name`/`subtitle`, kein Duplikat:**
- Für die per `[data-ds-card="…"]`/`[data-ds-card-trigger="…"]` aus der Route gescrapten
  Karten (`COMPONENT_CARDS`, `BOTTOMSHEET_CARD`, `CATEGORY_PICKER_CARD`, `MODAL_CARD`):
  `name`/`subtitle` werden als zwei neue HTML-Attribute direkt am bestehenden
  `data-ds-card`-Element deklariert (bzw. am `data-ds-card-trigger`-Element für die drei
  Portal-Karten, da deren tatsächlicher Inhalt erst zur Laufzeit per
  `extractPortalCard` zusammengebaut wird): `data-ds-card-name="Button"`,
  `data-ds-card-subtitle="…"` in `src/Client/Components/Styleguide/View.fs`, direkt neben
  der jeweiligen `data-ds-card`/`data-ds-card-trigger`-Zeile. Das Skript liest beide
  Attribute per `page.evaluate`/`getAttribute` aus, statt sie ein zweites Mal selbst zu
  pflegen. Der bisherige `title`-Parameter/`COMPONENT_CARDS[].title` entfällt dadurch
  (gleicher Wert, eine Quelle); `<title>` im `<head>` der Preview-Datei nutzt `name`.
  **Subtitle nicht aus dem bestehenden `note "…"`-Text ableiten**: der ist mehrsatzig,
  mit ADR-Verweisen, zu lang für eine Karten-Unterzeile und an mehreren Stellen reiner
  Fließtext ohne 1:1-Bezug zur Karte. Subtitle ist ein neuer, bewusst kurzer
  Ein-Zeiler, inhaltlich korrekt am tatsächlich gezeigten Zustand (Lehre aus dem Fund:
  Claude Design erfand für die Kategorienauswahl "Raster aus Kategorie-Kacheln mit
  Aktivzustand", tatsächlich ein Sheet mit Chips und Gruppenliste).
- Für die vier generierten Token-/Brand-Karten (`color-neon`, `color-surfaces`,
  `type-fonts`, `brand-icon`, gebaut in `writeTokenAndBrandPreviews`/`buildColorNeonHtml`
  etc., nicht aus dem DOM gescrapt): `name`/`subtitle` werden direkt als Literale an der
  jeweiligen `writePreview(...)`-Aufrufstelle in `writeTokenAndBrandPreviews` übergeben
  (einzige Stelle, an der diese vier Karten überhaupt entstehen, kein Duplikat möglich).

**Viewport-Höhe wird je Karte gemessen, Breite bleibt fix 880:**
- `writePreview` schreibt die Datei zunächst ohne `viewport`-Attribut (wie heute, nur mit
  `name`/`subtitle` ergänzt).
- Neue Funktion (z. B. `measureAndPatchViewports(cardIds)`), aufgerufen in `main()` nach
  `extractPreviews`/`writeTokenAndBrandPreviews`, vor `verifyFontsLoaded()`: ein Browser,
  pro Karte `page.setViewportSize({ width: 880, height: 1200 })`,
  `page.goto("file://" + <Pfad zur preview/<id>.html>)`, Höhe messen (z. B.
  `Math.ceil(document.querySelector(".max-w-3xl").getBoundingClientRect().height)`),
  danach Zeile 1 der Datei per Regex um ` viewport="880x<Zahl>"` vor dem schließenden
  `-->` ergänzen und zurückschreiben.
- Neue, reine (browserfrei testbare) Hilfsfunktion `buildDsCardMarker({ group, name,
  subtitle, viewport })`, die genau diese Zeile formatiert: einmal benutzt beim
  initialen Schreiben (ohne `viewport`) und einmal beim Patch (mit `viewport`), damit es
  nur eine Stelle gibt, die das Marker-Format kennt. Exportieren wie `resolveOutDir`.

### 2. `thumbnail.html` an der Bundle-Wurzel

Neue Funktion `buildThumbnailHtml(tokens)` (nutzt dieselben `tokens.neonColors`/
`tokens.backgroundVars` aus `buildColorsAndTypeCss()`, wie `buildBrandIconHtml` es schon
tut): Wortmarke "BudgetBuddy" in `font-display` (Space Grotesk, `@font-face` auf
`fonts/space-grotesk-latin-wght-normal.woff2`, relativer Pfad wie in `FONT_FACE_CSS`, da
`thumbnail.html` auf derselben Ebene wie `fonts/` liegt) auf `--bg-app`
(App-Schwarz), rechts ein Streifen aus vier Blöcken in `--color-neon-green`,
`--color-neon-teal`, `--color-neon-orange`, `--color-neon-purple` (in dieser
Reihenfolge). Geschrieben in `writeDocs()` (oder direkt daneben) nach
`path.join(OUT_DIR, "thumbnail.html")`; `"thumbnail.html"` zusätzlich in den `fixed`-Array
von `expectedBundleFiles()` aufnehmen.

Kein eigenes `manifest.json` nötig: `hasThumbnailHtml: true` setzt Claude Design selbst in
seinem eigenen Manifest, sobald es beim Self-Check eine `thumbnail.html` an der
Bundle-Wurzel vorfindet. Dieses Skript muss kein Manifest schreiben.

### Test-Erweiterung (`scripts/export-design-system.test.mjs`, design-system-w4n9r)

- Browserfreier Unit-Test für `buildDsCardMarker`: korrekte Attribut-Reihenfolge/
  Quotierung, mit und ohne `viewport`.
- Im Vollauf-Test (Browser-gebunden): für jede Karte aus `expectedBundleFiles`
  zusätzlich zu `html.startsWith("<!-- @dsCard")` eine Regex-Prüfung, dass Zeile 1 auf
  `group="…" name="…" subtitle="…" viewport="\d+x\d+"` passt.
- `thumbnail.html` in die Dateiexistenz-/Nicht-leer-Prüfungen der `expected`-Liste
  aufnehmen (automatisch, da über `expectedBundleFiles` bezogen).

## Acceptance criteria
- [ ] Jede `preview/*.html` beginnt mit einem `@dsCard`-Marker, der `group`, `name`,
      `subtitle` und `viewport="880x<Zahl>"` trägt (automatisiert geprüft).
- [ ] `name`/`subtitle` sind in `src/Client/Components/Styleguide/View.fs` direkt bei der
      jeweiligen `data-ds-card`/`data-ds-card-trigger`-Stelle deklariert bzw. für die vier
      generierten Token-/Brand-Karten direkt am `writePreview`-Aufruf im Export-Skript;
      keine zweite, separat gepflegte Namens-/Untertitel-Liste im Export-Skript.
- [ ] Die Viewport-Höhe wird je Karte gemessen (Breite fix 880), nicht hartkodiert.
- [ ] `dist/design-system/thumbnail.html` existiert, ist Teil von `expectedBundleFiles`,
      zeigt die Wortmarke "BudgetBuddy" in Space Grotesk auf `--bg-app` sowie einen
      Streifen in Grün/Teal/Orange/Violett. `[human-eye]` für den visuellen Eindruck des
      Streifens/der Wortmarke.
- [ ] `npm run export:design-system` und `npm run test:scripts`
      (`design-system-w4n9r`-Test) bleiben grün, inklusive der erweiterten Assertions.
- [ ] Punkt 3 (`@kind`-Annotation, Custom-Properties-Ersetzung) ist bewusst NICHT
      umgesetzt (Entscheidung Roman, s. Why); Self-Check-Meldungen dazu werden
      hingenommen, solange der Self-Check insgesamt durchläuft.

## Notes
Captured via `quick-capture` on 2026-09-11, raw, jetzt verfeinert. `design-system-hadjy`
ist `done`. Lernpunkt für das README: der Self-Check läuft nicht nach einem
DesignSync-Upload, sondern erst, wenn im Projekt-Chat eine Anfrage gestellt wird (Roman:
"Bitte den Karten-Index aus den @dsCard-Markern in preview/ neu aufbauen").

**Überschneidung mit `design-system-bz0gn`:** beide Tasks ändern
`src/Client/Components/Styleguide/View.fs` (hier: neue `data-ds-card-name`/
`data-ds-card-subtitle`-Attribute an bestehenden Karten; dort: drei neue Karten/Sektionen)
und `scripts/export-design-system.mjs` (hier: Marker-Format/Viewport/Thumbnail; dort: drei
neue Einträge in `COMPONENT_CARDS` und der Regressionsanker-Liste). Kein `depends_on`
zwischen den beiden, der Conductor serialisiert den Dateikonflikt. Falls `bz0gn` zuerst
landet, gilt das hier beschriebene `name`/`subtitle`-Attribut-Schema auch für die drei
neuen Karten aus `bz0gn`; falls dieser Task zuerst landet, bekommen die drei neuen Karten
aus `bz0gn` beim Anlegen direkt `data-ds-card-name`/`data-ds-card-subtitle` statt nur
`group`.
