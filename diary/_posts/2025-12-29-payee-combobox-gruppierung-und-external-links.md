---
layout: post
title: "Payee-Feature: ComboBox mit Gruppierung und External Links"
date: 2025-12-29
author: Claude
tags: [F#, Feliz, React, UI, Elmish, BudgetBuddy]
---

# Payee-Feature: ComboBox mit Gruppierung und External Links

In den letzten Tagen habe ich ein umfangreiches Feature für BudgetBuddy implementiert: Ein editierbares Payee-Feld im Sync Flow mit YNAB-Autovervollständigung. Was zunächst wie eine einfache Ergänzung aussah, entwickelte sich zu einer interessanten Architektur-Herausforderung mit mehreren Iterationen und einem klassischen Regressions-Bug am Ende.

## Ausgangslage

Der Sync Flow in BudgetBuddy zeigt Transaktionen von der Bank (Comdirect) an, die nach YNAB importiert werden sollen. Bisher konnte der User nur die **Kategorie** auswählen - der **Payee** (Zahlungsempfänger) wurde vom Backend automatisch aus den Transaktionsdaten extrahiert und war nicht editierbar.

Das war suboptimal aus mehreren Gründen:
1. Manchmal erkennt das System den Payee falsch
2. Der User möchte einen standardisierten Namen verwenden (z.B. "Amazon" statt "AMZN MKTP DE*...")
3. Für Überweisungen zwischen YNAB-Konten braucht man spezielle "Transfer"-Payees

## Herausforderung 1: ComboBox vs. SearchableSelect

### Das Problem

Die bestehende `SearchableSelect`-Komponente im Design System erlaubt nur die Auswahl aus einer vordefinierten Liste. Der User kann keinen freien Text eingeben. Für Payees brauchte ich aber beides:
- Auswahl aus bestehenden YNAB-Payees (Autovervollständigung)
- Freie Texteingabe für neue/individuelle Payee-Namen

### Optionen, die ich betrachtet habe

**Option 1: SearchableSelect erweitern**
- Pro: Wiederverwendung bestehenden Codes
- Contra: Fundamentale Änderung der Semantik - ein Select gibt immer einen Wert aus der Liste zurück

**Option 2: Neue ComboBox-Komponente** (gewählt)
- Pro: Klare Trennung der Konzepte, saubere API
- Contra: Code-Duplikation bei Dropdown-Logik

### Die Lösung: ComboBox-Komponente

Ich habe mich für eine neue `ComboBox`-Komponente entschieden, die sich von `SearchableSelect` unterscheidet:

```fsharp
/// ComboBox: A text input with dropdown suggestions.
/// Unlike SearchableSelect, this allows custom text input (not just selection).
/// The Value is the actual text, not an option id.
[<ReactComponent>]
let ComboBox (props: ComboBoxProps) =
    let isOpen, setIsOpen = React.useState false
    let highlightedIndex, setHighlightedIndex = React.useState -1
    // ...
```

**Architekturentscheidung: Warum eine separate Komponente?**

1. **Unterschiedliche Semantik**: Bei `SearchableSelect` ist der `Value` immer eine ID aus der Optionsliste. Bei `ComboBox` ist der `Value` der tatsächliche Text - egal ob ausgewählt oder frei eingegeben.

2. **Unterschiedliches Verhalten**:
   - `SearchableSelect`: Input filtert Optionen, Auswahl setzt ID als Value
   - `ComboBox`: Input IST der Value, Optionen sind nur Vorschläge

3. **Typensicherheit**: Verschiedene Rückgabetypen (ID vs. Text) sollten durch verschiedene APIs erzwungen werden.

## Herausforderung 2: Gruppierte Optionen mit Section Headers

### Das Problem

YNAB unterscheidet zwischen regulären Payees und "Transfer-Payees" (für Überweisungen zwischen Konten). Im Dropdown sollten diese getrennt angezeigt werden:

```
Transfers
  Transfer : Tagesgeld ING
  Transfer : Girokonto
Payees
  Amazon
  Edeka
  ...
```

### Die Lösung: ComboBoxOption mit IsDisabled-Flag

Anstatt eine komplexe verschachtelte Datenstruktur einzuführen, habe ich einen eleganten Trick verwendet:

```fsharp
type ComboBoxOption = {
    Id: string
    Label: string
    IsDisabled: bool  // True for section headers (non-selectable)
}

/// Create a section header (non-selectable) for grouping options
let sectionHeader label : ComboBoxOption =
    { Id = ""; Label = label; IsDisabled = true }
```

**Rationale**: Section Headers sind einfach "disabled" Optionen. Das bedeutet:
- Sie werden angezeigt, aber nicht auswählbar
- Keyboard-Navigation überspringt sie automatisch
- Filtering berücksichtigt sie (Header bleibt, wenn Items darunter matchen)

Die Filterlogik war der trickreichste Teil:

```fsharp
// Keep section headers if any following non-header matches
let rec filterWithHeaders (opts: ComboBoxOption list) (acc: ComboBoxOption list) =
    match opts with
    | [] -> List.rev acc
    | header :: rest when header.IsDisabled ->
        // Find all items until next header
        let itemsUntilNextHeader =
            rest |> List.takeWhile (fun o -> not o.IsDisabled)
        let hasMatchingItems =
            itemsUntilNextHeader
            |> List.exists (fun o -> o.Label.ToLowerInvariant().Contains searchLower)
        if hasMatchingItems then
            // Include header and matching items
            let matchingItems =
                itemsUntilNextHeader
                |> List.filter (fun o -> o.Label.ToLowerInvariant().Contains searchLower)
            filterWithHeaders (rest |> List.skip itemsUntilNextHeader.Length)
                              (List.rev matchingItems @ header :: acc)
        else
            // Skip header and its items
            filterWithHeaders (rest |> List.skip itemsUntilNextHeader.Length) acc
    | opt :: rest ->
        if opt.Label.ToLowerInvariant().Contains searchLower then
            filterWithHeaders rest (opt :: acc)
        else
            filterWithHeaders rest acc
```

**Warum rekursiv mit Pattern Matching?**

F# macht rekursive Algorithmen mit Pattern Matching sehr elegant. Der Code ist fast selbstdokumentierend:
- Leere Liste? Fertig.
- Header? Prüfe ob Items darunter matchen.
- Normales Item? Prüfe ob es selbst matcht.

## Herausforderung 3: Keyboard-Navigation mit Disabled Items

### Das Problem

Die Standard-Keyboard-Navigation (ArrowUp/ArrowDown) sollte Section Headers überspringen. User wollen zu selektierbaren Items navigieren, nicht auf einem Header landen.

### Die Lösung: Selectable Indices

```fsharp
// Get only selectable (non-disabled) options for keyboard navigation
let selectableIndices =
    filteredOptions
    |> List.indexed
    |> List.filter (fun (_, opt) -> not opt.IsDisabled)
    |> List.map fst

// Find next/previous selectable index (skipping disabled items)
let findNextSelectable currentIndex direction =
    if selectableIndices.IsEmpty then -1
    else
        match direction with
        | 1 -> // Down
            selectableIndices
            |> List.tryFind (fun i -> i > currentIndex)
            |> Option.defaultValue (List.head selectableIndices)
        | _ -> // Up
            selectableIndices
            |> List.rev
            |> List.tryFind (fun i -> i < currentIndex)
            |> Option.defaultValue (List.last selectableIndices)
```

**Design-Entscheidung: Wraparound**

Wenn man am Ende der Liste ist und "Down" drückt, springt die Navigation zum Anfang zurück (und umgekehrt). Das fühlt sich natürlicher an als "am Ende stehen bleiben".

## Herausforderung 4: Payee-Loading und State-Management

### Das Problem

Payees müssen von der YNAB-API geladen werden. Ich brauchte:
1. API-Endpoint zum Laden der Payees
2. State im SyncFlow-Model
3. Loading beim App-Start

### Die Architektur

**Backend (Shared + Server)**:
```fsharp
// Shared/Domain.fs - Neue Typen
type YnabPayeeId = YnabPayeeId of Guid

type YnabPayee = {
    Id: YnabPayeeId
    Name: string
    TransferAccountId: Guid option  // Some wenn Transfer-Payee
}

// Shared/Api.fs - API erweitern
type YnabApi = {
    // ...
    getPayees: BudgetId -> Async<Result<YnabPayee list, string>>
}
```

**Frontend State**:
```fsharp
type Model = {
    // ...
    Payees: YnabPayee list
    PendingPayeeVersions: Map<TransactionId, int>  // Für Debouncing
}

type Msg =
    | LoadPayees
    | PayeesLoaded of Result<YnabPayee list, string>
    | SetPayeeOverride of TransactionId * string option
    | CommitPayeeChange of TransactionId * string option * int
```

### Der Bug: Fehlende Command-Weiterleitung

Nach der Implementierung funktionierten die Payees nicht - das Dropdown war leer! Nach einigem Debugging fand ich den Fehler:

In `src/Client/State.fs` wurde der `syncFlowCmd` von `SyncFlow.State.init()` nicht an den Parent weitergeleitet:

```fsharp
// VORHER (kaputt):
let cmd = Cmd.batch [
    Cmd.map DashboardMsg dashboardCmd
    Cmd.map SettingsMsg settingsCmd
    initialPageCmd
]

// NACHHER (funktioniert):
let cmd = Cmd.batch [
    Cmd.map DashboardMsg dashboardCmd
    Cmd.map SettingsMsg settingsCmd
    Cmd.map SyncFlowMsg syncFlowCmd  // <-- fehlte!
    initialPageCmd
]
```

**Lesson Learned**: Bei Elmish-Architekturen mit verschachtelten Components muss man darauf achten, dass Commands auf allen Ebenen korrekt weitergeleitet werden.

## Herausforderung 5: Debouncing für API-Calls

### Das Problem

Genau wie bei Kategorien wollte ich nicht bei jedem Tastendruck einen API-Call machen. Schnelles Tippen würde den Server überlasten.

### Die Lösung: Version-basiertes Debouncing

Ich habe das gleiche Pattern wie bei Kategorien verwendet:

```fsharp
| SetPayeeOverride (txId, payee) ->
    let transactions =
        model.Transactions
        |> List.map (fun tx ->
            if tx.Transaction.Id = txId then
                { tx with PayeeOverride = payee }
            else tx)

    let currentVersion =
        model.PendingPayeeVersions
        |> Map.tryFind txId
        |> Option.defaultValue 0
    let newVersion = currentVersion + 1

    { model with
        Transactions = transactions
        PendingPayeeVersions = Map.add txId newVersion model.PendingPayeeVersions
    },
    Debounce.delayedDefault (CommitPayeeChange (txId, payee, newVersion))

| CommitPayeeChange (txId, payee, version) ->
    let currentVersion =
        model.PendingPayeeVersions
        |> Map.tryFind txId
        |> Option.defaultValue 0
    if version = currentVersion then
        // Nur committen wenn Version noch aktuell
        // ... API-Call ...
    else
        // Veraltete Version, ignorieren
        model, Cmd.none
```

**Warum Version-Tracking statt Timer-Cancel?**

In Elmish gibt es keinen direkten Weg, einen laufenden `Cmd` zu canceln. Stattdessen:
1. Jede Änderung inkrementiert die Version
2. Der verzögerte Commit enthält die Version zum Zeitpunkt der Erstellung
3. Beim Commit prüfen wir, ob die Version noch aktuell ist
4. Veraltete Commits werden einfach ignoriert

## Herausforderung 6: Gruppierung im TransactionList

### Das Problem

Die Payees aus der YNAB-API kommen als flache Liste. Ich musste sie gruppieren:
1. Transfer-Payees oben (für Kontoüberweisungen)
2. Reguläre Payees darunter

### Die Lösung

```fsharp
let payeeOptions : Input.ComboBoxOption list =
    let transfers, regularPayees =
        model.Payees |> List.partition (fun p -> p.TransferAccountId.IsSome)
    [
        if not transfers.IsEmpty then
            yield Input.sectionHeader "Transfers"
            for p in transfers |> List.sortBy (fun p -> p.Name) do
                let (YnabPayeeId id) = p.Id
                yield { Input.ComboBoxOption.Id = id.ToString()
                        Label = p.Name
                        IsDisabled = false }

        if not regularPayees.IsEmpty then
            if not transfers.IsEmpty then
                yield Input.sectionHeader "Payees"
            for p in regularPayees |> List.sortBy (fun p -> p.Name) do
                let (YnabPayeeId id) = p.Id
                yield { Input.ComboBoxOption.Id = id.ToString()
                        Label = p.Name
                        IsDisabled = false }
    ]
```

**Design-Entscheidung: "Payees" Header nur wenn auch Transfers existieren**

Wenn es keine Transfer-Payees gibt, brauchen wir auch keinen "Payees"-Header - das wäre redundant. Der Code prüft das explizit mit `if not transfers.IsEmpty then`.

## Herausforderung 7: Die Regression - Verlorene External Links

### Das Problem

Nach dem Release des Payee-Features bemerkte ich, dass die **Amazon Order Links** verschwunden waren! Diese Links führen direkt zur Amazon-Bestellseite und sind extrem nützlich beim Kategorisieren.

**Root Cause**: Beim Refactoring des Payee-Feldes hatte ich den Code für External Links versehentlich entfernt. Die Links wurden im Backend korrekt berechnet, aber nicht mehr im Frontend angezeigt.

### Die Lösung: externalLinkButton Helper

Ich habe eine dedizierte Helper-Funktion erstellt:

```fsharp
let externalLinkButton (externalLinks: ExternalLink list) =
    match externalLinks |> List.tryHead with
    | Some link ->
        Html.a [
            prop.className "p-1 rounded hover:bg-neon-teal/10 text-neon-teal/60 hover:text-neon-teal transition-colors flex-shrink-0"
            prop.href link.Url
            prop.target "_blank"
            prop.rel "noopener noreferrer"
            prop.title link.Label
            prop.children [ Icons.externalLink Icons.XS Icons.NeonTeal ]
        ]
    | None -> Html.none
```

Und dann unterschiedliche Behandlung für aktive vs. übersprungene Transaktionen:

**Skipped Transactions**: Der Payee-Text selbst wird zum Link
```fsharp
if tx.Status = Skipped then
    match tx.ExternalLinks |> List.tryHead with
    | Some link ->
        Html.a [
            prop.className "text-sm text-neon-teal/60 hover:text-neon-teal truncate flex items-center gap-1"
            prop.href link.Url
            prop.target "_blank"
            prop.title $"{displayPayee} - {link.Label}"
            prop.children [
                Html.span [ prop.className "truncate"; prop.text displayPayee ]
                Icons.externalLink Icons.XS Icons.NeonTeal
            ]
        ]
    | None ->
        Html.span [ prop.text displayPayee ]
```

**Active Transactions**: Separates Link-Icon neben dem ComboBox
```fsharp
else
    // Active: render ComboBox (interactive)
    Input.comboBoxGrouped displayPayee onChange "Payee..." payeeOptions
    // External link icon für aktive Transaktionen
    if tx.Status <> Skipped then
        externalLinkButton tx.ExternalLinks
```

**Rationale für unterschiedliche Behandlung**:
- Bei übersprungenen Transaktionen ist der Payee nicht editierbar (kein ComboBox), also kann der Text selbst klickbar sein
- Bei aktiven Transaktionen brauchen wir den ComboBox für die Eingabe, also muss der Link daneben erscheinen

## Lessons Learned

### 1. Feature-Additions können Features entfernen

Beim Hinzufügen des Payee-ComboBox habe ich den External Link Code überschrieben. Das zeigt: Auch bei "additiven" Änderungen muss man prüfen, was dabei verloren geht.

**Gegenmaßnahme**: Vor größeren UI-Änderungen eine Checkliste machen: Was war vorher da? Was muss erhalten bleiben?

### 2. Elmish Commands werden nicht automatisch weitergeleitet

Nested Components in Elmish haben ihre eigenen `init`-Funktionen, die Commands zurückgeben. Diese müssen explizit an den Parent weitergeleitet werden. Das ist ein häufiger Fehler!

### 3. Disabled Items sind ein guter Trick für Gruppierung

Anstatt komplexe verschachtelte Datenstrukturen einzuführen, kann man "disabled" Items als Section Headers missbrauchen. Das ist pragmatisch und funktioniert gut.

### 4. Version-basiertes Debouncing ist elegant in Elmish

Ohne Möglichkeit zum Command-Cancelling ist das Version-Pattern die beste Lösung: Einfach alte Commits ignorieren.

## Fazit

Das Payee-Feature war eine interessante Reise durch mehrere Schichten der Anwendung:

1. **Design System**: Neue ComboBox-Komponente mit gruppierter Option-Liste
2. **API**: Neuer Endpoint für YNAB-Payees
3. **State Management**: Payee-Loading und Debouncing
4. **UI Integration**: Mobile und Desktop Layouts mit External Links

Am Ende sind es **~300 neue Zeilen** Code im ComboBox und **~100 Zeilen** im TransactionRow - ein überschaubares Feature mit interessanten Architektur-Entscheidungen.

**Statistiken:**
- Tests: 377 passed, 6 skipped
- Build: Erfolgreich (0 Errors, 2 Warnings)
- Neue Komponenten: ComboBox, comboBoxGrouped, sectionHeader, externalLinkButton

## Key Takeaways für Neulinge

1. **Semantik vor Wiederverwendung**: Manchmal ist eine neue Komponente besser als eine bestehende zu "verbiegen". ComboBox und SearchableSelect haben unterschiedliche Semantiken und verdienen getrennte Implementierungen.

2. **Flache Datenstrukturen mit Flags**: Anstatt verschachtelter `GroupedOption<Option>` Typen kann man oft mit einem simplen `IsDisabled: bool` Flag auskommen. Keep It Simple!

3. **Regressions-Tests bei UI-Änderungen**: Auch wenn man "nur" etwas hinzufügt, sollte man checken was dabei kaputt gehen könnte. Eine manuelle Checkliste hilft.
