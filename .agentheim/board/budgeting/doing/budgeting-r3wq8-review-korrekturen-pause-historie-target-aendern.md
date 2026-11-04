---
id: budgeting-r3wq8
title: "Review-Korrekturen zu Target pausieren, Tab Historie und Target ändern: Eingabe beim Speichern bleibt, Pause-Aufrufe lesen bei Abbruch neu, ein Name je Pause-Ereignis, Absicht statt Bool, Fokus und Rollen der Balken, fehlende Tests"
status: doing
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
