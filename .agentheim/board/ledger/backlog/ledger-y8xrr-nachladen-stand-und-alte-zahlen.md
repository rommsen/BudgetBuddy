---
id: ledger-y8xrr
title: "Kassenbuch-Liste behält Seiten, Scrollstand und Auswahl auch nach eigenen Aktionen: Anlegen, Rückgängig nach dem Anlegen, Löschen, Wiederherstellen und „Geprüft“ für die Auswahl laden im Hintergrund nach statt neu aufzubauen (Components/Ledger/State.fs, LoadEntries an fünf Stellen)"
status: backlog
type: bug
context: ledger
created: 2026-11-19
completed:
depends_on: [design-system-001-styleguide]
blocks: [ledger-pz028]
tags: [bestandsreview, frontend, tests]
related_adrs: [0014, 0092]
related_research: []
prior_art: [ledger-ct3cx]
---

## Why
Bestandsreview ledger 2026-11-19, Gruppe G3 (Bericht `.agentheim/knowledge/reviews/2026-11-19-ledger.md`), Teil 1 von 4.
Teil 2 ist ledger-pfhdq (späte und gescheiterte Antworten bei Available, Kontoständen, Spiegel-Status), Teil 3
ledger-pz028 (Aufräumen: Antworten mit `Result`, eine Quelle fürs Quick-Add-Konto, tote Zweige, Ladezustand der
Liste), Teil 4 ledger-awf5b (Betragsfilter hält den Update-Neustart zurück).

ledger-ct3cx hat das Nachladen im Hintergrund (`RefreshEntries`) für Spiegel-Lauf, Abruf und „Rückgängig“ auf ein
Verwerfen eingeführt. Fünf Wege nach Romans eigenen Aktionen bauen die Liste weiter neu auf (`LoadEntries` →
`rebuildEntries`): Skeleton, Sprung nach oben, nachgeladene Seiten weg, Auswahl weg. Wer auf Seite 3 eine Buchung
löscht oder wiederherstellt, landet wieder oben. Das widerspricht dem Kommentar an `rebuildEntries` (State.fs:199:
Neuaufbau „nur bei geänderter Abfrage … ohne geladene Liste und beim Betreten der Seite“) und dem README-Bullet
**Nachladen im Hintergrund**. Kann-Sammlung ledger-k4n2s Punkt 76 (dritter Teil) nennt dieselben Aufrufer.

## What
Die fünf Stellen schicken `RefreshEntries` statt `LoadEntries`; `LoadBalances` bleibt daneben, wie es ist:
- `CaptureSubmitted (Ok _)` (State.fs:3510), „Buchung anlegen“; das Öffnen des Details am PC (`openDetail`) bleibt.
- `CapturedDiscarded (_, Ok ())` (3542), „Rückgängig“ im Toast nach dem Anlegen.
- `EntryDeleteFinished (_, Ok ())` (3574), „Buchung löschen“ im Detail.
- `EntryRestoreFinished (_, Ok ())` (3601), „Rückgängig“ nach dem Löschen.
- `ReviewMarkBatchDone (Ok _)` mit `reload = true` (3992), „Geprüft“ für die Auswahl bei aktivem Chip „Zu prüfen“
  unter „alle“. Die Auswahl endet dort weiter (`applyReviewMarkBatch`), es bleiben Seiten und Scrollstand.

Unverändert bleiben die Knöpfe „Erneut laden“ bzw. „Erneut versuchen“ nach einem gescheiterten Laden der Liste
(`View.fs:2906`, `4015`): sie bauen neu auf, es gibt keine Liste zu halten. `LoadEntries` bleibt als Nachricht dafür.

Entscheidungen Conductor 2026-11-19, nach Empfehlung:
- `RefreshEntries` bei `Entries = Loading` baut weiter neu auf (Kann-Sammlung Punkt 76, zweiter Teil). Nach einer
  eigenen Aktion ist das sogar nötig: die laufende Abfrage kann vor der Aktion gestartet sein und den alten Stand
  liefern. Kein Umbau.
- Die gelöschte Buchung bleibt bis zur Antwort des Nachladens in der Liste stehen (heute steht so lange ein
  Skeleton). Das Detail schließt wie heute; der Toast mit „Rückgängig“ kommt wie heute.

## Acceptance criteria
- [ ] Regressionstest, vor dem Fix rot (Expecto über das echte `update`, Liste „Kassenbuch: Nachladen nach eigenen
  Aktionen“ in `src/Tests/LedgerBackgroundRefreshTests.fs`), je für die fünf Antworten oben: GIVEN geladene Liste mit
  zwei Seiten (`NextPage = 2`), Fokus auf einer Zeile der zweiten Seite, WHEN die Antwort mit `Ok`, THEN `Entries`
  bleibt `Success` mit denselben Zeilen, `NextPage` bleibt 2, im Cmd steht `RefreshEntries` und `LoadBalances`, weder
  `LoadEntries` noch `RunLoadEntriesPage 0`. Für Anlegen, Rückgängig nach dem Anlegen, Löschen und Wiederherstellen
  zusätzlich: Auswahl-Modus an und zwei Zeilen gewählt bleiben `Selection` und `SelectionMode` unverändert.
- [ ] Intakt, Expecto über `update`: nach `ReviewMarkBatchDone (Ok _)` mit `reload = true` ist die Auswahl leer und
  der Auswahl-Modus aus (wie heute); mit `reload = false` steht kein Nachladen im Cmd. Die Antworten mit `Error`
  schicken weder `RefreshEntries` noch `LoadEntries`.
- [ ] Nach dem Nachladen ist eine gelöschte Buchung aus der Liste verschwunden und eine angelegte steht an ihrem Platz
  (Expecto über `update`: die Antwort des Nachladens ohne die gelöschte bzw. mit der neuen Buchung, gebaut mit der
  Antwort-Funktion aus ledger-ct3cx). Unter „Gelöschte zeigen“ steht die gelöschte als gelöschte Zeile.
- [ ] Läuft beim Anlegen oder Löschen gerade „Weitere laden“, wartet das Nachladen auf dessen Antwort
  (`RefreshAfterPage`). Beleg: die Aufzählung „Reihenfolgen beim Nachladen im Hintergrund“ in
  `LedgerBackgroundRefreshTests.fs` (Doktrin Punkt 20) nimmt `EntryDeleteFinished (_, Ok ())` als weiteren
  Nachladen-Start auf; das Orakel bleibt das aus ledger-ct3cx.
- [ ] Die Tests, die heute `LoadEntries` nach diesen Antworten erwarten, ziehen um (`src/Tests/LedgerCaptureClientTests.fs:399`
  und `:434` „Liste neu“, dazu jeder Fund von `grep -n "LoadEntries" src/Tests/*.fs` an diesen fünf Antworten).
- [ ] Kein Browser-Test: dass eine `Success`-Liste ohne Skeleton den Scrollstand hält, belegt schon der `[scroll]`-Test
  aus ledger-ct3cx (Doktrin Punkt 2).
- [ ] README ledger, Bullet **Nachladen im Hintergrund**: die Auslöser um Anlegen, Rückgängig nach dem Anlegen, Löschen,
  Wiederherstellen und „Geprüft“ für die Auswahl bei aktivem Chip ergänzen; Bullet **Auswahl** entsprechend
  (Anlegen, Löschen und Wiederherstellen behalten die Auswahl). Delta vor der Integration durch
  `npm run check:readme-delta`.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

## Notes
Urteile (geprüft an HEAD f98a8ce6):
- Befund 1 (State.fs:3510, 3542, 3574, 3601, 3992 schicken `LoadEntries`): bestätigt, Zeilen stimmen. `LoadEntries`
  (1700) ruft `rebuildEntries` (202), das `resetEntries` setzt (Auswahl, Seiten, Generation). Weitere Sender von
  `LoadEntries` im Client sind nur die zwei Fehlerknöpfe in `View.fs` (2906, 4015).
- Kann-Sammlung ledger-k4n2s Punkt 76: erster Teil (`presetQuickFilter` leert die Entwürfe) ist im Code erledigt
  (`presetQuickFilter` leert `SearchDraft`, `AmountMinDraft`, `AmountMaxDraft`), zweiter Teil bleibt bewusst (siehe
  Entscheidungen), dritter Teil ist diese Task. Den Punkt bei der Integration durchstreichen mit „geht auf in
  ledger-y8xrr“.

Hinweise für den Worker:
- `ReviewMarkBatchDone` setzt vor dem Nachladen Zeilen lokal (`NeedsReview = false`, `ReviewMarkWriteBackFailure`,
  `LocallyRemoved`). Das Nachladen startet danach, die Basis in `RefreshRunning` ist also schon der lokale Stand; die
  Prüfung „Einzelbefehl seit dem Start“ greift nicht fälschlich. Prüfen, dass der Server `ReviewMarkWriteBackFailure`
  in der Antwort liefert, sonst verschwände der Hinweis nach dem Nachladen.
- Löschen und Wiederherstellen setzen `DeletingEntries`/`RestoringEntries` als Zeilen-Sperre; das Nachladen ändert sie
  nicht (`src/Tests/LedgerRowLockWaysTests.fs` braucht nichts Neues).
