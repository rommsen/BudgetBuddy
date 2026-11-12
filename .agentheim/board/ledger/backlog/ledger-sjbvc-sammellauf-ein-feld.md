---
id: ledger-sjbvc
title: "Sammellauf der Auswahl in einem Feld statt drei Bool-Flags: alle Sammelstarts und ihr Rückgängig prüfen einen Merker, StartReviewMarkBatch und StartUndoReviewMarkBatch sperren richtig, Aufzählung über die drei Sammelaktionen nach Doktrin Punkt 20"
status: backlog
type: refactor
context: ledger
created: 2026-11-12
completed:
depends_on: [design-system-001-styleguide]
blocks: []
tags: [bestandsreview, frontend, tests]
related_adrs: []
related_research: []
prior_art: []
---

## Why
Bestandsreview ledger 2026-11-12, Gruppe G1, Teil 2 von 2 (Bericht `.agentheim/knowledge/reviews/2026-11-12-ledger.md`).
Drei Bool-Flags (`CategoryBatchRunning`, `ReviewMarkBatchRunning`, `LenaBatchRunning`) beschreiben einen Lauf, der sich
gegenseitig ausschließt. Jede Prüfung muss alle drei nennen; `StartReviewMarkBatch` vergisst einen,
`StartUndoReviewMarkBatch` prüft und setzt gar keinen. So können „Geprüft“ und „Kategorie setzen“ für die Auswahl
gleichzeitig laufen, und „Rückgängig“ nach „Geprüft“ läuft neben jedem anderen Sammellauf. `elmish-state.md` verbietet
Bool-Flags für Async-Zustand.

## What
- `src/Client/Components/Ledger/Types.fs:1374, 1384, 1389`: die drei Bool-Flags ersetzen durch
  `BatchRunning: BatchRun option` (Name frei) mit je einem Fall für Prüfvermerk, Kategorie und Lena. Die View braucht den
  Fall für `IsBusy` an der richtigen Schaltfläche (View.fs:3550, 3568, 3587, 3603).
- Alle Sammelstarts prüfen nur noch `BatchRunning = None` und setzen das Feld: `StartReviewMarkBatch` (State.fs:3484),
  `OpenBatchCategoryPicker` (3556), `StartCategoryBatch` (3566), `StartUndoCategoryBatch` (3610), `StartLenaBatch`
  (3672), `StartUndoFlagBatch` (3714), `StartUndoReviewMarkBatch` (3515). Jede Antwort löst es, auch
  `ReviewMarkBatchRestored` (Ok und Error, 3527 bis 3543).
- Leser außerhalb: `src/Client/State.fs:448` bis 450 (laufende Aktivität), `src/Tests/PwaTests.fs:254` bis 257.

Befunde:
- src/Client/Components/Ledger/State.fs:3515 `StartUndoReviewMarkBatch` setzt und prüft keine Laufsperre.
- State.fs:3484 `StartReviewMarkBatch` prüft `CategoryBatchRunning` nicht (Types.fs:1372).
- src/Client/Components/Ledger/Types.fs:1374 drei Bool-Flags für einen gegenseitig ausschließenden Sammellauf;
  `BatchRunning: BatchAction option`.
- src/Tests/LedgerReviewMarkBatchClientTests.fs:87 Aufzählung über die drei Sammelaktionen samt Rückgängig fehlt.

## Acceptance criteria
- [ ] `Model` hat statt der drei Bool-Flags ein Feld vom Typ `… option` mit einem Fall je Sammelaktion.
  `grep -rn "BatchRunning: bool" src/Client` ist leer. Beleg: Types.fs, Build.
- [ ] Jeder Sammelstart und jedes Sammel-Rückgängig startet nur bei `BatchRunning = None` und setzt das Feld; jede
  Antwort (Ok und Error, auch `ReviewMarkBatchRestored`) löst es. Beleg: Aufzählung unten.
- [ ] Aufzählung nach Doktrin Punkt 20 über die drei Sammelaktionen samt Rückgängig durch das echte `update`, Vorbild
  `moveWays` in `src/Tests/MoveMoneyClientTests.fs`. Ereignisse: `StartReviewMarkBatch`, `StartCategoryBatch`,
  `StartLenaBatch`, `StartUndoReviewMarkBatch`, `StartUndoCategoryBatch`, `StartUndoFlagBatch`,
  `OpenBatchCategoryPicker`, je zugehörige Antwort Ok und Error. Orakel: eigener Zähler offener Sammel-`Run…`-Aufrufe
  aus ausgegebenen Nachrichten und zugestellten Antworten, nicht aus `BatchRunning`. Prüfung: nie mehr als ein offener
  Sammelaufruf; der Picker öffnet nicht, solange einer offen ist; nach jeder Antwort ist wieder ein Start möglich. Beleg:
  `src/Tests/LedgerReviewMarkBatchClientTests.fs` (oder eine gemeinsame Datei für alle drei), ersetzt den Einzelfall ab
  Zeile 87.
- [ ] Die Aufzählung ist vor dem Umbau rot für: `StartReviewMarkBatch` neben laufendem `StartCategoryBatch`,
  `StartUndoReviewMarkBatch` doppelt und neben jedem anderen Sammellauf. Beleg: Outcome nennt den roten Lauf.
- [ ] Gesperrter Start: Modell gleich, keine Nachricht, `NoOp`, kein Toast. Beleg: die Aufzählung.
- [ ] Die Leiste zeigt den Ladezustand an derselben Schaltfläche wie heute (Kategorie, Lena, Geprüft) und sperrt alle
  Sammelaktionen, solange einer läuft. Beleg: bestehende Browser-Tests der Auswahlleiste in `scripts/*.test.mjs` bleiben
  grün; prüft keiner den Ladezustand, ein Expecto-Test auf die View-Ableitung (`locked`, `IsBusy`), herausgezogen als
  reine Funktion.
- [ ] `src/Client/State.fs` (laufende Aktivität) und `src/Tests/PwaTests.fs` lesen das neue Feld; je Fall ein
  `active`-Eintrag. Beleg: PwaTests.
- [ ] `./verify.sh` endet mit `VERIFY: PASS`.

## Notes
- Entscheidung Conductor 2026-11-12, nach Empfehlung: Ein gesperrtes „Rückgängig“ aus dem Toast bleibt still, auch für
  die Sammelaktionen (wie heute Betrag und Payee).
- Urteile: alle vier Befunde gelten, geprüft an HEAD 98f69c32. Der Befund zu `StartReviewMarkBatch` ist eine Folge der
  drei Flags und fällt mit einem Feld weg. Kein Befund verworfen.
- Nach „Geprüft“ für die Auswahl ist die Auswahl leer (`applyReviewMarkBatch`, State.fs:883 bis 891), die Leiste meist
  zu. Das Rückgängig kommt aus dem Toast; sein Fall von `BatchRunning` ist der Prüfvermerk-Fall.
- `ReviewMarkBatchRestored (Error …)` (3542) gibt heute nur einen Toast; mit der Sperre muss es sie lösen, sonst bleibt
  die Leiste gesperrt. Die Aufzählung findet das, weil nach Error ein Start möglich sein muss.
- Testdateien `LedgerCategoryBatchClientTests.fs` (13 Stellen) und `LedgerLenaFlagBatchClientTests.fs` (20 Stellen)
  setzen die alten Flags in Fixtures; mitziehen. Das Ledger-Model geht nicht über Fable.Remoting, Fixtures in
  `scripts/*.test.mjs` sind nicht betroffen.
- Überschneidungen: ledger-smv1s (Teil 1) ändert in State.fs die Zeilen-Zweige 1855 bis 2460, 3303 und das Durchgehen,
  keine gemeinsame Funktion. Andere Bestandsreview-Tasks berühren den Sammel-Abschnitt nicht.
- Keine Server-Änderung.
