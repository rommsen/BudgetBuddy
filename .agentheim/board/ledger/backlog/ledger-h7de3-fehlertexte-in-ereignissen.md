---
id: ledger-h7de3
title: "Fehlertexte in Ereignissen: fehlendes Token als „nicht eingerichtet“, YNAB-Rohtext nur ins Log, Token- und Budget-Zugriff an einer Stelle"
status: backlog
type: bug
context: ledger
created: 2026-11-12
completed:
depends_on: [ledger-xqqnv]
blocks: []
tags: [bestandsreview, captured]
related_adrs: []
related_research: []
prior_art: []
---

## Why
Bestandsreview ledger vom 2026-11-12, Gruppe G3. Roman hat die Gruppe nachträglich als Task gewählt (vorher Kann-Sammlung ledger 65).

## What
Alle Befunde der Gruppe in Befundform, wörtlich aus dem Bericht `.agentheim/knowledge/reviews/2026-11-12-ledger.md`:

### G3 Fehlertexte: fehlendes Token und rohe YNAB-Antworten landen in Ereignissen (mittel, 6 Befunde, ledger)
Gründe in `events` und in der Oberfläche sind falsch („Token ungültig“ statt „fehlt“) oder englische Roh-Antworten.
- src/Server/Api.fs:998 | standards/backend/overview.md Konventionen | mittel | Fehlendes Token kommt in zwölf Push-Wegen als `Unauthorized` an und wird zu „YNAB-Token ungültig (HTTP 401)“; als `RefusedByBb "Kein YNAB-Token eingerichtet"` melden.
- src/Server/YnabClient.fs:1482 | Kommentar YnabClient.fs:318 (infrastructure-vp308) | mittel | Schreibwege des Kassenbuchs (1328, 1418, 1561, 1625, 1702, 1766, 1833, 1899, 2051, sendPrepared 1098/1102) legen YNABs rohen englischen Antworttext als Grund in `events`; über `badRequestOf`/`statusFailureOf`, Rumpf ins Log.
- src/Server/Api.fs:988 | test-doctrine.md Punkt 3 | mittel | Zehn Kopien von „Token und Budget aus Settings“; auf `ynabPushWith` umstellen und in `apiWirings` belegen (backend und tests).
- src/Server/BotApi.fs:566 | ADR 0066 Nachtrag 2026-11-07 | niedrig | Bob bekommt ohne Einrichtung „YNAB-Token ungültig“; Satz für fehlende Einrichtung.
- src/Server/Ledger/CommandHandlers.fs:588 | Api.fs:2024 (infrastructure-vp308) | niedrig | „Spiegel-Lauf abgebrochen: {ex.Message}“ und `WriteFailed ex.Message` geben rohe Ausnahmetexte an den Client.
- src/Server/Api.fs:1217 | Lesbarkeit | niedrig | `budgetAccountsCache` als `ref` ohne Sperre mit `DateTime.UtcNow`.

## Acceptance criteria
- [ ] To be defined during refinement.

## Notes
Captured via `quick-capture` on 2026-11-12, raw, unrefined. Überschneidet sich mit Kann-Sammlung ledger 54 (Push-Wege auf `ynabPushWith`) und 55 („Token ungültig“ statt „fehlt“); beide gehen hier auf.

Nach ledger-xqqnv (vorhandene import_id heißt angekommen) bauen: beide ändern `pushComdirectImports` (`WriteFailed ex.Message`, `CommandHandlers.fs:1347`) und `ynabErrorText`/`describeNotCreatedReason` (Refinement ledger-xqqnv, 2026-11-12).

Aus dem Review-Lauf 2026-11-12: `mapSplitStatus` in `YnabClient.fs` (PUT und DELETE des Split-Wegs) legt den Antwortrumpf in den Fehlertext (`"Bad request: {bodyText}"`, `"Conflict: {bodyText}"`, `"HTTP {status}: {bodyText}"`), der so an den Client und in `events` geht; gehört zu Befund 2 dieser Task (auf `badRequestOf`/`statusFailureOf` umstellen). Der Ersatz-POST nutzt seit ledger-f8bjm `interpretReplacementResponse` und ist schon sauber.
