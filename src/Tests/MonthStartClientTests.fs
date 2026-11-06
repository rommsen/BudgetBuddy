module Tests.MonthStartClientTests

// budgeting-m3x7f: die Leiste Monatserster und die Vorschau mit abwählbaren Zeilen, als reine Funktionen
// (`Components.Budget.MonthStart`, `Components.Budget.Targets`) und durch das echte `update` der Budget-Seite. Die Cmds
// werden unter .NET nur gebaut, nie ausgeführt. Namen und Beträge sind erfunden.

open System
open Expecto
open Shared.Domain
open Shared.Budgeting
open Types
open Components.Budget.Types
open Components.Budget.Targets
open Components.Budget.MonthStart

let private eur (amount: decimal) : Money = { Amount = amount; Currency = "EUR" }

let private past = "2026-10-01"
let private current = "2026-11-01"
let private next = "2026-12-01"

let private foodId = Guid.Parse "00000000-0000-0000-0000-0000000a3701"
let private drugstoreId = Guid.Parse "00000000-0000-0000-0000-0000000a3702"
let private clothesId = Guid.Parse "00000000-0000-0000-0000-0000000a3703"

let private category (id: Guid) (name: string) (available: decimal) (underfunded: decimal option) : BudgetCategory =
    { Id = id; Name = name; Assigned = eur 0m; Activity = eur 0m; Available = eur available
      Underfunded = underfunded |> Option.map eur; Target = None; Pause = None; OpenWriteBack = None }

/// Lebensmittel und Drogerie sind überzogen, Kleidung nicht. Fehlbeträge 900 und 80.
let private monthOf (key: string) (toBeBudgeted: decimal) (categories: BudgetCategory list) : BudgetMonth =
    { Month = key; ToBeBudgeted = eur toBeBudgeted; Groups = [ { Name = "Laufend"; Categories = categories } ] }

let private openMonth (key: string) =
    monthOf key 1650m
        [ category foodId "Lebensmittel" -24.1m (Some 900m)
          category drugstoreId "Drogerie" -12.3m (Some 80m)
          category clothesId "Kleidung" 143m None ]

let private settledMonth (key: string) =
    monthOf key 90m [ category foodId "Lebensmittel" 0m (Some 0m); category clothesId "Kleidung" 143m None ]

let private frameAt (currentMonth: string) : BudgetFrame =
    { CurrentMonth = currentMonth; Months = [ past; current; next ]; CategoryOrder = [ foodId; drugstoreId; clothesId ]; MonthStartHidden = [] }

let private monthly (goal: decimal) : Target =
    { Goal = eur goal; Rhythm = Months 1; Kind = Need; Date = None; NeedsWholeAmount = Some true; Day = Some 15; CreatedIn = None }

/// Zwei Kategorien für Targets prüfen: eine gibt mehr aus (Hinweis), eine passt.
let private checkData (hints: bool) : TargetCheckData =
    let spending amount = [ for i in 0..11 -> { Month = DateTime(2025, 11, 1).AddMonths(i).ToString("yyyy-MM-01"); Activity = eur -amount } ]
    let source id name spent : TargetCheckSource =
        { CategoryId = id; Name = name; GroupName = "Laufend"; Target = Some (monthly 100m); Spending = spending spent; LastChange = None; FirstBooking = None }
    { Month = current
      Today = "2026-11-02"
      Categories = [ source foodId "Lebensmittel" (if hints then 150m else 100m); source clothesId "Kleidung" 100m ] }

/// Die Seite im gezeigten Monat, mit geladener Prüfung.
let private pageWith (frame: BudgetFrame) (month: BudgetMonth) (check: TargetCheckData) : Model =
    { Components.Budget.Types.init () with
        Frame = Success frame
        Month = Success month
        ChosenMonth = Some month.Month
        MonthStartCheck = Success check }

let private page = pageWith (frameAt current) (openMonth current) (checkData true)

let private update (msg: Msg) (model: Model) = Components.Budget.State.update msg model

let private step (msg: Msg) (model: Model) : Model =
    let next, _, _ = update msg model
    next

let private cmdOf (msg: Msg) (model: Model) =
    let _, cmd, _ = update msg model
    cmd

let private fieldOf (kind: MonthStartField) (fields: MonthStartFieldView list) =
    fields |> List.find (fun f -> f.Field = kind)

let private figures hints apply overdrawn tbb : MonthStartFigures =
    { Hints = hints; ApplyTotal = eur apply; Overdrawn = overdrawn; ToBeBudgeted = eur tbb }

let private fieldTests =
    testList "Felder der Leiste" [
        test "Targets prüfen zeigt die Hinweise, erledigt bei 0, ohne lesbare Prüfung leer" {
            let check hints = monthStartFields (figures hints 0m 0 0m) Set.empty |> fieldOf CheckField
            Expect.equal (check (HintsKnown 2)) { Field = CheckField; Value = "2"; Label = "Targets prüfen"; State = FieldOpen } "offen"
            Expect.equal ((check (HintsKnown 0)).Value, (check (HintsKnown 0)).State) (DoneMark, FieldDone) "erledigt"
            Expect.equal ((check HintsUnreadable).Value, (check HintsUnreadable).State) (UnknownMark, FieldOpen) "nicht lesbar"
            Expect.equal (check HintsPending).Value UnknownMark "lädt"
        }
        test "Targets prüfen zählt wie die Kopf-Karte der Seite: nur Zeilen außerhalb von Passt" {
            Expect.equal (hintsOf (Success (checkData true))) (HintsKnown 1) "eine mehr, eine passt"
            Expect.equal (hintsOf (Success (checkData false))) (HintsKnown 0) "beide passen"
            Expect.equal (hintsOf (Failure "weg")) HintsUnreadable "Lesefehler"
        }
        test "Targets anwenden zeigt die Summe, erledigt bei 0, aktiv bei offener Vorschau" {
            let apply total active = monthStartFields (figures (HintsKnown 0) total 0 0m) active |> fieldOf ApplyField
            Expect.equal ((apply 1560m Set.empty).Value, (apply 1560m Set.empty).State) ("1.560,00", FieldOpen) "offen"
            Expect.equal ((apply 0m Set.empty).Value, (apply 0m Set.empty).State) (DoneMark, FieldDone) "erledigt"
            Expect.equal (apply 1560m (Set.ofList [ ApplyField ])).State FieldActive "aktiv"
        }
        test "Überzogene decken zählt die überzogenen Kategorien, erledigt bei 0, aktiv beim Filter" {
            let covered count active = monthStartFields (figures (HintsKnown 0) 0m count 0m) active |> fieldOf OverdrawnField
            Expect.equal ((covered 2 Set.empty).Value, (covered 2 Set.empty).State) ("2", FieldOpen) "offen"
            Expect.equal ((covered 0 Set.empty).Value, (covered 0 Set.empty).State) (DoneMark, FieldDone) "erledigt"
            Expect.equal (covered 0 (Set.ofList [ OverdrawnField ])).State FieldActive "aktiv geht vor erledigt"
            Expect.equal (covered 2 Set.empty).Label "Überzogene decken" "Bezeichnung"
        }
        test "Verteilen zeigt Zu verteilen, solange positiv, sonst erledigt" {
            let spread tbb = monthStartFields (figures (HintsKnown 0) 0m 0 tbb) Set.empty |> fieldOf DistributeField
            Expect.equal ((spread 1650m).Value, (spread 1650m).State) ("1.650,00", FieldOpen) "offen"
            Expect.equal ((spread 0m).Value, (spread 0m).State) (DoneMark, FieldDone) "erledigt"
            Expect.equal (spread -5m).State FieldDone "negativ"
        }
        test "Die Felder stehen in der Reihenfolge der Skizze und folgen dem Monat" {
            let fields = monthStartBar page |> Option.defaultValue []
            Expect.equal (fields |> List.map (fun f -> f.Field)) [ CheckField; ApplyField; OverdrawnField; DistributeField ] "Reihenfolge"
            Expect.equal (fields |> List.map (fun f -> f.Value)) [ "1"; "980,00"; "2"; "1.650,00" ] "Werte aus Prüfung und Monat"
        }
        test "Filter Überzogen und offene Vorschau machen ihr Feld aktiv" {
            let states model = monthStartBar model |> Option.defaultValue [] |> List.map (fun f -> f.State)
            Expect.equal (states { page with Filter = OverdrawnOnly }) [ FieldOpen; FieldOpen; FieldActive; FieldOpen ] "Überzogen"
            Expect.equal (states { page with Targets = PreviewLoading }) [ FieldOpen; FieldActive; FieldOpen; FieldOpen ] "Vorschau"
        }
    ]

let private standsTests =
    testList "Wann die Leiste steht" [
        test "Im laufenden Monat steht sie, solange eines der Felder 1 bis 3 offen ist" {
            Expect.isTrue (monthStartStands CurrentMonth false true MonthStartByRule) "offen"
            Expect.isFalse (monthStartStands CurrentMonth false false MonthStartByRule) "Felder 1 bis 3 erledigt"
        }
        test "Für diesen Monat ausgeblendet steht sie nicht, im Vormonat ausgeblendet steht sie wieder" {
            Expect.isFalse (monthStartStands CurrentMonth true true MonthStartByRule) "ausgeblendet"
            let november = { page with MonthStartHidden = Set.ofList [ current ] }
            Expect.isNone (monthStartBar november) "im November ausgeblendet"
            let december = pageWith (frameAt next) (openMonth next) (checkData true)
            Expect.isSome (monthStartBar { december with MonthStartHidden = Set.ofList [ current ] }) "im Dezember steht sie wieder"
            let fromServer = { page with Frame = Success { frameAt current with MonthStartHidden = [ current ] } }
            Expect.isNone (monthStartBar fromServer) "ausgeblendet laut Server"
        }
        test "Felder 1 bis 3 erledigt bei offenem Verteilen: sie steht nicht" {
            let settled = pageWith (frameAt current) (settledMonth current) (checkData false)
            Expect.isNone (monthStartBar settled) "Zu verteilen 90 hält sie nicht"
            Expect.isFalse (anyFieldOpen (figures (HintsKnown 0) 0m 0 1650m)) "verteilen zählt nicht"
        }
        test "Eine Prüfung, die lädt oder nicht lesbar ist, hält die Leiste nicht auf" {
            Expect.isFalse (anyFieldOpen (figures HintsPending 0m 0 0m)) "lädt"
            Expect.isFalse (anyFieldOpen (figures HintsUnreadable 0m 0 0m)) "nicht lesbar"
            Expect.isTrue (anyFieldOpen (figures (HintsKnown 1) 0m 0 0m)) "ein Hinweis"
        }
        test "Im kommenden Monat nicht von selbst, im vergangenen nie" {
            Expect.isFalse (monthStartStands FutureMonth false true MonthStartByRule) "kommender Monat"
            Expect.isFalse (monthStartStands PastMonth false true MonthStartByRule) "vergangener Monat"
            Expect.isFalse (monthStartStands PastMonth false true MonthStartOpened) "auch nicht über den Kopfknopf"
        }
        test "Über den Kopfknopf geöffnet steht sie trotz Ausblenden oder fertig, auch im kommenden Monat" {
            Expect.isTrue (monthStartStands CurrentMonth true true MonthStartOpened) "ausgeblendet"
            Expect.isTrue (monthStartStands CurrentMonth false false MonthStartOpened) "fertig"
            Expect.isTrue (monthStartStands FutureMonth false false MonthStartOpened) "kommender Monat"
            Expect.isFalse (monthStartStands CurrentMonth false true MonthStartClosed) "über den Kopfknopf geschlossen"
        }
    ]

let private updateTests =
    testList "Leiste durch update" [
        test "Der Kopfknopf öffnet und schließt die Leiste und schreibt nichts" {
            let settled = pageWith (frameAt current) (settledMonth current) (checkData false)
            let opened = step ToggleMonthStart settled
            Expect.isSome (monthStartBar opened) "geöffnet, obwohl fertig"
            Expect.isEmpty (cmdOf ToggleMonthStart settled) "kein Aufruf"
            Expect.isNone (monthStartBar (step ToggleMonthStart opened)) "wieder zu"
            Expect.isNone (monthStartBar (step ToggleMonthStart page)) "eine stehende Leiste schließt er"
            Expect.isNone (monthStartBar (step PageLeft opened)) "nach dem Verlassen gilt die Regel"
        }
        test "Den Kopfknopf gibt es im laufenden und kommenden Monat, im vergangenen tut er nichts" {
            let pastPage = pageWith (frameAt current) (openMonth past) (checkData true)
            Expect.isFalse (monthStartButtonVisible pastPage) "nicht im vergangenen"
            Expect.equal (step ToggleMonthStart pastPage).MonthStartToggle MonthStartByRule "keine Wirkung"
            let future = pageWith (frameAt current) (openMonth next) (checkData true)
            Expect.isNone (monthStartBar future) "kommender Monat: nicht von selbst"
            Expect.isSome (monthStartBar (step ToggleMonthStart future)) "über den Kopfknopf"
        }
        test "Ausblenden blendet für den Monat aus, setzt den Filter auf Alle und speichert auf dem Server" {
            let filtered = { (step ToggleMonthStart (pageWith (frameAt current) (settledMonth current) (checkData false))) with Filter = OverdrawnOnly }
            let hidden, cmd, _ = update HideMonthStart filtered
            Expect.equal hidden.MonthStartHidden (Set.ofList [ current ]) "der November"
            Expect.equal hidden.Filter AllCategories "Filter Alle"
            Expect.equal hidden.MonthStartToggle MonthStartByRule "der Kopfknopf gilt nicht mehr"
            Expect.isNone (monthStartBar hidden) "weg"
            Expect.equal (List.length cmd) 1 "ein Aufruf an den Server"
        }
        test "Überzogene decken am PC: Filter Überzogen, erste überzogene Zeile, Tab Geld" {
            let covered = step (CoverOverdrawn true) { page with Tab = HistoryTab }
            Expect.equal covered.Filter OverdrawnOnly "Filter"
            Expect.equal (covered.Selected |> Option.map (fun s -> s.CategoryId)) (Some foodId) "Lebensmittel"
            Expect.equal covered.Tab MoneyTab "Tab Geld"
            // leiste-pc-rot.png: "900,00 zuweisen". Eine rote Kategorie startet sonst auf Holen.
            Expect.equal (covered.TabMoney |> Option.map (fun t -> t.CategoryId, t.Mode)) (Some (foodId, AssignMode)) "Modus Zuweisen"
            let fromTake = step (CoverOverdrawn true) (step (SwitchMoneyMode TakeMode) covered)
            Expect.equal (fromTake.TabMoney |> Option.map (fun t -> t.Mode)) (Some AssignMode) "auch wenn die Zeile schon gewählt war"
        }
        test "Überzogene decken am Handy setzt nur den Filter, ohne überzogene Zeile bleibt die leere Ansicht" {
            let phone = step (CoverOverdrawn false) page
            Expect.equal (phone.Filter, phone.Selected) (OverdrawnOnly, None) "nur der Filter"
            let settled = step (CoverOverdrawn true) (pageWith (frameAt current) (settledMonth current) (checkData false))
            Expect.equal (settled.Filter, settled.Selected) (OverdrawnOnly, None) "nichts gewählt"
        }
        test "Verteilen setzt den Filter Alle" {
            Expect.equal (step (SelectFilter AllCategories) { page with Filter = OverdrawnOnly }).Filter AllCategories "Alle"
        }
        test "Das Betreten lädt die Prüfung neu, eine späte Antwort zählt nicht, ein Fehler macht nur das Feld leer" {
            let entered = step PageEntered page
            Expect.equal entered.MonthStartCheck (Success (checkData true)) "der alte Stand bleibt bis zur Antwort"
            let stale = step (MonthStartCheckLoaded (entered.MonthStartCheckGeneration - 1, Ok (checkData false))) entered
            Expect.equal stale.MonthStartCheck (Success (checkData true)) "überholt"
            let failed = step (MonthStartCheckLoaded (entered.MonthStartCheckGeneration, Error "weg")) entered
            // Das Betreten lädt auch den Monat neu; die Leiste steht mit dem geladenen Monat.
            let shown = { page with MonthStartCheck = failed.MonthStartCheck }
            Expect.equal (monthStartBar shown |> Option.map (List.head >> fun f -> f.Value)) (Some UnknownMark) "Feld leer, Leiste steht"
        }
    ]

let private lineA = { CategoryId = foodId; CategoryName = "Lebensmittel"; GroupName = "Laufend"; OldAssigned = eur 0m; Underfunded = eur 900m; NewAssigned = eur 900m; Target = None }
let private lineB = { lineA with CategoryId = drugstoreId; CategoryName = "Drogerie"; Underfunded = eur 80m; NewAssigned = eur 80m }

let private preview : TargetPreview =
    { Month = current; Lines = [ lineA; lineB ]; Skipped = []; Total = eur 980m; ToBeBudgeted = eur 1650m; ToBeBudgetedAfter = eur 670m; CommandId = None }

let private ready = { page with Targets = PreviewReady { Shown = preview; Changed = false; Excluded = Set.empty } }

/// Eine Nachricht im Ablauf der Vorschau.
type private PreviewWay =
    | Deselect of Guid
    | Apply
    | ChangeMonth

let rec private permutations (items: 'a list) : 'a list list =
    match items with
    | [] -> [ [] ]
    | _ -> items |> List.collect (fun x -> permutations (List.filter ((<>) x) items) |> List.map (fun rest -> x :: rest))

let private previewTests =
    testList "Vorschau mit abwählbaren Zeilen" [
        test "Summe und Zu verteilen danach folgen der Auswahl, ohne Auswahl ist Anwenden gesperrt" {
            let shown = { Shown = preview; Changed = false; Excluded = Set.ofList [ foodId ] }
            let selected = selectedPreview shown
            Expect.equal (selected.Total, selected.ToBeBudgetedAfter) (eur 80m, eur 1570m) "nur Drogerie"
            Expect.isTrue (canApplyTargets shown) "eine Zeile gewählt"
            Expect.isFalse (canApplyTargets { shown with Excluded = Set.ofList [ foodId; drugstoreId ] }) "keine gewählt"
            Expect.isTrue (lineChosen shown drugstoreId && not (lineChosen shown foodId)) "Haken"
        }
        test "Wieder anwählen nimmt die Zeile zurück, eine neue Vorschau beginnt mit allen Zeilen" {
            let twice = ready |> step (ToggleTargetLine foodId) |> step (ToggleTargetLine foodId)
            Expect.equal twice.Targets ready.Targets "wie vorher"
            let changed = { (step (ToggleTargetLine foodId) ready) with Targets = Applying preview } |> step (TargetsApplied (Ok (PreviewChanged preview)))
            Expect.equal changed.Targets (PreviewReady { Shown = preview; Changed = true; Excluded = Set.empty }) "alle gewählt"
        }
        // Doktrin Punkt 20: alle Reihenfolgen von Abwählen, Anwenden und Monatswechsel nach dem Öffnen.
        test "Anwenden schickt genau die Zeilen, die beim Anwenden gewählt sind, in jeder Reihenfolge" {
            let ways = permutations [ Deselect foodId; Deselect drugstoreId; Apply; ChangeMonth ]
            for way in ways do
                // Orakel: bis zum Anwenden zählt jedes Abwählen, danach keins; der Monatswechsel ändert die Auswahl nicht.
                let excluded, sent =
                    way
                    |> List.fold
                        (fun (excluded: Set<Guid>, sent: Guid list option) msg ->
                            match msg, sent with
                            | Deselect id, None -> excluded.Add id, None
                            | Apply, None ->
                                let chosen = [ foodId; drugstoreId ] |> List.filter (excluded.Contains >> not)
                                excluded, (if List.isEmpty chosen then None else Some chosen)
                            | _ -> excluded, sent)
                        (Set.empty, None)
                let toMsg w =
                    match w with
                    | Deselect id -> ToggleTargetLine id
                    | Apply -> ConfirmTargets
                    | ChangeMonth -> SelectMonth next
                let final, sentCmds =
                    way |> List.fold (fun (model, sends) w ->
                        let next, cmd, _ = update (toMsg w) model
                        next, (if w = Apply then sends + List.length cmd else sends)) (ready, 0)
                match sent with
                | Some lines ->
                    match final.Targets with
                    | Applying p ->
                        Expect.equal (p.Lines |> List.map (fun l -> l.CategoryId)) lines $"%A{way}: gesendete Zeilen"
                        Expect.equal p.Total (eur (p.Lines |> List.sumBy (fun l -> l.Underfunded.Amount))) $"%A{way}: Summe der Auswahl"
                        Expect.equal p.Month current $"%A{way}: Monat der Vorschau"
                    | other -> failtest $"%A{way}: Anwenden erwartet, bekam %A{other}"
                    Expect.equal sentCmds 1 $"%A{way}: genau ein Aufruf"
                | None ->
                    Expect.equal final.Targets (PreviewReady { Shown = preview; Changed = false; Excluded = excluded }) $"%A{way}: nichts gesendet"
                    Expect.equal sentCmds 0 $"%A{way}: kein Aufruf"
        }
    ]

[<Tests>]
let tests =
    testList "Monatserster, Client (budgeting-m3x7f)" [ fieldTests; standsTests; updateTests; previewTests ]
