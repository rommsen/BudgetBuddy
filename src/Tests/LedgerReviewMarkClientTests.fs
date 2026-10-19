module LedgerReviewMarkClientTests

// Der Prüfvermerk an der Zeile (ledger-r777c, ADR 0050), Client-Seite: der reine Reducer
// und die reinen Bausteine der Ansicht. Kein Api-Aufruf: `StartSetReviewMark` und
// `StartRetryReviewMark` setzen nur den Zustand, der Proxy steckt hinter `Run...` (ADR 0020).

open System
open System.IO
open Expecto
open Shared.Domain
open Types
open Components.Ledger.Types

let private eur (amount: decimal) : Money = { Amount = amount; Currency = "EUR" }

let private entryView (n: int) (needsReview: bool) : LedgerEntryView = {
    Id = Guid.Parse $"0195f100-0000-7000-8000-0000000000{n:D2}"
    AccountId = Guid.Parse "0195e100-0000-7000-8000-000000000001"
    Date = DateTime(2026, 10, 1)
    Amount = eur -10m
    Payee = Some $"Payee {n}"
    Memo = None
    CategoryName = None
    CategoryGroupName = None
    Splits = []
    Origin = MirroredFromYnab
    DeletedInYnab = false
    NeedsReview = needsReview
    Flag = NoFlag
    IsTransfer = false
    ReviewMarkWriteBackFailure = None
}

let private modelWith (onlyNeedsReview: bool) (entries: LedgerEntryView list) : Model =
    let model, _ = Components.Ledger.State.init ()
    { model with
        Entries = Success entries
        OnlyNeedsReview = onlyNeedsReview
        TotalCount = List.length entries
        TotalAmount = eur (entries |> List.sumBy (fun e -> e.Amount.Amount))
        NeedsReviewCount = entries |> List.filter (fun e -> e.NeedsReview) |> List.length }

let private update = Components.Ledger.State.update

let private outcome (change: ReviewMarkChange) (writeBack: ReviewMarkWriteBack) : ReviewMarkOutcome = {
    Previous = { IsApproved = false; ClearedState = ReviewUncleared }
    Change = change
    WriteBack = writeBack
}

let private entriesOf (model: Model) =
    match model.Entries with
    | Success entries -> entries
    | _ -> failtest "Entries sollte geladen sein"

[<Tests>]
let reviewMarkReducerTests =
    testList "Kassenbuch-Seite Prüfvermerk (Reducer)" [

        testCase "Klick auf Geprüft setzt den Zeilenzustand auf Laden" <| fun () ->
            let e = entryView 1 true
            let model, _, _ = update (StartSetReviewMark (e.Id, Reviewed)) (modelWith false [ e ])
            Expect.equal (model.ReviewMarkRows |> Map.tryFind e.Id) (Some ReviewMarkSending) "Zeile sendet"

        testCase "Erfolg ohne aktiven Chip: Zeile bleibt, ist nicht mehr zu prüfen, Zähler sinkt" <| fun () ->
            let a, b = entryView 1 true, entryView 2 true
            let start, _, _ = update (StartSetReviewMark (a.Id, Reviewed)) (modelWith false [ a; b ])
            let model, _, _ = update (ReviewMarkSet (a.Id, Reviewed, Ok (outcome ReviewMarkChanged ReviewMarkSent))) start
            Expect.hasLength (entriesOf model) 2 "beide Zeilen bleiben"
            Expect.isFalse (entriesOf model |> List.find (fun e -> e.Id = a.Id)).NeedsReview "geprüft"
            Expect.equal model.NeedsReviewCount 1 "Zähler sinkt um eins"
            Expect.isFalse (model.ReviewMarkRows |> Map.containsKey a.Id) "Zeilenzustand zurückgesetzt"

        testCase "Erfolg bei aktivem Chip Zu prüfen: Zeile verschwindet, Zähler und Treffer sinken" <| fun () ->
            let a, b = entryView 1 true, entryView 2 true
            let start, _, _ = update (StartSetReviewMark (a.Id, Reviewed)) (modelWith true [ a; b ])
            let model, _, _ = update (ReviewMarkSet (a.Id, Reviewed, Ok (outcome ReviewMarkChanged ReviewMarkSent))) start
            Expect.equal (entriesOf model |> List.map (fun e -> e.Id)) [ b.Id ] "die Zeile verlässt die Liste"
            Expect.equal model.NeedsReviewCount 1 "Chipzähler sinkt um eins"
            Expect.equal model.TotalCount 1 "Trefferzahl sinkt"
            Expect.equal model.TotalAmount.Amount -10m "Summe ohne die entfernte Zeile"

        testCase "Ungeprüft macht die Zeile wieder zu prüfen und hebt den Zähler" <| fun () ->
            let a = entryView 1 false
            let model, _, _ =
                update (ReviewMarkSet (a.Id, Unreviewed, Ok (outcome ReviewMarkChanged ReviewMarkSent))) (modelWith false [ a ])
            Expect.isTrue (entriesOf model |> List.head).NeedsReview "wieder zu prüfen"
            Expect.equal model.NeedsReviewCount 1 "Zähler steigt um eins"

        testCase "Fehlschlag beim Zurückschreiben: Zeile bleibt, trägt den Grund, ist geprüft" <| fun () ->
            let a = entryView 1 true
            let result = outcome ReviewMarkChanged (ReviewMarkSendFailed "Keine Verbindung zu YNAB")
            let model, _, _ = update (ReviewMarkSet (a.Id, Reviewed, Ok result)) (modelWith true [ a ])
            let row = entriesOf model |> List.head
            Expect.equal row.ReviewMarkWriteBackFailure (Some "Keine Verbindung zu YNAB") "Grund an der Zeile"
            Expect.isFalse row.NeedsReview "in BB geprüft"
            Expect.equal model.NeedsReviewCount 0 "der Zähler folgt dem BB-Stand"

        testCase "Fehler des Befehls: Zeile bleibt unverändert und zeigt den Fehler" <| fun () ->
            let a = entryView 1 true
            let model, _, _ = update (ReviewMarkSet (a.Id, Reviewed, Error "Die Buchung ist in YNAB gelöscht")) (modelWith false [ a ])
            Expect.isTrue (entriesOf model |> List.head).NeedsReview "unverändert"
            Expect.equal
                (model.ReviewMarkRows |> Map.tryFind a.Id)
                (Some (ReviewMarkCommandFailed "Die Buchung ist in YNAB gelöscht"))
                "Fehler an der Zeile"
            let hint = reviewMarkHintFor (model.ReviewMarkRows |> Map.tryFind a.Id) (entriesOf model |> List.head)
            Expect.equal (hint |> Option.map (fun h -> h.Reason)) (Some "Die Buchung ist in YNAB gelöscht") "Hinweis nennt den Fehler"

        testCase "Ziel gleich Ist ohne Versand lässt einen offenen Fehlschlag stehen" <| fun () ->
            let a = { entryView 1 false with ReviewMarkWriteBackFailure = Some "Timeout" }
            let model, _, _ =
                update (ReviewMarkSet (a.Id, Reviewed, Ok (outcome ReviewMarkUnchanged ReviewMarkNotSent))) (modelWith false [ a ])
            Expect.equal (entriesOf model |> List.head).ReviewMarkWriteBackFailure (Some "Timeout") "Fehlschlag bleibt"

        testCase "Erneut senden: Laden, dann Erfolg leert den Hinweis, ein neuer Fehler ersetzt den Grund" <| fun () ->
            let a = { entryView 1 false with ReviewMarkWriteBackFailure = Some "alt" }
            let start, _, _ = update (StartRetryReviewMark a.Id) (modelWith false [ a ])
            Expect.equal (start.ReviewMarkRows |> Map.tryFind a.Id) (Some ReviewMarkSending) "sendet"
            let failed, _, _ = update (ReviewMarkRetried (a.Id, Ok (ReviewMarkSendFailed "neu"))) start
            Expect.equal (entriesOf failed |> List.head).ReviewMarkWriteBackFailure (Some "neu") "neuer Grund"
            let sent, _, _ = update (ReviewMarkRetried (a.Id, Ok ReviewMarkSent)) failed
            Expect.isNone (entriesOf sent |> List.head).ReviewMarkWriteBackFailure "Hinweis weg"
            Expect.isFalse (sent.ReviewMarkRows |> Map.containsKey a.Id) "Zeilenzustand zurückgesetzt"
    ]

[<Tests>]
let reviewMarkViewTests =
    testList "Kassenbuch-Seite Prüfvermerk (Ansicht)" [

        testCase "Zeile zu prüfen zeigt Geprüft, geprüfte Zeile zeigt Ungeprüft" <| fun () ->
            let toReview = reviewMarkActionFor (entryView 1 true)
            let reviewed = reviewMarkActionFor (entryView 2 false)
            Expect.equal (toReview.Label, toReview.Target) ("Geprüft", Reviewed) "Zeile zu prüfen"
            Expect.equal (reviewed.Label, reviewed.Target) ("Ungeprüft", Unreviewed) "geprüfte Zeile"

        testCase "Zeile mit Fehlschlag zeigt den Hinweis mit Grund und Erneut senden" <| fun () ->
            let e = { entryView 1 false with ReviewMarkWriteBackFailure = Some "Keine Verbindung zu YNAB" }
            match reviewMarkHintFor None e with
            | Some hint ->
                Expect.equal hint.Title "Prüfvermerk nicht in YNAB" "Titel"
                Expect.equal hint.Reason "Keine Verbindung zu YNAB" "Grund"
                Expect.equal hint.RetryLabel (Some "Erneut senden") "Schaltfläche"
            | None -> failtest "Hinweis erwartet"

        testCase "Zeile ohne Fehlschlag zeigt keinen Hinweis" <| fun () ->
            Expect.isNone (reviewMarkHintFor None (entryView 1 true)) "kein Hinweis"

        testCase "Die Ansicht verdrahtet die Schaltfläche mit den Start-Nachrichten" <| fun () ->
            // Unter .NET lässt sich die React-Ansicht nicht rendern (Fable-only): die
            // Verdrahtung wird am Quelltext geprüft (Muster aus RuhigeListenTests).
            let rec findRoot (dir: string) =
                if File.Exists(Path.Combine(dir, "BudgetBuddy.sln")) || Directory.Exists(Path.Combine(dir, "src", "Client")) then dir
                else findRoot (Directory.GetParent(dir).FullName)
            let root = findRoot AppContext.BaseDirectory
            let source = File.ReadAllText(Path.Combine(root, "src", "Client", "Components", "Ledger", "View.fs"))
            Expect.stringContains source "StartSetReviewMark (entry.Id, action.Target)" "Klick setzt den Vermerk"
            Expect.stringContains source "StartRetryReviewMark entryId" "Erneut senden"
            Expect.stringContains source "e.stopPropagation ()" "Klick öffnet nicht die Historie"
    ]
