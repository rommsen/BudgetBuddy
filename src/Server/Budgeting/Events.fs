/// Die Budget-Ereignisse in `events` (ADR 0083, ADR 0084 Punkte 1, 2 und 4, budgeting-hyn77).
///
/// Hier stehen nur die Typen, die BB heute schreibt oder aus dem Altbestand übernimmt, samt
/// JSON-Codec, Typnamen, Actor und Stream. Kein I/O. Beträge stehen in Milliunits (JSON-Zahl),
/// der Monat im Ereignismodell als `YYYY-MM` (ADR 0084 Punkt 1). Die Gruppen-Id einer Entscheidung
/// ist ihre `event_id`; die Ergebnis-Ereignisse verweisen über `decisionEventId` darauf.
module Budgeting.Events

open System
open System.Text.RegularExpressions
open Newtonsoft.Json.Linq
open Thoth.Json.Net
open Shared.Budgeting

// ============================================
// Kontext, Stream und Monat
// ============================================

/// Der Kontext in `events.context`.
[<Literal>]
let Context = "budgeting"

let private monthKeyPattern = Regex(@"^(\d{4}-(0[1-9]|1[0-2]))-01$", RegexOptions.CultureInvariant)
let private monthIdPattern = Regex(@"^\d{4}-(0[1-9]|1[0-2])$", RegexOptions.CultureInvariant)

/// Die eine Funktion, die den Monat des Ereignismodells baut: aus dem gespeicherten oder
/// ausgetauschten Monatsersten "YYYY-MM-01" wird "YYYY-MM". Alles andere wird gemeldet.
let monthIdOf (monthKey: string) : Result<string, string> =
    match (if isNull monthKey then null else monthKeyPattern.Match monthKey) with
    | null -> Error "Monat fehlt"
    | m when m.Success -> Ok m.Groups[1].Value
    | _ -> Error $"Monat '{monthKey}' ist kein Monatserster im Format YYYY-MM-01"

/// Die Gegenrichtung für die Leser: "YYYY-MM" wird wieder "YYYY-MM-01", wie Remoting und Client ihn
/// kennen (ADR 0084, Neutral).
let monthKeyOfId (monthId: string) : string = monthId + "-01"

/// Ein Stream des Budgets.
type BudgetStream =
    /// `budgeting:month:<YYYY-MM>`: Zuweisen, Verschieben, Targets anwenden, Pausen,
    /// Rückschreib-Ergebnisse, Übernahme und Beobachtungen eines Monats.
    | MonthStream of monthId: string
    /// `budgeting:target:<categoryId>`: das Target einer Kategorie.
    | TargetStream of categoryId: Guid

/// Aggregat und Id für `EventStore.AppendRequest`.
let streamParts (stream: BudgetStream) : string * EventStore.AggregateId =
    match stream with
    | MonthStream monthId -> "month", EventStore.TextId monthId
    | TargetStream categoryId -> "target", EventStore.GuidId categoryId

/// Der Stream-Name, gebaut über `EventStore.streamIdOf` (ADR 0083 Punkt 2).
let streamIdOf (stream: BudgetStream) : string =
    let aggregate, id = streamParts stream
    EventStore.streamIdOf Context aggregate id

let private targetStreamPrefix = Context + ":target:"

let private monthStreamPrefix = Context + ":month:"

/// Der Monat "YYYY-MM" eines Monats-Streams, `None` für jeden anderen Stream (budgeting-dkjc5).
let monthOfMonthStream (streamId: string) : string option =
    if not (isNull streamId) && streamId.StartsWith(monthStreamPrefix, StringComparison.Ordinal) then
        let monthId = streamId.Substring monthStreamPrefix.Length
        if monthIdPattern.IsMatch monthId then Some monthId else None
    else
        None

/// Die Kategorie eines Target-Streams, `None` für jeden anderen Stream.
let categoryOfTargetStream (streamId: string) : Guid option =
    if not (isNull streamId) && streamId.StartsWith(targetStreamPrefix, StringComparison.Ordinal) then
        match Guid.TryParseExact(streamId.Substring targetStreamPrefix.Length, "D") with
        | true, g -> Some g
        | _ -> None
    else
        None

// ============================================
// Payloads
// ============================================

/// Eine Zeile einer Entscheidung. `LineId` füllt `HistoryMovement.MovementId`, `CategoryName` ist der
/// Name zum Zeitpunkt der Entscheidung.
type BudgetLine = {
    LineId: Guid
    CategoryId: Guid
    CategoryName: string
    PreviousAssigned: int64
    Assigned: int64
}

/// Eine pausierte Kategorie, die Targets anwenden ausließ.
type SkippedPausedLine = {
    CategoryId: Guid
    CategoryName: string
    Underfunded: int64
}

type AssignmentChange = { Month: string; Line: BudgetLine }

type MoneyMove = {
    Month: string
    From: BudgetLine
    To: BudgetLine
    Amount: int64
}

type TargetsApplication = {
    Month: string
    Lines: BudgetLine list
    SkippedPaused: SkippedPausedLine list
}

type PauseChange = {
    Month: string
    CategoryId: Guid
    CategoryName: string
}

/// Verweis eines Rückschreib-Ergebnisses auf seine Zeile: Entscheidung und Kategorie.
type WriteBackRef = { DecisionEventId: Guid; CategoryId: Guid }

/// Eine Target-Änderung aus dem Zwei-Schritt-Weg (Altbestand-Familie, bis budgeting-eh8xe). Nur
/// Teilwerte: Betrag in Milliunits, Datum "YYYY-MM-DD", der Rhythmus steckt in der Art.
type YnabGoalChange = {
    Month: string
    CategoryName: string
    ChangeKind: TargetChangeKind
    OldGoalTarget: int64 option
    NewGoalTarget: int64 option
    OldDate: string option
    NewDate: string option
}

/// Verweis eines Schritt-Ergebnisses auf seine Target-Änderung.
type GoalStepRef = { DecisionEventId: Guid; Step: int }

/// Eine Kategorie einer Übernahme: der Wert, den YNAB beim Übernehmen zeigte, ungerundet.
type TakenOverCategory = {
    CategoryId: Guid
    CategoryName: string
    Assigned: int64
}

/// Die Übernahme eines Monats aus YNAB (ADR 0084 Punkt 5): Startstand der Faltung. Alle Kategorien außer
/// `internal`, auch ausgeblendete und gelöschte.
type BudgetTakeover = {
    Month: string
    Categories: TakenOverCategory list
}

/// Eine Zuweisung, die BB in YNAB sah und nicht selbst geschrieben hat (ADR 0084 Punkt 3).
/// `PreviousAssigned` ist BBs Soll davor, `Assigned` der YNAB-Wert, ungerundet.
type ObservedAssignment = {
    Month: string
    CategoryId: Guid
    CategoryName: string
    PreviousAssigned: int64
    Assigned: int64
}

/// Die Übernahme des Targets einer Kategorie aus YNAB (ADR 0084 Punkt 5 und 6, budgeting-eh8xe): die Regel und
/// YNABs rohes `goal_type`. Startstand der Faltung des Target-Streams, gilt für alle Monate.
type TargetTakeover = {
    CategoryName: string
    Rule: Target
    YnabGoalType: string
}

/// Ein Target, in BB gesetzt (budgeting-eh8xe): immer die vollständige Regel, gültig ab `EffectiveFrom`
/// ("YYYY-MM", der laufende Monat beim Festhalten, ADR 0080 Punkt 1).
type TargetSetting = {
    Rule: Target
    EffectiveFrom: string
}

/// Ein Target, in BB entfernt, ab `EffectiveFrom` ("YYYY-MM").
type TargetRemoval = { EffectiveFrom: string }

/// Verweis eines Schreib-Ergebnisses auf die Target-Entscheidung, deren Regel nach YNAB ging (budgeting-e2z7v,
/// Nachtrag ADR 0084 vom 2026-11-08): die `event_id` eines `TargetSet`, `TargetRemoved` oder
/// `TargetTakenOverFromYnab`. Der Stream nennt die Kategorie.
type TargetWriteRef = { DecisionEventId: Guid }

/// Ein Budget-Ereignis. Qualifiziert zu benutzen: die Namen gleichen Fällen in `Shared.Budgeting`.
[<RequireQualifiedAccess>]
type BudgetEvent =
    | AssignmentChangedInBb of AssignmentChange
    | MoneyMoved of MoneyMove
    | TargetsApplied of TargetsApplication
    | TargetPaused of PauseChange
    | TargetPauseLifted of PauseChange
    | AssignmentWrittenBackToYnab of WriteBackRef
    | AssignmentWriteBackFailed of WriteBackRef * reason: string
    | AssignmentWriteBackSupersededInYnab of WriteBackRef
    | AssignmentWriteBackWithdrawn of WriteBackRef
    | YnabGoalChangedFromBb of YnabGoalChange
    | YnabGoalStepWrittenToYnab of GoalStepRef
    | YnabGoalStepWriteFailed of GoalStepRef * reason: string
    | YnabGoalStepSupersededInYnab of GoalStepRef
    | BudgetTakenOverFromYnab of BudgetTakeover
    | AssignmentChangeObservedInYnab of ObservedAssignment
    | TargetTakenOverFromYnab of TargetTakeover
    | TargetSet of TargetSetting
    | TargetRemoved of TargetRemoval
    | TargetWrittenToYnab of TargetWriteRef
    | TargetWriteToYnabFailed of TargetWriteRef * reason: string

/// Der Typname in `events.event_type` (ADR 0083 Punkt 9).
let eventTypeName (event: BudgetEvent) : string =
    match event with
    | BudgetEvent.AssignmentChangedInBb _ -> "AssignmentChangedInBb"
    | BudgetEvent.MoneyMoved _ -> "MoneyMoved"
    | BudgetEvent.TargetsApplied _ -> "TargetsApplied"
    | BudgetEvent.TargetPaused _ -> "TargetPaused"
    | BudgetEvent.TargetPauseLifted _ -> "TargetPauseLifted"
    | BudgetEvent.AssignmentWrittenBackToYnab _ -> "AssignmentWrittenBackToYnab"
    | BudgetEvent.AssignmentWriteBackFailed _ -> "AssignmentWriteBackFailed"
    | BudgetEvent.AssignmentWriteBackSupersededInYnab _ -> "AssignmentWriteBackSupersededInYnab"
    | BudgetEvent.AssignmentWriteBackWithdrawn _ -> "AssignmentWriteBackWithdrawn"
    | BudgetEvent.YnabGoalChangedFromBb _ -> "YnabGoalChangedFromBb"
    | BudgetEvent.YnabGoalStepWrittenToYnab _ -> "YnabGoalStepWrittenToYnab"
    | BudgetEvent.YnabGoalStepWriteFailed _ -> "YnabGoalStepWriteFailed"
    | BudgetEvent.YnabGoalStepSupersededInYnab _ -> "YnabGoalStepSupersededInYnab"
    | BudgetEvent.BudgetTakenOverFromYnab _ -> "BudgetTakenOverFromYnab"
    | BudgetEvent.AssignmentChangeObservedInYnab _ -> "AssignmentChangeObservedInYnab"
    | BudgetEvent.TargetTakenOverFromYnab _ -> "TargetTakenOverFromYnab"
    | BudgetEvent.TargetSet _ -> "TargetSet"
    | BudgetEvent.TargetRemoved _ -> "TargetRemoved"
    | BudgetEvent.TargetWrittenToYnab _ -> "TargetWrittenToYnab"
    | BudgetEvent.TargetWriteToYnabFailed _ -> "TargetWriteToYnabFailed"

/// Typnamen für Leser, die nicht den ganzen Kontext brauchen (Filter auf `event_type`, Index
/// `idx_events_context_type`). Aus `eventTypeName` abgeleitet, damit es keine zweite Schreibweise gibt;
/// die Payload der Muster bleibt leer und wird nie gelesen.
module EventTypes =
    let private none<'T> : 'T = Unchecked.defaultof<'T>

    /// Das Entscheidungsereignis zu einer Herkunft.
    let decisionOf (origin: MovementOrigin) : string =
        match origin with
        | MoneyAssigned -> eventTypeName (BudgetEvent.AssignmentChangedInBb none)
        | MoneyMoved -> eventTypeName (BudgetEvent.MoneyMoved none)
        | TargetsApplied -> eventTypeName (BudgetEvent.TargetsApplied none)

    /// Die drei Entscheidungen (Zuweisen, Verschieben, Targets anwenden).
    let decisions : string list = [ MoneyAssigned; MoneyMoved; TargetsApplied ] |> List.map decisionOf

    /// Target pausieren und die Pause aufheben.
    let paused : string = eventTypeName (BudgetEvent.TargetPaused none)
    let pauseLifted : string = eventTypeName (BudgetEvent.TargetPauseLifted none)

    /// Die vier Ergebnisse einer Rückschreibung.
    let writeBacks : string list =
        [ BudgetEvent.AssignmentWrittenBackToYnab none
          BudgetEvent.AssignmentWriteBackFailed (none, none)
          BudgetEvent.AssignmentWriteBackSupersededInYnab none
          BudgetEvent.AssignmentWriteBackWithdrawn none ]
        |> List.map eventTypeName

    /// Die Übernahme eines Monats aus YNAB (budgeting-dgrp7).
    let takenOver : string = eventTypeName (BudgetEvent.BudgetTakenOverFromYnab none)

    /// Übernahme und Beobachtung aus YNAB (budgeting-dgrp7), so wie die Historie sie liest.
    let fromYnab : string list = [ takenOver; eventTypeName (BudgetEvent.AssignmentChangeObservedInYnab none) ]

    /// Die Target-Änderung aus dem Zwei-Schritt-Weg und ihre drei Schritt-Ergebnisse.
    let goalChanges : string list =
        [ BudgetEvent.YnabGoalChangedFromBb none
          BudgetEvent.YnabGoalStepWrittenToYnab none
          BudgetEvent.YnabGoalStepWriteFailed (none, none)
          BudgetEvent.YnabGoalStepSupersededInYnab none ]
        |> List.map eventTypeName

    /// Die Übernahme eines Targets aus YNAB (budgeting-eh8xe).
    let targetTakenOver : string = eventTypeName (BudgetEvent.TargetTakenOverFromYnab none)

    /// Eine Target-Änderung in BB: gesetzt oder entfernt (budgeting-eh8xe).
    let targetChanges : string list =
        [ BudgetEvent.TargetSet none; BudgetEvent.TargetRemoved none ] |> List.map eventTypeName

    /// Was BBs Regel eines Targets bestimmt: Übernahme und Änderungen in BB. Liegt eins davon im Target-Stream,
    /// führt BB das Target der Kategorie.
    let targetRules : string list = targetTakenOver :: targetChanges

    /// Die Ergebnisse des Schreibens eines Targets nach YNAB (budgeting-e2z7v). Nicht in `targetRules`: sie ändern
    /// BBs Regel nicht und führen kein Target.
    let targetWrites : string list =
        [ BudgetEvent.TargetWrittenToYnab none; BudgetEvent.TargetWriteToYnabFailed (none, none) ] |> List.map eventTypeName

/// Alle Budget-Ereignisse tragen heute Schema-Version 1.
[<Literal>]
let SchemaVersion = 1

/// Die Actor-Art (ADR 0084 Punkt 2): Entscheidungen, Pausen und ungesendet geschlossene Ziele
/// kommen von Roman, Rückschreib- und Schritt-Ergebnisse vom Spiegel.
let actorKindOf (event: BudgetEvent) : string =
    match event with
    | BudgetEvent.AssignmentChangedInBb _
    | BudgetEvent.MoneyMoved _
    | BudgetEvent.TargetsApplied _
    | BudgetEvent.TargetPaused _
    | BudgetEvent.TargetPauseLifted _
    | BudgetEvent.AssignmentWriteBackWithdrawn _
    | BudgetEvent.YnabGoalChangedFromBb _
    | BudgetEvent.TargetSet _
    | BudgetEvent.TargetRemoved _ -> "UserInBb"
    | BudgetEvent.AssignmentWrittenBackToYnab _
    | BudgetEvent.AssignmentWriteBackFailed _
    | BudgetEvent.AssignmentWriteBackSupersededInYnab _
    | BudgetEvent.YnabGoalStepWrittenToYnab _
    | BudgetEvent.YnabGoalStepWriteFailed _
    | BudgetEvent.YnabGoalStepSupersededInYnab _
    | BudgetEvent.BudgetTakenOverFromYnab _
    | BudgetEvent.AssignmentChangeObservedInYnab _
    | BudgetEvent.TargetTakenOverFromYnab _
    | BudgetEvent.TargetWrittenToYnab _
    | BudgetEvent.TargetWriteToYnabFailed _ -> "YnabMirror"

// ============================================
// Ausgang einer Rückschreibung
// ============================================

/// Wie eine Zeile einer Entscheidung beim Rückschreiben nach YNAB ausging.
type WriteBackOutcome =
    | WrittenBack
    | WriteBackFailed of reason: string
    /// YNAB stand weder auf dem alten noch auf dem Zielwert: YNAB gewinnt.
    | SupersededInYnab
    /// BB schloss die Zeile ungesendet (Ziel einer Verschiebung, deren Quelle verloren ist).
    | Withdrawn

/// Das Ergebnis-Ereignis zu einer Zeile.
let writeBackEvent (reference: WriteBackRef) (outcome: WriteBackOutcome) : BudgetEvent =
    match outcome with
    | WrittenBack -> BudgetEvent.AssignmentWrittenBackToYnab reference
    | WriteBackFailed reason -> BudgetEvent.AssignmentWriteBackFailed (reference, reason)
    | SupersededInYnab -> BudgetEvent.AssignmentWriteBackSupersededInYnab reference
    | Withdrawn -> BudgetEvent.AssignmentWriteBackWithdrawn reference

/// Was Historie und Bericht zeigen. Ersetzt heißt "in YNAB geändert", Zurückgezogen "in BB zurückgezogen"
/// (budgeting-my8p8): YNAB hat eine zurückgezogene Zeile nie berührt. Beide schließen die Zeile für "erneut
/// senden" (budgeting-hyn77).
let lineOutcomeOf (outcome: WriteBackOutcome) : LineOutcome =
    match outcome with
    | WrittenBack -> Written
    | WriteBackFailed reason -> Failed reason
    | SupersededInYnab -> ChangedInYnab
    | Withdrawn -> WithdrawnInBb

/// Das Schritt-Ergebnis-Ereignis einer Target-Änderung aus dem Altbestand zu einem Ausgang (Startmigration).
let goalStepEvent (reference: GoalStepRef) (outcome: LineOutcome) : BudgetEvent =
    match outcome with
    | Written -> BudgetEvent.YnabGoalStepWrittenToYnab reference
    | Failed reason -> BudgetEvent.YnabGoalStepWriteFailed (reference, reason)
    // Ein Target-Schritt wird nie zurückgezogen, der Altbestand kennt den Stand nicht.
    | ChangedInYnab
    | WithdrawnInBb -> BudgetEvent.YnabGoalStepSupersededInYnab reference

// ============================================
// Codec
// ============================================

module Codec =

    /// Milliunits als JSON-Zahl. `Encode.int64` schriebe einen String.
    let encodeMilliunits (value: int64) : JsonValue = JValue(value) :> JsonValue

    let encodeGuid (g: Guid) = Encode.string (g.ToString "D")

    let decodeGuid : Decoder<Guid> =
        Decode.string
        |> Decode.andThen (fun s ->
            match Guid.TryParseExact(s, "D") with
            | true, g -> Decode.succeed g
            | _ -> Decode.fail $"Keine Guid im Format D: {s}")

    let decodeMonth : Decoder<string> =
        Decode.string
        |> Decode.andThen (fun s ->
            if monthIdPattern.IsMatch s then Decode.succeed s
            else Decode.fail $"Monat '{s}' passt nicht auf YYYY-MM")

    let encodeOption (encode: 'a -> JsonValue) (value: 'a option) : JsonValue =
        match value with
        | Some v -> encode v
        | None -> Encode.nil

    let encodeLine (l: BudgetLine) =
        Encode.object [
            "lineId", encodeGuid l.LineId
            "categoryId", encodeGuid l.CategoryId
            "categoryName", Encode.string l.CategoryName
            "previousAssigned", encodeMilliunits l.PreviousAssigned
            "assigned", encodeMilliunits l.Assigned
        ]

    let decodeLine : Decoder<BudgetLine> =
        Decode.object (fun get -> {
            LineId = get.Required.Field "lineId" decodeGuid
            CategoryId = get.Required.Field "categoryId" decodeGuid
            CategoryName = get.Required.Field "categoryName" Decode.string
            PreviousAssigned = get.Required.Field "previousAssigned" Decode.int64
            Assigned = get.Required.Field "assigned" Decode.int64
        })

    let encodeSkipped (s: SkippedPausedLine) =
        Encode.object [
            "categoryId", encodeGuid s.CategoryId
            "categoryName", Encode.string s.CategoryName
            "underfunded", encodeMilliunits s.Underfunded
        ]

    let decodeSkipped : Decoder<SkippedPausedLine> =
        Decode.object (fun get -> {
            CategoryId = get.Required.Field "categoryId" decodeGuid
            CategoryName = get.Required.Field "categoryName" Decode.string
            Underfunded = get.Required.Field "underfunded" Decode.int64
        })

    let encodeAssignment (p: AssignmentChange) =
        Encode.object [ "month", Encode.string p.Month; "line", encodeLine p.Line ]

    let decodeAssignment : Decoder<AssignmentChange> =
        Decode.object (fun get -> {
            Month = get.Required.Field "month" decodeMonth
            Line = get.Required.Field "line" decodeLine
        })

    let encodeMove (p: MoneyMove) =
        Encode.object [
            "month", Encode.string p.Month
            "from", encodeLine p.From
            "to", encodeLine p.To
            "amount", encodeMilliunits p.Amount
        ]

    let decodeMove : Decoder<MoneyMove> =
        Decode.object (fun get -> {
            Month = get.Required.Field "month" decodeMonth
            From = get.Required.Field "from" decodeLine
            To = get.Required.Field "to" decodeLine
            Amount = get.Required.Field "amount" Decode.int64
        })

    let encodeApplication (p: TargetsApplication) =
        Encode.object [
            "month", Encode.string p.Month
            "lines", Encode.list (p.Lines |> List.map encodeLine)
            "skippedPaused", Encode.list (p.SkippedPaused |> List.map encodeSkipped)
        ]

    let decodeApplication : Decoder<TargetsApplication> =
        Decode.object (fun get -> {
            Month = get.Required.Field "month" decodeMonth
            Lines = get.Required.Field "lines" (Decode.list decodeLine)
            SkippedPaused = get.Required.Field "skippedPaused" (Decode.list decodeSkipped)
        })

    let encodePause (p: PauseChange) =
        Encode.object [
            "month", Encode.string p.Month
            "categoryId", encodeGuid p.CategoryId
            "categoryName", Encode.string p.CategoryName
        ]

    let decodePause : Decoder<PauseChange> =
        Decode.object (fun get -> {
            Month = get.Required.Field "month" decodeMonth
            CategoryId = get.Required.Field "categoryId" decodeGuid
            CategoryName = get.Required.Field "categoryName" Decode.string
        })

    let private writeBackFields (r: WriteBackRef) =
        [ "decisionEventId", encodeGuid r.DecisionEventId; "categoryId", encodeGuid r.CategoryId ]

    let encodeWriteBack (r: WriteBackRef) = Encode.object (writeBackFields r)

    let encodeWriteBackFailed (r: WriteBackRef, reason: string) =
        Encode.object (writeBackFields r @ [ "reason", Encode.string reason ])

    let decodeWriteBack : Decoder<WriteBackRef> =
        Decode.object (fun get -> {
            DecisionEventId = get.Required.Field "decisionEventId" decodeGuid
            CategoryId = get.Required.Field "categoryId" decodeGuid
        })

    let decodeWriteBackFailed : Decoder<WriteBackRef * string> =
        Decode.map2 (fun r reason -> r, reason) decodeWriteBack (Decode.field "reason" Decode.string)

    /// Der Rhythmus in der Payload: "monthly", "weekly", "yearly". Die frühere Spalte `new_frequency`
    /// trug dieselben Texte, die Migration liest sie über `frequencyOfText`.
    let frequencyText (f: TargetFrequency) =
        match f with
        | Weekly -> "weekly"
        | Monthly -> "monthly"
        | Yearly -> "yearly"

    /// Der Rhythmus zu einem Text aus `frequencyText`, `None` bei jedem anderen.
    let frequencyOfText (text: string) : TargetFrequency option =
        [ Weekly; Monthly; Yearly ] |> List.tryFind (fun f -> frequencyText f = text)

    let private changeKindText (kind: TargetChangeKind) : string * TargetFrequency option =
        match kind with
        | TargetEdited -> "Edited", None
        | TargetRemoved -> "Removed", None
        | TargetCreated f -> "Created", Some f
        | TargetKindChanged f -> "KindChanged", Some f

    let encodeGoalChange (p: YnabGoalChange) =
        let kind, frequency = changeKindText p.ChangeKind
        Encode.object [
            "month", Encode.string p.Month
            "categoryName", Encode.string p.CategoryName
            "changeKind", Encode.string kind
            "newFrequency", encodeOption (frequencyText >> Encode.string) frequency
            "oldGoalTarget", encodeOption encodeMilliunits p.OldGoalTarget
            "newGoalTarget", encodeOption encodeMilliunits p.NewGoalTarget
            "oldDate", encodeOption Encode.string p.OldDate
            "newDate", encodeOption Encode.string p.NewDate
        ]

    let private decodeChangeKind : Decoder<TargetChangeKind> =
        let frequency =
            Decode.field "newFrequency" Decode.string
            |> Decode.andThen (fun s ->
                match frequencyOfText s with
                | Some f -> Decode.succeed f
                | None -> Decode.fail $"Unbekannter Rhythmus einer Target-Änderung: {s}")
        Decode.field "changeKind" Decode.string
        |> Decode.andThen (fun kind ->
            match kind with
            | "Edited" -> Decode.succeed TargetEdited
            | "Removed" -> Decode.succeed TargetRemoved
            | "Created" -> frequency |> Decode.map TargetCreated
            | "KindChanged" -> frequency |> Decode.map TargetKindChanged
            | other -> Decode.fail $"Unbekannte Art einer Target-Änderung: {other}")

    let decodeGoalChange : Decoder<YnabGoalChange> =
        Decode.object (fun get -> {
            Month = get.Required.Field "month" decodeMonth
            CategoryName = get.Required.Field "categoryName" Decode.string
            ChangeKind = get.Required.Raw decodeChangeKind
            OldGoalTarget = get.Optional.Field "oldGoalTarget" Decode.int64
            NewGoalTarget = get.Optional.Field "newGoalTarget" Decode.int64
            OldDate = get.Optional.Field "oldDate" Decode.string
            NewDate = get.Optional.Field "newDate" Decode.string
        })

    let private stepFields (r: GoalStepRef) =
        [ "decisionEventId", encodeGuid r.DecisionEventId; "step", Encode.int r.Step ]

    let encodeStep (r: GoalStepRef) = Encode.object (stepFields r)

    let encodeStepFailed (r: GoalStepRef, reason: string) =
        Encode.object (stepFields r @ [ "reason", Encode.string reason ])

    let decodeStep : Decoder<GoalStepRef> =
        Decode.object (fun get -> {
            DecisionEventId = get.Required.Field "decisionEventId" decodeGuid
            Step = get.Required.Field "step" Decode.int
        })

    let decodeStepFailed : Decoder<GoalStepRef * string> =
        Decode.map2 (fun r reason -> r, reason) decodeStep (Decode.field "reason" Decode.string)

    let encodeTakenOverCategory (c: TakenOverCategory) =
        Encode.object [
            "categoryId", encodeGuid c.CategoryId
            "categoryName", Encode.string c.CategoryName
            "assigned", encodeMilliunits c.Assigned
        ]

    let decodeTakenOverCategory : Decoder<TakenOverCategory> =
        Decode.object (fun get -> {
            CategoryId = get.Required.Field "categoryId" decodeGuid
            CategoryName = get.Required.Field "categoryName" Decode.string
            Assigned = get.Required.Field "assigned" Decode.int64
        })

    let encodeTakeover (p: BudgetTakeover) =
        Encode.object [
            "month", Encode.string p.Month
            "categories", Encode.list (p.Categories |> List.map encodeTakenOverCategory)
        ]

    let decodeTakeover : Decoder<BudgetTakeover> =
        Decode.object (fun get -> {
            Month = get.Required.Field "month" decodeMonth
            Categories = get.Required.Field "categories" (Decode.list decodeTakenOverCategory)
        })

    let encodeObserved (p: ObservedAssignment) =
        Encode.object [
            "month", Encode.string p.Month
            "categoryId", encodeGuid p.CategoryId
            "categoryName", Encode.string p.CategoryName
            "previousAssigned", encodeMilliunits p.PreviousAssigned
            "assigned", encodeMilliunits p.Assigned
        ]

    let decodeObserved : Decoder<ObservedAssignment> =
        Decode.object (fun get -> {
            Month = get.Required.Field "month" decodeMonth
            CategoryId = get.Required.Field "categoryId" decodeGuid
            CategoryName = get.Required.Field "categoryName" Decode.string
            PreviousAssigned = get.Required.Field "previousAssigned" Decode.int64
            Assigned = get.Required.Field "assigned" Decode.int64
        })

    /// Der Rhythmus einer Regel: `{"unit": "months", "every": 1}`, ohne Rhythmus `{"unit": "none"}`.
    let encodeRhythm (rhythm: TargetRhythm) =
        let unit, every =
            match rhythm with
            | Weeks n -> "weeks", Some n
            | Months n -> "months", Some n
            | Years n -> "years", Some n
            | NoRhythm -> "none", None
        Encode.object [ "unit", Encode.string unit; "every", encodeOption Encode.int every ]

    let decodeRhythm : Decoder<TargetRhythm> =
        Decode.object (fun get -> get.Required.Field "unit" Decode.string, get.Optional.Field "every" Decode.int)
        |> Decode.andThen (fun (unit, every) ->
            match unit, every with
            | "weeks", Some n -> Decode.succeed (Weeks n)
            | "months", Some n -> Decode.succeed (Months n)
            | "years", Some n -> Decode.succeed (Years n)
            | "none", None -> Decode.succeed NoRhythm
            | other, _ -> Decode.fail $"Unbekannter Rhythmus eines Targets: {other}")

    /// Die vollständige Regel eines Targets. Die Art steht als YNABs Kürzel (`goalTypeText`), der Betrag in
    /// Milliunits, das Datum als "YYYY-MM-DD", der Anlegemonat als "YYYY-MM-01" (budgeting-pf8n7, optional: ein
    /// Ereignis ohne das Feld hat keinen).
    let encodeRule (t: Target) =
        Encode.object [
            "goal", encodeMilliunits (toMilliunits t.Goal)
            "rhythm", encodeRhythm t.Rhythm
            "kind", Encode.string (goalTypeText t.Kind)
            "date", encodeOption Encode.string t.Date
            "needsWholeAmount", encodeOption Encode.bool t.NeedsWholeAmount
            "day", encodeOption Encode.int t.Day
            "createdIn", encodeOption Encode.string t.CreatedIn
        ]

    let decodeRule : Decoder<Target> =
        Decode.object (fun get -> {
            Goal = get.Required.Field "goal" Decode.int64 |> euroOfMilliunits
            Rhythm = get.Required.Field "rhythm" decodeRhythm
            Kind = get.Required.Field "kind" Decode.string |> targetKindOfGoalType
            Date = get.Optional.Field "date" Decode.string
            NeedsWholeAmount = get.Optional.Field "needsWholeAmount" Decode.bool
            Day = get.Optional.Field "day" Decode.int
            CreatedIn = get.Optional.Field "createdIn" Decode.string
        })

    let encodeTargetTakeover (p: TargetTakeover) =
        Encode.object [
            "categoryName", Encode.string p.CategoryName
            "rule", encodeRule p.Rule
            "ynabGoalType", Encode.string p.YnabGoalType
        ]

    let decodeTargetTakeover : Decoder<TargetTakeover> =
        Decode.object (fun get -> {
            CategoryName = get.Required.Field "categoryName" Decode.string
            Rule = get.Required.Field "rule" decodeRule
            YnabGoalType = get.Required.Field "ynabGoalType" Decode.string
        })

    let encodeTargetSet (p: TargetSetting) =
        Encode.object [ "rule", encodeRule p.Rule; "effectiveFrom", Encode.string p.EffectiveFrom ]

    let decodeTargetSet : Decoder<TargetSetting> =
        Decode.object (fun get -> {
            Rule = get.Required.Field "rule" decodeRule
            EffectiveFrom = get.Required.Field "effectiveFrom" decodeMonth
        })

    let encodeTargetRemoved (p: TargetRemoval) =
        Encode.object [ "effectiveFrom", Encode.string p.EffectiveFrom ]

    let decodeTargetRemoved : Decoder<TargetRemoval> =
        Decode.object (fun get -> { EffectiveFrom = get.Required.Field "effectiveFrom" decodeMonth })

    let encodeTargetWrite (r: TargetWriteRef) = Encode.object [ "decisionEventId", encodeGuid r.DecisionEventId ]

    let encodeTargetWriteFailed (r: TargetWriteRef, reason: string) =
        Encode.object [ "decisionEventId", encodeGuid r.DecisionEventId; "reason", Encode.string reason ]

    let decodeTargetWrite : Decoder<TargetWriteRef> =
        Decode.object (fun get -> { DecisionEventId = get.Required.Field "decisionEventId" decodeGuid })

    let decodeTargetWriteFailed : Decoder<TargetWriteRef * string> =
        Decode.map2 (fun r reason -> r, reason) decodeTargetWrite (Decode.field "reason" Decode.string)

/// Die Payload als JSON-Text, Felder in camelCase.
let encodePayload (event: BudgetEvent) : string =
    let json =
        match event with
        | BudgetEvent.AssignmentChangedInBb p -> Codec.encodeAssignment p
        | BudgetEvent.MoneyMoved p -> Codec.encodeMove p
        | BudgetEvent.TargetsApplied p -> Codec.encodeApplication p
        | BudgetEvent.TargetPaused p
        | BudgetEvent.TargetPauseLifted p -> Codec.encodePause p
        | BudgetEvent.AssignmentWrittenBackToYnab r
        | BudgetEvent.AssignmentWriteBackSupersededInYnab r
        | BudgetEvent.AssignmentWriteBackWithdrawn r -> Codec.encodeWriteBack r
        | BudgetEvent.AssignmentWriteBackFailed (r, reason) -> Codec.encodeWriteBackFailed (r, reason)
        | BudgetEvent.YnabGoalChangedFromBb p -> Codec.encodeGoalChange p
        | BudgetEvent.YnabGoalStepWrittenToYnab r
        | BudgetEvent.YnabGoalStepSupersededInYnab r -> Codec.encodeStep r
        | BudgetEvent.YnabGoalStepWriteFailed (r, reason) -> Codec.encodeStepFailed (r, reason)
        | BudgetEvent.BudgetTakenOverFromYnab p -> Codec.encodeTakeover p
        | BudgetEvent.AssignmentChangeObservedInYnab p -> Codec.encodeObserved p
        | BudgetEvent.TargetTakenOverFromYnab p -> Codec.encodeTargetTakeover p
        | BudgetEvent.TargetSet p -> Codec.encodeTargetSet p
        | BudgetEvent.TargetRemoved p -> Codec.encodeTargetRemoved p
        | BudgetEvent.TargetWrittenToYnab r -> Codec.encodeTargetWrite r
        | BudgetEvent.TargetWriteToYnabFailed (r, reason) -> Codec.encodeTargetWriteFailed (r, reason)
    Encode.toString 0 json

/// Liest eine gespeicherte Payload. Typname und Schema-Version bestimmen den Decoder; ein unbekannter
/// Typ ist ein Fehler, er wird nicht übersprungen.
let decodePayload (eventType: string) (version: int) (payload: string) : Result<BudgetEvent, string> =
    let decode decoder map = Decode.fromString decoder payload |> Result.map map
    match eventType, version with
    | "AssignmentChangedInBb", 1 -> decode Codec.decodeAssignment BudgetEvent.AssignmentChangedInBb
    | "MoneyMoved", 1 -> decode Codec.decodeMove BudgetEvent.MoneyMoved
    | "TargetsApplied", 1 -> decode Codec.decodeApplication BudgetEvent.TargetsApplied
    | "TargetPaused", 1 -> decode Codec.decodePause BudgetEvent.TargetPaused
    | "TargetPauseLifted", 1 -> decode Codec.decodePause BudgetEvent.TargetPauseLifted
    | "AssignmentWrittenBackToYnab", 1 -> decode Codec.decodeWriteBack BudgetEvent.AssignmentWrittenBackToYnab
    | "AssignmentWriteBackFailed", 1 -> decode Codec.decodeWriteBackFailed BudgetEvent.AssignmentWriteBackFailed
    | "AssignmentWriteBackSupersededInYnab", 1 ->
        decode Codec.decodeWriteBack BudgetEvent.AssignmentWriteBackSupersededInYnab
    | "AssignmentWriteBackWithdrawn", 1 -> decode Codec.decodeWriteBack BudgetEvent.AssignmentWriteBackWithdrawn
    | "YnabGoalChangedFromBb", 1 -> decode Codec.decodeGoalChange BudgetEvent.YnabGoalChangedFromBb
    | "YnabGoalStepWrittenToYnab", 1 -> decode Codec.decodeStep BudgetEvent.YnabGoalStepWrittenToYnab
    | "YnabGoalStepWriteFailed", 1 -> decode Codec.decodeStepFailed BudgetEvent.YnabGoalStepWriteFailed
    | "YnabGoalStepSupersededInYnab", 1 -> decode Codec.decodeStep BudgetEvent.YnabGoalStepSupersededInYnab
    | "BudgetTakenOverFromYnab", 1 -> decode Codec.decodeTakeover BudgetEvent.BudgetTakenOverFromYnab
    | "AssignmentChangeObservedInYnab", 1 -> decode Codec.decodeObserved BudgetEvent.AssignmentChangeObservedInYnab
    | "TargetTakenOverFromYnab", 1 -> decode Codec.decodeTargetTakeover BudgetEvent.TargetTakenOverFromYnab
    | "TargetSet", 1 -> decode Codec.decodeTargetSet BudgetEvent.TargetSet
    | "TargetRemoved", 1 -> decode Codec.decodeTargetRemoved BudgetEvent.TargetRemoved
    | "TargetWrittenToYnab", 1 -> decode Codec.decodeTargetWrite BudgetEvent.TargetWrittenToYnab
    | "TargetWriteToYnabFailed", 1 -> decode Codec.decodeTargetWriteFailed BudgetEvent.TargetWriteToYnabFailed
    | t, v -> Error $"Unbekannter Budget-Ereignistyp '{t}' in Schema-Version {v}"

/// Ein Ereignis für `EventStore.append`. Der Zeitpunkt muss UTC sein (`EventStore.validate`).
let toNewEvent (occurredAt: DateTime) (event: BudgetEvent) : EventStore.NewEvent = {
    EventType = eventTypeName event
    SchemaVersion = SchemaVersion
    Payload = encodePayload event
    OccurredAt = occurredAt
    ActorKind = actorKindOf event
    ActorRef = None
}
