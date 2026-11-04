/// Die Sprache des Budgets (Context `budgeting`): Monat, Zu verteilen, Kategorien nach
/// Gruppe mit Zugewiesen, Aktivität und Verfügbar. Die Beträge stammen live aus YNAB und
/// werden nirgends gespeichert (budgeting-k7wmd).
module Shared.Budgeting

open System
open Shared.Domain

/// Wie ein Monat zum laufenden steht (ADR 0074). Der Monat selbst ist ein String "YYYY-MM-01",
/// wie `BudgetMonth.Month`: Client und Server tauschen ihn direkt aus.
type MonthKind =
    | PastMonth
    | CurrentMonth
    | FutureMonth

/// Die Monatsart aus dem Vergleich mit dem laufenden Monat. "YYYY-MM-01" sortiert ordinal wie der
/// Kalender. Der laufende Monat kommt als Parameter: Server `currentMonthKey`, Client `BudgetFrame`.
let monthKind (current: string) (month: string) : MonthKind =
    let order = compare month current
    if order < 0 then PastMonth
    elif order = 0 then CurrentMonth
    else FutureMonth

/// Geht "Targets anwenden" in dieser Monatsart? In vergangenen Monaten nicht (ADR 0074 Punkt 5).
/// Gilt an der API-Grenze und für den Knopf, ebenso für Target pausieren (ADR 0073 Punkt 5).
let targetsAllowed (kind: MonthKind) : bool =
    match kind with
    | PastMonth -> false
    | CurrentMonth
    | FutureMonth -> true

/// Geht Target ändern, entfernen und anlegen in dieser Monatsart? Nur im laufenden Monat (ADR 0080
/// Punkt 1): ein Target gilt ab dem Monat, in dem man es ändert. Pausieren bleibt `targetsAllowed`.
let targetEditAllowed (kind: MonthKind) : bool =
    match kind with
    | CurrentMonth -> true
    | PastMonth
    | FutureMonth -> false

/// In welchem Abstand ein Target wiederkehrt, in YNABs Kadenz gelesen (budgeting-kjsht).
/// Die Zahl ist der Abstand: `Months 1` ist monatlich, `Months 3` alle 3 Monate.
type TargetRhythm =
    | Weeks of every: int
    | Months of every: int
    | Years of every: int
    /// Keine Kadenz: nur der Betrag wird gezeigt.
    | NoRhythm

/// Die Art eines Targets aus YNABs `goal_type` (budgeting-k8wq3). BB erfindet keine eigene Art
/// (ADR 0072 Punkt 2), ein unbekannter Wert bleibt als `OtherKind` lesbar.
type TargetKind =
    /// `NEED`: Monats-, Wochen- oder Jahres-Target ("Set aside another" oder "Refill up to").
    | Need
    /// `TB`: Guthaben erreichen.
    | TargetBalance
    /// `TBD`: Guthaben bis zu einem Datum.
    | TargetBalanceByDate
    /// `MF`: monatlich ansparen (ältere Form).
    | MonthlyFunding
    /// `DEBT`: Schulden tilgen.
    | Debt
    | OtherKind of goalType: string

/// Das Target einer Kategorie, wie YNAB es liefert (`goal_target` und Kadenz). BB zeigt es nur
/// an und rechnet nicht damit (ADR 0065): was fehlt, sagt der Fehlbetrag von YNAB.
type Target = {
    Goal: Money
    Rhythm: TargetRhythm
    /// Die Art aus `goal_type` (budgeting-k8wq3).
    Kind: TargetKind
    /// Das Datum aus `goal_target_date` als "YYYY-MM-DD", wie der Monatsschlüssel ein String.
    Date: string option
    /// `goal_needs_whole_amount`, nur gelesen: `Some false` heißt "Auffüllen bis" (F6).
    NeedsWholeAmount: bool option
    /// `goal_day`, nur gelesen. BB kann den Tag nicht schreiben (ADR 0072).
    Day: int option
}

/// Eine Kategorie so, wie YNABs Monats-Endpunkt sie liefert (vor dem Gruppieren).
/// Ausgeblendete, gelöschte und interne Kategorien (Inflow: Ready to Assign) sind hier noch
/// enthalten und werden erst von `Server.Budgeting.Domain.toBudgetMonth` weggelassen.
type YnabMonthCategory = {
    Id: Guid
    Name: string
    GroupName: string
    /// Zugewiesen (YNAB `budgeted`).
    Assigned: Money
    /// Aktivität (YNAB `activity`).
    Activity: Money
    /// Verfügbar (YNAB `balance`).
    Available: Money
    /// Fehlbetrag (YNAB `goal_under_funded`): `None` ohne Target, `Some 0` wenn erfüllt.
    Underfunded: Money option
    /// Das Target: `None`, wenn YNAB keins liefert.
    Target: Target option
    /// Pausiert in YNAB (`goal_snoozed_at` gesetzt), der rohe YNAB-Wert. Die Pause in BB kommt erst
    /// in `toBudgetMonth` dazu (`BudgetCategory.Pause`, ADR 0079).
    Snoozed: bool
    Hidden: bool
    Deleted: bool
    Internal: bool
}

/// Der Monat roh aus YNAB: Zu verteilen und alle Kategorien. Die Folge der Antwort ist ungeordnet und
/// je Monat anders (ADR 0074 Punkt 6), sie trägt keine Bedeutung.
type YnabMonth = {
    /// Erster des Monats als "YYYY-MM-01".
    Month: string
    /// Zu verteilen (YNAB `to_be_budgeted`).
    ToBeBudgeted: Money
    Categories: YnabMonthCategory list
}

/// Wo die Pause eines Targets im Monat gesetzt ist (ADR 0073 Punkt 2, ADR 0079). In YNAB heißt:
/// `goal_snoozed_at` ist gesetzt, die Pause lässt sich nur dort aufheben. In BB heißt: das jüngste
/// Ereignis in `target_pause_events` für Kategorie und Monat ist "Pausiert".
type PauseSource =
    | PausedInYnab
    | PausedInBb
    | PausedInBoth

/// Die Quelle aus beiden Seiten. `None`, wenn keine Seite pausiert.
let pauseSourceOf (inYnab: bool) (inBb: bool) : PauseSource option =
    match inYnab, inBb with
    | true, true -> Some PausedInBoth
    | true, false -> Some PausedInYnab
    | false, true -> Some PausedInBb
    | false, false -> None

/// Steht die Pause (auch) in BB? Nur diese lässt sich in BB aufheben.
let pausedInBb (pause: PauseSource option) : bool =
    match pause with
    | Some PausedInBb
    | Some PausedInBoth -> true
    | Some PausedInYnab
    | None -> false

/// Steht die Pause (auch) in YNAB?
let pausedInYnab (pause: PauseSource option) : bool =
    match pause with
    | Some PausedInYnab
    | Some PausedInBoth -> true
    | Some PausedInBb
    | None -> false

/// Eine angezeigte Kategorie. Felder wie `YnabMonthCategory`, ohne die Filter-Flags.
/// `Underfunded` (Fehlbetrag) und `Pause` braucht "Targets anwenden" (budgeting-h2zfn); die Seite
/// zeigt sie zusammen mit dem Target an (budgeting-kjsht).
type BudgetCategory = {
    Id: Guid
    Name: string
    Assigned: Money
    Activity: Money
    Available: Money
    Underfunded: Money option
    Target: Target option
    /// Die Pause des Targets in diesem Monat mit ihrer Quelle (ADR 0079). Fachregeln fragen nur
    /// `isPaused`, die Quelle braucht die Oberfläche für Aufheben und den Hinweis zur YNAB-Pause.
    Pause: PauseSource option
}

/// Ist das Target der Kategorie in diesem Monat pausiert, in BB, in YNAB oder auf beiden Seiten?
/// Die einzige Frage der Fachregeln (Fehlt, Frei, Targets anwenden).
let isPaused (c: BudgetCategory) : bool =
    c.Pause.IsSome

/// Eine Kategoriegruppe mit ihren sichtbaren Kategorien. Vom Server in der ungeordneten Folge der
/// Monatsantwort, die Seite ordnet sie mit `orderGroups`.
type BudgetGroup = {
    Name: string
    Categories: BudgetCategory list
}

/// Der Monat, wie die Budget-Seite ihn zeigt.
type BudgetMonth = {
    /// Erster des Monats als "YYYY-MM-01".
    Month: string
    ToBeBudgeted: Money
    Groups: BudgetGroup list
}

/// Der Rahmen der Budget-Seite (budgeting-g8s3w, ADR 0074): liegt je Seitenaufruf im Client, damit
/// ein Monatswechsel nur eine Anfrage kostet.
type BudgetFrame = {
    /// Der laufende Monat "YYYY-MM-01" nach Berliner Kalender.
    CurrentMonth: string
    /// Das Monatsfenster aus `GET /months`, aufsteigend.
    Months: string list
    /// Alle Kategorie-Ids flach, Gruppe für Gruppe aus `GET /categories`.
    CategoryOrder: Guid list
}

/// Ordnet Gruppen und Kategorien nach dem Rang in `order` (ADR 0074 Punkt 6). Der Rang einer Gruppe
/// ist der kleinste ihrer Kategorien. Ohne Rang steht eine Kategorie am Ende ihrer Gruppe und eine
/// Gruppe am Ende der Liste, jeweils nach Name, weil die Monatsfolge springt. Es fällt nichts weg.
let orderGroups (order: Guid list) (groups: BudgetGroup list) : BudgetGroup list =
    let ranks = order |> List.mapi (fun index id -> id, index) |> Map.ofList
    let rankOf (category: BudgetCategory) = ranks |> Map.tryFind category.Id
    let sortKey (rank: int option) (name: string) =
        match rank with
        | Some r -> 0, r, ""
        | None -> 1, 0, name
    groups
    |> List.map (fun group ->
        { group with
            Categories = group.Categories |> List.sortBy (fun c -> sortKey (rankOf c) c.Name) })
    |> List.sortBy (fun group ->
        // Die Kategorien stehen schon nach Rang, die erste trägt den kleinsten.
        sortKey (group.Categories |> List.tryHead |> Option.bind rankOf) group.Name)

/// Fehlt: BBs Antwort je Kategorie, auch ohne Target. Das Größere aus negativem Verfügbar und
/// Fehlbetrag, `None` ohne Betrag über 0. Bei einem pausierten Target (in BB, in YNAB oder beidem)
/// zählt der Fehlbetrag nicht, negatives Verfügbar bleibt (ADR 0073 Punkt 4).
let missingAmount (c: BudgetCategory) : Money option =
    let negative = if c.Available.Amount < 0m then -c.Available.Amount else 0m
    let underfunded =
        match c.Underfunded with
        | Some under when not (isPaused c) -> under.Amount
        | _ -> 0m
    let amount = if underfunded > negative then underfunded else negative
    if amount > 0m then Some { c.Available with Amount = amount } else None

/// Frei: Verfügbar minus offener Fehlbetrag (budgeting-c5w2n). Ein pausiertes Target sperrt nichts,
/// ohne Target ist alles Verfügbare frei. Grundlage der Warnung beim Holen und Abgeben.
let freeAmount (c: BudgetCategory) : Money =
    match c.Underfunded with
    | Some under when not (isPaused c) -> { c.Available with Amount = c.Available.Amount - under.Amount }
    | _ -> c.Available

// ============================================
// Targets anwenden (budgeting-h2zfn)
// ============================================

/// Milliunits eines Betrags (YNAB rechnet in Tausendsteln). Ein Betrag mit mehr als drei
/// Nachkommastellen wird gerundet; Beträge aus YNAB haben nie mehr.
let toMilliunits (money: Money) : int64 =
    int64 (Math.Round(money.Amount * 1000m, MidpointRounding.AwayFromZero))

/// Ein Betrag in Euro aus Milliunits, die Gegenrichtung zu `toMilliunits` (gespeicherte Bewegungen).
let euroOfMilliunits (milliunits: int64) : Money =
    { Amount = decimal milliunits / 1000m; Currency = "EUR" }

/// YNAB nimmt Zuweisungen nur auf 10 Milliunits genau an (12345 wurde 12350). Ein Betrag
/// dazwischen wäre eine stille Rundung und wird deshalb abgelehnt. Rechnet ohne Multiplikation,
/// damit ein zu großer Betrag nicht überläuft (budgeting-nfrds).
let isOnTenMilliunits (money: Money) : bool =
    money.Amount % 0.01m = 0m

/// Eine Kategorie der Vorschau: neu = altes Zugewiesen + Fehlbetrag.
type TargetLine = {
    CategoryId: Guid
    CategoryName: string
    GroupName: string
    OldAssigned: Money
    Underfunded: Money
    NewAssigned: Money
    /// Nur zur Anzeige; zählt nicht beim Vergleich der Vorschau (`previewsMatch`).
    Target: Target option
}

/// Eine Kategorie, deren Target pausiert ist: wird nicht angewendet, aber ausgewiesen.
type SkippedLine = {
    CategoryId: Guid
    CategoryName: string
    Underfunded: Money
}

/// Die Vorschau von "Targets anwenden". Schreibt nichts.
type TargetPreview = {
    /// Erster des Monats als "YYYY-MM-01".
    Month: string
    Lines: TargetLine list
    Skipped: SkippedLine list
    /// Summe der Fehlbeträge (= Summe der Erhöhungen).
    Total: Money
    /// Zu verteilen vor dem Anwenden.
    ToBeBudgeted: Money
    /// Zu verteilen nach dem Anwenden (Zu verteilen minus Summe), darf negativ sein.
    ToBeBudgetedAfter: Money
}

/// Stand einer Zeile einer Anwendung (das jüngste Ergebnis).
type LineOutcome =
    /// Nach YNAB geschrieben.
    | Written
    /// Nicht geschrieben, mit Grund (auch Rate-Limit). Erneut sendbar.
    | Failed of reason: string
    /// YNAB stand weder auf dem alten noch auf dem Zielwert: YNAB gewinnt, nichts geschrieben.
    | ChangedInYnab

type ApplyLineResult = {
    CategoryId: Guid
    CategoryName: string
    OldAssigned: Money
    NewAssigned: Money
    Outcome: LineOutcome
}

/// Ausgang einer Anwendung oder eines erneuten Sendens.
type ApplyReport = {
    ApplicationId: string
    Month: string
    Lines: ApplyLineResult list
    /// Zu verteilen, wie YNAB es nach dem Lauf meldet (`None`, wenn das erneute Lesen scheiterte).
    ActualToBeBudgeted: Money option
}

/// Antwort auf "Übernehmen": entweder ist der Stand gleich geblieben und es wurde
/// angewendet, oder YNAB steht anders als in der Vorschau und es wurde nichts geschrieben.
type ApplyResult =
    | Applied of ApplyReport
    | PreviewChanged of TargetPreview

/// Anzahl der Zeilen im Zustand `Written`.
let writtenCount (report: ApplyReport) : int =
    report.Lines |> List.filter (fun l -> l.Outcome = Written) |> List.length

/// Anzahl der Zeilen im Zustand `Failed`.
let failedCount (report: ApplyReport) : int =
    report.Lines |> List.filter (fun l -> match l.Outcome with Failed _ -> true | _ -> false) |> List.length

/// Ist dieser Monat der letzte im Fenster? `months` steht aufsteigend.
let isLastMonth (months: string list) (month: string) : bool =
    List.tryLast months = Some month

/// Kann dieser Bericht das Fenster geöffnet haben? Eine geschriebene Zuweisung ungleich 0 im letzten
/// Monat öffnet den nächsten (ADR 0074). Nur dann liest der Client den Rahmen neu.
let windowMayHaveGrown (months: string list) (report: ApplyReport) : bool =
    isLastMonth months report.Month
    && report.Lines |> List.exists (fun l -> l.Outcome = Written && l.NewAssigned.Amount <> 0m)

// ============================================
// Geld verschieben (budgeting-afd8h, ADR 0069)
// ============================================

/// Eine Seite einer Verschiebung: eine Kategorie oder Zu verteilen.
type MoveSide =
    | ToBeBudgetedSide
    | CategorySide of Guid

/// Der Auftrag des Clients. Keine Vorschau: wer 20 € verschiebt, meint 20 €, der Server rechnet
/// relativ zum frisch gelesenen Monat.
type MoveRequest = {
    Source: MoveSide
    Target: MoveSide
    /// Positiv, in Euro.
    Amount: Money
}

/// Das Feld Zugewiesen (budgeting-c5w2n, ADR 0078): der fertige Wert, absolut. Der Auftrag trägt
/// keinen erwarteten alten Stand, der eingegebene Wert gilt.
type AssignRequest = {
    CategoryId: Guid
    /// Das neue Zugewiesen in Euro, negativ ist gültig.
    Assigned: Money
}

/// Was ein Auftrag zur Pause will (budgeting-r3wq8): das Target in BB pausieren oder die BB-Pause
/// aufheben. Eine Pause aus YNAB bleibt davon unberührt (ADR 0073 Punkt 2).
type PauseIntent =
    | PauseTarget
    | LiftPause

/// Target pausieren oder die Pause aufheben (budgeting-t6p8z, ADR 0079). Der Monat geht als eigener
/// Parameter mit.
type TargetPauseRequest = {
    CategoryId: Guid
    Intent: PauseIntent
}

/// Die Ablehnung im vergangenen Monat (ADR 0073 Punkt 5, `targetsAllowed`). Geprüft an der Grenze und
/// im Ablauf, bevor YNAB gelesen wird.
[<Literal>]
let PausePastMonthText = "Target pausieren geht in vergangenen Monaten nicht."

// ============================================
// Target ändern und entfernen (budgeting-k8wq3, ADR 0072, ADR 0080)
// ============================================

/// Ein Feld im PATCH eines Targets: `Keep` lässt es weg, `SetTo` setzt es, `Clear` sendet `null`.
type FieldPatch<'T> =
    | Keep
    | SetTo of 'T
    | Clear

/// Der Rhythmus, den YNABs `goal_frequency` setzt. Er ersetzt das Target (ADR 0072 Punkt 4): BB sendet ihn
/// nur beim Anlegen und bei einem echten Wechsel der Art (budgeting-xp46a).
type TargetFrequency =
    | Weekly
    | Monthly
    | Yearly

/// Ein PATCH auf `/budgets/{id}/categories/{id}`: nur die gesetzten Felder gehen hinaus (ADR 0072
/// Punkt 3). `GoalTarget` in Milliunits, `TargetDate` als "YYYY-MM-DD".
type TargetPatch = {
    GoalTarget: FieldPatch<int64>
    TargetDate: FieldPatch<string>
    /// `goal_frequency`, nur beim Anlegen und beim Wechsel der Art. Nie zusammen mit `TargetDate`
    /// (`buildTargetBody` lehnt das ab, ADR 0072 Punkt 5).
    Frequency: TargetFrequency option
}

/// Bleibt der Rhythmus, oder wird er gesetzt? Beim Anlegen ist `SetRhythm` Pflicht. Bei einem bestehenden
/// Target ist `SetRhythm` mit dem gelesenen Rhythmus kein Wechsel (`targetKindChange`).
type RhythmChoice =
    | KeepRhythm
    | SetRhythm of TargetFrequency

/// Die Währung eines Targets: BB kennt nur Euro, die API-Grenze lehnt jede andere ab (budgeting-g2ty9, X11).
[<Literal>]
let TargetCurrency = "EUR"

/// Das gewünschte Target, wie das Formular es schickt.
type TargetDraft = {
    /// Der Betrag in Euro, 0 oder mehr.
    NewGoal: Money
    /// "YYYY-MM-DD", `None` heißt ohne Datum.
    NewDate: string option
    NewRhythm: RhythmChoice
}

/// Der Auftrag Target ändern oder entfernen. `Shown` ist das Target, das der Client gezeigt hat,
/// `Wanted = None` heißt entfernen. Der Monat geht als eigener Parameter mit.
type TargetChangeRequest = {
    CategoryId: Guid
    Shown: Target option
    Wanted: TargetDraft option
}

/// Antwort auf Target ändern. Ein Fehler kommt als `Error` mit konkreter Meldung.
type TargetChangeResult =
    /// Geschrieben, oder YNAB stand schon auf dem Wunsch. Trägt das Target danach (`None`: entfernt).
    | TargetWritten of Target option
    /// YNAB stand anders als gezeigt: nichts gesendet, nichts festgehalten. Trägt den frischen Stand.
    | TargetChangedInYnab of Target option
    /// Schritt 1 geschrieben, Schritt 2 (das Datum) nicht (budgeting-xp46a, ADR 0080 Punkt 6). Trägt den
    /// Stand nach Schritt 1 und die Meldung, etwa "Target angelegt, Datum 01.06.2027 nicht gesetzt: …".
    | TargetPartlyWritten of target: Target option * message: string

/// Die Ablehnung außerhalb des laufenden Monats (ADR 0080 Punkt 1, `targetEditAllowed`).
[<Literal>]
let TargetEditMonthText = "Target ändern geht nur im laufenden Monat."

/// Die Ablehnung eines neuen Targets ohne Rhythmus: BB legt nur NEED monatlich, wöchentlich oder jährlich an
/// (ADR 0072 Punkt 2).
[<Literal>]
let TargetRhythmMissingText = "Für ein neues Target fehlt der Rhythmus: monatlich, wöchentlich oder jährlich."

/// Die Ablehnung, wenn ohne Target in einem anderen Monat als dem laufenden angelegt werden soll, und der
/// Satz unter "Kein Target" dort (budgeting-g2ty9, X5).
[<Literal>]
let TargetCreateMonthText = "Target anlegen geht nur im laufenden Monat."

/// Die Ablehnung eines neuen Targets mit 0 € (budgeting-g2ty9, X8): YNAB legte sonst ein Target ohne
/// Fehlbetrag an.
[<Literal>]
let TargetCreateZeroText = "Ein neues Target braucht einen Betrag über 0 €."

/// Der Rhythmus als Wort: "monatlich", "alle 3 Monate", "ohne Rhythmus" (budgeting-g2ty9, X11). Ein Text
/// für Server und Client.
let rhythmWordText (rhythm: TargetRhythm) : string =
    match rhythm with
    | Weeks 1 -> "wöchentlich"
    | Months 1 -> "monatlich"
    | Years 1 -> "jährlich"
    | Weeks n -> $"alle {n} Wochen"
    | Months n -> $"alle {n} Monate"
    | Years n -> $"alle {n} Jahre"
    | NoRhythm -> "ohne Rhythmus"

/// Der Rhythmus, den YNAB nach `goal_frequency` liefert: jeweils mit Abstand 1.
let rhythmOfFrequency (frequency: TargetFrequency) : TargetRhythm =
    match frequency with
    | Weekly -> Weeks 1
    | Monthly -> Months 1
    | Yearly -> Years 1

/// "monatlich", "wöchentlich", "jährlich".
let frequencyText (frequency: TargetFrequency) : string = rhythmWordText (rhythmOfFrequency frequency)

/// Ist der Wunsch ein echter Wechsel der Art (ADR 0072 Punkt 4)? Ja, wenn `SetRhythm f` vom gelesenen
/// Rhythmus abweicht oder die heutige Art kein NEED ist. Gleicher Rhythmus bei NEED ist kein Wechsel, dann
/// geht nie `goal_frequency` hinaus: sonst ginge auch bei "jährlich auf jährlich" das Datum verloren.
/// Liefert den neuen Rhythmus.
let targetKindChange (shown: Target) (draft: TargetDraft) : TargetFrequency option =
    match draft.NewRhythm with
    | KeepRhythm -> None
    | SetRhythm f when shown.Kind = Need && shown.Rhythm = rhythmOfFrequency f -> None
    | SetRhythm f -> Some f

/// Was ein Wechsel der Art am gelesenen Target verliert (budgeting-xp46a).
type TargetLoss =
    /// `goal_day` ist gesetzt: der Tag geht verloren, BB kann ihn nicht schreiben.
    | LosesDay
    /// `goal_target_date` ist gesetzt und der Entwurf hat kein Datum.
    | LosesDate
    /// Die Art ist kein NEED (TB, TBD, MF und andere): BB legt nur NEED an.
    | LosesKind
    /// Der Rhythmus ist einer, den BB nicht herstellen kann (alle n Monate, Wochen oder Jahre mit n über 1,
    /// ein NEED ohne Kadenz). Der Wechsel ist endgültig.
    | LosesRhythm

/// Was am gelesenen Stand verloren geht, wenn der Entwurf gesendet wird. Leer heißt keine Warnung: auch
/// jede Änderung ohne Wechsel der Art ist leer, sie sendet kein `goal_frequency`. Rein.
let targetChangeLosses (shown: Target) (draft: TargetDraft) : TargetLoss list =
    match targetKindChange shown draft with
    | None -> []
    | Some _ ->
        let rhythmLost =
            match shown.Rhythm with
            | Weeks n
            | Months n
            | Years n -> n > 1
            | NoRhythm -> shown.Kind = Need
        [ if shown.Day.IsSome then LosesDay
          if shown.Date.IsSome && draft.NewDate.IsNone then LosesDate
          if shown.Kind <> Need then LosesKind
          if rhythmLost then LosesRhythm ]

/// Ein Betrag als "1.234,50 €", ohne Kultur und ohne Format-String, gleich unter .NET und Fable.
let euroText (money: Money) : string =
    let cents = int64 (Math.Round(abs money.Amount * 100m, MidpointRounding.AwayFromZero))
    let rec group (digits: string) =
        if digits.Length <= 3 then digits else group digits.[.. digits.Length - 4] + "." + digits.[digits.Length - 3 ..]
    let sign = if money.Amount < 0m && cents > 0L then "-" else ""
    let rest = (string (cents % 100L)).PadLeft(2, '0')
    $"{sign}{group (string (cents / 100L))},{rest} €"

/// "2027-06-01" als "01.06.2027". Ein anderer Text bleibt, wie er ist.
let dateText (iso: string) : string =
    match iso.Split '-' with
    | [| year; month; day |] when year.Length = 4 && month.Length = 2 && day.Length = 2 -> $"{day}.{month}.{year}"
    | _ -> iso

// ============================================
// Historie je Kategorie (budgeting-dyn8g)
// ============================================

/// Woher eine Budget-Bewegung stammt (ADR 0065, ADR 0069). Die Historie zeigt und filtert danach.
type MovementOrigin =
    | TargetsApplied
    /// Zwischen zwei Kategorien.
    | MoneyMoved
    /// Eine Seite ist Zu verteilen.
    | MoneyAssigned

/// Eine festgehaltene Budget-Bewegung, wie die Historie sie liest.
type HistoryMovement = {
    MovementId: Guid
    /// Die Gruppen-Id des Durchlaufs (`application_id`, ADR 0069 Punkt 1).
    ApplicationId: Guid
    /// Zeitpunkt des Festhaltens, UTC.
    OccurredAt: DateTime
    Origin: MovementOrigin
    /// "YYYY-MM-01".
    Month: string
    CategoryId: Guid
    CategoryName: string
    OldAssigned: Money
    NewAssigned: Money
    /// Bei einer Verschiebung die Kategorie auf der anderen Seite (dieselbe Gruppen-Id), sonst `None`.
    Counterpart: string option
    /// Das jüngste Ergebnis. `None`: es ist noch keins angehängt.
    Latest: LineOutcome option
}

/// Die Netto-Aktivität einer Kategorie in einem Monat aus dem Kassenbuch (Erstattungen mindern,
/// gelöschte Buchungen zählen nicht, Splits mit ihrem Anteil). Ausgaben sind negativ wie bei YNAB.
type MonthSpending = {
    /// "YYYY-MM-01".
    Month: string
    Activity: Money
}

/// Ein Pause-Ereignis aus `target_pause_events` (budgeting-t6p8z). Es geht nicht nach YNAB.
type HistoryPause = {
    EventId: Guid
    /// Zeitpunkt des Festhaltens, UTC.
    OccurredAt: DateTime
    /// "YYYY-MM-01".
    Month: string
    CategoryId: Guid
    CategoryName: string
}

/// Was eine Target-Änderung tat (budgeting-k8wq3, budgeting-xp46a).
type TargetChangeKind =
    /// Betrag, Datum oder beides geändert.
    | TargetEdited
    /// `goal_target: null`.
    | TargetRemoved
    /// Neu angelegt, NEED mit diesem Rhythmus.
    | TargetCreated of TargetFrequency
    /// Die Art gewechselt (`goal_frequency`), auf NEED mit diesem Rhythmus.
    | TargetKindChanged of TargetFrequency

/// "angelegt" beim Anlegen, sonst "geändert".
let private changedWord (kind: TargetChangeKind) : string =
    match kind with
    | TargetCreated _ -> "angelegt"
    | TargetEdited
    | TargetRemoved
    | TargetKindChanged _ -> "geändert"

/// Die Meldung, wenn Schritt 2 nicht geschrieben ist: "Target angelegt, Datum 01.06.2027 nicht gesetzt:
/// <Grund>", beim Wechsel der Art "Target geändert, …".
let targetDateNotSetText (kind: TargetChangeKind) (date: string) (reason: string) : string =
    $"Target {changedWord kind}, Datum {dateText date} nicht gesetzt: {reason}"

/// Die Meldung, wenn YNABs Antwort auf Schritt 1 einen anderen Rhythmus zeigt und Schritt 2 deshalb
/// zurückgehalten ist (budgeting-g2ty9, X9): "Target in YNAB mit anderem Rhythmus angelegt, Datum
/// 01.06.2027 nicht gesetzt: <Grund>".
let targetRhythmUnexpectedText (kind: TargetChangeKind) (date: string) (reason: string) : string =
    $"Target in YNAB mit anderem Rhythmus {changedWord kind}, Datum {dateText date} nicht gesetzt: {reason}"

/// Eine festgehaltene Target-Änderung aus `target_changes`, mit dem jüngsten Ergebnis je Schritt (ADR 0080
/// Punkt 2 und 4, budgeting-g2ty9 X7). Keine Budget-Bewegung: das Zugewiesen bleibt.
type HistoryTargetChange = {
    ChangeId: Guid
    /// Zeitpunkt des Festhaltens, UTC.
    OccurredAt: DateTime
    /// Der Monat der Änderung, immer der laufende, "YYYY-MM-01".
    Month: string
    CategoryId: Guid
    CategoryName: string
    ChangeKind: TargetChangeKind
    GoalBefore: Money option
    GoalAfter: Money option
    /// "YYYY-MM-DD".
    DateBefore: string option
    DateAfter: string option
    /// Das jüngste Ergebnis von Schritt 1 (alte Zeilen ohne Schritt zählen als Schritt 1). `None`: noch keins
    /// angehängt.
    Latest: LineOutcome option
    /// Das jüngste Ergebnis von Schritt 2, dem Datum nach Anlegen oder Wechsel der Art. `None`: kein
    /// zweiter Schritt oder noch kein Ergebnis.
    DateStep: LineOutcome option
}

/// Ein Eintrag im Tab Historie: eine Budget-Bewegung, ein Pause-Ereignis oder eine Target-Änderung.
type HistoryEntry =
    | Movement of HistoryMovement
    | TargetPaused of HistoryPause
    | PauseLifted of HistoryPause
    | TargetChanged of HistoryTargetChange

/// Der Zeitpunkt eines Eintrags, nach dem die Historie ordnet.
let historyEntryTime (entry: HistoryEntry) : DateTime =
    match entry with
    | Movement m -> m.OccurredAt
    | TargetPaused p
    | PauseLifted p -> p.OccurredAt
    | TargetChanged c -> c.OccurredAt

/// Antwort der Historie für Kategorie und Monat.
type CategoryHistory = {
    /// Bewegungen und Pause-Ereignisse der Kategorie im Monat, neueste zuerst.
    Entries: HistoryEntry list
    /// Die 6 Monate bis einschließlich des gezeigten, aufsteigend. Monate ohne Buchung stehen mit 0 da.
    Spending: MonthSpending list
}

/// Der Stand einer Bewegung in der Historie: ihr jüngstes Ergebnis oder noch offen. Neben
/// `LineOutcome option` steht er, weil die Historie einen Fall mehr kennt als ein Ergebnis: "noch
/// offen" ist kein Ergebnis, sondern sein Fehlen. Die Ansicht matcht so über einen Typ mit benannten
/// Fällen, statt `None` an jeder Stelle als "offen" zu deuten.
type HistoryStatus =
    | HistoryWritten
    | HistoryFailed of reason: string
    | HistoryChangedInYnab
    /// Kein Ergebnis angehängt: der Lauf brach vor dieser Zeile ab oder läuft noch.
    | HistoryOpen
    /// Nur bei einer Target-Änderung in zwei Schritten (budgeting-g2ty9, X7): Schritt 1 ist geschrieben,
    /// das Datum in Schritt 2 nicht, mit dessen Grund. Das Target steht ohne Datum in YNAB.
    | HistoryPartlyWritten of reason: string

/// Eine Zeile im Tab Historie, aus Sicht der Kategorie des Eintrags.
type HistoryLine = {
    /// Die Id der Bewegung oder des Pause-Ereignisses.
    EntryId: Guid
    OccurredAt: DateTime
    /// Was sich am Zugewiesen der Kategorie geändert hat: neu minus alt. Eine Pause hat keinen Betrag.
    Change: Money option
    /// Der Text hinter dem Betrag: "nach Urlaub", "von Lebensmittel", "zugewiesen",
    /// "zurück nach Zu verteilen", "Targets anwenden", "Target pausiert", "Pause aufgehoben",
    /// "Target 50,00 € → 60,00 €", "Target entfernt".
    Text: string
    /// Der Stand beim Schreiben nach YNAB. `None` bei einer Pause: sie geht nie nach YNAB, die Zeile
    /// zeigt nur die Uhrzeit (budgeting-h7q4v, H1).
    Status: HistoryStatus option
}

/// Was sich am Zugewiesen einer Bewegung geändert hat: neu minus alt.
let private changeOf (m: HistoryMovement) : Money =
    { m.NewAssigned with Amount = m.NewAssigned.Amount - m.OldAssigned.Amount }

let private historyStatus (latest: LineOutcome option) : HistoryStatus =
    match latest with
    | Some Written -> HistoryWritten
    | Some (Failed reason) -> HistoryFailed reason
    | Some ChangedInYnab -> HistoryChangedInYnab
    | None -> HistoryOpen

/// Bewegung zu Zeile. Die Richtung folgt aus dem Vorzeichen von neu minus alt: Bei einer Verschiebung
/// geht Geld nach der Gegenseite oder kommt von ihr, bei Zuweisen kommt es aus Zu verteilen oder geht
/// dorthin zurück.
let historyLine (m: HistoryMovement) : HistoryLine =
    let change = changeOf m
    let incoming = change.Amount >= 0m
    let text =
        match m.Origin, m.Counterpart with
        | TargetsApplied, _ -> "Targets anwenden"
        | MoneyAssigned, _ -> if incoming then "zugewiesen" else "zurück nach Zu verteilen"
        | MoneyMoved, Some other -> if incoming then $"von {other}" else $"nach {other}"
        | MoneyMoved, None -> "verschoben"
    { EntryId = m.MovementId
      OccurredAt = m.OccurredAt
      Change = Some change
      Text = text
      Status = Some (historyStatus m.Latest) }

/// Der Stand einer Target-Änderung aus den Ergebnissen ihrer Schritte (budgeting-g2ty9, X7). Ist Schritt 1
/// geschrieben, entscheidet Schritt 2: fehlgeschlagen heißt teilweise geschrieben, ohne Ergebnis bei
/// einem erwarteten Datum noch offen. Sonst gilt Schritt 1 wie bei einer Bewegung.
let targetChangeStatus (c: HistoryTargetChange) : HistoryStatus =
    // Zwei Schritte hat nur ein Anlegen oder Wechsel der Art mit Datum (`planTargetWrite`).
    let twoSteps =
        match c.ChangeKind, c.DateAfter with
        | (TargetCreated _ | TargetKindChanged _), Some _ -> true
        | _ -> false
    match c.Latest, c.DateStep with
    | Some Written, Some (Failed reason) -> HistoryPartlyWritten reason
    | Some Written, Some ChangedInYnab -> HistoryChangedInYnab
    | Some Written, None when twoSteps -> HistoryOpen
    | latest, _ -> historyStatus latest

/// Der Text einer Target-Änderung: "Target 50,00 € → 60,00 €", "Datum 01.06.2027", "Datum entfernt",
/// beides mit Komma getrennt, oder "Target entfernt". Angelegt: "Target angelegt, 50,00 € monatlich", ein
/// Datum mit Komma dahinter. Art gewechselt: "Rhythmus jährlich", Betrag und Datum wie beim Ändern dahinter.
/// Ist nur Schritt 1 geschrieben, heißt das Datum "Datum 01.06.2027 nicht gesetzt" (budgeting-g2ty9, X7).
let targetChangeText (c: HistoryTargetChange) : string =
    let amount (m: Money option) = m |> Option.map euroText |> Option.defaultValue "kein Betrag"
    let dateNotSet =
        match targetChangeStatus c with
        | HistoryPartlyWritten _ -> " nicht gesetzt"
        | _ -> ""
    let changes =
        [ if c.GoalBefore <> c.GoalAfter then
              $"Target {amount c.GoalBefore} → {amount c.GoalAfter}"
          if c.DateBefore <> c.DateAfter || dateNotSet <> "" then
              match c.DateAfter with
              | Some date -> $"Datum {dateText date}{dateNotSet}"
              | None -> "Datum entfernt" ]
    match c.ChangeKind with
    | TargetRemoved -> "Target entfernt"
    | TargetCreated frequency ->
        [ $"Target angelegt, {amount c.GoalAfter} {frequencyText frequency}"
          match c.DateAfter with
          | Some date -> $"Datum {dateText date}{dateNotSet}"
          | None -> () ]
        |> String.concat ", "
    | TargetKindChanged frequency -> $"Rhythmus {frequencyText frequency}" :: changes |> String.concat ", "
    | TargetEdited ->
        match changes with
        | [] -> "Target unverändert"
        | parts -> String.concat ", " parts

/// Eintrag zu Zeile. Eine Pause hat weder Betrag noch Stand: sie geht nie nach YNAB (ADR 0073 Punkt 5).
/// Eine Target-Änderung behält ihren echten Stand.
let historyEntryLine (entry: HistoryEntry) : HistoryLine =
    let pauseLine (p: HistoryPause) (text: string) =
        { EntryId = p.EventId
          OccurredAt = p.OccurredAt
          Change = None
          Text = text
          Status = None }
    match entry with
    | Movement m -> historyLine m
    | TargetPaused p -> pauseLine p "Target pausiert"
    | PauseLifted p -> pauseLine p "Pause aufgehoben"
    | TargetChanged c ->
        { EntryId = c.ChangeId
          OccurredAt = c.OccurredAt
          Change = None
          Text = targetChangeText c
          Status = Some (targetChangeStatus c) }

// ============================================
// Allgemeine Historie (budgeting-h7q4v)
// ============================================

/// Eine Kategorie eines Durchlaufs Targets anwenden, sichtbar nach dem Aufklappen.
type HistoryRunLine = {
    MovementId: Guid
    CategoryName: string
    /// Neu minus alt.
    Change: Money
    Status: HistoryStatus
}

/// Der Stand eines Durchlaufs (budgeting-c6vn4, A6). Eigener Typ neben `HistoryStatus`, weil ein
/// Durchlauf einen Fall mehr kennt: einige seiner Kategorien scheiterten, andere nicht. Die Zahl steckt
/// so nicht im Fehlgrund, die Ansicht formt den Text.
type RunStatus =
    | RunWritten
    | RunOpen
    | RunChangedInYnab
    /// Gescheitert mit dem Grund der ersten gescheiterten Bewegung. Bei Targets anwenden nur, wenn alle
    /// Kategorien scheiterten oder es nur eine gibt (budgeting-b5n8q).
    | RunFailed of reason: string
    /// Bei Targets anwenden mit mehreren Kategorien: `failed` von `total` scheiterten, mindestens eine
    /// nicht. Die Gründe stehen in den Einzelzeilen.
    | RunPartlyFailed of failed: int * total: int

/// Ein Durchlauf der allgemeinen Historie: alle Bewegungen mit derselben Gruppen-Id (ADR 0069 Punkt 1),
/// als eine Zeile.
type HistoryRun = {
    /// Die Gruppen-Id.
    RunId: Guid
    /// Zeitpunkt des Festhaltens, UTC: der jüngste seiner Bewegungen.
    OccurredAt: DateTime
    Origin: MovementOrigin
    /// "YYYY-MM-01".
    Month: string
    /// Art, Kategorie oder Richtung, Anzahl, ohne Betrag (budgeting-c6vn4, A5): "Verschieben,
    /// Lebensmittel → Urlaub", "Zuweisen, Urlaub", "Targets anwenden, 37 Kategorien".
    Text: string
    /// Der Betrag rechts in der Zeile. Targets anwenden: die Summe neu minus alt. Zuweisen: neu minus alt.
    /// Verschieben: der verschobene Betrag, immer positiv (die Richtung steht im Text).
    Amount: Money
    /// Fehlgeschlagen, sobald eine Bewegung fehlschlug: bei Targets anwenden teilweise fehlgeschlagen,
    /// solange mindestens eine Kategorie nicht scheiterte, sonst gescheitert mit dem ersten Grund. Ohne
    /// Fehlschlag noch offen, in YNAB geändert oder geschrieben, in dieser Rangfolge.
    Status: RunStatus
    /// Bei Targets anwenden die Kategorien in der Reihenfolge des Durchlaufs, sonst leer.
    Lines: HistoryRunLine list
}

/// Name einer Seite einer Verschiebung, die weder in der Abfrage noch als Gegenseite vorkommt.
[<Literal>]
let UnknownCategoryText = "unbekannte Kategorie"

/// Der Stand eines Durchlaufs aus den Ständen seiner Bewegungen. Bei Targets anwenden mit mehreren
/// Kategorien zählt er die Fehlschläge, die Gründe je Kategorie stehen in den Einzelzeilen. Scheitern
/// alle, ist er gescheitert mit dem ersten Grund (budgeting-b5n8q).
let private runStatus (origin: MovementOrigin) (statuses: HistoryStatus list) : RunStatus =
    let failed = statuses |> List.choose (function HistoryFailed reason -> Some reason | _ -> None)
    match failed, origin with
    | _ :: _, TargetsApplied when List.length failed < List.length statuses ->
        RunPartlyFailed (List.length failed, List.length statuses)
    | reason :: _, _ -> RunFailed reason
    | [], _ ->
        if List.contains HistoryOpen statuses then RunOpen
        elif List.contains HistoryChangedInYnab statuses then RunChangedInYnab
        else RunWritten

/// Text und Betrag einer Verschiebung: Quelle und Ziel, der Betrag ohne Vorzeichen. Fehlt eine Seite in
/// der Abfrage, kommt ihr Name aus der Gegenseite der anderen, sonst heißt sie "unbekannte Kategorie".
let private moveTextAndAmount (movements: HistoryMovement list) : string * Money =
    let source = movements |> List.tryFind (fun m -> (changeOf m).Amount < 0m)
    let target = movements |> List.tryFind (fun m -> (changeOf m).Amount > 0m)
    let named (side: HistoryMovement option) (other: HistoryMovement option) =
        match side, other with
        | Some m, _ -> Some m.CategoryName
        | None, Some o -> o.Counterpart
        | None, None -> None
    let from = named source target |> Option.defaultValue UnknownCategoryText
    let towards = named target source |> Option.defaultValue UnknownCategoryText
    let any = List.choose id [ source; target ] |> List.tryHead |> Option.defaultValue (List.head movements)
    let amount = changeOf any
    $"Verschieben, {from} → {towards}", { amount with Amount = abs amount.Amount }

let private historyRun (movements: HistoryMovement list) : HistoryRun =
    // Die Abfrage liefert neueste zuerst, innerhalb einer Transaktion die zuletzt eingefügte vorn.
    let inOrder = List.rev movements
    let first = List.head inOrder
    let statuses = inOrder |> List.map (fun m -> historyStatus m.Latest)
    let text, amount, lines =
        match first.Origin with
        | TargetsApplied ->
            let count = List.length inOrder
            let total = { first.NewAssigned with Amount = inOrder |> List.sumBy (fun m -> (changeOf m).Amount) }
            let categories = if count = 1 then "1 Kategorie" else $"{count} Kategorien"
            $"Targets anwenden, {categories}",
            total,
            inOrder
            |> List.map (fun m ->
                { MovementId = m.MovementId
                  CategoryName = m.CategoryName
                  Change = changeOf m
                  Status = historyStatus m.Latest })
        | MoneyAssigned -> $"Zuweisen, {first.CategoryName}", changeOf first, []
        | MoneyMoved ->
            let text, amount = moveTextAndAmount inOrder
            text, amount, []
    { RunId = first.ApplicationId
      OccurredAt = inOrder |> List.map (fun m -> m.OccurredAt) |> List.max
      Origin = first.Origin
      Month = first.Month
      Text = text
      Amount = amount
      Status = runStatus first.Origin statuses
      Lines = lines }

/// Bewegungen zu Durchlauf-Zeilen: eine Zeile je Gruppen-Id, neueste zuerst.
let historyRuns (movements: HistoryMovement list) : HistoryRun list =
    movements
    |> List.groupBy (fun m -> m.ApplicationId)
    |> List.map (snd >> historyRun)
    |> List.sortByDescending (fun r -> r.OccurredAt)

let private lastSundayUtc (year: int) (month: int) : DateTime =
    let last = DateTime(year, month, DateTime.DaysInMonth(year, month), 0, 0, 0, DateTimeKind.Utc)
    last.AddDays(-(float (int last.DayOfWeek)))

/// Ein UTC-Zeitpunkt als Berliner Wanduhrzeit, unabhängig von der Zeitzone des Rechners oder
/// Browsers. Fable kennt `TimeZoneInfo` nicht, deshalb stehen die EU-Regeln hier: Sommerzeit (UTC+2)
/// vom letzten Sonntag im März bis zum letzten Sonntag im Oktober, jeweils 01:00 UTC, sonst UTC+1.
let berlinTime (instant: DateTime) : DateTime =
    let utc = if instant.Kind = DateTimeKind.Local then instant.ToUniversalTime() else instant
    let at (d: DateTime) = DateTime(d.Year, d.Month, d.Day, d.Hour, d.Minute, d.Second, DateTimeKind.Utc)
    let moment = at utc
    let summerStart = (lastSundayUtc utc.Year 3).AddHours 1.0
    let summerEnd = (lastSundayUtc utc.Year 10).AddHours 1.0
    let offset = if moment >= summerStart && moment < summerEnd then 2.0 else 1.0
    let local = moment.AddHours offset
    DateTime(local.Year, local.Month, local.Day, local.Hour, local.Minute, local.Second, DateTimeKind.Unspecified)

/// Der Zeitpunkt einer Zeile in Berliner Zeit: "03.10. 11:30".
let historyTimeText (instant: DateTime) : string =
    let t = berlinTime instant
    let two (n: int) = (string n).PadLeft(2, '0')
    $"{two t.Day}.{two t.Month}. {two t.Hour}:{two t.Minute}"

/// Der Aufbau von Verfügbar: Vortrag aus dem Vormonat, dazu Zugewiesen und Aktivität.
type AvailableBuildup = {
    CarryOver: Money
    Assigned: Money
    Activity: Money
    Available: Money
}

/// Rechnet den Vortrag aus dem geladenen Monat zurück: Verfügbar minus Zugewiesen minus Aktivität.
/// Keine Anfrage, YNABs Werte bleiben, wie sie sind (ADR 0065).
let availableBuildup (c: BudgetCategory) : AvailableBuildup =
    { CarryOver = { c.Available with Amount = c.Available.Amount - c.Assigned.Amount - c.Activity.Amount }
      Assigned = c.Assigned
      Activity = c.Activity
      Available = c.Available }

/// Wie viele Monate die Ausgaben im Tab Historie zeigen.
[<Literal>]
let SpendingMonthCount = 6

/// Ein Balken der Ausgaben der letzten 6 Monate.
type SpendingBar = {
    /// "YYYY-MM-01".
    Month: string
    /// Ausgaben, also die Netto-Aktivität mit umgekehrtem Vorzeichen. Überwiegen Erstattungen, ist der
    /// Wert negativ.
    Spending: Money
    /// Höhe in Prozent des größten Balkens, 0 bis 100. Ein Monat ohne Ausgaben hat 0.
    Percent: int
    /// Wie der Monat zum laufenden steht. Der laufende ist noch nicht vorbei.
    Kind: MonthKind
}

/// Die Balken aus den Monatssummen. `currentMonth` ist der laufende Monat ("YYYY-MM-01").
let spendingBars (currentMonth: string) (spending: MonthSpending list) : SpendingBar list =
    let spent (s: MonthSpending) = -s.Activity.Amount
    let highest = spending |> List.map spent |> List.fold max 0m
    spending
    |> List.map (fun s ->
        let amount = spent s
        let percent =
            if highest <= 0m || amount <= 0m then 0
            else int (Math.Round(amount / highest * 100m, MidpointRounding.AwayFromZero))
        { Month = s.Month
          Spending = { s.Activity with Amount = amount }
          Percent = percent
          Kind = monthKind currentMonth s.Month })
