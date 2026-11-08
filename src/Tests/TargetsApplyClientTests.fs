module Tests.TargetsApplyClientTests

// budgeting-h2zfn: Zustandsübergänge des Dialogs "Targets anwenden" auf der Budget-Seite.
// Der Aufruf an den Server (Fable.Remoting-Proxy) ist unter .NET nicht testbar; die
// Übergänge liegen deshalb als reine Funktionen in `Components.Budget`.

open System
open Expecto
open Shared.Domain
open Shared.Budgeting
open Types
open Components.Budget.Types
open Components.Budget.Month
open Components.Budget.Targets
open Components.Budget.Moves
open Components.Budget.Inspector
open Client.DesignSystem

let private eur (amount: decimal) : Money = { Amount = amount; Currency = "EUR" }

let private line name (oldA: decimal) (under: decimal) : TargetLine =
    { CategoryId = Guid.NewGuid()
      CategoryName = name
      GroupName = "Alltag"
      OldAssigned = eur oldA
      Underfunded = eur under
      NewAssigned = eur (oldA + under)
      Target = None }

let private previewOf (after: decimal) (lines: TargetLine list) : TargetPreview =
    { Month = "2026-11-01"
      Lines = lines
      Skipped = []
      Total = eur (lines |> List.sumBy (fun l -> l.Underfunded.Amount))
      ToBeBudgeted = eur 100m
      ToBeBudgetedAfter = eur after
      CommandId = None }

let private month : BudgetMonth = { Month = "2026-11-01"; ToBeBudgeted = eur 100m; Groups = [] }
let private frame : BudgetFrame =
    { CurrentMonth = "2026-11-01"; Months = [ "2026-10-01"; "2026-11-01" ]; CategoryOrder = []; MonthStartHidden = [] }
let private loaded = { init () with Frame = Success frame; Month = Success month }
let private preview = previewOf 50m [ line "Essen" 0m 30m; line "Kleidung" 20m 20m ]

let private reportOf (outcomes: LineOutcome list) (actual: Money option) : ApplyReport =
    { ApplicationId = Guid.NewGuid().ToString()
      Month = "2026-11-01"
      Lines =
        outcomes
        |> List.mapi (fun i o ->
            { CategoryId = Guid.NewGuid()
              CategoryName = $"K{i}"
              OldAssigned = eur 0m
              NewAssigned = eur 10m
              Outcome = o })
      ActualToBeBudgeted = actual }

let private openTests =
    testList "Dialog öffnen (h2zfn)" [
        test "Mit geladenem Monat öffnet der Dialog und verlangt die Vorschau" {
            let m, load = openTargets loaded
            Expect.equal m.Targets PreviewLoading "lädt"
            Expect.isTrue load "Vorschau holen"
        }
        test "Ohne geladenen Monat oder bei offenem Dialog passiert nichts" {
            let _, load1 = openTargets (init ())
            Expect.isFalse load1 "Monat nicht geladen"
            let open' = { loaded with Targets = PreviewReady { Shown = preview; Changed = false; Excluded = Set.empty } }
            let m, load2 = openTargets open'
            Expect.equal m open' "unverändert"
            Expect.isFalse load2 "kein zweites Laden"
        }
        test "In einem vergangenen Monat öffnet der Dialog nicht und holt keine Vorschau" {
            let lastMonth = { loaded with ChosenMonth = Some "2026-10-01" }
            let m, load = openTargets lastMonth
            Expect.equal m lastMonth "unverändert"
            Expect.isFalse load "keine Vorschau"
        }
        // Bug F1: "Wiederholen" im Fehlerdialog schickte OpenTargets, das nur bei geschlossenem
        // Dialog griff. Der Knopf tat nichts.
        test "Wiederholen nach gescheiterter Vorschau holt die Vorschau neu" {
            let failed = { loaded with Targets = PreviewFailed "kaputt" }
            let m, load = openTargets failed
            Expect.equal m.Targets PreviewLoading "lädt"
            Expect.isTrue load "Vorschau holen"
        }
        test "In einem vergangenen Monat holt auch Wiederholen keine Vorschau" {
            let failed = { loaded with ChosenMonth = Some "2026-10-01"; Targets = PreviewFailed "kaputt" }
            let m, load = openTargets failed
            Expect.equal m failed "unverändert"
            Expect.isFalse load "keine Vorschau"
        }
        test "Die Vorschau wird eingenommen, eine späte Antwort zu anderem Monat oder geschlossenem Dialog nicht" {
            let waiting = { loaded with Targets = PreviewLoading }
            Expect.equal (applyPreviewLoaded "2026-11-01" 0 (Ok preview) waiting).Targets (PreviewReady { Shown = preview; Changed = false; Excluded = Set.empty }) "eingenommen"
            Expect.equal (applyPreviewLoaded "2026-12-01" 0 (Ok preview) waiting) waiting "anderer Monat"
            Expect.equal (applyPreviewLoaded "2026-11-01" 0 (Ok preview) loaded) loaded "Dialog geschlossen"
            Expect.equal (applyPreviewLoaded "2026-11-01" 0 (Error "kaputt") waiting).Targets (PreviewFailed "kaputt") "Fehler"
        }
    ]

let private confirmTests =
    testList "Übernehmen (h2zfn)" [
        test "Übernehmen schickt die gezeigte Vorschau und sperrt weitere Klicks" {
            let ready = { loaded with Targets = PreviewReady { Shown = preview; Changed = false; Excluded = Set.empty } }
            let m, sent = confirmTargets ready
            Expect.equal sent (Some preview) "gezeigte Vorschau geht an den Server"
            Expect.equal m.Targets (Applying { Shown = preview; Changed = false; Excluded = Set.empty }) "läuft"
            let _, again = confirmTargets m
            Expect.isNone again "kein zweiter Aufruf während des Laufs"
        }
        test "Ein negatives Zu verteilen danach sperrt Übernehmen nicht" {
            let negative = { previewOf -20m [ line "Essen" 0m 30m ] with ToBeBudgeted = eur 10m }
            let _, sent = confirmTargets { loaded with Targets = PreviewReady { Shown = negative; Changed = false; Excluded = Set.empty } }
            Expect.equal sent (Some negative) "bleibt aktiv"
        }
        test "Ohne Zeilen gibt es nichts zu übernehmen" {
            let empty = previewOf 100m []
            let _, sent = confirmTargets { loaded with Targets = PreviewReady { Shown = empty; Changed = false; Excluded = Set.empty } }
            Expect.isNone sent "kein Aufruf"
        }
        test "Hat sich YNABs Stand geändert, zeigt der Dialog die neue Vorschau mit Hinweis" {
            let applying = { loaded with Targets = Applying { Shown = preview; Changed = false; Excluded = Set.empty } }
            let fresh = previewOf 40m [ line "Essen" 10m 30m ]
            let m = applyApplied (Ok (PreviewChanged fresh)) applying
            Expect.equal m.Targets (PreviewReady { Shown = fresh; Changed = true; Excluded = Set.empty }) "neue Vorschau, changed = true"
        }
        test "Der Ausgang wird gezeigt, ein Aufruffehler bleibt sichtbar" {
            let applying = { loaded with Targets = Applying { Shown = preview; Changed = false; Excluded = Set.empty } }
            let report = reportOf [ Written; Written ] (Some preview.ToBeBudgetedAfter)
            Expect.equal (applyApplied (Ok (Applied report)) applying).Targets (ApplyDone { Sent = preview; Report = report; Retrying = false; RetryError = None }) "fertig"
            Expect.equal (applyApplied (Error "Netz weg") applying).Targets (ApplyFailed (preview, "Netz weg")) "Fehler"
        }
    ]

let private retryTests =
    testList "Erneut senden und Schließen (h2zfn)" [
        test "Erneut senden nennt die Anwendung und läuft nur, wenn etwas fehlgeschlagen ist" {
            let failed = reportOf [ Written; Failed "Rate-Limit" ] None
            let done' = { loaded with Targets = ApplyDone { Sent = preview; Report = failed; Retrying = false; RetryError = None } }
            let m, id = retryFailed done'
            Expect.equal id (Some failed.ApplicationId) "Anwendung"
            Expect.equal m.Targets (ApplyDone { Sent = preview; Report = failed; Retrying = true; RetryError = None }) "läuft"
            let _, again = retryFailed m
            Expect.isNone again "nicht doppelt"
            let ok = reportOf [ Written ] None
            let _, none = retryFailed { loaded with Targets = ApplyDone { Sent = preview; Report = ok; Retrying = false; RetryError = None } }
            Expect.isNone none "nichts Fehlgeschlagenes"
        }
        // budgeting-my8p8: Erneut senden geht auch hinaus, wenn der Monat der Anwendung inzwischen vergangen ist.
        test "Im Folgemonat nennt erneut senden die Anwendung des Vormonats" {
            let december = { frame with CurrentMonth = "2026-12-01"; Months = [ "2026-11-01"; "2026-12-01" ] }
            let failed = reportOf [ Written; Failed "Rate-Limit" ] None
            let pastMonth =
                { loaded with
                    Frame = Success december
                    ChosenMonth = Some "2026-11-01"
                    Targets = ApplyDone { Sent = preview; Report = failed; Retrying = false; RetryError = None } }
            Expect.equal (displayedMonthKind pastMonth) (Some PastMonth) "der November ist vergangen"
            let _, id = retryFailed pastMonth
            Expect.equal id (Some failed.ApplicationId) "Anwendung"
        }
        test "Das Ergebnis des erneuten Sendens ersetzt den Bericht, ein Fehler lässt ihn stehen" {
            let failed = reportOf [ Failed "Rate-Limit" ] None
            let running = { loaded with Targets = ApplyDone { Sent = preview; Report = failed; Retrying = true; RetryError = None } }
            let better = { failed with Lines = failed.Lines |> List.map (fun l -> { l with Outcome = Written }) }
            Expect.equal (applyRetried (Ok better) running).Targets (ApplyDone { Sent = preview; Report = better; Retrying = false; RetryError = None }) "neuer Bericht"
            Expect.equal (applyRetried (Error "weg") running).Targets (ApplyDone { Sent = preview; Report = failed; Retrying = false; RetryError = Some "weg" }) "alter Bericht, Fehler"
        }
        test "Schließen nach einer Anwendung lädt den Monat neu, davor nicht" {
            let report = reportOf [ Written ] None
            let _, reload = closeTargets { loaded with Targets = ApplyDone { Sent = preview; Report = report; Retrying = false; RetryError = None } }
            Expect.isSome reload "nach Anwendung neu laden"
            let _, reloadFailed = closeTargets { loaded with Targets = ApplyFailed (preview, "x") }
            Expect.isSome reloadFailed "auch nach unklarem Ausgang"
            let m, reloadPreview = closeTargets { loaded with Targets = PreviewReady { Shown = preview; Changed = false; Excluded = Set.empty } }
            Expect.equal m.Targets TargetsClosed "geschlossen"
            Expect.isNone reloadPreview "Abbrechen schreibt nichts, kein Neuladen"
        }
        test "Während des Schreibens lässt sich der Dialog nicht schließen" {
            let applying = { loaded with Targets = Applying { Shown = preview; Changed = false; Excluded = Set.empty } }
            Expect.equal (closeTargets applying) (applying, None) "Applying bleibt"
            let report = reportOf [ Failed "x" ] None
            let retrying = { loaded with Targets = ApplyDone { Sent = preview; Report = report; Retrying = true; RetryError = None } }
            Expect.equal (closeTargets retrying) (retrying, None) "erneutes Senden bleibt"
        }
        // budgeting-r7t3k: Schließen nach einem Lauf setzte `Month = Loading`. Liste und Inspektor
        // verschwanden kurz, Fokus und Scrollposition gingen verloren.
        test "Schließen nach einem Lauf lässt den Monat stehen und liest ihn still neu, die Antwort setzt die neuen Beträge" {
            let report = reportOf [ Written ] None
            let applied = { loaded with Targets = ApplyDone { Sent = preview; Report = report; Retrying = false; RetryError = None } }
            let closed, cmd, _ = Components.Budget.State.update CloseTargets applied
            Expect.equal closed.Targets TargetsClosed "Dialog zu"
            Expect.equal closed.Month (Success month) "der alte Stand bleibt"
            Expect.equal closed.Freshness MonthStale "bis zur Antwort veraltet"
            Expect.isGreaterThan closed.MonthGeneration applied.MonthGeneration "eine neue Abfrage"
            Expect.equal (List.length cmd) 1 "das Neu-Lesen ist unterwegs"
            Expect.equal (rereadMonth applied |> snd) QuietReread "still, über Reloaded"
            let fresh = { month with ToBeBudgeted = eur 50m }
            let answered, _, external = Components.Budget.State.update (Reloaded (closed.MonthGeneration, Ok fresh)) closed
            Expect.equal answered.Month (Success fresh) "neue Beträge"
            Expect.equal answered.Freshness MonthRead "gelesen"
            Expect.equal external NoOp "kein Hinweis"
        }
        // budgeting-r7t3k: Die Liste bleibt jetzt stehen. Ohne die geschriebenen Zeilen rechnete ein
        // Schritt im Feld bis zur Antwort vom Stand vor dem Lauf und nähme ihn zurück.
        test "Schließen nach einem Lauf trägt die geschriebenen Zeilen ein, die fehlgeschlagenen nicht" {
            let essen = Guid.Parse "00000000-0000-0000-0000-0000000a7001"
            let kino = Guid.Parse "00000000-0000-0000-0000-0000000a7002"
            let category id : BudgetCategory =
                { Id = id; Name = "K"; Assigned = eur 0m; Activity = eur 0m; Available = eur 0m
                  Underfunded = None; Target = None; Pause = None; OpenWriteBack = None; Deactivated = None }
            let withCategories = { month with Groups = [ { Name = "Alltag"; Categories = [ category essen; category kino ] } ] }
            let report =
                { reportOf [] None with
                    Lines =
                      [ { CategoryId = essen; CategoryName = "Essen"; OldAssigned = eur 0m; NewAssigned = eur 30m; Outcome = Written }
                        { CategoryId = kino; CategoryName = "Kino"; OldAssigned = eur 0m; NewAssigned = eur 20m; Outcome = Failed "x" } ] }
            let applied = { loaded with Month = Success withCategories; Targets = ApplyDone { Sent = preview; Report = report; Retrying = false; RetryError = None } }
            let closed, _ = closeTargets applied
            let assigned id =
                match closed.Month with
                | Success m -> categoriesOf m |> List.find (fun c -> c.Id = id) |> fun c -> c.Assigned.Amount
                | other -> failtest $"Monat erwartet, kam {other}"
            Expect.equal (assigned essen) 30m "geschrieben"
            Expect.equal (assigned kino) 0m "fehlgeschlagen"
        }
        test "Ist der Ausgang des Laufs unklar, lädt Schließen den Monat wie bisher" {
            let closed, reread = closeTargets { loaded with Targets = ApplyFailed (preview, "weg") }
            Expect.equal closed.Month Loading "kein alter Stand als Rechengrundlage"
            Expect.equal reread (Some FullReread) "über Loaded mit Fehlerkarte"
            let lostRetry = { loaded with Targets = ApplyDone { Sent = preview; Report = reportOf [ Failed "x" ] None; Retrying = false; RetryError = Some "weg" } }
            let closedAfterRetry, rereadAfterRetry = closeTargets lostRetry
            Expect.equal closedAfterRetry.Month Loading "erneutes Senden mit unklarem Ausgang"
            Expect.equal rereadAfterRetry (Some FullReread) "über Loaded"
        }
        test "Läuft der Lauf noch, bleibt der Dialog beim Schließen offen und nichts wird gelesen" {
            let applying = { loaded with Targets = Applying { Shown = preview; Changed = false; Excluded = Set.empty } }
            let model, cmd, _ = Components.Budget.State.update CloseTargets applying
            Expect.equal model applying "unverändert"
            Expect.isEmpty cmd "kein Aufruf"
        }
    ]

// infrastructure-t5m2b: aus dem Browser gezogen.
let private reportLineTests =
    testList "Bericht (h2zfn)" [
        test "Probleme stehen vor den geschriebenen Zeilen, sonst bleibt die Folge" {
            let report = reportOf [ Written; Failed "a"; Written; ChangedInYnab ] None
            Expect.equal (reportLines report |> List.map (fun l -> l.CategoryName)) [ "K1"; "K3"; "K0"; "K2" ] "Folge"
        }
        // budgeting-my8p8: eine in BB zurückgezogene Zeile hieß im Bericht "in YNAB geändert".
        test "Satz und Badge je Stand" {
            let look outcome =
                let detail, badge = Components.Budget.TargetsView.outcomeLook (reportOf [ outcome ] None).Lines.Head
                detail, badge.Text, badge.Variant, badge.Size
            Expect.equal (look Written) ("0,00 € auf 10,00 €", "geschrieben", Badge.Success, Badge.Medium) "geschrieben"
            Expect.equal (look (Failed "Rate-Limit")) ("Rate-Limit", "fehlgeschlagen", Badge.Error, Badge.Medium) "fehlgeschlagen"
            Expect.equal
                (look ChangedInYnab)
                ("YNAB steht anders als erwartet, nichts überschrieben", "in YNAB geändert", Badge.Warning, Badge.Medium)
                "in YNAB geändert"
            Expect.equal
                (look WithdrawnInBb)
                ("Nicht gesendet, BB hat die Zeile zurückgezogen. YNAB blieb unverändert.", "in BB zurückgezogen", Badge.Neutral, Badge.Small)
                "in BB zurückgezogen, neutral und klein wie in BB ersetzt"
        }
    ]

let private actualTests =
    testList "Zu verteilen nach dem Lauf (h2zfn)" [
        test "Gleich der Vorschau, abweichend oder unbekannt" {
            let same = reportOf [ Written ] (Some (eur 50m))
            Expect.equal (actualCheck preview same) SameAsPreview "gleich"
            let differs = reportOf [ Written ] (Some (eur 47m))
            Expect.equal (actualCheck preview differs) (DiffersFromPreview (eur 47m)) "weicht ab"
            Expect.equal (actualCheck preview (reportOf [ Written ] None)) ActualUnknown "unbekannt"
        }
    ]

let private viewSourceTests =
    // Die Ansicht nutzt nur Design-System-Bausteine (Styleguide-Gate); eigene Farben, Größen
    // und rohe HTML-Knöpfe wären ein Verstoß.
    let source =
        let rec up (dir: string) =
            let candidate = IO.Path.Combine(dir, "src", "Client", "Components", "Budget")
            if IO.Directory.Exists candidate then candidate
            else up (IO.Path.GetDirectoryName dir)
        IO.Directory.GetFiles(up AppContext.BaseDirectory, "*View.fs")
        |> Array.map IO.File.ReadAllText
        |> String.concat "\n"
    testList "Ansicht des Dialogs (h2zfn)" [
        test "Die Ansicht enthält keine Hex-Farben und keine rohen Knöpfe" {
            Expect.isFalse (Text.RegularExpressions.Regex.IsMatch(source, "#[0-9a-fA-F]{3,8}\\b")) "keine Hex-Werte"
            Expect.isFalse (source.Contains "Html.button") "kein roher Html.button"
        }
    ]

/// Ein Schritt im Ablauf der Vorschau: zweimal öffnen, dazwischen schließen, zwei Antworten.
type private PreviewStep =
    | OpenFirst
    | CloseDialog
    | OpenSecond
    | AnswerFirst
    | AnswerSecond

/// Was der Test selbst mitschreibt, ohne den getesteten Code zu fragen: ist der Dialog offen, welche Öffnung ist die
/// jüngste, welche Antworten sind da.
type private PreviewOracle = { IsOpen: bool; Latest: int; Answered: Set<int> }

let rec private orders (items: 'a list) : 'a list list =
    match items with
    | [] -> [ [] ]
    | _ -> items |> List.collect (fun x -> orders (List.filter ((<>) x) items) |> List.map (fun rest -> x :: rest))

/// Roman öffnet, schließt und öffnet in dieser Reihenfolge; eine Antwort kommt irgendwann nach ihrer Öffnung.
let private possible (order: PreviewStep list) : bool =
    let at step = List.findIndex ((=) step) order
    at OpenFirst < at CloseDialog && at CloseDialog < at OpenSecond
    && at OpenFirst < at AnswerFirst && at OpenSecond < at AnswerSecond

let private lateAnswerTests =
    // budgeting-gvajj, Bug: `applyPreviewLoaded` prüfte nur Monat und `PreviewLoading`. Nach Öffnen, Schließen und
    // erneutem Öffnen füllte die Antwort des ersten Öffnens den Dialog, auch wenn das zweite Öffnen einen neueren Stand
    // bekäme.
    let previews = Map.ofList [ 1, previewOf 50m [ line "Essen" 0m 30m ]; 2, previewOf 40m [ line "Essen" 0m 40m ] ]
    let update msg model =
        let next, _, _ = Components.Budget.State.update msg model
        next
    let walk (order: PreviewStep list) =
        let start = loaded, Map.empty<int, int>, { IsOpen = false; Latest = 0; Answered = Set.empty }
        order
        |> List.fold
            (fun (model, generations: Map<int, int>, oracle) step ->
                // Die Generation, die der echte Aufruf der Öffnung mitträgt, steht nach dem Öffnen im Modell.
                let opened n =
                    let next = update OpenTargets model
                    next, generations.Add(n, next.TargetsPreviewGeneration), { oracle with IsOpen = true; Latest = n }
                let answered n =
                    let next = update (PreviewLoaded ("2026-11-01", generations[n], Ok previews[n])) model
                    next, generations, { oracle with Answered = oracle.Answered.Add n }
                let model', generations', oracle' =
                    match step with
                    | OpenFirst -> opened 1
                    | OpenSecond -> opened 2
                    | CloseDialog -> update CloseTargets model, generations, { oracle with IsOpen = false }
                    | AnswerFirst -> answered 1
                    | AnswerSecond -> answered 2
                let expected =
                    if not oracle'.IsOpen then TargetsClosed
                    elif oracle'.Answered.Contains oracle'.Latest then
                        PreviewReady { Shown = previews[oracle'.Latest]; Changed = false; Excluded = Set.empty }
                    else PreviewLoading
                Expect.equal model'.Targets expected $"%A{order}, nach %A{step}"
                model', generations', oracle')
            start
        |> ignore
    testList "Späte Antwort der Vorschau (gvajj)" [
        test "In jeder Reihenfolge von Öffnen, Schließen, Öffnen und zwei Antworten gilt nur die jüngste Öffnung" {
            let all = orders [ OpenFirst; CloseDialog; OpenSecond; AnswerFirst; AnswerSecond ] |> List.filter possible
            Expect.equal all.Length 4 "alle möglichen Reihenfolgen"
            all |> List.iter walk
        }
    ]

[<Tests>]
let tests =
    testList "Targets anwenden, Client (budgeting-h2zfn)" [ openTests; confirmTests; retryTests; actualTests; reportLineTests; viewSourceTests; lateAnswerTests ]
