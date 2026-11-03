---
id: infrastructure-v8m2d
title: "Dev-Server: Vite stürzt ab, wenn dotnet parallel das Client-Projekt baut"
status: backlog
type: bug
context: infrastructure
created: 2026-11-03
completed:
depends_on: []
blocks: []
tags: [dev, vite, fable]
related_adrs: []
related_research: []
prior_art: []
---

## Why
Läuft `npm run dev` und baut daneben `dotnet` das Client-Projekt (`dotnet build src/Client`, `dotnet test`, das über `src/Tests` den Client mitbaut), beendet sich Vite mit `[transform] Received id mismatch! Got 3, expected 5` aus `vite-plugin-fable`. Beobachtet am 2026-11-03 zweimal hintereinander, ein dritter Lauf (`./verify.sh`) ließ Vite stehen. Der Dev-Server muss dann von Hand neu starten.

## What
Ursache klären: vermutlich teilen sich der Fable-Daemon des Plugins und der dotnet-Build `src/Client/obj`. Ziel: Dev-Server und Gate laufen nebeneinander, oder der Absturz ist vermieden (eigenes `obj` für den Plugin-Lauf, Neustart des Plugins nach dem Fehler).

## Acceptance criteria
- [ ] Nachgestellt: Vite läuft, `dotnet build src/Client/Client.fsproj` daneben, Ergebnis notiert (wie oft stürzt es ab).
- [ ] Nach dem Fix übersteht Vite drei solche Builds hintereinander.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.
