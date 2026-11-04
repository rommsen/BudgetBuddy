---
id: budgeting-r3wq8
title: "Review-Korrekturen zu Target pausieren, Tab Historie und Target ändern: Eingabe beim Speichern bleibt, Pause-Aufrufe lesen bei Abbruch neu, ein Name je Pause-Ereignis, Absicht statt Bool, Fokus und Rollen der Balken, fehlende Tests"
status: done
type: refactor
context: budgeting
created: 2026-11-04
completed:
depends_on: [budgeting-t6p8z, budgeting-v6m3z, budgeting-k8wq3]
blocks: [budgeting-xp46a]
tags: [budget, review, target, pausieren, historie]
related_adrs: [0049, 0072, 0073, 0075, 0079, 0080]
related_research: []
prior_art: [budgeting-x4k7p, budgeting-t6p8z, budgeting-v6m3z, budgeting-k8wq3]
---

## Why
Die vier Review-Agenten (qa-milestone-reviewer, backend-code-quality, frontend-architecture, design-quality) sind am 2026-11-04 über budgeting-t6p8z (ad6a4fe), budgeting-v6m3z (6e6f000) und budgeting-k8wq3 (9ac3d6b) gelaufen. Roman hat beauftragt, die Befunde selbstständig zu beheben. Diese Task sammelt alle Befunde mit Schwere "muss" und "sollte" sowie billige "kann"-Punkte. Befunde zu den Zeilen der Historie (Stand bei Pause- und Target-Zeilen, Betrag über eine reine Funktion, Überschrift) übernimmt budgeting-h7q4v, das diese Zeilen ohnehin umbaut. Der Mehrschritt-Pfad beim Target-Schreiben (ADR 0080 Punkt 4) gehört zu budgeting-xp46a.

## What
**MUSS, Target ändern (k8wq3, Client)**
- M1: Während `TargetSaving` lassen sich Betrags- und Datumsfeld weiter bearbeiten, `Ok (TargetWritten _)` setzt das Formular per `resetForm` zurück, die nachgetippte Eingabe ist weg (`TargetEdit.fs:212-220`, `TargetEditView.fs:107-130`). Fix: beide Felder sperren, solange `targetSavingFor model category.Id` gilt. Regressionstest im Client-`update`, vor dem Fix rot.

**Pause (t6p8z)**
- P1 (Backend): `planPause` (`Domain.fs:275`) lehnt auch Aufheben ohne Target ab. Aufheben einer BB-Pause geht ohne Target, Pausieren verlangt weiter eines. `toBudgetCategory` meldet eine BB-Pause ohne Target nicht als Pause (sie wirkt nicht). Regressionstest: pausieren, Target weg, aufheben geht; Target neu, keine Pause, falls aufgehoben.
- P2 (Backend): ein Name je Ereignis. `PauseKind.Resumed`, Spaltentext "Fortgesetzt" und `HistoryEntry.PauseLifted` werden zu `PauseLifted` mit Spaltentext "Aufgehoben". Beim Lesen wird "Fortgesetzt" als Altwert weiter als `PauseLifted` erkannt (lokale Dev-Datenbanken), geschrieben wird nur "Aufgehoben". README Key events mitziehen.
- P3 (Backend und Client): `TargetPauseRequest.Paused: bool` und `SetTargetPause of paused: bool` werden zu einer Absicht als DU (`PauseIntent = PauseTarget | LiftPause`), im Request, in `planPause`, in der Nachricht und in `PauseButton`. Fixtures in `scripts/*.test.mjs` und die Negativzeile mitziehen.
- P4 (Backend): `loadPausedCategories` und `LoadPauseEvents` liefern `Result`. Ein Lesefehler oder unbekannter Art-Text wird in `fetchBudgetMonth` und `budgetMonth` zu `YnabError.InvalidResponse "Pausen nicht lesbar"` (wie `loadStates`), in der Historie zu einem Fehler mit Text. Das Zusammenführen steht außerhalb des `try` (`QueryHandlers.fs:494-513`).
- P5 (Backend, kann): Stand und Historie ordnen "jüngstes Ereignis" gleich (`Persistence.fs:397` und `:417`, beide nach `rowid`). Fehlertext in `planPause` (`Domain.fs:274`) ohne Guid und Monatsschlüssel: "Die Kategorie ist in diesem Monat nicht sichtbar (ausgeblendet oder nicht vorhanden)." Dasselbe in `CommandHandlers.fs:419` (Target ändern).
- P6 (Client): Die Pause-Antwort erkennt jedes laufende Neu-Lesen. `PauseRunning` trägt die Monatsgeneration beim Start. Bei `Ok` wird neu gelesen, wenn der Monat gezeigt wird und `MonthStale` gilt, die Generation sich geändert hat oder `Month` nicht `Success` ist, nicht während eines Monatswechsels (`Pause.fs:124`, `State.fs:300`). Test: Pause läuft, `MoveCallLost`, dann `TargetPauseAnswered Ok`, es wird neu gelesen.
- P7 (Client): Ein abgebrochener Pause-Aufruf (Exception) ist eine eigene Nachricht `TargetPauseCallLost`, zeigt die Meldung und liest den Monat still neu (Vorbild `MoveCallLost`). `Ok (Error ...)` bleibt die echte Ablehnung.
- P8 (Client, Darstellung): Bei `PausedInBoth` heißt der Knopf "Pause in BB aufheben", der Satz darüber "In YNAB ist das Target ebenfalls pausiert, diese Pause lässt sich nur dort aufheben." (`Pause.fs:38`, `MovesView.fs:280-306`). Bei reiner BB-Pause bleibt "Pause aufheben".
- P9 (Client, Darstellung): Der Pause-Knopf liegt im Modus Zuweisen in `keepsFocus` wie der Fehlt-Knopf (ADR 0005 Punkt 2, Styleguide 7.1). Ein laufender eigener Aufruf setzt kein `disabled` auf den fokussierten Knopf: eine DS-Variante in `Button.fs` (etwa `IsBusy`) setzt `aria-disabled` und `aria-busy` und zeigt den Spinner, der Fokus bleibt. Der Pause-Knopf nutzt sie.
- P10 (kann): `pauseControlView` zieht in ein eigenes `PauseView.fs` vor `MovesView.fs`. Kein leeres `class`-Attribut (`MovesView.fs:283`). Der Zweig `TargetPauseAnswered` wird ein Helfer `afterPauseAnswered`. `PauseSlotFree` entfällt im Tab Target (nur im Tab Geld nötig).

**Monatsbalken (v6m3z)**
- B1 (Client, Zugänglichkeit): Das `li` bleibt ein reines Listenelement. Ein inneres Element mit `role "img"`, `aria-label` und `tabIndex 0` trägt den Fokus (`Stats.fs:385-392`). Im `aria-label` steht ein Komma statt "·", sichtbar bleibt "·".
- B2 (Darstellung): Am gewählten Balken wird zusätzlich der Monatsname auf `--color-text-primary` gehoben, damit auch flache Balken (0, Erstattungsüberschuss) die Wahl zeigen (`styles.css:293-302`). Die drei gleichen Selektorblöcke werden mit `:is(...)` zu einer Stelle für die Vorrangregel. Der sichtbare Wert hat `pointer-events: none`. Die Wertzeile steht in `font-mono`.
- B3 (Styleguide): In `standards/frontend/styleguide.md` stehen die Absätze "Bewegungslisten" unter den beiden Ausnahmen und "Monatsbalken" am Ende von §2 oder in der Zeile Stats in §5. Die Zeile Stats nennt `monthBars`, `monthBarsChosen` (nur im Styleguide) und `monthBarsPlaceholder`.
- B4 (kann): Vorzeichenregel von `barAmountText` und `spendingText` aus einem Helfer (`Inspector.fs:267`, `:274`). Basisklasse für die Balken-`li` (`Stats.fs:357`).

**Target ändern (k8wq3)**
- T1 (Client): Der Toast für eine Antwort erscheint auch, wenn der Tab Target nicht offen ist (`targetAnswerShown` prüft zusätzlich `model.Tab = TargetTab`, `TargetEdit.fs:195-196`, `State.fs:329-334`).
- T2 (Darstellung): Ein ungültiger oder negativer Betrag zeigt einen Hinweis unter dem Feld. `invalidHint` und `amountFieldClass` aus `MovesView.fs` werden geteilt, bei negativem Betrag "Target darf nicht negativ sein".
- T3 (Darstellung): Die Fehlermeldung steht unter "Target speichern", der gelbe YNAB-Hinweis bleibt oben. Im vergangenen und künftigen Monat steht unter dem Target "Target ändern geht nur im laufenden Monat." (`TargetEditMonthText`) in `ListRoles.muted`.
- T4 (Darstellung): Reihenfolge im Tab Target: Felder, Status und Fehlbetrag, dann Speichern und Pausieren, "Target entfernen" am Ende.
- T5 (DS): Die Bestätigung "Target entfernen" am Handy wird ein DS-Baustein (etwa `BottomSheet.confirmDanger`) mit Titel "Target entfernen?", Text, Abbrechen und Entfernen, mit `role "alertdialog"`, `aria-labelledby` und Fokus auf Abbrechen beim Öffnen. Gezeigt in `/styleguide`, genannt in Styleguide §5. Am PC ist "Target entfernen" gesperrt, solange das Modal offen ist.
- T6 (Backend): Scheitert das Anhängen des Ergebnisses nach einem `Ok` von YNAB, wird das geloggt und das YNAB-Ergebnis trotzdem zurückgegeben, benannt als Best-Effort (`CommandHandlers.fs:432-445`). Ein leerer Patch läuft nicht als `InvalidResponse` ("YNAB hat abgelehnt"), BB lehnt ihn vor dem Senden mit eigenem Text ab (`YnabClient.fs:523`, `:557`).
- T7 (kann): `Modal.confirm` sagt "Abbrechen" (Aufrufer und Browser-Tests prüfen). Ein Hilfsblock "Auffüllen bis" statt zweier Kopien (`TargetEditView.fs:30-32`, `:138-140`). Hinweistext bei `TargetChangedInYnab`: "Target wurde inzwischen in YNAB geändert. Hier steht jetzt der Stand aus YNAB, deine Eingabe ist verworfen." `statusFromText` mit neutralem Fehlertext (`Persistence.fs:66-73`).

**Tests (QA)**
- Q1 (t6p8z): Die Aufzählung der Reihenfolgen in `TargetPauseClientTests.fs` trennt "Monat" und "Geladen" und sieht laufende Monatswechsel; Gegenprobe: `not (monthChanging model)` in `startPause` streichen wird rot.
- Q2 (t6p8z): Handler-Fall "Monat nicht lesbar: außen Error, keine Zeile" für `setTargetPause` (Vorlage `MoveMoneyTests.fs:690`).
- Q3 (k8wq3): `setCategoryTargetWith` prüft den gesendeten Body. Validation: 1.000.000,00 € geht durch, Encoder-Fall mit 1e9 Milliunits als JSON-Zahl.
- Q4 (k8wq3): `targetFormState` mit Target mit Datum: leeres Datum löscht es (`NewDate = None`), gleiches Datum sperrt Speichern.
- Q5 (k8wq3): Die Negativzeile `BudgetApi.changeCategoryTarget` deckt auch fehlende Einrichtung und unbekannte Kategorie ab (Vorbild Zeile `setTargetPause`).
- Q6 (k8wq3): "Zwei Kategorien laufen unabhängig" endet bei einer globalen Sperre rot statt zu hängen (Warten mit Timeout).
- Q7 (v6m3z): Die Klassenprüfung von `Money.signedListAmountClass` steht in `RuhigeListenTests.fs`, ohne Feldkopie der Font-Klassen. Der Test "Stats.monthBarAmountClass / Erstattungsüberschuss" heißt "Der Balkenbetrag ist immer text-secondary, nie Rot".
- Q8 (kann): Schwache Prüfungen schärfen (`TargetPauseTests.fs:370`, `:351-352`, `TargetChangeTests.fs:523-524` auf konkrete Meldungen), eindeutiger Monat statt "2099-01-01" (`TargetPauseTests.fs:278`), laufender Monat nicht aus `DateTime.UtcNow` in `RemotingEndpointNegativeTests.fs:381`. `YnabIntegrationTests.fs`: ein gescheitertes Zurücksetzen und ein Fehler beim Vormonat werden per `printfn` gemeldet und färben den Test nicht.

## Acceptance criteria
- [ ] M1 behoben, mit Regressionstest im Client-`update`, der vor dem Fix rot ist und den Bug im Kommentar nennt.
- [ ] P1 bis P9 umgesetzt, je mit Test auf der Heimat-Ebene (Doktrin Punkt 1). P2 liest den Altwert "Fortgesetzt", schreibt "Aufgehoben".
- [ ] B1 bis B3 umgesetzt; der `[fokus]`-Test zu den Balken bleibt grün und prüft den Fokus am inneren Element.
- [ ] T1 bis T6 umgesetzt; der neue DS-Baustein steht in `/styleguide` und in Styleguide §5.
- [ ] Q1 bis Q7 umgesetzt, Gegenproben im Outcome genannt.
- [ ] Die "kann"-Punkte P5, P10, B4, T7 und Q8 sind umgesetzt oder im Outcome mit Grund ausgelassen.
- [ ] Playwright-Fixtures in `scripts/*.test.mjs` tragen geänderte Shared-Felder.
- [ ] README budgeting: Pause-Ereignis heißt PauseAufgehoben ("Aufgehoben"), die Absicht `PauseIntent` ist beschrieben. Delta geprüft mit `npm run check:readme-delta`.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

## Notes
Review-Berichte vollständig: Scratchpad der Sitzung vom 2026-11-04 (nicht im Repo). Die Punkte oben sind daraus übernommen. Nicht übernommen: Roving-Tabindex für die Balken (bräuchte Zustand im DS-Baustein, Owner-Entscheidung), deutscher Text für Netzfehler seitenweit, englische Texte "Failed to ..." im YnabClient (eigener kleiner Task).

Parallel läuft budgeting-h7q4v. Es übernimmt die Historienzeilen (Stand bei Pause- und Target-Zeilen, Betrag über eine reine Funktion, Überschrift) und fasst `InspectorView.historyLineView` an. Diese Task fasst die Historienzeilen nicht an.

## Outcome

Alle Befunde der Pflichtpunkte und alle "kann"-Punkte sind umgesetzt. `./verify.sh` im Worktree: `VERIFY: PASS` (dotnet 3693 grün, 11 opt-in übersprungen, node 293 grün, beide Builds und beide Wächter grün). Das README-Delta ist mit `npm run check:readme-delta -- --bc budgeting` geprüft (Exit 0). Kein Live-Aufruf, `RUN_INTEGRATION_TESTS` nicht gesetzt. Die Historienzeilen (`historyEntryLine`, `historyLineView`, `historyStatusView`, `historyChangeText`, `mergeHistoryEntries`/`mergeHistoryWithTargetChanges`) sind nicht angefasst, sie gehören budgeting-h7q4v.

**MUSS, Target ändern**
- M1: `TargetEdit.targetFieldsLocked` sperrt Betrag und Datum, solange das eigene Speichern läuft. `typeTargetAmount`/`typeTargetDate` nehmen dann nichts an, die View setzt `ReadOnly` am Betragsfeld und das neue `DateField.Disabled`. Regressionstest "GIVEN Speichern läuft WHEN getippt wird THEN nehmen die Felder nichts an, danach wieder" (`src/Tests/TargetChangeClientTests.fs`), vor dem Fix rot (Gegenprobe: `typeInForm` ohne Sperre, 1 rot).

**Pause**
- P1: `Domain.planPause` bekommt die gespeicherten BB-Pausen (`fetchBudgetMonthWithPauses` in `CommandHandlers.fs`) und verlangt ein Target nur beim Pausieren. `toBudgetCategory` meldet eine BB-Pause ohne Target nicht. Regressionstest "GIVEN BB-Pause, Target danach entfernt WHEN aufheben ..." (kaputt-Szenario) und "GIVEN kein Target WHEN pausieren THEN abgelehnt" (intakt) in `src/Tests/TargetPauseTests.fs`; Gegenprobe mit der alten Target-Pflicht: rot.
- P2: `PauseKind.Resumed` heißt `PauseLifted`, geschrieben wird "Aufgehoben", `pauseKindOfText` liest "Fortgesetzt" weiter als `PauseLifted` (`LegacyPauseLiftedText`). Tests auf Domain- und Persistenzebene. ADR 0079 Punkt 1 nennt noch "Fortgesetzt"; der Stand steht im README, ein Nachtrag am ADR ist nicht nötig.
- P3: `Shared.Budgeting.PauseIntent` (`PauseTarget | LiftPause`) in `TargetPauseRequest.Intent`, `planPause`, `Msg.SetTargetPause` und `PauseButton.Intent`. Die Negativzeile und `TargetsApplyTests.fs` sind mitgezogen. Kein Playwright-Fixture trägt `TargetPauseRequest`.
- P4: `queryPausedCategories`/`loadPausedCategories` und `queryPauseEvents`/`loadPauseEvents` liefern `Result`. In `fetchBudgetMonth` und `QueryHandlers.budgetMonth` wird ein Fehler zu `YnabError.InvalidResponse "Pausen nicht lesbar"`, in der Historie zu "Pause-Ereignisse nicht lesbar". Das Zusammenführen in `categoryHistory` steht außerhalb des `try`. Test "Pausen nicht lesbar: Monat, Pausieren und Historie melden den Fehler".
- P5: Stand und Historie ordnen nach `rowid` (Test mit verstellter Uhr). `Domain.CategoryNotVisibleText` ohne Guid und Monat, auch in `changeTarget`.
- P6: `PauseRunning` trägt die Monatsgeneration. `Pause.pauseNeedsReread month startGeneration model` liest neu, wenn der Monat gewählt ist, kein Wechsel läuft und `MonthStale` gilt, die Generation sich bewegt hat oder `Month` nicht `Success` ist. Tests: Pause, `MoveCallLost`, Antwort liest neu; Generation bewegt; kein Lesen im Monatswechsel. Gegenprobe mit der alten Bedingung: 3 rot.
- P7: neue Nachricht `TargetPauseCallLost`. Meldung `pauseCallLostText` im Tab (als Toast, wenn die Kategorie nicht gezeigt wird), der Monat wird still neu gelesen. `Ok (Error ...)` bleibt die echte Ablehnung.
- P8: bei `PausedInBoth` heißt der Knopf "Pause in BB aufheben" mit `Pause.bothPauseNote`.
- P9: im Modus Zuweisen liegt der Pause-Knopf in `keepsFocus` (jetzt in `MonthView.fs`, geteilt mit dem Fehlt-Knopf). Neue DS-Variante `Button.IsBusy` über die reine Funktion `Button.buttonState`: `aria-disabled`, `aria-busy`, Spinner, kein `disabled`, ein Tipp tut nichts. Der Pause-Knopf nutzt sie für den eigenen Aufruf. Test in `src/Tests/ButtonSizeTests.fs`; der `[fokus]`-Test in `scripts/budget-money-tab.test.mjs` tippt jetzt auch den Pause-Knopf und prüft, dass der Fokus im Feld bleibt (Fixture: Kategorie 2 mit Target, Route `setTargetPause`). Gezeigt in `/styleguide` Abschnitt 3.
- P10: `src/Client/Components/Budget/PauseView.fs` (vor `MovesView.fs`) mit `PausePlacement` (`PauseInMoneyTab | PauseInTargetTab`); im Tab Target entfällt `PauseSlotFree`. Kein leeres `class`-Attribut. Helfer `afterPauseAnswered` in `State.fs`.

**Monatsbalken**
- B1: das `li` ist ein reines Listenelement, ein inneres Element mit `role "img"`, `tabIndex 0` und `aria-label` trägt den Fokus (`Stats.monthBarItem`). `Stats.monthBarAriaLabel` setzt Komma statt "·", sichtbar bleibt "·". Der `[fokus]`-Test in `scripts/budget-page.test.mjs` prüft Rolle, Namen und das `li` ohne tabindex am fokussierten Element.
- B2: `styles.css`: die Vorrangregel steht in einem `:is(...)`, die Wirkung über Variablen (Wert sichtbar, Monatsname in `--color-text-primary`, hellere Fläche). `.month-bar-value` hat `pointer-events: none`, die Wertzeile `font-mono`.
- B3: Styleguide: "Bewegungslisten" steht unter den beiden Ausnahmen, "Monatsbalken" als eigener Abschnitt am Ende von §2, die Zeile Stats in §5 nennt `monthBars`, `monthBarsChosen` und `monthBarsPlaceholder`.
- B4: `Inspector.spendingWithSign` trägt die Vorzeichenregel für `barAmountText` und `spendingText`; die Balken-`li` haben eine Basisklasse.

**Target ändern**
- T1: `TargetEdit.targetAnswerInTab` prüft zusätzlich `model.Tab = TargetTab`; eine Ablehnung bei anderem Tab kommt als Toast, die Meldung bleibt im Formular.
- T2: `TargetEdit.targetAmountHint` ("Target darf nicht negativ sein", "Keine gültige Rechnung" aus `AmountMath.InvalidCalculationText`); `amountFieldClass` und `invalidHint` liegen geteilt in `MonthView.fs`.
- T3/T4: Reihenfolge im Tab Target: gelber YNAB-Hinweis oben, Felder, Rhythmus und Art, Status und Fehlbetrag, "Target speichern" mit der Fehlermeldung darunter, Pause-Knopf, "Target entfernen" am Ende (`TargetEditView.editableTarget` bekommt Status und Pause als Elemente). Nur lesend steht "Target ändern geht nur im laufenden Monat." in `ListRoles.muted`.
- T5: `BottomSheet.confirmDanger` (`ConfirmDangerProps`): Titel, Text, Abbrechen, roter Knopf, `role "alertdialog"`, `aria-labelledby`, `aria-describedby`, Fokus auf Abbrechen beim Öffnen. Am Handy im Tab Target genutzt, in `/styleguide` Abschnitt 18 und in Styleguide §5 genannt. Am PC sperrt `removeTargetLocked` "Target entfernen", solange die Bestätigung offen ist. Neuer `[fokus]`-Test in `scripts/target-change.test.mjs`.
- T6: `recordWrittenBestEffort` hält "geschrieben" nach einem Ok von YNAB als Best-Effort fest (geloggt, YNABs Ergebnis kommt zurück). Ein leerer Patch wird vor dem Festhalten mit `Domain.EmptyTargetPatchText` abgelehnt (`targetPatchIsEmpty`). Test mit einem Trigger nur für die eigene Kategorie; Gegenprobe ohne Best-Effort: rot (Ausnahme).
- T7: `Modal.confirm` sagt "Abbrechen" (ohne Aufrufer, kein Browser-Test betroffen), ein Block `refillFact` statt zweier Kopien, neuer Hinweistext bei `TargetChangedInYnab`, `statusFromText` mit neutralem Fehlertext.

**Tests (27 neu)** und Gegenproben
- Q1: die Reihenfolgen in `TargetPauseClientTests.fs` trennen "Monat" und "Geladen", Tiefe 6. Gegenprobe: `not (monthChanging model)` in `startPause` gestrichen ergab rot ("Tipp, Monat, Geladen, Monat, Antwort, Tipp: gesendet").
- Q2: "Monat nicht lesbar: außen Error, keine Zeile" für `setTargetPause`.
- Q3: `setCategoryTargetWith` prüft den gesendeten Body, 1.000.000,00 € geht durch die Validation, Encoder-Fall 1e9 Milliunits als JSON-Zahl.
- Q4: `targetFormState` mit Datum: leer löscht (`NewDate = None`), gleiches Datum ist `FormUnchanged`.
- Q5: Negativzeile `BudgetApi.changeCategoryTarget` mit fehlender Einrichtung und unbekannter Kategorie über `budgetApiWith` mit fester Uhr.
- Q6: "Zwei Kategorien laufen unabhängig" wartet mit 10 s Zeitgrenze.
- Q7: Klassenprüfung `Money.signedListAmountClass` in `RuhigeListenTests.fs` gegen `listAmountClass 0m` statt Feldkopie; der Balkentest heißt "Der Balkenbetrag ist immer text-secondary, nie Rot".
- Q8: konkrete Meldungen statt `stringContains`/`isSome`, eigener Monat statt "2099-01-01", kein `DateTime.UtcNow` in der Negativzeile, `YnabIntegrationTests.fs` meldet gescheitertes Zurücksetzen und Vormonat-Fehler per `printfn`.
- Gegenproben je Mutante einzeln gesetzt und zurückgenommen: M1 (1 rot), P1 (1 rot), P6 (3 rot), T6 (1 rot), Q1 (rot). Die Aussage gilt nur für diese Mutanten.

**Offen [human-eye]**: Reihenfolge im Tab Target, die Rückfrage im Sheet und der hervorgehobene Monatsname am Balken sind nicht per Screenshot angesehen. Sinnvolle Reviews: `qa-milestone-reviewer`, `backend-code-quality`, `frontend-architecture`, `design-quality`.
