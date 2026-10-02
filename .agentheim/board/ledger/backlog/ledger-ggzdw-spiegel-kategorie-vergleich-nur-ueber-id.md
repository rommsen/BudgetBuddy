---
id: ledger-ggzdw
title: "Spiegel wertet eine Kategorie-Umbenennung in YNAB als Umkategorisierung jeder Buchung: observe vergleicht CategoryRef samt Name statt nur die Id"
status: backlog
type: bug
context: ledger
created: 2026-09-25
completed:
depends_on: []
blocks: []
tags: [ledger, spiegel, kassenbuch, historie]
related_adrs: [0013, 0014, 0039, 0044]
related_research: []
prior_art: [ledger-m8r2v, ledger-p3wnq]
---

## Why
`Ledger.Domain.observe` (`src/Server/Ledger/Domain.fs:514`) vergleicht `entry.Category <>
obs.Category` als volle Record-Gleichheit. `CategoryRef` (`src/Server/Ledger/Events.fs:66-69`)
trägt `CategoryId` und `CategoryName`. Benennt Roman eine Kategorie in YNAB um, ohne eine
einzige Buchung umzukategorisieren, schreibt der nächste Spiegel-Lauf für jede bereits
gespiegelte Buchung dieser Kategorie ein `EntryCategorizationChangeObservedInYnab`
("Kategorie in YNAB geändert: Lebensmittel → Essen"), einmal je Buchung, append-only für
immer (ADR 0013). Bei rund 14.000 Buchungen ist das potenziell die ergiebigste Quelle
falscher Historie-Zeilen im Kassenbuch. Gefunden vom tactical-modeler bei der Verfeinerung
von `ledger-tpbe7` am 2026-09-25; bewusst nicht dort mitgelöst, weil es jede Umbenennung
betrifft, nicht nur den Push-Zeitpunkt.

## What

**Entscheidung getroffen (2026-10-02, ADR 0044, scope: ledger).** Zusammenfassung, Details
und Begründung stehen in `.agentheim/knowledge/decisions/0044-kategorie-umbenennung-ist-keine-umkategorisierung.md`:

1. `observe` entscheidet über `EntryCategorizationChangeObservedInYnab` künftig über eine
   neue Funktion `Ledger.Domain.categorizationIdentity`, nicht mehr über volle
   Record-Gleichheit von `entry.Category`/`obs.Category` und `entry.Splits`/`obs.Splits`.
   `categorizationIdentity` bildet die Kopf-Kategorie auf `CategoryId option` ab (Name
   verworfen) und die Split-Zeilen auf eine Liste, in der jedes `ToCategory`-Ziel nur die
   `categoryId` behält (Name durch `""` ersetzt, NUR für den Vergleich); `ToTransfer` und
   `Uncategorized` bleiben unverändert (eine `ToTransfer`-Kontonamen-Umbenennung ist eine
   verwandte, aber andere Bug-Klasse, siehe Notes).

   ```fsharp
   let private splitTargetIdentity (target: SplitTarget) : SplitTarget =
       match target with
       | ToCategory (categoryId, _) -> ToCategory (categoryId, "")
       | other -> other

   let categorizationIdentity (category: CategoryRef option) (splits: TransactionSplit list) =
       category |> Option.map (fun c -> c.CategoryId),
       splits |> List.map (fun s -> { s with Target = splitTargetIdentity s.Target })

   let categorizationChanged =
       categorizationIdentity entry.Category entry.Splits
       <> categorizationIdentity obs.Category obs.Splits
   ```

   Löst ein ANDERER Grund ein Ereignis aus (andere Id, andere Split-Beträge, andere
   Reihenfolge, andere Memos), trägt der Payload weiterhin die vollen echten Werte samt
   Name (`OldCategory = entry.Category`, `NewCategory = obs.Category`, analog für Splits);
   nur die Ja/Nein-Entscheidung ist namens-blind, die gespeicherte Nutzlast nicht.

2. Der im Zustand gespeicherte Kategorie-Name bleibt nach einer reinen Umbenennung
   bewusst veraltet (kein Ereignis, kein Fold-Schritt, der ihn nachzieht), siehe ADR 0044
   Decision 2, kein eigenes "Umbenennung beobachtet"-Ereignis (wäre bei rund 14.000
   Buchungen derselbe Lärm, den dieser Task beseitigen soll).

3. **Die Kassenbuch-Liste löst den Blattnamen jetzt live aus `ledger_categories` auf**,
   analog zur Gruppe (ADR 0039), mit Rückfall auf den gespeicherten Namen, wenn die
   Kategorie dort fehlt oder der Name nach `Trim()` leer ist. Betroffen:
   `Ledger.QueryHandlers.ReadModel.toEntryView` (`src/Server/Ledger/QueryHandlers.fs:106-128`)
   bekommt eine Namens-Zuordnung (`CategoryId -> string`, aus `ledger_categories.name`) als
   zusätzlichen Parameter, zieht sie sowohl für `CategoryName` (Kopf) als auch für jedes
   `ToCategory`-Split-Ziel in `LedgerEntryView.Splits`. Beide Aufrufer
   (`getLedgerEntriesPage`, Zeile ~367-397; `getLedgerEntriesForAccount`, Zeile ~309-328)
   laden die Zuordnung zusätzlich und reichen sie durch. Konkrete Form (Lader-Funktion
   `loadCategoryNames`-artig, ein neuer kombinierter Lader `loadCategoryLabels`, der
   `loadCategoryGroups` ersetzt, oder zwei getrennte Maps) ist Umsetzungsdetail des
   Workers; wichtig ist nur: EINE Quelle (`ledger_categories`), live, mit Rückfall, für
   Kopf UND Split-Ziele gleichermaßen.

4. Die Historien-Satz-Funktionen in `QueryHandlers.fs` (`describeCategorizationChange`,
   Zeile ~169-188, ggf. sinngemäß `describeRecategorized`, Zeile ~212-234) unterscheiden
   für den Kopf künftig über `CategoryId`-Gleichheit, nicht über volle `CategoryRef`-
   Gleichheit; für Splits über dieselbe `categorizationIdentity`-Normalisierung. Ein
   bereits gespeichertes Ereignis mit gleicher Id, anderem Namen, sonst keiner Änderung
   (siehe Befund unten: aktuell keins vorhanden, aber zukünftig möglich an
   Bestandsdaten, falls YNAB vor dem Fix schon umbenannt hat und der Spiegel es erst
   jetzt einholt) wird NICHT umgeschrieben und NICHT ausgeblendet (ADR 0013), aber ehrlich
   beschriftet: "Kategorie in YNAB umbenannt: Lebensmittel → Essen", statt des sonst
   irreführenden "Kategorie in YNAB geändert: Lebensmittel → Essen".

Dasselbe gilt sinngemäß für Split-Zeilen: `entry.Splits <> obs.Splits` hätte ohne den Fix
auch bei einer reinen `ToCategory`-Namensänderung ausgelöst; mit `categorizationIdentity`
ist das behoben (Punkt 1).

**Explizit außerhalb dieses Tasks:** `SplitTarget.ToTransfer` trägt den Zielkonto-Namen
(`accountName`) nach demselben Muster; eine Konto-Umbenennung in YNAB erzeugt dieselbe
Fehlerklasse, ist aber NICHT Teil dieses Tasks und nicht Teil von ADR 0044. Eigener
Bug-Task, mit derselben Frage zur Live-Auflösung über `ledger_accounts`.

## Befund: bereits vorhandene falsche Rename-Ereignisse

Geprüft gegen die Kopie der Entwicklungsdatenbank (`dev-copy.db`, rein lesend, `sqlite3`):
insgesamt **2** gespeicherte `EntryCategorizationChangeObservedInYnab`-Ereignisse, **0**
davon reine Umbenennungen (gleiche `categoryId` bei `oldCategory`/`newCategory`,
unterschiedlicher Name, unveränderte Splits). Beide vorhandenen Ereignisse sind echte
Umkategorisierungen (unterschiedliche Kategorie-Ids). Es gibt also aktuell KEINE
Altlast, die AC "umbenannt"-Satz (Punkt 4 oben) im Bestand sichtbar macht; die neue
Beschriftung ist eine Vorsichtsmaßnahme für künftige Fälle, nicht eine Korrektur
bestehender Zeilen. Bereinigung bestehender (hier: keiner gefundenen) falschen Zeilen ist
NICHT Teil dieses Tasks (append-only, ADR 0013); diese Feststellung ist eine Notiz, keine
Maßnahme.

## Acceptance criteria
- [ ] **Kaputt, jetzt behoben:** GIVEN eine gespiegelte Buchung mit Kopf-Kategorie
      (Id X, Name "Lebensmittel") WHEN ein Snapshot mit (Id X, Name "Essen") beobachtet
      wird, sonst unverändert, THEN liefert `observe` eine leere Ereignisliste.
      Regressionstest in `src/Tests/LedgerMirrorTests.fs` (dort liegen die bestehenden
      `observe`-Tests), muss vor dem Fix rot sein.
- [ ] Dieselbe Regel für eine Split-Buchung: nur der Name eines `ToCategory`-Ziels
      ändert sich (gleiche `categoryId`, gleicher Betrag, gleiches Memo, gleiche
      Reihenfolge) → `observe` liefert eine leere Ereignisliste.
- [ ] Eine Buchung mit gespeichertem `CategoryName = ""` (YNAB lieferte beim ersten Sehen
      keinen Namen) und einem Snapshot mit nicht-leerem Namen, gleiche `CategoryId` →
      ebenfalls keine Ereignisse (deckt den Fall ab, dass ein leerer Name keine
      "Änderung" ist).
- [ ] **Intakt, nicht neu zu schreiben, nur zu prüfen:** eine andere Kategorie-Id löst
      weiterhin genau ein `EntryCategorizationChangeObservedInYnab` aus, Payload mit
      vollen, echten `CategoryRef`-Werten samt Name (`OldCategory`/`NewCategory`).
- [ ] **Intakt:** eine Split-Änderung bei Betrag, Memo, Anzahl oder Reihenfolge der
      Zeilen löst weiterhin ein Ereignis aus; eine `ToTransfer`-Zeile mit geändertem
      Kontonamen löst weiterhin ein Ereignis aus (bewusst unverändert, siehe "Explizit
      außerhalb dieses Tasks").
- [ ] **Intakt:** eine echte Split-Änderung bei GLEICHZEITIG umbenannter Kopf-Kategorie
      löst genau ein Ereignis aus (nicht zwei, nicht keins); die Payload trägt beide
      Kopf-Namen (alt und neu, auch wenn nur die Splits sich "wirklich" geändert haben),
      der Historien-Satz erwähnt keine Kopf-Änderung, weil die Kopf-Identität gleich
      blieb.
- [ ] `Ledger.QueryHandlers.ReadModel.toEntryView` löst `CategoryName` (Kopf) und jedes
      `ToCategory`-Split-Ziel live aus `ledger_categories` auf, mit Rückfall auf den
      gespeicherten Namen bei fehlendem oder leerem Treffer. `ToTransfer`/`Uncategorized`
      bleiben unverändert. Ohne Kategorie (`entry.Category = None`) bleibt `CategoryName =
      None`. Beide Aufrufer (`getLedgerEntriesPage`, `getLedgerEntriesForAccount`) reichen
      die Zuordnung durch; Tests für beide.
- [ ] Historien-Satz für ein (ggf. künstlich geseedetes) gespeichertes Ereignis mit
      gleicher `CategoryId`, unterschiedlichem Namen, unveränderten Splits: "Kategorie in
      YNAB umbenannt: A → B", nicht "Kategorie in YNAB geändert: A → B".
- [ ] Jeder neue Test trägt einen Kommentar, welchen Bug er verhindert bzw. welches
      Intakt-Verhalten er schützt (Bug-Fix-Protokoll).
- [ ] `dotnet build` und `dotnet test` grün.

## Notes
Erfasst am 2026-09-25 aus der Verfeinerung von `ledger-tpbe7` (Orchestrator, tactical-modeler).

Verfeinerung 2026-10-02 (Orchestrator, tactical-modeler, ADR 0044): die Live-Auflösung
des Blattnamens (AC `toEntryView`) ist kein optionales Extra, sondern notwendiger Teil
des Fixes. Ohne sie zeigt die Liste nach einer reinen Umbenennung dauerhaft die heutige
Gruppe (ADR 0039) neben dem alten Blattnamen, ein neuer, bisher nicht existierender
Mischzustand. Details und verworfene Alternativen (eigenes Umbenennung-Ereignis, eigener
Kategorie-Katalog mit Log, Liste bleibt beim gespeicherten Namen) stehen in ADR 0044.

`describeRecategorized` (BB-seitiger Pfad, Roman kategorisiert in BB) hat dasselbe Muster
wie `describeCategorizationChange`, ist aber nicht von ADR 0044 normiert: BB setzt beim
Umkategorisieren selbst immer einen frischen Namen, das Risiko falscher Sätze ist dort
strukturell kleiner. Kann im selben Zug mit derselben Regel behandelt werden
(empfohlen vom tactical-modeler), ist aber nicht Teil der Acceptance criteria oben; der
Worker darf es zusätzlich tun, muss aber nicht.

Die Signaturänderung von `toEntryView` betrifft alle bestehenden Tests, die die Funktion
direkt aufrufen (zusätzlicher Parameter): diese Tests müssen einmalig angepasst werden,
das ist kein eigenes Akzeptanzkriterium, sondern Teil von "dotnet test grün".
