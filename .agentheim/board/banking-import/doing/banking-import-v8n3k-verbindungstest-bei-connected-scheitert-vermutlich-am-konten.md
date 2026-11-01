---
id: banking-import-v8n3k
title: "Verbindungstest bei Connected prüft über den Umsatzabruf, der Kontenabruf fliegt raus"
status: doing
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
