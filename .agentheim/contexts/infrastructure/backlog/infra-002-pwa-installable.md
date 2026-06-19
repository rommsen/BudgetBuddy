---
id: infra-002
title: PWA-Mechanik — installierbar (Manifest, Shell-SW, iOS-Meta, vite-plugin-pwa)
status: backlog
type: feature
context: infrastructure
created: 2026-06-19
completed:
commit:
depends_on: [design-system-008, design-system-001]
blocks: []
tags: [frontend, infrastructure, pwa, manifest, service-worker, vite, ios]
related_adrs: []
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
1. **`vite-plugin-pwa`** in den bestehenden Fable/Vite-Build integrieren.
2. **`manifest.webmanifest`**: `name` "BudgetBuddy", `short_name` ("BB"/"BudgetBuddy"),
   `display: standalone`, `start_url`, `scope`, `theme_color` + `background_color`
   (aus `design-system-008`), `icons` (192/512/maskable aus `design-system-008`).
3. **Service Worker** (Workbox via Plugin): **nur die App-Shell** precachen (gebaute
   JS/CSS/HTML-Assets). **`/api/*` (Fable.Remoting) ist network-only und wird NIE gecacht** —
   keinerlei Transaktions-/YNAB-Daten im Cache. Kein Offline-Daten-Verhalten.
4. **iOS-Meta in `index.html`**: `apple-mobile-web-app-capable` (bzw. `mobile-web-app-capable`),
   `apple-mobile-web-app-status-bar-style`, `apple-touch-icon`-Link, `theme-color`-Meta.
   (iOS hat kein `beforeinstallprompt` → "Zum Home-Bildschirm" ist manuell; saubere Meta-Tags
   + `apple-touch-icon` sorgen für korrektes Icon + standalone-Start.)

## Acceptance criteria
- [ ] App ist in **Chrome/Edge (Desktop + Android) installierbar** — Install-Prompt erscheint
      bzw. Lighthouse-PWA "installable" ist grün.
- [ ] **iOS Safari "Zum Home-Bildschirm"**: korrektes `apple-touch-icon`, standalone-Start
      (kein Safari-Chrome), Status-Bar-Style gesetzt.
- [ ] **`manifest.webmanifest` vollständig**: name/short_name/`display: standalone`/start_url/
      scope/theme_color/background_color/icons (192, 512, maskable) — Icons + Farben stammen aus
      `design-system-008`.
- [ ] **Service Worker precached nur die App-Shell**; `/api/*` ist network-only und wird **nie**
      gecacht (verifiziert: keine YNAB-/Transaktions-Response im Cache-Storage). Keine
      Offline-Daten.
- [ ] **Hinter Tailscale-HTTPS getestet**: Install + SW-Registrierung funktionieren im secure
      context (nicht nur `localhost`).
- [ ] `dotnet build` + `vite build` (Fable) grün; bestehende Tests grün; Diary aktualisiert.

## Notes
- **`depends_on`:** `design-system-008` (Icon-Set + `theme_color`/`background_color`) und
  `design-system-001` (Styleguide-Gate — die Marken-/Theme-Optik gehört dem Design-System;
  Frontend-berührender Task fällt unters Gate).
- **Vorbedingung secure context ERFÜLLT:** Roman bestätigt **HTTPS via Tailscale** (2026-06-19).
  Plain-HTTP im Tailnet wäre ein **Blocker** gewesen — Service Worker + Installierbarkeit
  brauchen einen secure context (HTTPS oder `localhost`).
- **"Nur installierbar" ≠ kein SW:** Chrome/Android brauchen i. d. R. einen Service Worker, damit
  die Install-Kriterien greifen; `vite-plugin-pwa` liefert einen Workbox-Shell-Precache-SW quasi
  gratis. Die Entscheidung ist also **nicht** "SW ja/nein", sondern "SW precached nur die Shell,
  fasst `/api` nie an" — exakt der gewählte Scope, **nicht** das abgelehnte Offline-Daten-Szenario.
- **Offene Refine-Fragen:**
  - `registerType: 'autoUpdate'` vs `'prompt'` — wie wird ein neuer Deploy dem laufenden
    PWA-Client zugestellt (stilles Update vs. "Neue Version verfügbar"-Hinweis)?
  - Offline-Fallback-Seite nötig? (Roman wählte explizit "nur installierbar" → eher nein, aber
    eine minimale "kein Netz"-Seite kostet wenig.)
  - iOS-Splash-Matrix (mit `design-system-008` abstimmen — ja/nein).
  - **Tailscale-Reverse-Proxy/Pfad prüfen:** SW-`scope` und Asset-Pfade müssen zum
    Serve-Pfad hinter Tailscale passen (sonst registriert der SW im falschen Scope).
- **Tooling:** `vite-plugin-pwa` (+ `@vite-pwa/assets-generator` für die Icon-Generierung,
  gemeinsam mit `design-system-008`).
