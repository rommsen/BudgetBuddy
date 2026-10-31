---
id: banking-import-tshq9
title: "Bankverbindung als Zustandsautomat mit Erneuern im Hintergrund. Abbruch und Verbindungstest beenden sie nicht mehr"
status: doing
type: feature
context: banking-import
created: 2026-10-30
completed:
depends_on: [banking-import-p5mcp]
blocks: [banking-import-z4h6p, banking-import-bftq0, banking-import-cyg94]
tags: [backend, comdirect, session, hintergrunddienst]
related_adrs: [0071]
related_research: []
prior_art: []
---

## Why
Offene Challenge und aktive Session sind heute nicht unterscheidbar (`isAuthenticated` ist bei
beiden wahr). `cancelSync` löscht die Session, ein Verbindungstest ersetzt sie.

## What
- Reiner Automat `Disconnected | ChallengePending | Connected` nach ADR 0071 Punkt 2, Uhr als Parameter.
- Semaphor um Refresh und Bankzugriff.
- `tick ports now`, `BackgroundService` nur in `Program.fs`.
- Strukturwächter für `AddHostedService` mit Gegenbeweis, neue Module in den Netzwächter.
- `cancelSync` und Verbindungstest umstellen, geänderte Zugangsdaten trennen die Verbindung.
- `DateTime.Today` in `getTransactions` durch einen Parameter ersetzen.

## Acceptance criteria
- [ ] GIVEN `Connected` und das Token läuft in unter 60 s ab WHEN tick THEN genau ein Refresh, das neue Token ist in Verwendung.
- [ ] GIVEN `ChallengePending` WHEN tick THEN kein Refresh.
- [ ] GIVEN zwei gleichzeitige Refresh-Anlässe THEN ein Aufruf, beide nutzen das Ergebnis.
- [ ] GIVEN Refresh `ReloginRequired` THEN `Disconnected`, der Verlust ist einmal gemeldet, keine Challenge.
- [ ] GIVEN Refresh `RetryLater` THEN `Connected`, nächster Versuch nach 60 s.
- [ ] GIVEN ein Datenabruf antwortet 401 oder 403 THEN ein Refresh und ein Wiederholversuch, bei erneutem Fehlschlag `Disconnected`.
- [ ] Kaputt: GIVEN `Connected` WHEN `cancelSync` THEN die Verbindung bleibt (vor dem Fix rot). Intakt: GIVEN `ChallengePending` WHEN `cancelSync` THEN `Disconnected`.
- [ ] Kaputt: GIVEN `Connected` WHEN `testComdirectConnection` THEN keine Challenge, Verbindung unverändert. Intakt: GIVEN `Disconnected` THEN Challenge wie bisher.
- [ ] GIVEN Zugangsdaten geändert THEN `Disconnected`.
- [ ] GIVEN `Disconnected` WHEN beliebig viele ticks THEN keine Challenge und kein Passwort-Login.
- [ ] Ein Testlauf im TestServer startet keinen Dienst.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

## Notes
Offene Frage an Roman: Soll eine Verbindung nach fester Zeit zwangsweise eine neue TAN verlangen?
Stand ADR 0071: nein, "Bank trennen" reicht. Ein Ja wäre eine kleine Ergänzung hier.

Promotion erst nach Go in `banking-import-p5mcp`.
