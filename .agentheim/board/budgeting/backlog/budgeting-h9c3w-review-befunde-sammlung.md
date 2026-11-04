---
id: budgeting-h9c3w
title: "Gesammelte Sollte- und Kann-Befunde der Reviews zu budgeting-c6vn4 und budgeting-g2ty9"
status: backlog
type: chore
context: budgeting
created: 2026-11-04
completed:
depends_on: []
blocks: []
tags: [budget, review, ux, test, a11y]
related_adrs: [0075, 0080]
related_research: []
prior_art: [budgeting-c6vn4, budgeting-g2ty9, budgeting-f4k9n]
---

## Why
Ab budgeting-c6vn4 werden im selbstständigen work-Lauf nur Muss-Befunde sofort behoben (budgeting-f4k9n), damit keine endlose Kette aus Fix und Review entsteht. Alles andere steht hier, damit Roman priorisiert und schneidet. Quelle: Reviews zu 9faa461 (c6vn4) und bd78ffc (g2ty9).

## Hohe Priorität

1. **Kopf bei 1024 px bricht mit echten Daten um** (Design c6vn4). `src/Client/Components/Budget/View.fs:93-112`, `MonthView.fs:340`. Gemessen 1012 px Bedarf auf 984 px, die Uhr steht allein in Zeile 2, erst ab etwa 1060 px eine Zeile. Der `[layout]`-Test ist grün, weil die Fixture kürzere Werte hat. Vorschlag: zwischen md und xl mindestens 28 px gewinnen (Suche `md:w-24 xl:w-32` oder kleineres `gap-x` bis xl); Fixture mit realistischen Längen (Zu verteilen fünfstellig, Chip-Zähler zweistellig, "September 2026").
2. **Historie am Handy schneidet die tragende Info ab** (Design c6vn4). `HistoryView.fs:62`: `truncate` im Kopf des Durchlaufs schneidet "Quelle → Ziel", Vorzeichen und Anzahl ab ("Verschieben, Essen/Einkaufen → ...", "Targets anwenden, 2 Kate..."). Vorschlag: `line-clamp-2` oder Kategorien am Handy in eigener Zeile.
3. **Badge "1 von 3" am Handy ohne Screenreader-Text** (Design und Frontend c6vn4). `HistoryView.fs:44-51`, `History.fs:73`: "fehlgeschlagen" nur über Farbe, das Wort ist `hidden md:inline`. Vorschlag: `sr-only` " fehlgeschlagen" im Handy-Zweig oder zugänglicher Name aus `runBadge`.
4. **"YNAB hat abgelehnt" für BB-eigene Fehler** (Backend c6vn4). `src/Server/Budgeting/CommandHandlers.fs:96` (`plan`: Betrag außerhalb des 10-Milliunits-Rasters wird von BB verweigert, Meldung sagt YNAB) und `:546` (`retryFailed` mit `Ok []` meldet "YNAB hat abgelehnt: Unbekannte Anwendung"). Vorschlag: `:546` als `LocalReadFailed "Unbekannte Anwendung"`, `:96` eigener Fall ohne YNAB-Präfix; je Test auf den Nutzertext.
5. **`RunPartlyFailed` auch bei n von n** (Backend c6vn4). `src/Shared/Budgeting.fs:747-750`, `:784-786`: scheitern alle Kategorien, heißt der Lauf trotzdem "teilweise" ("3 von 3"), der Doc-Kommentar sagt anderes. Vorschlag: bei failed = total `RunFailed` mit erstem Grund, oder Fall umbenennen; Test "alle von n gescheitert".

## Sollte (g2ty9, Target-Formular)

6. **Pause-Sperre nur im Tab Target, zwei Schreibaufrufe möglich** (Frontend g2ty9 S2, QA g2ty9 S3). `Pause.fs:20`, `:71-74`, `TargetEdit.fs:231-238`. Rückfrage im Tab Target öffnen, Tab Geld, "Target pausieren" läuft; zurück, "Wechseln" oder "Entfernen" prüft nur `targetBusy`, nicht `pauseBusy`. Zwei Aufrufe mit altem `Shown`, der spätere bekommt `TargetChangedInYnab`. **Widerspruch der Reviews, Roman entscheidet:** Frontend will die Tab-Bedingung in `pauseHeldByConfirm` streichen und `start`/`askRemoveTarget` auch `pauseBusy` prüfen lassen; QA warnt, dass ohne Tab-Bedingung der Pause-Knopf im Tab Geld ohne sichtbaren Grund gesperrt ist. Denkbar: Tab-Wechsel schließt offene Rückfragen, dazu `pauseBusy` in `start`. Walk um "Tab wechseln" erweitern.
7. **Entfernen-Bestätigung bleibt nach stillem Neulesen offen** (Frontend g2ty9 S3). `TargetEdit.fs:265-274` (`syncTargetForm`), `TargetEditView.fs:289`. Liefert das Neulesen die Kategorie ohne Target, bleibt "Target entfernen?" offen, "Entfernen" tut nichts, "Target anlegen" und Pause bleiben gesperrt bis "Abbrechen". Vorschlag: `syncTargetForm` setzt `ConfirmingRemove` zurück, wenn kein Target mehr gezeigt ist; Test mit stillem Neulesen.
8. **Fokus nach erfolgreichem Entfernen fällt auf body** (Design g2ty9 S2). `TargetEditView.fs:91`, `:273-287`, `TargetEdit.fs:381`. Der Knopf "Target entfernen" verschwindet mit `shown = None`, VoiceOver springt an den Seitenanfang, keine Erfolgsmeldung. Vorschlag: Fokus auf "Target anlegen" oder den Tab-Kopf, oder `role="status"` "Target entfernt"; `[fokus]`-Test.
9. **Satzzeichen im gelben Hinweis** (Design g2ty9 S1). `TargetEdit.fs:408` (`targetNoticeText`): "... nicht gesetzt: YNAB nicht erreichbar Speichern setzt das Datum erneut." Vorschlag: mit Punkt anschließen, wenn die Meldung nicht auf . ! ? endet; Erwartung in `TargetChangeClientTests.fs:450` anpassen.
10. **Meldung "anderer Rhythmus" auch bei anderer Art oder ohne Target** (Backend g2ty9 S1). `Shared/Budgeting.fs:613`, `Server/Budgeting/Domain.fs:243`, `CommandHandlers.fs:539`. MF auf NEED monatlich ergibt "zeigt nach Schritt 1 monatlich statt monatlich"; ohne Target in der Antwort "mit anderem Rhythmus angelegt". Vorschlag: kleine DU als Grund (keine NEED, kein Target, anderer Rhythmus), je Fall ein Test.
11. **"Speichern setzt das Datum erneut" auch beim zurückgehaltenen Schritt 2** (Backend g2ty9 S2). `TargetPartlyWritten` trennt nicht `LaterNotWritten` und `LaterHeldBack`; nach falschem Rhythmus plant erneutes Speichern wieder einen Wechsel mit `goal_frequency`. Vorschlag: Grund-DU (`DateRejected | DateHeldBack`) im Shared-Vertrag, Satz nur bei `DateRejected`; Fixtures in `scripts/target-change.test.mjs` im selben Schritt mitziehen. Hängt an der offenen Frage, ob Schritt 1 nach unerwartetem Rhythmus erneut gehen darf.
12. **Anlegen in anderem Monat bekommt den Ändern-Text** (Backend g2ty9 S3). `Validation.fs:300`, `CommandHandlers.fs:508`: bei `Shown = None` und `Wanted.IsSome` `TargetCreateMonthText` melden; Negativzeile "Anlegen im Vormonat".
13. **Walk prüft keine Knopfzustände** (QA g2ty9 S2). `TargetChangeClientTests.fs:662`: je Schritt `removeButtonState(...).Disabled` und die Pause-Sperre gegen das Orakel prüfen; die Pause-Sperre aus `PauseView.fs` als reine Funktion ziehen.

## Sollte (c6vn4, Tests)

14. **Benannter Regressionstest für die Pause-Antwort während eines Monatswechsels** (QA c6vn4 S1). Bug nur im Walk bei Tiefe 6 sichtbar. GIVEN/WHEN/THEN SetTargetPause, SelectMonth next, Loaded next, SelectMonth current, TargetPauseAnswered: `MonthGeneration` steigt; dasselbe für `TargetPauseCallLost`; Gegenprobe `|| monthChanging model` streichen macht rot.
15. **"Zurück geht einen Schritt zurück" nur halb getestet** (QA c6vn4 S2). `BudgetGeneralHistoryClientTests.fs:186`: echter Ablauf mit `PageLeft`, `GeneralHistoryEntered`, Antwort; Intakt-Fall `GeneralHistoryAbandoned` mit Link auf `ShowBudgetPage`.

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
Zum Schneiden: 1 bis 3 sind eine UI-Task (Kopf und Historie), 4 und 5 eine Backend-Task, 6 bis 13 eine Task im Target-Formular (nicht parallel zu anderen Arbeiten dort), 14 und 15 eine Test-Task.
