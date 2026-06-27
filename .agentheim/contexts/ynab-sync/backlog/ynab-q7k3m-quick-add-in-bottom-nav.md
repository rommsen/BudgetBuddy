---
id: ynab-q7k3m
title: Quick Add aus der Haupt-Navigation erreichbar (Bottom-Nav-Eintrag)
status: backlog
type: feature
context: ynab-sync
created: 2026-06-27
completed:
depends_on: [design-system-001]
blocks: []
tags: [quick-add, navigation, discoverability, ui]
related_adrs: [0003, 0004]
related_research: []
prior_art: [2026-06-11-quick-add-manual-entry, 2026-06-12-quick-add-feedback-round]
---

## Why
Quick Add (manuelle Bar-Buchung → YNAB) ist im Alltag schwer erreichbar. Es gibt keinen
direkten Klickpfad — z. B. direkt nach einem erfolgreichen Import landet Roman ohne
naheliegenden Weg zum Quick-Add-Formular. Die Einstiege aus der Feedback-Runde
(Secondary-Button unter „Sync starten" + Plus-Icon im Review-Header,
`done/2026-06-12-quick-add-feedback-round.md`) sind an den Sync-Flow gebunden und decken
den „ich will eben schnell eine Bar-Ausgabe erfassen"-Fall nicht ab. Quick Add ist aber
eine eigenständige, häufige Aktion und verdient einen festen, kontextfreien Einstieg.

## What
Quick Add als first-class Eintrag in der unteren Haupt-Navigation
(`Client.DesignSystem.Navigation`) verankern, sodass das Quick-Add-Formular von jeder
Seite mit einem Tap erreichbar ist — unabhängig vom Sync-Flow.

## Acceptance criteria
- [ ] Quick Add ist ein eigener Eintrag in der unteren Haupt-Navigation, von jeder Seite
      mit einem Tap erreichbar.
- [ ] Direkt nach erfolgreichem Import existiert ein Klickpfad zu Quick Add (durch die
      Bottom-Nav abgedeckt).
- [ ] Der Eintrag folgt dem Styleguide (Icon/Label/Aktiv-Zustand konsistent mit den
      übrigen Nav-Items).

## Notes
Offen für die Refine-Phase:
- **Nav-Slot:** Die Bottom-Nav hat begrenzte Plätze — neuer Slot oder Umgruppierung mit
  einem bestehenden? Welche Tabs gibt es aktuell? (Quelle: `Client.DesignSystem.Navigation`,
  Verwendung in `src/Client/View.fs`.)
- **Icon-Wahl:** Plus? Bargeld/Wallet? Konsistent mit dem Plus-Icon-Einstieg aus der
  Feedback-Runde.
- **Verhältnis zu den bestehenden Einstiegen:** Secondary-Button + Review-Header-Plus —
  bleiben sie, oder löst der Nav-Eintrag sie (teilweise) ab?
- **Routing:** Eigene Route fürs Quick-Add-Sheet, oder öffnet der Nav-Eintrag das Sheet
  als Overlay über der aktuellen Seite? (`fsharp-routing` / `Feliz.Router`.)
- Setzt UI voraus → hängt am Styleguide-Gate (`design-system-001`, done).
