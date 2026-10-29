# Frontend in BudgetBuddy

Was der Code nicht von selbst erklärt. Visuelle Regeln: `standards/frontend/styleguide.md`. MVU-Regeln: `.claude/rules/elmish-state.md`.

## Aufbau

- `src/Client/App.fs` startet Elmish, `src/Client/State.fs` und `src/Client/View.fs` sind die Wurzel mit Seitenwechsel.
- Je Seite ein Ordner `src/Client/Components/<Seite>/` mit `Types.fs` (Model, Msg), `State.fs` (init, update) und `View.fs`.
- `src/Client/Api.fs`: Fable.Remoting-Proxys, Route `/api/<Interface>/<Methode>` wie im Server.
- `src/Client/Types.fs`: `RemoteData<'T>` mit Helfern (`map`, `withDefault`, `fromResult`, ...). `src/Client/Retry.fs`: Wiederholung beim Laden.
- `src/Client/DesignSystem/`: alle UI-Bausteine; Inventar im Styleguide Abschnitt 5, lebend unter der Route `/styleguide`.

## Gelerntes

- Späte Antworten: wenn Eingaben schnell aufeinander folgen, trägt jede Änderung eine Version, und nur die neueste wird übernommen (`PendingCategoryVersions` in `src/Client/Components/SyncFlow/State.fs`).
- Lange Listen: teure Ableitungen (etwa Kategorie-Optionen) einmal vor der Schleife berechnen, inaktive Zeilen als schlichten Text statt als volle Auswahlkomponente rendern.
- Listenelemente mit `prop.key`, sonst rendert React ganze Listen neu.
- Schaltflächen, die einen Aufruf auslösen, zeigen den Ladezustand und sind solange gesperrt (Doppelklick).
- Keine Aktion nur per Hover; das Handy ist ein Hauptgerät.
- Fable-Fallen (Serialisierung, Formatierung): `.claude/rules/fable-pitfalls.md`.
