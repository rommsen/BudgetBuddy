module LedgerRuleOfferClientTests

// ledger-hg5m5: Regel aus der Buchung, Kassenbuch-Seite. Der feste Eintrag „Regel anlegen“ meldet Payee und Kategorie
// nach oben, das Angebot nach einer Kategorie-Änderung fragt den Server und verschwindet mit ×, „Rückgängig“ oder
// einem Wechsel der Buchung. Der Aufruf an den Server steckt hinter `RunCheckRuleOffer` und läuft hier nicht.

open System
open Expecto
open Shared.Domain
open Types
open Components.Ledger.Types

let private eur (amount: decimal) : Money = { Amount = amount; Currency = "EUR" }

let private lebensmittel = YnabCategoryId (Guid.Parse "0195c100-0000-7000-8000-000000000001")
let private haushalt = YnabCategoryId (Guid.Parse "0195c100-0000-7000-8000-000000000002")
let private guidOf (YnabCategoryId g) = g

let private entryOf (n: int) (payee: string option) : LedgerEntryView =
    { Id = Guid.Parse $"0195f600-0000-7000-8000-{n:D12}"
      AccountId = Guid.Parse "0195e600-0000-7000-8000-000000000001"
      Date = DateTime.Today
      Amount = eur -10m
      Payee = payee
      Memo = None
      CategoryName = Some "Haushalt"
      CategoryId = Some (guidOf haushalt)
      CategoryGroupName = Some "Alltag"
      Reconciled = false
      SplitCategoryGroupNames = Map.empty
      Splits = []
      Origin = MirroredFromYnab
      DeletedInYnab = false
      NeedsReview = false
      Flag = NoFlag
      IsTransfer = false
      TransferAccountId = None
      ReviewMarkWriteBackFailure = None
      CategorizationWriteBackFailure = None
      FlagWriteBackFailure = None
      MemoWriteBackFailure = None
      AmountWriteBackFailure = None
      AmountLockReason = None
      PayeeWriteBackFailure = None
      LinkedToYnab = true
      PushToYnabFailure = None; SplitLocks = None; ExternalLinks = []
      PossibleDuplicate = None; CategoryFromOrderId = false }

let private rewe = entryOf 1 (Some "Rewe")
let private other = entryOf 2 (Some "Aldi")
let private noPayee = entryOf 3 None

/// Zeilen geladen, Detail offen auf der ersten. Ohne geladene Kategorien: keine Karte „Budget nach dem Wechsel“ und
/// damit kein schlafender Ablauf-Befehl.
let private modelWith (rows: LedgerEntryView list) : Model =
    let model, _ = Components.Ledger.State.init ()
    { model with
        Entries = Success rows
        NextPage = 1
        TotalCount = List.length rows
        TotalAmount = eur -30m
        HasMoreEntries = false
        DetailFor = List.tryHead rows
        DetailForm = DetailPanel }

let private update = Components.Ledger.State.update

let private run (msg: Msg) (model: Model) : Model * Msg list * ExternalMsg =
    let model', cmd, external = update msg model
    let sent = ResizeArray<Msg>()
    for sub in cmd do
        sub sent.Add
    model', List.ofSeq sent, external

let private step (msg: Msg) (model: Model) : Model = let m, _, _ = update msg model in m

let private changed : CategoryChangeOutcome =
    { Previous = Some { CategoryId = haushalt; CategoryName = "Haushalt" }
      Change = CategoryChanged
      WriteBack = CategorySent
      PreviousTransferAccountId = None }

/// Roman ändert im Detail die Kategorie der Buchung auf Lebensmittel, das Speichern gelingt.
let private changedTo (entry: LedgerEntryView) (model: Model) : Model * Msg list =
    let started = step (StartSetEntryCategory (entry.Id, Some lebensmittel)) model
    let after, sent, _ = run (EntryCategorySet (entry.Id, Some lebensmittel, Ok changed)) started
    after, sent

let private reweCheck = RunCheckRuleOffer (rewe.Id, "Rewe", "", lebensmittel)

let private shownEntry (model: Model) = detailEntryOf model |> Option.get

[<Tests>]
let seedTests =
    testList "Regel aus der Buchung: Vorbelegung" [
        test "Payee und Kategorie der Buchung belegen das Formular vor" {
            Expect.equal (ruleSeedFor { rewe with Payee = Some "  Rewe  " }) (Some ("Rewe", Some haushalt)) "Payee getrimmt, Kategorie"
        }
        test "Ohne Kategorie, bei Aufteilung oder Übertrag bleibt die Kategorie leer" {
            Expect.equal (ruleSeedFor { rewe with CategoryId = None; CategoryName = None }) (Some ("Rewe", None)) "ohne"
            let split : TransactionSplit = { Target = ToCategory (lebensmittel, "Lebensmittel"); Amount = eur -10m; Memo = None }
            Expect.equal (ruleSeedFor { rewe with Splits = [ split; split ] }) (Some ("Rewe", None)) "Aufteilung"
            Expect.equal (ruleSeedFor { rewe with IsTransfer = true }) (Some ("Rewe", None)) "Übertrag"
        }
        test "Ohne Payee gibt es keinen Eintrag" {
            Expect.isNone (ruleSeedFor noPayee) "ohne"
            Expect.isNone (ruleSeedFor { rewe with Payee = Some "   " }) "nur Leerzeichen"
        }
        test "Regel anlegen meldet Buchung, Payee und Kategorie nach oben, ohne Payee nichts" {
            let model = modelWith [ rewe; noPayee ]
            let _, _, external = run (RuleFromEntryRequested rewe.Id) model
            Expect.equal external (OpenRuleForEntry (rewe.Id, "Rewe", Some haushalt)) "Formular öffnen"
            let _, _, none = run (RuleFromEntryRequested noPayee.Id) model
            Expect.equal none NoOp "ohne Payee"
        }
    ]

[<Tests>]
let offerTests =
    testList "Regel aus der Buchung: Angebot nach einer Kategorie-Änderung" [
        // GIVEN eine Buchung mit Payee „Rewe“ WHEN Roman im Detail die Kategorie auf „Lebensmittel“ ändert und das
        // Speichern gelingt THEN fragt das Kassenbuch den Server und zeigt das Angebot, wenn keine Regel passt.
        test "Nach der Änderung fragt das Detail den Server und zeigt danach das Angebot" {
            let asked, sent = changedTo rewe (modelWith [ rewe; other ])
            Expect.contains sent reweCheck "Frage an den Server mit Payee, Memo und neuer Kategorie"
            Expect.isNone (ruleOfferFor asked (shownEntry asked)) "vor der Antwort kein Angebot"
            let answered = step (RuleOfferChecked (rewe.Id, lebensmittel, Ok None)) asked
            Expect.equal (ruleOfferFor answered (shownEntry answered)) (Some "Rewe") "Angebot"
            Expect.equal (ruleOfferText "Rewe") "Regel für „Rewe“ anlegen?" "Satz"
        }
        // Der Server prüft, ob eine Regel auf Payee und Memo schon passt (`MemoContains`); ohne das Memo der Buchung
        // böte das Detail an, obwohl eine Memo-Regel die Buchung schon kategorisiert.
        test "Die Frage an den Server trägt das Memo der Buchung" {
            let withMemo = { rewe with Memo = Some "Miete Mai" }
            let _, sent = changedTo withMemo (modelWith [ withMemo ])
            Expect.contains sent (RunCheckRuleOffer (rewe.Id, "Rewe", "Miete Mai", lebensmittel)) "Memo in der Frage"
        }
        test "Gibt schon eine Regel die neue Kategorie, kein Angebot; eine andere Kategorie der Regel bietet an" {
            let asked, _ = changedTo rewe (modelWith [ rewe ])
            let covered = step (RuleOfferChecked (rewe.Id, lebensmittel, Ok (Some lebensmittel))) asked
            Expect.isNone (ruleOfferFor covered (shownEntry covered)) "passt schon"
            Expect.isNone covered.RuleOffer "kein Rest"
            let differs = step (RuleOfferChecked (rewe.Id, lebensmittel, Ok (Some haushalt))) asked
            Expect.equal (ruleOfferFor differs (shownEntry differs)) (Some "Rewe") "Regel gibt anderes"
        }
        test "Ohne Antwort des Servers kein Angebot" {
            let asked, _ = changedTo rewe (modelWith [ rewe ])
            Expect.isNone (step (RuleOfferChecked (rewe.Id, lebensmittel, Error "Netz weg")) asked).RuleOffer "kein Angebot"
        }
        test "Kein Angebot ohne Payee, ohne neue Kategorie und auf der Karte des Durchgehens" {
            let _, sent = changedTo noPayee (modelWith [ noPayee ])
            Expect.isFalse (sent |> List.exists (function RunCheckRuleOffer _ -> true | _ -> false)) "ohne Payee"
            let removed =
                let started = step (StartSetEntryCategory (rewe.Id, None)) (modelWith [ rewe ])
                let _, sent, _ = run (EntryCategorySet (rewe.Id, None, Ok changed)) started
                sent
            Expect.isFalse (removed |> List.exists (function RunCheckRuleOffer _ -> true | _ -> false)) "Kategorie entfernt"
            // Die Buchung trägt die neue Kategorie schon, wie nach dem Speichern.
            let onCard =
                { modelWith [ { rewe with CategoryId = Some (guidOf lebensmittel) } ] with
                    Review = Some { reviewStarting with Queue = Success [ rewe.Id ]; DetailShown = false } }
            Expect.isNone (Components.Ledger.State.ruleOfferCheckFor onCard rewe.Id (Some lebensmittel)) "Karte ohne Detail"
            let withDetail = { onCard with Review = onCard.Review |> Option.map (fun r -> { r with DetailShown = true }) }
            Expect.isSome (Components.Ledger.State.ruleOfferCheckFor withDetail rewe.Id (Some lebensmittel)) "Details offen"
        }
        test "Kein Angebot bei Aufteilung oder Übertrag" {
            let split : TransactionSplit = { Target = ToCategory (lebensmittel, "Lebensmittel"); Amount = eur -5m; Memo = None }
            let splitEntry = { rewe with CategoryId = Some (guidOf lebensmittel); Splits = [ split; split ] }
            Expect.isNone (Components.Ledger.State.ruleOfferCheckFor (modelWith [ splitEntry ]) rewe.Id (Some lebensmittel)) "Aufteilung"
            let transfer = { rewe with IsTransfer = true; CategoryId = Some (guidOf lebensmittel) }
            Expect.isNone (Components.Ledger.State.ruleOfferCheckFor (modelWith [ transfer ]) rewe.Id (Some lebensmittel)) "Übertrag"
        }
        test "Kein Angebot nach der Sammelaktion Kategorie setzen" {
            let model = modelWith [ rewe; other ]
            let outcome : CategoryBatchOutcome = { Changed = []; UnchangedCount = 0; Skipped = [] }
            let after, sent, _ = run (CategoryBatchDone (Some lebensmittel, Ok outcome)) model
            Expect.isNone after.RuleOffer "kein Angebot"
            Expect.isFalse (sent |> List.exists (function RunCheckRuleOffer _ -> true | _ -> false)) "keine Frage"
        }
        test "Anlegen im Angebot öffnet das vorbelegte Formular und nimmt das Angebot weg" {
            let asked, _ = changedTo rewe (modelWith [ rewe ])
            let shown = step (RuleOfferChecked (rewe.Id, lebensmittel, Ok None)) asked
            let after, _, external = run (RuleFromEntryRequested rewe.Id) shown
            Expect.equal external (OpenRuleForEntry (rewe.Id, "Rewe", Some lebensmittel)) "neue Kategorie vorbelegt"
            Expect.isNone after.RuleOffer "Angebot weg"
        }
    ]

/// Was das Angebot beendet: ×, „Rückgängig“ und der Wechsel zu einer anderen Buchung (danach zurück zur ersten).
type private Ending =
    | Dismiss
    | Undo
    | OtherEntry

let private endingMsgs (ending: Ending) : Msg list =
    match ending with
    | Dismiss -> [ RuleOfferDismissed ]
    | Undo -> [ StartUndoCategory (rewe.Id, changed.Previous) ]
    | OtherEntry -> [ OpenDetail other; OpenDetail rewe ]

let private answer = RuleOfferChecked (rewe.Id, lebensmittel, Ok None)

/// Alle Reihenfolgen, in denen die Antwort des Servers und eine Teilmenge der Enden ankommen.
let private orders : (Ending list * Msg list list) list =
    let subsets = [ []; [ Dismiss ]; [ Undo ]; [ OtherEntry ]; [ Dismiss; OtherEntry ]; [ Undo; OtherEntry ]; [ Dismiss; Undo; OtherEntry ] ]
    let rec insertAll x list =
        match list with
        | [] -> [ [ x ] ]
        | head :: tail -> (x :: list) :: (insertAll x tail |> List.map (fun rest -> head :: rest))
    let rec permutations list =
        match list with
        | [] -> [ [] ]
        | head :: tail -> permutations tail |> List.collect (insertAll head)
    subsets
    |> List.collect (fun endings ->
        permutations (Choice1Of2 () :: (endings |> List.map Choice2Of2))
        |> List.map (fun order ->
            endings,
            order |> List.map (function Choice1Of2 () -> [ answer ] | Choice2Of2 e -> endingMsgs e)))

[<Tests>]
let endingTests =
    testList "Regel aus der Buchung: das Angebot in jeder Reihenfolge" [
        // Orakel: das Angebot steht genau dann, wenn nichts es beendet hat; eine späte Antwort bringt es nicht zurück.
        test "Das Angebot steht nur ohne ×, Rückgängig und Wechsel der Buchung, gleich in welcher Reihenfolge" {
            let asked, _ = changedTo rewe (modelWith [ rewe; other ])
            for endings, order in orders do
                let final = order |> List.concat |> List.fold (fun m msg -> step msg m) asked
                let expected = if List.isEmpty endings then Some "Rewe" else None
                let entry = detailEntryOf final |> Option.get
                Expect.equal (ruleOfferFor final entry) expected $"%A{endings} in %A{order}"
        }
    ]
