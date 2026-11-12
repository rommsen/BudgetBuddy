---
id: ledger-vfr4h
title: "Schreib-Sperre, Spiegel-Auslöser nach Freigabe und Wartesperre für Verwerfen nach dem Erfassen, Übertrag, Löschen, Wiederherstellen und Ist neu durch Verhalten belegt; Wartesperre als erster Zweig in discardCapturedBooking und restoreDeletedEntry"
status: done
type: refactor
context: ledger
created: 2026-11-12
completed:
depends_on: []
blocks: []
tags: [bestandsreview, server, tests]
related_adrs: [0091, 0092, 0093]
related_research: []
prior_art: [ledger-kfd5m]
---

## Why
Bestandsreview ledger 2026-11-12, Gruppe G4, Teil 2 von 2 (Bericht `.agentheim/knowledge/reviews/2026-11-12-ledger.md`).

Die Befehle halten die Schreib-Sperre und stoßen den Spiegel erst nach der Freigabe an, aber für Verwerfen nach dem
Erfassen, Übertrag, Löschen und Wiederherstellen ist das nicht oder nur halb belegt. Ein entfernter
`WriteGuard.exclusive` oder ein Auslöser im gesperrten Abschnitt bliebe grün (test-doctrine.md Punkt 4). Dazu meldet
„Rückgängig“ bei einer wartenden Buchung einen anderen Satz als ADR 0093 Punkt 6 verlangt.

## What
- `src/Tests/LedgerBookingCaptureTests.fs:482` | test-doctrine.md Punkt 4; ADR 0091 | hoch | Fall „abgeglichene erfasste
  Buchung: Error discardReconciledText, kein DELETE, kein Ereignis“ und „in YNAB gelöscht“ ergänzen.
- `src/Tests/LedgerBookingCaptureTests.fs:475` | test-doctrine.md Punkt 3, 4 | mittel | Spiegel-Auslöser von
  `discardCapturedBooking` zählen: einmal nach Erfolg, nie nach Ablehnung.
- `src/Tests/LedgerBookingCaptureTests.fs:482` | README *ErfassteBuchungVerwerfen* | mittel | Schreib-Sperre mit
  `commandWaitsForMirror` für `discardCapturedBooking`.
- `src/Tests/LedgerTransferTests.fs:280` | README *ÜbertragSetzen* | mittel | Schreib-Sperre für `setEntryTransfer`.
- `src/Tests/LedgerEntryDeletionTests.fs:518` | test-doctrine.md Punkt 4 | mittel | Auslöser erst nach Freigabe
  (`SyncMirrorTrigger`) für `deleteEntry`, `restoreDeletedEntry`, `discardCapturedBooking` (Anteil
  `pushComdirectImports` in ledger-xqqnv).
- `src/Tests/LedgerDuplicateDecisionTests.fs:347` | test-doctrine.md Punkt 4 | niedrig | Auslöser von
  `confirmEntryIsNew` mit `SyncMirrorTrigger` statt „triggers = 0 während des Laufs“.
- `src/Server/Ledger/CommandHandlers.fs:2327` | ADR 0093 Punkt 6 | niedrig | `isHeldAsPossibleDuplicate` als erster Zweig
  nach „nicht gefunden“ in `discardCapturedBooking` und `restoreDeletedEntry` (`:3412`), Satz
  `possibleDuplicateLockedText`.

## Acceptance criteria
- [ ] `discardCapturedBooking` auf einer abgeglichenen erfassten Buchung (verknüpft, `Cleared = Reconciled`):
      `Error discardReconciledText`, kein DELETE, kein Ereignis, kein Auslöser. Handler-Test in
      `src/Tests/LedgerBookingCaptureTests.fs`, Liste „Buchung erfassen: Rückgängig“. Gegenprobe im Outcome:
      Reconciled-Prüfung entfernt, Test rot.
- [ ] `discardCapturedBooking` auf einer erfassten Buchung, die der Spiegel als in YNAB gelöscht beobachtet hat
      (`EntryDeletionObservedInYnab`): `Error discardDeletedText`, kein DELETE, kein Ereignis. Gleiche Liste.
- [ ] Auslöser von `discardCapturedBooking` gezählt: genau einmal nach Erfolg; null nach jeder Ablehnung (unbekannt,
      gelöscht, nicht erfasst, abgeglichen, nicht verknüpft, wartend) und nach gescheitertem DELETE. Gleiche Liste, ein
      Test je Fall oder eine Tabelle über die Ablehnungen.
- [ ] Schreib-Sperre `discardCapturedBooking` über `LedgerWriteGuardProbe.commandWaitsForMirror`: während der Lauf hängt,
      kein DELETE-Aufruf und kein `EntryDiscardedInBb`; danach genau ein DELETE und das Ereignis. `testSequenced`-Liste in
      `LedgerBookingCaptureTests.fs`.
- [ ] Schreib-Sperre `setEntryTransfer` über `commandWaitsForMirror`: während der Lauf hängt, kein
      `EntryTransferTargetSetInBb` und kein PUT; danach beides genau einmal. Neue `testSequenced`-Liste „Übertrag in BB:
      Schreib-Sperre“ in `src/Tests/LedgerTransferTests.fs`.
- [ ] Auslöser nach der Freigabe mit `LedgerWriteGuardProbe.SyncMirrorTrigger(2000)` unter
      `LedgerWriteGuardProbe.serialized`, `FinishedInTime = Some true`, je für `deleteEntry` und `restoreDeletedEntry`
      (`src/Tests/LedgerEntryDeletionTests.fs`, Liste „Buchung löschen: Schreib-Sperre“) und `discardCapturedBooking`
      (`LedgerBookingCaptureTests.fs`). Gegenprobe im Outcome für einen der drei: Auslöser in den gesperrten Abschnitt
      verschoben, Test rot.
- [ ] `confirmEntryIsNew`: in `src/Tests/LedgerDuplicateDecisionTests.fs`, Liste „Wartende Buchung entscheiden:
      Schreib-Sperre“, prüft ein Test den Auslöser mit `SyncMirrorTrigger`, `FinishedInTime = Some true`; die Zählung
      „genau einmal nach Erfolg“ bleibt.
- [ ] Wartesperre (vor dem Fix rot, heute `discardNotCapturedText` bzw. `entryRestoreNotDeletedInBbText`):
      `discardCapturedBooking` und `restoreDeletedEntry` auf einer wartenden Buchung (`seedHeld` aus
      `LedgerDuplicateDecisionTests.fs` oder gleichwertig) liefern `Error possibleDuplicateLockedText`, ohne
      YNAB-Aufruf, ohne Ereignis, ohne Auslöser. Handler-Tests in `LedgerBookingCaptureTests.fs` und
      `LedgerEntryDeletionTests.fs`.
- [ ] `./verify.sh` mit `VERIFY: PASS`.

## Notes
- Urteile: `LedgerBookingCaptureTests.fs:482` (Reconciled, gelöscht) gilt teilweise: der Zweig `DeletedInYnab` ist über
  „eine schon verworfene Buchung wird nicht ein zweites Mal gelöscht“ (`:529`) belegt, es fehlt der Fall „abgeglichen“
  (`CommandHandlers.fs:2345`); der Spiegel-Fall ist billig mitzunehmen. `:475` gilt (`discardWith` übergibt `ignore`).
  `:482` Sperre gilt (kein `commandWaitsForMirror` in der Datei). `LedgerTransferTests.fs:280` gilt (Sperre im Code,
  `CommandHandlers.fs:2170`, kein Test). `LedgerEntryDeletionTests.fs:518` gilt: Löschen und Wiederherstellen zählen den
  Auslöser (`:288`, `:343`, `:414`), keiner prüft „nach der Freigabe“. `LedgerDuplicateDecisionTests.fs:347` gilt: während
  `whileHeld` wartet der Befehl noch auf die Sperre, `triggers = 0` gilt dort auch bei einem Auslöser im gesperrten
  Abschnitt. `CommandHandlers.fs:2327` gilt teilweise: kein Geldrisiko, eine wartende Buchung entsteht nur aus
  `EntryImportedFromComdirect` und scheitert heute am Ursprung bzw. an fehlendem `EntryDeletedInBb`; abweichend ist nur
  der Satz.
- Vorbilder: `src/Tests/LedgerRetryPushTests.fs:164` (Sperre und Auslöser zusammen), `LedgerEntryDeletionTests.fs:518`
  (`waitsForMirror` mit Probe). `SyncMirrorTrigger` zählt nicht; Zählung mit eigenem Zähler wie
  `DeletionProbe.MirrorTriggers` (`LedgerEntryDeletionTests.fs:95`).
- Tests mit `commandWaitsForMirror` oder `SyncMirrorTrigger` laufen über `serialized` bzw. in `testSequenced`-Listen
  (Kommentar in `src/Tests/LedgerWriteGuardProbe.fs`). Eindeutige Ids je Test (Doktrin Punkt 16).
- Der neue erste Zweig kommt direkt nach „nicht gefunden“, wie in `retryPushToYnab` (`:1010`) und `deleteEntry`
  (`:3370`). Eine als doppelt verworfene Buchung (Wartemarke und `DeletedInYnab`) bekommt damit ebenfalls
  `possibleDuplicateLockedText`; das ist gewollt (ADR 0093 Punkt 6).
- Doktrin Punkt 20 (Aufzählung aller Reihenfolgen) gilt für Client-`update`; hier geht es um die Server-Sperre, die
  Reihenfolge belegt `commandWaitsForMirror`.
- README ledger: *ErfassteBuchungVerwerfen* und *BuchungLöschen / GelöschteBuchungWiederherstellen* um die Wartesperre
  ergänzen (wie bei *PayeeÄndern*); Delta mit `npm run check:readme-delta` prüfen.
- Überschneidung ledger-xqqnv (Teil 1): beide berühren `LedgerDuplicateDecisionTests.fs`, diese Task nur die Sperre-Liste.
  `pushComdirectImports` gehört ganz zu xqqnv und wird hier nicht angefasst. Überschneidung ledger-f8bjm: dieselbe
  Funktion `restoreDeletedEntry`; hier nur der erste Zweig, f8bjm ändert den POST-Ausgang. Nicht gleichzeitig bauen.
- Sichtbar ändert sich nur der Satz, wenn eine wartende Buchung verworfen oder wiederhergestellt werden soll.

## Outcome

Wartesperre als erster Zweig nach „nicht gefunden“ in `discardCapturedBooking` und `restoreDeletedEntry` (`src/Server/Ledger/CommandHandlers.fs`, Satz `possibleDuplicateLockedText`, Doc-Kommentare nachgezogen). Sonst nur Tests:

- `src/Tests/LedgerBookingCaptureTests.fs`, Liste „Buchung erfassen: Rückgängig (ledger-at63w)“: abgeglichene erfasste Buchung (`EntryClearedStateChangeObservedInYnab` auf `Reconciled`, verknüpft) liefert `discardReconciledText`; vom Spiegel als gelöscht beobachtete (`EntryDeletionObservedInYnab`) liefert `discardDeletedText`; wartende Buchung liefert `possibleDuplicateLockedText`. Je ohne DELETE, ohne Ereignis, ohne Auslöser (Helfer `expectRefused`). Auslöser genau einmal nach Erfolg, null nach jeder Ablehnung (Tabelle: unbekannt, gelöscht, nicht erfasst, abgeglichen, nicht verknüpft, wartend) und nach gescheitertem DELETE. Neue `testSequenced`-Liste „Buchung erfassen: Rückgängig, Schreib-Sperre“ mit `commandWaitsForMirror` (kein DELETE, kein `EntryDiscardedInBb` während des Laufs, danach genau eins) und `SyncMirrorTrigger(2000)` unter `serialized`.
- `src/Tests/LedgerTransferTests.fs`: neue Liste „Übertrag in BB: Schreib-Sperre“ für `setEntryTransfer` (kein `EntryTransferTargetSetInBb`, kein PUT während des Laufs, danach je genau eins).
- `src/Tests/LedgerEntryDeletionTests.fs`: in „Buchung löschen: Schreib-Sperre“ je ein `SyncMirrorTrigger`-Test für `deleteEntry` und `restoreDeletedEntry`; in den Befehlen „wartende Buchung lässt sich nicht wiederherstellen“ (kein POST, kein Ereignis, kein Auslöser).
- `src/Tests/LedgerDuplicateDecisionTests.fs`, „Wartende Buchung entscheiden: Schreib-Sperre“: `SyncMirrorTrigger`-Test für `confirmEntryIsNew`; die Zählung „genau einmal“ im bestehenden Test bleibt.

Gegenproben (jeweils Code geändert, gebaut, Liste rot, danach zurückgesetzt):
- Wartesperre-Zweige in beiden Befehlen entfernt: „wartende Buchung lässt sich nicht verwerfen“ und „nicht wiederherstellen“ rot (vorher `discardNotCapturedText` bzw. `entryRestoreNotDeletedInBbText`).
- Reconciled-Prüfung in `discardCapturedBooking` entfernt (`if false`): „abgeglichene erfasste Buchung“ und die Ablehnungstabelle rot.
- Auslöser von `deleteEntry` in den gesperrten Abschnitt verschoben: „Spiegel-Auslöser nach dem Löschen“ rot (`FinishedInTime = Some false`).
- `WriteGuard.exclusive` in `discardCapturedBooking` und `setEntryTransfer` durch `id` ersetzt: beide Sperre-Tests rot.

`./verify.sh`: VERIFY: PASS (dotnet 4780 grün, node 316 grün).

README-Delta: geprüft mit `npm run check:readme-delta -- --bc ledger`, ohne Befund. Die Key-commands-Bullets *ErfassteBuchungVerwerfen* und *BuchungLöschen / GelöschteBuchungWiederherstellen* haben einen kursiven Kopf und sind per replace nicht erreichbar (Prüfung 6), der Abschnitt hat kein Bullet mit fettem Kopf. Darum trägt das Delta die Wartesperre in den fetten Bullets **Erfasste Buchung verwerfen (Rückgängig)** und **Buchung löschen (im Detail)** und in der Liste unter **Unsicheres Duplikat**. Vorschlag zum Nachziehen von Hand in Key commands: bei *ErfassteBuchungVerwerfen* „Ablehnung ohne Aufruf und ohne Ereignis, als erster Zweig für eine wartende Buchung (`possibleDuplicateLockedText`, ledger-vfr4h), sonst für eine nicht in BB erfasste …“; bei *BuchungLöschen / GelöschteBuchungWiederherstellen* „Wiederherstellen: … Ablehnung ohne Aufruf, als erster Zweig für eine wartende Buchung (`possibleDuplicateLockedText`, ledger-vfr4h), sonst wenn die Buchung nicht in BB gelöscht ist.“

`pushComdirectImports` nicht angefasst (ledger-xqqnv). Kein Client-Code geändert, keine Skizze.
