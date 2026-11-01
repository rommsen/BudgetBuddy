---
id: banking-import-bftq0
title: "Status der Bankverbindung sichtbar, Bank trennen"
status: todo
type: feature
context: banking-import
created: 2026-10-30
completed:
depends_on: [banking-import-tshq9]
blocks: [banking-import-t4raw, banking-import-r9tfx]
tags: [backend, frontend, comdirect, session]
related_adrs: [0071]
related_research: []
prior_art: []
---

## Why
Hintergrundlogik darf nicht still ausfallen. Roman muss sehen, ob der nächste Sync eine TAN braucht,
und die Verbindung selbst beenden können.

## What
- `getBankConnectionStatus` liefert `Disconnected`, `ChallengePending` oder `Connected` als eigenen
  Typ in `src/Shared/`, ohne Tokens und ohne Bankzugriff.
- `disconnectBank` ruft das vorhandene `disconnectWith`.
- Anzeige in den Einstellungen und im SyncFlow, Knopf "Bank trennen" in den Einstellungen.

Die Meldung per `notify` bei einem Verlust ist `banking-import-r9tfx`.

## Acceptance criteria
- [ ] GIVEN `Connected` WHEN `getBankConnectionStatus` THEN der Status lautet verbunden, kein Bankzugriff.
- [ ] GIVEN `ChallengePending` WHEN `getBankConnectionStatus` THEN der Status nennt die offene TAN und zählt nicht als verbunden.
- [ ] GIVEN "Bank trennen" THEN kein `ReportLoss`, `Disconnected`, der gespeicherte Wert ist gelöscht.
- [ ] GIVEN das Löschen des gespeicherten Werts scheitert WHEN "Bank trennen" THEN Roman sieht einen Fehler, statt dass ein Token still liegen bleibt.
- [ ] GIVEN eine Sitzung in `AwaitingBankAuth` WHEN "Bank trennen" THEN `confirmTan` scheitert sichtbar und die Sitzung bleibt nicht in `AwaitingBankAuth` stehen.
- [ ] GIVEN eine Sitzung in `ReviewingTransactions` WHEN "Bank trennen" THEN die Prüfung bleibt offen.
- [ ] Der Status enthält keine Tokens (Vorbild `src/Tests/SettingsSecretsTests.fs`).
- [ ] `src/Tests/RemotingEndpointNegativeTests.fs` hat eine Zeile je neuem Endpoint, Fixtures in `scripts/*.test.mjs` sind im selben Schritt nachgezogen.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.
- [ ] Die Anzeige ist am Handy und am PC geprüft. [human-eye]

## Notes
Geteilt am 2026-11-01: Status und Trennen brauchen keinen Push, deshalb hängt dieser Task nicht mehr
an `infrastructure-jsys4`.

`disconnectWith` wartet am Bank-Gate, ein laufender Abruf endet also zuerst. Der Status liest nur den
Zustand und ruft nicht `awaitResumeWith`, das könnte bis zu 100 s an der Bank hängen. Direkt nach einem
Neustart kann er deshalb kurz `Disconnected` zeigen, bis der Hintergrunddienst die Verbindung
wiederaufgenommen hat.

Design-System: nichts Neues. Der Styleguide nennt `Badge` für persistenten Status, Toast ist dafür
verboten, Texte nach §8. Vor dem Bau gegen `/styleguide` prüfen, fehlt eine Variante, geht ein
Follow-up an `design-system`.
