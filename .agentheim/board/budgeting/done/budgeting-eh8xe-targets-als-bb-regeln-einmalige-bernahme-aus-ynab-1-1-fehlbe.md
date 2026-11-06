---
id: budgeting-eh8xe
title: "Targets in BB führen: einmalige Übernahme aus YNAB, Target ändern schreibt nur in BB mit Befehls-Id, Wochentag setzbar, Pause nur in BB, Rückbau des Goal-Schreibwegs"
status: done
type: feature
context: budgeting
created: 2026-11-05
completed:
depends_on: [budgeting-hyn77, budgeting-v7kfw, budgeting-dgrp7]
blocks: [budgeting-hbhzf, budgeting-e2z7v, budgeting-q4v8n, budgeting-m3x7f]
tags: [targets, uebernahme, rueckbau, fuehrung]
related_adrs: [0072, 0079, 0080, 0082, 0083, 0084]
related_research: []
prior_art: [budgeting-k8wq3, budgeting-t6p8z, budgeting-h2zfn, budgeting-p2zr6]
---

## Why
Nach ADR 0084 Punkt 6 führt BB die Targets selbst. Die Goals in YNAB werden eingefroren und nur
noch gelesen. Damit entfällt der Zwei-Schritt-Schreibweg aus ADR 0072 und 0080 mit seinen
Teilfehlern, und Targets können, was YNAB-Goals nicht können (Wochentag setzen).

Teil 1 von 2 (Teilung 2026-11-06). Teil 2 ist budgeting-hbhzf: BBs Fehlbetrag in "Targets
anwenden" und in der Anzeige, Rückbau der Parallelrechnung.

## What
- Einmalige Übernahme aus `/categories` (`goal_*`, `src/Server/YnabClient.fs`) als
  `TargetTakenOverFromYnab` je Kategorie (Regel plus rohes `ynabGoalType`) im Stream
  `budgeting:target:<categoryId>`. Nur die in Romans Budget genutzten Arten; DEBT wird nicht
  übernommen und gemeldet.
- `changeCategoryTarget` hängt nur noch `TargetSet` (vollständige Regel, `effectiveFrom` = laufender
  Monat) oder `TargetRemoved` an, unter `TargetGuard`, verglichen gegen `Shown` aus der BB-Faltung.
- Befehls-Id für Target ändern (`TargetChangeRequest.CommandId`): Prüfung unter `TargetGuard` im
  Target-Stream, eine Wiederholung mit derselben Id bekommt die Antwort aus dem vorhandenen
  `TargetSet` bzw. `TargetRemoved` und hängt nichts an (Entscheidung Roman, Verfeinerung qm5x9/dgrp7).
- Pause nur noch in BB: `PausedInYnab` und `PausedInBoth` entfallen (ADR 0079 Punkt 2).
- Rückbau: `planTargetWrite`, `sendSteps`, Port `SetTarget`, `TargetPartlyWritten`,
  `TargetChangedInYnab`. `setCategoryTarget` in `YnabClient.fs` bleibt nur, wenn budgeting-e2z7v
  ihn nutzt (heute: entfernen, e2z7v baut ihn bei Bedarf neu).
- Wochentag wöchentlicher Targets in BB setzbar.
- Vergangene Monate zeigen die damals gültige Regel.

## Acceptance criteria
- [ ] Übernahme: GIVEN Kategorien mit jeder in Romans Budget genutzten Art und eine mit DEBT, WHEN
      die Übernahme läuft, THEN steht je Kategorie mit übernehmbarem Target genau ein
      `TargetTakenOverFromYnab`, DEBT fehlt und wird gemeldet, ein zweiter Lauf schreibt nichts.
- [ ] Vergleich Übernahme gegen YNAB: Für jede übernommene Kategorie ergibt die Faltung dieselbe
      Art, denselben Betrag, Rhythmus, Datum und Tag wie die gelesenen `goal_*`-Felder.
- [ ] `changeCategoryTarget`: Anlegen, Ändern, Wechsel der Art und Entfernen hängen je genau ein
      Ereignis an und rufen YNAB nicht auf (der Port `SetTarget` existiert nicht mehr); weicht
      `Shown` von der Faltung ab, wird abgelehnt; außerhalb des laufenden Monats wird abgelehnt wie
      heute (ADR 0080 Punkt 1).
- [ ] Befehls-Id: GIVEN ein gespeichertes Target ändern mit Id X, WHEN derselbe Befehl mit Id X noch
      einmal kommt, THEN hängt er nichts an und antwortet wie beim ersten Mal; ein Befehl mit neuer
      Id und veraltetem `Shown` wird abgelehnt.
- [ ] Die Historie zeigt "Angelegt" und "50 → 60" aus der vorigen Faltung, ohne eigene Typen dafür.
- [ ] GIVEN ein Target, das im Monat M geändert wurde, WHEN ein Monat vor M gezeigt wird, THEN
      zeigt er die Regel vor der Änderung.
- [ ] Der Wochentag eines wöchentlichen Targets lässt sich setzen, und die reine Funktion
      `underfunded` aus budgeting-v7kfw rechnet mit ihm (Test der Faltung und der Funktion).
- [ ] Eine in YNAB gesetzte Pause ändert in BB nichts mehr; `PauseSource` kennt nur noch die
      BB-Pause oder entfällt.
- [ ] Die Target-Prüfung (ADR 0082 Punkt 7) nimmt die letzte Änderung aus `TargetSet` oder
      `TargetRemoved` (bzw. dem Altbestand-Typ mit geschriebenem Schritt 1);
      `TargetTakenOverFromYnab` zählt nicht als Änderung.
- [ ] Die entfernten Teile (`planTargetWrite`, `sendSteps`, `TargetPartlyWritten`,
      `TargetChangedInYnab`, `PausedInYnab`, `PausedInBoth`) sind aus Code und Tests verschwunden.
- [ ] Die Fixtures in `scripts/*.test.mjs` werden im selben Schritt an die geänderten Typen aus
      `src/Shared` angepasst.
- [ ] Die Ubiquitous language der budgeting-README hat Einträge für "Target-Übernahme" und
      "Target ändern (nur BB)".
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

## Notes
- Quelle: ADR 0084 Punkte 5, 6 und 7.
- **Nicht allein deployen.** Nach diesem Teil sind die YNAB-Goals eingefroren, YNABs
  `goal_under_funded` passt bei jedem in BB geänderten Target nicht mehr. Deploy erst zusammen mit
  budgeting-hbhzf, das "Targets anwenden" und die Anzeige auf BBs Fehlbetrag umstellt.
- Die Parallelrechnung (`underfunded_comparisons`, Speichern beim Lesen eines Monats) bleibt in
  diesem Teil unverändert. Ihr Rückbau gehört zu budgeting-hbhzf.
- Erweiterungen (etwa Auffüllen über den Monatswechsel aus budgeting-bk6tq) sind eigene Tasks
  mit neuen Ereignistypen.
- Fachsprache: "Soll", "Übernahme" und "Rückweg" bekommen spätestens hier einen Eintrag in der
  Ubiquitous language der budgeting-README (Hinweis aus der Verifikation von budgeting-zkbbd).
  "Soll" und "Übernahme" (des Budgets) stehen seit dgrp7 dort.
- Teilung 2026-11-06 (Roman: "ja" zum Vorschlag): Fehlbetrag, Toleranz und Rückbau der
  Parallelrechnung gingen nach budgeting-hbhzf.

## Geparkt nach Verifikation (2026-11-06)
- Verifier: PASS in Iteration 1 (Gate im Worktree: dotnet 4091 grün, node 311 grün), geprüft nach einem
  sauberen Merge von `main` mit budgeting-r4kqm.
- Auf Romans Entscheidung bleibt die Arbeit auf dem Zweig `aw/budgeting-eh8xe` (Worktree
  `.worktrees/budgeting-eh8xe`), bis budgeting-b9869 deployt und in Prod verglichen ist. Erst dann wird
  integriert. Das RESULT liegt in `.worktrees/.results/budgeting-eh8xe.iter-1.md`.

## Outcome

BB führt die Targets nach ADR 0084 Punkte 5 bis 7. `./verify.sh` im Worktree: `VERIFY: PASS` (dotnet 4083 grün, 13 opt-in übersprungen nur "(requires .env)"; node 311 grün). Teil 2 (BBs Fehlbetrag, Rückbau der Parallelrechnung) bleibt budgeting-hbhzf: "Targets anwenden", "fehlt" und `underfunded_comparisons` nutzen weiter YNABs `goal_under_funded`.

**Ereignisse** (`src/Server/Budgeting/Events.fs`): `TargetTakenOverFromYnab` (Kategoriename, Regel, rohes `ynabGoalType`, Actor `YnabMirror`), `TargetSet` (vollständige Regel, `effectiveFrom` "YYYY-MM") und `TargetRemoved` (`effectiveFrom`), beide `UserInBb`, Stream `budgeting:target:<categoryId>`, Codec mit Regel (Betrag in Milliunits, Rhythmus `{unit, every}`, Art als `goal_type`-Kürzel, Datum, `needsWholeAmount`, Tag). `EventTypes.targetTakenOver`, `targetChanges`, `targetRules`.

**Reine Regeln** (neu `src/Server/Budgeting/TargetRules.fs`): `planTargetTakeover` (je Kategorie mit Goal NEED, TB, TBD oder MF genau ein Ereignis, DEBT und unbekannte Art in `NotTakenOver`, geführte Kategorien nichts), `awaitsTakeover`, `ruleAt monthId` (Übernahme gilt für alle Monate, Set/Removed ab `effectiveFrom`, Altbestand ändert nichts), `withStates`, `changeKindBetween` (Angelegt, Geändert, Wechsel, Entfernt aus der vorigen Faltung), `planTargetChange` (Abweichung von `Shown` wird mit `TargetChangedMeanwhileText` abgelehnt). In `src/Shared/Budgeting.fs`: `targetAfter` (Regel nach dem Wunsch, Wochentag nur bei wöchentlich, 0 bis 6), `keptWeekday`, `frequencyOfRhythm`, `weekdays`, `weekdayText`, `targetKindOfGoalType`, `YnabCategoryGoal`.

**Shared-Typen**: `TargetDraft.NewDay`, `TargetChangeRequest.CommandId`, `TargetChangeResult` nur noch `TargetWritten`, `PauseSource` nur noch `PausedInBb`, `YnabMonthCategory.Snoozed` entfällt, `HistoryTargetChange` bekommt `DayBefore`, `DayAfter`, `SentToYnab` (eine Änderung nur in BB hat keinen Stand). Entfernt: `TargetPatch`, `FieldPatch`, `TargetHeldBackCause`, `TargetDateMissed`, `targetPartlyText`, `pauseSourceOf`, `pausedInYnab`.

**Handler** (`src/Server/Budgeting/CommandHandlers.fs`): Port `SetTarget` ersetzt durch `FetchCategoryGoals` (nur Übernahme). `takeOverTargetsWith connect fetchGoals now` unter `TargetTakeoverGuard`, ein Append über alle Target-Streams in einer Transaktion (`Persistence.appendTargetTakeoverOn`), DEBT ins Log. `targetRulesFor` / `targetRulesOf`: übernimmt, wenn eine Kategorie des gelesenen Monats wartet, dann `loadTargetRulesWith`. Eingebunden beim Lesen (`QueryHandlers.budgetMonthWith`), in `preparedMonth` (alle Befehle auf einem Monat), in `previewWith` (bekommt jetzt `now`) und in Targets prüfen (`TargetCheckPort.LoadTargets`, `liveTargetCheckPort port utcNow`). `changeTargetWith connect now currentMonth monthKey request`: Monatsprüfung, dann `TargetGuard` → Befehls-Id (`onceFor`, Antwort aus dem gespeicherten `TargetSet`/`TargetRemoved`, fremde Id `ForeignCommandText`) → `TargetTakeoverGuard` → Faltung → `planTargetChange` → höchstens ein Ereignis. Kein YNAB-Aufruf, der Endpoint braucht keine Einrichtung mehr (`Api.changeCategoryTargetWith change today now ...`). Pause: Antwort `Some PausedInBb`, `planPause` ohne YNAB-Anteil.

**Rückbau**: `planTargetWrite`, `continueAfter`, `heldBackCause`, `targetPatchIsEmpty`, `targetChangeKindOf`, `sendSteps`, `recordBestEffort`, `TargetPartlyWritten`, `TargetChangedInYnab`, `PausedInYnab`, `PausedInBoth`, `recordTargetChange*`, `setCategoryTarget*`, `buildTargetBody`, `interpretTargetResponse`, `categoryTargetDecoder`. Neu in `src/Server/YnabClient.fs`: `interpretCategoryGoalsResponse`, `getCategoryGoalsWith`, `getCategoryGoals` (teilt `fetchCategoriesText` mit `getCategories`).

**Persistenz** (`src/Server/Budgeting/Persistence.fs`): `queryLedTargets`, `queryTargetRules`/`loadTargetRulesWith`, `loadTargetStreamOn`, `recordTargetChangeOn` (mit Befehls-Id), `appendTargetTakeoverOn`. `queryTargetChanges` liefert Altbestand und Änderungen in BB (Art, vorher, nachher aus der Faltung). `queryLastTargetChanges` nimmt `TargetSet`/`TargetRemoved` oder den Altbestand mit geschriebenem Schritt 1, die Übernahme zählt nicht.

**Client**: Wochentag als `Segmented` "Mo" bis "So" im Target-Formular bei wöchentlich (`TargetWeekdayChanged`, `formChooseWeekday`, `targetWeeklyAfter`, `targetWeekdayValue`), Target ändern über `Retry.command` mit Befehls-Id (`targetChangeCmdWith`), Ablehnung "inzwischen geändert" liest den Monat neu, gelber Teilergebnis-Hinweis und YNAB-Hinweis entfallen, Pause-Knopf ohne YNAB-Satz, Zusatz "pausiert". Fixture `scripts/budget-page.test.mjs` auf `PausedInBb`.

**Tests**: `src/Tests/TargetChangeTests.fs` neu (Planung, Wochentag mit `underfunded` aus der Faltung, Faltung je Monat, Übernahme rein und als Ablauf inklusive zweitem Lauf und Vergleich der Faltung mit den `goal_*`-Feldern über den echten Decoder, Abläufe nur in BB mit Befehls-Id, Historie, Grenze, BudgetApi ohne Einrichtung). Dazu `TargetCheckQueryTests` (TargetSet/Removed, Übernahme zählt nicht), `BudgetCommandCmdTests` (gleiche Id beim Wiederholen), `TargetChangeClientTests` (Wochentag, Ablehnung liest neu), `BudgetingTests` (BBs Target, YNAB-Pause wirkt nicht). Bestehende Fakes liefern ihre Goals über `BudgetEventsFixture.goalsOf`; `TargetsApplyTests` pausiert C jetzt in BB. Der Integrationstest zum Goal-PATCH ist ersetzt durch "Die Goals aus /categories gleichen den Targets des laufenden Monats (Budget Test)".

### Für den Conductor

- **Nicht allein deployen** (Task-Notes): erst zusammen mit budgeting-hbhzf. Vor dem Deploy die Datenbank sichern, alter Code kann die drei neuen Ereignistypen nicht lesen.
- Nachtrag zu ADR 0084 vorschlagen: (a) Die Target-Übernahme läuft nicht beim Start, sondern beim ersten Lesen eines Monats, bei der Vorschau und vor jedem Befehl auf einem Monat, sobald eine Kategorie des gelesenen Monats ein übernehmbares Goal zeigt und BB ihr Target noch nicht führt (Übernahme, `TargetSet` oder `TargetRemoved` im Stream); ein YNAB-Aufruf `/categories`, danach nie wieder für diese Kategorie. Grund: der Serverstart hängt nicht von YNAB ab, und ein globales "schon übernommen" würde eine Kategorie ohne Goal beim ersten Lauf ausschließen. Folge: ein Goal, das Roman nach dem Umschalten in YNAB für eine Kategorie ohne BB-Target neu anlegt, wird beim nächsten Lesen übernommen. (b) "Übernehmbar" sind NEED, TB, TBD und MF; DEBT und unbekannte Arten stehen im Server-Log. (c) Target ändern liest YNAB nicht mehr, deshalb prüft BB nicht mehr, ob die Kategorie im Monat sichtbar ist; der Endpoint braucht keine YNAB-Einrichtung. (d) Ein Wochentag wird beim Wechsel der Art nur übernommen, wenn das Ziel wöchentlich ist; ein Tag eines anderen Rhythmus geht beim Wechsel verloren (`LosesDay`).
- Nachtrag zu ADR 0079 Punkt 2 und ADR 0080 Punkte 3 bis 6: umgesetzt wie in ihren Nachträgen vom 2026-11-05.
- Override (C): Reviews vorschlagen: `qa-milestone-reviewer` (Tests), `backend-code-quality` (src/Server), `frontend-architecture` (src/Client), `design-quality` (Wochentag-Auswahl im Target-Formular).

### Kann-Funde

- `src/Tests/BudgetTakeoverTests.fs`, "die Wartefunktion des Servers wartet wirklich" schlug in einem Vor-Lauf einmal mit 49,69 ms statt 50 ms fehl (Zeitmessung, nicht von diesem Task berührt); im Gate-Lauf grün.
- Die Vorschau kann jetzt die einmalige Target-Übernahme festhalten (sonst schreibt sie nichts). Im Alltag hat das Lesen des Monats vorher übernommen.
- Die lesende Anzeige des Targets nennt den Wochentag nicht, nur das Formular zeigt ihn als aktives Segment.
- `TargetChangeResult` hat nur noch einen Fall; ein Record wäre schlichter, hätte aber alle Fixtures und Clients gebrochen.

### Integration (2026-11-06)

- Geparkt bis zum Vergleich in Prod. Vierter Vergleich nach budgeting-pf8n7 (November 2026, Monate ab Dezember 2024 gelesen): 99 von 100 gleich, keine Abweichung, eine Kategorie "nicht gerechnet", weil ihre erste Periode vor Dezember 2024 beginnt. Roman hat das als erfüllte Toleranz angenommen.
- Merge von `main` (d3d8b77) mit budgeting-b9869 und budgeting-pf8n7. `QueryHandlers.budgetMonthWith` rechnet die Parallelrechnung mit BBs Regeln und der festgehaltenen Aktivität der Periode. Tests des entfernten YNAB-Schreibwegs bleiben entfernt, die Prüfung auf `goal_creation_month` liegt in der Target-Übernahme, die b9869- und pf8n7-Tests laufen über `FetchCategoryGoals`.
- **Anlegemonat in BBs Regel** (`Target.CreatedIn`): die Übernahme bringt `goal_creation_month` mit, der Codec hält `createdIn` optional (ein Ereignis ohne das Feld hat keinen), eine Änderung behält ihn, eine neue Regel (Anlegen, Wechsel der Art) bekommt den Monat, ab dem sie gilt (`TargetRules.planTargetChange`, neuer Parameter `month`). Gegenproben: ohne Stempel 3 rot, ohne Codec-Feld 3 rot, "immer neu" 8 rot.
- Verifier: Iteration 2 und 3 FAIL nur wegen der README (veraltete Sätze aus dem Delta von Iteration 1), Code und Tests PASS. Gate nach dem Merge: dotnet 4138 grün, 13 opt-in übersprungen, node 311 grün. Integriert ohne vierte Runde auf Romans Entscheidung, Lehre in CLAUDE.md (README-Deltas, 10d9e48).
- Nicht deployt: geht nur zusammen mit budgeting-hbhzf in Prod.
- Kann-Funde aus dem RESULT stehen in budgeting-h9c3w (der Timer-Test in infrastructure-mshaf).
