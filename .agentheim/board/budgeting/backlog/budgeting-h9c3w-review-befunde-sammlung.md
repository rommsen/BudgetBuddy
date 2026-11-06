---
id: budgeting-h9c3w
title: "Kann-Sammlung budgeting"
status: backlog
type: chore
context: budgeting
created: 2026-11-04
completed:
depends_on: []
blocks: []
tags: [budget, review, ux, test, a11y, design, pause, bug]
related_adrs: [0075, 0080, 0049]
related_research: []
prior_art: [budgeting-c6vn4, budgeting-g2ty9, budgeting-f4k9n]
---

## Why
Ab budgeting-c6vn4 werden im selbstständigen work-Lauf nur Muss-Befunde sofort behoben (budgeting-f4k9n), damit keine endlose Kette aus Fix und Review entsteht. Alles andere steht hier, damit Roman priorisiert und schneidet. Quelle: Reviews zu 9faa461 (c6vn4) und bd78ffc (g2ty9).

## Aufteilung (2026-11-04, Roman: "teil ihn so auf und verfeinere, entscheide nach bestem Wissen")
- Punkte 1 und 2 (Kopf bei 1024 px, Historie am Handy): budgeting-a3k7m
- Punkte 4 und 5 (BB-eigene Ablehnungen, n von n): budgeting-b5n8q
- Punkte 6, 7, 9 bis 13 (Target-Formular): budgeting-c2t9w
- Punkte 14 und 15 (Regressionstests): budgeting-e6p4r
- Verworfen, Roman "Screenreader ist mir gerade egal": Punkt 3 (Badge "1 von 3" ohne Screenreader-Text) und Punkt 8 (Fokus nach erfolgreichem Entfernen mit VoiceOver).

Was hier bleibt, sind Kann-Befunde. Sie werden nicht automatisch gebaut.

## Kann

- Backend c6vn4: eigener `BudgetError`-Typ statt `LocalReadFailed` in `YnabError`; `loadApplication` (`Persistence.fs:236-269`) mit rohem Fehlertext und ohne try/with; zwei `logReadFailure` gleichen Namens; `List.head` in `Shared/Budgeting.fs:804` kommentieren; Doc `HistoryRun.Status` (`:768`) veraltet.
- Design c6vn4: `/styleguide` Beispiel 8a mit `md:hidden`/`md:inline`-Aufbau; Styleguide §7.3 Satzreihenfolge ("der Knopf" mehrdeutig), §5 Komma vor `listRow`; `History.fs:39` Leertext ohne Weg zurück (`Card.emptyState` "Zurück zum Budget"); `items-baseline` in `mobileLine2` DS-weit.
- Frontend c6vn4: `Pause.fs:128` `pauseAnswerInTab` mit `Option.exists` statt `Option.forall`; `State.fs:346-351` Ablehnung kann doppelt erscheinen (Toast und Karte); Doc-Kommentar `pauseNeedsReread`.
- QA c6vn4: Kommentar und Reihenfolge in `BudgetGeneralHistoryTests.fs:113`; `EntriesTab` in der `pauseAnswerInTab`-Matrix; `aria-controls` im Flow-Test; `runAmount` auch gegen `signedListAmountClass`; Bedingungen `generationMoved`/`notLoaded` in Q1 streichen oder benennen.
- g2ty9 Outcome: Verlustsätze der Warnung als Liste (braucht neue DS-Variante in `Modal.confirm` und `BottomSheet.confirm`); `Modal.confirm` fängt am PC den Fokus nicht (Design xp46a K11).

## Augenprüfung für Roman
- Uhr-Knopf ohne Text: als "Historie" auffindbar? (Design c6vn4)
- Am Handy während des Speicherns: wirken der gesperrte Rhythmus (opacity-50) und die gesperrten Felder (opacity-60) wie eine Gruppe? (Design g2ty9)
- `/styleguide` Abschnitt 4 bei 375 px: sind "teilweise geschrieben" und "in YNAB geändert" auseinanderzuhalten? (Design g2ty9)
- Gelber Hinweis `TargetPartlyNotice` und gesperrtes Segmented im echten Tab Target ansehen.

## Notes
Roman will BB bald zur Quelle machen und YNAB spiegeln. Kann-Punkte zu YNAB-Sonderfällen sind dann vermutlich hinfällig.

## Kann-Sammlung (ab 2026-11-06)
Diese Task ist die Kann-Sammlung des BC nach der Regel Backlog-Disziplin in CLAUDE.md. Neue Funde ohne Geld-, Daten-, Sicherheits- oder Alltagsfehler kommen als Zeile hierher. Sie wird nie automatisch gebaut.

## Aufgenommen aus budgeting-w2f6c (Triage 2026-11-06)
**Budget-Seite: Listenbeträge gegen ADR 0049** (bug, erfasst 2026-11-02)

### Why
`plainAmount` in `Components/Budget/View.fs:27-31` setzt `text-secondary` statt `text-primary`, und eine positive Aktivität ist nicht Teal. Das weicht von ADR 0049 ab. Die Reviews zu budgeting-g8s3w haben es gefunden, budgeting-p4n6r schloss es aus.

### What
Die Beträge an ADR 0049 angleichen oder die Abweichung per ADR als Ausnahme für die Budget-Seite festhalten.

### Acceptance criteria
- [ ] (Browser) Zugewiesen, Aktivität und Target folgen den Farbregeln aus ADR 0049, oder ein ADR nennt die Ausnahme und ihren Grund.
- [ ] Styleguide und `/styleguide` stimmen mit der Entscheidung überein.

## Aufgenommen aus budgeting-w5k2r (Triage 2026-11-06)
**Pause verschwindet, wenn beim Tipp ein Lesen des Monats schon läuft (Wechsel hin und zurück)** (bug, erfasst 2026-11-04)

### Why
Aus budgeting-m4v8t. Ablauf: Monat geladen, `SelectMonth` nächster Monat, vor dessen Antwort `SelectMonth` zurück. Die alte Liste steht, kein Wechsel läuft (`monthChanging` falsch), `Freshness = MonthRead`, das Lesen des Rückwegs läuft noch. Jetzt `SetTargetPause`: der Aufruf startet mit der Generation dieses Lesens. Kommt die Pause-Antwort vor dem Lesen, sieht `pauseNeedsReread` nichts (Generation gleich, geladen, nicht veraltet). Danach kommt das Lesen an, das vor dem Speichern begann, und `applyLoaded` überschreibt die Pause bis zum nächsten Lesen. Die Monatspfeile sind während eines Wechsels nicht gesperrt, der Fall ist in der UI erreichbar. Der Walk in `src/Tests/TargetPauseClientTests.fs` erreicht ihn nicht: sein Ereignis "Monat" kommt nur, wenn `Displayed = Chosen`.

### What
Kaputt-Szenario (GIVEN/WHEN/THEN): GIVEN Wechsel hin und zurück, das Lesen des Rückwegs läuft, Tipp auf Pausieren WHEN die Pause-Antwort vor dem Lesen kommt und danach das Lesen ohne Pause THEN zeigt der Monat nach allen Lesevorgängen die Pause. Intakt-Szenario: ohne laufendes Lesen beim Tipp liest die Antwort nicht neu (bestehender Test "GIVEN Fehlbetrag WHEN Pause bestätigt ..."). Mögliche Wege: ein Lesen, das beim Start schon lief, im Modell erkennbar machen (etwa `MonthStale` auch bei `select`, Folgen für `Moves.fs:481` prüfen) oder `startPause` sperren, solange ein Lesen läuft. Den Walk um "Monat" während eines Wechsels erweitern.

### Acceptance criteria
- [ ] Regressionstest mit Bug-Kommentar, vor dem Fix rot.
- [ ] Der Walk kennt den Monatswechsel während eines laufenden Wechsels und bleibt grün.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

## Aus dem Review zu budgeting-eh8xe (2026-11-06)
- Design: Die sieben Segmente des Wochentags (`Segmented`) sind am Handy (375 px) vermutlich etwa 43 bis 45 px breit, Styleguide Abschnitt 4 verlangt 48 px. Am Bildschirm prüfen, sonst Wochentag-Leiste ohne Rahmen oder mit kleinerer Lücke.
- Frontend: Die lesende Anzeige des Targets nennt den Wochentag nicht (`Month.fs` `targetText`), im vergangenen oder künftigen Monat ist er unsichtbar. Produktfrage an Roman: "/Woche, Freitag"?
- Backend: `categoryNameIn` (`Budgeting/Persistence.fs`) setzt für ein in BB angelegtes Target ohne Übernahme still `""`. `CategoryName` in `HistoryTargetChange` optional machen oder den Namen im `TargetSet` tragen.
- Tests: optional ein `[layout]`-Test, dass die sieben Segmente im 500-px-Sheet nicht abgeschnitten werden.
- Backend (Worker eh8xe): Die Vorschau kann jetzt die einmalige Target-Übernahme festhalten, sonst schreibt sie nichts. Im Alltag hat das Lesen des Monats vorher übernommen.
- Shared (Worker eh8xe): `TargetChangeResult` hat nur noch einen Fall. Ein Record wäre schlichter, bricht aber Fixtures und ältere Clients.

## Aus budgeting-k5q2w (2026-11-06)
- Backend (Worker k5q2w): Ein Lesefehler im Kassenbuch steht im Server-Log nur unter "Parallelrechnung Fehlbetrag", ohne eigenen Text für die Quelle.
- Backend (Review k5q2w, V1): `periodActivityFromLedger` ist rein und gehört nach `Domain.fs`; dabei `spendingOf` und das Monatsparsen wiederverwenden (eine Schreibweise des Null-Betrags).
- Backend (Review k5q2w, V4): Ein `TargetSet` trägt `effectiveFrom` als "YYYY-MM" und `rule.createdIn` als "YYYY-MM-01". Ein eigener Typ für den Monatsschlüssel schlösse Verwechslungen aus.
- Tests (Review k5q2w): Kein Test belegt, dass ohne Periodenmonat das Kassenbuch nicht gelesen wird (`loadPeriodActivity`, Zweig `[]`); nur eine überflüssige Abfrage, kein falsches Ergebnis.

## Aus budgeting-hbhzf (2026-11-06)
- Tests (Verifier hbhzf): `BudgetLeadTests` schreibt die Gruppe "Server-Log" als Literal statt `UnderfundedTests.ServerLogGroup`.
- Tests (Verifier hbhzf, wie bei k5q2w): der Aufruf von `dropUnderfundedComparisonsOn` in `initializeBudgetingSchema` ist nur über den Funktionstest belegt, ein Mutant ohne Aufruf überlebt.
- Backend (Review hbhzf, W1): "Nicht gerechnet" verschwindet ohne Spur, Targets anwenden lässt die Kategorie still weg und die Vorschau nennt sie nicht. Heute ohne Fall (fünfter Vergleich ohne "nicht gerechnet", das Formular legt nur NEED monatlich, wöchentlich, jährlich an). Vorschlag: Grund bis in die Vorschau tragen oder mindestens ins Log.
- Backend (Review hbhzf, V1/V2/V3): `LedgerActivityPort` und `loadPeriodActivity` sind Lese-Bausteine in `CommandHandlers`, eine eigene Datei vor beiden Handlern ordnete das; `DateTime.ParseExact` des Monatsersten doppelt zu `Domain.firstOfMonth`; `YnabError.LocalReadFailed` für einen lokalen Lesefehler führt im Namen in die falsche Richtung.
- Tests (Review hbhzf): `stderrOf` steht doppelt in `BudgetLeadTests` und `UnderfundedTests`, Helfer und Gruppenname an eine Stelle vor beiden Dateien.

## Aus budgeting-q4v8n (2026-11-06)
- UI (Worker q4v8n): Das vorbelegte Betragsfeld zeigt "1200,00" ohne Tausenderpunkt (`AmountMath.formatSignedAmount`, wie im Tab Target).
- UI (Review q4v8n, Frontend V6 und Design V2): Die Panel-Hülle der Seite Targets prüfen (`TargetCheckView.fs`, Klassenkette `budget-inspector-panel … rounded-xl border`) wiederholt die des Inspektors (`InspectorView.fs`), `groupCard` die Karte der Historie (`HistoryView.fs`). Eine gemeinsame Hülle bzw. eine `Card`-Variante ohne Innenabstand ordnete alle drei.

## Aus budgeting-c4ve4 (2026-11-06)
- UI (Worker und Verifier c4ve4): Im Sheet "Anpassen …" am Handy schließt das X des `BottomSheet.inspector` statt des Worts "Schließen" wie in der Skizze; `BottomSheet.inspector` hat keinen Text-Schließknopf. Roman entscheidet, ob es so bleibt.
