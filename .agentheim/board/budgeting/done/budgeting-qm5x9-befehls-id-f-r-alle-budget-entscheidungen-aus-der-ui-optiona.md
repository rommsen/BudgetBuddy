---
id: budgeting-qm5x9
title: "Befehls-Id für Budget-Entscheidungen aus der UI: optionales CommandId-Feld, Prüfung gegen processed_commands unter der Sperre vor YNAB, Antwort aus dem Stream, Client wiederholt einmal"
status: done
type: feature
context: budgeting
created: 2026-11-05
completed:
depends_on: [budgeting-hyn77]
blocks: [budgeting-dgrp7]
tags: [idempotenz, remoting, client, fuehrung]
related_adrs: [0083, 0084]
related_research: []
prior_art: [budgeting-afd8h, budgeting-c5w2n, budgeting-r3wq8]
---

## Why
Zuweisen setzt einen absoluten Wert. Kommt eine Wiederholung verspätet an, setzte sie einen
inzwischen späteren Wert zurück. Ein wiederholtes Verschieben buchte doppelt, eine wiederholte
Pause höbe ein späteres Aufheben auf. ADR 0084 Punkt 8 gibt den UI-Entscheidungen im Budget eine
Befehls-Id (Grundlage ADR 0083 Punkt 3). Heute wiederholt der Client Schreibaufrufe nie
(`src/Client/Components/Budget/State.fs:62-119`); mit der Id darf er es einmal.

## What
Vorhanden ist: `EventStore.appendOn` (`src/Server/EventStore.fs:241-344`) prüft
`processed_commands`, liefert `CommandAlreadyProcessed firstPosition` und schreibt die Zeile nur bei
mindestens einem Ereignis. Neu zu bauen:

- **Vertrag** (`src/Shared/Budgeting.fs`): `CommandId: Guid option` in `MoveRequest` (343),
  `AssignRequest` (352), `TargetPauseRequest` (366) und in `TargetPreview` (269). `applyTargets`
  behält seine Signatur `string -> TargetPreview -> ...`: ein neuer Parametertyp bräche alte
  Clients beim Deserialisieren, ein Optionsfeld nicht. `preview` liefert `CommandId = None`,
  `previewsMatch` (`src/Server/Budgeting/Domain.fs:251`) bleibt unverändert und ignoriert das Feld.
  `retryFailedTargets` bekommt keins. `TargetChangeRequest` bekommt keins (siehe Notes).
- **Grenze** (`src/Server/Validation.fs`, `validateMoveRequest` 230, `validateAssignRequest` 245,
  `validateTargetPauseRequest`, dazu ein Weg für `applyTargets` in `Api.applyTargetsWith`):
  `Some Guid.Empty` wird mit der Meldung "Befehls-Id ist leer." abgelehnt, gesammelt mit den übrigen
  Fehlern. Eine Id, die keine Guid ist, scheitert schon beim Deserialisieren in Fable.Remoting.
- **EventStore**: `tryProcessedCommandOn conn commandId : RecordedEvent option` liest
  `processed_commands` und das Ereignis an `first_position` in einer Abfrage.
- **Persistence** (`src/Server/Budgeting/Persistence.fs`): `appendOn`, `recordDecisionOn` und
  `recordPauseOn` bekommen `commandId: Guid option` und geben `CommandAlreadyProcessed` weiter, statt
  zu werfen (heute fest `CommandId = None`, `Persistence.fs:540-550`). Ergebnis-Ereignisse
  (`recordWriteBackOn`) schreiben weiter `None`.
- **Handler** (`src/Server/Budgeting/CommandHandlers.fs`, `apply` 191, `moveMoney` 283,
  `assignMoney` 304, `setTargetPauseWith` 337): Bei `Some id` steht als Erstes unter `MonthGuard`,
  vor `FetchMonth`, `tryProcessedCommandOn`. Ist die Id bekannt, wird YNAB nicht gelesen und
  nichts angehängt, die Antwort kommt aus dem Stream:
  - Verschieben, Zuweisen: `reportWith` (121) mit der `event_id` des Entscheidungsereignisses und
    dessen Monat, `ActualToBeBudgeted = None` wie heute.
  - Targets anwenden: `Applied` mit `reportWith ... None`. Die erste Antwort trug Zu verteilen aus
    einem neuen Lesen, die Wiederholung trägt `None` (der Typ erlaubt es, "erneutes Lesen
    scheiterte").
  - Pause: `TargetPaused` → `Some PausedInBb`, `TargetPauseLifted` → `None`. Der YNAB-Anteil der
    Pause (`PausedInYnab`, `PausedInBoth`) fehlt in dieser Antwort; er entfällt mit
    budgeting-eh8xe ohnehin, der Client liest den Monat danach neu.
  - Gehört das gefundene Ereignis nicht zum Kontext `budgeting`, nicht zum Monats-Stream des
    Auftrags oder nicht zum erwarteten Typ (Verschieben: `MoneyMoved`, oder `AssignmentChangedInBb`
    bei Zu verteilen als Gegenseite; Zuweisen: `AssignmentChangedInBb`; Anwenden: `TargetsApplied`;
    Pause: `TargetPaused`/`TargetPauseLifted`), lehnt der Handler innen mit "Befehls-Id gehört zu
    einem anderen Auftrag." ab und schreibt nichts.
  - Liefert das Anhängen trotz Vorabprüfung `CommandAlreadyProcessed` (Netz der Transaktion, etwa
    dieselbe Id in zwei Monaten), baut der Handler die Antwort auf demselben Weg aus dem Stream.
- **Client** (`src/Client/Retry.fs`): `Retry.writing` neben `Retry.reading` (45): höchstens eine
  Wiederholung nach Status 0 (`withRetry isNetworkError [ 400 ]`). Dazu ein Baustein, der die Id
  einmal je Absicht zieht und an beide Versuche gibt, mit injizierter Id-Quelle
  (`newId: unit -> Guid`). Die Cmds `applyCmd`, `moveCmd`, `assignCmd`, `pauseCmd`
  (`Components/Budget/State.fs`) nutzen ihn. Jeder Eintrag der Zuweisen-Schlange ist eine eigene
  Absicht. Scheitert auch die Wiederholung, bleibt der heutige Weg (`MoveCallLost`,
  `AssignCallLost`, `TargetPauseCallLost`, `TargetsApplied (Error ...)`). Die Kommentare "nie über
  `Retry`" in `State.fs` werden angepasst.

## Acceptance criteria
Handler-Ebene (In-Memory-SQLite, Ports als Funktionswerte, Uhr als Parameter):
- [ ] GIVEN ein Zuweisen mit Id X ist festgehalten, WHEN derselbe Request mit X erneut kommt, THEN
      entsteht kein Ereignis, der Port zählt null `FetchMonth`- und null `SetAssigned`-Aufrufe, und
      die Antwort hat dieselbe `ApplicationId` und dieselben Zeilen wie die erste.
- [ ] Dasselbe für Verschieben (`MoneyMoved`) und Targets anwenden (`Applied`, Zeilen gleich,
      `ActualToBeBudgeted = None`).
- [ ] Pause: Wiederholung von Pausieren mit Id X nach einem Aufheben mit Id Y hängt nichts an,
      ruft YNAB nicht auf und antwortet `Some PausedInBb`; die Pause bleibt aufgehoben.
- [ ] GIVEN Zuweisen X auf 50 € und danach Zuweisen Y auf 60 € sind gesendet, WHEN X verspätet
      wiederholt wird, THEN geht kein PATCH hinaus und YNAB (Fake) bleibt auf 60 €.
- [ ] Ein Befehl ohne Ereignis (Zuweisen, bei dem YNAB schon auf dem Wert steht) hinterlässt keine
      Zeile in `processed_commands`; seine Wiederholung mit derselben Id wird neu ausgewertet und
      schreibt, wenn YNAB inzwischen abweicht.
- [ ] Eine Id, die zu einem Verschieben gehört, als Zuweisen gesendet: innerer `Error` "Befehls-Id
      gehört zu einem anderen Auftrag.", kein Ereignis, kein YNAB-Aufruf.
- [ ] Ergebnis-Ereignisse (`AssignmentWrittenBackToYnab`, `AssignmentWriteBackFailed`) einer
      Entscheidung mit Id haben `command_id` leer, das Entscheidungsereignis trägt sie.
- [ ] Ohne Id (`None`) verhalten sich alle vier Befehle wie heute; die bestehenden Handler-Tests
      bleiben grün, ohne geändert zu werden (außer dem neuen Feld in Literalen).

Validation-Ebene:
- [ ] `Some Guid.Empty` wird je Request mit "Befehls-Id ist leer." abgelehnt, zusammen mit den
      übrigen Fehlern desselben Requests.

Pipeline-Ebene (`src/Tests/RemotingPipelineTests.fs`, echter Host):
- [ ] Ein `assignMoney`- und ein `applyTargets`-Aufruf mit JSON ohne `CommandId` (Stand alter
      Client) werden deserialisiert und erreichen den Handler: Die Antwort ist die fachliche
      Meldung ohne YNAB-Token, kein Fehler beim Deserialisieren. (ADR 0084 Punkt 8, benannte
      Kompatibilitätsaussage.)

Client unter .NET (`src/Tests/RetryTests.fs`):
- [ ] `Retry.writing` wiederholt nach Status 0 genau einmal, nach anderem Fehler nie.
- [ ] Der Id-Baustein gibt beiden Versuchen dieselbe Id; zwei Absichten bekommen zwei Ids aus der
      injizierten Quelle.

Gate:
- [ ] Die Fixtures in `scripts/budget-money-tab.test.mjs`, `scripts/move-money.test.mjs` und
      `scripts/targets-apply.test.mjs` sind im selben Schritt um `CommandId` ergänzt, wo sie
      Requests oder `TargetPreview` nachbilden.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

## Notes
- Quelle: ADR 0084 Punkt 8, ADR 0083 Punkt 3, Nachtrag zur Verfeinerung von qm5x9 und dgrp7.
- Reihenfolge mit budgeting-dgrp7: Die Prüfung der Befehls-Id steht vor Übernahme und
  Beobachtung. Eine Wiederholung mit bekannter Id beobachtet nicht, übernimmt nicht und ruft YNAB
  nicht auf. dgrp7 baut auf diesen Handlern auf.
- Target ändern (`TargetChangeRequest`, `changeTarget` 510, unter `TargetGuard`) bekommt seine
  Befehls-Id mit budgeting-eh8xe (entschieden von Roman am 2026-11-05). Der Vergleich gegen
  `Shown` lässt eine verspätete Wiederholung schon heute ins Leere laufen (`TargetChangedInYnab`,
  nichts geschrieben), und aus `YnabGoalChangedFromBb` lässt sich kein vollständiges `Target` für
  die Antwort bauen; mit eh8xe trägt `TargetSet` die ganze Regel.
- Sinnvoll vorher, kein Blocker: infrastructure-aaryk (alter Client gegen neuen Server).

## Outcome

Budget-Entscheidungen aus der UI tragen eine Befehls-Id (ADR 0084 Punkt 8, Nachtrag zur Verfeinerung von qm5x9 und dgrp7).

- Vertrag (`src/Shared/Budgeting.fs`): `CommandId: Guid option` in `MoveRequest`, `AssignRequest`, `TargetPauseRequest` und `TargetPreview`. `preview` liefert `None` (`Domain.planTargetApplication`), `previewsMatch` bleibt unverändert. `TargetChangeRequest` und `retryFailedTargets` haben kein Feld.
- Grenze (`src/Server/Validation.fs`): `Some Guid.Empty` wird mit `EmptyCommandIdText` ("Befehls-Id ist leer.") abgelehnt, gesammelt mit den übrigen Fehlern. Für Übernehmen neu `validateTargetsApply` (Monat und Id zusammen), aufgerufen in `Api.applyTargetsWith`.
- EventStore (`src/Server/EventStore.fs`): `tryProcessedCommandOn conn commandId` liest `processed_commands` und das Ereignis an `first_position` in einer Abfrage.
- Persistence (`src/Server/Budgeting/Persistence.fs`): neuer Typ `Recorded<'T>` (`Recorded` oder `CommandAlreadyProcessed`). `recordDecisionOn`, `recordDecision`, `recordPauseOn` und `recordPause` nehmen `commandId` und geben `CommandAlreadyProcessed` weiter. `appendOn` und damit die Ergebnis-Ereignisse schreiben weiter ohne Id. `Events.EventTypes` hat dazu `paused` und `pauseLifted`.
- Handler (`src/Server/Budgeting/CommandHandlers.fs`): `onceFor` prüft die Id unter `MonthGuard` vor dem Lesen des Monats. Bei bekannter Id antworten `moveMoney` und `assignMoney` über `reportWith` mit der `event_id` der Entscheidung, `apply` mit `Applied` und `ActualToBeBudgeted = None`, `setTargetPauseWith` aus dem Ereignistyp. Passt das Ereignis nicht (Kontext, Monats-Stream, Typ; bei Verschieben mit Zu verteilen als Seite `AssignmentChangedInBb`), kommt `ForeignCommandText` als innerer Fehler, bei Targets anwenden als `RefusedByBb`. Meldet das Anhängen trotz Vorabprüfung `CommandAlreadyProcessed`, baut `answerAgain` die Antwort auf demselben Weg.
- Client (`src/Client/Retry.fs`, `src/Client/Components/Budget/State.fs`): `Retry.writing`/`writingWith` (eine Wiederholung nach Status 0, Pause 400 ms) und `Retry.command`/`commandWith` mit injizierter Id-Quelle. `applyCmd`, `moveCmd`, `assignCmd` und `pauseCmd` laufen darüber, je Cmd-Ausführung eine Id. Die reinen Funktionen (`transferRequest`, `startPause`) liefern weiter `CommandId = None`, die Id setzt erst der Cmd.

Tests: Handler-Ebene mit In-Memory-SQLite in `src/Tests/MoveMoneyTests.fs` (Liste "Befehls-Id: Verschieben und Zuweisen", auch der Fall, dass die Id während des Lesens an der Vorabprüfung vorbeirennt), `src/Tests/TargetsApplyTests.fs` und `src/Tests/TargetPauseTests.fs`; EventStore-Ebene in `src/Tests/EventStoreTests.fs`; Validation in `src/Tests/ValidationTests.fs`; Pipeline mit JSON ohne `CommandId` für `assignMoney` und `applyTargets` in `src/Tests/RemotingPipelineTests.fs`; Client unter .NET in `src/Tests/RetryTests.fs`. Hilfen `eventsMentioning`, `processedCommandRows` und `recordedOrFail` in `src/Tests/BudgetEventsFixture.fs`. Bestehende Tests sind nur um das neue Feld in Literalen und um die neue Signatur der Persistence-Aufrufe ergänzt. Browser-Fixtures: `scripts/targets-apply.test.mjs` (Vorschau mit `CommandId: null`, Übernehmen trägt eine Guid) und `scripts/budget-money-tab.test.mjs` (Zuweisen-Auftrag mit Guid); `scripts/move-money.test.mjs` bildet weder Requests noch `TargetPreview` als Record nach und blieb unverändert. `./verify.sh`: VERIFY: PASS (dotnet 3999, node 310).
