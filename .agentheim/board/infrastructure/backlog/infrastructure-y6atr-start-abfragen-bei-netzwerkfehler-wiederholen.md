---
id: infrastructure-y6atr
title: Lesende Start-Abfragen wiederholen sich bei einem Netzwerkfehler (Status 0) ein- bis zweimal, bevor der Fehler erscheint
status: backlog
type: bug
context: infrastructure
created: 2026-10-24
completed:
depends_on: []
blocks: []
tags: [captured, pwa, netzwerk, firefox]
related_adrs: []
related_research: []
prior_art: []
---

## Why
Live-Befund 2026-10-24 (Firefox am PC, nach einem Deploy): Beim Neuladen zeigt das
Kassenbuch öfter "Konten konnten nicht geladen werden" und den Toast "Failed to load
settings" mit "Http error (0)". Der Server hat dieselben Anfragen in etwa 1 ms mit 200
beantwortet, der Tailscale-Proxy meldet dazu "context canceled": Der Browser hat die
Verbindung abgebrochen. Nach etwa drei Versuchen oder mit Cmd+Shift+R (am Service Worker
vorbei) geht es. Ein einmaliger Abbruch lässt die Seite bisher dauerhaft im Fehlerzustand.

## What
Die lesenden Start-Abfragen (`getSettings`, `getLedgerAccounts`, `getLastMirrorRun`,
`getCategories`) versuchen es bei einem Netzwerkfehler (Status 0) nach kurzer Pause ein-
bis zweimal erneut, bevor der Fehler erscheint. Mit Regressionstest.

## Acceptance criteria
- [ ] To be defined during refinement.

## Notes
Captured via `quick-capture` on 2026-10-24: raw, unrefined. Needs a `modeling` refine pass before
it can be promoted. Die eigentliche Ursache der Abbrüche ist offen (Verdacht: Service Worker
in Firefox, eventuell im Zusammenspiel mit dem sofortigen Neuladen aus infrastructure-bnbdc).
Ein Firefox-Test über Playwright gegen die Live-App lief zum Zeitpunkt der Erfassung noch.
Die Wiederholung behebt nur die Folge, nicht die Ursache.
