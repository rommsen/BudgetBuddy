---
id: banking-import-cyg94
title: "Sync ohne TAN bei aktiver Bankverbindung, über einen schmalen Bank-Port"
status: todo
type: feature
context: banking-import
created: 2026-10-30
completed:
depends_on: [banking-import-tshq9]
blocks: [banking-import-t4raw]
tags: [backend, frontend, comdirect, sync, bank-port]
related_adrs: [0071]
related_research: []
prior_art: []
---

## Why
Das ist das eigentliche Ziel. Der Abruf steckt in `confirmTan`, die TAN-Entscheidung im Client:
`SyncFlow/State.fs` schickt nach `SyncStarted` immer `InitiateComdirectAuth`.

## What
- `BankPort`-Record, nur Comdirect implementiert ihn. Keine Umbenennung der `Comdirect*`-Typen.
- `fetchForReview` aus `confirmTan` ziehen.
- `startSync` liefert `SyncStart = TanRequired | ReviewReady`, `initiateComdirectAuth` entfällt.
  `src/Tests/RemotingEndpointNegativeTests.fs` und Fixtures in `scripts/*.test.mjs` im selben Schritt.
- `ReviewReady` lädt im Client direkt die Transaktionen. Toast-Text und Fortschritt nach Styleguide.

## Acceptance criteria
- [ ] Regression: GIVEN Comdirect-JSON mit fester Reference WHEN Decoder und `generateImportId` THEN `TransactionId` und `import_id` sind exakt wie vor dem Umbau, einschließlich heutiger Normalisierung.
- [ ] GIVEN `Connected` WHEN `startSync` THEN `ReviewReady`, keine Challenge.
- [ ] GIVEN `Disconnected` WHEN `startSync` THEN `TanRequired`, genau eine Challenge.
- [ ] GIVEN die Verbindung stirbt im Abruf WHEN `startSync` THEN `TanRequired`.
- [ ] GIVEN eine offene Prüfung WHEN `startSync` THEN dieselbe Sitzung zurück, Transaktionen unverändert.
- [ ] GIVEN ein Fake-Port WHEN `fetchForReview` THEN Regeln, Duplikate, Order-ID und Auto-Skip verhalten sich wie bisher.
- [ ] Browser-Wächter: ein Happy Path ohne TAN-Schritt und ein Pfad mit TAN.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.
- [ ] Ein echter zweiter Sync läuft im Betrieb ohne TAN. [human-eye]

## Notes
Design-System: nichts Neues.

Promotion erst nach Go in `banking-import-p5mcp`.
