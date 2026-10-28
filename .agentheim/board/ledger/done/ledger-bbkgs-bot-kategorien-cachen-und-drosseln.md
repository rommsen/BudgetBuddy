---
id: ledger-bbkgs
title: "Bot-Vorschau und /bot/categories fragen YNAB ohne Cache: Kategorien cachen, /bot/ drosseln (Security-Review M3)"
status: done
type: feature
context: ledger
created: 2026-10-28
completed:
depends_on: [ledger-ssmp3]
blocks: [ledger-dtrn2]
tags: [security, bob, ynab-ratelimit]
related_adrs: [0066, 0068]
related_research: []
prior_art: []
---

## Why
Jede Bot-Vorschau und jedes `GET /bot/categories` ruft YNAB direkt ab. Eine Schleife bei Bob verbraucht das YNAB-Ratenlimit, das sich Bob mit Sync und Quick Add teilt (Security-Review M3).

## What
Umsetzung von ADR 0068 Punkt 4:

- **Kategorien-Cache:** Die Bot-Schnittstelle hält die YNAB-Kategorien 5 Minuten im Speicher (`BotApi.liveDeps`, heute `YnabClient.getCategories` je Aufruf). Vorschau und `GET /bot/categories` teilen den Cache. Ein YNAB-Fehler ohne gültigen Cache bleibt 502 `ynab_unavailable`.
- **Drosselung:** höchstens 10 Anfragen pro Minute auf `/bot/`, gezählt über alle Bot-Anfragen (auch die mit 401, damit Raten des Tokens gebremst wird). Darüber 429 mit Code `rate_limited`, JSON-Fehlerform wie die übrigen Fehler und Kopf `Retry-After` in Sekunden. Im Speicher, ein Neustart setzt zurück. Uhr injizierbar wie die übrigen `BotDeps`.

## Acceptance criteria
- [ ] Zwei Vorschauen und ein `GET /bot/categories` innerhalb von 5 Minuten lösen genau einen YNAB-Kategorienabruf aus; nach Ablauf von 5 Minuten (injizierte Uhr) einen zweiten (Test mit zählender YNAB-Attrappe).
- [ ] Ohne gültigen Cache und mit fehlschlagendem YNAB-Abruf antwortet die Vorschau weiter mit 502 `ynab_unavailable` (bestehender Test grün).
- [ ] Die 11. Anfrage innerhalb einer Minute bekommt 429 `rate_limited` mit `Retry-After`; nach Ablauf der Minute (injizierte Uhr) geht die nächste wieder durch (Test).
- [ ] Anfragen mit falschem Token zählen mit: 10 Fehlversuche, danach 429 auch mit richtigem Token innerhalb derselben Minute (Test).
- [ ] Der Quick-Add-Weg und der Sync nutzen den Bot-Cache nicht und sind unverändert (bestehende Tests grün).
- [ ] `docs/bot-api.md` nennt Cache, Drosselung und `rate_limited`; die ledger-README (Bot-Schnittstelle) ebenso.
- [ ] `dotnet build`, `dotnet test` und `npm run test:scripts` grün.

## Notes
Herkunft: Security-Review der Homelab-Sitzung (homelab-58) am 2026-10-28, erfasst per quick-capture, verfeinert mit Roman am 2026-10-28. Befund M3, vierter in der Reihenfolge. ADR 0068 Punkt 4.
Hängt an ledger-ssmp3, weil beide `BotApi.fs` ändern.

## Outcome

ADR 0068 Punkt 4 umgesetzt in `src/Server/BotApi.fs`. Neu: `BotRateLimiter` (gleitendes Fenster, 10 je Minute, Sperre per `lock`, abgewiesene Anfragen zählen nicht, `Retry-After` = Sekunden bis zur nächsten freien Anfrage, mindestens 1) und `BotCategoryCache` (5 Minuten, nur Erfolge, Abruf unter Semaphore, damit parallele Aufrufe nicht doppelt zu YNAB gehen). Beides sind neue Felder in `BotDeps` (`Limiter`, `Categories`), die Uhr kommt aus `deps.Now`. `liveDeps` erzeugt je eine Instanz. Die Drosselung läuft in `handler` direkt nach `isBotPath`, vor der Anmeldung, also zählen auch 401. 429 antwortet mit `error: rate_limited`, `message`, `retry_after_seconds` und Kopf `Retry-After`. `isBotPath` ist unverändert. Quick Add und Sync rufen YNAB weiter ohne Cache. `docs/bot-api.md` beschreibt Cache, Drosselung und `rate_limited`.

Tests (6 neu in `BotApiTests.fs`): Cache (ein Abruf für zwei Vorschauen und eine Liste, zweiter nach 5 Minuten per Uhr), YNAB-Fehler bleibt 502 und wird nicht gecacht, 11. Anfrage 429 mit Retry-After 60 und danach wieder 200, Retry-After-Rechnung am Limiter, 10 falsche Tokens dann 429 mit richtigem Token, 200 parallele Aufrufe lassen genau 10 durch. Die Test-`Env` bekommt je Test einen frischen Limiter mit hoher Grenze (100000), nur die Drosseltests setzen 10; dadurch bleiben bestehende Tests stabil.

Läufe aus dem Worktree: `dotnet build` 0 Warnungen, `dotnet test` 2498 bestanden, 7 übersprungen, 0 fehlgeschlagen (Bot-Filter: 74), `npm run build` ok, `guard:classes` ok, `guard:tokens` ok, `npm run test:scripts` 281 bestanden, 0 fehlgeschlagen. Der Selbstcheck auf Gedankenstriche und Umschreibungen blieb ohne Treffer. README_DELTA vom Gate geprüft.

Hinweis: Hinter Caddy sieht BB die Anfragen als eine Quelle, die Drosselung ist daher global über alle Aufrufer (gewollt laut Task: "über alle Bot-Anfragen").
