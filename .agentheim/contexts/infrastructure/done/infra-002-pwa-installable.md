---
id: infra-002
title: PWA-Mechanik — installierbar (Manifest, Shell-SW, iOS-Meta, vite-plugin-pwa)
status: done
type: feature
context: infrastructure
created: 2026-06-19
completed: 2026-06-19
depends_on: [design-system-008, design-system-001]
blocks: []
tags: [frontend, infrastructure, pwa, manifest, service-worker, vite, ios]
related_adrs: [0010]
related_research: []
prior_art: []
---

## Why
Roman will BudgetBuddy **als PWA umsetzen** — installierbar auf PC und mobil, mit
Home-Screen-Icon und eigenem App-Fenster (kein Browser-Chrome). Das ist eine
globally-true Frontend-Infra-Eigenschaft des gebauten Clients (Vite/Fable-Build +
Deployment), kein fachlicher Belang eines einzelnen BCs → `infrastructure`.

**Scope (Roman 2026-06-19): nur installierbar, KEIN Daten-Caching.** BB ist ein
Live-Daten-Companion (Comdirect → YNAB); gecachte Finanzdaten/Transaktionen wären
gegen die Vision aktiv schädlich (stale Beträge, Dedup-/ImportId-Verwirrung). Die App
braucht weiterhin Server + Tailscale, um irgendetwas Nützliches zu tun.

## What
1. **`vite-plugin-pwa`** in den bestehenden Fable/Vite-Build integrieren
   (`vite.config.js`, `root: ./src/Client`, `build.outDir: ../../dist/public`).
2. **`manifest.webmanifest`**: `name` "BudgetBuddy", `short_name` "BB",
   `display: standalone`, `start_url: /`, `scope: /`, `theme_color: #08081a` +
   `background_color: #08081a` (aus `design-system-008`), `icons` (192/512 `any` +
   maskable-512 `purpose: maskable` aus `design-system-008`).
3. **Service Worker** (Workbox via Plugin, `registerType: 'autoUpdate'`): **nur die
   App-Shell** precachen (gebaute JS/CSS/HTML-Assets aus `dist/public`).
   **`/api/*` (Fable.Remoting) ist network-only und wird NIE gecacht** — keinerlei
   Transaktions-/YNAB-Daten im Cache. Kein Offline-Daten-Verhalten.
4. **iOS-Meta in `index.html`**: `mobile-web-app-capable`/`apple-mobile-web-app-capable`,
   `apple-mobile-web-app-status-bar-style`, `apple-touch-icon`-Link (→
   `icons/apple-touch-icon.png`), und `theme-color`-Meta auf **`#08081a`** korrigieren
   (ersetzt das aktuell hardcodierte generische `#0f172a`). Favicon-Links auf das
   generierte Set zeigen.
   (iOS hat kein `beforeinstallprompt` → "Zum Home-Bildschirm" ist manuell; saubere
   Meta-Tags + `apple-touch-icon` sorgen für korrektes Icon + standalone-Start.)
5. **Minimale Offline-Seite** (`offline.html`, precached): gebrandete "Keine Verbindung
   — BudgetBuddy braucht Netz/Tailscale"-Seite in den DS-Farben, als Workbox
   `navigateFallback`/Floor für Navigationen, die weder aus Netz noch Precache bedient
   werden können. **Kein Daten-Caching** — nur ein statisches HTML statt eines nackten
   Browser-Fehlers.

## Acceptance criteria
- [ ] App ist in **Chrome/Edge (Desktop + Android) installierbar** — Install-Prompt erscheint
      bzw. Lighthouse-PWA "installable" ist grün.
- [ ] **iOS Safari "Zum Home-Bildschirm"**: korrektes `apple-touch-icon`, standalone-Start
      (kein Safari-Chrome), Status-Bar-Style gesetzt.
- [ ] **`manifest.webmanifest` vollständig**: name/short_name/`display: standalone`/
      `start_url: /`/`scope: /`/theme_color/background_color/icons (192, 512, maskable) —
      Icons + Farben stammen aus `design-system-008` (`#08081a`).
- [ ] **Update-Strategie `autoUpdate`**: nach einem Deploy aktualisiert sich die installierte
      Shell still beim nächsten Laden/Navigieren (kein "neue Version"-Prompt). Verifiziert:
      neuer Build wird ohne manuelles Cache-Leeren übernommen.
- [ ] **Service Worker precached nur die App-Shell**; `/api/*` ist network-only und wird **nie**
      gecacht (verifiziert: keine YNAB-/Transaktions-Response im Cache-Storage). Keine
      Offline-Daten.
- [ ] **Minimale Offline-Seite**: bei fehlendem Netz/Tailscale erscheint die gebrandete
      `offline.html` (DS-Farben) statt eines nackten Browser-Fehlers; sie cached keine Daten.
- [ ] **`theme-color`/`apple-touch-icon`/Favicons in `index.html` korrigiert** auf das
      `design-system-008`-Set bzw. `#08081a` (kein verbliebenes `#0f172a`).
- [ ] **Hinter Tailscale-HTTPS getestet**: Install + SW-Registrierung funktionieren im secure
      context (nicht nur `localhost`), SW registriert im Scope `/`.
- [ ] `dotnet build` + `vite build` (Fable) grün; bestehende Tests grün; Diary aktualisiert.

## Notes
- **`depends_on` (beide DONE 2026-06-19):** `design-system-008` (Icon-Set + `theme_color`/
  `background_color` `#08081a`, 2-Quellen-Icon-Mapping) und `design-system-001`
  (Styleguide-Gate — Frontend-berührender Task fällt unters Gate). → Task ist **entsperrt**.
- **Vorbedingung secure context ERFÜLLT:** Roman bestätigt **HTTPS via Tailscale** (2026-06-19).
  Plain-HTTP im Tailnet wäre ein **Blocker** gewesen — Service Worker + Installierbarkeit
  brauchen einen secure context (HTTPS oder `localhost`).
- **"Nur installierbar" ≠ kein SW:** Chrome/Android brauchen i. d. R. einen Service Worker, damit
  die Install-Kriterien greifen; `vite-plugin-pwa` liefert einen Workbox-Shell-Precache-SW quasi
  gratis. Die Entscheidung ist also **nicht** "SW ja/nein", sondern "SW precached nur die Shell,
  fasst `/api` nie an" — exakt der gewählte Scope.

- **Refine-Entscheidungen (Roman, 2026-06-19) — alle offenen Fragen geschlossen:**
  - **Update-Zustellung → `registerType: 'autoUpdate'`** (still). Begründung: Single-User,
    Roman kontrolliert den Deploy; stille Aktualisierung beim nächsten Öffnen ist ideal, kein
    Prompt nötig. (Die Toast-basierte `'prompt'`-Variante wurde bewusst verworfen.)
  - **Offline-Fallback → minimale `offline.html`** (statt "keine"). Gebrandete "Keine
    Verbindung"-Seite, ein File, kostet wenig, verhindert den hässlichen Browser-Fehler wenn
    Tailscale droppt. **Kein** Daten-Caching.
  - **iOS-Splash-Matrix → übersprungen.** Keine `apple-touch-startup-image`-Matrix; ~15
    Asset-Dateien + Media-Query-Meta für ~1s Kosmetik lohnen sich für ein Single-User-Tool
    nicht ("was nicht real gebraucht wird, fliegt raus"). iOS zeigt kurz einen themed Screen.
    Revidierbar, falls es real stört.
  - **Tailscale-Scope/Pfad → durch Config gelöst (kein Blocker):** `compose.yaml` macht
    `tailscale serve --https=443 http://127.0.0.1:5081` → App wird an der **Root** (`/`) des
    Tailnet-Hosts ausgeliefert. Daher SW-`scope: /` und **kein** Vite-`base`-Prefix nötig.
    Giraffe serviert `dist/public` statisch an `/`, `/api/*` ist Fable.Remoting → "Shell
    precachen, `/api` nie anfassen" passt 1:1 auf die Serve-Topologie. Worker muss nur sicher-
    stellen, dass `sw.js` + `manifest.webmanifest` vom Giraffe-Static-Handler mit korrektem
    MIME an der Root ausgeliefert werden.

- **Tooling:** `vite-plugin-pwa` (Workbox-SW + Manifest-Injection). Icon-Raster liegen bereits
  unter `src/Client/public/icons/` (aus `design-system-008`) — `@vite-pwa/assets-generator` ist
  **optional** zum Neu-Generieren; das vorhandene Set + die zwei Farbwerte sind der Vertrag, die
  Rasterdateien sind ableitbar (siehe `src/Client/public/icons/README.md`).

- **Out of scope (bewusst, ggf. separater Capture):** Eine *reichere* In-App-Offline-UX
  (Elmish erkennt fehlgeschlagene `/api`-Calls und rendert einen gebrandeten "offline"-Zustand
  im laufenden Shell) ist **Frontend-App-Verhalten**, nicht PWA-Infra — gehört nicht in
  infra-002. Hier nur die statische `offline.html` als Navigations-Floor.

- **ADR-Kandidat (beim Work festzuhalten):** Die Haltung "PWA installierbar, aber **bewusst kein
  Offline-Daten-Cache**; `/api` immer network-only" ist eine vision-getriebene Architektur-
  Entscheidung (stale Finanzdaten wären schädlich). Beim Umsetzen einen kurzen ADR schreiben und
  hier in `related_adrs` zurückverlinken.
  → **Erledigt: ADR 0010** (`.agentheim/knowledge/decisions/0010-pwa-installable-no-offline-data-cache.md`),
  in `related_adrs` verlinkt.

## Outcome
**PWA-Mechanik umgesetzt: BudgetBuddy ist installierbar (Manifest + Workbox-Shell-SW),
bewusst ohne Offline-Daten-Cache; `/api` bleibt strikt network-only.**

### Was gebaut wurde
- **`vite-plugin-pwa` (v1.3.0)** in den Fable/Vite-Build integriert (`vite.config.js`,
  repo-root). `registerType: 'autoUpdate'` (stilles Update), `injectRegister: 'script'`
  (Plugin injiziert die SW-Registrierung als `<script>` in die gebaute `index.html` —
  der Fable-Entry `App.fs` bleibt unangetastet), `scope: '/'` (kein Vite-`base`).
- **`manifest.webmanifest`** (generiert): `name` "BudgetBuddy", `short_name` "BB",
  `display: standalone`, `start_url: /`, `scope: /`, `theme_color` + `background_color`
  `#08081a`, Icons 192/512 (`any`) + maskable-512 (`maskable`) aus `design-system-008`.
- **Service Worker** (`dist/public/sw.js`, generateSW): precached **nur die Shell**
  (24 Einträge — App-JS/CSS, `index.html`, alle Icons/Favicons, `manifest.webmanifest`,
  `registerSW.js`, `offline.html`). **`runtimeCaching: []`** → keine API-Antwort gelangt
  je in den Cache. `/api/*` via `navigateFallbackDenylist: [/^\/api\//]` von der
  Navigations-Behandlung ausgenommen → network-only.
- **`offline.html`** (`src/Client/public/`): selbsttragende, gebrandete "Keine
  Verbindung"-Seite (DS-Farben/Fonts inline, `#08081a`/Gradient teal→green→orange), als
  Workbox `navigateFallback`. Cached keine Daten.
- **`index.html`**: `theme-color` auf `#08081a` korrigiert (altes generisches `#0f172a`
  entfernt), `apple-touch-icon` + Favicon-Set (svg/32/16/ico), `mobile-web-app-capable` +
  `apple-mobile-web-app-*`-Meta. Manifest-Link wird vom Plugin injiziert.
- **Giraffe-Static-Handler** (`src/Server/Program.fs`): `FileExtensionContentTypeProvider`
  mit `.webmanifest → application/manifest+json` erweitert, damit der Manifest am Root mit
  korrektem MIME ausgeliefert wird (ASP.NET-Default kennt `.webmanifest` nicht).
- **ADR 0010** geschrieben: "installierbar, aber kein Offline-Daten-Cache; /api network-only".

### Verifiziert (automatisch, hier ausführbar)
- `npm run build` (Fable + Vite) grün → Manifest + `sw.js` + `workbox-*.js` + `registerSW.js`
  generiert; Precache = nur Shell (per `sw.js`-Inspektion bestätigt: **kein** `/api`,
  keine Daten-URLs; `offline.html` precached + als `NavigationRoute`-Fallback mit
  `/^\/api\//`-Denylist verdrahtet).
- Generiertes `manifest.webmanifest` vollständig + korrekt (alle Pflichtfelder, Farben
  `#08081a`, Icons 192/512/maskable).
- Generiertes `index.html`: Manifest-Link + Registrierungs-Script (`/sw.js`, scope `/`),
  `theme-color #08081a` (kein `#0f172a` mehr), apple-touch-icon + Favicons.
- `dotnet build` (Server) grün; `dotnet test` grün (595 passed, 6 skipped = externe
  `.env`-Integrationstests, unverändert).

### Human gate (nicht von hier verifizierbar — Roman bestätigt am Gerät)
Diese ACs erfordern echte Geräte/Netz und können in dieser Umgebung nicht abgenommen werden:
- **Chrome/Edge Install-Prompt** (Desktop/Android) bzw. Lighthouse-"installable" grün.
- **iOS Safari "Zum Home-Bildschirm"**: korrektes Icon, standalone-Start, Status-Bar-Style.
- **Update-Strategie `autoUpdate`** end-to-end (neuer Build wird ohne Cache-Leeren
  übernommen) — Config ist gesetzt; das Live-Verhalten zeigt sich erst nach zwei Deploys.
- **Hinter Tailscale-HTTPS**: Install + SW-Registrierung im secure context (nicht nur
  `localhost`). Voraussetzung (HTTPS via Tailscale) ist von Roman bestätigt; die konkrete
  SW-Registrierung am Tailnet-Host muss er einmal verifizieren.
- **Offline-Seite live**: `offline.html` erscheint bei gedropptem Netz/Tailscale.

Die Build-/Config-Seite ist vollständig; alle gerätegebundenen ACs sind sauber als
Human-Gate ausgewiesen (kein Claim von nicht-verifizierbaren ACs).

### Schlüsseldateien
- `vite.config.js` (PWA-Plugin-Config)
- `src/Client/index.html` (Meta/Icons/theme-color)
- `src/Client/public/offline.html` (Offline-Floor)
- `src/Server/Program.fs` (`.webmanifest`-MIME)
- `.agentheim/knowledge/decisions/0010-pwa-installable-no-offline-data-cache.md`
- `package.json` / `package-lock.json` (vite-plugin-pwa dependency)

## Verifier note (iteration 1)

**VERDICT: FAIL** — `ITERATION_HINT: likely-fixable`. One genuine cross-reference defect; everything else verified clean.

**REASONS:**
- `vite.config.js:20` — code comment reads "See ADR 0009" but the governing ADR for the
  no-offline-data-cache / `/api`-network-only decision is **0010**. ADR 0009 is the unrelated
  design-system ADR (onNeon foreground / micro-font tokens). The comment block at
  `vite.config.js:15-20` describes ADR 0010's decision verbatim, so the pointer actively
  misdirects a future maintainer. (The BC README at `README.md:37` and the ADR's own
  frontmatter correctly reference 0010 — only this one source comment is wrong.)

**SUGGESTED_FIX:** Change the trailing comment in `vite.config.js` from "See ADR 0009." to
"See ADR 0010." Nothing else needs to move — implementation, manifest, sw.js (`/api` denylist
+ empty runtimeCaching), README, and ADR 0010 are all correct and both builds
(`npm run build`, `dotnet build`) are green.

**Verifier's clean-aspects note:** generated `manifest.webmanifest` complete
(name/short_name/display:standalone/start_url:/scope:/theme_color #08081a/background_color
#08081a/icons 192+512 any + maskable-512); `dist/public/sw.js` precaches only the 24-entry
static shell, no `/api`/data URLs, navigation denylist `[/^\/api\//]`; `index.html` carries
`#08081a` (no remaining `#0f172a` outside explanatory comments), apple-touch-icon + full
favicon set; referenced icon rasters all exist; device-bound ACs honestly documented as a
human gate in `## Outcome`.
