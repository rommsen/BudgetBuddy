---
id: ADR-0044
title: Kategorie-Umbenennung ist keine Umkategorisierung, Spiegel vergleicht die Id, die Liste löst den Blattnamen live auf
scope: ledger
status: accepted
date: 2026-10-02
supersedes: []
superseded_by: []
related_tasks: [ledger-ggzdw]
related_adrs: [0013, 0014, 0039]
related_research: []
---

# ADR 0044: Kategorie-Umbenennung ist keine Umkategorisierung, Spiegel vergleicht die Id, die Liste löst den Blattnamen live auf

## Context

`Ledger.Domain.observe` vergleicht den Kategorisierungs-Zustand einer Buchung als volle
Record-Gleichheit: `entry.Category <> obs.Category` (Kopf) und `entry.Splits <>
obs.Splits` (Split-Zeilen). `CategoryRef` trägt `CategoryId` UND `CategoryName`,
`SplitTarget.ToCategory` ebenso `categoryId` UND `categoryName`. Benennt Roman eine
Kategorie in YNAB um, ohne eine einzige Buchung umzukategorisieren, erzeugt der nächste
Spiegel-Lauf für jede schon gespiegelte Buchung dieser Kategorie ein
`EntryCategorizationChangeObservedInYnab`-Ereignis, append-only für immer (ADR 0013). Bei
rund 14.000 Buchungen ist das die ergiebigste bekannte Quelle falscher Historie-Zeilen im
Kassenbuch.

Romans Entscheidung (festgehalten in `ledger-ggzdw`, nicht mehr offen): die
Kategorie-**Identität** einer Buchung ist allein `CategoryId`. Der Name ist Beschriftung,
keine Änderung an der Zuordnung.

ADR 0039 hat genau diese Trennung schon für die Kategorie-**Gruppe** entschieden: die
Gruppe steht nie im Ereignis-Log, sondern wird im Lesepfad live aus der gespiegelten
Tabelle `ledger_categories` nachgeschlagen (`Ledger.Persistence.loadCategoryGroups`,
`Ledger.QueryHandlers.ReadModel.toEntryView`), mit der ausdrücklichen Begründung "eine
Umbenennung oder Verschiebung ist eine Korrektur der Einordnung, kein neuer Sachverhalt".
Für den Kategorie-**Blattnamen** (`CategoryName`, nicht die Gruppe) galt das bisher nicht:
`toEntryView` setzt `CategoryName = entry.Category |> Option.map (fun c ->
c.CategoryName)`, also den im Zustand gespeicherten, zuletzt BEOBACHTETEN Namen, gefüttert
ausschließlich über `apply` aus dem Ereignis-Log (ADR 0013). Ohne eine zusätzliche
Entscheidung an dieser Stelle hätte der bloße Vergleichs-Fix eine neue Unstimmigkeit
eingebaut: nach einer reinen Umbenennung zeigt die Liste dauerhaft die **heutige** Gruppe
(ADR 0039), aber den **alten** Blattnamen, weil kein Ereignis mehr entsteht, das `apply`
anstößt, das den gespeicherten Namen nachzieht.

`EntryObservedInYnabPayload.Category`/`obs.Category` wird je Spiegel-Lauf frisch aus der
aktuellen YNAB-Antwort gebaut (`YnabObservations.fs`); `ledger_categories` ist daran nicht
beteiligt und wird erst am Ende des Laufs aktualisiert (`refreshCategories`). Ein echtes
Kategorisierungs-Ereignis trägt also ohnehin schon den zum Beobachtungszeitpunkt aktuellen
Namen; nur das Unterbleiben eines Ereignisses nach einer reinen Umbenennung lässt den
gespeicherten Namen veralten.

## Decision

1. **`observe` entscheidet über `EntryCategorizationChangeObservedInYnab` allein anhand
   der Kategorie-Identität, nicht anhand des Namens.** Eine neue Funktion
   `Ledger.Domain.categorizationIdentity` bildet Kopf-Kategorie (nur `CategoryId`, Name
   verworfen) und Split-Zeilen (bei `ToCategory` nur `categoryId`, `ToTransfer` und
   `Uncategorized` unverändert) auf eine vergleichbare Form ohne Kategorie-Namen ab.
   `categorizationChanged` vergleicht diese Identität, nicht mehr `entry.Category`/
   `entry.Splits` direkt:

   ```fsharp
   let private splitTargetIdentity (target: SplitTarget) : SplitTarget =
       match target with
       | ToCategory (categoryId, _) -> ToCategory (categoryId, "")
       | other -> other

   let categorizationIdentity (category: CategoryRef option) (splits: TransactionSplit list) =
       category |> Option.map (fun c -> c.CategoryId),
       splits |> List.map (fun s -> { s with Target = splitTargetIdentity s.Target })
   ```

   Löst ein ANDERER Grund (andere Id, andere Split-Beträge, andere Reihenfolge, andere
   Memos) trotzdem ein Ereignis aus, trägt der Payload weiterhin die vollen, echten Werte
   samt Namen (`OldCategory = entry.Category`, `NewCategory = obs.Category`, analog für
   Splits): nur die Ereignis-Entscheidung ist namens-blind, nicht die gespeicherte Nutzlast.

2. **Der im Zustand gespeicherte Kategorie-Name bleibt nach einer reinen Umbenennung
   veraltet, bewusst.** Ohne Ereignis gibt es keinen Fold-Schritt, der ihn nachzieht. Das
   ist gewollt: ADR 0013 erlaubt kein Umschreiben des Zustands ohne ein Ereignis, das ihn
   herleitet, und ein eigenes "Umbenennung beobachtet"-Ereignis an jeder betroffenen Buchung
   wäre genau der Lärm, den dieser ADR beseitigen soll (siehe Alternatives).

3. **Die Kassenbuch-Liste löst den Blattnamen live aus `ledger_categories` auf, mit
   Rückfall auf den gespeicherten Namen.** `Ledger.QueryHandlers.ReadModel.toEntryView`
   bekommt eine Namens-Zuordnung (`CategoryId -> string`, aus `ledger_categories.name`,
   analog zu `loadCategoryGroups`) und löst damit sowohl den Kopf als auch jedes
   `ToCategory`-Split-Ziel auf. Kein Treffer, ein leerer Name nach `Trim()`, oder eine noch
   leere Tabelle (frischer Deploy, kein Spiegel-Lauf gelaufen): dann gilt der gespeicherte
   Name, wie bisher. Damit gilt dieselbe Linie wie ADR 0039 Punkt 2 jetzt auch für den
   Blattnamen: nach einer reinen Umbenennung zeigt die Liste sofort "Alltag: Essen", nicht
   "Alltag: Lebensmittel" (heutige Gruppe, veralteter Blattname). Beide Aufrufer von
   `toEntryView` (`getLedgerEntriesPage`, `getLedgerEntriesForAccount`) reichen die neue
   Zuordnung durch.

4. **Die Historien-Sätze (`QueryHandlers.describeCategorizationChange` und Geschwister)
   unterscheiden ebenfalls über die Identität, nicht über den Namen.** Ein Kopf-Unterschied
   wird nur behauptet, wenn sich `CategoryId` unterscheidet; Splits über dieselbe
   normalisierte Identität wie Punkt 1. Ein bereits gespeichertes Ereignis, das der Fehler
   vor diesem ADR erzeugt hat (gleiche Id, anderer Name, sonst keine Änderung), wird NICHT
   umgeschrieben und NICHT ausgeblendet (ADR 0013), aber ehrlich beschriftet: "Kategorie in
   YNAB umbenannt: Lebensmittel → Essen" statt des sonst irreführenden "Kategorie in YNAB
   geändert".

5. **Kein Backfill, keine neue Schema-Version.** `Events.fs` bleibt unverändert; die
   Entscheidung wirkt ausschließlich in `observe` (Vergleich), `toEntryView` (Anzeige) und
   den Historien-Satz-Funktionen (Beschriftung bestehender Ereignisse).

## Consequences

### Positive
- Eine reine Kategorie-Umbenennung in YNAB erzeugt ab jetzt kein einziges
  `EntryCategorizationChangeObservedInYnab`-Ereignis mehr, unabhängig von der Zahl der
  betroffenen Buchungen.
- Die Liste zeigt Gruppe und Blattname konsistent in der heutigen Fassung (ADR 0039 jetzt
  vollständig statt nur für die Gruppe), ohne gemischte Beschriftungen.
- Buchungen, bei denen YNAB keinen `category_name` geliefert hat und `observationDecoder`
  deshalb `""` gespeichert hat, bekommen über die Id wieder einen Namen, wie schon in ADR
  0039 für die Gruppe beschrieben.
- Kein neuer Abruf, keine neue Tabelle: `ledger_categories` existiert bereits (ADR 0039).

### Negative
- `OldCategory`/`OldSplits` in einem späteren, aus anderem Grund ausgelösten Ereignis
  können einen inzwischen in YNAB veralteten Namen tragen (den zuletzt beobachteten, nicht
  den heutigen). Die Historie spricht damit weiterhin in den Namen ihrer Zeit (ADR 0014),
  die Liste in den heutigen (ADR 0039/0044); dieselbe bewusste Trennung wie in ADR 0039.
- Nach einem frischen Deploy ohne gelaufenen Spiegel-Lauf zeigt die Liste bis zum ersten
  Lauf weiterhin die gespeicherten, nicht die aufgelösten Namen (`ledger_categories` ist
  leer, Rückfall greift).

### Neutral
- `SplitTarget.ToTransfer` trägt den Zielkonto-Namen (`accountName`) nach demselben Muster
  wie `ToCategory` den Kategorienamen; eine Konto-Umbenennung in YNAB erzeugt dieselbe
  Fehlerklasse, ist aber NICHT Teil dieses ADRs und von `ledger-ggzdw` ausdrücklich
  ausgenommen. Eigener Bug-Task, mit derselben Frage zur Live-Auflösung über
  `ledger_accounts`.
- `describeRecategorized` (BB-seitiger Pfad, Roman kategorisiert in BB) hat dasselbe
  Muster (volle Record-Gleichheit für die Satzbildung), ist aber nicht von diesem ADR
  normiert: BB setzt beim Umkategorisieren selbst immer einen frischen Namen, das Risiko
  falscher Sätze ist dort strukturell kleiner. Kann im selben Zug mit derselben Regel
  behandelt werden, ist aber nicht zwingend Teil von `ledger-ggzdw`.

## Alternatives considered

- **Ein eigenes, ausblendbares "Kategorie umbenannt"-Ereignis je Buchung** (nach dem
  Muster von `EntryClearedStateChangeObservedInYnab`): verworfen. Bei rund 14.000
  Buchungen stünde ein reines Stammdaten-Ereignis an jeder einzelnen betroffenen Buchung,
  genau der Lärm, den dieser ADR beseitigen soll. Eine Umbenennung ist zudem kein Ereignis
  an der Buchung, sondern am Stammdatum Kategorie.
- **Ein eigener beobachteter Kategorie-Katalog mit eigenem Log**, aus dem sich der
  Beschriftungs-Stand zu jedem Zeitpunkt rekonstruieren ließe: von ADR 0039 bereits als
  "der richtige Weg, falls der historische Name doch gebraucht wird" benannt. Für diesen
  ADR nicht gewählt, weil Roman für die Liste ausdrücklich die heutige Fassung will (wie
  bei der Gruppe), nicht die historische.
- **Gespeicherten Namen im Zustand per Backfill nachziehen**: ausgeschlossen durch ADR
  0013 (kein Umschreiben des Logs oder der daraus abgeleiteten Zeilen ohne ein Ereignis,
  das sie herleitet).
- **Liste zeigt weiterhin nur den gespeicherten Namen, keine Live-Auflösung des
  Blattnamens**: verworfen. Hätte den eigentlichen Bug nur halb behoben: die Liste zeigte
  dann die heutige Gruppe neben dem alten Blattnamen, ein neuer Mischzustand, den niemand
  gewollt hat, und zwei Buchungen derselben Kategorie hießen unterschiedlich, je nachdem,
  ob eine von ihnen zufällig später noch ein anderes Ereignis bekommen hat.

## References
- `src/Server/Ledger/Domain.fs` (`observe`, `categorizationIdentity`, `apply`)
- `src/Server/Ledger/Events.fs` (`CategoryRef`, `EntryCategorizationChangeObservedInYnabPayload`)
- `src/Shared/Domain.fs` (`SplitTarget`, `TransactionSplit`)
- `src/Server/Ledger/QueryHandlers.fs` (`ReadModel.toEntryView`, `describeCategorizationChange`,
  `describeRecategorized`, Aufrufer `getLedgerEntriesPage`/`getLedgerEntriesForAccount`)
- `src/Server/Ledger/Persistence.fs` (`loadCategoryGroups`, `loadCategoryNames`,
  `ledger_categories`)
- `src/Server/YnabObservations.fs` (Aufbau von `obs.Category`/Split-Zielen aus der
  jeweils aktuellen YNAB-Antwort)
- `.agentheim/knowledge/decisions/0039-kategoriegruppe-im-lesepfad-statt-im-ereignis-log.md`
- `.agentheim/board/ledger/backlog/ledger-ggzdw-spiegel-kategorie-vergleich-nur-ueber-id.md`
