---
id: design-system-008
title: PWA-App-Icon (neon-on-dark) — "B" im Sync-Ring, Signatur-Gradient
status: todo
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
- [ ] **Master-SVG** (`icon-master.svg`, Client-Asset-Pfad): "B" im Sync-Ring, Signatur-Gradient
      (teal→green→orange, 135deg) + Glow, auf transparentem Grund. Vektorsauber, optisch stimmig
      mit dem neon-on-dark-Styleguide.
- [ ] **Vereinfachte Favicon-SVG** (`favicon.svg`): solid `#00ff88`, nur das "B", ohne Ring/Gradient
      — liest bei 16/32px klar.
- [ ] **Icon-Set generiert** und im Build verfügbar:
      - `192×192` + `512×512` (voller Hybrid, Gradient; transparent oder #08081a),
      - **maskable** `512×512` (voller Hybrid auf **opakem `#08081a`**, **≥20 % Safe-Zone-Padding**),
      - `apple-touch-icon` `180×180` (voller Hybrid auf **opakem `#08081a`** — iOS ignoriert Transparenz),
      - Favicon (`favicon.svg` + `.ico`/32/16 aus der **vereinfachten** Variante).
- [ ] **`theme_color` und `background_color` dokumentiert = `#08081a`** (für `infra-002` zur Übernahme
      in Manifest + `index.html`).
- [ ] **Gate-Review:** Roman hat den gerenderten Mark abgenommen — klein (16/32px Favicon), im
      **maskable-Squircle/Kreis** (nichts Wichtiges in der Beschnitt-Zone), und auf hellem **und**
      dunklem Hintergrund.

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

## Refine-Log
**2026-06-19 (Refine + Promote):** Suggestor-Runde — vier Mark-Konzepte (Monogramm / Sync-Loop /
B-im-Ring-Hybrid / Münze+Flow) × drei Farb-Behandlungen vorgelegt. Roman wählte **B-im-Sync-Ring**
+ **Signatur-Gradient**. Klein-Vereinfachung (solid-green B ohne Ring ≤32px) gegen das
Hybrid-16px-Dichte-Risiko ergänzt; Hintergrund/Theme auf `#08081a` festgelegt (korrigiert das
Slate-`#0f172a`-Drift via `infra-002`); maskable-/apple-touch-Opazitäts-Regeln + ≥20 % Safe-Zone
in die AC gezogen; iOS-Splash bewusst ausgeklammert. Damit ist die eine offene Frage ("wie sieht der
Mark aus") gelöst → **todo**. Kein Orchestrator nötig (visuelle Geschmacks-/Marken-Entscheidung,
keine Domänen-/Architektur-Frage); kein ADR (keine Architekturentscheidung).
