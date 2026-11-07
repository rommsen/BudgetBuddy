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
- UI (Review c4ve4, Frontend V3): Der Vorschlagshinweis im Panel nennt den Rhythmus des gelesenen Targets; wechselt man im Panel den Rhythmus, bleibt "… 35 € wöchentlich" stehen. Besser ein Zusatz "(im bisherigen Rhythmus)" oder der Hinweis verschwindet nach dem Wechsel.
- UI (Review c4ve4, Design V5): Die Titel der Seite Targets prüfen nutzen rohe Größen (`text-lg`, `text-xl`, `text-2xl`). Eine Rolle in `Tokens.fs` für schlanke Seitenköpfe teilten Budget-Historie und diese Seite.

## Aus budgeting-m3x7f (2026-11-06)
- UI (Worker m3x7f): Die Checkbox der Vorschau von Targets anwenden ist die native des Browsers wie in der Skizze. Ein DS-Baustein im Stil der Skizze wäre einheitlicher.
- UI (Verifier m3x7f): Am Handy steht "Zu verteilen" im Kopf ohne €, damit der Kopf in eine Zeile passt; die Beträge der Liste darunter tragen €. Am Bildschirm prüfen, ob das gemischt stört.
- Test (Verifier m3x7f): Der Dichte-Test bei 1440 × 900 in `scripts/budget-page.test.mjs` erlaubt die erste Zeile jetzt in den oberen 30 % statt 25 % (eigene Suchzeile wie die Skizze). Der Abstand zur Grenze ist klein (etwa 265 px bei 270 px).
- Code (Verifier m3x7f): In `src/Server/Budgeting/Persistence.fs` stehen die neuen Funktionen für Ausblenden zwischen dem Doc-Kommentar von `initializeBudgetingSchema` und der Funktion; der Kommentar hängt jetzt über dem Banner.

## Aus dem Bestandsreview budgeting (2026-11-07)
- Darstellung (Gruppe 5, 10 Befunde, höchste Schwere mittel): globale h-Regel in `styles.css:196` setzt Gruppenköpfe am Handy zu eng und Panel-Namen in Orbitron (Abhilfe `role "heading"` oder die Regel in `@layer base`), `Button` überstimmt zwischen 768 und 1023 px die 48 px im Sheet, Kopfzähler von Targets prüfen nicht in der Farbausnahme, Badges im Bericht, zwei verschiedene Zurück-Knöpfe, Ladetexte mit "...", "Keine Kategorien" ohne Aktion, Spinner statt Skeleton. Offen für Roman: Ist der Titel "Targets prüfen" in Orbitron gewollt? Befunde: `.agentheim/knowledge/reviews/2026-11-07-budgeting.md`, Abschnitt 5.
- Tests (Gruppe 6, 9 Befunde, höchste Schwere mittel): Pausieren im vergangenen Monat ohne update-Test, Aufzählung ohne Lesefehler, `viewSourceTests` ohne Gegenbeweis, Logik in `[fable]`- und `[layout]`-Tests der Budget-Seite, Konstanten- und Feldkopie-Tests, Grenzfall 1.000.000,00 € beim Target. Abschnitt 6.
- Kleinkram (Gruppe 7, 10 Befunde): Goal statt Target für YNABs Goals, `DestinationHeldBack`, eigener Fehlertyp der Budget-Abläufe, `berlinDayOf` aus `Ledger.Bot.Domain`, Bool-Flags `Retrying` und `LoadPending`, `init` ohne Cmd, Nachrichtennamen `Load`/`Loaded`, veralteter Kommentar in `YnabClient.fs:444`, stiller Default in `categoryNameIn`. Abschnitt 7.
- Design (Review m3x7f): Die Kommentare in `Tokens.fs` zu den Radien (sm 6, md 8, lg 12) stimmen nicht. `styles.css` setzt `--radius-xl: 24px` ungelayert, gemessen sind rounded-md 12 px, rounded-lg 16 px, rounded-xl 24 px. Deshalb bleiben `rounded-[12px]` und `rounded-[10px]` der Leiste und `p-[18px]` der Vorschau als Werte der Skizze stehen.
- Skizze (Review m3x7f): Nicht als Abweichung in budgeting-m3x7f gelistet, aber bestehender App-Standard: Historie ist ein Icon-Knopf (Skizze: Textknopf "Historie"), Zu verteilen am PC mit € (Skizze ohne). Beim nächsten Skizzen-Vergleich als bekannt behandeln oder in der Task nachtragen.

## Aus dem Modeling von budgeting-my8p8 (2026-11-07, Bestandsreview budgeting Gruppe 1)
- Benennung (erst beim nächsten Vertragsbruch): `retryFailedTargets` heißt besser `retryWriteBack` (`src/Shared/Api.fs:524`), `ApplyReport.ApplicationId` ist `string` statt `Guid` (`src/Shared/Budgeting.fs:340`), `Guid.TryParse` der Anwendungs-Id gehört nach `Validation.fs` (`src/Server/Api.fs:1998`), "Targets-Anwendung nicht gefunden" gilt auch für Verschieben und Zuweisen (`src/Server/Budgeting/CommandHandlers.fs:1082`).
- UI: Der Bericht von Targets anwenden trägt in jeder Zeile ein grünes `Badge.success` (`TargetsView.fs:110`); schlicht wie die Historie (styleguide §2).

## Aus budgeting-r5h2n (2026-11-07)
- Am Handy kürzt der Fehlgrund neben dem Badge ("Rate…") in der allgemeinen Historie. Das betrifft Durchläufe und Target-Zeilen gleich und bestand schon vorher.
