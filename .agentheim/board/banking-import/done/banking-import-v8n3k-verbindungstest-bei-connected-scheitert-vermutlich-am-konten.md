---
id: banking-import-v8n3k
title: "Verbindungstest bei Connected prüft über den Umsatzabruf, der Kontenabruf fliegt raus"
status: done
type: bug
context: banking-import
created: 2026-10-31
completed:
depends_on: []
blocks: []
tags: [backend, comdirect]
related_adrs: [0071]
related_research: []
prior_art: [banking-import-tshq9]
---

## Why
ADR 0071 Punkt 2 lässt den Verbindungstest bei `Connected` die Konten lesen. Laut Live-Befund lieferte
`getAccounts` in allen sechs Runden 404, Umsätze kamen dagegen. Roman sieht dann einen Fehler, obwohl
die Verbindung lebt.

Der Kontenabruf hat im Server nur diesen einen Aufrufer.

## What
Der Test liest bei `Connected` die erste Umsatzseite der konfigurierten Konto-Id über `withConnection`,
also denselben Pfad wie der Import. `getAccounts`, `getAccountsWith`, `accountsDecoder`,
`fetchAccountsWith`, `FetchAccounts` an den Ports und `ComdirectAccount` fliegen raus, samt ihrer Tests
und dem Schritt in `src/Tests/ComdirectProbe.fs`.

## Acceptance criteria
- [ ] Kaputt: GIVEN `Connected`, Umsätze kommen und der Kontenabruf liefert 404 WHEN `testComdirectConnection` THEN `BankConnectionActive` (Test vor dem Fix rot).
- [ ] Intakt: GIVEN `Connected` und die Bank lehnt auch nach dem Refresh ab THEN Fehler ohne Challenge.
- [ ] Intakt: GIVEN `Connected` und ein 503 THEN Fehler ohne Challenge, die Verbindung bleibt `Connected`.
- [ ] GIVEN `Connected` und keine Konto-Id eingetragen THEN konkrete Meldung, kein Bankzugriff, keine Challenge.
- [ ] Der Test holt genau eine Seite, auch bei langem Zeitraum.
- [ ] `grep -rn "getAccounts\|ComdirectAccount\b" src` findet nichts mehr, `getBudgetAccounts` und andere YNAB-Namen ausgenommen.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.
- [ ] Der Verbindungstest meldet bei lebender Verbindung in der Produktion Erfolg, ohne TAN. [human-eye]

## Notes
Entschieden von Roman am 2026-11-01, siehe Nachtrag in ADR 0071. Die Alternative war der Pfad
`clients/user/v2/accounts/balances`. Er fehlt in den drei probierten Pfaden und erklärt den 404
vermutlich, belegt ist er nur über Drittquellen. Der Umsatzpfad ist live gemessen.

Die Seite kommt ohne `paging-count`, also genau wie die erste Seite beim Import. So bleibt es beim
gemessenen Aufruf. Die Konto-Id braucht der Import ohnehin.

Die README von `banking-import` nennt die Konten an drei Stellen und muss im README-Delta mit: Verbindungstest,
Bank-Port, Invariante Abbruch. Dazu der Kommentar an `testComdirectConnection` in `src/Shared/Api.fs`.

## Verifier note (iteration 1)

REASONS:
- Check 5 (README sync): `ComdirectAccount` ist aus `src/Shared/Domain.fs` entfernt, der Bullet in der README (Zeile 28, `## Ubiquitous language`) bleibt aber stehen, das Delta hat dafür keine Operation.
- Derselbe Begriff steht in `.agentheim/knowledge/context-map.md:19`.
- Veralteter Kommentar: `src/Shared/Domain.fs:698` sagt an `BankConnectionActive` noch "die Konten waren lesbar".
- Checks 1 bis 4 bestanden, `./verify.sh` zeigte `VERIFY: PASS`.

SUGGESTED_FIX: Den Kommentar auf die erste Umsatzseite ändern. Die beiden `.agentheim/`-Stellen löscht der Conductor bei der Integration, weil das README-Delta keine Löschung kennt.

ITERATION_HINT: likely-fixable

## Outcome

Der Verbindungstest bei `Connected` liest jetzt die erste Umsatzseite der konfigurierten Konto-Id, der Kontenabruf ist entfernt. `./verify.sh` zeigt `VERIFY: PASS` (dotnet 3212, node 419). Das README-Delta besteht `check:readme-delta` (Exit 0, Lauf aus Iteration 1, das Delta ist unverändert).

**Nicht live gelaufen:** Es gab keinen Aufruf gegen Comdirect. Das Kriterium mit `[human-eye]` (Erfolg ohne TAN in der Produktion) ist offen und braucht Romans Klick nach dem Deploy.

**Rot vor dem Fix:** Der Fake-Port `FetchAccounts` in `src/Tests/BankConnectionTests.fs` antwortete temporär mit 404, die Umsätze blieben intakt. "Kaputt: Connected, testComdirectConnection" war rot: `Error (ComdirectCredentialsInvalid ("accounts", "Network error (HTTP 404)"))` statt `Ok BankConnectionActive`, die übrigen 11 Fälle der Liste grün.

**Änderung:**
- `src/Server/ComdirectClient.fs`: `getFirstTransactionsPageWith` und `getFirstTransactionsPage` rufen `getTransactionsPage` mit Offset 0 auf, ohne `paging-count` und ohne Blättern.
- `src/Server/ComdirectAuthSession.fs`: `fetchFirstTransactionsPageWith` läuft über `withConnection` (Semaphor, Refresh, ein Wiederholversuch), neuer Port `FetchFirstTransactionsPage`.
- `src/Server/Api.fs`: `testComdirectConnectionWith` liest bei `Connected` die Konto-Id. Fehlt sie oder ist sie leer, kommt `ComdirectCredentialsInvalid ("account_id", ...)` ohne Bankzugriff. Das Fehlerfeld des Abrufs heißt jetzt `transactions` statt `accounts`.
- `src/Shared/Domain.fs`: der Kommentar an `BankConnectionActive` nennt die erste Umsatzseite des konfigurierten Kontos (Iteration 2, Verifier-Befund).
- Entfernt: `getAccounts`, `getAccountsWith`, `accountsDecoder`, `accountDecoder`, `fetchAccountsWith`, `FetchAccounts` an `ConnectionPorts`, `BankPort` und den Sonden-Ports, `ComdirectAccount`, die drei Konten-Tests aus `src/Tests/ComdirectHttpTests.fs`.

**Tests:**
- `src/Tests/BankConnectionTests.fs`: Erfolg bei `Connected` prüft die Konto-Id und genau einen Abruf. Neu: Ablehnung auch nach dem Refresh, 503, Konto-Id fehlt oder ist leer.
- `src/Tests/ComdirectHttpTests.fs`: eine vollständig im Zeitraum liegende Seite löst genau eine Anfrage aus, Pfad und Query sind exakt geprüft (`?transactionState=BOOKED&paging-first=0`). Der Rumpf-Test läuft jetzt über diese Funktion.
- `src/Tests/NetworkCallGuardTests.fs`: die Stichprobe nennt `getFirstTransactionsPageWith`.

**Über den Auftrag hinaus:** Die Sonde in `src/Tests/ComdirectProbe.fs` hatte als Rückfall das erste Konto aus dem Kontenabruf. Ohne `COMDIRECT_ACCOUNT_ID` protokolliert sie jetzt `umsaetze status=keine-konto-id` und holt nichts. Das README-Delta passt zusätzlich `fetchForReview` und den Live-Befund zum Kontenabruf an.

**Grep-Kriterium:** `grep -rn "getAccounts\|ComdirectAccount\b" src` findet nur noch YNAB-Namen: `getAccountsDelta` und seinen Aufrufer sowie alles, was den Teilstring in `budgetAccounts` enthält (`budgetAccountsCache`, `fetchBudgetAccounts`, `openOnBudgetAccounts` und andere). `FetchAccounts` in `src/Server/Ledger/` und den Ledger-Tests gehört ebenfalls zum YNAB-Spiegel und bleibt.

**Kommentar-Durchsicht (Iteration 2):** Die Suche über `src` nach Konten-Aussagen zum Verbindungstest fand außer dem Kommentar an `BankConnectionActive` keine aktuelle Behauptung. `src/Tests/BankConnectionTests.fs:422` nennt den Kontenabruf bewusst in der Vergangenheit als den verhinderten Bug und bleibt.
