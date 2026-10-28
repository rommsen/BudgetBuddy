---
id: ledger-dtrn2
title: "isBotPath vergleicht /bot exakt und mit Groß-/Kleinschreibung, Caddy matcht anders: Pfadabgleich in BB robust machen (Security-Review N1, niedrig)"
status: done
type: chore
context: ledger
created: 2026-10-28
completed:
depends_on: [ledger-bbkgs]
blocks: []
tags: [security, bob]
related_adrs: [0066, 0068]
related_research: []
prior_art: []
---

## Why
BB und der Proxy sollen dieselben Pfade als Bot-Pfade ansehen. Caddy matcht ohne Groß-/Kleinschreibung und dekodiert `%2f`; BB tut das nur teilweise (Security-Review N1, niedrig).

## What
`BotApi.isBotPath` erkennt heute `/bot/…` schon ohne Groß-/Kleinschreibung, aber das nackte `/bot` nur exakt, und `/bot%2f…` gar nicht. Solche Pfade gehen heute weiter an Fable.Remoting bzw. den SPA-Fallback und liefern `index.html`, keine Daten; das Risiko ist niedrig. Die Homelab-Seite stellt Caddy auf `path_regexp ^/bot(/|$)` um.

In BB: `/bot`, `/BOT`, `/Bot/…` und Pfade, die mit `/bot%2f` bzw. `/bot%2F` beginnen (ohne Groß-/Kleinschreibung), landen alle im Bot-Handler. Dort gilt die übliche Reihenfolge: ohne gültiges Token 401, mit Token und unbekanntem Pfad 404 JSON. Bekannte Routen bleiben exakt wie heute.

## Acceptance criteria
- [ ] `/BOT`, `/Bot/categories`, `/bot%2fcategories` und `/BOT%2Fcategories` ohne Token antworten 401 JSON, nicht `index.html` (Test über den Giraffe-Handler).
- [ ] Dieselben Pfade mit gültigem Token: `/Bot/categories` liefert die Kategorien wie `/bot/categories` oder 404 JSON (eine der beiden Varianten festlegen und testen); die kodierten Varianten antworten 404 JSON.
- [ ] `/botanik` und `/api/...` gehen unverändert weiter (bestehender Test grün).
- [ ] `docs/bot-api.md` nennt die Pfadregel kurz.
- [ ] `dotnet build`, `dotnet test` und `npm run test:scripts` grün.

## Notes
Herkunft: Security-Review der Homelab-Sitzung (homelab-58) am 2026-10-28, erfasst per quick-capture, verfeinert mit Roman am 2026-10-28. Befund N1, letzter in der Reihenfolge, niedrige Priorität. ADR 0068 (Bezug: Pfadabgleich unter `/bot`).
Hängt an ledger-bbkgs, weil beide `BotApi.fs` ändern.

## Outcome

`BotApi.isBotPath` (src/Server/BotApi.fs) erkennt `/bot` (ohne Groß-/Kleinschreibung), `/bot/…` und `/bot%2f…` (ohne Groß-/Kleinschreibung). Diese Pfade durchlaufen wie bisher Drossel, Authentifizierung (401) und Routing (404 JSON). `/botanik` und `/api/…` werden weitergereicht.

Entscheidung zu Kriterium 2: `/Bot/categories` mit gültigem Token antwortet 404 JSON, liefert also nicht die Kategorien. Grund: Die Routen bleiben exakt kleingeschrieben und damit eindeutig, die Änderung bleibt auf die Erkennung des Bot-Pfads beschränkt, und der Bot ruft ohnehin `/bot/categories` auf. Das steht in docs/bot-api.md (Abschnitt Routen, Pfadregel) und ist getestet.

Kodierter Schrägstrich: Kestrel lässt `%2F` in `Request.Path` unausgepackt. Die Tests setzen deshalb `PathString "/bot%2fcategories"` und `"/BOT%2Fcategories"` unverändert, das ist der Wert, den der Handler im Betrieb sieht. Nicht gegen einen echten Kestrel geprüft, nur gegen DefaultHttpContext.

Tests (2 neu in src/Tests/BotApiTests.fs): fünf Pfadvarianten ohne Token 401 JSON; mit Token 404 JSON, `/bot/categories` bleibt 200, `/botanik` und `/api/bot` gehen weiter. Der bestehende Test für /botanik und /api/irgendwas ist grün. Rot-Prüfung ohne die Änderung: der Versuch, den Code kurz zurückzudrehen, scheiterte an einem sed-Fehler (Datei blieb unverändert), daher nicht ausgeführt. Der alte Code reichte /BOT und /bot%2f… nachweislich weiter (kein Treffer in isBotPath), die Tests erwarten dort 401/404.

Läufe aus dem Worktree: dotnet build 0 Warnungen, 0 Fehler; dotnet test 2500 bestanden, 7 übersprungen, 0 fehlgeschlagen; npm run build, guard:classes und guard:tokens grün; npm run test:scripts 281 bestanden, 0 fehlgeschlagen. Kein README_DELTA nötig (keine neue Sprache, kein neuer Begriff).
