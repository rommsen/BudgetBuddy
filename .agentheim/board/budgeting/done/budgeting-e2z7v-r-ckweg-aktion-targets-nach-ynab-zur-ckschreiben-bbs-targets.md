---
id: budgeting-e2z7v
title: "Targets beim Speichern direkt nach YNAB schreiben (Nachtrag ADR 0084), mit Hinweis bei nicht Darstellbarem und Erneut senden"
status: done
type: feature
context: budgeting
created: 2026-11-05
completed:
depends_on: [budgeting-eh8xe]
blocks: []
tags: [etappe-2, targets, ynab-spiegel]
related_adrs: [0072, 0084, 0088, 0090]
related_research: [ynab-reihenfolge-monate-target-schreiben-2026-11-02]
prior_art: [budgeting-k8wq3]
---

## Why
Roman will Targets wie die Kategorien (ADR 0088) beim Speichern direkt nach YNAB schreiben, damit YNAB als
Fallback dieselben Ziele zeigt (Nachtrag ADR 0084 vom 2026-11-08, ersetzt Punkt 6 "Goals einfrieren" und die
Rückweg-Aktion).

## What
- **Beim Speichern:** Anlegen, Ändern und Entfernen eines Targets in BB (Tab Target, Targets prüfen, Targets
  anwenden ändert nur Zuweisungen und bleibt außen vor) schreibt nach dem BB-Ereignis das Goal per
  `PATCH .../categories/{id}`: `goal_target`, `goal_frequency` (`monthly`, `weekly`, `yearly`), `goal_target_date`,
  `goal_needs_whole_amount`. Entfernen sendet `goal_target: null`. "Jährlich mit Datum" geht in zwei Aufrufen
  (ADR 0072 Punkt 5).
- **BB führt:** Das BB-Target gilt sofort. Scheitert das Schreiben ganz oder nach dem ersten von zwei Aufrufen,
  steht am Target gelb "Nicht in YNAB" mit "Erneut senden" (ADR 0087 Nachtrag); ein späterer Erfolg löst es auf.
- **Nicht Darstellbares:** Eine reine Funktion bildet eine BB-Regel auf die YNAB-Felder ab und liefert dazu, was
  verloren geht (etwa der Wochentag, ADR 0072 Punkt 4). BB schreibt das nächste Passende und zeigt am Target "In
  YNAB ohne Wochentag" (Wortlaut je Fall).
- **Pause:** fachlich YNABs Snooze, per API nicht schreibbar (`goal_snoozed_at` nur lesend). Das Goal in YNAB bleibt
  unverändert; am pausierten Target steht "In YNAB nicht gesnoozt".
- **Einmaliger Lauf:** Nach dem Einbau bringt eine Aktion alle bestehenden BB-Targets nach YNAB (Budget ⋯, mit
  Bestätigung), mit Liste "Geschrieben", "Mit Abweichung" und "Fehlgeschlagen". Bei 429 bricht sie ab und nennt die
  offenen Kategorien.
- Ob Erfolg und Fehlschlag des Schreibens Ereignisse oder Zustand sind, folgt ADR 0083 (Push-Ergebnis zu einem
  Ereignis über Geld-Entscheidungen; Vorbild `EntryPushToYnabFailed`). Der Worker klärt das mit dem
  tactical-modeler und nennt die Wahl im Outcome.

## Acceptance criteria
- [ ] Reine Funktion: BB-Regel je Art (monatlich, wöchentlich mit und ohne Wochentag, jährlich, jährlich mit Datum,
      einmalig mit Datum, ohne Target) ergibt die YNAB-Felder und die Liste des Verlorenen; ein Test je Art.
- [ ] Handler-Test: Target speichern schreibt das BB-Ereignis und ruft danach den Port mit den Feldern; bei "jährlich
      mit Datum" zwei Aufrufe in der Reihenfolge aus ADR 0072 Punkt 5.
- [ ] Handler-Test: Port scheitert beim ersten oder zweiten Aufruf: das BB-Target ist gespeichert, der Fehlschlag
      steht mit Grund fest; "Erneut senden" schreibt erneut und löst ihn bei Erfolg auf.
- [ ] Handler-Test: Target entfernen sendet `goal_target: null`; Pausieren und Aufheben rufen den Port nicht.
- [ ] Handler-Test: der einmalige Lauf schreibt alle Targets, meldet Abweichungen und bricht bei 429 mit den offenen
      Kategorien ab; die Faltung von BBs Targets ist vor und nach dem Lauf gleich.
- [ ] Encoder-Test für den JSON-Rumpf (Beträge als JSON-Zahl in Milliunits, `Encode.int64`).
- [ ] Client-`update` unter .NET: Hinweise "Nicht in YNAB" mit "Erneut senden", "In YNAB ohne Wochentag" und "In
      YNAB nicht gesnoozt" erscheinen aus dem Zustand der Kategorie; Erneut senden ist gesperrt, solange es läuft.
- [ ] Je neuem Endpoint eine Zeile in `src/Tests/RemotingEndpointNegativeTests.fs`; ein neues Pflichtfeld in
      `src/Shared/` zieht die Fixtures in `scripts/*.test.mjs` mit.
- [ ] Live-Aufrufe gegen YNAB nur hinter `RUN_INTEGRATION_TESTS`.

## Notes
- Nachtrag ADR 0084 vom 2026-11-08 (Entscheidung), ADR 0072 (Grenzen des Goal-Schreibens: Wochentag, zwei Aufrufe,
  kein Löschen und Neuanlegen), ADR 0080 (Target-Änderungen nur im laufenden Monat), ADR 0088 (Kategorien,
  OpenAPI-Felder), ADR 0087 Nachtrag (Gelb mit "Erneut senden").
- Den alten Schreibweg (`setCategoryTarget`) gibt es nach budgeting-eh8xe nicht mehr; neu bauen, Probe
  budgeting-q7m2c und Forschung `ynab-reihenfolge-monate-target-schreiben-2026-11-02` lesen.
- Kein Bezug mehr auf die Rückweg-Aktion; der einmalige Lauf ersetzt sie.

## Verifier note (iteration 1)

REASONS:
- Check 5 (README sync): Das Delta für "Target ändern" ersetzt den Text, lässt aber zwei Aussagen stehen, die der neue Code falsch macht. (1) Der Begriff heißt weiter `**Target ändern (nur BB)**`, der Text sagt jetzt "danach geht ... nach YNAB". (2) Der Bullet endet weiter mit "Grenze (hingenommen): ohne YNAB-Aufruf prüft BB nicht mehr, ob die Kategorie im Monat sichtbar ist." `changeTargetWith` ruft jetzt nach dem Festhalten `FetchCategoryGoals` und `WriteGoal` (`sendOpenDecision`), und `writeDecision` hält "Kategorie in YNAB nicht gefunden" fest. CLAUDE.md "README-Deltas" verlangt, jeden Satz eines ersetzten Bullets am aktuellen Code zu prüfen.
- Check 5: Weitere Bullets mit der alten Regel bleiben unberührt. README Zeile 81 (**Target-Änderung**) sagt "Wie sie entsteht, sagt **Target ändern (nur BB)**" und "eine Änderung nur in BB hat keinen Stand (`SentToYnab = false`)". Jede Target-Änderung geht jetzt nach YNAB, die Historie setzt aber weiter `SentToYnab = false` (src/Server/Budgeting/Persistence.fs:624). Entweder sagt die README, dass das gewollt ist (Historie ohne Sende-Stand, der Stand steht als "Nicht in YNAB" am Target), oder der Satz ändert sich. Zeile 120 (**TargetÄndern**, Key commands) verweist ebenfalls auf "**Target ändern (nur BB)**".
- Check 5, geringeres Gewicht: Zeile 157 (Open questions, durchgestrichen) begründet mit "Gegenstandslos (2026-11-06): BB schreibt seit budgeting-eh8xe keine Goals nach YNAB". Mit dem einmaligen Lauf gilt die Vorbedingung wieder; eine datierte Notiz "seit budgeting-e2z7v schreibt BB wieder Goals ..." gehört ins Delta.
- Bestanden: `./verify.sh` VERIFY: PASS (4374 grün), Scope sauber, Encoder mit JSON-Zahl erfüllt die Absicht des Kriteriums, nur geänderte Felder senden entspricht ADR 0072 Punkt 3 bis 5, Bob-Grenzen und Secrets unberührt.

SUGGESTED_FIX: README_DELTA korrigieren: Begriff "Target ändern (nur BB)" umbenennen (im eigenen Bullet und in den Verweisen Zeile 81 und 120) oder diese Bullets ganz ersetzen; die Grenze "ohne YNAB-Aufruf ..." neu fassen oder streichen; den Satz "nur in BB hat keinen Stand (`SentToYnab = false`)" in Zeile 81 korrigieren oder erklären; datierte Notiz an der Open question Zeile 157. Danach `npm run check:readme-delta -- --bc budgeting` erneut. Am Code ist nichts zu ändern.

ITERATION_HINT: likely-fixable

## Merge-conflict note (iteration 2)

- Geschwister: budgeting-sn2sn (Kategorie deaktivieren und Reihenfolge, Commit f5501a3e) hat `BudgetCategory` um das Optionsfeld `Deactivated` erweitert, `applyDeactivation` in `QueryHandlers.budgetMonthWithLedger` eingebaut und in `src/Shared/Budgeting.fs` und `src/Server/Budgeting/Domain.fs` Regeln und Typen ergänzt.
- Neue Basis: f5501a3e6995cd05b5e5436c36b6ead27afa9e5d (`main`, mit ledger-p3vq8, budgeting-pscgb, ledger-qvjvs, budgeting-sn2sn).
- Allow-list: `src/Server/Budgeting/Domain.fs`, `src/Server/Budgeting/QueryHandlers.fs`, `src/Shared/Budgeting.fs` und 14 Testdateien (`BudgetGeneralHistoryClientTests`, `BudgetHistoryClientTests`, `BudgetHistoryTests`, `BudgetPageTests`, `BudgetingTests`, `MonthStartClientTests`, `MoveMoneyClientTests`, `MoveMoneyTests`, `TargetChangeClientTests`, `TargetCheckClientTests`, `TargetPauseClientTests`, `TargetPauseTests`, `TargetsApplyClientTests`, `TargetsApplyTests`). Meist dasselbe Muster: beide Seiten haben ein Optionsfeld an `BudgetCategory`-Fixtures gehängt (`TargetInYnab = None` gegen `Deactivated = None`).
- Rung-1-Patch: `.agentheim/salvage/budgeting-e2z7v-merge-conflict.patch`.

## Outcome

Target-Entscheidungen gehen nach dem Festhalten als Goal nach YNAB (Nachtrag ADR 0084 vom 2026-11-08). `./verify.sh` im Worktree nach dem Merge mit main: `VERIFY: PASS` (dotnet 4432 grün, 13 opt-in übersprungen "(requires .env)", node 309 grün, beide Wächter und Build grün). `npm run check:readme-delta -- --bc budgeting` mit dem Delta: Exit 0. Kein Live-Aufruf, `RUN_INTEGRATION_TESTS` nicht gesetzt; ein neuer Integrationstest kam nicht dazu (die Formen belegt die Probe budgeting-q7m2c).

**Ereignis oder Zustand (mit dem tactical-modeler geklärt):** Ereignisse im Target-Stream, `TargetWrittenToYnab {decisionEventId}` und `TargetWriteToYnabFailed {decisionEventId, reason}`, Actor `YnabMirror`, ohne Befehls-Id, nicht in `EventTypes.targetRules` (`EventTypes.targetWrites`). Die letzte Entscheidung gewinnt, kein Zurückziehen-Ereignis. Abweichend vom Vorschlag des Modelers (Diff gegen eine gefaltete "bestätigte Regel") plant BB gegen YNABs echtes Goal aus einem `GET /categories` je Senden: so stimmen Teilfehler, Lücken und eine in YNAB geänderte Form. Hingenommen: eine Änderung direkt in YNAB beobachtet BB nicht, sie bleibt bis zum nächsten Schreiben. Festgehalten in ADR 0090.

**Shared** (`src/Shared/Budgeting.fs`, `src/Shared/Api.fs`): `YnabGoalGap` (`WeekdayMissing`, `WeekdayDiffers`, `DayMissing`, `RhythmDiffers`, `KindDiffers`), `TargetYnabState`, neues optionales Feld `BudgetCategory.TargetInYnab` (Fixtures in `scripts/*.test.mjs` brauchen es nicht: ein fehlendes Optionsfeld liest Fable.Remoting als `None`), Texte `TargetNotInYnabText`, `TargetNotSnoozedText`, `TargetNotSentText`, `ynabGoalGapText`, Bericht `TargetToYnabOutcome`, `TargetToYnabLine`, `TargetsToYnabReport`. Endpoints `BudgetApi.retryTargetToYnab` und `BudgetApi.writeAllTargetsToYnab`.

**Server:** `YnabClient` (Ende der Datei, auf der PATCH-Naht der Kategorien aus budgeting-pscgb): `GoalField`, `GoalPatch`, `buildGoalBody` (`goal_target` als JSON-Zahl über `JValue`; `Encode.int64` aus dem Kriterium schriebe einen String, deshalb bewusst nicht), `patchCategoryGoalWith`, `patchCategoryGoal`. `TargetRules`: `planGoalWrite`, `goalGaps`, `latestDecision`, `openTargetWrite`, `targetYnabState`. `Events`: die zwei Ereignisse mit Codec. `Persistence`: `loadTargetStreamWithIdsOn`, `queryTargetWriteStreams`, `loadOpenTargetWritesWith`, `loadTargetWriteStreamsWith`, `recordTargetWriteOn`. `CommandHandlers`: `YnabPort.WriteGoal`, `changeTargetWith connect port …` sendet nach dem Festhalten unter derselben `TargetGuard` (`sendOpenDecision`, `writeDecision`, `sendGoalCalls`), `retryTargetWriteWith`, `writeAllTargetsWith` (ein `GET /categories`, je Kategorie unter `TargetGuard`, Abbruch bei 429, offene ohne Ereignis). `QueryHandlers.withTargetInYnab` trägt den Stand beim Lesen eines Monats ein (nach bestem Bemühen). `Api`: `livePort.WriteGoal`, `unconfiguredPort` (Target ändern ohne Einrichtung hält BBs Target und den Fehlschlag "YNAB nicht eingerichtet (Settings)" fest), Endpoints `retryTargetToYnab` und `writeAllTargetsToYnab`. Pausieren und Aufheben rufen den Port nicht.

**Client:** `TargetEdit.fs` (rein): `targetYnabHints`, `targetRetryRunning`, `startTargetRetry`, `applyTargetRetried`, `askWriteAllTargets`, `startWriteAllTargets`, `applyAllTargetsWritten`, `closeWriteAllTargets`, `targetsToYnabGroups`. `Types.fs`: `TargetsToYnabRun`, `Model.TargetYnabRetry`, `Model.TargetsToYnab`, sechs Nachrichten. `State.fs`: `targetRetryCmdWith`, `writeAllTargetsCmdWith` (Proxy hinter Lambda), danach liest der Monat still neu. `InspectorView.fs`: `targetYnabView` im Tab Target (gelb "Nicht in YNAB" mit Grund und "Erneut senden" wie der Prüfvermerk-Hinweis im Kassenbuch, Lücken und "In YNAB nicht gesnoozt" gedämpft). `View.fs`: Eintrag "Targets nach YNAB schreiben" im Menü ⋮, `Modal.confirm`, `Modal.loading`, Bericht mit "Fehlgeschlagen", "Nicht versucht", "Mit Abweichung", "Geschrieben" und der Zahl der schon gleichen.

**Tests (43 neu):** `src/Tests/TargetToYnabTests.fs` (Planung je Art: monatlich, wöchentlich mit und ohne Wochentag, jährlich, jährlich mit Datum, einmalig mit Datum, ohne Target; Planung gegen YNABs Goal; Lücken und Seitenstand; Faltung der offenen Schreibung; Codec und Actor; Rumpf mit JSON-Zahl und `null`; Abläufe mit eigener In-Memory-DB je Test: TargetSet vor dem PATCH, zwei Aufrufe in Reihenfolge, Fehlschlag im ersten und zweiten Aufruf, Erneut senden löst auf und sendet nur den fehlenden Teil, Erneut senden mit Fehler, Entfernen mit `null`, Wiederholung und unverändert senden nichts, einmaliger Lauf mit Abweichungen, Abbruch bei 429 mit offener Kategorie, Faltung vorher und nachher gleich). `src/Tests/TargetToYnabClientTests.fs` (Hinweise aus dem Zustand, Erneut senden gesperrt während es läuft, Aufzählung aller Reihenfolgen bis Tiefe 4, einmaliger Lauf, Gruppen des Berichts). `TargetPauseTests`: "Pausieren und Aufheben schreiben kein Goal nach YNAB". Je neuem Endpoint eine Zeile in `RemotingEndpointNegativeTests.fs`. Strukturwächter `findMissingApiWirings` um `WriteGoal = fun categoryId patch -> YnabClient.patchCategoryGoal …` mit Gegenbeweis "Goal schreiben ohne YNAB" (`StructureGuardTests.fs`). Bestehende Fakes tragen `WriteGoal`; `TargetChangeTests` nutzt ein ablehnendes YNAB und filtert die Schreib-Ergebnisse aus `streamTypes`.

**Live-Anschlüsse (Doktrin Punkt 3):** `livePort.WriteGoal` (Strukturwächter mit Gegenbeweis); `budgetApiWith.changeCategoryTarget` baut den Port aus den Settings oder `unconfiguredPort`; `retryTargetToYnab` und `writeAllTargetsToYnab` über `portFor` (Negativzeilen ohne Einrichtung). `liveBudgetApi` und `budgetApi` unverändert.

**Gegenprobe, ehrlich:** "kein Senden nach dem Festhalten" (`if false && recordedNew`) machte 6 Tests rot, "immer `goal_frequency`" (gleiche Form deaktiviert) 7 Tests rot; beide zurückgenommen. Die Aussage gilt nur für diese zwei Mutanten.

**Abweichungen vom Task:** `Encode.int64` aus dem Encoder-Kriterium nicht genutzt (schreibt einen String, CLAUDE.md), Beleg im Encoder-Test. Gesendet werden nicht immer alle vier Felder, sondern gegen YNABs Goal nur was abweicht (ADR 0072 Punkt 3 und seine verworfene Alternative "immer alle Felder"), sonst verlöre der einmalige Lauf den Tag der 42 Monats-Targets.

**Nachtrag ADR 0084 (vom Conductor einzutragen):** "Nachtrag 2026-11-08 (budgeting-e2z7v, umgesetzt): Erfolg und Fehlschlag des Goal-Schreibens sind Ereignisse im Target-Stream (`TargetWrittenToYnab`, `TargetWriteToYnabFailed`, Actor `YnabMirror`), die letzte Entscheidung gewinnt, kein Zurückziehen-Ereignis. Geplant wird gegen YNABs heutiges Goal (ein `GET /categories` je Senden), gesendet nur, was abweicht (ADR 0072 Punkt 3); `goal_frequency` nur bei anderer Art oder anderem Rhythmus, ein Datum dann im zweiten Aufruf. Eine Änderung direkt in YNAB beobachtet BB nicht. Einzelheiten in ADR 0090."

**Iteration 2 (Verifier-Note iteration 1, nur README):** "Target ändern (nur BB)" heißt jetzt **Target ändern**, im eigenen Bullet und in den Verweisen **Target-Änderung** und **TargetÄndern** (ganze README nach "nur BB" gesucht, keine weitere Stelle). Die Grenze am Ende von **Target ändern** ist neu gefasst: vor dem Festhalten prüft BB die Sichtbarkeit nicht, kennt YNAB die Kategorie nicht, steht "Nicht in YNAB" mit "Kategorie in YNAB nicht gefunden" am Target (`writeDecision`, `GoalCategoryMissingText`). **Wahl zu `SentToYnab`:** die Historie bleibt bewusst ohne Sende-Stand (`SentToYnab = false` in `Persistence.queryTargetChanges`), der Stand gegenüber YNAB steht als **Nicht in YNAB (Target)** am Target; keine Codeänderung, die README sagt das jetzt in **Target-Änderung**. Jeder Satz der ersetzten Bullets ist am Code geprüft; `npm run check:readme-delta -- --bc budgeting --delta -` mit dem ganzen Array: Exit 0. Kein Code geändert, `./verify.sh` erneut grün.

**Merge mit main (budgeting-sn2sn, ledger-p3vq8, budgeting-pscgb, ledger-qvjvs):** 17 Dateien mit Konfliktmarkern, alle aufgelöst, beide Absichten erhalten. `BudgetCategory` trägt beide Optionsfelder, `TargetInYnab` (diese Task) und `Deactivated` (sn2sn), in `src/Shared/Budgeting.fs`, `Domain.toBudgetCategory` setzt beide auf `None`. In den 14 Testdateien haben die Fixtures beide Felder (`… OpenWriteBack = None; TargetInYnab = None; Deactivated = None`). Nachgezogen ohne Konflikt: `src/Tests/CategoryLayoutTests.fs` (sn2sn) bekommt `TargetInYnab = None` im Fixture und `WriteGoal` in zwei Fake-Ports, `src/Tests/TargetToYnabClientTests.fs` bekommt `Deactivated = None`. **Reihenfolge im Lesepfad** (`QueryHandlers.budgetMonthWithLedger`): erst `applyDeactivation` (sn2sn), dann `withTargetInYnab`. Beide setzen nur ihr eigenes Feld, `withTargetInYnab` ist eine reine Abbildung je Kategorie und lässt `Deactivated` stehen; das Ergebnis ist in beiden Folgen gleich, so bekommt aber eine weggelassene deaktivierte Kategorie keinen Stand berechnet. Das README_DELTA prüft gegen die README auf main (mit sn2sns Änderungen an "Seite Kategorien" und "Deaktiviert") mit Exit 0, keiner meiner Bullets ist von sn2sn berührt.

**Von Hand nachzuziehen** (Abschnitt "Open questions" ist per replace nicht erreichbar, kein Bullet mit fettem Kopf; die Prüfung meldet "Prüfung 6 (Anker-Erreichbarkeit)"): das Bullet, das mit "- ~~Monatlich mit `goal_day` (budgeting-k8wq3)" beginnt, bekommt diesen vollen Wortlaut:

- ~~Monatlich mit `goal_day` (budgeting-k8wq3): der Integrationstest "Ein Monats-Target mit Tag behält Tag, Kadenz und Datum" (Budget Test, hinter `RUN_INTEGRATION_TESTS`) belegt die Form und beobachtet, ob YNAB `goal_under_funded` des Vormonats neu rechnet. Ein grüner Lauf mit der Ausgabe zum Vormonat steht aus und wird vor dem ersten Target-Schreiben in Romans Budget vermerkt (F5); rechnet YNAB den Vormonat neu, entscheidet Roman, wie BB alte Monate zeigt. Weiter unbelegt: alle 3 Monate mit Datum und MF.~~ Gegenstandslos (2026-11-06): BB schreibt seit budgeting-eh8xe keine Goals nach YNAB, und seit budgeting-hbhzf ist der Fehlbetrag BBs eigener. 2026-11-08: seit budgeting-e2z7v schreibt BB wieder Goals nach YNAB (beim Speichern, bei "Erneut senden" und im einmaligen Lauf); bei gleicher Art und gleichem Rhythmus geht nur `goal_target` (und geänderte Felder ohne `goal_frequency`), der Tag eines Monats-Targets bleibt wie im Nachtrag 2026-11-05 zu ADR 0072 belegt.

Die erledigte Open question "Wer führt die Targets" (Goals eingefroren) bleibt als Historie stehen.

**Vor dem ersten Lauf in Romans Budget:** Der einmalige Lauf schreibt in die echte YNAB-Datenbank. Erst nach dem Deploy und mit Romans Bestätigung im Dialog; vorher hilft ein Lauf im Budget Test.

**Kann-Sammlung budgeting:**
- Ein vergangener Monat vergleicht die Lücken mit der damals gültigen Regel gegen YNABs heutiges Goal; kann dort eine falsche Lücke zeigen (der Tab zeigt vergangene Monate nur lesend).
- Eine offene Entfernung (`TargetRemoved` ohne Ergebnis) zeigt im Tab Target "Kein Target" mit "Nicht in YNAB" darunter; ein eigener Satz dafür fehlt.
- Speichern und Erneut senden lesen alle Kategorien (`GET /categories`) für ein Goal; ein Aufruf je Kategorie wäre schlanker, braucht aber einen eigenen Decoder.
- Der Bericht des einmaligen Laufs ist nicht im Sweep `scripts/layout-states.mjs` als Zustand eingetragen.
- `[human-eye]` für Hinweis und Bericht nicht per Screenshot angesehen.

**Reviews (Override C):** `qa-milestone-reviewer` (Tests), `backend-code-quality` (src/Server), `frontend-architecture` (src/Client), `design-quality` (Hinweis im Tab Target, Dialog des Laufs).
