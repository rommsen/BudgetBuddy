---
id: ledger-smv1s
title: "Zeilen-Sperre für Erneut senden, Rückgängig und Setzen einheitlich in update: ein Helfer für alle Start-Zweige je Buchung und Feld, Aufzählung je Sperrgruppe nach Doktrin Punkt 20, Deciding im Durchgehen an die Id gebunden"
status: done
type: bug
context: ledger
created: 2026-11-12
completed:
depends_on: [design-system-001-styleguide]
blocks: [ledger-nc708, ledger-dn4vy]
tags: [bestandsreview, frontend, tests]
related_adrs: [0086, 0093]
related_research: []
prior_art: []
---

## Why
Bestandsreview ledger 2026-11-12, Gruppe G1, Teil 1 von 2 (Bericht `.agentheim/knowledge/reviews/2026-11-12-ledger.md`).
Ein Teil der Start-Zweige sperrt in `update` (Betrag, Payee, Memo-Erneut-senden), der Rest verlässt sich auf die
gesperrte Schaltfläche. Zwei Befehle zur selben Buchung können gleichzeitig laufen, der zuletzt antwortende gewinnt,
und die Zeile zeigt den falschen Stand. Doktrin Punkt 20 verlangt solche Sperren als Aufzählung aller Reihenfolgen.

## What
Ein Helfer in `src/Client/Components/Ledger/State.fs`, etwa `whenRowIdle`, prüft für eine Buchung, ob in ihrer
Sperrgruppe ein Aufruf läuft, und führt sonst den Start aus. Alle Start-Zweige der Gruppe gehen durch ihn. Gesperrt
heißt: Modell gleich, kein Befehl, `NoOp`.

Vorbild für die Sperre: `StartUndoAmount` (State.fs:2508), `StartRetryAmount` (2541), `StartRetryMemo` (2479),
`StartUndoPayee` (2803), `StartRetryPayee` (2836), `StartRestoreDeletedEntry` (3133). Einen Helfer gibt es heute nicht.

Sperrgruppen (je Map bzw. Merker):
- Kategorie und Split (`CategoryRows`, `CategorySending`): `StartSetEntryCategory` (schon geprüft), `StartUndoCategory`
  (2114), `StartRetryCategorization` (2199), `StartSaveSplits` (schon über `splitBusy`), `StartUndoSplit` (3303).
  Übertrag-Starts mitnehmen, falls sie dieselbe Map setzen.
- Prüfvermerk (`ReviewMarkRows`, `ReviewMarkSending`, dazu `Review.Confirming`): `StartSetReviewMark` (1855, auch über
  den Schalter `reviewedSwitchMsg` View.fs:1417), `StartRetryReviewMark` (1882), `StartSwipeReviewed` (schon geprüft),
  `StartUndoReviewMark` (1989), `ReviewConfirm` („Passt, weiter“, 3907).
- Lena (`FlagRows`, `FlagSending`): `StartSetLenaFlag` (schon geprüft), `StartUndoFlag` (2302), `StartRetryFlag`
  (2333), `ReviewLena` (schon geprüft).
- Memo (`MemoRows`): `StartSaveMemo` und `StartRetryMemo` (schon geprüft), `StartUndoMemo` (2445).
- Betrag (`AmountRows`): Code schon richtig, nur die Testform.
- Noch nicht in YNAB (`PushRetryRows`, `PushSending`): `StartRetryPush` (1912).
- Duplikat-Karte im Durchgehen: `ReviewConfirmedNew` und `ReviewDiscarded` lösen `Deciding` nur, wenn es die Id der
  Antwort trägt (wie `Confirming`, 3920).

Befunde:
- src/Client/Components/Ledger/State.fs:2114 `StartUndoCategory`, `StartUndoSplit` (3303), `StartUndoFlag` (2302),
  `StartUndoReviewMark` (1989) prüfen den Sende-Zustand nicht.
- State.fs:1882 `StartRetryReviewMark`, `StartRetryPush` (1912), `StartRetryCategorization` (2199), `StartRetryFlag`
  (2333), `StartSetReviewMark` (1855) sperren nur in der View.
- State.fs:2445 `StartUndoMemo` ohne Sperre.
- State.fs:3907 „Passt, weiter“ prüft eine laufende Prüfvermerk-Aktion aus „Details“ nicht.
- State.fs:4020 `ReviewConfirmedNew`/`ReviewDiscarded` lösen `Deciding` ohne Prüfung der Id.
- src/Tests/LedgerCategoryChangeClientTests.fs:327, LedgerReviewMarkClientTests.fs:72, LedgerLenaFlagClientTests.fs:176,
  LedgerMemoClientTests.fs:149, LedgerNotInYnabClientTests.fs:59: Aufzählung „höchstens ein Aufruf je Buchung“ fehlt.
- src/Tests/LedgerAmountClientTests.fs:375: Einzelfälle der Betrags-Sperre durch Aufzählung ersetzen.

## Acceptance criteria
- [ ] Ein Helfer (Name frei, etwa `whenRowIdle`) entscheidet für alle in „What“ genannten Start-Zweige, ob gestartet
  wird. Kein Start-Zweig der Liste prüft den Sende-Zustand noch von Hand. Beleg: Code; die Aufzählung unten.
- [ ] Je Sperrgruppe (Kategorie und Split, Prüfvermerk samt `ReviewConfirm`, Lena, Memo, Betrag, Noch nicht in YNAB)
  eine Aufzählung durch das echte `update` nach Doktrin Punkt 20, Vorbild `moveWays` in
  `src/Tests/MoveMoneyClientTests.fs`. Ereignisse je Gruppe: jeder Start-Zweig der Gruppe, Antwort Ok, Antwort Error,
  für eine zweite Buchung ein Start (Isolation). Orakel: ein eigener Merker zählt offene `Run…`-Aufrufe je Buchung und
  Gruppe aus den ausgegebenen Nachrichten und den zugestellten Antworten, nicht aus `…Rows`. Prüfung: nie zwei offene
  Aufrufe derselben Gruppe für dieselbe Buchung; nach jeder Antwort ist wieder ein Start möglich. Beleg: neue Datei
  `src/Tests/LedgerRowLockWaysTests.fs` (Name frei), ein `testList` je Gruppe, eingetragen in `src/Tests/Tests.fsproj`.
- [ ] Die Aufzählung ist vor dem Fix rot für: `StartUndoCategory` neben laufendem `StartSetEntryCategory`,
  `StartUndoSplit` neben laufendem `StartSaveSplits`, `StartRetryCategorization` doppelt, `StartUndoReviewMark` und
  `StartRetryReviewMark` neben `StartSetReviewMark`, `StartSetReviewMark` doppelt, `ReviewConfirm` neben laufendem
  `StartSetReviewMark` derselben Karte und umgekehrt, `StartUndoFlag` und `StartRetryFlag` neben `StartSetLenaFlag`,
  `StartUndoMemo` neben `StartSaveMemo`, `StartRetryPush` doppelt. Beleg: Outcome der Task nennt je Gruppe den roten
  Lauf vor dem Fix.
- [ ] Gesperrter Start: Modell gleich (`Expect.equal`), keine Nachricht, `NoOp`, kein Toast. Bei `StartUndoFlag` läuft
  auch `returnToReviewCard` nicht. Beleg: die Aufzählung prüft je gesperrtem Schritt Modellgleichheit und `NoOp`.
- [ ] Die Einzelfälle der Betrags-Sperre in `src/Tests/LedgerAmountClientTests.fs` ab Zeile 375 sind durch die Gruppe
  Betrag der Aufzählung ersetzt; was sie prüften (Rückgängig und Erneut senden neben laufendem Speichern), deckt die
  Aufzählung. Beleg: Diff der Testdatei, Gruppe Betrag in der neuen Datei.
- [ ] `ReviewConfirmedNew` und `ReviewDiscarded` (Ok und Error) setzen `Deciding` nur zurück, wenn
  `Deciding = Some entryId`. Regressionstest, vor dem Fix rot: Entscheidung für Karte A läuft, Durchgehen verlassen und
  neu geöffnet, Entscheidung für Karte B gestartet, Antwort zu A kommt; danach ist `Deciding = Some B`, und ein zweites
  „Ist neu“ für B löst keinen Aufruf aus. Beleg: `src/Tests/LedgerReviewClientTests.fs`.
- [ ] Kann-Sammlung ledger-k4n2s Punkt 53 (`StartUndoMemo` ohne Memo-Sperre) geht in dieser Task auf und ist beim
  Abschluss dort als erledigt durch ledger-smv1s vermerkt. Beleg: Task-Datei ledger-k4n2s.
- [ ] `./verify.sh` endet mit `VERIFY: PASS`.

## Notes
- Entscheidung Conductor 2026-11-12, nach Empfehlung: Ein gesperrtes „Rückgängig“ aus dem Toast bleibt still, für alle
  Felder (wie heute Betrag und Payee). Kein Hinweis-Toast; der Toast ist nach dem Tipp weg (src/Client/State.fs:700).
- Kann-Sammlung ledger-k4n2s Punkt 53 geht hier auf (siehe Kriterium).
- Urteile: `StartUndo…`-Befund gilt teilweise (ein zweiter Tipp auf denselben Toast ist schon gesperrt,
  src/Client/State.fs:696 bis 704; offen ist Rückgängig neben einem anderen laufenden Aufruf derselben Buchung). Alle
  übrigen Befunde dieses Teils gelten, geprüft an HEAD 98f69c32. Kein Befund verworfen.
- Die Aufzählung läuft über die Ledger-Nachrichten `StartUndo…` direkt, nicht über `ToastActionClicked`.
- Kategorie und Split teilen `CategoryRows`. Eine Sperre für beide ist gewollt (ein Split ändert die Kategorie).
- `ReviewConfirm` setzt heute `Review.Confirming`, nicht `ReviewMarkRows`. Der Helfer liest beide Merker, oder
  `ReviewConfirm` setzt zusätzlich `ReviewMarkSending`. Der Worker wählt; das Orakel ist davon unabhängig.
- `StartUndoFlag` ruft heute erst `returnToReviewCard` und setzt dann `FlagSending`. Die Prüfung kommt davor.
- `Deciding` wird auch in `showReviewCardAt` (State.fs:1212) zurückgesetzt; der Regressionstest nimmt den Weg über
  `ReviewLeft` und `ReviewOpened`.
- Überschneidungen: ledger-ct3cx ändert `EntryCategorySet` (State.fs:2089) und den Fehlerzweig von `ReviewLena` (3954),
  ledger-ng110 ändert `SplitsSaved (Error …)` (3301) direkt über `StartUndoSplit`. Diese Zweige hier nicht anfassen.
  ledger-sjbvc (Sammellauf, Teil 2) ändert den Sammel-Abschnitt 3480 bis 3760, keine gemeinsame Funktion.
- Sichtbar ändert sich nur: ein gesperrter Tipp tut nichts.

## Outcome

Ein Helfer sperrt jetzt alle Start-Zweige je Buchung und Sperrgruppe in `update`. Im Alltag sichtbar: ein gesperrter Tipp tut nichts.

**Code** (`src/Client/Components/Ledger/State.fs`)
- `RowLock` (`CategoryLock`, `ReviewMarkLock`, `FlagLock`, `MemoLock`, `AmountLock`, `PushLock`), `rowBusy` und `whenRowIdle` stehen vor `editSplit`. `ReviewMarkLock` liest `ReviewMarkRows` und `Review.Confirming` (die Task ließ die Wahl offen). `ReviewConfirm` setzt `ReviewMarkSending` nicht zusätzlich, so blieb sein Fehlerzweig unberührt.
- Durch `whenRowIdle` laufen: `StartSetEntryCategory`, `StartUndoCategory`, `StartSetEntryTransfer`, `StartRetryCategorization`, `StartSaveSplits`, `StartUndoSplit`, `StartSetReviewMark`, `StartRetryReviewMark`, `StartSwipeReviewed`, `StartUndoReviewMark`, `ReviewConfirm`, `StartSetLenaFlag`, `StartUndoFlag` (die Prüfung kommt vor `returnToReviewCard`), `StartRetryFlag`, `StartSaveMemo`, `StartUndoMemo`, `StartRetryMemo`, `StartSaveAmount`, `StartUndoAmount`, `StartRetryAmount` und `StartRetryPush`. Keiner dieser Zweige prüft den Sende-Zustand noch von Hand. `ReviewLena` fragt `rowBusy FlagLock` (eine Zeile), weil es sonst trotz gesperrtem Start zur nächsten Karte ginge. `ReviewConfirm` wartet weiter auf eine laufende Kategorie-Änderung, jetzt über `rowBusy CategoryLock`.
- `releaseDeciding` (neben `editReview`): `ReviewConfirmedNew` und `ReviewDiscarded` (Ok und Error) lösen `Deciding` nur, wenn es `Some entryId` ist.
- Nicht angefasst: `EntryCategorySet`, der Fehlerzweig von `ReviewLena`, `SplitsSaved (Error …)`, der Sammel-Abschnitt und alles um `AccountsLoaded`, `resetEntries` und `LoadEntries` (ledger-ct3cx, ledger-ng110, ledger-sjbvc).

**Tests**
- Neu ist `src/Tests/LedgerRowLockWaysTests.fs`, eingetragen in `Tests.fsproj`. Es hat je Gruppe einen `testList` (Kategorie und Split, Prüfvermerk, Lena, Memo, Betrag, Noch nicht in YNAB). Die Ereignisse sind jeder Start-Zweig für Buchung A, ein Start für Buchung B und je offenem Aufruf seine Antwort Ok und Error. Das Orakel ist eine eigene Liste offener Aufrufe, gebildet aus den ausgegebenen `Run…`-Nachrichten und den zugestellten Antworten, also nicht aus den `…Rows`. Prüfungen:
  - nie zwei offene Aufrufe derselben Gruppe für eine Buchung;
  - ein gesperrter Start lässt das Modell gleich (`Expect.equal`), schickt keine Nachricht und gibt `NoOp` zurück;
  - nach jeder Antwort startet wieder ein Start-Zweig für die Buchung;
  - jeder Start-Zweig hat mindestens einmal gestartet.
  - Tiefe 6. Der Zustandsraum ist schon vor dieser Tiefe abgeschlossen, je Gruppe 4 bis 19 Zustände.
- `src/Tests/LedgerReviewClientTests.fs` hat einen neuen Regressionstest „die späte Antwort zu einer verlassenen Karte gibt die Entscheidung der neuen Karte nicht frei“. Er läuft für alle vier Antworten über `ReviewLeft` und `ReviewOpened`. Vor dem Fix war er rot (`expected: Some <B>, actual: <null>` bei `ReviewConfirmedNew (A, Ok PushToYnabSent)`).
- In derselben Datei ist im Orakel der bestehenden Aufzählung „Durchgehen: jede Reihenfolge bis Tiefe 6“ das Ereignis „Lena-Rückgängig“ geändert. Es stellt jetzt erst die Antwort auf „Lena fragen“ zu und dann `StartUndoFlag`, so wie es real geschieht: der Toast mit „Rückgängig“ erscheint erst nach dieser Antwort. Das alte Orakel ließ „Rückgängig“ neben dem laufenden Lena-Aufruf zu, und genau das sperrt diese Task jetzt.
- In `src/Tests/LedgerAmountClientTests.fs` sind die Einzelfälle der Betrags-Sperre entfernt: der Test „Rückgängig schickt nichts, solange für die Buchung ein Aufruf läuft“ und die Zeilen zum zweiten Tipp in „Erneut senden: Erfolg löscht den Fehlschlag …“ (Review-Fund zu ledger-fxt09). An ihrer Stelle steht ein Kommentar mit Verweis auf die Gruppe Betrag. Gegenprobe nach Doktrin Punkt 8: im fertigen Code die Sperre an `StartUndoAmount` und `StartRetryAmount` lokal entfernt, dann wird die Gruppe Betrag rot mit „zwei offene Aufrufe für eine Buchung: Betrag speichern, Betrag zurück“. Nur `StartRetryAmount` entsperrt ergibt „Betrag speichern, Betrag erneut senden“. Danach wiederhergestellt.

**Roter Lauf vor dem Fix** (Aufzählung gegen `State.fs` von HEAD, alle Verstöße gesammelt, Rohliste unter `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/20e05553-37dc-4f2f-bcee-5ef520140bf9/scratchpad/e3/ledger-smv1s/red-raw.txt`, kürzeste Pfade):
- Kategorie und Split:
  - „Kategorie setzen, Kategorie zurück“ (`StartUndoCategory` neben `StartSetEntryCategory`);
  - „Split speichern, Split zurück“ (`StartUndoSplit` neben `StartSaveSplits`);
  - „Kategorie erneut senden, Kategorie erneut senden“;
  - dazu „Übertrag setzen, Split zurück“ und „Zweite Buchung, Zweite Buchung“.
- Prüfvermerk:
  - „Prüfvermerk setzen, Prüfvermerk zurück“ und „Prüfvermerk setzen, Prüfvermerk erneut senden“;
  - „Prüfvermerk setzen, Prüfvermerk setzen“;
  - „Prüfvermerk setzen, Passt, weiter“ und „Passt, weiter, Prüfvermerk setzen“ (je zwei offene Aufrufe und geändertes Modell);
  - dazu „Wisch geprüft, Prüfvermerk zurück“.
- Lena:
  - „Lena fragen, Lena zurück“ und „Lena fragen, Lena erneut senden“;
  - „Lena auf der Karte, Lena zurück“: gesperrter Start ändert das Modell, das ist `returnToReviewCard`.
- Memo: „Memo speichern, Memo zurück“, „Memo zurück, Memo zurück“ und „Memo erneut senden, Memo zurück“.
- Noch nicht in YNAB: „Erneut senden, Erneut senden“.
- Betrag: grün vor dem Fix, der Code war schon richtig. Die Gegenprobe steht oben.

**Kann-Sammlung**: ledger-k4n2s Punkt 53 (`StartUndoMemo` ohne Memo-Sperre) geht hier auf. Der Punkt ist dort schon durchgestrichen mit „geht auf in ledger-smv1s“. Beim Integrieren bitte in „erledigt durch ledger-smv1s“ ändern, denn der Worker schreibt nicht unter `.agentheim/`.

**Skizzenvergleich**: entfällt, es gibt keine Skizze (Vorgabe des Conductors).

**Gate**: `./verify.sh` endet mit `VERIFY: PASS` (dotnet 4757 grün, node 316 grün).

Für die Kann-Sammlung:
- `StartUndoPayee`, `StartRetryPayee` und `StartSavePayee` sperren weiter von Hand und noch nicht über `whenRowIdle`. Payee war keine Gruppe dieser Task. Ein `PayeeLock` würde es einheitlich machen.
- `review.Confirming.IsNone` in `ReviewConfirm` bleibt als Sperre für die ganze Sitzung. `showReviewCardAt` setzt `Confirming` beim Kartenwechsel zurück, ein „Passt“, das noch läuft, verliert damit seinen Merker. Ein Prüfvermerk aus „Details“ derselben Buchung wäre danach nicht mehr gesperrt. Die Aufzählung erreicht das nicht, denn „Später“ ist kein Ereignis der Gruppe.
