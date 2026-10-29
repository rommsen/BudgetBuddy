---
id: infrastructure-b85yv
title: tzdata im Produktions-Image installieren
status: doing
type: chore
context: infrastructure
created: 2026-10-28
completed:
depends_on: []
blocks: []
tags: [captured, docker, timezone]
related_adrs: [0068]
related_research: []
prior_art: []
---

## Why
Im Produktions-Image (aspnet:10.0-alpine) fehlt tzdata. Die Bob-Tagesgrenze (ADR 0068) findet Europe/Berlin deshalb nicht über TimeZoneInfo und rechnet über den Rückfall `berlinOffsetByEuRules` in `src/Server/Validation.fs`. Der Rückfall bildet nur die heutigen EU-Regeln nach und verdeckt still, dass die Zonendaten fehlen.

## What
Die Runtime-Stufe des Dockerfiles bekommt tzdata. Der Rückfall `berlinOffsetByEuRules` entfällt: `berlinOffsetAt` nutzt nur noch die echte Zone Europe/Berlin aus TimeZoneInfo. Fehlt die Zone, startet der Server nicht und sagt klar, warum (statt beim ersten Bob-Aufruf zu scheitern oder still falsch zu rechnen). Ein Wächter in `scripts/` hält fest, dass tzdata im Image bleibt.

## Acceptance criteria
- [ ] Die Runtime-Stufe im `Dockerfile` installiert tzdata (`apk add --no-cache curl tzdata`, ein RUN zusammen mit curl).
- [ ] Ein Wächter `scripts/dockerfile-tzdata.test.mjs` liest das `Dockerfile`, findet die Runtime-Stufe (`FROM … AS runtime`) und schlägt fehl, wenn dort kein `apk add` mit `tzdata` steht. Er ist rot, wenn tzdata aus der Runtime-Stufe entfernt wird, und grün mit der Änderung. Läuft mit `npm run test:scripts -- scripts/dockerfile-tzdata.test.mjs`.
- [ ] `berlinOffsetByEuRules` ist aus `src/Server/Validation.fs` entfernt. `berlinOffsetAt` liest den Offset nur aus der Zone Europe/Berlin, `berlinZone` ist kein `option` mehr.
- [ ] Der Server löst Europe/Berlin beim Start einmal auf. Fehlt die Zone, bricht der Start mit einer Meldung ab, die Europe/Berlin und tzdata nennt.
- [ ] Der Vergleichstest Rückfall gegen Zone in `src/Tests/BotValidationTests.fs` (um Zeile 246) entfällt. Die übrigen Tests zu `berlinDayOf` und `berlinDayBoundsUtc` (Winter, Sommer, Umstellungstage 29.03. und 25.10.2026) bleiben grün.
- [ ] Nach `docker build -t budgetbuddy-tz .` liefert `docker run --rm --entrypoint test budgetbuddy-tz -e /usr/share/zoneinfo/Europe/Berlin` Exit-Code 0.
- [ ] `dotnet build` und `dotnet test` sind grün.

## Notes
Captured via `quick-capture` on 2026-10-28, refined am 2026-10-29.

- Entscheidung des Builders (2026-10-29): Rückfall entfernen statt behalten. Lieber ein lauter Startfehler als eine stille Nachbildung der EU-Regeln, die bei einer Regeländerung falsch rechnen würde.
- .NET liest IANA-Zonen auf Linux direkt aus `/usr/share/zoneinfo`. Der Globalisierungs-Invariant-Modus des Alpine-Images (kein ICU) spielt dafür keine Rolle, tzdata allein reicht.
- Aufrufer der Tagesgrenze: `src/Server/BotApi.fs` (`berlinDayBoundsUtc`, `berlinDayOf`). Die Signaturen bleiben gleich.
- Ort für die Startprüfung: dort, wo der Server seine Konfiguration beim Start prüft (`src/Server/Program.fs`), etwa durch erzwungene Auswertung der Zone vor `app.Run`.
- Nach dem Deploy: im Live-Container `/usr/share/zoneinfo/Europe/Berlin` per `docker exec` prüfen und in den Logs sehen, dass der Start ohne Zonenfehler durchlief. Deploy-Prüfweg siehe Memory "Deploy-Hook und Prüfweg".
- Kein Frontend-Anteil, kein Styleguide-Gate.
