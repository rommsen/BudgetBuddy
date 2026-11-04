---
id: budgeting-h9c3w
title: "Kann-Befunde der Reviews zu budgeting-c6vn4 und budgeting-g2ty9 (Rest nach der Aufteilung)"
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
