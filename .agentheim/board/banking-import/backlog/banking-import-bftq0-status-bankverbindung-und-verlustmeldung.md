---
id: banking-import-bftq0
title: "Status der Bankverbindung sichtbar, Bank trennen, Web-Push bei Verlust"
status: backlog
type: feature
context: banking-import
created: 2026-10-30
completed:
depends_on: [banking-import-tshq9, infrastructure-jsys4]
blocks: [banking-import-t4raw]
tags: [backend, frontend, comdirect, session, benachrichtigung]
related_adrs: [0071]
related_research: []
prior_art: []
---

## Why
Hintergrundlogik darf nicht still ausfallen. Roman muss sehen, ob der nächste Sync eine TAN braucht,
und die Verbindung selbst beenden können.

## What
- `getBankConnectionStatus` und `disconnectBank`.
- Anzeige in den Einstellungen und im SyncFlow.
- Ein Verlust ruft `notify` einmal je Übergang von `Connected` nach `Disconnected`.
- Trennen und geänderte Zugangsdaten sind kein Verlust.

## Acceptance criteria
- [ ] GIVEN `Connected` und der Refresh wird abgelehnt THEN genau ein `notify` mit festem Text ohne Bankdaten.
- [ ] GIVEN "Bank trennen" THEN kein `notify`, `Disconnected`, der gespeicherte Wert ist gelöscht.
- [ ] GIVEN `notify` scheitert THEN der Zustand bleibt unberührt, Log-Eintrag.
- [ ] Der Status enthält keine Tokens (Vorbild `src/Tests/SettingsSecretsTests.fs`).
- [ ] `src/Tests/RemotingEndpointNegativeTests.fs` hat eine Zeile je neuem Endpoint, Fixtures in `scripts/*.test.mjs` sind im selben Schritt nachgezogen.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.
- [ ] Die Anzeige ist auf iPhone und PC geprüft. [human-eye]

## Notes
Design-System: nichts Neues. Der Styleguide nennt `Badge` für persistenten Status, Toast ist dafür
verboten, Texte nach §8. Vor dem Bau gegen `/styleguide` prüfen, fehlt eine Variante, geht ein
Follow-up an `design-system`.

Scheitert der Geräte-Beweis in `infrastructure-jsys4`, entfällt `notify` und der Task meldet nur im UI.

Promotion erst nach Go in `banking-import-p5mcp`.
