module Tests.TargetCheckClientTests

// budgeting-q4v8n: die Seite Targets prüfen `#/budget/target-check` durch das echte `update` der Budget-Seite
// und der App. Die Cmds werden unter .NET nur gebaut, nie ausgeführt: der Proxy liegt hinter einem Lambda.
// Sichtbarkeit und Auftrag sind reine Funktionen (`TargetCheck`). Namen und Beträge sind erfunden.

open System
open Expecto
open Shared.Domain
open Shared.Budgeting
open Types
open Client.DesignSystem
open Components.Budget.Types
open Components.Budget.Month
open Components.Budget.TargetFormCore
open Components.Budget.TargetEdit
open Components.Budget.TargetCheck

let private eur (amount: decimal) : Money = { Amount = amount; Currency = "EUR" }

let private past = "2026-10-01"
let private current = "2026-11-01"
let private today = "2026-11-06"

let private moreId = Guid.Parse "00000000-0000-0000-0000-0000000c4801"
let private lessId = Guid.Parse "00000000-0000-0000-0000-0000000c4802"
let private fitsId = Guid.Parse "00000000-0000-0000-0000-0000000c4803"
let private withoutId = Guid.Parse "00000000-0000-0000-0000-0000000c4804"
let private neverId = Guid.Parse "00000000-0000-0000-0000-0000000c4805"

let private monthly (goal: decimal) : Target =
    { Goal = eur goal; Rhythm = Months 1; Kind = Need; Date = None; NeedsWholeAmount = Some true; Day = Some 15; CreatedIn = None }

let private weekly (goal: decimal) : Target =
    { monthly goal with Rhythm = Weeks 1; Day = Some 1 }

/// Mehr: Target 100 € mit Fälligkeit, Ausgaben 150 € je Monat.
let private moreTarget = { monthly 100m with Date = Some "2027-03-01" }
/// Weniger: 50 € je Woche (216,67 € im Monat), Ausgaben 100 € je Monat.
let private lessTarget = weekly 50m

/// Die 12 vollen Monate vor November 2026, je Monat dieselben Ausgaben.
let private spending (amount: decimal) : MonthSpending list =
    [ for i in 0..11 -> { Month = DateTime(2025, 11, 1).AddMonths(i).ToString("yyyy-MM-01"); Activity = eur -amount } ]

let private source (id: Guid) (name: string) (target: Target option) (spent: decimal) : TargetCheckSource =
    { CategoryId = id; Name = name; GroupName = "Haushalt"; Target = target; Spending = spending spent; LastChange = None }

let private checkData : TargetCheckData =
    { Month = current
      Today = today
      Categories =
        [ source moreId "Strom" (Some moreTarget) 150m
          source lessId "Kantine" (Some lessTarget) 100m
          source fitsId "Internet" (Some (monthly 100m)) 100m
          source withoutId "Bücher" None 40m
          source neverId "Kino" (Some (monthly 30m)) 0m ] }

let private budgetCategory (id: Guid) (name: string) (target: Target option) : BudgetCategory =
    { Id = id; Name = name; Assigned = eur 0m; Activity = eur 0m; Available = eur 0m
      Underfunded = None; Target = target; Pause = None; OpenWriteBack = None }

let private budgetMonth (month: string) : BudgetMonth =
    { Month = month
      ToBeBudgeted = eur 0m
      Groups =
        [ { Name = "Haushalt"
            Categories =
              [ budgetCategory moreId "Strom" (Some moreTarget)
                budgetCategory lessId "Kantine" (Some lessTarget)
                budgetCategory fitsId "Internet" (Some (monthly 100m))
                budgetCategory withoutId "Bücher" None
                budgetCategory neverId "Kino" (Some (monthly 30m)) ] } ] }

let private frame : BudgetFrame =
    { CurrentMonth = current; Months = [ past; current ]; CategoryOrder = [ moreId; lessId; fitsId; withoutId; neverId ] }

/// Die Budget-Seite im laufenden Monat.
let private page : Model =
    { Components.Budget.Types.init () with
        Frame = Success frame
        Month = Success (budgetMonth current)
        ChosenMonth = Some current }

let private update (msg: Msg) (model: Model) = Components.Budget.State.update msg model

let private step (msg: Msg) (model: Model) : Model =
    let next, _, _ = update msg model
    next

let private sends (msg: Msg) (model: Model) : bool =
    let _, cmd, _ = update msg model
    not (List.isEmpty cmd)

let private answer (result: Result<TargetCheckData, string>) (model: Model) : Model =
    model |> step (TargetCheckLoaded (model.TargetCheck.Generation, result))

/// Die Seite betreten und die Rohdaten einnehmen.
let private checkedPage (model: Model) : Model =
    model |> step TargetCheckEntered |> answer (Ok checkData)

let private loaded () = checkedPage page

let private dataOf (model: Model) : TargetCheckData =
    match model.TargetCheck.Data with
    | Success data -> data
    | other -> failtest $"Rohdaten erwartet, kam {other}"

let private rowIn (model: Model) (id: Guid) : TargetCheckRow =
    checkGroups (dataOf model) |> List.collect snd |> List.find (fun r -> r.CategoryId = id)

let private groupOf (model: Model) (id: Guid) : CheckGroup = (rowIn model id).Group

let private budgetTarget (model: Model) (id: Guid) : Target option =
    match model.Month with
    | Success month -> (categoriesOf month |> List.find (fun c -> c.Id = id)).Target
    | other -> failtest $"Monat erwartet, kam {other}"

let private panelOf (model: Model) : TargetForm =
    match model.TargetCheck.Panel with
    | Some form -> form
    | None -> failtest "Panel erwartet"

let private answered (id: Guid) (result: Result<TargetChangeResult, string>) (model: Model) =
    update (TargetChangeAnswered (id, current, result)) model

let private rootModel (pageNow: Page) (budget: Model) : State.Model =
    let settingsModel, _ = Components.Settings.State.init ()
    let syncFlowModel, _ = Components.SyncFlow.State.init ()
    let rulesModel, _ = Components.Rules.State.init ()
    let ledgerModel, _ = Components.Ledger.State.init ()
    { CurrentPage = pageNow
      Toasts = []
      Pwa = Pwa.initialState
      Settings = settingsModel
      SyncFlow = syncFlowModel
      Rules = rulesModel
      Ledger = ledgerModel
      Budget = budget
      QuickAdd = None
      QuickAddTemplates = NotAsked }

let private dispatched (cmd: Elmish.Cmd<State.Msg>) : State.Msg list =
    let collected = ResizeArray<State.Msg>()
    cmd |> List.iter (fun effect -> effect collected.Add)
    List.ofSeq collected

// ============================================
// Route, Rückkehr, Laden, Aufklappen
// ============================================

let private pageTests =
    testList "Targets prüfen: Seite" [
        test "#/budget/target-check wird geparst und erzeugt" {
            Expect.equal (Routing.parseUrl [ "budget"; "target-check" ]) BudgetTargetCheck "parse"
            Expect.equal (Routing.toUrlSegments BudgetTargetCheck) [ "budget"; "target-check" ] "toUrl"
        }
        test "Der Knopf Targets prüfen führt über das App-update zur Route, die Budget-Seite wartet auf die Rückkehr" {
            let root, cmd = State.update (State.BudgetMsg OpenTargetCheck) (rootModel Page.Budget page)
            Expect.contains (dispatched cmd) (State.NavigateTo BudgetTargetCheck) "zur Seite"
            Expect.isTrue root.Budget.TargetCheck.ReturnPending "wartet"
            let ledger, _ = Components.Ledger.State.init ()
            let messages = dispatched (State.urlChangedCmd ledger Page.Budget BudgetTargetCheck)
            Expect.contains messages (State.BudgetMsg PageLeft) "PageLeft"
            Expect.contains messages (State.BudgetMsg TargetCheckEntered) "die Prüfung lädt"
        }
        test "Zurück nach dem Knopf geht einen Schritt zurück, Monat und Auswahl bleiben, der Monat wird still gelesen" {
            let chosen = { page with ChosenMonth = Some past; Month = Success (budgetMonth past) } |> step (SelectCategory moreId)
            let shown = chosen |> step OpenTargetCheck |> step PageLeft |> checkedPage
            let _, _, back = update LeaveTargetCheck shown
            Expect.equal back StepBackToBudgetPage "ein Schritt zurück"
            let returned, cmd, _ = update PageEntered shown
            Expect.equal returned.ChosenMonth (Some past) "der Monat bleibt"
            Expect.equal (returned.Selected |> Option.map (fun s -> s.CategoryId)) (Some moreId) "die Auswahl bleibt"
            Expect.isGreaterThan returned.MonthGeneration shown.MonthGeneration "still neu gelesen"
            Expect.isNonEmpty cmd "Aufruf"
            Expect.isFalse returned.TargetCheck.ReturnPending "angekommen"
        }
        test "Per Link geöffnet führt Zurück zur Budget-Seite, woandershin verlassen beginnt sie von vorn" {
            let viaLink = { page with ChosenMonth = Some past } |> checkedPage
            let _, _, back = update LeaveTargetCheck viaLink
            Expect.equal back ShowBudgetPage "neuer Eintrag"
            let _, cmd = State.update (State.BudgetMsg LeaveTargetCheck) (rootModel BudgetTargetCheck viaLink)
            Expect.contains (dispatched cmd) (State.NavigateTo Page.Budget) "zur Budget-Seite"
            Expect.isNone (viaLink |> step PageEntered).ChosenMonth "per Link: laufender Monat"
            let ledger, _ = Components.Ledger.State.init ()
            let messages target = dispatched (State.urlChangedCmd ledger BudgetTargetCheck target)
            Expect.contains (messages Page.Settings) (State.BudgetMsg TargetCheckAbandoned) "woandershin"
            Expect.isFalse (messages Page.Budget |> List.contains (State.BudgetMsg TargetCheckAbandoned)) "zurück"
            let later =
                { page with ChosenMonth = Some past } |> step OpenTargetCheck |> step TargetCheckAbandoned |> step PageEntered
            Expect.isNone later.ChosenMonth "danach laufender Monat"
        }
        test "Laden: eine Antwort mit fremder Generation wird verworfen, ein Fehler zeigt Wiederholen" {
            let first = page |> step TargetCheckEntered
            let old = first.TargetCheck.Generation
            let fresh = first |> step TargetCheckEntered |> answer (Ok checkData)
            Expect.equal (fresh |> step (TargetCheckLoaded (old, Ok { checkData with Categories = [] }))).TargetCheck.Data fresh.TargetCheck.Data "alte Antwort"
            Expect.equal (fresh |> step (TargetCheckLoaded (old, Error "alt"))).TargetCheck.Data fresh.TargetCheck.Data "alter Fehler"
            let failed = page |> step TargetCheckEntered |> answer (Error "kaputt")
            Expect.equal failed.TargetCheck.Data (Failure "kaputt") "Fehlerkarte"
        }
        test "Wiederholen lädt neu und behält das Aufklappen, eine Antwort von vorher zählt nicht" {
            let opened = loaded () |> step ToggleFits
            let failed = opened |> step RetryTargetCheck |> answer (Error "kaputt")
            let retrying, cmd, _ = update RetryTargetCheck failed
            Expect.equal retrying.TargetCheck.Data Loading "lädt"
            Expect.isNonEmpty cmd "Aufruf"
            Expect.isTrue retrying.TargetCheck.FitsOpen "aufgeklappt bleibt"
            let stale = retrying |> step (TargetCheckLoaded (failed.TargetCheck.Generation, Ok checkData))
            Expect.equal stale.TargetCheck.Data Loading "die Antwort vor dem Wiederholen zählt nicht"
        }
        test "Passt ist beim Betreten zu, ein Tipp klappt auf, ein zweiter zu, ein neues Betreten wieder zu" {
            let entered = loaded ()
            Expect.isFalse entered.TargetCheck.FitsOpen "zu"
            let opened = entered |> step ToggleFits
            Expect.isTrue opened.TargetCheck.FitsOpen "auf"
            Expect.isFalse (opened |> step ToggleFits).TargetCheck.FitsOpen "zu"
            Expect.isFalse (opened |> checkedPage).TargetCheck.FitsOpen "neu betreten: zu"
        }
        test "Sind die vier ersten Gruppen leer, steht Nichts zu prüfen" {
            let onlyFits = { checkData with Categories = [ source fitsId "Internet" (Some (monthly 100m)) 100m ] }
            Expect.isTrue (nothingToCheck (checkGroups onlyFits)) "nur Passt"
            Expect.isFalse (nothingToCheck (checkGroups checkData)) "mit Gruppen"
        }
        test "Die Werte einer Zeile: Monatswert, gelesener Rhythmus, Ø und Monate, bei Passt mit Zusatz nur der Zusatz" {
            let row id = checkGroups checkData |> List.collect snd |> List.find (fun r -> r.CategoryId = id)
            let values id =
                match checkRowFacts (row id) with
                | Values values -> values
                | NoteOnly note -> failtest $"Werte erwartet, kam der Zusatz {note}"
            let less = values lessId
            Expect.equal (less.Monthly, less.Read, less.Average, less.Count)
                (Some { Before = "Target "; Amount = "216,67 €"; After = "/Monat" },
                 Some { Before = ""; Amount = "50,00 €"; After = "/Woche" },
                 Some { Before = "Ø "; Amount = "100,00 €"; After = "" },
                 "0 von 12 Monaten darüber") "weniger, wöchentlich, Beträge getrennt von den Wörtern"
            let more = values moreId
            Expect.equal (more.Read, more.Count) (None, "12 von 12 Monaten darüber") "mehr, monatlich ohne gelesenen Betrag"
            Expect.equal (values withoutId).Count "12 von 12 Monaten mit Ausgaben" "ohne Target"
            Expect.equal (values neverId).Count "0 von 12 Monaten mit Ausgaben" "nie"
            Expect.equal (values fitsId).Monthly (Some { Before = "Target "; Amount = "100,00 €"; After = "/Monat" }) "Passt ohne Zusatz zeigt Werte"
            let set = { source fitsId "Internet" (Some (monthly 100m)) 100m with LastChange = Some (TargetSetOn "2026-11-02") }
            Expect.equal (checkRowFacts (targetCheckRow today set)) (NoteOnly "neu gesetzt am 02.11.2026") "mit Zusatz nur der Zusatz, ohne Werte"
            Expect.equal (checkRowBars checkData (row moreId) |> List.map (fun b -> b.Kind)) (List.replicate 12 PastMonth) "12 Balken, alle vorbei"
        }
        test "Der Toast einer Antwort: offenes Formular keiner, von der Seite wie übernehmen, sonst Ablehnung als Warnung" {
            let results = [ Ok (TargetWritten None); Ok (TargetChangedMeanwhile None); Error "nein" ]
            for origin in [ Some FromCheckRow; Some FromCheckPanel; Some FromTargetTab; None ] do
                for result in results do
                    Expect.equal (targetAnswerToast origin true result) NoOp $"{origin}, {result}: offenes Formular"
            for origin in [ Some FromCheckRow; Some FromCheckPanel ] do
                for result in results do
                    Expect.equal (targetAnswerToast origin false result) (takeAnswerToast result) $"{origin}, {result}: wie übernehmen"
            Expect.equal (takeAnswerToast (Error "nein")) (ShowToast ("Target nicht gespeichert: nein", ToastError)) "abgelehnt als Fehler"
            for origin in [ Some FromTargetTab; None ] do
                Expect.equal (targetAnswerToast origin false (Error "nein")) (ShowToast ("Target nicht gespeichert: nein", ToastWarning)) $"{origin}: Warnung"
                Expect.equal (targetAnswerToast origin false (Ok (TargetChangedMeanwhile None)))
                    (ShowToast (targetRejectedAwayText TargetChangedMeanwhileText, ToastWarning)) $"{origin}: inzwischen geändert"
                Expect.equal (targetAnswerToast origin false (Ok (TargetWritten None))) NoOp $"{origin}: festgehalten"
        }
        test "Der Toast eines Abbruchs: offenes Formular keiner, von der Seite verspricht er nur das Neuladen der Prüfung" {
            let toast rereads = ShowToast (targetCallLostText rereads "weg", ToastError)
            for origin in [ Some FromCheckRow; Some FromCheckPanel; Some FromTargetTab; None ] do
                Expect.equal (targetCallLostToast origin true true true "weg") NoOp $"{origin}: offenes Formular"
            for origin in [ Some FromCheckRow; Some FromCheckPanel ] do
                Expect.equal (targetCallLostToast origin false true false "weg") (toast true) $"{origin}: Prüfung lädt neu"
                Expect.equal (targetCallLostToast origin false false true "weg") (toast false) $"{origin}: nur der Monat, kein Versprechen"
            for origin in [ Some FromTargetTab; None ] do
                Expect.equal (targetCallLostToast origin false false true "weg") (toast true) $"{origin}: Monat liest neu"
                Expect.equal (targetCallLostToast origin false true false "weg") (toast true) $"{origin}: Prüfung lädt neu"
                Expect.equal (targetCallLostToast origin false false false "weg") (toast false) $"{origin}: nichts liest neu"
        }
    ]

// ============================================
// Übernehmen
// ============================================

let private takeTests =
    testList "Targets prüfen: übernehmen" [
        test "Der Auftrag: mit Target das gelesene als Shown, der Vorschlag im Rhythmus mit Datum und KeepRhythm, ohne Target monatlich" {
            let row id = checkGroups checkData |> List.collect snd |> List.find (fun r -> r.CategoryId = id)
            Expect.equal
                (takeRequest checkData (row moreId))
                (Some (current, { CategoryId = moreId; Shown = Some moreTarget; Wanted = Some { NewGoal = eur 150m; NewDate = Some "2027-03-01"; NewRhythm = KeepRhythm; NewDay = None }; CommandId = None }))
                "mehr"
            Expect.equal
                (takeRequest checkData (row lessId) |> Option.bind (fun (_, r) -> r.Wanted))
                (Some { NewGoal = eur 23m; NewDate = None; NewRhythm = KeepRhythm; NewDay = Some 1 })
                "weniger, wöchentlich auf volle Euro"
            Expect.equal
                (takeRequest checkData (row withoutId))
                (Some (current, { CategoryId = withoutId; Shown = None; Wanted = Some { NewGoal = eur 40m; NewDate = None; NewRhythm = SetRhythm Monthly; NewDay = None }; CommandId = None }))
                "Kein Target"
            Expect.isNone (takeRequest checkData (row neverId)) "nie"
            Expect.isNone (takeRequest checkData (row fitsId)) "Passt"
            Expect.equal (takeText (row lessId)) (Some "23,00 €/Woche übernehmen") "Beschriftung im Rhythmus"
            Expect.equal (takeText (row withoutId)) (Some "40,00 €/Monat übernehmen") "ohne Target monatlich"
        }
        test "Übernehmen sendet einmal, der Spinner steht nur an der Zeile, alle anderen übernehmen sind gesperrt" {
            let running, cmd, _ = update (TakeSuggestion moreId) (loaded ())
            Expect.isNonEmpty cmd "gesendet"
            Expect.equal running.TargetCall (TargetSaving (moreId, current, SavingTarget, FromCheckRow)) "läuft"
            Expect.isFalse (sends (TakeSuggestion moreId) running) "zweiter Tipp"
            Expect.isFalse (sends (TakeSuggestion lessId) running) "andere Zeile"
            Expect.isFalse (sends (TakeSuggestion neverId) (loaded ())) "ohne Vorschlag nichts"
        }
        test "Die Knöpfe je Gruppe und Lage: übernehmen nur mit Vorschlag, gesperrt solange ein Aufruf oder eine Pause läuft" {
            let buttons (model: Model) = [ for id in [ moreId; lessId; fitsId; withoutId; neverId ] -> takeButton model (dataOf model) (rowIn model id) ]
            let free = Some { Busy = false; Disabled = false }
            let locked = Some { Busy = false; Disabled = true }
            Expect.equal (buttons (loaded ())) [ free; free; None; free; None ] "frei"
            let byRow = loaded () |> step (TakeSuggestion moreId)
            Expect.equal (buttons byRow) [ Some { Busy = true; Disabled = false }; locked; None; locked; None ] "aus der Zeile"
            let byPanel = loaded () |> step (AdjustTarget moreId) |> step SaveCheckTarget
            Expect.equal byPanel.TargetCall (TargetSaving (moreId, current, SavingTarget, FromCheckPanel)) "aus dem Panel"
            Expect.equal (buttons byPanel) [ locked; locked; None; locked; None ] "der Spinner steht im Panel"
            let paused = { loaded () with PauseCall = PauseRunning (moreId, current, 0) }
            Expect.equal (buttons paused) [ locked; free; None; free; None ] "Pause derselben Kategorie"
            Expect.isFalse (sends (TakeSuggestion moreId) paused) "sendet nicht"
        }
        test "TargetWritten setzt die Zeile nach Passt mit neu gesetzt am heute und das Target im geladenen Monat" {
            let written = { moreTarget with Goal = eur 150m }
            let after, _, external = loaded () |> step (TakeSuggestion moreId) |> answered moreId (Ok (TargetWritten (Some written)))
            Expect.equal ((rowIn after moreId).Group, (rowIn after moreId).Note) (Fits, Some (RecentlySet today)) "Passt, neu gesetzt"
            Expect.equal (fitsNoteText (RecentlySet today)) "neu gesetzt am 06.11.2026" "der Satz"
            Expect.equal (budgetTarget after moreId) (Some written) "im geladenen Monat"
            Expect.equal (groupOf after lessId) SpendsLess "andere Zeilen bleiben"
            Expect.equal (after.TargetCall, external) (TargetIdle, NoOp) "frei, kein Toast"
        }
        test "TargetChangedMeanwhile ordnet mit BBs Stand neu ein und meldet per Warn-Toast" {
            let bbs = monthly 150m
            let after, _, external = loaded () |> step (TakeSuggestion moreId) |> answered moreId (Ok (TargetChangedMeanwhile (Some bbs)))
            Expect.equal ((rowIn after moreId).Target, (rowIn after moreId).Group, (rowIn after moreId).Note) (Some bbs, Fits, None) "BBs Stand, ohne neue Änderung"
            Expect.equal (budgetTarget after moreId) (Some bbs) "im geladenen Monat"
            Expect.equal external (ShowToast (targetRejectedAwayText TargetChangedMeanwhileText, ToastWarning)) "Warn-Toast"
        }
        test "Error lässt die Zeile stehen und meldet Target nicht gespeichert mit Grund" {
            let after, _, external = loaded () |> step (TakeSuggestion moreId) |> answered moreId (Error "Kategorie gelöscht")
            Expect.equal (groupOf after moreId) SpendsMore "unverändert"
            Expect.equal external (ShowToast ("Target nicht gespeichert: Kategorie gelöscht", ToastError)) "Fehler-Toast"
            Expect.equal after.TargetCall TargetIdle "frei"
        }
        test "Ein abgebrochener Aufruf meldet und lädt die Prüfung still neu, die Liste bleibt stehen" {
            let running = loaded () |> step (TakeSuggestion moreId)
            let lost, cmd, external = update (TargetChangeCallLost (moreId, current, "Http error (0)")) running
            Expect.equal external (ShowToast (targetCallLostText true "Http error (0)", ToastError)) "Fehler-Toast"
            Expect.isGreaterThan lost.TargetCheck.Generation running.TargetCheck.Generation "lädt neu"
            Expect.equal lost.TargetCheck.Data running.TargetCheck.Data "ohne Loading"
            Expect.isNonEmpty cmd "Aufruf"
            let reloaded = lost |> step (TargetCheckReloaded (lost.TargetCheck.Generation, Ok { checkData with Categories = [] }))
            Expect.equal (dataOf reloaded).Categories [] "die neue Antwort steht"
            let failed, _, failedToast = update (TargetCheckReloaded (lost.TargetCheck.Generation, Error "weg")) lost
            Expect.equal failed.TargetCheck.Data running.TargetCheck.Data "ein Fehler lässt die Liste stehen"
            // Review zu budgeting-q4v8n: ein gescheitertes stilles Neuladen blieb ohne jede Anzeige.
            Expect.equal failedToast (ShowToast ("Prüfung nicht neu gelesen, sie kann veraltet sein: weg", ToastWarning)) "Warn-Toast mit Grund"
            let _, _, staleToast = update (TargetCheckReloaded (lost.TargetCheck.Generation - 1, Error "alt")) lost
            Expect.equal staleToast NoOp "ein überholter Fehler meldet nichts"
        }
        // Review zu budgeting-q4v8n: ein stilles Neuladen, das vor einer Schreib-Antwort startete, ersetzte mit
        // seinem älteren Stand die Rohdaten danach. Ebenso das Laden beim Betreten, das noch lief.
        test "Kommt eine Schreib-Antwort, während die Prüfung lädt, lädt sie neu und die ältere Antwort ersetzt nichts" {
            let writtenLess = Ok (TargetWritten (Some { lessTarget with Goal = eur 23m }))
            // GIVEN Abbruch A lädt still neu, dann übernehmen B. WHEN Antwort B, danach die alte Antwort des Neuladens.
            let lostA = loaded () |> step (TakeSuggestion moreId) |> step (TargetChangeCallLost (moreId, current, "weg"))
            let stale = lostA.TargetCheck.Generation
            let answeredB, cmd, _ = lostA |> step (TakeSuggestion lessId) |> answered lessId writtenLess
            Expect.equal (groupOf answeredB lessId) Fits "B steht bei Passt"
            Expect.isGreaterThan answeredB.TargetCheck.Generation stale "lädt neu"
            let afterStale = answeredB |> step (TargetCheckReloaded (stale, Ok checkData))
            Expect.equal (groupOf afterStale lessId) Fits "die ältere Antwort ersetzt B nicht"
            // intakt: ohne offenes Laden lädt eine Antwort nicht neu.
            let plain, plainCmd, _ = loaded () |> step (TakeSuggestion lessId) |> answered lessId writtenLess
            Expect.equal plain.TargetCheck.Generation (loaded ()).TargetCheck.Generation "ohne offenes Laden kein Neuladen"
            Expect.isGreaterThan (List.length cmd) (List.length plainCmd) "mit offenem Laden ein Aufruf mehr"
            // Beim Betreten: die Seite lädt noch, als Antwort B kommt.
            let entered = loaded () |> step (TakeSuggestion lessId) |> step TargetCheckEntered
            let answeredLoading, _, _ = entered |> answered lessId writtenLess
            Expect.isGreaterThan answeredLoading.TargetCheck.Generation entered.TargetCheck.Generation "lädt beim Betreten neu"
            let afterStaleLoad = answeredLoading |> step (TargetCheckLoaded (entered.TargetCheck.Generation, Ok checkData))
            Expect.equal afterStaleLoad.TargetCheck.Data Loading "die ältere Antwort beim Betreten zählt nicht"
        }
        // Review zu budgeting-q4v8n: der Satz versprach das Neuladen auch, wenn die Prüfung nicht neu lud.
        test "Lädt die Prüfung nach einem Abbruch nicht neu, verspricht der Satz in Toast und Panel kein Neuladen" {
            let failedRow = loaded () |> step (TakeSuggestion moreId) |> step TargetCheckEntered |> answer (Error "kaputt")
            let lost, _, external = update (TargetChangeCallLost (moreId, current, "weg")) failedRow
            Expect.equal external (ShowToast (targetCallLostText false "weg", ToastError)) "Toast der Zeile"
            Expect.equal lost.TargetCheck.Generation failedRow.TargetCheck.Generation "lädt nicht"
            let failedPanel =
                loaded () |> step (AdjustTarget moreId) |> step SaveCheckTarget |> step RetryTargetCheck |> answer (Error "kaputt")
            let lostPanel = failedPanel |> step (TargetChangeCallLost (moreId, current, "weg"))
            Expect.equal (panelOf lostPanel).Notice (Some (TargetRejectedNotice (targetCallLostText false "weg"))) "Satz im Panel"
        }
    ]

// ============================================
// Anpassen … (Panel)
// ============================================

let private panelTests =
    testList "Targets prüfen: Anpassen" [
        test "Anpassen öffnet mit dem Vorschlag im Rhythmus, ohne Vorschlag mit dem gelesenen Target, ohne Target monatlich" {
            let more = panelOf (loaded () |> step (AdjustTarget moreId))
            Expect.equal (more.CategoryId, more.Month, more.AmountInput, more.RhythmInput) (moreId, current, Some "150,00", None) "mehr"
            Expect.equal (panelOf (loaded () |> step (AdjustTarget lessId))).AmountInput (Some "23,00") "im Rhythmus"
            Expect.equal (panelOf (loaded () |> step (AdjustTarget withoutId))).RhythmInput (Some Monthly) "ohne Target monatlich"
            Expect.equal (panelOf (loaded () |> step (AdjustTarget neverId))) (emptyTargetForm neverId current) "nie: ohne Eingabe"
            Expect.equal (panelOf (loaded () |> step (AdjustTarget fitsId))) (emptyTargetForm fitsId current) "Passt: ohne Eingabe"
            Expect.isNone (loaded ()).TargetCheck.Panel "ohne Anpassen zu"
        }
        test "Abbrechen schließt ohne Aufruf, Anpassen einer anderen Zeile ersetzt den Inhalt" {
            let typed = loaded () |> step (AdjustTarget moreId) |> step (CheckAmountChanged "170")
            let closed, cmd, _ = update CloseCheckPanel typed
            Expect.isNone closed.TargetCheck.Panel "zu"
            Expect.isEmpty cmd "kein Aufruf"
            Expect.equal (panelOf (typed |> step (AdjustTarget fitsId))) (emptyTargetForm fitsId current) "ersetzt, Eingabe verworfen"
        }
        test "Speichern sendet mit dem Target der Zeile, TargetWritten schließt das Panel und setzt die Zeile nach Passt" {
            let opened = loaded () |> step (AdjustTarget moreId)
            match startCheckSave opened with
            | _, Some (month, request) ->
                Expect.equal (month, request.Shown, request.Wanted |> Option.map (fun w -> w.NewGoal)) (current, Some moreTarget, Some (eur 150m)) "Auftrag"
            | _, None -> failtest "Auftrag erwartet"
            let after, _, external = opened |> step SaveCheckTarget |> answered moreId (Ok (TargetWritten (Some (monthly 150m))))
            Expect.isNone after.TargetCheck.Panel "zu"
            Expect.equal (rowIn after moreId).Note (Some (RecentlySet today)) "Passt, neu gesetzt"
            Expect.equal external NoOp "kein Toast"
        }
        test "Entfernen mit TargetWritten None setzt entfernt am heute" {
            let asked = loaded () |> step (AdjustTarget fitsId) |> step AskRemoveCheckTarget
            Expect.isTrue (panelOf asked).ConfirmingRemove "Bestätigung offen"
            let removing, cmd, _ = update ConfirmRemoveCheckTarget asked
            Expect.isNonEmpty cmd "gesendet"
            Expect.equal removing.TargetCall (TargetSaving (fitsId, current, RemovingTarget, FromCheckPanel)) "entfernen läuft"
            let after, _, _ = removing |> answered fitsId (Ok (TargetWritten None))
            Expect.equal ((rowIn after fitsId).Note, (rowIn after fitsId).Target) (Some (RecentlyRemoved today), None) "entfernt"
            Expect.equal (fitsNoteText (RecentlyRemoved today)) "entfernt am 06.11.2026" "der Satz"
            Expect.isNone after.TargetCheck.Panel "zu"
        }
        test "TargetChangedMeanwhile, Error und Abbruch lassen das Panel offen mit Hinweis und behalten die Eingabe" {
            let running = loaded () |> step (AdjustTarget moreId) |> step (CheckAmountChanged "170") |> step SaveCheckTarget
            let meanwhile, _, external = running |> answered moreId (Ok (TargetChangedMeanwhile (Some (monthly 120m))))
            Expect.equal ((panelOf meanwhile).AmountInput, (panelOf meanwhile).Notice) (Some "170", Some TargetChangedMeanwhileNotice) "gelber Hinweis"
            Expect.equal (rowIn meanwhile moreId).Target (Some (monthly 120m)) "BBs Stand in der Zeile"
            Expect.equal (fst (panelRow meanwhile |> Option.get)).Target (Some (monthly 120m)) "und im Formular"
            Expect.equal external NoOp "kein Toast"
            let failed, _, external = running |> answered moreId (Error "Kategorie gelöscht")
            Expect.equal ((panelOf failed).AmountInput, (panelOf failed).Notice) (Some "170", Some (TargetRejectedNotice "Kategorie gelöscht")) "Meldung"
            Expect.equal external NoOp "kein Toast"
            let lost, cmd, external = update (TargetChangeCallLost (moreId, current, "weg")) running
            Expect.equal ((panelOf lost).AmountInput, (panelOf lost).Notice) (Some "170", Some (TargetRejectedNotice (targetCallLostText true "weg"))) "Satz"
            Expect.equal external NoOp "kein Toast"
            Expect.isGreaterThan lost.TargetCheck.Generation running.TargetCheck.Generation "lädt still neu"
            Expect.isNonEmpty cmd "Aufruf"
        }
        // Review zu budgeting-q4v8n: Abgelehnt nach geschlossenem Panel meldet derselbe Fehler-Toast wie übernehmen.
        test "Kommt die Ablehnung, nachdem das Panel während des Aufrufs geschlossen wurde, meldet ein Fehler-Toast" {
            let after, _, external =
                loaded () |> step (AdjustTarget moreId) |> step SaveCheckTarget |> step CloseCheckPanel
                |> answered moreId (Error "Kategorie gelöscht")
            Expect.equal external (ShowToast ("Target nicht gespeichert: Kategorie gelöscht", ToastError)) "Fehler-Toast"
            Expect.isNone after.TargetCheck.Panel "bleibt zu"
        }
        // Review zu budgeting-q4v8n: ein neuer Monat in den Rohdaten ließ das Panel unsichtbar offen.
        test "Bringt Laden oder stilles Neuladen einen anderen Monat, schließt das Panel, beim selben Monat bleibt es offen" {
            let december = { checkData with Month = "2026-12-01" }
            let opened = loaded () |> step (AdjustTarget moreId)
            Expect.isNone (opened |> step RetryTargetCheck |> answer (Ok december)).TargetCheck.Panel "Laden"
            Expect.isSome (opened |> step RetryTargetCheck |> answer (Ok checkData)).TargetCheck.Panel "Laden, selber Monat"
            let lost = opened |> step SaveCheckTarget |> step (TargetChangeCallLost (moreId, current, "weg"))
            let reload data = lost |> step (TargetCheckReloaded (lost.TargetCheck.Generation, Ok data))
            Expect.isNone (reload december).TargetCheck.Panel "stilles Neuladen"
            Expect.isSome (reload checkData).TargetCheck.Panel "stilles Neuladen, selber Monat"
        }
        test "Solange übernehmen läuft, wartet Speichern im Panel einer anderen Kategorie mit Satz, Anpassen und Abbrechen gehen" {
            let running = loaded () |> step (TakeSuggestion lessId) |> step (AdjustTarget moreId)
            let row, form = panelRow running |> Option.get
            let locks = panelLocks running form
            Expect.isFalse (formCanSave row.Target locks form) "Speichern wartet"
            Expect.equal (formSaveWaitHint row.Target locks form) (Some "Wartet, bis das Target bei Kantine gespeichert ist.") "Satz"
            Expect.isFalse (sends SaveCheckTarget running) "sendet nicht"
            Expect.isNone (running |> step CloseCheckPanel).TargetCheck.Panel "Abbrechen geht"
        }
    ]

// ============================================
// Herkunft
// ============================================

/// Der Tab Target der Kategorie "mehr" im laufenden Monat mit der Eingabe 60.
let private tabTyped () : Model =
    { page with Tab = TargetTab } |> step (SelectCategory moreId) |> step (TargetAmountChanged "60") |> checkedPage

let private originTests =
    testList "Targets prüfen: Herkunft" [
        test "Eine Antwort auf übernehmen oder das Panel ändert das Tab-Formular nicht, das Target setzen beide" {
            let tabForm (model: Model) = model.TargetForm |> Option.map (fun f -> f.AmountInput, f.Notice)
            for name, start in [ "Zeile", step (TakeSuggestion moreId); "Panel", (step (AdjustTarget moreId) >> step SaveCheckTarget) ] do
                let running = tabTyped () |> start
                for result in [ Ok (TargetWritten (Some (monthly 150m))); Ok (TargetChangedMeanwhile (Some (monthly 150m))); Error "nein" ] do
                    let after, _, _ = running |> answered moreId result
                    Expect.equal (tabForm after) (Some (Some "60", None)) $"{name}, {result}: Tab unverändert"
                    match result with
                    | Ok _ -> Expect.equal (budgetTarget after moreId) (Some (monthly 150m)) $"{name}, {result}: Target im Monat"
                    | Error _ -> ()
                let lost = running |> step (TargetChangeCallLost (moreId, current, "weg"))
                Expect.equal (tabForm lost) (Some (Some "60", None)) $"{name}, Abbruch: Tab unverändert"
        }
        test "Eine Antwort auf einen Aufruf aus dem Tab ändert das Panel nicht, das Target in der Prüfung setzt sie" {
            let running =
                tabTyped () |> step (AdjustTarget moreId) |> step (CheckAmountChanged "170") |> step SaveTarget
            Expect.equal running.TargetCall (TargetSaving (moreId, current, SavingTarget, FromTargetTab)) "aus dem Tab"
            let locks = panelLocks running (panelOf running)
            Expect.equal (locks.Running, formFieldsLocked locks) (None, false) "im Panel dreht nichts"
            let after, _, _ = running |> answered moreId (Ok (TargetWritten (Some (monthly 60m))))
            Expect.equal ((panelOf after).AmountInput, (panelOf after).Notice) (Some "170", None) "Panel unverändert"
            Expect.equal (rowIn after moreId).Target (Some (monthly 60m)) "Target in der Prüfung"
            let failed, _, _ = running |> answered moreId (Error "nein")
            Expect.equal (panelOf failed).Notice None "auch ein Fehler nicht"
            let lost = running |> step (TargetChangeCallLost (moreId, current, "weg"))
            Expect.equal ((panelOf lost).AmountInput, (panelOf lost).Notice) (Some "170", None) "auch ein Abbruch nicht"
        }
        test "Zeigt die Budget-Seite einen anderen Monat, dreht im Panel der eigene Knopf und die Felder sind gesperrt" {
            let elsewhere = { page with ChosenMonth = Some past; Month = Success (budgetMonth past) } |> checkedPage
            let running = elsewhere |> step (AdjustTarget moreId) |> step SaveCheckTarget
            let row, form = panelRow running |> Option.get
            let locks = panelLocks running form
            Expect.equal locks.Running (Some SavingTarget) "eigener Aufruf"
            Expect.isTrue (formFieldsLocked locks) "Felder gesperrt"
            Expect.equal (formSaveButton row.Target locks form).Busy true "der Knopf dreht"
            Expect.equal (panelOf (running |> step (CheckAmountChanged "1"))).AmountInput (Some "150,00") "Tippen ändert nichts"
        }
    ]

// ============================================
// Reihenfolgen (Doktrin Punkt 20)
// ============================================

/// Was die Aufzählung über den Zustand weiß, unabhängig vom getesteten Code.
type private Walk = {
    /// Kategorie und Herkunft des laufenden Aufrufs.
    Pending: (Guid * TargetOrigin) option
    /// Kategorien, deren Target festgehalten ist: sie stehen bei Passt, ohne Vorschlag.
    Written: Set<Guid>
    /// Die Kategorie des offenen Panels.
    Panel: Guid option
}

let private written (id: Guid) : Target =
    if id = moreId then { moreTarget with Goal = eur 150m } else { lessTarget with Goal = eur 23m }

let private walkEvents (walk: Walk) : (string * Walk) list =
    let take id =
        match walk.Pending with
        | None when not (walk.Written.Contains id) -> { walk with Pending = Some (id, FromCheckRow) }
        | _ -> walk
    [ "übernehmen A", take moreId
      "übernehmen B", take lessId
      "Anpassen B mit Speichern",
      (match walk.Pending with
       | None when not (walk.Written.Contains lessId) -> { walk with Pending = Some (lessId, FromCheckPanel); Panel = Some lessId }
       | _ -> { walk with Panel = Some lessId })
      "Abbrechen", { walk with Panel = None }
      for id, name in [ moreId, "Antwort A"; lessId, "Antwort B" ] do
          match walk.Pending with
          | Some (pending, origin) when pending = id ->
              name,
              { walk with
                  Pending = None
                  Written = walk.Written.Add id
                  Panel = if origin = FromCheckPanel && walk.Panel = Some id then None else walk.Panel }
          | _ -> () ]

let private walkTests =
    let start = { Pending = None; Written = Set.empty; Panel = None }
    let rec walkFrom (depth: int) (path: string list) (walk: Walk) (model: Model) : unit =
        if depth > 0 then
            for name, after in walkEvents walk do
                let route = List.rev (name :: path) |> String.concat ", "
                let model', cmd, _ =
                    match name with
                    | "übernehmen A" -> update (TakeSuggestion moreId) model
                    | "übernehmen B" -> update (TakeSuggestion lessId) model
                    | "Anpassen B mit Speichern" -> model |> step (AdjustTarget lessId) |> update SaveCheckTarget
                    | "Abbrechen" -> update CloseCheckPanel model
                    | "Antwort A" -> answered moreId (Ok (TargetWritten (Some (written moreId)))) model
                    | _ -> answered lessId (Ok (TargetWritten (Some (written lessId)))) model
                let started = after.Pending.IsSome && after.Pending <> walk.Pending
                Expect.equal (not (List.isEmpty cmd)) (started || (name.StartsWith "Antwort")) $"{route}: gesendet, nur wenn nichts läuft und kein zweiter Aufruf für dieselbe Kategorie"
                let call =
                    match model'.TargetCall with
                    | TargetSaving (c, m, _, origin) -> Some (c, origin)
                    | TargetIdle -> None
                Expect.equal call after.Pending $"{route}: höchstens ein Aufruf, nach der Antwort keiner"
                for id, open' in [ moreId, SpendsMore; lessId, SpendsLess ] do
                    let expected = if after.Written.Contains id then Fits else open'
                    Expect.equal (groupOf model' id) expected $"{route}: jede Antwort ändert nur ihre Kategorie ({id})"
                Expect.equal (model'.TargetCheck.Panel |> Option.map (fun f -> f.CategoryId)) after.Panel $"{route}: Panel"
                walkFrom (depth - 1) (name :: path) after model'
    testList "Targets prüfen: Reihenfolgen" [
        test "In jeder Reihenfolge läuft höchstens ein Aufruf, keiner doppelt, jede Antwort wirkt nur auf ihre Kategorie" {
            walkFrom 6 [] start (loaded ())
        }
    ]

[<Tests>]
let tests = testList "Targets prüfen" [ pageTests; takeTests; panelTests; originTests; walkTests ]
