module LedgerReviewClientTests

// Durchgehen Karte für Karte, Client-Seite (ledger-v6s7c):
//
//  * reine Funktionen: Ziel für „Passt“, Wischschwelle, Tastenzuordnung, Texte von Karte und Endansicht;
//  * die Zustandsfolge durch das echte `update` als Aufzählung aller Reihenfolgen (Doktrin Punkt 20): Passt mit
//    später Antwort und Fehler je gesendetem Aufruf, Später, Lena mit Rückgängig je Karte, Kategorie, Verwerfen und
//    Ist neu mit getrennter Antwort (Ok und Fehler), Rückgängig nach dem Verwerfen (Ok und Fehler), Ende.
//    Das Orakel führt Stelle, Schlange und offene Antworten selbst und fragt den getesteten Code nicht;
//  * Einzelfälle: Toasts, Überspringen beim Neulesen, Details, Esc und Verlassen.

open System
open Expecto
open Shared.Domain
open Types
open Components.Ledger.Types

let private eur (amount: decimal) : Money = { Amount = amount; Currency = "EUR" }
let private account = Guid.Parse "0195e800-0000-7000-8000-0000000000aa"
let private food = Guid.Parse "0195d800-0000-7000-8000-0000000000f1"

let private entryOf (n: int) : LedgerEntryView =
    { Id = Guid.Parse $"0195f810-0000-7000-8000-{n:D12}"
      AccountId = account
      Date = DateTime(2026, 11, n)
      Amount = eur -10m
      Payee = Some $"Payee v6s7c {n}"
      Memo = None
      CategoryName = Some "Lebensmittel"
      CategoryId = Some food
      CategoryGroupName = Some "Alltag"
      Reconciled = false
      SplitCategoryGroupNames = Map.empty
      Splits = []
      Origin = ImportedFromComdirect
      DeletedInYnab = false
      NeedsReview = true
      Flag = NoFlag
      IsTransfer = false
      TransferAccountId = None
      ReviewMarkWriteBackFailure = None
      CategorizationWriteBackFailure = None
      FlagWriteBackFailure = None
      MemoWriteBackFailure = None
      AmountWriteBackFailure = None
      AmountLockReason = None
      LinkedToYnab = true
      PushToYnabFailure = None
      SplitLocks = None
      ExternalLinks = []
      PayeeWriteBackFailure = None
      PossibleDuplicate = None
      CategoryFromOrderId = false }

let private withoutCategory (e: LedgerEntryView) = { e with CategoryName = None; CategoryId = None; CategoryGroupName = None }

let private held (n: int) : LedgerEntryView =
    { withoutCategory (entryOf n) with
        LinkedToYnab = false
        PossibleDuplicate =
            Some { Reason = "Gleicher Betrag, gleicher Tag, gleicher Payee, aber eine andere Bank-Referenz."
                   MatchDate = DateTime(2026, 11, 1)
                   MatchAmount = eur -10m
                   MatchPayee = Some "Markt"
                   MatchCategoryName = Some "Lebensmittel"
                   MatchEntryId = None } }

let private idOf (e: LedgerEntryView) = e.Id

let private categories : YnabCategory list =
    [ { Id = YnabCategoryId food; Name = "Lebensmittel"; GroupName = "Alltag"; Available = Some (eur 120m); Hidden = false } ]

let private update = Components.Ledger.State.update

/// Ein Schritt durch das echte `update`; die Folgenachrichten werden gesammelt, nicht ausgeführt. Ein Effekt
/// (Gerätespeicher) läuft unter .NET nicht und bleibt außen vor.
let private run (msg: Msg) (model: Model) : Model * Msg list * ExternalMsg =
    let model', cmd, external = update msg model
    let sent = ResizeArray<Msg>()
    for sub in cmd do
        try sub sent.Add with _ -> ()
    model', List.ofSeq sent, external

let private step (msg: Msg) (model: Model) : Model =
    let model', _, _ = run msg model
    model'

/// Das Durchgehen mit geladener Schlange, wie `ReviewOpened` und die Antwort es hinterlassen.
let private reviewing (queue: LedgerEntryView list) : Model =
    let model, _ = Components.Ledger.State.init ()
    { model with LenaFlagColor = Success (Some "purple"); Categories = Success categories; DetailForm = DetailSheet }
    |> step ReviewOpened
    |> fun opened -> step (ReviewQueueLoaded (opened.ReviewGeneration, Ok queue)) opened

let private cardId (model: Model) = reviewCardOf model |> Option.map idOf
let private indexOf (model: Model) = model.Review |> Option.map (fun r -> r.Index) |> Option.defaultValue -1

let private reviewed (entryId: Guid) : Msg =
    ReviewConfirmed (entryId, Ok { Previous = { IsApproved = false; ClearedState = ReviewUncleared }; Change = ReviewMarkChanged; WriteBack = ReviewMarkSent })

let private categorySet (entryId: Guid) : Msg list =
    [ StartSetEntryCategory (entryId, Some (YnabCategoryId food))
      EntryCategorySet (
          entryId,
          Some (YnabCategoryId food),
          Ok { Previous = None; Change = CategoryChanged; WriteBack = CategorySent; PreviousTransferAccountId = None }
      ) ]

[<Tests>]
let pureTests =
    testList "Durchgehen: reine Funktionen" [

        testCase "Passt ist ohne Ziel gesperrt und mit Kategorie, Übertrag oder Aufteilung frei" <| fun () ->
            let bare = withoutCategory (entryOf 1)
            Expect.isFalse (reviewHasTarget bare) "ohne Ziel"
            Expect.isTrue (reviewHasTarget (entryOf 1)) "Kategorie"
            Expect.isTrue (reviewHasTarget { bare with IsTransfer = true; TransferAccountId = Some Guid.Empty }) "Übertrag"
            let split : TransactionSplit = { Amount = eur -5m; Memo = None; Target = Uncategorized }
            Expect.isTrue (reviewHasTarget { bare with Splits = [ split; split ] }) "Aufteilung"

        testCase "die Wischschwelle liegt über 90 Pixeln in beide Richtungen" <| fun () ->
            Expect.equal (reviewSwipeOf 91.0 0.0) SwipeConfirm "rechts: Passt"
            Expect.equal (reviewSwipeOf 90.0 0.0) SwipeNothing "genau 90: nichts"
            Expect.equal (reviewSwipeOf -91.0 0.0) SwipeSkip "links: Später"
            Expect.equal (reviewSwipeOf -90.0 0.0) SwipeNothing "genau -90: nichts"
            Expect.equal (reviewSwipeOf 0.0 0.0) SwipeNothing "kein Weg"

        // Review-Fund zu ledger-v6s7c: `touchend` entschied nur über den Weg zur Seite. Eine schräge Scrollbewegung
        // mit mehr als 90 px seitlich setzte den Prüfvermerk (geht nach YNAB, ohne Rückgängig).
        testCase "nur ein waagrechter Wisch entscheidet, ein schräger Scroll nicht" <| fun () ->
            Expect.equal (reviewSwipeOf 120.0 150.0) SwipeNothing "mehr senkrecht als waagrecht: nichts"
            Expect.equal (reviewSwipeOf 120.0 120.0) SwipeNothing "gleich viel: nichts"
            Expect.equal (reviewSwipeOf -120.0 -130.0) SwipeNothing "nach links, mehr senkrecht: nichts"
            Expect.equal (reviewSwipeOf 120.0 -40.0) SwipeConfirm "leicht schräg nach rechts: Passt"
            Expect.equal (reviewSwipeOf -120.0 40.0) SwipeSkip "leicht schräg nach links: Später"

        testCase "Tasten je Kartenart: Enter nur mit Ziel und nicht auf der Duplikat-Karte, K und L nicht auf ihr" <| fun () ->
            let normal = Some (NormalCard, true)
            let noTarget = Some (NormalCard, false)
            let dup = Some (DuplicateCard, false)
            let cases =
                [ "Enter", normal, Some KeyConfirm
                  "Enter", noTarget, None
                  "Enter", dup, None
                  "s", normal, Some KeySkip
                  "S", dup, Some KeySkip
                  "k", normal, Some KeyOpenCategory
                  "K", noTarget, Some KeyOpenCategory
                  "k", dup, None
                  "l", normal, Some KeyLena
                  "L", dup, None
                  "Escape", normal, Some KeyLeave
                  "Escape", dup, Some KeyLeave
                  "Escape", None, Some KeyLeave
                  "s", None, None
                  "x", normal, None ]
            for key, card, expected in cases do
                Expect.equal (reviewKeyAction card key) expected $"{key} auf {card}"

        testCase "Tasten aus einem Feld, mit Strg oder bei offener Auswahl wirken nicht" <| fun () ->
            let model = reviewing [ entryOf 1 ]
            let key = Components.Ledger.View.reviewKeyFor
            Expect.equal (key model "BODY" false false "Enter") (Some KeyConfirm) "frei"
            Expect.equal (key model "INPUT" false false "s") None "im Feld"
            Expect.equal (key model "BODY" false true "s") None "mit Strg"
            Expect.equal (key model "BUTTON" false false "Enter") None "Enter auf einem Knopf"
            let picking = { model with CategoryPickerFor = Some (entryOf 1).Id }
            Expect.equal (key picking "BODY" false false "Escape") None "der Picker behandelt Esc selbst"
            let details = model |> step ReviewShowDetails
            Expect.equal (key details "BODY" false false "s") None "mit offenem Detail nur Esc"
            Expect.equal (key details "BODY" false false "Escape") (Some KeyLeave) "Esc schließt das Detail"

        testCase "Endansicht: Zahl mit Einzahl, ohne Karte nur der Satz zum Import" <| fun () ->
            Expect.equal (reviewDoneText 10) "10 Buchungen durchgegangen. Neue erscheinen nach dem nächsten Import." "Mehrzahl"
            Expect.equal (reviewDoneText 1) "1 Buchung durchgegangen. Neue erscheinen nach dem nächsten Import." "Einzahl"
            Expect.equal (reviewDoneText 0) "Neue erscheinen nach dem nächsten Import." "leere Schlange"
            Expect.equal (reviewProgressText 3 10) (Some "4 von 10") "i von n"
            Expect.equal (reviewProgressPercent 3 10) 30.0 "(i−1)/n"
            Expect.equal (reviewProgressPercent 10 10) 100.0 "voll am Ende"

        testCase "Kategorie, Übertrag und Aufteilung im Feld der Karte" <| fun () ->
            let model = reviewing [ entryOf 1 ]
            let value = Components.Ledger.View.reviewCategoryValue model
            Expect.equal (value (entryOf 1)) (Components.Ledger.View.ReviewCategoryNamed "Lebensmittel") "Kategorie"
            Expect.equal (value (withoutCategory (entryOf 1))) Components.Ledger.View.ReviewMissing "ohne"
            let split : TransactionSplit = { Amount = eur -5m; Memo = None; Target = Uncategorized }
            Expect.equal (value { withoutCategory (entryOf 1) with Splits = [ split; split ] }) Components.Ledger.View.ReviewSplit "aufgeteilt"
            match value { withoutCategory (entryOf 1) with IsTransfer = true; TransferAccountId = Some Guid.Empty } with
            | Components.Ledger.View.ReviewCategoryNamed text -> Expect.stringStarts text "→ " "Übertrag"
            | other -> failtestf "Übertrag erwartet: %A" other

        testCase "die Amazon-Bestellung heißt Bestellung bei Amazon mit Nummer, andere Links behalten ihr Wort" <| fun () ->
            let entry =
                { entryOf 1 with
                    ExternalLinks =
                        [ { Label = "Bestellung 302-1234567-1234567"; Url = "https://example.invalid/order" }
                          { Label = "PayPal Activity"; Url = "https://example.invalid/paypal" } ] }
            Expect.equal
                (Components.Ledger.View.reviewLinks entry)
                [ "Bestellung bei Amazon", Some "302-1234567-1234567", "https://example.invalid/order"
                  "PayPal Activity", None, "https://example.invalid/paypal" ]
                "zwei Zeilen"

        testCase "die Zeile des Treffers nimmt die aufgelöste Buchung, sonst den Schnappschuss" <| fun () ->
            let today = DateTime(2026, 11, 12)
            let dup = (held 5).PossibleDuplicate.Value
            let name _ = "Girokonto"
            let payee, meta, amount = Components.Ledger.View.reviewMatchRow today name (held 5) None dup
            Expect.equal payee "Markt" "Payee aus dem Ereignis"
            Expect.equal meta "01.11. · Girokonto · Lebensmittel" "Tag, Konto, Kategorie"
            Expect.stringContains amount "10,00" "Betrag"
            let resolved = { entryOf 12 with Payee = Some "Markt im Kassenbuch" }
            let payee', meta', _ = Components.Ledger.View.reviewMatchRow today name (held 5) (Some resolved) dup
            Expect.equal payee' "Markt im Kassenbuch" "aufgelöst"
            Expect.equal meta' "Heute · Girokonto · Lebensmittel" "Tag der aufgelösten Buchung"
    ]

// ============================================
// Aufzählung der Reihenfolgen (Doktrin Punkt 20)
// ============================================

/// Die Schlange: zuerst die wartende D, dann A (mit Kategorie), B (ohne), C (mit).
let private dupD = held 1
let private cardA = entryOf 2
let private cardB = withoutCategory (entryOf 3)
let private cardC = entryOf 4
let private queue = [ dupD; cardA; cardB; cardC ]
let private ids = queue |> List.map idOf

/// Eine Entscheidung auf der Duplikat-Karte, deren Antwort aussteht.
type private Decision =
    | DecideDiscard
    | DecideNew

/// Was das Orakel über das Durchgehen weiß, ohne den getesteten Code zu fragen.
type private Walk =
    { Index: int
      /// „Passt“ wartet auf die Antwort für die gezeigte Karte (sperrt ein zweites „Passt“).
      Pending: Guid option
      /// Gesendete Prüfvermerke, deren Antwort noch aussteht (auch nach dem Weitergehen), in Sendefolge.
      InFlight: Guid list
      /// Karten mit Ziel (Kategorie, Übertrag oder Aufteilung).
      Targets: Set<Guid>
      /// D wartet noch (vor „Ist neu“).
      DupHeld: bool
      /// Lena gefragt, „Rückgängig“ steht aus (jüngste zuerst).
      LenaUndo: Guid list
      /// Flag-Aufruf läuft (sperrt „Lena fragen“ auf der Karte).
      FlagSending: Set<Guid>
      /// „Ist neu“ oder „Verwerfen“ wartet auf die Antwort für die gezeigte Karte (sperrt beide Knöpfe).
      Deciding: Guid option
      /// Gesendete Entscheidungen, deren Antwort noch aussteht (auch nach dem Weitergehen).
      Decisions: (Guid * Decision) list
      /// D verworfen, „Rückgängig“ steht aus.
      DiscardUndo: bool }

let private startWalk =
    { Index = 0
      Pending = None
      InFlight = []
      Targets = set [ idOf cardA; idOf cardC ]
      DupHeld = true
      LenaUndo = []
      FlagSending = Set.empty
      Deciding = None
      Decisions = []
      DiscardUndo = false }

let private current (w: Walk) = if w.Index < ids.Length then Some ids.[w.Index] else None
let private isDup (w: Walk) = current w = Some (idOf dupD) && w.DupHeld
let private moveTo (index: int) (w: Walk) = { w with Index = index; Pending = None; Deciding = None }

/// Entfernt das erste Vorkommen von `x`.
let rec private removeFirst (x: 'a) (xs: 'a list) : 'a list =
    match xs with
    | [] -> []
    | y :: rest when y = x -> rest
    | y :: rest -> y :: removeFirst x rest

/// Alle Ereignisse, die im Zustand kommen können: Name, Nachrichten und das Orakel danach. Jede ausstehende
/// Antwort kann als nächste kommen, nicht nur die älteste: Ok und Fehler je gesendetem Aufruf.
let private events (w: Walk) : (string * Msg list * Walk) list =
    [ match current w with
      | Some id when not (isDup w) ->
          if w.Pending.IsNone && w.Targets.Contains id then
              "Passt", [ ReviewConfirm ], { w with Pending = Some id; InFlight = w.InFlight @ [ id ] }
          "Später", [ ReviewSkip ], moveTo (w.Index + 1) w
          if not (w.FlagSending.Contains id) then
              "Lena", [ ReviewLena ], { moveTo (w.Index + 1) w with LenaUndo = id :: w.LenaUndo; FlagSending = w.FlagSending.Add id }
          if not (w.Targets.Contains id) then
              "Kategorie", categorySet id, { w with Targets = w.Targets.Add id }
      | Some id ->
          "Später", [ ReviewSkip ], moveTo (w.Index + 1) w
          if w.Deciding.IsNone then
              "Verwerfen", [ ReviewDiscard ], { w with Deciding = Some id; Decisions = w.Decisions @ [ id, DecideDiscard ] }
              "Ist neu", [ ReviewConfirmNew ], { w with Deciding = Some id; Decisions = w.Decisions @ [ id, DecideNew ] }
      | None -> ()
      for id in List.distinct w.InFlight do
          let answered = { w with InFlight = removeFirst id w.InFlight; Pending = (if w.Pending = Some id then None else w.Pending) }
          $"Passt-Antwort {id}", [ reviewed id ], (if current w = Some id then moveTo (w.Index + 1) answered else answered)
          $"Passt-Fehler {id}", [ ReviewConfirmed (id, Error "Netz") ], answered
      for (id, decision) in List.distinct w.Decisions do
          let answered = { w with Decisions = removeFirst (id, decision) w.Decisions; Deciding = (if w.Deciding = Some id then None else w.Deciding) }
          match decision with
          | DecideDiscard ->
              "Verwerfen-Antwort",
              [ ReviewDiscarded (id, Ok ()) ],
              (let after = { answered with DiscardUndo = true }
               if current w = Some id then moveTo (w.Index + 1) after else after)
              "Verwerfen-Fehler", [ ReviewDiscarded (id, Error "Netz") ], answered
          | DecideNew ->
              "Ist-neu-Antwort", [ ReviewConfirmedNew (id, Ok PushToYnabSent) ], { answered with DupHeld = false }
              "Ist-neu-Fehler", [ ReviewConfirmedNew (id, Error "Netz") ], answered
      for id in w.LenaUndo do
          let back = ids |> List.findIndex ((=) id)
          let after = { w with LenaUndo = w.LenaUndo |> List.filter ((<>) id) }
          // „Rückgängig“ steht erst im Toast, wenn „Lena fragen“ geantwortet hat; solange der Aufruf läuft,
          // ist es gesperrt (Zeilen-Sperre, ledger-smv1s).
          let noFlag = { Color = None; Name = None }
          $"Lena-Rückgängig {id}",
          [ LenaFlagSet (id, AssignLena, Ok { Previous = noFlag; Change = FlagChanged; WriteBack = FlagSent })
            StartUndoFlag (id, noFlag) ],
          (if back <> w.Index then moveTo back after else after)
      if w.DiscardUndo then
          let back = ids |> List.findIndex ((=) (idOf dupD))
          let after = { w with DiscardUndo = false }
          "Verwerfen-Rückgängig",
          [ StartRestoreDiscardedDuplicate (idOf dupD); DiscardedDuplicateRestored (idOf dupD, Ok ()) ],
          (if back <> w.Index then moveTo back after else after)
          "Verwerfen-Rückgängig-Fehler",
          [ StartRestoreDiscardedDuplicate (idOf dupD); DiscardedDuplicateRestored (idOf dupD, Error "Netz") ],
          after ]

let private observe (model: Model) =
    let review = model.Review.Value
    review.Index, cardId model, review.Confirming, review.Deciding, (reviewCardOf model |> Option.map reviewCardKindOf)

let private expected (w: Walk) =
    w.Index,
    current w,
    w.Pending,
    w.Deciding,
    current w |> Option.map (fun id -> if id = idOf dupD && w.DupHeld then DuplicateCard else NormalCard)

[<Tests>]
let walkTests =
    testCase "Durchgehen: jede Reihenfolge bis Tiefe 6 landet auf der Karte, die das Orakel erwartet" <| fun () ->
        let mutable visited = 0
        let rec walk (depth: int) (path: string list) (model: Model) (w: Walk) =
            visited <- visited + 1
            Expect.equal (observe model) (expected w) (String.concat " → " (List.rev path))
            if depth > 0 then
                for name, msgs, w' in events w do
                    let model' = msgs |> List.fold (fun m msg -> step msg m) model
                    walk (depth - 1) (name :: path) model' w'
        walk 6 [] (reviewing queue) startWalk
        Expect.isGreaterThan visited 1000 "die Aufzählung läuft wirklich"

// ============================================
// Einzelfälle
// ============================================

[<Tests>]
let caseTests =
    testList "Durchgehen: Einzelfälle" [

        testCase "die Schlange startet mit der ersten Karte, lädt sie neu und holt die Vorschläge" <| fun () ->
            let model, _ = Components.Ledger.State.init ()
            let opened, msgs, _ = run ReviewOpened model
            Expect.contains msgs (RunLoadReviewQueue opened.ReviewGeneration) "Schlange laden"
            let loaded, msgs, _ = run (ReviewQueueLoaded (opened.ReviewGeneration, Ok [ cardA; cardB ])) opened
            Expect.equal (cardId loaded) (Some (idOf cardA)) "erste Karte"
            Expect.contains msgs (RunReviewRefresh (idOf cardA)) "neu lesen"
            Expect.contains msgs (RunReviewSuggestions (idOf cardA, "Payee v6s7c 2")) "Vorschläge"

        testCase "eine inzwischen geprüfte oder gelöschte Buchung wird beim Erreichen übersprungen" <| fun () ->
            let model = reviewing [ cardA; cardB ]
            let basis = model.DetailFor
            let skipped = model |> step (ReviewEntryRefreshed (idOf cardA, basis, Ok (Some { cardA with NeedsReview = false })))
            Expect.equal (cardId skipped) (Some (idOf cardB)) "geprüft: weiter"
            let gone = skipped |> step (ReviewEntryRefreshed (idOf cardB, skipped.DetailFor, Ok None))
            Expect.equal (indexOf gone) 2 "unbekannt: weiter zur Endansicht"
            Expect.isNone (reviewCardOf gone) "keine Karte"

        testCase "eine veraltete Antwort des Neulesens überschreibt die Karte nicht" <| fun () ->
            let model = reviewing [ cardB ] |> step (List.head (categorySet (idOf cardB)))
            let stale = model |> step (ReviewEntryRefreshed (idOf cardB, Some cardA, Ok (Some { cardB with Payee = Some "alt" })))
            Expect.equal (reviewCardOf stale |> Option.bind (fun e -> e.Payee)) cardB.Payee "unverändert"

        testCase "scheitert der Prüfvermerk, bleibt die Karte und ein Toast nennt den Grund" <| fun () ->
            let model = reviewing [ cardA; cardC ] |> step ReviewConfirm
            let failed, _, external = run (ReviewConfirmed (idOf cardA, Error "YNAB antwortet nicht")) model
            Expect.equal (cardId failed) (Some (idOf cardA)) "dieselbe Karte"
            Expect.equal external (ShowToast ("Nicht geprüft: YNAB antwortet nicht", ToastError)) "Grund"
            let ok, _, external = run (reviewed (idOf cardA)) (failed |> step ReviewConfirm)
            Expect.equal (cardId ok) (Some (idOf cardC)) "weiter"
            Expect.equal external NoOp "kein Toast"

        testCase "Wisch nach rechts ohne Ziel meldet es und die Karte bleibt" <| fun () ->
            let model = reviewing [ cardB; cardA ]
            let after, msgs, external = run (ReviewSwiped (120.0, 0.0)) model
            Expect.equal (cardId after) (Some (idOf cardB)) "bleibt"
            Expect.equal external (ShowToast (reviewNeedsTargetText, ToastInfo)) "Hinweis"
            Expect.isEmpty (msgs |> List.filter (function RunReviewConfirm _ -> true | _ -> false)) "kein Prüfvermerk"
            Expect.equal (cardId (step (ReviewSwiped (-120.0, 0.0)) model)) (Some (idOf cardA)) "nach links: Später"

        testCase "Wisch nach rechts mit Ziel setzt den Prüfvermerk" <| fun () ->
            let _, msgs, _ = run (ReviewSwiped (120.0, 0.0)) (reviewing [ cardA ])
            Expect.contains msgs (RunReviewConfirm (idOf cardA)) "Prüfvermerk"

        // Review-Fund zu ledger-v6s7c: ein schräger Scroll über die Karte setzte den Prüfvermerk.
        testCase "ein schräger Scroll über die Karte setzt keinen Prüfvermerk und lässt die Karte stehen" <| fun () ->
            let model = reviewing [ cardA; cardC ]
            let after, msgs, external = run (ReviewSwiped (120.0, 150.0)) model
            Expect.isEmpty (msgs |> List.filter (function RunReviewConfirm _ -> true | _ -> false)) "kein Prüfvermerk"
            Expect.equal (cardId after) (Some (idOf cardA)) "Karte bleibt"
            Expect.equal external NoOp "kein Hinweis"
            let _, _, back = run (ReviewSwiped (-120.0, -150.0)) model
            Expect.equal (cardId (step (ReviewSwiped (-120.0, -150.0)) model)) (Some (idOf cardA)) "auch nach links nichts"
            Expect.equal back NoOp "kein Hinweis"

        // Review-Fund zu ledger-v6s7c: die Antwort auf die Schlange war nur an `Review.IsSome` gebunden. Die späte
        // Antwort eines früheren Öffnens setzte die laufende Sitzung auf Karte 1 zurück.
        testCase "die späte Antwort eines früheren Öffnens setzt die laufende Sitzung nicht zurück" <| fun () ->
            let model, _ = Components.Ledger.State.init ()
            let first = model |> step ReviewOpened
            let second = first |> step ReviewOpened
            let walking =
                second
                |> step (ReviewQueueLoaded (second.ReviewGeneration, Ok [ cardA; cardC ]))
                |> step ReviewSkip
            Expect.equal (cardId walking) (Some (idOf cardC)) "zweite Karte"
            let late = walking |> step (ReviewQueueLoaded (first.ReviewGeneration, Ok [ cardB; cardA; cardC ]))
            Expect.equal (cardId late) (Some (idOf cardC)) "bleibt auf der zweiten Karte"
            Expect.equal (reviewQueueOf late) (Some [ idOf cardA; idOf cardC ]) "Schlange unverändert"
            let failed = walking |> step (ReviewQueueLoaded (first.ReviewGeneration, Error "Netz"))
            Expect.equal (reviewQueueOf failed) (Some [ idOf cardA; idOf cardC ]) "ein später Fehler auch nicht"
            // Verlassen und neu öffnen: die Antwort der verlassenen Sitzung zählt ebenso nicht.
            let reopened = walking |> step ReviewLeft |> step ReviewOpened
            let stale = reopened |> step (ReviewQueueLoaded (second.ReviewGeneration, Ok [ cardB ]))
            Expect.equal (stale.Review |> Option.map (fun r -> r.Queue)) (Some Loading) "lädt weiter"

        // Review-Fund zu ledger-v6s7c: fand `showReviewCardAt` keinen Stand der Buchung, blieb die Karte leer und der
        // Nachlauf in `update` stieß bei jeder Nachricht ein neues Neulesen an.
        testCase "eine Buchung ohne bekannten Stand wird übersprungen und nicht bei jeder Nachricht neu gelesen" <| fun () ->
            let model = reviewing [ cardA; cardC ]
            let unknown = Guid.Parse "0195f810-0000-7000-8000-0000000000ff"
            let broken =
                { model with
                    DetailFor = None
                    Review = model.Review |> Option.map (fun r -> { r with Queue = Success [ unknown; idOf cardC ]; Snapshots = r.Snapshots |> Map.remove (idOf cardA) }) }
            let after, msgs, _ = run (ReviewSuggestionsLoaded (Guid.NewGuid (), [])) broken
            Expect.equal (cardId after) (Some (idOf cardC)) "weiter zur nächsten bekannten Karte"
            Expect.isEmpty (msgs |> List.filter (function RunReviewRefresh id -> id = unknown | _ -> false)) "die unbekannte wird nicht gelesen"
            let _, again, _ = run (ReviewSuggestionsLoaded (Guid.NewGuid (), [])) after
            Expect.isEmpty (again |> List.filter (function RunReviewRefresh _ -> true | _ -> false)) "kein Neulesen ohne Anlass"

        testCase "Rückgängig nach dem Verwerfen meldet sich mit einem Toast und schickt nur einmal" <| fun () ->
            let model = reviewing [ dupD; cardA ] |> step ReviewDiscard |> step (ReviewDiscarded (idOf dupD, Ok ()))
            let started, msgs, _ = run (StartRestoreDiscardedDuplicate (idOf dupD)) model
            Expect.contains msgs (RunRestoreDiscardedDuplicate (idOf dupD)) "gesendet"
            let _, twice, _ = run (StartRestoreDiscardedDuplicate (idOf dupD)) started
            Expect.isEmpty twice "ein zweiter Tipp schickt nichts"
            let restored, _, external = run (DiscardedDuplicateRestored (idOf dupD, Ok ())) started
            Expect.equal external (ShowToast (reviewRestoredText, ToastSuccess)) "Toast"
            Expect.equal (cardId restored) (Some (idOf dupD)) "die Karte steht wieder da"
            let _, again, _ = run (StartRestoreDiscardedDuplicate (idOf dupD)) restored
            Expect.contains again (RunRestoreDiscardedDuplicate (idOf dupD)) "nach der Antwort wieder frei"
            let outside, _, external = run (DiscardedDuplicateRestored (idOf dupD, Ok ())) (started |> step ReviewLeft)
            Expect.equal external (ShowToast (reviewRestoredText, ToastSuccess)) "auch außerhalb des Durchgehens"
            Expect.isFalse (outside.RestoringDuplicates.Contains (idOf dupD)) "Sperre frei"
            let failed, _, external = run (DiscardedDuplicateRestored (idOf dupD, Error "Netz")) started
            Expect.equal external (ShowToast ("Rückgängig nicht möglich: Netz", ToastError)) "Fehler"
            Expect.isFalse (failed.RestoringDuplicates.Contains (idOf dupD)) "Sperre frei nach dem Fehler"

        testCase "Verwerfen geht weiter und bietet Rückgängig an, Ist neu meldet den Weg nach YNAB" <| fun () ->
            let model = reviewing [ dupD; cardA ] |> step ReviewDiscard
            let after, _, external = run (ReviewDiscarded (idOf dupD, Ok ())) model
            Expect.equal (cardId after) (Some (idOf cardA)) "weiter"
            Expect.equal
                external
                (ShowToastWithAction (reviewDiscardedText, ToastSuccess, { Label = "Rückgängig"; Kind = UndoDiscardDuplicate (idOf dupD) }))
                "Toast"
            let newOne = reviewing [ dupD; cardA ] |> step ReviewConfirmNew
            let confirmed, _, external = run (ReviewConfirmedNew (idOf dupD, Ok PushToYnabSent)) newOne
            Expect.equal external (ShowToast (reviewConfirmedNewText, ToastSuccess)) "Toast"
            Expect.equal (reviewCardOf confirmed |> Option.map reviewCardKindOf) (Some NormalCard) "normale Karte an derselben Stelle"
            let _, _, failed = run (ReviewConfirmedNew (idOf dupD, Ok (PushToYnabFailed "Timeout"))) newOne
            match failed with
            | ShowToast (text, ToastWarning) -> Expect.stringContains text "Timeout" "Grund"
            | other -> failtestf "Warnung erwartet: %A" other

        // ledger-smv1s: Die Antwort einer Entscheidung aus einem verlassenen Durchgehen löste die laufende
        // Entscheidung einer anderen Karte; ein zweites „Ist neu“ schickte einen zweiten Aufruf.
        testCase "die späte Antwort zu einer verlassenen Karte gibt die Entscheidung der neuen Karte nicht frei" <| fun () ->
            let dupE = held 5
            let answers =
                [ ReviewConfirmedNew (idOf dupD, Ok PushToYnabSent)
                  ReviewConfirmedNew (idOf dupD, Error "Netz")
                  ReviewDiscarded (idOf dupD, Ok ())
                  ReviewDiscarded (idOf dupD, Error "Netz") ]
            for answer in answers do
                let left = reviewing [ dupD ] |> step ReviewConfirmNew |> step ReviewLeft
                let reopened = left |> step ReviewOpened
                let deciding =
                    reopened
                    |> step (ReviewQueueLoaded (reopened.ReviewGeneration, Ok [ dupE ]))
                    |> step ReviewConfirmNew
                let after = step answer deciding
                Expect.equal (after.Review |> Option.bind (fun r -> r.Deciding)) (Some (idOf dupE)) $"{answer}: Entscheidung läuft weiter"
                let _, msgs, _ = run ReviewConfirmNew after
                Expect.isEmpty msgs $"{answer}: kein zweiter Aufruf"

        testCase "die Kategorie auf der Karte meldet Name und Verfügbares" <| fun () ->
            let model = reviewing [ cardB ]
            let msgs = categorySet (idOf cardB)
            let started = step msgs.Head model
            let _, _, external = run msgs.[1] started
            match external with
            | ShowToastWithAction (text, _, _) -> Expect.stringStarts text "Kategorie: Lebensmittel · " "Name"
            | other -> failtestf "Toast erwartet: %A" other

        testCase "Details öffnet das Detail über der Karte, Schließen lässt die Karte stehen und liest sie neu" <| fun () ->
            let model = reviewing [ cardA; cardC ]
            let opened, msgs, _ = run ReviewShowDetails model
            Expect.isTrue opened.Review.Value.DetailShown "offen"
            Expect.contains msgs (LoadHistory (idOf cardA)) "Historie"
            let closed, msgs, _ = run CloseDetail opened
            Expect.isFalse closed.Review.Value.DetailShown "zu"
            Expect.equal (cardId closed) (Some (idOf cardA)) "Karte bleibt"
            Expect.contains msgs (RunReviewRefresh (idOf cardA)) "neu lesen"

        testCase "Esc schließt erst den Picker, dann das Detail, dann die Ansicht" <| fun () ->
            let picking = reviewing [ cardA ] |> step (ReviewKey KeyOpenCategory)
            Expect.equal picking.CategoryPickerFor (Some (idOf cardA)) "K öffnet den Picker"
            let closed, _, external = run (ReviewKey KeyLeave) picking
            Expect.isNone closed.CategoryPickerFor "Picker zu"
            Expect.equal external NoOp "Ansicht bleibt"
            let details = closed |> step ReviewShowDetails
            let back, _, external = run (ReviewKey KeyLeave) details
            Expect.isFalse back.Review.Value.DetailShown "Detail zu"
            Expect.equal external NoOp "Ansicht bleibt"
            let _, _, external = run (ReviewKey KeyLeave) back
            Expect.equal external LeaveReview "zurück zur Übersicht"

        testCase "Verlassen schließt Karte und Detail" <| fun () ->
            let left = reviewing [ cardA ] |> step ReviewLeft
            Expect.isNone left.Review "kein Durchgehen"
            Expect.isNone left.DetailFor "kein Detail im Kassenbuch"

        testCase "die Endansicht kommt nach der letzten Karte, eine leere Schlange zeigt sie sofort" <| fun () ->
            let model = reviewing [ cardA ] |> step ReviewSkip
            Expect.equal (indexOf model) 1 "hinter der letzten"
            Expect.isNone (reviewCardOf model) "keine Karte"
            let empty = reviewing []
            Expect.equal (indexOf empty) 0 "Stelle 0"
            Expect.equal (reviewQueueOf empty) (Some []) "leer"
            Expect.isNone (reviewCardOf empty) "Endansicht"
    ]

// ============================================
// Gescheitertes „Lena fragen“ holt die Karte zurück (ledger-nc708)
// ============================================

let private lenaA = entryOf 5
let private lenaB = entryOf 6
let private lenaIds = [ idOf lenaA; idOf lenaB ]
let private lenaFailed (entryId: Guid) : Msg = LenaFlagSet (entryId, AssignLena, Error "Netz")

[<Tests>]
let lenaFailureTests =
    testList "Durchgehen: gescheitertes „Lena fragen“ (ledger-nc708)" [

        testCase "der Fehler holt Karte A zurück, Fehlerzeile und Toast wie außerhalb" <| fun () ->
            let asked = reviewing [ lenaA; lenaB ] |> step ReviewLena
            Expect.equal (cardId asked) (Some (idOf lenaB)) "nach „Lena fragen“ steht B"
            let failed, _, external = run (lenaFailed (idOf lenaA)) asked
            Expect.equal (cardId failed) (Some (idOf lenaA)) "Karte A ist wieder sichtbar"
            Expect.equal (failed.FlagRows |> Map.tryFind (idOf lenaA)) (Some (FlagCommandFailed "Netz")) "Fehler an A"
            Expect.equal external (ShowToast ("Flag nicht geändert: Netz", ToastError)) "Toast"

        testCase "außerhalb des Durchgehens ändert der Fehler nur Zeile und Toast" <| fun () ->
            let model, _ = Components.Ledger.State.init ()
            let ledger = { model with Entries = Success [ lenaA; lenaB ]; LenaFlagColor = Success (Some "purple") }
            let sending = ledger |> step (StartSetLenaFlag (idOf lenaA, AssignLena))
            let failed, msgs, external = run (lenaFailed (idOf lenaA)) sending
            Expect.equal failed { sending with FlagRows = sending.FlagRows |> Map.add (idOf lenaA) (FlagCommandFailed "Netz") } "nur die Zeile"
            Expect.isEmpty msgs "keine Folgenachricht"
            Expect.equal external (ShowToast ("Flag nicht geändert: Netz", ToastError)) "Toast"

        testCase "nach dem Verlassen passiert am Durchgehen nichts" <| fun () ->
            let left = reviewing [ lenaA; lenaB ] |> step ReviewLena |> step ReviewLeft
            let failed = left |> step (lenaFailed (idOf lenaA))
            Expect.isNone failed.Review "kein Durchgehen"
            Expect.isNone failed.DetailFor "kein Detail"

        testCase "nach Erfolg bleibt B sichtbar" <| fun () ->
            let asked = reviewing [ lenaA; lenaB ] |> step ReviewLena
            let ok = LenaFlagSet (idOf lenaA, AssignLena, Ok { Previous = { Color = None; Name = None }; Change = FlagChanged; WriteBack = FlagSent })
            Expect.equal (cardId (asked |> step ok)) (Some (idOf lenaB)) "B bleibt"
    ]

/// Was das Orakel zu „Lena im Durchgehen“ weiß: Stelle, ob „Lena fragen“ für A läuft, ob „Passt“ für B läuft.
type private LenaWalk = { At: int; LenaSent: bool; ConfirmSent: bool }

let private lenaCurrent (w: LenaWalk) = if w.At < lenaIds.Length then Some lenaIds.[w.At] else None

let private lenaEvents (w: LenaWalk) : (string * Msg list * LenaWalk) list =
    [ match lenaCurrent w with
      | Some id ->
          if id = idOf lenaA && not w.LenaSent then
              "Lena A", [ ReviewLena ], { w with At = w.At + 1; LenaSent = true }
          if id = idOf lenaB && not w.ConfirmSent then
              "Passt B", [ ReviewConfirm ], { w with ConfirmSent = true }
          "Später", [ ReviewSkip ], { w with At = w.At + 1 }
      | None -> ()
      if w.ConfirmSent then
          let after = { w with ConfirmSent = false }
          "Passt-Antwort B", [ reviewed (idOf lenaB) ], (if lenaCurrent w = Some (idOf lenaB) then { after with At = w.At + 1 } else after)
      if w.LenaSent then
          "Lena-Fehler A", [ lenaFailed (idOf lenaA) ], { w with LenaSent = false; At = 0 } ]

[<Tests>]
let lenaWalkTests =
    testCase "Lena im Durchgehen: jede Reihenfolge bis Tiefe 6, der Fehler für A holt A zurück, auch aus der Endansicht" <| fun () ->
        let mutable visited = 0
        let mutable fromEnd = 0
        let rec walk (depth: int) (path: string list) (model: Model) (w: LenaWalk) =
            visited <- visited + 1
            let shown = String.concat " → " (List.rev path)
            Expect.equal (indexOf model, cardId model) (w.At, lenaCurrent w) shown
            if depth > 0 then
                for name, msgs, w' in lenaEvents w do
                    if name = "Lena-Fehler A" && w.At >= lenaIds.Length then fromEnd <- fromEnd + 1
                    let model' = msgs |> List.fold (fun m msg -> step msg m) model
                    walk (depth - 1) (name :: path) model' w'
        walk 6 [] (reviewing [ lenaA; lenaB ]) { At = 0; LenaSent = false; ConfirmSent = false }
        Expect.isGreaterThan visited 100 "die Aufzählung läuft wirklich"
        Expect.isGreaterThan fromEnd 0 "der Fehler kommt auch in der Endansicht an"
