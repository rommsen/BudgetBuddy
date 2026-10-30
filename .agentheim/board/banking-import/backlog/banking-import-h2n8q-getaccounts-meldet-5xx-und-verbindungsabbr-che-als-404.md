---
id: banking-import-h2n8q
title: "getAccounts meldet 5xx und Verbindungsabbrüche als 404"
status: backlog
type: bug
context: banking-import
created: 2026-10-30
completed:
depends_on: []
blocks: []
tags: [backend, comdirect]
related_adrs: [0071]
related_research: []
prior_art: [banking-import-cx4b7]
---

## Why
`getAccountsWith` probiert bei jedem Status außer 401 und 403 den nächsten Pfad und endet mit `NetworkError (404, ...)`. Ein 503 oder ein Abbruch sieht dadurch wie ein fehlender Endpunkt aus. Für das Urteil "Später nochmal" aus ADR 0071 Punkt 3 muss der echte Status sichtbar sein.

## What
Den letzten Fehler, der kein 404 ist, zurückgeben, statt ihn durch 404 zu ersetzen.

## Acceptance criteria
- [ ] Kaputt: GIVEN alle Pfade antworten 503 WHEN `getAccountsWith` THEN der Fehler trägt Status 503 (Test vor dem Fix rot).
- [ ] Intakt: GIVEN alle Pfade antworten 404 THEN bleibt es bei `NetworkError (404, ...)`, und alle drei Pfade wurden probiert.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.