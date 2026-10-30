---
id: banking-import-z4h6p
title: "Bankverbindung übersteht einen Neustart über verschlüsselt gespeichertes Refresh-Token"
status: backlog
type: feature
context: banking-import
created: 2026-10-30
completed:
depends_on: [banking-import-tshq9]
blocks: []
tags: [backend, comdirect, session, persistenz, secrets]
related_adrs: [0071]
related_research: []
prior_art: []
---

## Why
Ein Deploy soll keine TAN kosten, solange das Refresh-Token noch gilt.

## What
Verschlüsselter JSON-Wert unter einem Settings-Schlüssel nach ADR 0071 Punkt 4, geschrieben vor
der Verwendung des neuen Tokens, ein Wiederaufnahmeversuch beim Start.

## Acceptance criteria
- [ ] GIVEN Refresh erfolgreich THEN der gespeicherte Wert enthält das neue Token, bevor es verwendet wird.
- [ ] GIVEN ein gespeicherter Wert WHEN Start und Refresh gelingt THEN `Connected`, keine Challenge.
- [ ] GIVEN `ReloginRequired` beim Start oder ein nicht entschlüsselbarer Wert THEN Wert gelöscht, `Disconnected`, keine Challenge.
- [ ] GIVEN `RetryLater` beim Start THEN der Wert bleibt, nächster Versuch nach 60 s.
- [ ] GIVEN kein `BUDGETBUDDY_ENCRYPTION_KEY` THEN nichts gespeichert, die Verbindung lebt im Speicher.
- [ ] GIVEN Trennen oder geänderte Zugangsdaten THEN Wert gelöscht.
- [ ] Der Wert erscheint nie in `getSettings` (Vorbild `src/Tests/SettingsSecretsTests.fs`).
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

## Notes
Entfällt, wenn die Wiederaufnahme in `banking-import-p5mcp` scheitert.
