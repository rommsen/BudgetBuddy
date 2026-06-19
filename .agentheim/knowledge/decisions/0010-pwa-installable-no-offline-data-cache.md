---
id: 0010
title: PWA installierbar, aber bewusst kein Offline-Daten-Cache — /api immer network-only
scope: infrastructure
status: accepted
date: 2026-06-19
supersedes: []
superseded_by: []
related_tasks:
  - contexts/infrastructure/done/infra-002-pwa-installable.md
related_research: []
---

# ADR 0010: PWA installierbar, aber bewusst kein Offline-Daten-Cache

## Context
BudgetBuddy wird als PWA umgesetzt — installierbar auf Desktop und Mobil, mit
Home-Screen-Icon und eigenem App-Fenster (kein Browser-Chrome). Installierbarkeit
verlangt in Chrome/Android praktisch einen Service Worker, und `vite-plugin-pwa`
(Workbox) liefert einen Shell-Precache-SW quasi gratis mit. Damit drängt sich die
naheliegende PWA-Erwartung auf: **Offline-Fähigkeit durch Caching von Daten.**

Genau das ist für BudgetBuddy **schädlich**. BB ist ein **Live-Daten-Companion**
(Comdirect → YNAB): es zeigt und synchronisiert echte Kontoumsätze und Beträge. Ein
Cache von Finanzdaten brächte:
- **stale Beträge** (gecachte Salden/Transaktionen, die nicht mehr stimmen),
- **Dedup-/ImportId-Verwirrung** (der Sync-Pfad geht von frischem Server-State aus;
  ein zwischengeschalteter Daten-Cache untergräbt die Duplikat-Erkennung),
- einen falschen „funktioniert offline"-Eindruck, obwohl die App ohne Server +
  Tailscale **nichts Nützliches** tun kann.

Das widerspricht der Vision direkt („was nicht real gebraucht wird, fliegt raus";
Companion, kein Offline-Tresor).

## Decision
**PWA = nur installierbar, kein Offline-Daten-Verhalten.**

1. **Service Worker precached ausschließlich die statische App-Shell** (gebaute
   JS/CSS/HTML + Icons/Manifest aus `dist/public`). Workbox `globPatterns` deckt nur
   Shell-Asset-Typen ab.
2. **`/api/*` (Fable.Remoting) ist strikt network-only und wird NIE gecacht.** Es gibt
   **keine** `runtimeCaching`-Route (`runtimeCaching: []`) → keine API-Antwort gelangt
   je in den Cache. Zusätzlich ist `/api/*` per `navigateFallbackDenylist`
   (`[/^\/api\//]`) von der Navigations-Behandlung ausgenommen, damit ein API-Request
   niemals die Shell/Offline-Seite serviert bekommt.
3. **Update-Strategie `registerType: 'autoUpdate'`** (still). Single-User, Roman
   kontrolliert den Deploy → die installierte Shell aktualisiert sich beim nächsten
   Laden/Navigieren ohne „neue Version"-Prompt.
4. **Statische `offline.html`** als einziges Offline-Zugeständnis: eine gebrandete
   „Keine Verbindung — BudgetBuddy braucht Netz/Tailscale"-Seite (DS-Farben/Fonts
   inline, selbsttragend), als Workbox `navigateFallback`. Sie cached **keine Daten** —
   sie ersetzt nur den nackten Browser-Fehler durch eine ehrliche, gebrandete Seite,
   wenn eine Navigation weder aus dem Netz noch aus dem Precache bedient werden kann.

## Consequences
- **Positiv:** App ist installierbar (Lighthouse-„installable"-Kriterien erfüllt:
  Manifest + SW + secure context via Tailscale-HTTPS), ohne je stale Finanzdaten
  anzuzeigen. Der Sync-Pfad sieht immer frischen Server-State. Deploys werden ohne
  manuelles Cache-Leeren übernommen.
- **Neutral/bewusst akzeptiert:** Ohne Server/Tailscale ist die App **nicht** benutzbar
  — sie zeigt dann `offline.html`. Das ist gewollt und ehrlich, kein Mangel.
- **Folge-Verpflichtung:** Wer künftig Caching hinzufügen will, muss diesen ADR
  explizit revidieren. Insbesondere darf **keine** Workbox-`runtimeCaching`-Route auf
  `/api/*` zeigen. Eine *reichere* In-App-Offline-UX (Elmish erkennt fehlgeschlagene
  `/api`-Calls und rendert einen gebrandeten Zustand in der laufenden Shell) wäre
  **Frontend-App-Verhalten**, nicht PWA-Infra, und kein Daten-Cache — sie wäre mit
  diesem ADR vereinbar, ist aber separat zu capturen.

## Alternatives considered
- **Voll-PWA mit Offline-Daten-Cache (StaleWhileRevalidate / NetworkFirst auf `/api`):**
  verworfen — der Kern-Schaden (stale Beträge, Dedup-/ImportId-Verwirrung) tritt genau
  hier auf; widerspricht der Live-Companion-Vision.
- **Gar kein Service Worker (nur Manifest):** verworfen — Chrome/Android verlangen für
  die Install-Kriterien faktisch einen SW; ohne ihn wäre „installierbar" nicht zuverlässig
  erreichbar. Der Shell-Precache-SW kostet nichts und cached bewusst keine Daten.
- **Keine Offline-Seite (nackter Browser-Fehler bei Verbindungsverlust):** verworfen —
  ein File (`offline.html`) kostet minimal und verhindert den hässlichen, verwirrenden
  Browser-Standardfehler, ohne irgendetwas zu cachen.
- **`registerType: 'prompt'` mit Toast-Update:** verworfen — für ein deploy-kontrolliertes
  Single-User-Tool ist stilles Auto-Update angenehmer; ein Versions-Prompt wäre Reibung
  ohne Mehrwert.
