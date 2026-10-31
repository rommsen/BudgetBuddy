---
id: infrastructure-p9kd4
title: "Kassenbuch: Nachladen mit eingefrorener Query kann eine Zeile überspringen, wenn währenddessen eine Zeile aus der Liste fällt"
status: backlog
type: bug
context: infrastructure
created: 2026-10-31
completed:
depends_on: [infrastructure-r3tw7]
blocks: []
tags: [ledger, retry]
related_adrs: []
related_research: []
prior_art: [infrastructure-r3tw7]
---

## Why
Aus der Verifikation von infrastructure-r3tw7, nur aus dem Quelltext abgeleitet, nicht nachgefahren. Die Query einer Nachlade-Seite ist zum Start eingefroren, auch `LocallyRemoved`. Fällt während einer Wiederholung eine Zeile aus der Liste (Chip "Zu prüfen"), passt der Versatz nicht mehr, und bis zum Neuladen kann eine Zeile fehlen. Das Fenster gab es schon vorher, die Wiederholung verlängert es auf bis zu 1,6 s.

## What
Nachstellen. Wenn es sich bestätigt, den Versatz beim Antworten abgleichen.

## Acceptance criteria
- [ ] Kaputt: GIVEN eine Nachlade-Wiederholung läuft und eine Zeile fällt aus der Liste WHEN die Antwort kommt THEN fehlt keine Zeile.
- [ ] Intakt: GIVEN normales Nachladen THEN bleibt die Reihenfolge lückenlos.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.
