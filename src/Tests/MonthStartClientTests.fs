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
      Underfunded = underfunded |> Option.map eur; Target = None; Pause = None; OpenWriteBack = None; TargetInYnab = None; Deactivated = None }

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
            let check hints = monthStartFields CurrentMonth (figures hints 0m 0 0m) Set.empty |> fieldOf CheckField
            Expect.equal (check (HintsKnown 2)) { Field = CheckField; Value = "2"; Label = "Targets prüfen"; State = FieldOpen; Title = None; AriaLabel = None } "offen"
            Expect.equal ((check (HintsKnown 0)).Value, (check (HintsKnown 0)).State) (DoneMark, FieldDone) "erledigt"
            let unreadable = check (HintsUnreadable "YNAB antwortet nicht")
            Expect.equal (unreadable.Value, unreadable.State) (UnknownMark, FieldOpen) "nicht lesbar"
            Expect.equal unreadable.Title (Some "YNAB antwortet nicht") "die Meldung als Tooltip"
            Expect.equal (check HintsPending).Value UnknownMark "lädt"
            Expect.isNone (check HintsPending).Title "lädt: kein Tooltip"
        }
        test "Im kommenden Monat zeigt Targets prüfen die Zahl mit dem Hinweis auf den laufenden Monat" {
            // budgeting-gvajj, Bug: `getTargetCheck` gilt immer für den laufenden Monat. Im kommenden Monat zeigte das
            // Feld dieselbe Zahl ohne Hinweis, dass sie zum laufenden Monat gehört (Entscheidung Roman: B).
            let check hints = monthStartFields FutureMonth (figures hints 0m 0 0m) Set.empty |> fieldOf CheckField
            let three = check (HintsKnown 3)
            Expect.equal (three.Value, three.State) ("3", FieldOpen) "die Zahl bleibt"
            Expect.equal three.Title (Some "Hinweise für den laufenden Monat") "Tooltip"
            Expect.equal three.AriaLabel (Some "Targets prüfen 3, Hinweise für den laufenden Monat") "aria-label"
            let none = check (HintsKnown 0)
            Expect.equal (none.Value, none.State) (DoneMark, FieldDone) "erledigt"
            Expect.equal none.AriaLabel (Some "Targets prüfen erledigt, Hinweise für den laufenden Monat") "aria-label erledigt"
            let unreadable = check (HintsUnreadable "YNAB antwortet nicht")
            Expect.equal unreadable.Value UnknownMark "nicht lesbar"
            Expect.equal unreadable.Title (Some "Hinweise für den laufenden Monat. YNAB antwortet nicht") "Hinweis und Meldung"
            Expect.equal unreadable.AriaLabel (Some "Targets prüfen nicht bekannt, Hinweise für den laufenden Monat") "aria-label ohne Zahl"
            let others = monthStartFields FutureMonth (figures (HintsKnown 3) 10m 1 5m) Set.empty |> List.filter (fun f -> f.Field <> CheckField)
            Expect.isTrue (others |> List.forall (fun f -> f.Title.IsNone && f.AriaLabel.IsNone)) "die anderen Felder gelten dem gezeigten Monat"
            let future = step ToggleMonthStart (pageWith (frameAt current) (openMonth next) (checkData true))
            let shown = monthStartBar future |> Option.map (fieldOf CheckField)
            Expect.equal (shown |> Option.bind (fun f -> f.Title)) (Some "Hinweise für den laufenden Monat") "die Leiste im Dezember"
            Expect.isNone (monthStartBar page |> Option.bind (fieldOf CheckField >> fun f -> f.AriaLabel)) "im laufenden Monat kein Zusatz"
        }
        test "Targets prüfen zählt wie die Kopf-Karte der Seite: nur Zeilen außerhalb von Passt" {
            Expect.equal (hintsOf (Success (checkData true))) (HintsKnown 1) "eine mehr, eine passt"
            Expect.equal (hintsOf (Success (checkData false))) (HintsKnown 0) "beide passen"
            Expect.equal (hintsOf (Failure "weg")) (HintsUnreadable "weg") "Lesefehler mit Meldung"
        }
        test "Targets anwenden zeigt die Summe, erledigt bei 0, aktiv bei offener Vorschau" {
            let apply total active = monthStartFields CurrentMonth (figures (HintsKnown 0) total 0 0m) active |> fieldOf ApplyField
            Expect.equal ((apply 1560m Set.empty).Value, (apply 1560m Set.empty).State) ("1.560,00", FieldOpen) "offen"
            Expect.equal ((apply 0m Set.empty).Value, (apply 0m Set.empty).State) (DoneMark, FieldDone) "erledigt"
            Expect.equal (apply 1560m (Set.ofList [ ApplyField ])).State FieldActive "aktiv"
        }
        test "Überzogene decken zählt die überzogenen Kategorien, erledigt bei 0, aktiv beim Filter" {
            let covered count active = monthStartFields CurrentMonth (figures (HintsKnown 0) 0m count 0m) active |> fieldOf OverdrawnField
            Expect.equal ((covered 2 Set.empty).Value, (covered 2 Set.empty).State) ("2", FieldOpen) "offen"
            Expect.equal ((covered 0 Set.empty).Value, (covered 0 Set.empty).State) (DoneMark, FieldDone) "erledigt"
            Expect.equal (covered 0 (Set.ofList [ OverdrawnField ])).State FieldActive "aktiv geht vor erledigt"
            Expect.equal (covered 2 Set.empty).Label "Überzogene decken" "Bezeichnung"
        }
        test "Verteilen zeigt Zu verteilen, solange positiv, sonst erledigt" {
            let spread tbb = monthStartFields CurrentMonth (figures (HintsKnown 0) 0m 0 tbb) Set.empty |> fieldOf DistributeField
            Expect.equal ((spread 1650m).Value, (spread 1650m).State) ("1.650,00", FieldOpen) "offen"
            Expect.equal ((spread 0m).Value, (spread 0m).State) (DoneMark, FieldDone) "erledigt"
            Expect.equal (spread -5m).State FieldDone "negativ"
        }
        test "Die Felder stehen in der Reihenfolge der Skizze und folgen dem Monat" {
            let fields = monthStartBar page |> Option.defaultValue []
            Expect.equal (fields |> List.map (fun f -> f.Field)) [ CheckField; ApplyField; OverdrawnField; DistributeField ] "Reihenfolge"
            Expect.equal (fields |> List.map (fun f -> f.Value)) [ "1"; "980,00"; "2"; "1.650,00" ] "Werte aus Prüfung und Monat"
        }
        test "Targets anwenden zählt eine in BB deaktivierte Kategorie mit Fehlbetrag nicht mit" {
            // Regressionstest budgeting-t7dka: vorher zählte die Leiste den Fehlbetrag der deaktivierten Drogerie mit.
            let month =
                monthOf current 1650m
                    [ category foodId "Lebensmittel" -24.1m (Some 900m)
                      { category drugstoreId "Drogerie" -12.3m (Some 80m) with Deactivated = Some true } ]
            let fields = monthStartBar (pageWith (frameAt current) month (checkData true)) |> Option.defaultValue []
            Expect.equal (fields |> List.map (fun f -> f.Field, f.Value) |> List.find (fun (f, _) -> f = ApplyField)) (ApplyField, "900,00") "ohne Drogerie"
        }
        test "Filter Überzogen und offene Vorschau machen ihr Feld aktiv" {
            let states model = monthStartBar model |> Option.defaultValue [] |> List.map (fun f -> f.State)
            Expect.equal (states { page with Filter = OverdrawnOnly }) [ FieldOpen; FieldOpen; FieldActive; FieldOpen ] "Überzogen"
            Expect.equal (states { page with Targets = PreviewLoading }) [ FieldOpen; FieldActive; FieldOpen; FieldOpen ] "Vorschau"
        }
    ]

let private fieldMsgTests =
    testList "Was ein Feld tut" [
        test "Jedes Feld schickt seine Nachricht, nur Überzogene decken unterscheidet PC und Handy" {
            for panel in [ true; false ] do
                Expect.equal (fieldMsg panel CheckField) OpenTargetCheck $"Targets prüfen, panel {panel}"
                Expect.equal (fieldMsg panel ApplyField) OpenTargets $"Targets anwenden, panel {panel}"
                Expect.equal (fieldMsg panel OverdrawnField) (CoverOverdrawn panel) $"Überzogene decken, panel {panel}"
                Expect.equal (fieldMsg panel DistributeField) (SelectFilter AllCategories) $"verteilen, panel {panel}"
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
            Expect.isFalse (anyFieldOpen (figures (HintsUnreadable "weg") 0m 0 0m)) "nicht lesbar"
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
        test "Im kommenden Monat über den Kopfknopf geöffnet, blendet Ausblenden nur diesen Monat aus" {
            let december = step ToggleMonthStart (pageWith (frameAt current) (openMonth next) (checkData true))
            Expect.isSome (monthStartBar december) "über den Kopfknopf im Dezember"
            let hidden, sent = hideMonthStart december
            Expect.equal sent (Some next) "der Server hält den Dezember fest"
            Expect.equal hidden.MonthStartHidden (Set.ofList [ next ]) "nur der Dezember"
            let _, cmd, _ = update HideMonthStart december
            Expect.equal (List.length cmd) 1 "ein Aufruf an den Server"
        }
        test "Ausblenden nicht gespeichert: Warnung, die Leiste bleibt auf dieser Seite ausgeblendet" {
            let hidden = step HideMonthStart page
            let after, cmd, external = update (MonthStartHideAnswered (Error "Ausblenden nicht gespeichert.")) hidden
            Expect.equal external (ShowToast ("Ausblenden nicht gespeichert.", ToastWarning)) "Warnung mit dem Satz des Servers, ohne Doppelung"
            let _, _, offline = update (MonthStartHideAnswered (Error "Netzwerk nicht erreichbar")) hidden
            Expect.equal offline (ShowToast ("Ausblenden nicht gespeichert: Netzwerk nicht erreichbar", ToastWarning)) "ein Netzfehler bekommt den Zusammenhang"
            Expect.isEmpty cmd "kein neuer Aufruf"
            Expect.isNone (monthStartBar after) "bleibt ausgeblendet"
            Expect.equal after.MonthStartHidden (Set.ofList [ current ]) "der November"
        }
        test "Überzogene decken leert die Suche, damit Zahl und Liste übereinstimmen" {
            // Mit der Suche "Drog" wäre Drogerie die erste überzogene Zeile, das Feld zählt aber zwei.
            let covered = step (CoverOverdrawn true) { page with CategorySearch = "Drog" }
            Expect.equal covered.CategorySearch "" "Suche leer"
            Expect.equal (covered.Selected |> Option.map (fun s -> s.CategoryId)) (Some foodId) "die erste überzogene der ganzen Liste"
            let phone = step (CoverOverdrawn false) { page with CategorySearch = "Drog" }
            Expect.equal (phone.CategorySearch, phone.Filter) ("", OverdrawnOnly) "am Handy auch"
        }
        test "Überzogene decken während eines Monatswechsels setzt nur den Filter" {
            let changing = step (SelectMonth next) { page with Tab = HistoryTab }
            Expect.isTrue (Components.Budget.Month.monthChanging changing) "der Wechsel läuft"
            let covered = step (CoverOverdrawn true) changing
            Expect.equal covered.Filter OverdrawnOnly "Filter"
            Expect.isNone covered.Selected "keine Zeile aus dem alten Monat"
            Expect.equal covered.Tab HistoryTab "kein Tabwechsel"
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

let private targetChangeTests =
    // budgeting-gvajj, Bug: die Leiste las ihre Prüfung nur bei `PageEntered`. Nach einer Target-Änderung im Inspektor
    // oder auf der Seite Targets prüfen zeigte das Feld Targets prüfen den alten Stand, bis Roman die Seite verließ und
    // wieder betrat.
    let written = TargetChangeAnswered (foodId, current, Ok (TargetWritten (Some (monthly 150m))))
    let lost = TargetChangeCallLost (foodId, current, "Http error (0)")
    let rereads (name: string) (msg: Msg) =
        test name {
            let g = page.MonthStartCheckGeneration
            let answered = step msg page
            Expect.equal answered.MonthStartCheckGeneration (g + 1) "eine neue Prüfung"
            Expect.equal answered.MonthStartCheck (Success (checkData true)) "der alte Stand bleibt bis zur Antwort"
            let stale = step (MonthStartCheckLoaded (g, Ok (checkData false))) answered
            Expect.equal stale.MonthStartCheck (Success (checkData true)) "die ältere Antwort zählt nicht"
            let fresh = step (MonthStartCheckLoaded (g + 1, Ok (checkData false))) answered
            Expect.equal fresh.MonthStartCheck (Success (checkData false)) "der neue Stand"
        }
    testList "Leiste nach einer Target-Änderung" [
        rereads "Nach einem geschriebenen Target liest die Leiste ihre Prüfung neu" written
        rereads "Nach einem Target-Aufruf mit offenem Ausgang liest die Leiste ihre Prüfung neu" lost
        test "Eine abgelehnte Target-Änderung liest die Leiste nicht neu" {
            let rejected = step (TargetChangeAnswered (foodId, current, Error "abgelehnt")) page
            Expect.equal rejected.MonthStartCheckGeneration page.MonthStartCheckGeneration "keine neue Prüfung"
            Expect.equal rejected.MonthStartCheck page.MonthStartCheck "der Stand bleibt"
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
        test "Wieder anwählen nimmt die Zeile zurück" {
            let twice = ready |> step (ToggleTargetLine foodId) |> step (ToggleTargetLine foodId)
            Expect.equal twice.Targets ready.Targets "wie vorher"
        }
        // Regression: nach "Stand hat sich geändert" begann die neue Vorschau mit allen Zeilen, ein zweites Anwenden
        // schrieb die abgewählte Kategorie mit.
        test "Stand hat sich geändert: die Abwahl bleibt, soweit die Zeile in der neuen Vorschau noch steht" {
            let applying = ready |> step (ToggleTargetLine foodId) |> step ConfirmTargets
            let fresh = { preview with Lines = [ { lineA with Underfunded = eur 950m; NewAssigned = eur 950m }; lineB ]; Total = eur 1030m; ToBeBudgetedAfter = eur 620m }
            let changed = applying |> step (TargetsApplied (Ok (PreviewChanged fresh)))
            Expect.equal changed.Targets (PreviewReady { Shown = fresh; Changed = true; Excluded = Set.ofList [ foodId ] }) "Abwahl bleibt"
            let _, resent = confirmTargets changed
            Expect.equal (resent |> Option.map (fun p -> p.Lines |> List.map (fun l -> l.CategoryId))) (Some [ drugstoreId ]) "das zweite Anwenden schickt nur Drogerie"
            let withoutFood = { preview with Lines = [ lineB ]; Total = eur 80m; ToBeBudgetedAfter = eur 1570m }
            let gone = applying |> step (TargetsApplied (Ok (PreviewChanged withoutFood)))
            Expect.equal gone.Targets (PreviewReady { Shown = withoutFood; Changed = true; Excluded = Set.empty }) "eine verschwundene Zeile fällt aus der Abwahl"
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
                    // Summe und Zu verteilen danach aus den Beträgen der Testdaten: Lebensmittel 900, Drogerie 80,
                    // Zu verteilen 1.650.
                    let amountOf id = if id = foodId then 900m else 80m
                    let total = lines |> List.sumBy amountOf
                    match final.Targets with
                    | Applying shown ->
                        let p = selectedPreview shown
                        Expect.equal (p.Lines |> List.map (fun l -> l.CategoryId)) lines $"%A{way}: gesendete Zeilen"
                        Expect.equal p.Total (eur total) $"%A{way}: Summe der Auswahl"
                        Expect.equal p.ToBeBudgetedAfter (eur (1650m - total)) $"%A{way}: Zu verteilen danach"
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
    testList "Monatserster, Client (budgeting-m3x7f)" [ fieldTests; fieldMsgTests; standsTests; updateTests; targetChangeTests; previewTests ]
