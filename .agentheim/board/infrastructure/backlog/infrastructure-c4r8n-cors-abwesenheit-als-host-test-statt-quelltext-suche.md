---
id: infrastructure-c4r8n
title: "CORS-Abwesenheit als Host-Test statt Quelltext-Suche"
status: backlog
type: refactor
context: infrastructure
created: 2026-10-29
depends_on: []
blocks: []
tags: [harness, security]
related_adrs: []
related_research: []
prior_art: [infrastructure-hdf7j]
---

## Why
`src/Tests/CorsTests.fs` sucht Zeichenketten wie `Cors` in den Server-Quellen (Audit B1 N8, C B10). Eine Policy über Konfiguration oder ein eigenes Middleware-Lambda, das `Access-Control-Allow-Origin` setzt, bliebe unentdeckt.

## What
Die Middleware-Kette aus `src/Server/Program.fs` in eine Funktion ziehen (etwa `configureApp`) und in einem TestServer prüfen, dass ein Preflight `OPTIONS` mit `Origin` keinen `Access-Control-Allow-Origin` liefert. Danach die Quelltext-Suche löschen oder als Zusatz behalten.

## Acceptance criteria
- [ ] Ein Test schickt ein Preflight mit `Origin` durch die echte Middleware-Kette und erwartet keinen CORS-Header.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.