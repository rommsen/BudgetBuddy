---
id: ledger-r7h2w
title: "Wartende Buchung (unsicheres Duplikat) übernehmen oder verwerfen: Befehle „Ist neu“, „Ist doppelt, verwerfen“ und Rückgängig mit Ereignissen, „Ist neu“ schickt sie nach YNAB"
status: todo
type: feature
context: ledger
created: 2026-11-11
completed:
depends_on: [banking-import-d0fey]
blocks: [ledger-v6s7c]
tags: [etappe-3, backend, kassenbuch]
related_adrs: [0021, 0083, 0091, 0093]
related_research: []
prior_art: []
---

## Why
Mit `banking-import-d0fey` wartet ein unsicheres Duplikat im Kassenbuch und geht nicht nach YNAB (ADR 0093). Roman
entscheidet beim Durchgehen (`ledger-v6s7c`): „Ist neu“ schickt sie nach YNAB, „Ist doppelt, verwerfen“ entfernt sie,
mit Rückgängig. Diese Task baut die Befehle samt Ereignissen und API, die Karte baut `ledger-v6s7c`.

## What
Nach ADR 0093 Punkt 4, Ereignisse der Entscheidungs-Familie, Actor `UserInBb`, Version 1, alle unter der
Schreib-Sperre (`WriteGuard.exclusive`), Spiegel-Auslöser erst nach der Freigabe:
- `confirmEntryIsNew` (API `LedgerApi.confirmEntryIsNew: Guid -> …`): `EntryConfirmedAsNewInBb` (ohne Payload), danach
  der Push über den Einzel-Push wie `retryPushToYnab` (`EntryPush`, mit der `import_id`), bei Erfolg
  `EntryLinkedToYnab`, sonst `EntryPushToYnabFailed`. Kein Rollback (ADR 0021): nach einem Fehlschlag ist sie eine
  gewöhnliche „Buchung nicht in YNAB“ mit „Erneut senden“. Antwort nennt Erfolg oder Grund.
- `discardDuplicateEntry` (API `LedgerApi.discardDuplicateEntry`): `EntryDiscardedAsDuplicateInBb` (Payload
  `MatchedYnabTransactionId`, `MatchedEntryId option`). `apply` setzt `DeletedInYnab` (wie `EntryDiscardedInBb`), die
  Buchung zählt nirgends und verschwindet aus „zu prüfen“. Kein YNAB-Aufruf.
- `restoreDiscardedDuplicate` (API `LedgerApi.restoreDiscardedDuplicate`): `EntryDiscardRevokedInBb` (ohne Payload),
  `DeletedInYnab` wieder aus, die Buchung wartet wieder. Ohne Zeitgrenze im Server (wie ADR 0091 Punkt 4).
- Invarianten in `apply`: Übernehmen und Verwerfen nur bei wartender, nicht verworfener, nicht verknüpfter Buchung;
  Rückgängig nur bei verworfener wartender Buchung. Sonst Fehler, kein Ereignis.
- Historie: „Vielleicht doppelt, zurückgehalten: <Grund>“ (aus d0fey), „Als neu übernommen“, „Als doppelt
  verworfen“, „Verwerfen zurückgenommen“.
- Ein späterer Abruf liefert dieselbe Reference: eine verworfene oder wieder wartende Buchung bleibt, wie sie ist
  (ADR 0093 Punkt 7, gebaut in d0fey); eine übernommene ist verknüpft und gilt als `AlreadyImported`.

## Acceptance criteria
Handler-Tests mit Ports als Funktionswerten (Vorbild `src/Tests/LedgerMemoTests.fs`), Uhr als Parameter, eindeutige
Ids; Domain-Tests für `apply`.
- [ ] GIVEN eine wartende Buchung WHEN `confirmEntryIsNew` und der Push gelingt THEN `EntryConfirmedAsNewInBb`, dann
      genau ein POST mit der `import_id` `BB:<Reference>`, dann `EntryLinkedToYnab`; sie ist nicht mehr wartend,
      zählt im Kontostand und bleibt „zu prüfen“.
- [ ] GIVEN der Push scheitert WHEN `confirmEntryIsNew` THEN `EntryConfirmedAsNewInBb` bleibt, dazu
      `EntryPushToYnabFailed`; sie erscheint in `getEntriesNotInYnab`.
- [ ] GIVEN eine wartende Buchung WHEN `discardDuplicateEntry` THEN `EntryDiscardedAsDuplicateInBb`, kein YNAB-Aufruf,
      sie zählt nicht in „zu prüfen“, nicht im Kontostand, nicht in „Buchung nicht in YNAB“ und fehlt in der Liste
      ohne „gelöschte zeigen“.
- [ ] GIVEN eine verworfene Buchung WHEN `restoreDiscardedDuplicate` THEN `EntryDiscardRevokedInBb`, sie wartet
      wieder und zählt wieder in „zu prüfen“.
- [ ] GIVEN eine nicht wartende, eine verknüpfte, eine schon verworfene (für Übernehmen und Verwerfen), eine nicht
      verworfene (für Rückgängig) oder eine unbekannte Buchung WHEN der jeweilige Befehl THEN Fehler mit konkretem
      Satz, kein Ereignis, kein YNAB-Aufruf (ein Fall je Regel).
- [ ] GIVEN verworfen und danach ein Abruf mit derselben Reference THEN kein Ereignis, kein Push (Handler-Fall, der
      den Abgleich aus d0fey mit dem echten Ereignis durchläuft).
- [ ] Replay mit allen drei Typen ergibt denselben Zustand.
- [ ] `src/Tests/RemotingEndpointNegativeTests.fs` hat je Endpunkt eine Zeile (unbekannte Id, nicht wartend).
- [ ] README ledger: Begriffe **Ist neu**, **Als doppelt verworfen**, **Schon im Kassenbuch**, die drei Ereignisse
      unter Key events, Key commands *WartendeÜbernehmen*, *WartendeVerwerfen*.
- [ ] `./verify.sh` endet mit `VERIFY: PASS`.

## Notes
Keine Oberfläche: die Duplikat-Karte mit „Ist neu“ und „Ist doppelt, verwerfen“ baut `ledger-v6s7c`. Ein Zugang im
Buchungs-Detail des Kassenbuchs ist nicht Teil des Prototyps; er kommt nicht
(Roman, 2026-11-11): das Durchgehen reicht, die Zeile „doppelt?“ führt über Zu tun dorthin.

Ereignisnamen nach Rat des `tactical-modeler` (2026-11-11). Der Feldname `DeletedInYnab` passt beim Verwerfen nicht
wörtlich; er bedeutet seit ADR 0091 und 0092 „gelöscht, zählt nicht“ und deckt so rund 50 Filter ohne Änderung.
