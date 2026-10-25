---
id: ledger-cks06
title: "Noch nicht in YNAB vollständig: Kennzeichen an der Handy-Zeile und in der PC-Statusspalte, Erneut senden in der Hinweisleiste des Details mit sofortigem Push"
status: done
type: feature
context: ledger
created: 2026-10-25
completed:
depends_on: [design-system-001]
blocks: []
tags: [frontend, backend, kassenbuch, ynab, push, quick-add, tafeln]
related_adrs: [0049, 0050, 0055]
related_research: []
prior_art: [ledger-q1hw2, ledger-qns4j, ledger-tdnpk, ledger-r777c]
---

## Why
Abgleich der Design-Tafeln (2026-10-25, Ablauf 7). Eine Buchung, die in BB angelegt, aber
noch nicht in YNAB ist (`LinkedToYnab = false`, etwa aus Quick-Add, tdnpk), zeigt das nur
im Detail: die orange Leiste "Noch nicht in YNAB" (`NotInYnabNotice` in `View.fs`). In der
Liste fehlt das Kennzeichen an der Handy-Zeile und in der PC-Statusspalte (Tafel 7.1, 7.6,
auch 6.6); qns4j hatte es an q1hw2 verwiesen, dort wurde es nicht gebaut. Die Leiste hat
keine Aktion (`detailNoticeActionMsg` liefert `None`), ihr Satz verspricht "BB versucht es
beim nächsten Lauf erneut", ohne dass das geprüft ist. Prüfvermerk und Kategorie haben
"Erneut senden" schon (r777c, sfab0).

## What
1. **Kennzeichen in der Liste.** Eine nicht verknüpfte, nicht gelöschte Buchung trägt am Handy
   ein Kennzeichen an der Zeile und am PC in der Statusspalte das Wort "Nicht in YNAB" (Ton
   orange nach ADR 0049, Rangfolge in der Statusspalte: gelöscht vor nicht in YNAB vor den
   übrigen Zuständen).
2. **Erneut senden.** Die Leiste "Noch nicht in YNAB" bekommt "Erneut senden", wenn ein Push
   gescheitert ist oder aussteht. Der Knopf löst sofort einen Push dieser einen Buchung nach
   YNAB aus, über den bestehenden Push-Weg der Neuanlage, mit Sende-Zustand am Knopf. Erfolg:
   Leiste und Kennzeichen verschwinden, Toast. Fehlschlag: die Leiste zeigt Zeitpunkt und
   Grund des neuen Fehlschlags.
3. **Nächster Lauf.** Prüfen und per Test festhalten, dass der nächste Spiegel-Lauf eine nicht
   verknüpfte Buchung erneut pusht; stimmt das nicht, den Satz der Leiste an das tatsächliche
   Verhalten anpassen oder den Retry im Lauf ergänzen (Entscheidung im OUTCOME begründen).

## Acceptance criteria
- [ ] View-Test bzw. reine Funktion: nicht verknüpfte Buchung ergibt das Kennzeichen (Handy) und das Statuswort "Nicht in YNAB" (PC); gelöschte Buchung bleibt "gelöscht"; verknüpfte Buchung ohne Kennzeichen.
- [ ] Server-Test: der Retry-Befehl pusht genau diese Buchung; Erfolg verknüpft sie (YNAB-Id gesetzt, Fehlschlag gelöscht); Fehlschlag speichert Zeitpunkt und Grund.
- [ ] Server-Test: Retry auf eine schon verknüpfte oder gelöschte Buchung wird abgelehnt, ohne Push.
- [ ] Reducer-Test: Erneut senden setzt den Sende-Zustand, Erfolg entfernt die Leiste, Fehlschlag zeigt den neuen Grund.
- [ ] Test, der das Verhalten des nächsten Spiegel-Laufs für nicht verknüpfte Buchungen festhält, und der Satz der Leiste stimmt damit überein.
- [ ] Playwright: Buchung mit gescheitertem Push zeigt das Kennzeichen in der Liste (Handy und PC) und "Erneut senden" im Detail.
- [ ] `guard:tokens` und `guard:classes` grün.

## Notes
- Tafeln: `docs/ux/kassenbuch/tafeln/` (Ablauf 7, Screens 7.1, 7.2, 7.6, 7.7).
- Muster für den Retry: `retryReviewMarkWriteBack` (r777c), `retryCategorizationWriteBack` (sfab0), Split-Retry (ADR 0059).
- Gap-Bericht Ablauf 5 bis 7, Punkte 3 und 4; Ablauf 1 und 2, Punkt 3.

## Verifier note (iteration 1)

**Reasons:**
- README sync: der neue Kern-Befehl `Ledger.CommandHandlers.retryPushToYnab` fehlt unter `## Key commands` (die Geschwister `retryReviewMarkWriteBack` und `retryCategorizationWriteBack` stehen dort). Die OUTCOME-Begründung "nur in der Remoting-Schnittstelle, nicht im Kern" stimmt nicht.
- README sync: `retryPushToYnab` schreibt `EntryLinkedToYnab` und `EntryPushToYnabFailed` mit Actor `UserInBb`. Die README nennt für diese Ereignisse eine geschlossene Actor-Liste (Zeile ~234, ~379 bis 383 "Actor `ComdirectImport` bzw. `QuickAdd`", ~423 bis 425 Verknüpfung, ~190 Push-Fehlschlag "entsteht durch ... im Comdirect-Import-Push und in Quick Add"); kein Delta deckt das ab.
- Nebenbei: der Doc-Kommentar über `buildManualTransactionBodyWith` in `src/Server/YnabClient.fs` sagt noch "Deliberately sets no import_id".
- Alles andere bestanden (dotnet build 0 Warnungen, dotnet test 2097 grün, test:scripts 227/227, Guards grün, Kriterium 3 sauber begründet).
**Suggested fix:** README_DELTA bzw. "### README von Hand" für Key commands und die Actor-Aussagen nachliefern, OUTCOME-Satz korrigieren, Doc-Kommentar anpassen. Kein Code- oder Testeingriff nötig.
**Iteration hint:** likely-fixable

## Merge-conflict note (iteration 2)

**Sibling:** ledger-xfjcp (Lena fragen im Detail), integriert als 63d2e47.
**Neue Basis:** 40a55db (main).
**Konfliktdateien (Allow-List):** `src/Client/Components/Ledger/View.fs`, `src/Server/Api.fs`.
**Gesichert:** `.agentheim/salvage/ledger-cks06-merge-conflict.patch`.
Beide Absichten müssen erhalten bleiben: xfjcps `FlagNotice` samt Aktion und Api-Membern, cks06s Aktion an `NotInYnabNotice` und `retryPushToYnab`.

## Outcome

"Noch nicht in YNAB" ist vollständig.

1. **Kennzeichen in der Liste.** Neue reine Funktion `rowStatusMark` (View.fs) mit der Rangfolge `RowDeleted`, `RowNotInYnab`, `RowOtherStates`. Handy-Zeile: orangenes Badge "Nicht in YNAB" neben dem Gelöscht-Badge. PC-Statusspalte: eine nicht verknüpfte Buchung zeigt nur das Badge "Nicht in YNAB" (gemessen: Zelle läuft nicht über, Zeilenhöhe unverändert). Eine gelöschte Buchung behält ihre bisherigen Kennzeichen samt Symbol (der bestehende Wächter verlangt "zu prüfen" neben dem Symbol), eine verknüpfte trägt nichts.
2. **Erneut senden.** Neuer Befehl `retryPushToYnab` (Shared `LedgerApi`, `PushToYnabWriteBack = PushToYnabSent | PushToYnabFailed of reason`, Server `Ledger.CommandHandlers.retryPushToYnab`, Api-Verdrahtung über `entryPush ()` mit Token und Budget aus den Settings). Der Push geht über `YnabClient.createTransactionWith` (neu, `createManualTransaction` ruft es mit `None`), mit `import_id` der Buchung, wenn sie eine trägt. Unbekannt, gelöscht oder schon verknüpft wird abgelehnt, ohne Push. Client: `PushRetryRows`, `StartRetryPush`, `RunRetryPush`, `PushRetried`, rein getestetes `applyPushRetryResult`; die Leiste zeigt den Knopf mit Sende-Zustand, Erfolg entfernt Leiste und Kennzeichen (Toast), Fehlschlag zeigt neuen Zeitpunkt und Grund (Toast).
3. **Nächster Lauf (Entscheidung).** Der Spiegel-Lauf (`MirrorFetchers`) enthält keinen Schreibaufruf nach YNAB und pusht nie, eine nicht verknüpfte Buchung bleibt danach nicht verknüpft. Statt einen Retry im Lauf zu ergänzen (der bei Quick Add ohne import_id eine zweite YNAB-Buchung riskieren würde, ADR 0004), wurde der Satz der Leiste geändert: "Push gescheitert am ...: Grund. Erneut senden schickt die Buchung sofort." Das Verhalten ist per Test festgehalten (`LedgerRetryPushTests`).

Tests: dotnet test 2167 bestanden, 0 Fehler, 7 übersprungen (12 neue: 6 Server in LedgerRetryPushTests.fs, 6 reine Client-Tests in LedgerNotInYnabClientTests.fs, plus 3 angepasste bestehende). dotnet build 0 Warnungen. npm run build ok, guard:classes und guard:tokens grün. Playwright: scripts/*.test.mjs seriell 232 von 232 grün (darunter ledger-not-in-ynab, ledger-lena, ledger-detail). Iteration 2 änderte nur den Doc-Kommentar zu `buildManualTransactionBodyWith`.

**Auflösung des Merge-Konflikts (Iteration 3, Basis 40a55db):** In `Server/Api.fs` stehen jetzt `flagPush` (ledger-xfjcp) und `entryPush` (ledger-cks06) nebeneinander. In `View.fs` hat `detailNoticeActionMsg` beide Zweige (`FlagNotice`, dann `NotInYnabNotice`), und `detailContent` ruft `detailNoticeWithFlag` mit meinem zusätzlichen Argument `PushRetryRows` an `noticeView`. Keine Änderung an xfjcps Verhalten.

Angepasste bestehende Tests: LedgerDetailTests (Satz und Aktion der Leiste), LedgerViewTests (Quelltext-Zusicherung zur Gelöscht-Anzeige), scripts/ledger-detail.test.mjs (Satz, Knopf vorhanden), scripts/ledger-status-column.mjs (Fixture verknüpft).

### README von Hand
`retryPushToYnab` ist ein neuer Befehl im Kern (`Ledger.CommandHandlers`) und schreibt `EntryLinkedToYnab` und `EntryPushToYnabFailed` mit Actor `UserInBb`. Die Actor-Aussagen in Fließtext-Absätzen der ledger-README sind daher zu ergänzen (das README_DELTA deckt Ubiquitous language und Key commands ab). Alter und neuer Satz wörtlich:

1. Bullet **Push-Fehlschlag** (Ubiquitous language).
   Alt: `Entsteht durch `EntryPushToYnabFailed` im Comdirect-Import-Push und in Quick Add (Netz, Timeout, YNAB-Fehler, Duplikat, nicht auswertbare Antwort), aufgelöst durch `EntryLinkedToYnab`, gleich ob aus der Push-Antwort oder vom Spiegel.`
   Neu: `Entsteht durch `EntryPushToYnabFailed` im Comdirect-Import-Push, in Quick Add und beim erneuten Senden aus dem Detail (`retryPushToYnab`, Actor `UserInBb`; Netz, Timeout, YNAB-Fehler, Duplikat, nicht auswertbare Antwort), aufgelöst durch `EntryLinkedToYnab`, gleich ob aus der Push-Antwort, aus "Erneut senden" oder vom Spiegel.`

2. Ereignis-Katalog `## Ereignisse`, Zeile **EntryPushToYnabFailed** (Zeile 234).
   Alt: `Schema-Version 1, Actor `ComdirectImport` bzw. `QuickAdd`; der Zeitpunkt ist der Ereignis-Zeitstempel).`
   Neu: `Schema-Version 1, Actor `ComdirectImport`, `QuickAdd` bzw. `UserInBb` (Erneut senden, ledger-cks06); der Zeitpunkt ist der Ereignis-Zeitstempel).`

3. Abschnitt Key events, Eintrag `EntryPushToYnabFailed` (Zeilen 379 bis 383).
   Alt: `Schema-Version 1, Actor `ComdirectImport` bzw.\n  `QuickAdd`. Setzt `Entry.PushToYnabFailure` (Grund und Ereignis-Zeitstempel, ein zweiter\n  Fehlschlag ersetzt den ersten); `EntryLinkedToYnab` löst ihn auf, auch mit Actor\n  `YnabMirror`.`
   Neu: `Schema-Version 1, Actor `ComdirectImport`, `QuickAdd` bzw.\n  `UserInBb` (Erneut senden, ledger-cks06). Setzt `Entry.PushToYnabFailure` (Grund und Ereignis-Zeitstempel, ein zweiter\n  Fehlschlag ersetzt den ersten); `EntryLinkedToYnab` löst ihn auf, auch mit Actor\n  `YnabMirror` oder `UserInBb`.`

4. Absatz **Verknüpfung** (Zeilen 423 bis 425).
   Alt: `aus der Push-Antwort (Actor `ComdirectImport` bzw. `QuickAdd`) oder vom\nSpiegel über die ImportId (Actor `YnabMirror`, ADR 0021).`
   Neu: `aus der Push-Antwort (Actor `ComdirectImport` bzw. `QuickAdd`, beim erneuten Senden aus dem Detail `UserInBb`) oder vom\nSpiegel über die ImportId (Actor `YnabMirror`, ADR 0021).`
