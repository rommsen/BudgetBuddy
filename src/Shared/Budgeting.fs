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

/// Das Target einer Kategorie: BBs Regel aus dem Stream `budgeting:target:<categoryId>` (ADR 0084 Punkt 6,
/// budgeting-eh8xe). Einmal aus YNABs `goal_*`-Feldern übernommen, danach nur in BB geändert.
type Target = {
    Goal: Money
    Rhythm: TargetRhythm
    /// Die Art aus `goal_type` (budgeting-k8wq3).
    Kind: TargetKind
    /// Das Datum aus `goal_target_date` als "YYYY-MM-DD", wie der Monatsschlüssel ein String.
    Date: string option
    /// `goal_needs_whole_amount`: `Some false` heißt "Auffüllen bis" (F6).
    NeedsWholeAmount: bool option
    /// Der Tag: bei wöchentlich der Wochentag (0 Sonntag bis 6 Samstag, wie YNABs `goal_day`), in BB setzbar
    /// (budgeting-eh8xe). Bei anderen Rhythmen der aus YNAB übernommene Wert.
    Day: int option
    /// Der Anlegemonat als "YYYY-MM-01" (budgeting-pf8n7): bei der Target-Übernahme aus `goal_creation_month`,
    /// bei einer in BB neuen Regel (Anlegen, Wechsel der Art) der Monat, ab dem sie gilt; eine Änderung behält
    /// ihn (`TargetRules.planTargetChange`). BBs Fehlbetrag beginnt damit die erste Periode eines Ziels mit
    /// Fälligkeit.
    CreatedIn: string option
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
    /// Das Goal aus YNAB: `None`, wenn YNAB keins liefert. Die Seite zeigt BBs Regel (budgeting-eh8xe); das
    /// Goal sagt nur, ob eine Kategorie noch auf die Target-Übernahme wartet.
    Target: Target option
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

/// Das Goal einer Kategorie aus `GET /budgets/{id}/categories` (`goal_*`), Quelle der Target-Übernahme
/// (ADR 0084 Punkt 5 und 6, budgeting-eh8xe). `None` ohne Goal.
type YnabCategoryGoal = {
    Id: Guid
    Name: string
    Target: Target option
}

/// Die Pause eines Targets im Monat (ADR 0079, Nachtrag ADR 0084): es gibt sie nur in BB, das jüngste
/// Pause-Ereignis für Kategorie und Monat ist `TargetPaused`. Eine Pause in YNAB wirkt in BB nicht
/// (budgeting-eh8xe).
type PauseSource =
    | PausedInBb

/// Eine angezeigte Kategorie. Felder wie `YnabMonthCategory`, ohne die Filter-Flags.
/// `Underfunded` (Fehlbetrag) und `Pause` braucht "Targets anwenden" (budgeting-h2zfn); die Seite
/// zeigt sie zusammen mit dem Target an (budgeting-kjsht). Der Fehlbetrag ist BBs, gerechnet gegen das Soll
/// (budgeting-hbhzf, ADR 0084 Punkt 7): `None` ohne Target oder wenn BB ihn nicht rechnet, `Some 0` wenn erfüllt.
type BudgetCategory = {
    Id: Guid
    Name: string
    Assigned: Money
    Activity: Money
    Available: Money
    Underfunded: Money option
    Target: Target option
    /// Die Pause des Targets in diesem Monat (ADR 0079). Fachregeln fragen nur `isPaused`.
    Pause: PauseSource option
    /// Offene Rückschreibung (ADR 0084, budgeting-dgrp7): das Zugewiesen ist BBs Soll und noch nicht in
    /// YNAB. Trägt die Gruppen-Id der Entscheidung, über sie läuft "erneut senden". `None`: Soll und YNAB
    /// stimmen überein. Optional, damit ein älterer Client oder Server ohne das Feld weiter liest.
    OpenWriteBack: Guid option
}

/// Ist das Target der Kategorie in diesem Monat pausiert? Die einzige Frage der Fachregeln (Fehlt, Frei,
/// Targets anwenden).
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
    /// Die Monate ("YYYY-MM-01", ab dem laufenden), für die Roman die Leiste Monatserster ausgeblendet hat
    /// (budgeting-m3x7f). Zustand, kein Ereignis (ADR 0083): Ausblenden bewegt kein Geld.
    MonthStartHidden: string list
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
/// Fehlbetrag, `None` ohne Betrag über 0. Bei einem pausierten Target zählt der Fehlbetrag nicht, negatives
/// Verfügbar bleibt (ADR 0073 Punkt 4).
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
    int64 (Math.Round(money.Amount * 1000m, 0, MidpointRounding.AwayFromZero))

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
    /// Nur zur Anzeige; zählt nicht beim Vergleich der Vorschau (`chosenLinesIfCurrent`).
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
    /// Die Befehls-Id des Übernehmens (ADR 0084 Punkt 8). Die Vorschau liefert `None`, der Client setzt
    /// sie je Absicht. `chosenLinesIfCurrent` vergleicht sie nicht. Ein alter Client lässt das Feld weg.
    CommandId: Guid option
}

/// Die Zeilen von Targets anwenden aus dem Monat: jede Kategorie mit Fehlbetrag über 0 und ohne pausiertes Target,
/// neu = Soll + Fehlbetrag; pausierte mit Fehlbetrag über 0 stehen getrennt. Fehlbetrag 0 oder unbekannt fällt weg.
/// Eine Quelle für die Vorschau des Servers und das Feld Targets anwenden der Leiste (budgeting-m3x7f).
let targetApplicationLines (month: BudgetMonth) : TargetLine list * SkippedLine list =
    let lines, skipped =
        month.Groups
        |> List.collect (fun g -> g.Categories |> List.map (fun c -> g.Name, c))
        |> List.fold
            (fun (lines, skipped) (groupName, c) ->
                match c.Underfunded with
                | Some under when under.Amount > 0m ->
                    if isPaused c then
                        lines, ({ CategoryId = c.Id; CategoryName = c.Name; Underfunded = under } : SkippedLine) :: skipped
                    else
                        let line =
                            { CategoryId = c.Id
                              CategoryName = c.Name
                              GroupName = groupName
                              OldAssigned = c.Assigned
                              Underfunded = under
                              NewAssigned = { Amount = c.Assigned.Amount + under.Amount; Currency = c.Assigned.Currency }
                              Target = c.Target }
                        line :: lines, skipped
                | _ -> lines, skipped)
            ([], [])
    List.rev lines, List.rev skipped

/// Die Summe von Zeilen: was sie zusammen zuweisen (Summe der Fehlbeträge).
let linesTotal (currency: string) (lines: TargetLine list) : Money =
    { Amount = lines |> List.sumBy (fun l -> l.Underfunded.Amount); Currency = currency }

/// Was Targets anwenden im Monat zuweisen würde. Das Feld Targets anwenden der Leiste zeigt es, die Vorschau
/// rechnet ihre Summe mit denselben Zeilen (`targetApplicationLines`, `linesTotal`).
let targetApplicationTotal (month: BudgetMonth) : Money =
    linesTotal month.ToBeBudgeted.Currency (fst (targetApplicationLines month))

/// Die Vorschau mit diesen Zeilen: Summe und Zu verteilen danach folgen ihnen. So entsteht die Vorschau des Servers,
/// und so folgt die Auswahl im Dialog (abgewählte Zeilen fallen weg, budgeting-m3x7f).
let previewWithLines (preview: TargetPreview) (lines: TargetLine list) : TargetPreview =
    let total = linesTotal preview.ToBeBudgeted.Currency lines
    { preview with
        Lines = lines
        Total = total
        ToBeBudgetedAfter = { preview.ToBeBudgeted with Amount = preview.ToBeBudgeted.Amount - total.Amount } }

/// Stand einer Zeile einer Anwendung (das jüngste Ergebnis).
type LineOutcome =
    /// Nach YNAB geschrieben.
    | Written
    /// Nicht geschrieben, mit Grund (auch Rate-Limit). Erneut sendbar.
    | Failed of reason: string
    /// YNAB stand weder auf dem alten noch auf dem Zielwert: YNAB gewinnt, nichts geschrieben.
    | ChangedInYnab
    /// BB schloss die Zeile ungesendet, YNAB blieb unverändert (budgeting-my8p8): die Gegenseite einer
    /// Verschiebung ist verloren, oder eine spätere Entscheidung in BB setzte dieselbe Kategorie.
    | WithdrawnInBb

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
    /// Die Befehls-Id der Absicht (ADR 0084 Punkt 8): eine Wiederholung mit derselben Id verschiebt nicht
    /// noch einmal. `None` bei einem alten Client.
    CommandId: Guid option
}

/// Das Feld Zugewiesen (budgeting-c5w2n, ADR 0078): der fertige Wert, absolut. Der Auftrag trägt
/// keinen erwarteten alten Stand, der eingegebene Wert gilt.
type AssignRequest = {
    CategoryId: Guid
    /// Das neue Zugewiesen in Euro, negativ ist gültig.
    Assigned: Money
    /// Die Befehls-Id der Absicht (ADR 0084 Punkt 8): eine verspätete Wiederholung setzt keinen
    /// späteren Wert zurück. `None` bei einem alten Client.
    CommandId: Guid option
}

/// Was ein Auftrag zur Pause will (budgeting-r3wq8): das Target in BB pausieren oder die Pause aufheben.
type PauseIntent =
    | PauseTarget
    | LiftPause

/// Target pausieren oder die Pause aufheben (budgeting-t6p8z, ADR 0079). Der Monat geht als eigener
/// Parameter mit.
type TargetPauseRequest = {
    CategoryId: Guid
    Intent: PauseIntent
    /// Die Befehls-Id der Absicht (ADR 0084 Punkt 8): eine Wiederholung hebt kein späteres Aufheben auf.
    /// `None` bei einem alten Client.
    CommandId: Guid option
}

/// Die Ablehnung im vergangenen Monat (ADR 0073 Punkt 5, `targetsAllowed`). Geprüft an der Grenze und
/// im Ablauf, bevor YNAB gelesen wird.
[<Literal>]
let PausePastMonthText = "Target pausieren geht in vergangenen Monaten nicht."

// ============================================
// Target ändern und entfernen (budgeting-k8wq3, ADR 0080, budgeting-eh8xe)
// ============================================

/// Der Rhythmus, den BB beim Anlegen und bei einem Wechsel der Art setzt (budgeting-xp46a): jeweils mit
/// Abstand 1.
type TargetFrequency =
    | Weekly
    | Monthly
    | Yearly

/// Bleibt der Rhythmus, oder wird er gesetzt? Beim Anlegen ist `SetRhythm` Pflicht. Bei einem bestehenden
/// Target ist `SetRhythm` mit dem gezeigten Rhythmus kein Wechsel (`targetKindChange`).
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
    /// Der Wochentag eines wöchentlichen Targets, 0 Sonntag bis 6 Samstag (budgeting-eh8xe). `None` heißt: der
    /// Tag des gezeigten Targets bleibt, auch bei einem älteren Client ohne das Feld. Einen Wochentag entfernen
    /// gibt es nicht, ein wöchentliches Target braucht einen (das Formular sperrt Speichern ohne). Optional,
    /// damit ein älterer Client ohne das Feld weiter sendet.
    NewDay: int option
}

/// Der Auftrag Target ändern oder entfernen. `Shown` ist das Target, das der Client gezeigt hat,
/// `Wanted = None` heißt entfernen. Der Monat geht als eigener Parameter mit.
type TargetChangeRequest = {
    CategoryId: Guid
    Shown: Target option
    Wanted: TargetDraft option
    /// Die Befehls-Id der Absicht (ADR 0084 Punkt 8, budgeting-eh8xe): eine Wiederholung mit derselben Id
    /// hängt nichts an und bekommt die Antwort des ersten Mals. `None` bei einem alten Client.
    CommandId: Guid option
}

/// Antwort auf Target ändern. Ein Fehler kommt als `Error` mit konkreter Meldung.
type TargetChangeResult =
    /// In BB festgehalten, oder BB stand schon auf dem Wunsch. Trägt das Target danach (`None`: entfernt).
    | TargetWritten of Target option
    /// Das gezeigte Target ist nicht mehr BBs Stand (budgeting-eh8xe, ADR 0080 Punkt 3): nichts festgehalten.
    /// Trägt BBs Stand (`None`: kein Target), damit der Client ihn ohne Neulesen zeigt.
    | TargetChangedMeanwhile of Target option

/// Die Ablehnung außerhalb des laufenden Monats (ADR 0080 Punkt 1, `targetEditAllowed`).
[<Literal>]
let TargetEditMonthText = "Target ändern geht nur im laufenden Monat."

/// Die Ablehnung eines neuen Targets ohne Rhythmus: BB legt nur monatlich, wöchentlich oder jährlich an
/// (ADR 0084 Punkt 6, Arten 1:1).
[<Literal>]
let TargetRhythmMissingText = "Für ein neues Target fehlt der Rhythmus: monatlich, wöchentlich oder jährlich."

/// Die Ablehnung, wenn ohne Target in einem anderen Monat als dem laufenden angelegt werden soll, und der
/// Satz unter "Kein Target" dort (budgeting-g2ty9, X5).
[<Literal>]
let TargetCreateMonthText = "Target anlegen geht nur im laufenden Monat."

/// Die Ablehnung eines neuen Targets mit 0 € (budgeting-g2ty9, X8): ein Target ohne Fehlbetrag.
[<Literal>]
let TargetCreateZeroText = "Ein neues Target braucht einen Betrag über 0 €."

/// Der Hinweis, wenn das gezeigte Target nicht mehr BBs Stand ist (budgeting-eh8xe, ADR 0080 Punkt 3,
/// Nachtrag ADR 0084, `TargetChangedMeanwhile`): nichts festgehalten, der Tab zeigt BBs Stand, die Eingabe bleibt.
[<Literal>]
let TargetChangedMeanwhileText = "Das Target wurde inzwischen geändert. Neuen Stand prüfen und erneut speichern."

/// Die Ablehnung eines Wochentags außerhalb von 0 bis 6 oder an einem Target, das nicht wöchentlich ist
/// (budgeting-eh8xe).
[<Literal>]
let TargetWeekdayText = "Ein Wochentag geht nur bei einem wöchentlichen Target, Sonntag bis Samstag."

/// Die Ablehnung eines Targets, das nach dem Speichern wöchentlich ist und keinen Wochentag trägt
/// (budgeting-eh8xe): derselbe Satz im Formular und an der API-Grenze.
[<Literal>]
let TargetWeekdayMissingText = "Ein wöchentliches Target braucht einen Wochentag."

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

/// Der Rhythmus zu einer Wahl im Formular: jeweils mit Abstand 1.
let rhythmOfFrequency (frequency: TargetFrequency) : TargetRhythm =
    match frequency with
    | Weekly -> Weeks 1
    | Monthly -> Months 1
    | Yearly -> Years 1

/// Die Wahl zu einem Rhythmus, `None` bei einem, den BB nicht anlegt (alle n mit n über 1, ohne Rhythmus).
let frequencyOfRhythm (rhythm: TargetRhythm) : TargetFrequency option =
    [ Weekly; Monthly; Yearly ] |> List.tryFind (fun f -> rhythmOfFrequency f = rhythm)

/// "monatlich", "wöchentlich", "jährlich".
let frequencyText (frequency: TargetFrequency) : string = rhythmWordText (rhythmOfFrequency frequency)

/// Ist der Wunsch ein echter Wechsel der Art? Ja, wenn `SetRhythm f` vom gezeigten Rhythmus abweicht oder die
/// heutige Art kein NEED ist. Gleicher Rhythmus bei NEED ist kein Wechsel. Liefert den neuen Rhythmus.
let targetKindChange (shown: Target) (draft: TargetDraft) : TargetFrequency option =
    match draft.NewRhythm with
    | KeepRhythm -> None
    | SetRhythm f when shown.Kind = Need && shown.Rhythm = rhythmOfFrequency f -> None
    | SetRhythm f -> Some f

/// Die Wochentage für die Auswahl, Montag zuerst: Kurzname und Wert wie YNABs `goal_day` (0 Sonntag).
let weekdays : (string * int) list =
    [ "Mo", 1; "Di", 2; "Mi", 3; "Do", 4; "Fr", 5; "Sa", 6; "So", 0 ]

/// Der Wochentag als Wort: "Montag". Ein Wert außerhalb von 0 bis 6 bleibt als Zahl lesbar.
let weekdayText (day: int) : string =
    match day with
    | 0 -> "Sonntag"
    | 1 -> "Montag"
    | 2 -> "Dienstag"
    | 3 -> "Mittwoch"
    | 4 -> "Donnerstag"
    | 5 -> "Freitag"
    | 6 -> "Samstag"
    | other -> $"Tag {other}"

/// Was ein Wechsel der Art am gezeigten Target verliert (budgeting-xp46a).
type TargetLoss =
    /// Ein Tag ist gesetzt und gilt nur für den bisherigen Rhythmus: er geht verloren, außer der Entwurf setzt
    /// einen Wochentag.
    | LosesDay
    /// Ein Datum ist gesetzt und der Entwurf hat kein Datum.
    | LosesDate
    /// Die Art ist kein NEED (TB, TBD, MF und andere): BB legt nur NEED an.
    | LosesKind
    /// Der Rhythmus ist einer, den BB nicht herstellen kann (alle n Monate, Wochen oder Jahre mit n über 1,
    /// ein NEED ohne Kadenz). Der Wechsel ist endgültig.
    | LosesRhythm

/// Was am gezeigten Stand verloren geht, wenn der Entwurf gespeichert wird. Leer heißt keine Warnung: auch
/// jede Änderung ohne Wechsel der Art ist leer. Rein.
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
        [ if shown.Day.IsSome && draft.NewDay.IsNone then LosesDay
          if shown.Date.IsSome && draft.NewDate.IsNone then LosesDate
          if shown.Kind <> Need then LosesKind
          if rhythmLost then LosesRhythm ]

/// Der Wochentag, den ein Entwurf ohne Wechsel tragen muss, damit er bleibt: bei wöchentlich der Tag des
/// Targets, sonst keiner (`targetAfter`).
let keptWeekday (target: Target) : int option =
    match target.Rhythm with
    | Weeks _ -> target.Day
    | _ -> None

/// Baut der Wunsch eine neue Regel (Anlegen, Wechsel der Art)? Dann ihr Rhythmus, sonst `None`. Die eine Stelle,
/// die das entscheidet: `targetAfter` baut danach, der Server stempelt danach den Anlegemonat (`planTargetChange`).
let newRuleFrequency (shown: Target option) (draft: TargetDraft) : TargetFrequency option =
    match shown, draft.NewRhythm with
    | None, SetRhythm f -> Some f
    | None, KeepRhythm -> None
    | Some target, _ -> targetKindChange target draft

/// Das Target nach dem Wunsch, vom gezeigten Stand aus (budgeting-eh8xe). Anlegen und ein Wechsel der Art
/// ergeben NEED mit dem neuen Rhythmus; sonst bleiben Art, Rhythmus und "Auffüllen bis". Der Wochentag kommt
/// bei wöchentlich aus dem Entwurf; ohne Wochentag im Entwurf bleibt der Tag bei gleichem Rhythmus (ein älterer
/// Client ohne das Feld löscht ihn so nicht). Eine neu gebaute Regel hat keinen Anlegemonat, den setzt der Server
/// (`planTargetChange`); eine geänderte behält ihn (budgeting-pf8n7). Ein Wochentag an einem
/// Ergebnis, das nicht wöchentlich ist, und ein Wochentag außerhalb von 0 bis 6 sind ein Fehler. Rein, gleich
/// in Client und Server.
let targetAfter (shown: Target option) (draft: TargetDraft) : Result<Target, string> =
    let weekly (rhythm: TargetRhythm) =
        match rhythm with
        | Weeks _ -> true
        | _ -> false
    let built =
        match shown, newRuleFrequency shown draft with
        | None, None -> Error TargetRhythmMissingText
        | _, Some f ->
            Ok { Goal = draft.NewGoal; Rhythm = rhythmOfFrequency f; Kind = Need; Date = draft.NewDate; NeedsWholeAmount = None; Day = None; CreatedIn = None }
        | Some target, None -> Ok { target with Goal = draft.NewGoal; Date = draft.NewDate }
    match built, draft.NewDay with
    | Error message, _ -> Error message
    | Ok target, Some day when day < 0 || day > 6 || not (weekly target.Rhythm) -> Error TargetWeekdayText
    | Ok target, Some day -> Ok { target with Day = Some day }
    | Ok target, None -> Ok target

/// Ziffern mit Tausenderpunkt: "1234567" wird "1.234.567". Für `euroText` und `wholeEuroText`.
let rec private groupThousands (digits: string) : string =
    if digits.Length <= 3 then digits
    else groupThousands digits.[.. digits.Length - 4] + "." + digits.[digits.Length - 3 ..]

/// Ein Betrag als "1.234,50 €", ohne Kultur und ohne Format-String, gleich unter .NET und Fable: ein halber Cent
/// wird über `roundHalfUp` kaufmännisch aufgerundet (1,225 € wird "1,23 €").
let euroText (money: Money) : string =
    let cents = int64 (roundHalfUp (abs money.Amount * 100m))
    let sign = if money.Amount < 0m && cents > 0L then "-" else ""
    let rest = (string (cents % 100L)).PadLeft(2, '0')
    $"{sign}{groupThousands (string (cents / 100L))},{rest} €"

/// "2027-06-01" als "01.06.2027". Ein anderer Text bleibt, wie er ist.
let dateText (iso: string) : string =
    match iso.Split '-' with
    | [| year; month; day |] when year.Length = 4 && month.Length = 2 && day.Length = 2 -> $"{day}.{month}.{year}"
    | _ -> iso

let private monthNames =
    [| "Januar"; "Februar"; "März"; "April"; "Mai"; "Juni"
       "Juli"; "August"; "September"; "Oktober"; "November"; "Dezember" |]

/// "2026-10-01" als "Oktober 2026". Ein unlesbarer Wert bleibt, wie er ist. Liegt in Shared, weil auch
/// `fitsNoteText` den Monat ausschreibt (budgeting-k7r2w).
let monthLabel (month: string) : string =
    match month.Split '-' with
    | [| year; mm; _ |] ->
        match System.Int32.TryParse mm with
        | true, m when m >= 1 && m <= 12 -> $"{monthNames.[m - 1]} {year}"
        | _ -> month
    | _ -> month

let private shortMonthNames =
    [| "Jan"; "Feb"; "Mär"; "Apr"; "Mai"; "Jun"; "Jul"; "Aug"; "Sep"; "Okt"; "Nov"; "Dez" |]

/// "2026-10-01" als "Okt", für die Balken der Ausgaben. Ein unlesbarer Wert bleibt, wie er ist.
let shortMonthLabel (month: string) : string =
    match month.Split '-' with
    | [| _; mm; _ |] ->
        match System.Int32.TryParse mm with
        | true, m when m >= 1 && m <= 12 -> shortMonthNames.[m - 1]
        | _ -> month
    | _ -> month

// ============================================
// Historie je Kategorie (budgeting-dyn8g)
// ============================================

/// Welche Entscheidung in BB eine Budget-Bewegung festhielt (ADR 0065, ADR 0069). Nur Entscheidungen werden
/// nach YNAB geschrieben.
type MovementOrigin =
    | TargetsApplied
    /// Zwischen zwei Kategorien.
    | MoneyMoved
    /// Eine Seite ist Zu verteilen.
    | MoneyAssigned

/// Woher eine Zeile der Historie stammt: eine Entscheidung in BB oder ein Ereignis des Spiegels
/// (budgeting-dgrp7). Die Historie zeigt und filtert danach.
type HistoryOrigin =
    | Decision of MovementOrigin
    /// Eine Zuweisung, die BB in YNAB sah, nicht selbst schrieb (Beobachtung).
    | ObservedInYnab
    /// Der Startstand des Monats aus YNAB (Übernahme).
    | TakenOverFromYnab

/// Kommt die Zeile aus YNAB (Beobachtung, Übernahme)? Sie geht nicht nach YNAB und hat keinen Stand
/// beim Schreiben.
let isFromYnab (origin: HistoryOrigin) : bool =
    match origin with
    | ObservedInYnab
    | TakenOverFromYnab -> true
    | Decision _ -> false

/// Eine festgehaltene Budget-Bewegung, wie die Historie sie liest.
type HistoryMovement = {
    MovementId: Guid
    /// Die Gruppen-Id des Durchlaufs (`application_id`, ADR 0069 Punkt 1).
    ApplicationId: Guid
    /// Zeitpunkt des Festhaltens, UTC.
    OccurredAt: DateTime
    Origin: HistoryOrigin
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
    /// Die Rückschreibung wurde ungesendet zurückgezogen, weil eine spätere Entscheidung in BB dieselbe
    /// Kategorie setzte: die Gruppen-Id dieser Entscheidung (budgeting-dgrp7). Abgeleitet aus dem Stream, kein
    /// eigenes Ergebnis. `None` sonst, auch bei einem älteren Server ohne das Feld.
    ReplacedBy: Guid option
}

/// Die Netto-Aktivität einer Kategorie in einem Monat aus dem Kassenbuch (Erstattungen mindern,
/// gelöschte Buchungen zählen nicht, Splits mit ihrem Anteil). Ausgaben sind negativ wie bei YNAB.
type MonthSpending = {
    /// "YYYY-MM-01".
    Month: string
    Activity: Money
}

/// Die Ausgaben eines Monats: die Netto-Aktivität mit umgekehrtem Vorzeichen. Überwiegen Erstattungen, ist der
/// Wert negativ.
let netSpending (s: MonthSpending) : decimal = -s.Activity.Amount

/// Ein Pause-Ereignis (budgeting-t6p8z). Es geht nicht nach YNAB.
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

/// YNABs Kürzel der Art aus `goal_type`: "NEED", "MF", "TB".
let goalTypeText (kind: TargetKind) : string =
    match kind with
    | Need -> "NEED"
    | TargetBalance -> "TB"
    | TargetBalanceByDate -> "TBD"
    | MonthlyFunding -> "MF"
    | Debt -> "DEBT"
    | OtherKind goalType -> goalType

/// Die Art zu YNABs Kürzel aus `goal_type`, die Umkehrung von `goalTypeText`. Ein unbekanntes Kürzel bleibt
/// als `OtherKind` lesbar.
let targetKindOfGoalType (goalType: string) : TargetKind =
    match goalType with
    | "NEED" -> Need
    | "TB" -> TargetBalance
    | "TBD" -> TargetBalanceByDate
    | "MF" -> MonthlyFunding
    | "DEBT" -> Debt
    | other -> OtherKind other

/// Die Ablehnung außerhalb des laufenden Monats: beim Anlegen (kein Target gezeigt, ein Wunsch) "Target
/// anlegen geht nur im laufenden Monat.", sonst "Target ändern geht nur im laufenden Monat." (budgeting-c2t9w).
let targetMonthRefusalText (request: TargetChangeRequest) : string =
    match request.Shown, request.Wanted with
    | None, Some _ -> TargetCreateMonthText
    | _ -> TargetEditMonthText

/// Eine festgehaltene Target-Änderung: `TargetSet` oder `TargetRemoved` in BB (budgeting-eh8xe), oder aus dem
/// Altbestand `YnabGoalChangedFromBb` mit dem jüngsten Ergebnis je Schritt (ADR 0080 Punkt 2 und 4, budgeting-g2ty9
/// X7). Art, alter und neuer Wert einer Änderung in BB kommen aus der vorigen Faltung. Keine Budget-Bewegung:
/// das Zugewiesen bleibt.
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
    /// Der Tag vorher und nachher, bei wöchentlich der Wochentag (budgeting-eh8xe). Der Altbestand kennt ihn
    /// nicht und trägt `None`.
    DayBefore: int option
    DayAfter: int option
    /// Das jüngste Ergebnis von Schritt 1 (alte Zeilen ohne Schritt zählen als Schritt 1). `None`: noch keins
    /// angehängt.
    Latest: LineOutcome option
    /// Das jüngste Ergebnis von Schritt 2, dem Datum nach Anlegen oder Wechsel der Art. `None`: kein
    /// zweiter Schritt oder noch kein Ergebnis.
    DateStep: LineOutcome option
    /// Ging die Änderung nach YNAB? Nur der Altbestand aus dem Zwei-Schritt-Weg. Eine Änderung nur in BB hat
    /// keinen Stand beim Schreiben, wie eine Pause.
    SentToYnab: bool
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
    /// Ungesendet zurückgezogen, weil eine spätere Entscheidung in BB dieselbe Kategorie setzte
    /// (budgeting-dgrp7). Unterscheidet sich von "in YNAB geändert".
    | HistoryReplacedInBb
    /// Ungesendet zurückgezogen, ohne ersetzende Entscheidung in BB: die Gegenseite der Verschiebung ist
    /// verloren (budgeting-my8p8). YNAB hat die Zeile nie berührt.
    | HistoryWithdrawnInBb
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
    | Some WithdrawnInBb -> HistoryWithdrawnInBb
    | None -> HistoryOpen

/// Der Stand einer Bewegung: ersetzt in BB vor dem jüngsten Ergebnis, denn auch ein ersetztes Ziel trägt
/// zuletzt ein Zurückziehen.
let private movementStatus (m: HistoryMovement) : HistoryStatus =
    match m.ReplacedBy with
    | Some _ -> HistoryReplacedInBb
    | None -> historyStatus m.Latest

/// Bewegung zu Zeile. Die Richtung folgt aus dem Vorzeichen von neu minus alt: Bei einer Verschiebung
/// geht Geld nach der Gegenseite oder kommt von ihr, bei Zuweisen kommt es aus Zu verteilen oder geht
/// dorthin zurück.
let historyLine (m: HistoryMovement) : HistoryLine =
    let change = changeOf m
    let incoming = change.Amount >= 0m
    let text =
        match m.Origin, m.Counterpart with
        | Decision TargetsApplied, _ -> "Targets anwenden"
        | Decision MoneyAssigned, _ -> if incoming then "zugewiesen" else "zurück nach Zu verteilen"
        | Decision MoneyMoved, Some other -> if incoming then $"von {other}" else $"nach {other}"
        | Decision MoneyMoved, None -> "verschoben"
        | ObservedInYnab, _ -> "in YNAB zugewiesen"
        | TakenOverFromYnab, _ -> $"aus YNAB übernommen, {euroText m.NewAssigned}"
    { EntryId = m.MovementId
      OccurredAt = m.OccurredAt
      // Die Übernahme ändert nichts, sie nennt den Stand im Text.
      Change = (if m.Origin = TakenOverFromYnab then None else Some change)
      Text = text
      // Was aus YNAB kommt, wird nicht geschrieben und hat keinen Stand.
      Status = (if isFromYnab m.Origin then None else Some (movementStatus m)) }

/// Der Stand einer Target-Änderung aus den Ergebnissen ihrer Schritte (budgeting-g2ty9, X7). Ist Schritt 1
/// geschrieben, entscheidet Schritt 2: fehlgeschlagen heißt teilweise geschrieben, ohne Ergebnis bei
/// einem erwarteten Datum noch offen. Sonst gilt Schritt 1 wie bei einer Bewegung.
let targetChangeStatus (c: HistoryTargetChange) : HistoryStatus =
    // Zwei Schritte hatte im Altbestand nur ein Anlegen oder Wechsel der Art mit Datum.
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
    let weekdayAfter (frequency: TargetFrequency) =
        match frequency, c.DayAfter with
        | Weekly, Some day -> [ $"Wochentag {weekdayText day}" ]
        | _ -> []
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
          yield! weekdayAfter frequency
          match c.DateAfter with
          | Some date -> $"Datum {dateText date}{dateNotSet}"
          | None -> () ]
        |> String.concat ", "
    | TargetKindChanged frequency ->
        $"Rhythmus {frequencyText frequency}" :: weekdayAfter frequency @ changes |> String.concat ", "
    | TargetEdited ->
        // Bei gleichem Rhythmus ändert sich der Tag nur bei wöchentlich (`targetAfter`).
        let changes =
            changes
            @ [ if c.DayBefore <> c.DayAfter then
                    match c.DayAfter with
                    | Some day -> $"Wochentag {weekdayText day}"
                    | None -> "Wochentag entfernt" ]
        match changes with
        | [] -> "Target unverändert"
        | parts -> String.concat ", " parts

/// Eintrag zu Zeile. Eine Pause hat weder Betrag noch Stand: sie geht nie nach YNAB (ADR 0073 Punkt 5).
/// Eine Target-Änderung aus dem Altbestand behält ihren echten Stand, eine Änderung nur in BB hat keinen.
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
          Status = (if c.SentToYnab then Some (targetChangeStatus c) else None) }

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
    /// Ungesendet zurückgezogen, eine spätere Entscheidung in BB setzte dieselbe Kategorie (budgeting-dgrp7).
    | RunReplacedInBb
    /// Ungesendet zurückgezogen, ohne ersetzende Entscheidung in BB (budgeting-my8p8).
    | RunWithdrawnInBb
    /// Kam aus YNAB (Beobachtung, Übernahme): kein Schreiben, kein Stand (budgeting-dgrp7).
    | RunFromYnab
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
    Origin: HistoryOrigin
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
    /// Fehlschlag noch offen, in YNAB geändert, in BB ersetzt, in BB zurückgezogen oder geschrieben, in dieser
    /// Rangfolge.
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
let private runStatus (origin: HistoryOrigin) (statuses: HistoryStatus list) : RunStatus =
    let failed = statuses |> List.choose (function HistoryFailed reason -> Some reason | _ -> None)
    match failed, origin with
    | _ :: _, Decision TargetsApplied when List.length failed < List.length statuses ->
        RunPartlyFailed (List.length failed, List.length statuses)
    | reason :: _, _ -> RunFailed reason
    | [], _ ->
        if isFromYnab origin then RunFromYnab
        elif List.contains HistoryOpen statuses then RunOpen
        elif List.contains HistoryChangedInYnab statuses then RunChangedInYnab
        // Beide Stände in BB stehen hinter "in YNAB geändert". Kommen beide vor, nennt "in BB ersetzt" den Anlass.
        elif List.contains HistoryReplacedInBb statuses then RunReplacedInBb
        elif List.contains HistoryWithdrawnInBb statuses then RunWithdrawnInBb
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
    let statuses = inOrder |> List.map movementStatus
    let text, amount, lines =
        match first.Origin with
        | Decision TargetsApplied ->
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
                  Status = movementStatus m })
        | Decision MoneyAssigned -> $"Zuweisen, {first.CategoryName}", changeOf first, []
        | ObservedInYnab -> $"In YNAB zugewiesen, {first.CategoryName}", changeOf first, []
        | TakenOverFromYnab ->
            let count = List.length inOrder
            let categories = if count = 1 then "1 Kategorie" else $"{count} Kategorien"
            $"Aus YNAB übernommen, {categories}",
            { first.NewAssigned with Amount = inOrder |> List.sumBy (fun m -> m.NewAssigned.Amount) },
            []
        | Decision MoneyMoved ->
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

/// Eine Pause oder Target-Änderung als eigene Zeile der allgemeinen Historie (budgeting-r5h2n): ohne Betrag,
/// nicht aufklappbar.
type HistoryTargetRow = {
    /// Die Id des Pause-Ereignisses oder der Target-Änderung.
    EntryId: Guid
    /// Zeitpunkt des Festhaltens, UTC.
    OccurredAt: DateTime
    /// "YYYY-MM-01": bei einer Pause ihr Monat, bei einer Änderung `effectiveFrom` oder der Monat des Altbestands.
    Month: string
    /// "Art, Kategorie" wie bei den Durchläufen: "Target pausiert, Urlaub", "Target 50,00 € → 60,00 €, Urlaub".
    Text: string
    /// Der Stand wie im Tab Historie: nur beim Altbestand, der nach YNAB ging. `None` bei einer Pause und bei
    /// einer Änderung nur in BB.
    Status: HistoryStatus option
}

/// Eine Zeile der allgemeinen Historie: ein Durchlauf aus Bewegungen oder eine Pause oder Target-Änderung.
type GeneralHistoryRow =
    | RunRow of HistoryRun
    | TargetRow of HistoryTargetRow

/// Die Einträge der allgemeinen Historie zu Zeilen (budgeting-r5h2n): die Bewegungen zu Durchläufen
/// (`historyRuns`), jede Pause und Target-Änderung eine eigene Zeile, neueste zuerst. Ein Durchlauf zählt mit
/// seiner jüngsten Bewegung. Bei gleichem Zeitpunkt steht der Durchlauf vor der Pause und die Pause vor der
/// Target-Änderung, wie in `mergeHistory`.
let generalHistoryRows (entries: HistoryEntry list) : GeneralHistoryRow list =
    let targetRow (entry: HistoryEntry) (categoryName: string) (month: string) =
        let line = historyEntryLine entry
        let name = if String.IsNullOrEmpty categoryName then UnknownCategoryText else categoryName
        TargetRow
            { EntryId = line.EntryId
              OccurredAt = line.OccurredAt
              Month = month
              Text = $"{line.Text}, {name}"
              Status = line.Status }
    let runs =
        entries
        |> List.choose (function Movement m -> Some m | _ -> None)
        |> historyRuns
        |> List.map (fun r -> r.OccurredAt, 0, RunRow r)
    let others =
        entries
        |> List.choose (fun entry ->
            match entry with
            | Movement _ -> None
            | TargetPaused p
            | PauseLifted p -> Some (p.OccurredAt, 1, targetRow entry p.CategoryName p.Month)
            | TargetChanged c -> Some (c.OccurredAt, 2, targetRow entry c.CategoryName c.Month))
    runs @ others
    |> List.sortWith (fun (timeA, rankA, _) (timeB, rankB, _) ->
        match compare timeB timeA with
        | 0 -> compare rankA rankB
        | order -> order)
    |> List.map (fun (_, _, row) -> row)

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
    let highest = spending |> List.map netSpending |> List.fold max 0m
    spending
    |> List.map (fun s ->
        let amount = netSpending s
        let percent =
            if highest <= 0m || amount <= 0m then 0
            else int (roundHalfUp (amount / highest * 100m))
        { Month = s.Month
          Spending = { s.Activity with Amount = amount }
          Percent = percent
          Kind = monthKind currentMonth s.Month })

// ============================================
// Targets prüfen (budgeting-p2zr6, ADR 0082)
// ============================================

/// Die letzte in BB geschriebene Target-Änderung einer Kategorie (ADR 0082 Punkt 7) mit ihrem Berliner
/// Kalendertag "YYYY-MM-DD": gesetzt oder geändert, oder entfernt.
type TargetChangeMark =
    | TargetSetOn of day: string
    | TargetRemovedOn of day: string

/// Der Berliner Kalendertag einer letzten Änderung.
let changeMarkDay (mark: TargetChangeMark) : string =
    match mark with
    | TargetSetOn day
    | TargetRemovedOn day -> day

/// Eine sichtbare Kategorie des laufenden Monats mit ihren Rohdaten für die Target-Prüfung.
type TargetCheckSource = {
    CategoryId: Guid
    Name: string
    GroupName: string
    Target: Target option
    /// Die 12 vollen Monate vor dem laufenden, aufsteigend, je Monat genau ein Eintrag, 0 ohne Buchung.
    Spending: MonthSpending list
    /// Die jüngste geschriebene Target-Änderung über BB. `None`: keine.
    LastChange: TargetChangeMark option
    /// Der Buchungstag "YYYY-MM-DD" der ersten Buchung der Kategorie im Kassenbuch überhaupt, wie YNAB oder
    /// Comdirect ihn liefern (Tagesteil von `date`), auch vor den 12 Monaten (budgeting-k7r2w). Gezählt wird ab ihrem Monat. `None`: keine Buchung.
    FirstBooking: string option
}

/// Die Antwort von `getTargetCheck`: Rohdaten, die Einordnung rechnet der Client (ADR 0082 Punkt 12).
type TargetCheckData = {
    /// Der laufende Monat "YYYY-MM-01".
    Month: string
    /// Der Berliner Tag "YYYY-MM-DD". Mit ihm rechnet der Client nach einem Schreiben (`withWrittenTarget`).
    Today: string
    Categories: TargetCheckSource list
}

/// Die Gruppe einer Kategorie auf der Seite Targets prüfen, in der Reihenfolge von ADR 0082 Punkt 3.
type CheckGroup =
    | SpendsMore
    | SpendsLess
    | NeverSpends
    | WithoutTarget
    | Fits

/// Die Gruppen in der Reihenfolge der Seite.
let checkGroupOrder : CheckGroup list = [ SpendsMore; SpendsLess; NeverSpends; WithoutTarget; Fits ]

/// Der Satz über einer Gruppe.
let checkGroupText (group: CheckGroup) : string =
    match group with
    | SpendsMore -> "Gibst du mehr aus?"
    | SpendsLess -> "Gibst du weniger aus?"
    | NeverSpends -> "Gibst du nie aus?"
    | WithoutTarget -> "Kein Target"
    | Fits -> "Passt"

/// Warum eine Zeile bei "Passt" ohne Einordnung steht (ADR 0082 Punkt 5 und 7). Der Tag als "YYYY-MM-DD".
type FitsNote =
    /// Weniger gezählte Monate als nötig seit einer Änderung.
    | RecentlySet of day: string
    /// Weniger gezählte Monate als nötig seit dem Entfernen.
    | RecentlyRemoved of day: string
    /// Weniger gezählte Monate als nötig seit der ersten Buchung (budgeting-k7r2w). Der Monat "YYYY-MM-01".
    | NewSince of month: string
    /// TB und TBD: ein Guthaben, das wachsen soll.
    | SavingsGoalNotChecked
    /// DEBT, eine unbekannte Art, ein Target ohne Rhythmus oder ein Rhythmus über 12 Monate.
    | NotChecked

/// Der Zusatz in der Zeile: "neu gesetzt am 05.11.2026", "entfernt am 05.11.2026", "neu seit Juli 2026",
/// "Sparziel, wird nicht geprüft", "wird nicht geprüft".
let fitsNoteText (note: FitsNote) : string =
    match note with
    | RecentlySet day -> $"neu gesetzt am {dateText day}"
    | RecentlyRemoved day -> $"entfernt am {dateText day}"
    | NewSince month -> $"neu seit {monthLabel month}"
    | SavingsGoalNotChecked -> "Sparziel, wird nicht geprüft"
    | NotChecked -> "wird nicht geprüft"

/// Der Vorschlag zu einer Zeile (ADR 0082 Punkt 9 und 10).
type TargetSuggestion = {
    /// Ø auf volle 5 € aufgerundet.
    Monthly: Money
    /// Der Betrag im Rhythmus des Targets, den Übernehmen sendet.
    InRhythm: Money
    /// Der fertige Entwurf für `changeCategoryTarget`.
    Draft: TargetDraft
}

/// Eine eingeordnete Kategorie.
type TargetCheckRow = {
    CategoryId: Guid
    Name: string
    GroupName: string
    Target: Target option
    Group: CheckGroup
    /// Nur bei "Passt" ohne Einordnung.
    Note: FitsNote option
    /// Das Target auf einen Monat umgerechnet (`monthlyValue`).
    Monthly: Money option
    /// Ø der Netto-Ausgaben über die gezählten Monate. `None` ohne gezählten Monat.
    Average: Money option
    /// Die gezählten Monate, aufsteigend.
    CountedMonths: MonthSpending list
    /// Die vollen Monate vor dem laufenden, höchstens 12, aufsteigend, auch die vor der letzten Änderung und vor
    /// der ersten Buchung. Die Mini-Balken daraus mit `checkBars` (budgeting-c4ve4).
    Months: MonthSpending list
    /// Monate mit Netto-Ausgaben über dem Monatswert, 0 ohne Monatswert.
    MonthsOver: int
    /// Monate mit Netto-Ausgaben über 0.
    MonthsWithSpending: int
    /// Nur bei "mehr", "weniger" und "Kein Target".
    Suggestion: TargetSuggestion option
}

let private euro (amount: decimal) : Money = { Amount = amount; Currency = TargetCurrency }

/// Hat das Target einen belastbaren Monatswert? TB, TBD, DEBT, unbekannte Arten und NEED ohne Rhythmus
/// nicht (ADR 0082 Punkt 5). MF gilt immer als monatlich.
let private checkedRhythm (target: Target) : TargetRhythm option =
    match target.Kind, target.Rhythm with
    | MonthlyFunding, _ -> Some (Months 1)
    | Need, NoRhythm -> None
    | Need, rhythm -> Some rhythm
    | (TargetBalance | TargetBalanceByDate | Debt | OtherKind _), _ -> None

/// Das Target auf einen Monat umgerechnet (ADR 0082 Punkt 4): monatlich und MF bleiben, alle n Monate ÷ n,
/// alle n Jahre ÷ 12n, alle n Wochen × 52 ÷ 12n. Ohne Rundung. `None` bei einer Art ohne Monatswert.
let monthlyValue (target: Target) : Money option =
    checkedRhythm target
    |> Option.bind (fun rhythm ->
        match rhythm with
        | Months n when n > 0 -> Some (target.Goal.Amount / decimal n)
        | Years n when n > 0 -> Some (target.Goal.Amount / decimal (12 * n))
        | Weeks n when n > 0 -> Some (target.Goal.Amount * 52m / decimal (12 * n))
        | _ -> None)
    |> Option.map (fun amount -> { target.Goal with Amount = amount })

/// Die Periode P eines Rhythmus in Monaten (ADR 0082 Punkt 7).
let private periodMonths (rhythm: TargetRhythm) : int =
    match rhythm with
    | Months n -> max 1 n
    | Years n -> 12 * max 1 n
    | Weeks _
    | NoRhythm -> 1

/// "YYYY-MM-01" zu einem Tag "YYYY-MM-DD".
let private monthOfDay (day: string) : string = day.Substring(0, 7) + "-01"

/// Der Vorschlag aus Ø (ADR 0082 Punkt 9): Ø auf volle 5 € aufgerundet als Monatswert, zurückgerechnet in
/// den Rhythmus des Targets. Alle n Wochen kaufmännisch auf volle Euro, mindestens 1 €. Ohne Target ein
/// monatliches NEED. Ø ≤ 0 ergibt keinen Vorschlag.
let suggestTarget (target: Target option) (average: Money) : TargetSuggestion option =
    if average.Amount <= 0m then
        None
    else
        let monthly = Math.Ceiling(average.Amount / 5m) * 5m
        match target with
        | None ->
            Some
                { Monthly = euro monthly
                  InRhythm = euro monthly
                  Draft = { NewGoal = euro monthly; NewDate = None; NewRhythm = SetRhythm Monthly; NewDay = None } }
        | Some t ->
            let inRhythm =
                match checkedRhythm t |> Option.defaultValue (Months 1) with
                | Months n -> monthly * decimal (max 1 n)
                | Years n -> monthly * decimal (12 * max 1 n)
                | Weeks n ->
                    // Über `roundHalfUp`: `Math.Round` rundet im Fable-Kompilat einen Mittelwert zur geraden Zahl.
                    let weekly = roundHalfUp (monthly * decimal (12 * max 1 n) / 52m)
                    max 1m weekly
                | NoRhythm -> monthly
            Some
                { Monthly = euro monthly
                  InRhythm = euro inRhythm
                  Draft = { NewGoal = euro inRhythm; NewDate = t.Date; NewRhythm = KeepRhythm; NewDay = keptWeekday t } }

/// Über dem Target heißt (ADR 0082 Punkt 6): die Ausgaben des Monats liegen strikt über dem Monatswert, ohne
/// Spielraum. Dieselbe Regel zählt `MonthsOver` und färbt die Mini-Balken (`checkBars`).
let spendsOverTarget (monthly: decimal) (s: MonthSpending) : bool = netSpending s > monthly

/// Ordnet eine Kategorie ein (ADR 0082 Punkt 6 bis 9, Zählbeginn ab der ersten Buchung nach budgeting-k7r2w).
/// `today` ist der Berliner Tag "YYYY-MM-DD", der laufende Monat folgt daraus und zählt nie. Rein, gleich unter .NET und Fable.
let targetCheckRow (today: string) (source: TargetCheckSource) : TargetCheckRow =
    let current = monthOfDay today
    let changeMonth = source.LastChange |> Option.map (changeMarkDay >> monthOfDay)
    let full =
        source.Spending
        |> List.filter (fun s -> s.Month < current)
        |> List.rev
        |> List.truncate 12
        |> List.rev
    // Gezählt wird ab dem späteren von zwei Monaten: dem nach der letzten Änderung und dem der ersten Buchung
    // (budgeting-k7r2w). Eine Kategorie, die es vorher nicht gab, hat davor keine Ausgaben mit 0 €.
    let firstMonth = source.FirstBooking |> Option.map monthOfDay
    let counted =
        full
        |> List.filter (fun s ->
            changeMonth |> Option.forall (fun changed -> s.Month > changed)
            && firstMonth |> Option.forall (fun first -> s.Month >= first))
    let n = List.length counted
    let monthly = source.Target |> Option.bind monthlyValue
    let total = counted |> List.sumBy netSpending
    let average =
        if n = 0 then None
        else Some (euro (total / decimal n))
    let monthsWithSpending = counted |> List.filter (fun s -> netSpending s > 0m) |> List.length
    let monthsOver =
        match monthly with
        | Some m -> counted |> List.filter (spendsOverTarget m.Amount) |> List.length
        | None -> 0
    let period =
        source.Target |> Option.bind checkedRhythm |> Option.map periodMonths |> Option.defaultValue 1
    let notChecked =
        match source.Target with
        | Some { Kind = TargetBalance }
        | Some { Kind = TargetBalanceByDate } -> Some SavingsGoalNotChecked
        | Some _ when monthly.IsNone -> Some NotChecked
        | Some _ when period > 12 -> Some NotChecked
        | _ -> None
    let tooFew =
        let needed = if period > 3 then period else 3
        let byChange =
            match source.LastChange with
            | Some (TargetSetOn day) -> Some (day, RecentlySet day)
            | Some (TargetRemovedOn day) -> Some (day, RecentlyRemoved day)
            | None -> None
        // Nur eine erste Buchung, die Monate des Fensters abschneidet. Liegt sie davor, ändert sie nichts.
        let byFirstBooking =
            match source.FirstBooking, firstMonth with
            | Some day, Some first when full |> List.exists (fun s -> s.Month < first) -> Some (day, NewSince first)
            | _ -> None
        if n >= needed then None
        else
            // Erklären beide die zu wenigen Monate, nennt der Zusatz das spätere Ereignis.
            match byChange, byFirstBooking with
            | Some (changed, changeNote), Some (first, firstNote) -> Some (if first > changed then firstNote else changeNote)
            | Some (_, note), None
            | None, Some (_, note) -> Some note
            | None, None -> None
    let avg = average |> Option.map (fun a -> a.Amount) |> Option.defaultValue 0m
    let group, note =
        match notChecked, tooFew with
        | Some note, _
        | None, Some note -> Fits, Some note
        | None, None ->
            match monthly with
            | Some m ->
                let never = if period = 1 then monthsWithSpending <= 1 else monthsWithSpending = 0
                // Bei P > 1 und einzelnen Zahlungen (höchstens ⌈n ÷ P⌉ + 1 Monate mit Ausgaben, nie alle gezählten)
                // muss auch der Betrag je Zahlung über dem Target je Rhythmus liegen (budgeting-j8r4t). Sonst ergibt
                // eine verschobene Abbuchung, also zwei Zahlungen im Fenster, schon den doppelten Ø.
                let singlePayments =
                    period > 1
                    && monthsWithSpending > 0
                    && monthsWithSpending < n
                    && monthsWithSpending <= (n + period - 1) / period + 1
                let perPaymentOver =
                    // Das Target je Rhythmus ist der Betrag des Targets selbst, ohne Rückrechnung aus dem Monatswert.
                    let perRhythm = source.Target |> Option.map (fun t -> t.Goal.Amount) |> Option.defaultValue 0m
                    let perPayment = total / decimal (max monthsWithSpending 1)
                    perPayment > 1.1m * perRhythm && perPayment - perRhythm >= 10m
                let more =
                    avg > 1.1m * m.Amount
                    && avg - m.Amount >= 10m
                    && (period > 1 || 2 * monthsOver >= n)
                    && (not singlePayments || perPaymentOver)
                let less = avg < 0.7m * m.Amount && m.Amount - avg >= 10m
                if never then NeverSpends, None
                elif more then SpendsMore, None
                elif less then SpendsLess, None
                else Fits, None
            | None ->
                if monthsWithSpending >= 3 && avg >= 10m then WithoutTarget, None else Fits, None
    let suggestion =
        match group, average with
        | (SpendsMore | SpendsLess | WithoutTarget), Some a -> suggestTarget source.Target a
        | _ -> None
    { CategoryId = source.CategoryId
      Name = source.Name
      GroupName = source.GroupName
      Target = source.Target
      Group = group
      Note = note
      Monthly = monthly
      Average = average
      CountedMonths = counted
      Months = full
      MonthsOver = monthsOver
      MonthsWithSpending = monthsWithSpending
      Suggestion = suggestion }

/// Die Zeilen nach Gruppe in der Reihenfolge von ADR 0082 Punkt 3, leere Gruppen fehlen. In der Gruppe:
/// "mehr" nach Ø − Monatswert absteigend, "weniger" nach Monatswert − Ø absteigend, "Kein Target" nach Ø
/// absteigend, "nie" und "Passt" nach Name. Bei gleichem Abstand entscheidet der Name.
let targetCheckGroups (today: string) (data: TargetCheckData) : (CheckGroup * TargetCheckRow list) list =
    let rows = data.Categories |> List.map (targetCheckRow today)
    let amountOf (m: Money option) = m |> Option.map (fun x -> x.Amount) |> Option.defaultValue 0m
    let gap (row: TargetCheckRow) =
        match row.Group with
        | SpendsMore -> amountOf row.Average - amountOf row.Monthly
        | SpendsLess -> amountOf row.Monthly - amountOf row.Average
        | WithoutTarget -> amountOf row.Average
        | NeverSpends
        | Fits -> 0m
    checkGroupOrder
    |> List.choose (fun group ->
        match rows |> List.filter (fun r -> r.Group = group) with
        | [] -> None
        | members -> Some (group, members |> List.sortBy (fun r -> -(gap r), r.Name)))

/// Nach einem Schreiben (ADR 0082 Punkt 10): das Target aus der Antwort und `Today` als letzte Änderung,
/// `None` heißt entfernt. Andere Kategorien bleiben, wie sie sind.
let withWrittenTarget (categoryId: Guid) (target: Target option) (data: TargetCheckData) : TargetCheckData =
    let mark =
        match target with
        | Some _ -> TargetSetOn data.Today
        | None -> TargetRemovedOn data.Today
    { data with
        Categories =
            data.Categories
            |> List.map (fun c ->
                if c.CategoryId = categoryId then { c with Target = target; LastChange = Some mark } else c) }

/// Ein Betrag in ganzen Euro, kaufmännisch über `roundHalfUp`: "1.260 €", "-12 €" (budgeting-c4ve4). Gleich
/// unter .NET und Fable.
let wholeEuroText (amount: decimal) : string =
    let whole = int64 (roundHalfUp (abs amount))
    let sign = if amount < 0m && whole > 0L then "-" else ""
    $"{sign}{groupThousands (string whole)} €"

/// Wie ein Mini-Balken auf der Seite Targets prüfen steht (budgeting-c4ve4).
type CheckBarTone =
    /// Netto-Ausgaben über dem Monatswert des Targets (ADR 0082 Punkt 6): orange.
    | OverTarget
    /// Gezählt und nicht über dem Target, auch jeder gezählte Monat ohne Target: gedämpft.
    | Counted
    /// Vor der letzten Änderung oder vor der ersten Buchung, zählt nicht: dunkler.
    | NotCounted

/// Ein Mini-Balken: der Monat, die Höhe in Prozent des größten Werts und sein Ton.
type CheckBar = { Month: string; Percent: int; Tone: CheckBarTone }

/// Die Mini-Balken einer Zeile und die Höhe der Target-Linie in Prozent, `None` ohne Monatswert.
type CheckBars = { Bars: CheckBar list; LinePercent: int option }

/// Die Mini-Balken einer Zeile (budgeting-c4ve4): je voller Monat ein Balken, die Höhe relativ zum größten Wert
/// aus Monatsausgaben und Monatswert des Targets, kaufmännisch auf ganze Prozent. Ein Monat ohne Ausgaben oder
/// mit Erstattungsüberschuss hat 0 (die View zeigt ihn 2 px hoch). Über dem Monatswert heißt wie in
/// `MonthsOver` strikt darüber, ohne Spielraum, damit Balken und "x von N Monaten über dem Target" übereinstimmen.
let checkBars (row: TargetCheckRow) : CheckBars =
    let monthly = row.Monthly |> Option.map (fun m -> m.Amount)
    let highest =
        row.Months |> List.map netSpending |> List.fold max (monthly |> Option.defaultValue 0m)
    let percent (value: decimal) =
        if highest <= 0m || value <= 0m then 0 else int (roundHalfUp (value / highest * 100m))
    let counted = row.CountedMonths |> List.map (fun s -> s.Month) |> Set.ofList
    { Bars =
        row.Months
        |> List.map (fun s ->
            let tone =
                if not (counted.Contains s.Month) then NotCounted
                else
                    match monthly with
                    | Some m when spendsOverTarget m s -> OverTarget
                    | _ -> Counted
            { Month = s.Month; Percent = percent (netSpending s); Tone = tone })
      LinePercent = monthly |> Option.map percent }
