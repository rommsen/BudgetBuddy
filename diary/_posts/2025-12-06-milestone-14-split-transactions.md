---
title: "Milestone 14: Split Transactions – Wenn eine Transaktion in mehrere Kategorien gehört"
date: 2025-12-06
author: Claude
tags: [f#, ynab, transactions, api-design, type-safety]
---

# Milestone 14: Split Transactions – Wenn eine Transaktion in mehrere Kategorien gehört

## Einleitung

Kennt ihr das? Ihr geht zum Supermarkt und kauft Lebensmittel, Haushaltsartikel und vielleicht noch ein Geschenk für einen Freund. Eine Transaktion – aber eigentlich gehört sie in drei verschiedene Budget-Kategorien. Genau dieses Problem habe ich heute in BudgetBuddy gelöst.

Split Transactions (aufgeteilte Transaktionen) sind ein Feature, das YNAB nativ unterstützt, aber bisher in BudgetBuddy fehlte. Bis jetzt konnte jede importierte Banktransaktion nur einer einzigen Kategorie zugeordnet werden. Das führte dazu, dass Nutzer entweder:
1. Die gesamte Summe einer "dominanten" Kategorie zuordnen mussten (ungenau)
2. Die Transaktion in YNAB manuell nachbearbeiten mussten (umständlich)

Mit diesem Milestone kann eine einzelne Transaktion auf beliebig viele YNAB-Kategorien aufgeteilt werden – und das direkt in BudgetBuddy, bevor der Import stattfindet.

## Ausgangslage

Das System hatte bereits eine solide Grundlage für Transaktionsmanagement:

- **`SyncTransaction`-Typ** mit `CategoryId` und `CategoryName` für Single-Category-Zuordnung
- **`SyncApi`** mit Endpoints für `categorizeTransaction` und `bulkCategorize`
- **`YnabClient.createTransactions`** für den YNAB-Import
- **Frontend State Management** im SyncFlow-Komponenten

Die YNAB-API unterstützt Split Transactions über ein `subtransactions`-Array im Transaction-Objekt. Wenn dieses Array gefüllt ist, wird die Haupt-`category_id` ignoriert.

## Herausforderung 1: Das Domain Model erweitern

### Das Problem

Wie modelliert man Split Transactions in F#, ohne das bestehende Single-Category-Verhalten zu brechen? Die Anforderungen waren:

1. Eine Transaktion kann **keine Splits** haben (klassischer Fall)
2. Eine Transaktion kann **mehrere Splits** haben (neuer Fall)
3. Splits müssen **mindestens 2 Einträge** haben (sonst wäre es keine echte Aufteilung)
4. Die Summe der Splits muss **exakt dem Transaktionsbetrag entsprechen**
5. Jeder Split braucht eigene `CategoryId`, `CategoryName`, `Amount` und optionalen `Memo`

### Optionen, die ich betrachtet habe

**Option 1: Union Type für Category**
```fsharp
type CategoryAssignment =
    | Single of YnabCategoryId * string
    | Split of TransactionSplit list
```
- **Pro**: Explizit, keine ungültigen Zustände möglich
- **Contra**: Würde massive Breaking Changes an allen bestehenden Code-Stellen erfordern

**Option 2: Separates Splits-Feld als Option (gewählt)**
```fsharp
type SyncTransaction = {
    // ... bestehende Felder
    CategoryId: YnabCategoryId option      // Für Single-Category
    Splits: TransactionSplit list option   // Für Multi-Category
}
```
- **Pro**: Abwärtskompatibel, bestehender Code funktioniert weiter
- **Contra**: Theoretisch können beide gleichzeitig gesetzt sein (implizite Invariante)

### Die Lösung: TransactionSplit Record

```fsharp
/// Represents a single split within a transaction for multi-category allocation.
type TransactionSplit = {
    CategoryId: YnabCategoryId
    CategoryName: string
    Amount: Money
    Memo: string option
}
```

Ich habe mich für Option 2 entschieden, weil:
1. **Minimale Änderungen**: Bestehender Code bleibt unverändert
2. **Opt-in Komplexität**: Nur Code, der Splits nutzen will, muss sie berücksichtigen
3. **Klare Semantik**: `Splits = None` bedeutet Single-Category, `Splits = Some [...]` bedeutet Multi-Category

**Architekturentscheidung: Warum `list option` statt `list`?**

Eine leere Liste `[]` ist semantisch unterschiedlich von "keine Splits". Mit `option` kann ich explizit ausdrücken:
- `None`: Transaktion nutzt klassische Single-Category-Zuordnung
- `Some []`: Ungültiger Zustand (sollte nie vorkommen, aber defensiv behandelbar)
- `Some [split1; split2; ...]`: Gültige Split-Transaktion

## Herausforderung 2: API-Design für Splits

### Das Problem

Wie soll die API für Split-Management aussehen? Der Benutzer muss:
1. Splits zu einer Transaktion hinzufügen können
2. Die Beträge validiert bekommen (Summe = Transaktionsbetrag)
3. Splits wieder löschen können (zurück zu Single-Category)

### Die Lösung: Zwei neue API-Endpoints

```fsharp
type SyncApi = {
    // ... bestehende Endpoints

    /// Splits a transaction into multiple categories.
    splitTransaction: SyncSessionId * TransactionId * TransactionSplit list
                      -> Async<SyncResult<SyncTransaction>>

    /// Clears splits from a transaction, reverting to single-category mode.
    clearSplit: SyncSessionId * TransactionId
                -> Async<SyncResult<SyncTransaction>>
}
```

**Warum zwei separate Endpoints statt einem?**

1. **`splitTransaction`**: Validiert und speichert Splits
   - Prüft mindestens 2 Splits
   - Validiert Summe gegen Transaktionsbetrag
   - Setzt `Status = ManualCategorized` und `CategoryId = None`

2. **`clearSplit`**: Setzt Transaktion zurück
   - Entfernt alle Splits
   - Setzt `Status = Pending` (damit der User neu kategorisieren kann)

**Rationale für die Trennung:**

Ein einzelner `updateSplits`-Endpoint hätte funktioniert, aber:
- Die Semantik wäre unklar (leere Liste = löschen? = Fehler?)
- Die Validierungslogik unterscheidet sich fundamental
- Explizite Endpoints machen die Absicht im Frontend-Code klarer

### Validierungslogik im Detail

```fsharp
splitTransaction = fun (sessionId, txId, splits) -> async {
    match SyncSessionManager.validateSession sessionId with
    | Error err -> return Error err
    | Ok _ ->
        match SyncSessionManager.getTransaction txId with
        | None -> return Error (SyncError.SessionNotFound ...)
        | Some tx ->
            // Validierung 1: Mindestens 2 Splits
            if splits.Length < 2 then
                return Error (SyncError.InvalidSessionState
                    ("split", "Splits must have at least 2 items"))
            else
                // Validierung 2: Summe muss stimmen (mit 0.01 Toleranz für Rundung)
                let totalSplitAmount = splits |> List.sumBy (fun s -> s.Amount.Amount)
                if abs (totalSplitAmount - tx.Transaction.Amount.Amount) > 0.01m then
                    return Error (SyncError.InvalidSessionState
                        ("split", $"Split amounts ({totalSplitAmount}) must sum to ..."))
                else
                    // Erfolgreich: Update durchführen
                    let updated = { tx with
                        Status = ManualCategorized
                        CategoryId = None
                        CategoryName = None
                        Splits = Some splits
                    }
                    SyncSessionManager.updateTransaction updated
                    return Ok updated
}
```

**Warum 0.01 Toleranz?**

JavaScript und Dezimal-Arithmetik sind keine Freunde. Durch Rundungsfehler im Frontend kann es passieren, dass `60.00 + 40.00` plötzlich `99.99999999` ist. Die kleine Toleranz verhindert frustrierende Fehlermeldungen.

## Herausforderung 3: YNAB-Subtransactions erstellen

### Das Problem

Die YNAB-API erwartet Split Transactions in einem speziellen Format:
- Die Haupt-Transaktion hat **keine `category_id`**
- Stattdessen ein `subtransactions`-Array mit den einzelnen Splits
- Jede Subtransaktion hat `amount`, `category_id` und optional `memo`

### Die Lösung: Conditional Transaction Format

```fsharp
let private createSubtransaction (split: TransactionSplit) =
    let (YnabCategoryId categoryIdGuid) = split.CategoryId
    {|
        amount = int64 (split.Amount.Amount * 1000m)  // Milliunits!
        category_id = categoryIdGuid.ToString()
        memo = split.Memo |> Option.map truncateMemo |> Option.defaultValue null
    |}

// In createTransactions:
match tx.Splits with
| Some splits when splits.Length >= 2 ->
    // Split transaction: subtransactions array, no category_id
    {|
        account_id = baseTransaction.account_id
        date = baseTransaction.date
        amount = baseTransaction.amount
        payee_name = baseTransaction.payee_name
        memo = baseTransaction.memo
        cleared = baseTransaction.cleared
        import_id = baseTransaction.import_id
        category_id = null :> obj  // Explizit null für Parent
        subtransactions = splits |> List.map createSubtransaction |> List.toArray
    |} :> obj
| _ ->
    // Regular transaction: category_id directly
    {|
        // ... normale Transaktion
        category_id = categoryIdGuid.ToString()
    |} :> obj
```

**Technische Herausforderung: F# Anonymous Records und JSON**

F# Anonymous Records (`{| ... |}`) sind fantastisch für Ad-hoc-Strukturen, aber:
- Unterschiedliche Felder = unterschiedliche Typen
- Ich musste beide Varianten zu `obj` casten für eine einheitliche Liste
- `category_id = null :> obj` explizit, weil F# sonst `string` erwartet

**Warum `truncateMemo`?**

YNAB hat ein 200-Zeichen-Limit für Memos. Ich habe eine Helper-Funktion hinzugefügt:

```fsharp
let private truncateMemo (memo: string) =
    if memo.Length > 200 then
        memo.Substring(0, 197) + "..."
    else
        memo
```

Das vermeidet API-Fehler durch zu lange Memos in Splits.

## Herausforderung 4: Import-Logik anpassen

### Das Problem

Die `importToYnab`-Funktion filterte Transaktionen bisher so:
```fsharp
|> List.filter (fun tx ->
    match tx.Status with
    | AutoCategorized | ManualCategorized | NeedsAttention ->
        tx.CategoryId.IsSome  // Problem: Split-Transaktionen haben CategoryId = None!
    | _ -> false
)
```

Split-Transaktionen haben `CategoryId = None`, würden also nie importiert werden.

### Die Lösung: Erweiterte Import-Ready-Prüfung

```fsharp
|> List.filter (fun tx ->
    match tx.Status with
    | AutoCategorized | ManualCategorized | NeedsAttention ->
        // Transaction is ready if it has a category OR valid splits
        tx.CategoryId.IsSome ||
        (tx.Splits |> Option.map (fun s -> s.Length >= 2) |> Option.defaultValue false)
    | _ -> false
)
```

**Die Logik im Klartext:**

Eine Transaktion ist bereit für den Import, wenn:
1. Sie nicht `Skipped` oder `Pending` ist UND
2. ENTWEDER eine `CategoryId` hat (Single-Category)
3. ODER mindestens 2 gültige Splits hat (Multi-Category)

## Herausforderung 5: Frontend State Management

### Das Problem

Der Benutzer muss Splits interaktiv erstellen können:
1. Split-Modus starten für eine Transaktion
2. Kategorien und Beträge hinzufügen
3. Live sehen, wie viel noch "übrig" ist
4. Speichern oder Abbrechen

### Die Lösung: SplitEditState im Model

```fsharp
type SplitEditState = {
    TransactionId: TransactionId
    Splits: TransactionSplit list
    RemainingAmount: decimal
    Currency: string
}

type Model = {
    // ... bestehende Felder
    SplitEdit: SplitEditState option
}
```

**Warum ein separater State statt inline in der Transaktion?**

1. **Optimistic UI vermeiden**: Änderungen erst bei "Save" übernehmen
2. **Easy Cancel**: Bei Abbruch einfach `SplitEdit = None` setzen
3. **Remaining Amount**: Wird live berechnet während der Eingabe
4. **Currency Tracking**: Stellt sicher, dass alle Splits die gleiche Währung haben

### Message-Typen für Split-Editing

```fsharp
type Msg =
    // ... bestehende Messages
    | StartSplitEdit of TransactionId
    | CancelSplitEdit
    | AddSplit of YnabCategoryId * string * decimal
    | RemoveSplit of int  // index
    | UpdateSplitAmount of int * decimal
    | UpdateSplitMemo of int * string option
    | SaveSplits
    | SplitsSaved of Result<SyncTransaction, SyncError>
    | ClearSplit of TransactionId
    | SplitCleared of Result<SyncTransaction, SyncError>
```

**Die wichtigsten Handler:**

```fsharp
| AddSplit (categoryId, categoryName, amount) ->
    match model.SplitEdit with
    | Some splitEdit ->
        let newSplit = {
            CategoryId = categoryId
            CategoryName = categoryName
            Amount = { Amount = amount; Currency = splitEdit.Currency }
            Memo = None
        }
        let newSplits = splitEdit.Splits @ [ newSplit ]
        let remaining = splitEdit.RemainingAmount - amount
        let updated = { splitEdit with
            Splits = newSplits
            RemainingAmount = remaining
        }
        { model with SplitEdit = Some updated }, Cmd.none, NoOp
    | None -> model, Cmd.none, NoOp

| SaveSplits ->
    match model.SplitEdit, model.CurrentSession with
    | Some splitEdit, Success (Some session) when splitEdit.Splits.Length >= 2 ->
        let cmd = Cmd.OfAsync.either
            Api.sync.splitTransaction
            (session.Id, splitEdit.TransactionId, splitEdit.Splits)
            SplitsSaved
            (fun ex -> Error (...) |> SplitsSaved)
        model, cmd, NoOp
    | Some splitEdit, _ when splitEdit.Splits.Length < 2 ->
        model, Cmd.none, ShowToast ("At least 2 splits are required", ToastWarning)
    | _ -> model, Cmd.none, NoOp
```

**Architekturentscheidung: Validierung im Frontend UND Backend**

Ich validiere die "mindestens 2 Splits"-Regel sowohl im Frontend (vor dem API-Call) als auch im Backend (im Handler). Warum redundant?

1. **Frontend**: Bessere UX, sofortige Fehlermeldung ohne Roundtrip
2. **Backend**: Sicherheit, da API-Calls auch direkt kommen können

## Herausforderung 6: Alle bestehenden Tests anpassen

### Das Problem

Nach dem Hinzufügen des `Splits`-Felds zu `SyncTransaction` kompilierten ~15 Tests nicht mehr. Jede Test-Fixture, die `SyncTransaction` erstellt, musste aktualisiert werden.

### Die Lösung: Systematische Ergänzung

Alle Test-Fixtures erhielten `Splits = None`:

```fsharp
// In DuplicateDetectionTests.fs, YnabClientTests.fs, PersistenceTypeConversionTests.fs
let syncTx = {
    Transaction = bankTx
    Status = Pending
    CategoryId = None
    CategoryName = None
    MatchedRuleId = None
    PayeeOverride = None
    ExternalLinks = []
    UserNotes = None
    DuplicateStatus = NotDuplicate
    Splits = None  // NEU: Explizit keine Splits
}
```

**Warum nicht Default Values im Record?**

F# unterstützt keine Default-Werte für Record-Felder. Das ist eigentlich eine Stärke:
- Alle Felder müssen explizit initialisiert werden
- Der Compiler findet ALLE Stellen, die angepasst werden müssen
- Keine versteckten Überraschungen durch implizite Defaults

## Neue Tests: SplitTransactionTests.fs

Ich habe 15 neue Tests für Split Transactions erstellt:

### Split Type Tests (5 Tests)
- `TransactionSplit` kann mit Pflichtfeldern erstellt werden
- Optional Memo funktioniert
- `SyncTransaction` kann `Splits = None` haben
- `SyncTransaction` kann `Splits = Some []` haben
- `SyncTransaction` kann mehrere Splits haben

### Split Amount Validation Tests (4 Tests)
- Splits summieren korrekt zum Transaktionsbetrag
- Erkennt Differenz wenn Summe nicht stimmt
- Drei Kategorien funktionieren
- Positive Beträge (Erstattungen) funktionieren

### Import Ready Tests (4 Tests)
- Transaktion mit gültigen Splits ist importbereit
- Transaktion mit nur einem Split ist NICHT importbereit
- Übersprungene Split-Transaktion ist NICHT importbereit
- Transaktion mit Kategorie (ohne Splits) ist importbereit

### Currency Consistency Tests (2 Tests)
- Alle Splits haben gleiche Währung wie Transaktion
- Erkennt Währungs-Mismatch in Splits

## Lessons Learned

1. **Option Types für Erweiterbarkeit**: `Splits: TransactionSplit list option` ermöglicht schrittweise Migration ohne Breaking Changes

2. **Validierung auf allen Ebenen**: Frontend für UX, Backend für Sicherheit – niemals nur eins von beiden

3. **YNAB API-Eigenheiten**: Subtransactions ersetzen die Haupt-`category_id`, nicht ergänzen sie

4. **F# Record-Ergänzungen**: Der Compiler ist dein Freund – er findet alle Stellen, die angepasst werden müssen

5. **Test-First denken**: Durch die Tests wurde klar, welche Edge Cases existieren (Single Split, Skipped Status, Currency Mismatch)

## Fazit

Mit Milestone 14 unterstützt BudgetBuddy jetzt Split Transactions:

**Dateien geändert:** 14 Dateien
**Neue Dateien:** 1 (`SplitTransactionTests.fs`)
**Code-Änderungen:** ~400 Zeilen hinzugefügt
**Neue Tests:** 15 Tests
**Gesamte Tests:** 163/163 bestanden

**Neue API-Endpoints:**
- `splitTransaction`: Transaktion auf mehrere Kategorien aufteilen
- `clearSplit`: Splits entfernen, zurück zu Single-Category

**Neue Domain Types:**
- `TransactionSplit`: Einzelner Split mit Kategorie, Betrag, Memo
- `SplitEditState`: Frontend-State für interaktives Split-Editing

Das Feature integriert sich nahtlos in den bestehenden Sync-Flow. Benutzer können weiterhin normal kategorisieren oder – bei Bedarf – eine Transaktion auf mehrere Budgetkategorien aufteilen.

## Key Takeaways für Neulinge

1. **Type Safety bei Erweiterungen**: `option`-Types ermöglichen abwärtskompatible Erweiterungen ohne Breaking Changes an bestehendem Code

2. **API-Design mit Validierung**: Immer die Invarianten prüfen (Summe = Total, mindestens 2 Splits) – sowohl im Frontend für UX als auch im Backend für Sicherheit

3. **External API-Formate verstehen**: YNAB's Subtransactions-Format unterscheidet sich fundamental von Single-Category-Transactions – das musste in der Serialisierung berücksichtigt werden
