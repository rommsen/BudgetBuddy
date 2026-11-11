---
id: ledger-s1weh
title: "Übersicht als Einstieg (Variante A): Zu tun, Monat mit Balken und Marke heute, am Handy Konten, „+ Buchung“"
status: backlog
type: feature
context: ledger
created: 2026-11-11
completed:
depends_on: [design-system-001-styleguide, design-system-k6kfw]
blocks: []
tags: [etappe-3, frontend, uebersicht]
related_adrs: [0086,0087]
related_research: []
prior_art: []
---

## Why
Der Einstieg ist heute der Sync, weil er zuerst da war (ADR 0086). Die Übersicht beantwortet: wo stehe ich, was steht an.

## What
Neue Route Übersicht als Startseite. Zuerst „Zu tun“ als geordnete Liste mit genau einer Hauptaktion, dann der Monat mit fester Höhe, Balken und Marke „heute“, am Handy danach die Konten. „+ Buchung“ auch hier. Angepinnt kommt in budgeting-w9yt4 dazu.

## Acceptance criteria
- [ ] To be defined during refinement.

## Notes
Schnitt aus Etappe 3 (Fahrplan, Roman 2026-11-11). Übergang (Roman, 2026-11-11): bis Import direkt (banking-import-d0fey) und Durchgehen (ledger-v6s7c) stehen, zeigt „Zu tun“ auf den heutigen Sync-Ablauf. Der Sync ist dann kein Ort mehr, nur ein Ablauf.
Vorlage: Prototyp https://claude.ai/artifact/CE3WJZHhPX6NABosJaviRg (Version 17). Baut der Schnitt eine Seite um, fliegen ihre `[flow]`-, `[tastatur]`-, `[fokus]`- und `[scroll]`-Tests raus und werden gegen die Muster aus dem Styleguide neu geschrieben, je Flow einer.
