---
id: design-system-008
title: PWA-App-Icon (neon-on-dark) — "B" im Sync-Ring, Signatur-Gradient
status: done
type: feature
context: design-system
created: 2026-06-19
completed: 2026-06-19
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
Styleguide-Gate (`design-system-001`). Bisher hat BB **kein** eigenes Home-Screen-/App-Icon
(kein favicon, kein `public/`, Titel nur "BudgetBuddy") — ein installiertes BB läge mit dem
Browser-Default auf dem Homescreen. Das unterläuft das Vision-Ziel "fühlt sich angenehmer an
als YNAB-Web".

## What
Eine **Master-SVG-Mark** im neon-on-dark-Stil entwerfen und daraus das volle PWA-Icon-Set
ableiten, plus die `theme_color`/`background_color`-Werte festlegen, damit `infra-002` sie
im Manifest und in den iOS-Meta-Tags nur noch referenzieren muss.

## Design-Entscheid (Refine 2026-06-19, Suggestor-Runde mit Roman)
- **Mark-Konzept:** **"B" im Sync-Ring (Hybrid)** — ein fettes "B" zentriert in einem
  Sync-Ring (zwei Pfeilköpfe, die einen Kreislauf bilden; echot das vorhandene
  `Icons.sync`-Motiv). Identität ("B" = BudgetBuddy) **plus** Funktion (Sync = Bank→YNAB,
  der Kern des Tools) in einem Zeichen.
- **Farbe:** **Signatur-Gradient** `linear-gradient(135deg, #00d4aa 0%, #00ff88 50%, #ff6b2c 100%)`
  (teal→green→orange) — exakt der `gradientText`-Flourish der App (`styles.css:257` /
  `Presets.gradientText`). Mit dezentem Neon-Glow.
- **Klein-Vereinfachung (gegen das Hybrid-16px-Risiko):** ≤32px (Favicon) nutzt eine
  **vereinfachte** Variante — **solid Neon-Green `#00ff88`**, nur das "B", **ohne Ring**,
  ohne Gradient. → Zwei Quell-SVGs (s. AC).
- **Hintergrund/Theme:** Grund ist `--bg-app: #08081a` (echte App-Surface). `theme_color`
  **und** `background_color` = **`#08081a`**. (Das aktuelle `theme-color: #0f172a` in
  `index.html` ist generisches Slate = Drift; Korrektur gehört zu `infra-002`.)
- **Letterform-Referenz:** Display-Font ist real **Space Grotesk** (`--font-display`; der
  Styleguide nennt fälschlich Orbitron/Outfit — Code gewinnt). Das "B" orientiert sich an
  Space Grotesk Bold oder wird als geometrisches Custom-B im selben Geist gezeichnet.
- **iOS-Splash:** **bewusst out of scope** für diesen Task — die Per-Device-Splash-Matrix ist
  viel Aufwand für marginalen Nutzen bei einem Single-User-Tool. Bei Bedarf später eigener Task.

## Acceptance criteria
- [x] **Master-SVG** (`icon-master.svg`, Client-Asset-Pfad): "B" im Sync-Ring, Signatur-Gradient
      (teal→green→orange, 135deg) + Glow, auf transparentem Grund. Vektorsauber, optisch stimmig
      mit dem neon-on-dark-Styleguide. → `src/Client/public/icon-master.svg`
- [x] **Vereinfachte Favicon-SVG** (`favicon.svg`): solid `#00ff88`, nur das "B", ohne Ring/Gradient
      — liest bei 16/32px klar. → `src/Client/public/favicon.svg`
- [x] **Icon-Set generiert** und im Build verfügbar (`src/Client/public/icons/`):
      - `192×192` + `512×512` (voller Hybrid, transparent) → `icon-192.png`, `icon-512.png`
      - **maskable** `512×512` (voller Hybrid auf **opakem `#08081a`**, ~24 % Safe-Zone-Padding pro Kante;
        eigene Quelle `icon-maskable.svg`, 76 % skaliert) → `maskable-512.png`
      - `apple-touch-icon` `180×180` (voller Hybrid auf **opakem `#08081a`**, RGB ohne Alpha) → `apple-touch-icon.png`
      - Favicon aus der **vereinfachten** Variante → `favicon.svg` + `favicon-16.png` + `favicon-32.png` + `favicon.ico` (16+32)
- [x] **`theme_color` und `background_color` dokumentiert = `#08081a`** (für `infra-002` zur Übernahme
      in Manifest + `index.html`). → BC-README "App-Mark / Branding" + `src/Client/public/icons/README.md`
- [ ] **Gate-Review:** Roman hat den gerenderten Mark abgenommen — klein (16/32px Favicon), im
      **maskable-Squircle/Kreis** (nichts Wichtiges in der Beschnitt-Zone), und auf hellem **und**
      dunklem Hintergrund. *(HUMAN GATE — pending Romans Abnahme; analog design-system-001/003.)*

## Notes
- **Styleguide-Gate:** `depends_on: [design-system-001]` ✓ (done+reviewt) — der App-Mark ist
  Marken-Identität und wird gegen das Gate gemessen.
- **Farb-/Token-Herkunft (autoritativ = Code):** Grund `--bg-app #08081a`, Gradient
  `#00d4aa→#00ff88→#ff6b2c` (`styles.css:9-11,66,257`). Nicht neu erfinden.
- **Zwei-Quell-SVG-Koordination mit `infra-002`:** `@vite-pwa/assets-generator` skaliert **eine**
  Quelle uniform (simplifiziert nicht). Darum zwei Quellen: `icon-master.svg` → 192/512/maskable/
  apple-touch; `favicon.svg` (vereinfacht) → Favicon. Der Generator-Config in `infra-002` zeigt den
  Favicon auf die vereinfachte Quelle.
- **Ownership-Schnitt:** dieser Task *entscheidet/liefert* Mark + Farbwerte; `infra-002` *verdrahtet*
  sie (Manifest, `index.html`-Meta inkl. der `#0f172a→#08081a`-Korrektur, Generator-Config).
- **Out of scope:** iOS-Splash-Matrix (s. o.).

## Outcome
**2026-06-19:** App-Mark "B im Sync-Ring" geliefert und volles PWA-Icon-Set abgeleitet.
Buildbare Kriterien AC1–AC4 erfüllt.

- **Quell-SVGs (`src/Client/public/`):** `icon-master.svg` (voller Hybrid: B im Sync-Ring,
  Signatur-Gradient `135deg #00d4aa→#00ff88→#ff6b2c` + Neon-Glow, transparent), `favicon.svg`
  (vereinfacht: solid `#00ff88` B, kein Ring/Gradient), `icon-maskable.svg` (Hybrid auf opakem
  `#08081a`, 76 % skaliert → ≥20 % Safe-Zone). Drei Quellen, weil PWA-Generatoren eine Quelle
  uniform skalieren — `infra-002` mappt Favicon→`favicon.svg`, maskable→`icon-maskable.svg`,
  Rest→`icon-master.svg`.
- **Raster-Set (`src/Client/public/icons/`):** `icon-192.png`, `icon-512.png` (transparent),
  `maskable-512.png` (opak), `apple-touch-icon.png` 180 (opak, RGB), `favicon-16/32.png`,
  `favicon.ico` (16+32). Gerendert mit `npx sharp-cli` + `png-to-ico` (kein nativer Rasterizer
  vorhanden); kein permanenter Dependency in package.json (Tooling-Wiring ist `infra-002`).
- **Theme-/Hintergrund-Farbe für `infra-002`:** `theme_color` = `background_color` = **`#08081a`**
  (löst das generische `#0f172a`-Slate in `index.html` ab). Dokumentiert in BC-README und in
  `src/Client/public/icons/README.md` (Asset-/Farb-Kontrakt + Handoff-Pointer).
- **Asset-Ort:** `src/Client/public/` = Vite-publicDir (vite `root` = `src/Client`) → wird unter
  Web-Root ausgeliefert und nach `dist/public` kopiert. `index.html`/Manifest-Verdrahtung bewusst
  NICHT angefasst (gehört `infra-002`).
- **Build:** n/a — reine Vite-statische Assets, nicht vom dotnet-/Fable-Build konsumiert.
- **AC5 (Gate-Review) offen:** Romans visuelle Abnahme ist ein Human-Gate und steht aus
  (Checkbox bewusst nicht gesetzt; gleiches Muster wie design-system-001/003).

## Refine-Log
**2026-06-19 (Refine + Promote):** Suggestor-Runde — vier Mark-Konzepte (Monogramm / Sync-Loop /
B-im-Ring-Hybrid / Münze+Flow) × drei Farb-Behandlungen vorgelegt. Roman wählte **B-im-Sync-Ring**
+ **Signatur-Gradient**. Klein-Vereinfachung (solid-green B ohne Ring ≤32px) gegen das
Hybrid-16px-Dichte-Risiko ergänzt; Hintergrund/Theme auf `#08081a` festgelegt (korrigiert das
Slate-`#0f172a`-Drift via `infra-002`); maskable-/apple-touch-Opazitäts-Regeln + ≥20 % Safe-Zone
in die AC gezogen; iOS-Splash bewusst ausgeklammert. Damit ist die eine offene Frage ("wie sieht der
Mark aus") gelöst → **todo**. Kein Orchestrator nötig (visuelle Geschmacks-/Marken-Entscheidung,
keine Domänen-/Architektur-Frage); kein ADR (keine Architekturentscheidung).
