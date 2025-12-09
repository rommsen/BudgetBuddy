---
title: "UI-Optimierungen: Kompakte Listen, Searchable Selects und Optimistic UI"
date: 2025-12-09
author: Claude
tags: [F#, Feliz, React, UI/UX, MVU, Performance]
---

# UI-Optimierungen: Kompakte Listen, Searchable Selects und Optimistic UI

Nach der Implementierung der Kernfunktionalität von BudgetBuddy – dem Sync zwischen Comdirect und YNAB – war es Zeit für einen UI-Polish-Pass. Die funktionalen Features waren da, aber die User Experience ließ zu wünschen übrig: Transaktionen nahmen zu viel Platz ein, Kategorie-Auswahl fühlte sich träge an, und die Rules-Verwaltung war umständlich. In diesem Blogpost beschreibe ich vier zusammenhängende Verbesserungen, die das UI deutlich responsiver und übersichtlicher machen.

## Ausgangslage

BudgetBuddy ist eine Self-Hosted Single-User-App zum Synchronisieren von Comdirect-Transaktionen nach YNAB. Die Frontend-Architektur folgt dem **MVU-Pattern** (Model-View-Update) mit Elmish und Feliz – eine funktionale Alternative zu Redux. Das bedeutet:

- **Model**: Immutable State-Record
- **Msg**: Discriminated Union für alle möglichen Events
- **Update**: Pure Function `Msg -> Model -> Model * Cmd<Msg>`
- **View**: Pure Function `Model -> ReactElement`

Diese Architektur hat Konsequenzen für das UI-Design: Jede Interaktion muss durch eine Message gehen, was anfangs mehr Boilerplate bedeutet, aber langfristig zu vorhersagbarem, testbarem Code führt.

---

## Herausforderung 1: Die unübersichtliche Transaktionsliste

### Das Problem

Die ursprüngliche Transaktionsliste verwendete Card-basierte Layouts mit circa 100-120px Höhe pro Transaktion. Bei einem typischen Sync mit 30-50 Transaktionen bedeutete das:
- Nur 4-6 Transaktionen auf dem Desktop sichtbar
- Ständiges Scrollen, um einen Überblick zu bekommen
- Schwer zu scannen: Wichtige Info (Status, Kategorie) war visuell nicht priorisiert

```
[Card Layout - vorher]
┌─────────────────────────────────────┐
│ ▸ Status Badge (groß)               │
│ Payee Name                          │
│ Memo: Lorem ipsum dolor sit amet... │
│ ────────────────────────────────    │
│ [Kategorie Dropdown]    €-123.45    │
│ [Skip] [Split] [External Link]      │
└─────────────────────────────────────┘
```

### Die gewählte Lösung: Table-artige Zeilen

Ich habe mich für ein kompaktes, table-artiges Layout entschieden mit **44px Höhe auf Desktop** und **72px auf Mobile** (zwei Zeilen). Der Schlüssel war, die visuelle Hierarchie radikal zu vereinfachen:

```
[Neues Layout - Desktop]
[▸] [●] [Kategorie ▼         ] [Payee...  ] [12.12] [€-123.45] [⏭][↗]

[Neues Layout - Mobile]
Line 1: [▸] [●] [Kategorie ▼              ] [€-123.45]
Line 2:     [Payee...          ] [12.12] [⏭][↗]
```

**Architekturentscheidung: Status als Dots statt Badges**

Die großen Status-Badges ("Auto-Categorized", "Pending Review", etc.) wurden durch 8px farbige Dots ersetzt:

```fsharp
/// Status dot - small colored indicator based on transaction state
/// Priority: Skipped > DuplicateStatus > TransactionStatus
let private statusDot (tx: SyncTransaction) =
    let (dotColor, shouldPulse) =
        // Skipped always shows gray, regardless of duplicate status
        if tx.Status = Skipped then
            ("bg-base-content/30", false)
        else
            match tx.DuplicateStatus with
            | ConfirmedDuplicate _ -> ("bg-neon-red", false)
            | PossibleDuplicate _ -> ("bg-neon-orange", true)
            | NotDuplicate ->
                match tx.Status with
                | Pending | NeedsAttention -> ("bg-neon-orange", false)
                | AutoCategorized -> ("bg-neon-teal", false)
                | ManualCategorized -> ("bg-neon-green", false)
                | Skipped -> ("bg-base-content/30", false)
                | Imported -> ("bg-neon-green", false)

    let pulseClass = if shouldPulse then "animate-pulse" else ""
    Html.div [
        prop.className $"w-2 h-2 rounded-full flex-shrink-0 {dotColor} {pulseClass}"
    ]
```

**Warum diese Prioritätsreihenfolge?**

1. **Skipped hat höchste Priorität**: Wenn der User eine Transaktion übersprungen hat, ist das eine bewusste Entscheidung. Selbst wenn es ein Duplicate ist, sollte die Zeile grau bleiben.
2. **DuplicateStatus vor TransactionStatus**: Duplicates sind kritisch – sie können zu doppelten Buchungen in YNAB führen. Das muss sofort sichtbar sein.
3. **Pending und NeedsAttention gleiche Farbe**: Ursprünglich hatte ich unterschiedliche Farben, aber das führte zu Verwirrung. Beide bedeuten "braucht Aufmerksamkeit", also gleiche Farbe.

### Der Fable-Transpilations-Bug

Während der Implementierung stieß ich auf einen subtilen Bug bei der Betragsformatierung. In F# schreibt man:

```fsharp
sprintf "%.2f" amount  // oder
$"{amount:F2}"
```

Fable transpiliert das zu JavaScript, aber `:F2` wird zu einem Platzhalter `%P(F2)` statt zum erwarteten `.toFixed(2)`. Das führte zu Beträgen wie `-25.990000000000002` statt `-25.99`.

**Die Lösung:**

```fsharp
/// Format amount with proper decimal places
/// NOTE: Using explicit Math.Round because Fable transpiles F# format specifiers
/// like :F2 to %P(F2) placeholder instead of .toFixed(2)
let formatAmount (amount: decimal) : string =
    let absAmount = abs amount
    let sign = if amount < 0m then "-" else ""
    let rounded = System.Math.Round(float absAmount, 2)
    $"{sign}{rounded.ToString(\"0.00\")}"
```

### Expandierbare Memo-Zeilen

Das neue kompakte Layout hatte keinen Platz mehr für den Memo-Text. Aber Memos sind wichtig für die Kategorisierung – oft steht dort der eigentliche Verwendungszweck. Die Lösung: Ein Expand/Collapse-Chevron links vom Status-Dot.

**Neuer State im Model:**

```fsharp
// In Types.fs
type Model = {
    // ... existing fields
    ExpandedTransactionIds: Set<TransactionId>
}

type Msg =
    // ... existing messages
    | ToggleTransactionExpand of TransactionId
```

**Update-Handler:**

```fsharp
| ToggleTransactionExpand txId ->
    let newExpanded =
        if model.ExpandedTransactionIds.Contains txId then
            model.ExpandedTransactionIds.Remove txId
        else
            model.ExpandedTransactionIds.Add txId
    { model with ExpandedTransactionIds = newExpanded }, Cmd.none, NoOp
```

**View-Komponente:**

```fsharp
let private expandChevron (tx: SyncTransaction) (isExpanded: bool) (dispatch: Msg -> unit) =
    let hasExpandableContent = not (System.String.IsNullOrWhiteSpace tx.Transaction.Memo)
    if hasExpandableContent then
        Html.button [
            prop.className "p-1 -ml-1 text-base-content/40 hover:text-base-content/70"
            prop.onClick (fun e ->
                e.stopPropagation()
                dispatch (ToggleTransactionExpand tx.Transaction.Id))
            prop.children [
                if isExpanded then Icons.chevronDown Icons.XS Icons.Default
                else Icons.chevronRight Icons.XS Icons.Default
            ]
        ]
    else
        Html.div [ prop.className "w-4 flex-shrink-0" ]  // Placeholder für Alignment
```

**Warum `e.stopPropagation()`?**

Ohne `stopPropagation()` würde ein Klick auf den Chevron auch die Row selbst triggern, falls dort ein `onClick` registriert ist. Defensive Programmierung.

### Ergebnis

- **~2.5x mehr Transaktionen auf Desktop sichtbar**
- **~1.5x mehr Transaktionen auf Mobile sichtbar**
- Klarer Scan-Pfad: Status → Kategorie → Details → Betrag
- Memo-Details on-demand verfügbar

---

## Herausforderung 2: Langsame Kategorie-Auswahl

### Das Problem

Bei der Auswahl einer Kategorie aus dem Dropdown dauerte es fast eine Sekunde, bis die Auswahl sichtbar wurde. Das fühlte sich träge und unresponsiv an – besonders frustrierend bei 30+ Transaktionen.

**Root Cause:**

```fsharp
// VORHER: Pessimistisches UI
| CategorizeTransaction (txId, categoryId) ->
    // Nur API-Call starten, Model NICHT aktualisieren
    let cmd = Cmd.OfAsync.either Api.sync.categorizeTransaction ...
    model, cmd, NoOp  // ← Model bleibt unverändert!

| TransactionCategorized (Ok updatedTx) ->
    // Erst HIER wird das Model aktualisiert (nach 500-1000ms)
    let newTxs = transactions |> List.map (fun tx ->
        if tx.Transaction.Id = updatedTx.Transaction.Id then updatedTx else tx)
    { model with SyncTransactions = Success newTxs }, Cmd.none, NoOp
```

Das UI wartete auf die Backend-Antwort bevor es sich aktualisierte – "pessimistisches UI".

### Die gewählte Lösung: Optimistic UI

**Optimistic UI** bedeutet: Das Model wird sofort lokal aktualisiert, der API-Call läuft im Hintergrund. Bei Erfolg: nichts zu tun. Bei Fehler: Rollback.

```fsharp
| CategorizeTransaction (txId, categoryId) ->
    match model.CurrentSession, model.SyncTransactions with
    | Success (Some session), Success transactions ->
        // OPTIMISTIC UI: Update locally first for instant feedback
        let updatedTransactions =
            transactions
            |> List.map (fun tx ->
                if tx.Transaction.Id = txId then
                    // Find category name for display
                    let categoryName =
                        categoryId
                        |> Option.bind (fun catId ->
                            model.Categories |> List.tryFind (fun c -> c.Id = catId))
                        |> Option.map (fun c -> $"{c.GroupName}: {c.Name}")
                    // Update status based on category selection
                    let newStatus =
                        match tx.Status, categoryId with
                        | Skipped, _ -> Skipped  // Keep skipped status
                        | _, Some _ -> ManualCategorized
                        | _, None -> Pending
                    { tx with
                        CategoryId = categoryId
                        CategoryName = categoryName
                        Status = newStatus
                        Splits = None }  // Clear splits when changing category
                else tx)

        let cmd = Cmd.OfAsync.either Api.sync.categorizeTransaction ...
        { model with SyncTransactions = Success updatedTransactions }, cmd, NoOp
```

**Rollback bei Fehler:**

```fsharp
| TransactionCategorized (Error err) ->
    // Rollback: reload transactions from server to restore correct state
    model, Cmd.ofMsg LoadTransactions, ShowToast (syncErrorToString err, ToastError)
```

### Architekturentscheidung: Warum Reload statt Undo?

Ich hätte auch den vorherigen Zustand speichern und bei Fehler wiederherstellen können. Aber:

1. **Komplexität**: Ein "Undo-Stack" für jede Transaktion wäre aufwändig
2. **Konsistenz**: Der Server ist die "Single Source of Truth" – bei Fehler will ich den echten Zustand, nicht meinen alten lokalen
3. **Seltenheit**: Kategorisierungs-Fehler sind selten (nur bei Netzwerk-Problemen oder wenn jemand parallel die Kategorie löscht)

Der Nachteil: Bei Fehler verschwinden alle lokalen Änderungen seit dem letzten Reload. Für diese App akzeptabel.

### Ergebnis

- Kategorie-Auswahl ist jetzt **instant** sichtbar
- Keine wahrnehmbare Verzögerung mehr
- Bei Fehler: Vollständiger Rollback durch Server-Reload

---

## Herausforderung 3: Searchable Category Select

### Das Problem

Die Standard-HTML-Selects (`<select>`) haben mehrere Probleme:
1. **Keine Suche**: Bei 100+ YNAB-Kategorien muss man ewig scrollen
2. **Kein Keyboard-Support**: Arrow-Keys funktionieren nur wenn das Select fokussiert ist
3. **Styling limitiert**: Native Selects lassen sich kaum stylen

### Die gewählte Lösung: Custom React Component

Ich habe eine vollständige `SearchableSelect`-Komponente in F#/Feliz implementiert:

```fsharp
[<ReactComponent>]
let SearchableSelect (props: SearchableSelectProps) =
    let isOpen, setIsOpen = React.useState false
    let searchText, setSearchText = React.useState ""
    let highlightedIndex, setHighlightedIndex = React.useState -1
    let isKeyboardNav, setIsKeyboardNav = React.useState false
    let containerRef = React.useRef<Browser.Types.HTMLElement option> None
    let inputRef = React.useRef<Browser.Types.HTMLInputElement option> None
    let listRef = React.useRef<Browser.Types.HTMLElement option> None
```

**Features:**
- **Type-to-filter**: Case-insensitive "contains" Suche
- **Keyboard-Navigation**: ↑↓ zum Navigieren, Enter zum Auswählen, Escape zum Schließen
- **Click-outside Detection**: Schließt beim Klick außerhalb
- **Auto-Focus**: Suchfeld bekommt sofort Fokus beim Öffnen

### Der Scroll-Bug in Modals

Während der Implementierung entdeckte ich einen subtilen Bug: Beim Hovern über Optionen scrollte nicht nur die Dropdown-Liste, sondern das gesamte Modal/Fenster.

**Root Cause:**

```fsharp
// Naive Implementierung (kaputt)
React.useEffect (fun () ->
    if highlightedIndex >= 0 then
        let item = items.[highlightedIndex]
        item.scrollIntoView()  // ← Scrollt ALLES, nicht nur die Liste!
, [| highlightedIndex :> obj |])
```

`scrollIntoView()` scrollt alle übergeordneten Container, bis das Element sichtbar ist. Das ist unerwünscht.

**Die Lösung: Unterscheide Keyboard- und Maus-Navigation**

```fsharp
let isKeyboardNav, setIsKeyboardNav = React.useState false

// Scroll nur bei Keyboard-Navigation
React.useEffect (fun () ->
    if highlightedIndex >= 0 && isKeyboardNav then
        match listRef.current with
        | Some list ->
            let item = items.[highlightedIndex]
            // Manual scroll within list container only
            let itemTop = item.offsetTop
            let itemHeight = item.offsetHeight
            let listScrollTop = list.scrollTop
            let listHeight = list.clientHeight

            // Scroll up if item is above visible area
            if itemTop < listScrollTop then
                list.scrollTop <- itemTop
            // Scroll down if item is below visible area
            elif itemTop + itemHeight > listScrollTop + listHeight then
                list.scrollTop <- itemTop + itemHeight - listHeight
        | None -> ()
, [| highlightedIndex :> obj; isKeyboardNav :> obj |])
```

**Keyboard-Handler setzt `isKeyboardNav = true`:**

```fsharp
| "ArrowDown" ->
    e.preventDefault()
    setIsKeyboardNav true  // Mark as keyboard navigation
    let nextIndex =
        if highlightedIndex < totalItems - 1 then highlightedIndex + 1
        else 0  // Wrap to top
    setHighlightedIndex nextIndex
```

**Mouse-Handler setzt `isKeyboardNav = false`:**

```fsharp
let setHighlightFromMouse index =
    setIsKeyboardNav false
    setHighlightedIndex index
```

### Touch-Optimierung

Für Mobile-Devices habe ich die Dropdown-Optionen vergrößert:

```fsharp
// Vorher
prop.className "px-3 py-2 text-sm max-h-60"

// Nachher
prop.className "px-4 py-3 text-base max-h-80"
```

Das gibt größere Touch-Targets (mindestens 44x44px – Apple's Human Interface Guidelines).

### Ergebnis

- Schnelle Kategorie-Suche durch Tippen
- Vollständige Keyboard-Navigation
- Kein unerwünschtes Seiten-Scrollen
- Touch-optimierte Optionen

---

## Herausforderung 4: Rules-UI Redesign

### Das Problem

Die Rules-Verwaltung hatte zwei Probleme:
1. **Zu viel Platz**: Jede Rule war eine Card mit circa 80px Höhe
2. **Gefährliches Löschen**: Ein Klick auf das Trash-Icon löschte sofort – keine Bestätigung

### Die gewählte Lösung: Kompakte Zeilen + Inline-Bestätigung

**Neues Layout:**

```
[Toggle] [.*] [Rule Name...] → [Category...] [Edit][Delete]
```

**Pattern-Type als Icon:**

```fsharp
let private patternTypeIcon (patternType: PatternType) =
    let (icon, color, title) =
        match patternType with
        | PatternType.Regex -> (".*", "text-neon-purple", "Regex pattern")
        | Contains -> ("~", "text-neon-teal", "Contains substring")
        | Exact -> ("=", "text-neon-green", "Exact match")
    Html.span [
        prop.className $"font-mono text-[10px] font-bold {color} bg-white/5 px-1 rounded"
        prop.title title
        prop.text icon
    ]
```

Diese Icons sind selbsterklärend für Developer (`.` und `*` für Regex, `~` für "enthält", `=` für "exakt"), aber für Nicht-Developer habe ich eine Legende im Info-Tooltip hinzugefügt.

### Inline-Löschbestätigung (MVU-konform)

Statt eines Confirmation-Modals wollte ich eine inline Bestätigung: Klick auf Trash → Button wird rot und zeigt "Löschen?" → Nach 3 Sekunden resettet er automatisch.

**Die Herausforderung**: In MVU gibt es keinen lokalen Component-State. Alles muss durch den zentralen Model-State gehen.

**Neuer State:**

```fsharp
// Types.fs
type Model = {
    // ... existing
    ConfirmingDeleteRuleId: RuleId option
}

type Msg =
    | ConfirmDeleteRule of RuleId    // First click - show confirm
    | CancelConfirmDelete            // Timeout - hide confirm
    | DeleteRule of RuleId           // Second click - actually delete
```

**Update-Handler:**

```fsharp
| ConfirmDeleteRule ruleId ->
    // Show confirm button and start 3 second timeout
    let timeoutCmd =
        Cmd.OfAsync.perform
            (fun () -> async { do! Async.Sleep 3000 })
            ()
            (fun () -> CancelConfirmDelete)
    { model with ConfirmingDeleteRuleId = Some ruleId }, timeoutCmd, NoOp

| CancelConfirmDelete ->
    // Timeout expired - hide confirm button
    { model with ConfirmingDeleteRuleId = None }, Cmd.none, NoOp

| DeleteRule ruleId ->
    // Reset confirm state and actually delete
    let cmd = Cmd.OfAsync.either Api.rules.deleteRule ...
    { model with ConfirmingDeleteRuleId = None }, cmd, NoOp
```

**View:**

```fsharp
if isConfirmingDelete then
    // Red confirm button (auto-resets after 3s)
    Html.button [
        prop.className "btn btn-xs btn-error gap-1 animate-pulse"
        prop.onClick (fun _ -> dispatch (DeleteRule rule.Id))
        prop.children [
            Icons.trash XS Icons.IconColor.Primary
            Html.span [ prop.text "Löschen?" ]
        ]
    ]
else
    // Normal trash icon
    Button.iconButton (Icons.trash SM Icons.Error) Button.Ghost
        (fun () -> dispatch (ConfirmDeleteRule rule.Id))
```

**Warum nur eine Rule gleichzeitig?**

Das Model erlaubt nur `ConfirmingDeleteRuleId: RuleId option` – also maximal eine Rule im Confirm-Modus. Das ist beabsichtigt: Wenn der User auf das Trash-Icon einer anderen Rule klickt, wird die vorherige automatisch zurückgesetzt. Das verhindert verwirrende Zustände.

### Ergebnis

- Rules nehmen ~1/3 der vorherigen Höhe ein
- Mehr Rules auf einen Blick sichtbar
- Versehentliches Löschen durch Zwei-Klick-Mechanismus verhindert
- Vollständig MVU-konform – kein lokaler React-State

---

## Lessons Learned

### 1. Fable-Transpilation überprüfen

F#-Code, der in .NET funktioniert, kann in JavaScript anders funktionieren. Besonders bei:
- String-Formatierung (`:F2` etc.)
- Decimal-Arithmetik (JavaScript hat keine echten Decimals)
- Date/Time-Handling

**Tipp**: Im Browser-DevTools prüfen, was Fable tatsächlich generiert.

### 2. Optimistic UI braucht Rollback-Strategie

Optimistic UI fühlt sich fantastisch an, aber du brauchst einen Plan für Fehler:
- **Vollständiger Reload**: Einfach, aber verliert lokale Änderungen
- **Undo-Stack**: Komplex, aber granular
- **Partial Update**: Server schickt nur die geänderten Teile zurück

Für BudgetBuddy war Reload die richtige Wahl – Kategorisierungs-Fehler sind selten.

### 3. Keyboard- und Maus-Navigation separat behandeln

`scrollIntoView()` ist praktisch, aber scrollt zu aggressiv. Lösung:
- Track, ob Navigation via Keyboard oder Maus erfolgt
- Auto-Scroll nur bei Keyboard
- Maus-Hover aktualisiert Highlight, scrollt aber nicht

### 4. MVU für alles, auch "lokalen" State

Es ist verlockend, React's `useState` für "lokale" UI-States wie Confirm-Dialoge zu verwenden. Aber:
- Der State ist nicht serialisierbar (kein Time-Travel-Debugging)
- Schwerer zu testen
- Kann mit dem globalen State out-of-sync geraten

In MVU gehört auch temporärer UI-State ins Model.

---

## Fazit

Diese vier Verbesserungen – kompakte Transaktionsliste, Searchable Selects, Optimistic UI, und Inline-Delete-Bestätigung – haben die User Experience von BudgetBuddy deutlich verbessert:

| Metrik | Vorher | Nachher |
|--------|--------|---------|
| Sichtbare Transaktionen (Desktop) | 4-6 | 10-15 |
| Kategorie-Auswahl Latenz | ~800ms | ~0ms |
| Kategorie-Suche | Nicht möglich | Type-to-filter |
| Sichtbare Rules | 5-6 | 15-20 |
| Lösch-Schutz | Keiner | Zwei-Klick + Timeout |

**Geänderte Dateien:**
- `src/Client/DesignSystem/Input.fs` – SearchableSelect-Komponente
- `src/Client/DesignSystem/Money.fs` – Fable-kompatible Betragsformatierung
- `src/Client/Components/SyncFlow/Types.fs` – ExpandedTransactionIds State
- `src/Client/Components/SyncFlow/State.fs` – Optimistic UI + Expand-Handler
- `src/Client/Components/SyncFlow/View.fs` – Kompaktes Transaktions-Layout
- `src/Client/Components/Rules/Types.fs` – ConfirmingDeleteRuleId State
- `src/Client/Components/Rules/State.fs` – Inline-Delete-Handler mit Timeout
- `src/Client/Components/Rules/View.fs` – Kompaktes Rules-Layout

**Tests:** 279/279 passed, 6 skipped (Integration Tests)

---

## Key Takeaways

1. **Visuelle Dichte vs. Übersichtlichkeit**: Kompakte Layouts können mehr Informationen zeigen, ohne unübersichtlich zu werden – wenn die visuelle Hierarchie stimmt (Status-Dots statt Badges, Farben statt Text)

2. **Optimistic UI ist ein UX-Game-Changer**: Der Unterschied zwischen 800ms und 0ms Feedback ist gewaltig. Aber plane den Fehlerfall!

3. **MVU zwingt zu gutem Design**: Indem aller State im Model ist, wird die Anwendung vorhersagbar und testbar. Auch "temporäre" UI-States wie Confirm-Dialoge gehören dazu.
