/// Der Split-Editor im Kassenbuch (ledger-pafkp, ADR 0058, ADR 0006): der Entwurf und seine reinen
/// Rechen- und Prüffunktionen. Rest, Speichern-Sperre, die Zeilen für `setEntrySplits` und der
/// Rückweg für "Rückgängig" laufen über `Shared.Domain` (`splitRemainder`, `mkSplits`), nicht
/// über den Import-Ablauf. Beträge stehen im Editor als positive Beträge; das
/// Vorzeichen kommt vom Gesamtbetrag der Buchung.
module Components.Ledger.SplitEditor

open System
open Shared.Domain

/// Eine Zeile des Entwurfs. Der Betrag liegt als Text vor, damit Teileingaben möglich sind.
type SplitDraftLine = {
    /// Kategorie, Übertrag-Ziel oder "Ohne Kategorie"; `None`, solange nichts gewählt ist.
    Target: SplitTarget option
    AmountText: string
    Memo: string
}

/// Welche Auswahl über dem Editor offen steht und für welche Zeile.
type SplitPicker =
    | NoSplitPicker
    | SplitTargetPickerFor of lineIndex: int

/// Der Stand der Buchung vor dem Speichern: Grundlage für "Rückgängig".
type SplitBefore =
    /// Unaufgeteilt, mit einer Kategorie oder ohne.
    | WasPlain of CategoryChoice option
    /// Aufgeteilt, mit diesen Zeilen.
    | WasSplit of TransactionSplit list

type SplitEditorState = {
    EntryId: Guid
    /// Der Betrag der Buchung mit Vorzeichen.
    Total: Money
    Lines: SplitDraftLine list
    Picker: SplitPicker
    Before: SplitBefore
}

let emptyLine : SplitDraftLine = { Target = None; AmountText = ""; Memo = "" }

/// Ein Betrag als Eingabetext: positiver Betrag, Dezimalkomma, leer bei null.
let amountText (amount: decimal) : string =
    if amount = 0m then ""
    else (abs amount).ToString("0.00", Globalization.CultureInfo.InvariantCulture).Replace(".", ",")

/// Liest einen Betrag als positive Größe. Leer ist 0, Unlesbares ist `None`. Ein Minuszeichen
/// wird ignoriert: im Editor stehen nur positive Beträge.
let parseMagnitude (text: string) : decimal option =
    if String.IsNullOrWhiteSpace text then
        Some 0m
    else
        let trimmed = text.Trim().TrimStart('-')
        Types.parseAmountInput trimmed

let private magnitudeOf (line: SplitDraftLine) : decimal =
    parseMagnitude line.AmountText |> Option.defaultValue 0m

let private signOf (total: Money) : decimal = if total.Amount < 0m then -1m else 1m

/// Die Zeile als `TransactionSplit` mit dem Vorzeichen des Gesamtbetrags. Ohne Ziel gilt sie
/// als unkategorisiert (nur für die Rechnung; gespeichert wird so nie).
let private signedLine (total: Money) (line: SplitDraftLine) : TransactionSplit =
    { Target = line.Target |> Option.defaultValue Uncategorized
      Amount = { Amount = signOf total * magnitudeOf line; Currency = total.Currency }
      Memo = if String.IsNullOrWhiteSpace line.Memo then None else Some (line.Memo.Trim()) }

/// Der Rest als positive Größe: Gesamtbetrag minus Summe der eingegebenen Beträge. Null heißt
/// verteilt, über null ist noch offen, unter null ist zu viel verteilt. Rechnung über
/// `Shared.Domain.splitRemainder`.
let remainder (state: SplitEditorState) : decimal =
    signOf state.Total * splitRemainder state.Total (state.Lines |> List.map (signedLine state.Total))

/// Der Betrag, der die Zeile `index` füllt, damit der Rest null wird (Knopf "Rest" an der
/// Zeile): Gesamtbetrag minus Summe der übrigen Zeilen, nie unter null.
let restFor (index: int) (state: SplitEditorState) : decimal =
    let others =
        state.Lines
        |> List.indexed
        |> List.filter (fun (i, _) -> i <> index)
        |> List.map (fun (_, l) -> signedLine state.Total l)
    max 0m (signOf state.Total * splitRemainder state.Total others)

let fillRest (index: int) (state: SplitEditorState) : SplitEditorState =
    let value = restFor index state
    { state with
        Lines =
            state.Lines
            |> List.mapi (fun i l -> if i = index then { l with AmountText = amountText value } else l) }

/// Ist der Rest ausgeglichen (null auf den Cent)?
let isBalanced (state: SplitEditorState) : bool = remainder state = 0m

/// Zeile hinzufügen: ausgegraut, wenn nichts mehr offen ist.
let canAddLine (state: SplitEditorState) : bool = remainder state <> 0m

let addLine (state: SplitEditorState) : SplitEditorState =
    if canAddLine state then { state with Lines = state.Lines @ [ emptyLine ] } else state

/// Das Ziel für "Barabhebung": das Quick-Add-Konto (ADR 0004) als Übertrag, wenn es unter den
/// wählbaren Übertrag-Zielen vorkommt. Ohne eingestelltes oder wählbares Konto (zu, kein
/// Budget-Konto, das eigene Konto der Buchung) gibt es kein Ziel: Roman wählt das Konto selbst.
let cashbackTarget (quickAddAccountId: YnabAccountId option) (transferTargets: YnabAccount list) : SplitTarget option =
    quickAddAccountId
    |> Option.bind (fun id -> transferTargets |> List.tryFind (fun a -> a.Id = id))
    |> Option.map (fun a -> ToTransfer (a.Id, a.Name))

/// "Barabhebung" (ledger-s8t3s): die erste Zeile ohne Ziel wird zum Übertrag auf das Bar-Konto;
/// gibt es keine, kommt eine neue Zeile dazu. Bestehende Zeilen, Ziele und Beträge bleiben,
/// die Beträge bleiben leer (der Ausgleich geht über "Rest", wie im Import). Ohne Ziel (kein
/// Quick-Add-Konto) bleibt die Zeile leer und der Ziel-Picker für sie geht auf.
let startCashback (target: SplitTarget option) (state: SplitEditorState) : SplitEditorState =
    let lines, index =
        match state.Lines |> List.tryFindIndex (fun l -> l.Target.IsNone) with
        | Some i -> state.Lines, i
        | None -> state.Lines @ [ emptyLine ], List.length state.Lines
    match target with
    | Some t ->
        { state with
            Lines = lines |> List.mapi (fun i l -> if i = index then { l with Target = Some t } else l)
            Picker = NoSplitPicker }
    | None -> { state with Lines = lines; Picker = SplitTargetPickerFor index }

let removeLine (index: int) (state: SplitEditorState) : SplitEditorState =
    { state with Lines = state.Lines |> List.indexed |> List.filter (fun (i, _) -> i <> index) |> List.map snd }

let setAmountText (index: int) (text: string) (state: SplitEditorState) : SplitEditorState =
    { state with Lines = state.Lines |> List.mapi (fun i l -> if i = index then { l with AmountText = text } else l) }

let setMemo (index: int) (text: string) (state: SplitEditorState) : SplitEditorState =
    { state with Lines = state.Lines |> List.mapi (fun i l -> if i = index then { l with Memo = text } else l) }

let setTarget (index: int) (target: SplitTarget) (state: SplitEditorState) : SplitEditorState =
    { state with
        Lines = state.Lines |> List.mapi (fun i l -> if i = index then { l with Target = Some target } else l)
        Picker = NoSplitPicker }

let private isTransfer (line: SplitDraftLine) : bool =
    match line.Target with
    | Some (ToTransfer _) -> true
    | _ -> false

/// Die Zeilen für `setEntrySplits`, wenn der Entwurf speicherbar ist; sonst `None`. Zwei oder
/// mehr Zeilen müssen `mkSplits` bestehen; genau eine Zeile muss eine Kategorie oder
/// "Ohne Kategorie" tragen und den vollen Betrag haben (sie hebt die Aufteilung auf). Eine
/// leere Liste und eine einzelne Übertrags-Zeile gibt es nie. Alle Zeilen brauchen ein Ziel
/// und einen lesbaren Betrag.
let requestLines (state: SplitEditorState) : TransactionSplit list option =
    let allTargeted = state.Lines |> List.forall (fun l -> l.Target.IsSome)
    let allReadable = state.Lines |> List.forall (fun l -> (parseMagnitude l.AmountText).IsSome)
    let allPositive = state.Lines |> List.forall (fun l -> magnitudeOf l > 0m)
    if List.isEmpty state.Lines || not allTargeted || not allReadable || not allPositive then
        None
    else
        let lines = state.Lines |> List.map (signedLine state.Total)
        match state.Lines with
        | [ single ] ->
            if isTransfer single || remainder state <> 0m then None else Some lines
        | _ ->
            match mkSplits state.Total lines with
            | Ok valid -> Some valid
            | Error _ -> None

/// Ist "Speichern" aktiv?
let canSave (state: SplitEditorState) : bool = (requestLines state).IsSome

/// Wie viele Zeilen hat das Ergebnis? Eine Zeile heißt: die Aufteilung wird aufgehoben.
let resultLineCount (lines: TransactionSplit list) : int = List.length lines

let isUnsplitResult (lines: TransactionSplit list) : bool = resultLineCount lines = 1

/// Der Text des Toasts nach dem Speichern.
let savedToastText (lines: TransactionSplit list) : string =
    if isUnsplitResult lines then "Aufteilung aufgehoben" else $"Aufgeteilt in {resultLineCount lines} Zeilen"

let undoLabel = "Rückgängig"

/// Die Zeilen, mit denen "Rückgängig" den Stand vor dem Speichern wiederherstellt, über
/// denselben Schreibweg: war die Buchung aufgeteilt, die alten Zeilen; war sie es nicht, eine
/// Zeile der alten Kategorie über den vollen Betrag (`Uncategorized` ohne Kategorie).
let undoLines (before: SplitBefore) (total: Money) : TransactionSplit list =
    match before with
    | WasSplit lines -> lines
    | WasPlain (Some choice) ->
        [ { Target = ToCategory (choice.CategoryId, choice.CategoryName); Amount = total; Memo = None } ]
    | WasPlain None -> [ { Target = Uncategorized; Amount = total; Memo = None } ]

/// Bietet der Toast "Rückgängig" an? Entsteht eine Aufteilung (zwei oder mehr Zeilen), ist der
/// Rückweg ein Ersatz in YNAB (DELETE und POST). Der ist bei einer fremden `import_id` gesperrt
/// (ADR 0058 Punkt 2); dann fehlt der Knopf, statt in einen Fehler zu laufen.
let undoAvailable (lockBefore: SplitLocks option) (savedLines: TransactionSplit list) : bool =
    let changeBlocked = lockBefore |> Option.exists (fun l -> l.CannotChangeReason.IsSome)
    isUnsplitResult savedLines || not changeBlocked

/// Der Entwurf zu einer Buchung: eine aufgeteilte Buchung mit ihren Zeilen, eine unaufgeteilte
/// mit der bisherigen Kategorie in der ersten und einer leeren zweiten Zeile.
let startState (entry: LedgerEntryView) : SplitEditorState =
    let currentTarget =
        match entry.CategoryId, entry.CategoryName with
        | Some id, Some name -> Some (ToCategory (YnabCategoryId id, name))
        | _ -> None
    match entry.Splits with
    | [] ->
        { EntryId = entry.Id
          Total = entry.Amount
          Lines = [ { emptyLine with Target = currentTarget }; emptyLine ]
          Picker = NoSplitPicker
          Before =
            WasPlain (
                match entry.CategoryId, entry.CategoryName with
                | Some id, Some name -> Some { CategoryId = YnabCategoryId id; CategoryName = name }
                | _ -> None
            ) }
    | splits ->
        { EntryId = entry.Id
          Total = entry.Amount
          Lines =
            splits
            |> List.map (fun s ->
                { Target = Some s.Target
                  AmountText = amountText s.Amount.Amount
                  Memo = s.Memo |> Option.defaultValue "" })
          Picker = NoSplitPicker
          Before = WasSplit splits }

/// Die Beschriftung des Ziels einer Zeile; Übertrag mit Pfeil, ohne Wahl ein Aufruf zum Wählen.
let targetLabel (line: SplitDraftLine) : string =
    match line.Target with
    | Some (ToCategory (_, name)) -> name
    | Some (ToTransfer (_, name)) -> $"→ {name}"
    | Some Uncategorized -> "Ohne Kategorie"
    | None -> "Kategorie oder Konto wählen"


/// Der Grund, aus dem "Aufteilen" (unaufgeteilte Buchung) bzw. "Ändern" (aufgeteilte) gesperrt
/// ist, aus den Sperren der Buchung (`LedgerEntryView.SplitLocks`); `None`, wenn der Knopf wirkt.
/// Auch der Pfeil an der Kategorie einer Split-Buchung folgt dem.
let lockReasonFor (entry: LedgerEntryView) : string option =
    match entry.SplitLocks with
    | None -> None
    | Some locks -> if List.isEmpty entry.Splits then locks.CannotSplitReason else locks.CannotChangeReason

/// Die Beschriftung des festen Knopfs im Detail-Feld Split.
let splitActionLabel (entry: LedgerEntryView) : string =
    if List.isEmpty entry.Splits then "Aufteilen" else "Ändern"
