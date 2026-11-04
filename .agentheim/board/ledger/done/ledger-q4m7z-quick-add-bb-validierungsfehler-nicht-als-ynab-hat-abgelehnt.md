---
id: ledger-q4m7z
title: "Quick-Add: BB-Validierungsfehler nicht als \"YNAB hat abgelehnt\" melden"
status: done
type: bug
context: ledger
created: 2026-11-04
completed:
depends_on: []
blocks: []
tags: [meldungen, quick-add]
related_adrs: []
related_research: []
prior_art: [budgeting-b5n8q]
---

## Why
Gefunden bei budgeting-b5n8q. `addManualTransaction` in `src/Server/Api.fs` (etwa Zeile 436) gibt Fehler aus `Validation.validateManualTransaction` als `YnabError.InvalidResponse` zurück. `ynabErrorText` macht daraus "YNAB hat abgelehnt: ...", obwohl BB selbst abgelehnt hat. Soll BB führend werden, muss die Meldung den Ablehnenden richtig nennen.

## What
Die Validierungsfehler als `YnabError.RefusedByBb` (eingeführt in budgeting-b5n8q) melden. Vorher prüfen, ob der Client oder Bob (`/bot/`) den Fall `InvalidResponse` auswertet, und welcher BC Quick-Add besitzt (ledger oder ynab-sync).

## Acceptance criteria
- [ ] API-Adapter-Test: ungültiger Quick-Add liefert `RefusedByBb`, der Nutzertext enthält kein "YNAB hat abgelehnt" und nennt alle Fehler.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.
## Outcome

`ynabApi.addManualTransaction` in `src/Server/Api.fs` gibt Fehler aus `Validation.validateManualTransaction` jetzt als `YnabError.RefusedByBb` zurück, mit allen Fehlern durch Komma getrennt. Vorher war es `InvalidResponse`, und `ynabErrorText` machte daraus "YNAB hat abgelehnt: ...". Der Doc-Kommentar am Vertrag in `src/Shared/Api.fs` nennt jetzt `RefusedByBb`.

Regressionstest (API-Adapter-Ebene, ruft das Record-Feld aus `Server.Api.ynabApi` direkt auf): `src/Tests/QuickAddTests.fs`, Liste "addManualTransaction (API-Adapter)", Test "Ein ungültiger Quick-Add wird als Ablehnung durch BB gemeldet und nennt alle Fehler". Die Anfrage hat Betrag 0 und ein Memo mit 501 Zeichen. Geprüft wird: der Fall ist `RefusedByBb`, der Nutzertext enthält kein "YNAB hat abgelehnt" und enthält beide Fehler. Vor dem Fix war der Test rot (Fall `InvalidResponse`). Die Prüfung läuft vor jedem Lesen der Einstellungen, der Test berührt also weder Datenbank noch YNAB.

Was vorher geprüft wurde:
- Client: `SubmitQuickAdd` in `src/Client/State.fs` bildet Fehler nur über `ynabErrorText` auf Text ab und wertet `InvalidResponse` nicht eigens aus. Der Client zeigt jetzt die Fehler ohne YNAB-Präfix an.
- Bob (`/bot/`, `src/Server/BotApi.fs`) ruft `addManualTransaction` nicht auf, er geht direkt über `YnabClient.createManualTransaction`. Die Bob-Grenzen (ADR 0068) sind nicht betroffen.
- `scripts/*.test.mjs`: keine Fixture verwendet den alten Text oder den Fall für Quick-Add. `RefusedByBb` gibt es schon im Shared-Typ (budgeting-b5n8q), es kommt also kein neuer Fall über Fable.Remoting dazu.
- Die Zeile in `src/Tests/RemotingEndpointNegativeTests.fs` erwartet nur irgendeinen Fehler und bleibt grün.

Offene Frage zum BC: Quick-Add gehört heute zu `ynab-sync`. Der Endpoint liegt in `YnabApi`, und die Begriffe "ManualTransaction (Quick Add)" und "Quick-Add-Konto" stehen in `contexts/ynab-sync/README.md`. Laut ADR 0021 (context-map) soll Quick-Add erst ins Kassenbuch und dann zu YNAB gepusht werden (`ledger-tdnpk`), dann wäre `ledger` der Besitzer. Diese Änderung führt keine neuen Begriffe ein, deshalb gibt es kein README-Delta. Die Task-Datei bleibt in `ledger`.

Gate: `./verify.sh` im Worktree mit `VERIFY: PASS` (dotnet 3802 grün, node 300 grün). Log: /private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/78d1d4cc-38a9-4142-849d-286626b8b6e6/scratchpad/verify-ledger-q4m7z.log
