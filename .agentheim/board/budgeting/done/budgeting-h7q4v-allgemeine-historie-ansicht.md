---
id: budgeting-h7q4v
title: "Allgemeine Budget-Historie #/budget/history: alle Bewegungen, neueste zuerst, Filter nach Art, Targets-Durchlauf aufklappbar, Knopf Historie im Seitenkopf"
status: done
type: feature
context: budgeting
created: 2026-11-03
completed:
depends_on: [design-system-001, budgeting-dyn8g, infrastructure-b3t9k, budgeting-v6m3z]
blocks: []
tags: [budget, historie]
related_adrs: [0065, 0069]
related_research: []
prior_art: [budgeting-h2zfn]
---

## Why
Abgetrennt aus budgeting-dyn8g (Roman, 2026-11-03). Gesamtkonzept, Ablauf "Was ist passiert": Knopf Historie für alle, filterbar nach Art. Der Tab Historie zeigt nur eine Kategorie und einen Monat.

## What
Eigene Ansicht `#/budget/history`, geöffnet über den Knopf Historie im Seitenkopf der Budget-Seite. Neueste zuerst, alle Monate. Filter nach Art: Alle, Targets anwenden, Verschieben, Zuweisen.

Eine **Zeile je Durchlauf** (gleiche Gruppen-Id):
- Verschieben: Zeitpunkt, "20,00 € Lebensmittel → Urlaub", Monat, Stand.
- Zuweisen: Zeitpunkt, "+20,00 € Urlaub" bzw. "−20,00 € Urlaub" (zurück nach Zu verteilen), Monat, Stand.
- Targets anwenden: Zeitpunkt, "Targets anwenden, 37 Kategorien, 1.234,00 €", Monat, Stand; aufklappbar zu den Einzelzeilen (Kategorie, +Betrag, Stand).
- Ein Durchlauf mit mindestens einer fehlgeschlagenen Bewegung ist als solcher markiert.

Nutzt die Bewegungs-Abfrage und die Zeilenbausteine aus budgeting-dyn8g. Lesend, keine YNAB-Anfrage. Der Satz zu direkt in YNAB Zugewiesenem steht auch hier unter der Liste.

## Acceptance criteria
- [ ] (Domain) Bewegungen → Durchlauf-Zeilen: Gruppierung nach Gruppen-Id, Text je Art, Summe und Kategorienzahl bei Targets anwenden, Durchlauf-Stand "fehlgeschlagen", sobald eine Bewegung fehlschlug. Unit-Tests je Art, inklusive Targets-Durchlauf mit einer fehlgeschlagenen Zeile.
- [ ] (Domain) `#/budget/history` wird geparst und erzeugt.
- [ ] (Client-`update`) Filter nach Art zeigt nur Durchläufe dieser Herkunft; Aufklappen eines Targets-Durchlaufs zeigt seine Einzelzeilen, ein zweiter Tipp klappt zu.
- [ ] (Client-`update`) Knopf Historie im Seitenkopf führt zu `#/budget/history`, Zurück führt zur Budget-Seite mit erhaltenem Monat.
- [ ] Browser-Tests nach der Regel aus infrastructure-b3t9k (neuer Nutzerfluss Historie).
- [ ] README budgeting: die allgemeine Historie-Ansicht ist beschrieben.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

## Notes
Entscheidungen (Roman, 2026-10-29) aus budgeting-dyn8g: eigene Ansicht, kompakte Zeilen, Targets anwenden als eine aufklappbare Zeile. Hängt an infrastructure-b3t9k, weil der Browser-Happy-Path für den neuen Nutzerfluss erst mit der neuen Regel für Browser-Tests entschieden wird.

## Hinweise aus den Reviews vom 2026-11-04 (mit erledigen)
Die Reviews zu budgeting-t6p8z, budgeting-v6m3z und budgeting-k8wq3 haben Befunde zu den Historienzeilen, die diese Task ohnehin umbaut. Sie gehören hierher, nicht nach budgeting-r3wq8 (das parallel läuft und die Zeilen nicht anfasst):
- H1: Pause-Zeilen und Target-Änderungs-Zeilen tragen heute den Stand "geschrieben", obwohl eine Pause nie nach YNAB geht (`historyEntryLine` in `src/Shared/Budgeting.fs`, `historyStatusView` in `InspectorView.fs`). Pause-Zeilen zeigen keinen Stand, nur die Uhrzeit (etwa `Status: HistoryStatus option` oder ein Fall, der nichts rendert). Target-Änderungen behalten ihren echten Stand (geschrieben, fehlgeschlagen).
- H2: `historyLineView` ruft `Money.signedListAmount` direkt, der Test prüft `Inspector.historyChangeText`, das die View nicht benutzt. Eine reine Funktion liefert Text und Klasse des Betrags (etwa `historyChangeAmount: Money -> string * string`), die View rendert nur ihr Ergebnis, der Test in `BudgetHistoryClientTests.fs` prüft sie, `historyChangeText` fällt weg. Gegenprobe: View auf `Money.listAmount` zurückstellen muss rot werden. Eine Null-Bewegung zeigt kein Vorzeichen.
- H3: Die Überschrift "Bewegungen" im Tab Historie deckt Pausen und Target-Änderungen nicht mehr ab, etwa "Verlauf". Der Leertext gilt nur, wenn die Liste wirklich leer ist.
- H4 (kann): `mergeHistoryEntries` und `mergeHistoryWithTargetChanges` sortieren doppelt; eine Funktion `mergeHistory movements pauses changes` mit einem stabilen Sortieren (Gleichstand: Bewegung vor Pause, mit Test).
- Die allgemeine Historie zeigt auch Pausen und Target-Änderungen als eigene Arten im Filter, falls sinnvoll; sonst im Outcome begründen, warum nur die drei Bewegungsarten.

## Verifier note (iteration 1)
**REASONS:** Check 1, H2: Die Gegenprobe "View auf `Money.listAmount` zurückstellen muss rot werden" ist nicht erfüllt. `historyLineView` in `src/Client/Components/Budget/InspectorView.fs` rendert über `historyChangeView change`; stellt man diese Stelle auf `Money.listAmount change.Amount change.Currency` (oder `Money.signedListAmount`) zurück, bleiben alle Tests grün. `BudgetHistoryClientTests.fs` prüft nur die reine Funktion `historyChangeAmount`, die `[layout]`-Tests zum Tab Historie in `scripts/budget-page.test.mjs` (um Zeile 1229 und 1294) prüfen keinen Zeilenbetrag, der einzige Browser-Check auf den Betrag (`+60,00 €` in `scripts/budget-history.test.mjs`) deckt `runLineView` in `HistoryView.fs` ab. Alles andere ist erfüllt (verify PASS, H1, H3, H4, Filterbegründung, Ausnahme im Klassen-Wächter eng und begründet, README-Delta passt).
**SUGGESTED_FIX:** Einen Test ergänzen, der rot wird, wenn `historyLineView` nicht mehr über `historyChangeView`/`historyChangeAmount` rendert, dann die Gegenprobe laufen lassen (Aufrufstelle auf `Money.listAmount` zurück, rot sehen, zurücknehmen) und im Outcome vermerken.
**Hinweis des Conductors:** Eine Quelltextprüfung per Teilstring ist nach Test-Doktrin Punkt 7 verboten ("Keine neue View-Verdrahtung per Teilstring"). Erlaubt ist eine `[layout]`-Prüfung der berechneten Farbe (Doktrin Punkt 5: "berechnete Farben"): einen bestehenden `[layout]`-Test zum Tab Historie in `scripts/budget-page.test.mjs` erweitern, der Betrag einer Bewegungszeile mit positivem Betrag trägt die berechnete Farbe von `--color-text-primary` (Vorbild `tokenColor(page, ...)` in derselben Datei, etwa Zeile 311) und den Text mit Vorzeichen. Mit `Money.listAmount` (Teal) wird er rot.
**ITERATION_HINT:** likely-fixable

## Merge-conflict note (iteration 2)
Sibling: budgeting-r3wq8 (c6bd8bf, Review-Korrekturen: PauseIntent, PauseLifted/"Aufgehoben", Pausen als Result, Balken-Fokus, Klassentest nach RuhigeListenTests.fs verschoben). Neue Basis: main e69d362. Auflösung nur in: `src/Server/Budgeting/QueryHandlers.fs`, `src/Tests/BudgetHistoryClientTests.fs`. Gesicherter Diff vor dem Merge: `.agentheim/salvage/budgeting-h7q4v-merge-conflict.patch`.

## Outcome

Die allgemeine Budget-Historie ist gebaut, dazu H1 bis H4 aus den Reviews vom 2026-11-04. `./verify.sh` im Worktree: `VERIFY: PASS` (dotnet 3688 grün, 11 opt-in übersprungen, node 293 grün, beide Builds und beide Wächter grün). `npm run check:readme-delta -- --bc budgeting` endet mit Exit 0. Kein YNAB-Aufruf, kein Live-Test.

**Shared** (`src/Shared/Budgeting.fs`, `src/Shared/Api.fs`): `HistoryRun` und `HistoryRunLine` mit `historyRuns` (eine Zeile je Gruppen-Id, neueste zuerst). Texte: Verschieben "20,00 € Lebensmittel → Urlaub" (fehlt eine Seite in der Abfrage, kommt ihr Name aus der Gegenseite), Zuweisen "+20,00 € Urlaub" / "-20,00 € Urlaub", Targets anwenden "Targets anwenden, 37 Kategorien, 1.234,00 €" (Einzahl "1 Kategorie") mit Einzelzeilen in der Reihenfolge des Durchlaufs. Stand eines Durchlaufs: fehlgeschlagen, sobald eine Bewegung fehlschlug (bei Targets anwenden mit mehreren Kategorien "k von n fehlgeschlagen", sonst der Grund, bei Verschieben der der Quelle), sonst noch offen vor in YNAB geändert vor geschrieben. Neuer Endpoint `BudgetApi.getBudgetHistory: unit -> Async<Result<HistoryMovement list, string>>`. Das Minus ist wie in budgeting-v6m3z das ASCII-Minus.

**Server:** `QueryHandlers.budgetHistory` nutzt die bestehende Bewegungs-Abfrage `movements` mit leerem Filter, verdrahtet in `budgetApiWith` über `liveHistoryPort` (dieselbe Lese-Naht wie `getCategoryHistory`, kein neuer Port). H4: `Domain.mergeHistory movements pauses changes` ersetzt `mergeHistoryEntries` und `mergeHistoryWithTargetChanges`, ein stabiles Sortieren, bei Gleichstand Bewegung vor Pause vor Target-Änderung.

**Client:** Route `BudgetHistory` (`#/budget/history`, `src/Client/Types.fs`), Navigation zeigt den Tab Budget. Zustand auf der Budget-Seite (`Model.GeneralHistory`: `Runs`, `Filter: RunFilter`, `Expanded`, `ReturnPending`), reine Übergänge in `src/Client/Components/Budget/History.fs`, View `src/Client/Components/Budget/HistoryView.fs` (Zurück, Filter-Chips über `ChipBar.chip`, ruhige Liste, Targets-Durchlauf als `Button.listRow`, Satz zu YNAB unter der Liste). Nachrichten `OpenGeneralHistory`, `GeneralHistoryEntered`, `GeneralHistoryLoaded`, `SelectRunFilter`, `ToggleRun`, `LeaveGeneralHistory`, `GeneralHistoryAbandoned`; ExternalMsg `ShowGeneralHistory`, `ShowBudgetPage`. Der Knopf Historie (`Button.secondary`) steht am PC im Kopf neben Targets anwenden, am Handy in der Suchzeile. Rückkehr: `enterPage` behandelt `GeneralHistory.ReturnPending` wie die Rückkehr aus dem Kassenbuch (`KeptPage`: Monat, Auswahl, Tab, Filter bleiben, Monat still neu gelesen). Wer die Historie auf eine andere Seite verlässt, gibt die Rückkehr auf (`urlChangedCmd`), ein Aufruf per Link beginnt die Budget-Seite von vorn.

**H1:** `HistoryLine.Status` ist `HistoryStatus option`, Pausen tragen `None` und zeigen nur die Uhrzeit, Target-Änderungen behalten ihren Stand. **H2:** `Inspector.historyChangeAmount: Money -> string * string` liefert Text und Klassen, die View (`InspectorView.historyChangeView`) rendert nur das Ergebnis, `historyChangeText` ist entfallen, eine Null zeigt "0,00 €" ohne Vorzeichen. Der Klassen-Wächter verfolgt die Tupel-Bindung nicht, deshalb ein Eintrag `amountClass` in `scripts/tailwind-class-guard-exceptions.json`. **H3:** Überschrift "Verlauf", Leertext "Nichts festgehalten in diesem Monat" nur bei leerer Liste. **H4:** siehe Server.

**Warum nur die drei Bewegungsarten im Filter:** Die allgemeine Historie zeigt Durchläufe von Budget-Bewegungen (Geld, Gruppen-Id, Stand je Durchlauf). Pausen und Target-Änderungen sind keine Budget-Bewegungen (ADR 0080 Punkt 2) und haben keine Gruppen-Id. Für alle Monate bräuchten sie neue Abfragen über `target_pause_events` und `target_changes`, deren Pause-Code der parallele budgeting-r3wq8 gerade umbaut. Sie bleiben im Tab Historie je Kategorie. Als Folgeaufgabe im Backlog (budgeting-w3p7n).

**Tests (22 neu):** `src/Tests/BudgetGeneralHistoryTests.fs` (Durchläufe je Art, Verschiebung mit fehlender Seite, Quelle gescheitert, Targets mit Summe und Einzahl, Targets mit einer fehlgeschlagenen Zeile, Rangfolge der Stände, Gruppierung neueste zuerst; `mergeHistory` mit Gleichstand; `getBudgetHistory` gegen In-Memory-SQLite über alle Monate und Arten). `src/Tests/BudgetGeneralHistoryClientTests.fs` (Route parsen und erzeugen, Laden und Fehler, Filter je Art, Auf- und Zuklappen, nur Targets klappt auf, Knopf Historie über das echte App-`update` zu `NavigateTo BudgetHistory`, Zurück zu `NavigateTo Budget` mit erhaltenem Monat und Auswahl, Aufruf per Link und Verlassen woandershin beginnen von vorn, `PageLeft` beim Wechsel zur Historie). Zeile `BudgetApi.getBudgetHistory` in `RemotingEndpointNegativeTests.fs`, `getBudgetHistory` im Retry-Inventar von `RetryTests.fs`. `scripts/budget-history.test.mjs`: `[flow:budget-historie]` (neu in `FLOWS`). Angepasst: Stand als Option in `BudgetHistoryTests.fs`, `TargetChangeTests.fs`, `TargetPauseTests.fs` (Pause ohne Stand, Regressionskommentar H1), Betragstest in `BudgetHistoryClientTests.fs` prüft `historyChangeAmount` samt Null.

**Test zuerst, ehrlich:** Produktion und Tests entstanden je Kriterium dicht nacheinander, nicht streng rot vor grün. Rot per Gegenprobe belegt, je Mutante einzeln gesetzt und zurückgenommen: `enterPage` ohne `GeneralHistory.ReturnPending` (Zurück-Test rot), `historyChangeAmount` ohne Null-Regel (Betragstest rot). Die Aussage gilt nur für diese zwei Mutanten.

**Nachtrag Iteration 2 (Verifier H2):** Der `[layout]`-Test "Tab Historie am Handy: ..., der Betrag steht neutral mit Vorzeichen" in `scripts/budget-page.test.mjs` prüft jetzt den Betrag einer positiven Bewegungszeile im Tab Historie: Text "+50,00 €" und berechnete Farbe gleich `--color-text-primary` (`tokenColor`). Gegenprobe gelaufen: Aufrufstelle in `historyLineView` (`InspectorView.fs`) auf `Money.listAmount change.Amount change.Currency` zurückgestellt, der Test wird rot (actual `rgb(0, 212, 170)` Teal, expected `rgb(232, 232, 240)`), danach zurückgenommen und grün. Eine Rückstellung auf `Money.signedListAmount` bliebe in diesem Test grün, weil sie für Beträge ungleich null gleich aussieht; den Unterschied (Null ohne Vorzeichen) hält der Unit-Test zu `historyChangeAmount` fest. `./verify.sh` danach: `VERIFY: PASS` (dotnet 3688 grün, node 293 grün).

**Konflikt mit budgeting-r3wq8 aufgelöst (Iteration 3):** In `src/Server/Budgeting/QueryHandlers.fs` bleibt die Form von r3wq8: `LoadPauseEvents` liefert ein Result ("Pause-Ereignisse nicht lesbar"), die Ausgaben werden im eigenen `try` gelesen ("Ausgaben aus dem Kassenbuch nicht lesbar"), das Zusammenführen steht außerhalb des `try`. Dort ruft `categoryHistory` jetzt das eine `mergeHistory found pauses changes` (H4) statt `mergeHistoryWithTargetChanges (mergeHistoryEntries ...)`. In `src/Tests/BudgetHistoryClientTests.fs` prüft der Betragstest nur noch den Text von `historyChangeAmount` (Plus, Minus, Null ohne Vorzeichen); die Klassenprüfung ist bei r3wq8 nach `RuhigeListenTests.fs` (`Money.signedListAmountClass`) gewandert, und `historyChangeAmount` liefert genau diese Klasse. `./verify.sh` nach der Auflösung: `VERIFY: PASS` (dotnet 3714 grün, node 294 grün, beide Builds und beide Wächter grün). Das README-Delta passt weiter auf das zusammengeführte README (`check:readme-delta` Exit 0). Keine Konfliktmarker mehr im Worktree.

**Offen:** `[human-eye]` nicht per Screenshot angesehen (Seite am Handy und PC, Lage des Knopfs Historie im Kopf, Einzelzeilen auf `bg-surface-elevated`). Der Targets-Durchlauf ist ein `Button.listRow` ohne `aria-expanded` (Button.fs sollte wegen budgeting-r3wq8 unberührt bleiben), der Zustand steht im `aria-label` ("..., aufklappen" / "..., zuklappen"). `getBudgetHistory` liefert alle Bewegungen ohne Grenze; bei einem Haushalt bleibt das klein. Sinnvolle Reviews: `design-quality`, `frontend-architecture`, `backend-code-quality`, `qa-milestone-reviewer`.
