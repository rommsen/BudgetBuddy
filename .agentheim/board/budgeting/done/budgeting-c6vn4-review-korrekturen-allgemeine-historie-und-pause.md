---
id: budgeting-c6vn4
title: "Review-Korrekturen zur allgemeinen Historie und zu den Pause-Korrekturen: Suchfeld am Handy wieder breit, Ladeantwort mit Generation, Filter haftet, aufklappbare Zeile mit Chevron, Betrag rechts, BB-Lesefehler nicht als YNAB-Fehler, fehlende Tests"
status: done
type: refactor
context: budgeting
created: 2026-11-04
completed:
depends_on: [budgeting-h7q4v, budgeting-r3wq8]
blocks: [budgeting-g2ty9]
tags: [budget, review, historie, pausieren]
related_adrs: [0049, 0065, 0075, 0079]
related_research: []
prior_art: [budgeting-r3wq8, budgeting-h7q4v, budgeting-x4k7p]
---

## Why
Die vier Review-Agenten sind am 2026-11-04 über budgeting-r3wq8 (c6bd8bf) und budgeting-h7q4v (6b400ff) gelaufen. Roman hat beauftragt, die Befunde selbstständig zu beheben. Diese Task sammelt alle "muss"- und "sollte"-Befunde und die billigen "kann"-Punkte, außer denen im Target-Formular (`TargetEdit.fs`, `TargetEditView.fs`, `DateField.fs`), weil dort budgeting-xp46a gerade baut. Diese gehen nach budgeting-g2ty9 zusammen mit den Befunden zu xp46a.

## What
**MUSS**
- M1 (Darstellung, Rückschritt): Am Handy steht der Knopf Historie in der Suchzeile, das Suchfeld schrumpft bei 375 px auf etwa 70 px (`src/Client/Components/Budget/View.fs:113-121`, `MonthView.fs:340`). Der Knopf kommt aus `phoneSearch` heraus, etwa als Icon-Knopf (`Button.ghostIcon` mit `Icons.*`, `aria-label "Historie"`, 48 px) in `phoneHead` neben Zu verteilen. Am PC bei 1024 px bricht der Kopf heute zweizeilig um (`View.fs:101`); Styleguide 7.3 verlangt ab 1024 px eine Zeile. Am PC denselben Icon-Knopf oder eine kleinere Form nehmen. Styleguide 7.3 nennt den Knopf Historie.
- M2 (Test, h7q4v): `BudgetGeneralHistoryTests.fs:102` "Eine Zeile je Gruppen-Id, neueste zuerst" bekommt die Eingabe schon sortiert und bleibt ohne `List.sortByDescending` grün. Eingabe umgekehrt übergeben, und ein Durchlauf mit Bewegungen verschiedener Zeitpunkte (fängt `List.max` zu `List.min` bei `OccurredAt`).
- M3 (Test, r3wq8): `RemotingEndpointNegativeTests.fs:401` "fehlende Einrichtung" bleibt ohne `withYnabPort` grün, weil `apiWith None` denselben leeren Port nutzt. Für `apiWith None` einen `portFor` mit `failwith "ohne Einrichtung kein Port"` übergeben und die konkrete Meldung der fehlenden Einrichtung per `Expect.equal` prüfen.

**Allgemeine Historie (h7q4v)**
- A1 (Client): `GeneralHistoryLoaded` trägt eine Generation (`GeneralHistory.Generation`, hochgezählt beim Betreten und beim Wiederholen), eine Antwort mit fremder Generation wird verworfen (Vorbild `HistoryGeneration`/`EntriesGeneration` in `Inspector.fs`). Test: alte Antwort nach neuer ersetzt nichts.
- A2 (Client): "Wiederholen" ist eine eigene Nachricht `RetryGeneralHistory`, Filter und Aufklappen bleiben stehen (`HistoryView.fs:162`).
- A3 (Darstellung): Die Filter-Chips sind eine `ChipBar.barWithTestId "general-history-filter"` in eigener Zeile unter Zurück und Titel, sie haften und scrollen am Handy waagerecht (Styleguide 7.3). Chips mit `prop.key`.
- A4 (DS, Darstellung): Der aufklappbare Durchlauf Targets anwenden zeigt rechts einen Chevron in `text-muted`, der sich beim Öffnen dreht (`Transitions.fast`, aus unter `prefers-reduced-motion`), und trägt `aria-expanded` und `aria-controls`. Als DS-Variante (etwa `Button.disclosureListRow`), gezeigt im lebenden Styleguide bei den ruhigen Listen (8a) mit Durchlauf zu, offen und fehlgeschlagen.
- A5 (Shared, Darstellung): Der Durchlauf-Text trägt keinen Betrag mehr (Art, Kategorie oder Richtung, Anzahl), der Betrag steht rechts in Mono mit `tabular-nums` über dieselbe Betragsfunktion wie die Einzelzeilen.
- A6 (Shared): Der Stand eines Durchlaufs ist ein eigener Typ (etwa `RunStatus = RunWritten | RunOpen | RunChangedInYnab | RunFailed of reason | RunPartlyFailed of failed: int * total: int`), "k von n" steckt nicht mehr im Fehlgrund (`Shared/Budgeting.fs:693`). Am Handy wird der Teil-Fehlschlag kurz angezeigt ("1 von 3"), der Monat in der zweiten Zeile kurz ("Nov. 2026"), Gründe in den aufgeklappten Detailzeilen brechen um (`line-clamp-2` statt `truncate`). Keine Information nur im `title`.
- A7 (Shared): `moveText` zeigt bei fehlendem Namen beider Seiten keinen stillen Default "?", sondern "unbekannte Kategorie" (`Shared/Budgeting.fs:713-714`), mit Test.
- A8 (Backlog): `getBudgetHistory` liefert alle Bewegungen ohne Grenze. Eine Folgeaufgabe "Allgemeine Historie begrenzen oder nach Monat laden" als BACKLOG_ITEM, im Vertragskommentar steht, dass die Antwort heute unbegrenzt ist.
- A9 (kann): `historyStatusView`, `historyChangeView`, `historyFailureView` und `ynabAssignedNote` ziehen in ein kleines `HistoryRowsView.fs` vor `InspectorView.fs`; `HistoryView.fs` importiert `InspectorView` nicht mehr. `emptyText filter` und die Wahl aufklappen/zuklappen als reine Funktionen in `History.fs` mit Test. Leertext: "Noch keine Durchläufe. Targets anwenden, Verschieben oder Zuweisen erscheinen hier." `last:border-b-0` gegen die doppelte Linie am Kartenende. `changeOf` statt der Inline-Rechnung in `historyLine` (`Shared/Budgeting.fs:597`). "Zurück" prüft, ob ein Rücksprung im Browser-Verlauf (Vorbild Kassenbuch-Rückkehr) statt eines neuen Eintrags möglich ist; sonst im Outcome begründen. `/budget/history` in `KNOWN_ROUTES` in `scripts/screenshot-route.mjs`.

**Pause und Fehlerarten (r3wq8)**
- P1 (Backend): Ein BB-eigener Lesefehler läuft nicht mehr als `YnabError.InvalidResponse` ("YNAB hat abgelehnt: Pausen nicht lesbar"). Ein eigener Fehlerfall (etwa `YnabError.LocalReadFailed of string` mit Text "BB-Daten nicht lesbar: …") in `fetchBudgetMonth`, `QueryHandlers.budgetMonth` und `loadStates`, mit Test auf den Text für den Nutzer (`CommandHandlers.fs:78-79`, `QueryHandlers.fs:21-22`).
- P2 (Client): Eine echte Ablehnung beim Pausieren zu einer nicht gezeigten Kategorie zeigt einen Toast ("Pause nicht gespeichert: …", Vorbild `targetRejectedAwayText`, `State.fs:324`). Ablehnung und Abbruch zeigen den Toast auch, wenn der Pause-Knopf nicht im offenen Tab steht (`pauseAnswerInTab` analog `targetAnswerInTab`, nur Tab Target und Tab Geld im Modus Zuweisen). Der Toast-Text beim Abbruch verspricht kein Neu-Lesen, wenn keins stattfindet (`Pause.fs:146`), und hat keinen doppelten Punkt (`message.TrimEnd('.')`).
- P3 (kann): `[<RequireQualifiedAccess>]` auf `PauseKind`; `[<Literal>] PauseEventsUnreadableText` an beiden Stellen, ein Log-Helfer; `collectRows` an allen drei Stellen; `queryTargetChanges` liefert `Result` und ordnet nach `rowid` wie die Pausen; der Doc-Kommentar von `barAmountText` steht wieder an `barAmountText` (`Inspector.fs:264-272`); `Button.buttonState` nennt im Doc-Kommentar, dass IsBusy plus IsDisabled zu disabled ohne Spinner wird; `Button.IsBusy` reagiert nicht auf Hover; die Notiz zu den Monatsbalken in `/styleguide` (Abschnitt 7) nennt Komma im Label und den hervorgehobenen Monatsnamen; `PauseView.fs` Satz und Knopf mit `prop.key`.

**Tests (r3wq8, h7q4v)**
- Q1: Der Walk in `TargetPauseClientTests.fs` kennt "Neu-Lesen startet" (etwa `MoveCallLost`), "Neu-Lesen kommt an" mit dem Serverstand vom Start und "Abbruch"; Orakel: sind alle Lesevorgänge angekommen, entspricht die Pause im gezeigten Monat dem Serverstand. Gegenprobe: in `pauseNeedsReread` den Teil `|| generationMoved || notLoaded` streichen wird rot. Der von Hand gebaute Zustand in `TargetPauseClientTests.fs:162` entfällt oder entsteht durch echte Nachrichten.
- Q2: `RuhigeListenTests.fs:55` prüft wieder `has Colors.textPrimary`.
- Q3: Targets-Durchlauf mit einer einzigen fehlgeschlagenen Kategorie zeigt den Grund, nicht "1 von 1".
- Q4: `urlChangedCmd Budget -> BudgetHistory` enthält `GeneralHistoryEntered` (`BudgetGeneralHistoryClientTests.fs:169`).
- Q5 (kann): erneutes Betreten setzt Filter und Aufklappen zurück; Zuweisen mit 0 zeigt kein Vorzeichen; Konstantentests löschen (`BudgetGeneralHistoryClientTests.fs:123`, `TargetPauseTests.fs:124` erste Prüfung); `:113` ehrlich formulieren; `mergeTests` nach `BudgetHistoryTests.fs`; "mit Gegenseite" aus dem Namen `:174`; ein Aufbau je Datei; Vorbedingung `lost.Month` in `TargetPauseClientTests.fs:155`; doppelte Ebene `TargetPauseTests.fs:129`/`:361` auflösen; Labeltext im Browser-Test `budget-page.test.mjs:1337` nur vorhanden und ohne "·".

## Acceptance criteria
- [ ] M1: am Handy (375 px) ist das Suchfeld mindestens so breit wie vor budgeting-h7q4v, belegt durch einen `[layout]`-Test (Breite des Suchfelds bei 375 px); am PC bei 1024 px steht der Kopf in einer Zeile, belegt durch einen `[layout]`-Test; Styleguide 7.3 nennt den Knopf Historie.
- [ ] M2 und M3 behoben, Gegenproben im Outcome.
- [ ] A1 bis A8 umgesetzt, je mit Test auf der Heimat-Ebene (Doktrin Punkt 1); die neue DS-Variante steht in `/styleguide`.
- [ ] P1 und P2 umgesetzt, mit Tests.
- [ ] Q1 bis Q4 umgesetzt, Gegenproben im Outcome.
- [ ] Die "kann"-Punkte A9, P3 und Q5 sind umgesetzt oder im Outcome mit Grund ausgelassen.
- [ ] Playwright-Fixtures tragen geänderte Shared-Felder (etwa `RunStatus`).
- [ ] README budgeting beschreibt den Durchlauf-Stand und den BB-Lesefehler. Delta geprüft mit `npm run check:readme-delta`.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

## Notes
Review-Berichte vollständig im Scratchpad der Sitzung vom 2026-11-04 (nicht im Repo). Nicht übernommen: Roving-Tabindex für die Balken (Owner-Entscheidung), `historyChangeAmount` zurück auf `Money.signedListAmount` (H2 hat die Tupel-Form verlangt), DS-Baustein für die ruhige Listen-Karte (eigene Aufgabe, betrifft vier Seiten).

Parallel läuft budgeting-xp46a im Target-Formular (`TargetEdit.fs`, `TargetEditView.fs`, Target-Endpoints, `CommandHandlers.changeTarget`). Diese Task fasst das Target-Formular und `changeTarget` nicht an.

## Outcome

Alle MUSS- und Sollte-Befunde sind umgesetzt, die Kann-Punkte bis auf die unten genannten. `./verify.sh` im Worktree: `VERIFY: PASS` (dotnet 3762 grün, 11 opt-in übersprungen, node 298 grün, beide Builds und beide Wächter grün). README-Delta mit `npm run check:readme-delta -- --bc budgeting` geprüft (Exit 0). Kein Live-Aufruf. Target-Formular (`TargetEdit.fs`, `TargetEditView.fs`, `DateField.fs`) und `CommandHandlers.changeTarget` sind nicht angefasst.

**MUSS**
- M1: Der Knopf Historie ist ein Icon-Knopf (`Button.Ghost`, `Icons.clock` neu, `aria-label` und `title` "Historie", am Handy 48 px) in `src/Client/Components/Budget/View.fs`. Am Handy steht er im Kopf neben Zu verteilen, die Suchzeile teilt die Suche nur noch mit Targets anwenden. Am PC hält er zusammen mit `md:w-32` als Basis des Suchfelds (`MonthView.categorySearchField`, wächst weiter über `flex-1`) den Kopf bei 1024 px in einer Zeile. Neuer `[layout]`-Test "Handy 375 px: die Suche teilt ihre Zeile nur mit Targets anwenden ..." (Suche nimmt den Rest der Zeile neben Targets anwenden, also so breit wie vor h7q4v, mindestens 160 px; Historie im Kopf mit 48 px). Der bestehende `[layout]`-Test "Kopf: ab 1024 px eine Zeile ..." prüft jetzt auch Historie in der Zeile und am Ende der Reihenfolge; vor der Änderung rot (Historie bei 1024 px in Zeile 2). Styleguide 7.3 nennt den Knopf Historie und die Filterleiste der Budget-Historie.
- M2: `BudgetGeneralHistoryTests.fs` "Eine Zeile je Gruppen-Id, neueste zuerst, ein Durchlauf zählt mit seiner jüngsten Bewegung": Eingabe älteste zuerst, ein Targets-Durchlauf mit Bewegungen um 12 und 8 Uhr. Gegenprobe: `List.sortByDescending` in `historyRuns` gestrichen: rot; `List.max` zu `List.min`: rot.
- M3: `RemotingEndpointNegativeTests.fs`: `apiWith None` bekommt einen `portFor` mit `failwith "ohne Einrichtung kein Port"`, die Meldung wird per `Expect.equal` gegen "Kein YNAB-Token eingerichtet (Settings)" geprüft. Gegenprobe: in `withYnabPort` den Fall ohne Token auf den Port weitergeleitet: rot (Ausnahme).

**Allgemeine Historie**
- A1: `GeneralHistory.Generation`, hochgezählt in `enterGeneralHistory` und `retryGeneralHistory`; `GeneralHistoryLoaded of generation * Result`; fremde Generation wird verworfen (`History.applyGeneralHistoryLoaded`). Test "Eine alte Antwort nach einer neueren ersetzt nichts". Gegenprobe Generationsprüfung gestrichen: 2 rot.
- A2: Nachricht `RetryGeneralHistory` (Fehlerkarte), Filter und Aufklappen bleiben, Test.
- A3: `ChipBar.barWithTestId "general-history-filter"` in eigener Zeile unter Zurück und Titel, Chips mit `prop.key`. `[layout]`-Test in `scripts/budget-history.test.mjs`: haftet nach dem Scrollen unter der Navbar, `overflow-x: auto`.
- A4: neue DS-Variante `Button.disclosureListRow` (Chevron `Icons.chevronDown` in neuer Farbe `Icons.Muted` = `text-text-muted`, `rotate-180` mit `Transitions.fast`, `motion-reduce:transition-none`, `aria-expanded`, `aria-controls`; der Block bleibt zu `hidden` im DOM). In `/styleguide` Abschnitt 8a mit Durchlauf zu, offen und fehlgeschlagen; Styleguide §5 nennt sie. Der `[flow:budget-historie]` prüft `aria-expanded` vor und nach dem Tipp, der neue `[layout]`-Test die berechnete Farbe und die Drehung.
- A5: `HistoryRun.Text` ohne Betrag ("Verschieben, Lebensmittel → Urlaub", "Zuweisen, Urlaub", "Targets anwenden, 3 Kategorien"), neues Feld `HistoryRun.Amount`, rechts über `History.runAmount` (nutzt `historyChangeAmount`, Verschieben ohne Vorzeichen). Kein Playwright-Fixture trägt `HistoryRun` (der Vertrag liefert `HistoryMovement`), nur der Text im Flow-Test ist angepasst.
- A6: `Shared.Budgeting.RunStatus` (`RunWritten | RunOpen | RunChangedInYnab | RunFailed of reason | RunPartlyFailed of failed * total`). Abweichung von der Vorgabe "Badge plus 1 von 3 daneben": bei 375 px passen Zeit, kurzer Monat, Badge "fehlgeschlagen" und "1 von 2" nicht in die Zeile (gemessen 295 px Bedarf auf 281 px). Der Teil-Fehlschlag steht deshalb ganz im roten Badge, am Handy "1 von 3", am PC "1 von 3 fehlgeschlagen" (`History.runBadge`, `PartlyFailedBadge`). Monat am Handy "Nov. 2026" (`shortMonthYearLabel`). Gründe in den Einzelzeilen in eigener Zeile mit `line-clamp-2` (`HistoryRowsView.historyFailureWrapped`). `[layout]`-Test: "1 von 2" steht, keine Angabe der zweiten Zeile ist abgeschnitten.
- A7: `UnknownCategoryText` "unbekannte Kategorie" statt "?", Test.
- A8: Vertragskommentar an `getBudgetHistory` nennt die unbegrenzte Antwort; Folgeaufgabe budgeting-q8m2v im Backlog.
- A9: `HistoryRowsView.fs` vor `InspectorView.fs` mit `historyStatusView`, `historyChangeView`, `historyFailureView`, `historyFailureWrapped`, `ynabAssignedNote`; `HistoryView.fs` importiert `InspectorView` nicht mehr. `emptyText` (neuer Leertext) und `isRunOpen` in `History.fs` mit Test; die Wahl "aufklappen/zuklappen" entfällt, weil `aria-expanded` den Zustand trägt und der Name aus dem Zeileninhalt kommt. `last:border-b-0`. `changeOf` in `historyLine`. Zurück: über den Knopf Historie geöffnet ist es ein Schritt zurück im Browser-Verlauf (`ExternalMsg.StepBackToBudgetPage` zu `Cmd.navigateBack`, Vorbild `BackToBudget`), per Link geöffnet weiter `NavigateTo Budget`; Test auf Budget-Ebene. `/budget/history` in `KNOWN_ROUTES`; der Drift-Test in `scripts/screenshot-route.test.mjs` liest jetzt auch Arme mit mehreren Literal-Segmenten.

**Pause und Fehlerarten**
- P1: `YnabError.LocalReadFailed`, Text "BB-Daten nicht lesbar: ..." (`ynabErrorText`), in `fetchBudgetMonthWithPauses`, `QueryHandlers.budgetMonth` und `loadStates`. Test "Pausen nicht lesbar: Monat, Pausieren und Historie melden den Fehler als BB-Lesefehler" prüft Fehlerfall und Nutzertext; Gegenprobe `InvalidResponse` zurück in `budgetMonth`: rot. `loadStates` ohne eigenen Test: eine Bewegung mit unbekannter Herkunft lässt sich wegen der append-only-Trigger nicht wieder entfernen und bräche in der geteilten In-Memory-DB den Test über alle Monate von `getBudgetHistory`.
- P2: `Pause.pauseAnswerInTab` (Tab Target, Tab Geld beim Zuweisen); eine Ablehnung außerhalb kommt als Warn-Toast "Pause nicht gespeichert: ...", ein Abbruch als Fehler-Toast. `pauseCallLostText rereads message` verspricht das Neu-Lesen nur, wenn es stattfindet, und streicht einen Punkt am Ende der Meldung. Tests: andere Kategorie lehnt ab, anderer Monat bricht ab, Matrix der Tabs und Modi.
- Q1 hat einen echten Fehler gefunden: eine Pause-Antwort während eines Wechsels zurück in den Monat der Pause las nicht neu, das Lesen des Wechsels konnte vor dem Speichern begonnen haben, die Pause verschwand bis zum nächsten Lesen. `pauseNeedsReread` und `pauseCallLostRereads` lesen jetzt auch während eines Wechsels zu diesem Monat neu (Pfad "Tipp, Monat, Lesen 1 kommt an, Monat, Antwort, Lesen 2 kommt an" war rot, ist grün; der Test "Während eines Wechsels in einen anderen Monat liest die Pause-Antwort nicht neu" bleibt grün).
- P3: `[<RequireQualifiedAccess>]` auf `PauseKind`; `[<Literal>] PauseEventsUnreadableText` an beiden Stellen und ein Log-Helfer `Persistence.logReadFailure`, den `QueryHandlers` nutzt; `collectRows` an allen drei Stellen; Doc-Kommentar von `barAmountText` wieder an seiner Funktion; `Button.buttonState` nennt "IsBusy plus IsDisabled ergibt disabled ohne Spinner"; ein beschäftigter Knopf trägt keine Hover-Klassen (`Button.variantClassFor`, Test in `ButtonSizeTests.fs`); die Notiz zu den Monatsbalken in `/styleguide` nennt Komma im Label und den hervorgehobenen Monatsnamen; `PauseView` mit `prop.key` an Satz, Knopf und Meldung. Ausgelassen: `queryTargetChanges` als `Result` mit Ordnung nach `rowid`, weil es den Port `LoadTargetChanges` der Target-Historie ändert, den budgeting-g2ty9 mit den xp46a-Befunden anfasst.

**Tests**
- Q1: der Walk in `TargetPauseClientTests.fs` kennt "Neu-Lesen startet" (`MoveCallLost`), "Lesen n kommt an" mit dem Serverstand vom Start des Lesens, "Abbruch, gespeichert" und "Abbruch, nicht gespeichert". Ein Lesen gilt als gestartet, wenn die Generation steigt. Orakel: sind alle Lesevorgänge angekommen, zeigt der Monat den Serverstand; ein Tipp sendet nie bei laufendem Aufruf, Wechsel, fremder Kategorie oder ohne Liste. Der von Hand gebaute Zustand ist entfallen. Gegenprobe wie verlangt (`|| generationMoved || notLoaded` gestrichen): grün. Beide Bedingungen sind im erreichbaren Zustand von `MonthStale` gedeckt, denn jedes Lesen setzt `MonthStale` bis zu seiner Antwort, und eine während des Aufrufs angekommene Antwort überschreibt `withPause` ohnehin. Gegenproben mit Wirkung: `MonthStale` gestrichen: 2 rot; der neue Wechsel-Fall gestrichen: rot (siehe oben). Die Bedingungen bleiben als Absicherung stehen.
- Q2: `has Colors.textPrimary` wieder in `RuhigeListenTests.fs`.
- Q3: "Targets anwenden mit einer einzigen, fehlgeschlagenen Kategorie zeigt deren Grund"; Gegenprobe `when List.length statuses > 1` gestrichen: rot.
- Q4: `urlChangedCmd Budget -> BudgetHistory` enthält `GeneralHistoryEntered`.
- Q5: erneutes Betreten setzt Filter und Aufklappen zurück; Zuweisen mit 0 ohne Vorzeichen (`runAmount`); Konstantentest der Chip-Beschriftungen und die erste Prüfung in `TargetPauseTests.fs` gelöscht; "eine Abfrage" ehrlich als "das Laden ist angestoßen"; `mergeTests` in `BudgetHistoryTests.fs`; "mit Gegenseite" aus dem Namen; `BudgetGeneralHistoryTests.fs` nur noch `test`-CE; Vorbedingung `lost.Month` nicht geladen; doppelte Ebene aufgelöst (Handler-Prüfung "ohne Target wirkt die Pause nicht" gestrichen, `toBudgetMonth`-Test bleibt); der Balken-Label-Test im Browser prüft nur noch Vorhandensein und kein "·".
- Neu: 14 .NET-Tests, 2 Browser-Tests (`[layout]`).

**Offen:** `[human-eye]` nicht per Screenshot angesehen (Icon-Knopf im Kopf, Chevron, Badge "1 von 3"). Am PC ist der Ghost-Icon-Knopf ohne Mindestgröße (ab `md` wie alle Small-Icon-Knöpfe). Sinnvolle Reviews: `qa-milestone-reviewer`, `backend-code-quality`, `frontend-architecture`, `design-quality`.
