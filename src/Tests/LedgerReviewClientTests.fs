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

/// Was die Zeile der letzten Aktion zeigt: Ton, Aktion, zweiter Teil, Knopf (ledger-pz5wv).
let private lineOf (model: Model) =
    model.Review
    |> Option.bind (fun r -> r.Last)
    |> Option.map (fun last ->
        let line = reviewLastLine last
        line.Tone, line.Action, line.Detail, line.Button)

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

        // ledger-hme9k Punkt 5: das Detail-Sheet am Handy steht immer im DOM, offen ist es nur mit Karte und „Details“.
        // Aus scripts/regel-aus-buchung.test.mjs gezogen.
        testCase "das Detail-Sheet im Durchgehen ist nur mit Karte und offenen Details offen" <| fun () ->
            let session detailShown = { reviewStarting with DetailShown = detailShown }
            for card, detailShown, expected, label in
                [ Some (entryOf 1), true, true, "Karte, Details offen"
                  Some (entryOf 1), false, false, "Karte, Details zu"
                  None, true, false, "Endansicht, Details offen"
                  None, false, false, "Endansicht, Details zu" ] do
                Expect.equal (reviewDetailSheetOpen card (session detailShown)) expected label
            // Über das echte `update`: „Details“ öffnet, Schließen schließt.
            let opened = step ReviewShowDetails (reviewing [ entryOf 2 ])
            Expect.isTrue (reviewDetailSheetOpen (reviewCardOf opened) opened.Review.Value) "nach Details offen"
            let closed = step CloseDetail opened
            Expect.isFalse (reviewDetailSheetOpen (reviewCardOf closed) closed.Review.Value) "nach Schließen zu"

        // ledger-hme9k: am PC lag der Dialog „Memo“ über dem offenen Memo-Feld im Panel, zwei Editoren mit demselben
        // Entwurf. Mit offenen „Details“ öffnet der Payee nur im Detail; das Memo öffnet seit ledger-wg42w nur dort.
        // Aus scripts/layout-states.mjs gezogen (`assertOneMemoEditor`).
        testCase "der Payee öffnet bei offenen Details im Detail, sonst über der Karte" <| fun () ->
            for detailShown, expected in [ true, ReviewEditorPlace.InDetail; false, ReviewEditorPlace.OverCard ] do
                Expect.equal
                    (reviewEditorPlace { reviewStarting with DetailShown = detailShown })
                    expected
                    $"DetailShown = {detailShown}"
            let opened = step ReviewShowDetails (reviewing [ entryOf 2 ])
            Expect.equal (reviewEditorPlace opened.Review.Value) ReviewEditorPlace.InDetail "nach Details im Detail"
            let closed = step CloseDetail opened
            Expect.equal (reviewEditorPlace closed.Review.Value) ReviewEditorPlace.OverCard "nach Schließen über der Karte"

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

        // ledger-s2cg3: die Karte folgt dem Finger 1:1; ohne Ziel nach rechts ab 72 px zäh (72 + Rest × 0,12).
        testCase "die Karte folgt dem Finger 1:1, ohne Ziel nach rechts ab 72 Pixeln zäh" <| fun () ->
            Expect.equal (reviewSwipeOffset true 150.0) 150.0 "mit Ziel rechts 1:1"
            Expect.equal (reviewSwipeOffset true -150.0) -150.0 "mit Ziel links 1:1"
            Expect.equal (reviewSwipeOffset false 72.0) 72.0 "ohne Ziel bis 72 px 1:1"
            Expect.floatClose Accuracy.high (reviewSwipeOffset false 172.0) 84.0 "ohne Ziel danach zäh: 72 + 100 × 0,12"
            Expect.equal (reviewSwipeOffset false -150.0) -150.0 "ohne Ziel nach links 1:1 (Später)"
            Expect.equal (reviewSwipeOffset false 0.0) 0.0 "kein Weg"

        // ledger-wg42w: das Memo auf der Karte wird nur gekürzt, wenn die Karte überläuft, um so viele Zeilen, wie der
        // Überlauf braucht, mindestens eine Zeile bleibt (Skizze ledger-vf5h6 `fitMemo`).
        testCase "das Memo der Karte kürzt nur bei Überlauf, um den Überlauf, mindestens auf eine Zeile" <| fun () ->
            let clamp = Client.DesignSystem.ReviewCard.memoClampLines
            Expect.equal (clamp 96.0 0.0 24.0) None "kein Überlauf: ganz"
            Expect.equal (clamp 96.0 -30.0 24.0) None "Platz übrig: ganz"
            Expect.equal (clamp 96.0 10.0 24.0) (Some 3) "Überlauf kleiner als eine Zeile: eine Zeile weniger"
            Expect.equal (clamp 96.0 24.0 24.0) (Some 3) "Überlauf genau eine Zeile: eine Zeile weniger"
            Expect.equal (clamp 96.0 25.0 24.0) (Some 2) "etwas mehr als eine Zeile: zwei weniger"
            Expect.equal (clamp 96.0 200.0 24.0) (Some 1) "Überlauf größer als das Memo: eine Zeile"
            Expect.equal (clamp 24.0 24.0 24.0) (Some 1) "einzeiliges Memo bei Überlauf: eine Zeile"

        // ledger-s2cg3: eingerastet genau dann, wenn `reviewSwipeOf` beim Loslassen entschiede, nach rechts nur mit Ziel.
        testCase "die Fläche rastet an der Grenze von reviewSwipeOf ein, nach rechts nur mit Ziel" <| fun () ->
            Expect.isTrue (reviewSwipeArmed true 91.0 0.0) "rechts über 90 mit Ziel"
            Expect.isFalse (reviewSwipeArmed true 90.0 0.0) "genau 90 nicht"
            Expect.isTrue (reviewSwipeArmed true -91.0 0.0) "links über 90"
            Expect.isFalse (reviewSwipeArmed true -90.0 0.0) "genau -90 nicht"
            Expect.isFalse (reviewSwipeArmed false 200.0 0.0) "ohne Ziel rechts nie"
            Expect.isTrue (reviewSwipeArmed false -91.0 0.0) "ohne Ziel links wie Später"
            Expect.isFalse (reviewSwipeArmed true 120.0 150.0) "schräg, mehr senkrecht: nicht"
            Expect.isTrue (reviewSwipeArmed true 120.0 -40.0) "leicht schräg: eingerastet"

        // ledger-s2cg3: die Fläche unter der Karte je Richtung; ohne Ziel nach rechts nur „!“, das nie einrastet.
        testCase "die Fläche zeigt je Richtung ihr Symbol, ohne Ziel nach rechts nur das Ausrufezeichen" <| fun () ->
            let surface = Components.Ledger.Types.reviewSwipeSurface
            Expect.equal (surface true 0.0 0.0) Client.DesignSystem.ReviewCard.SurfaceRest "ohne Weg nichts"
            Expect.equal (surface true 40.0 0.0) (Client.DesignSystem.ReviewCard.SurfaceConfirm false) "rechts unter der Grenze"
            Expect.equal (surface true 91.0 0.0) (Client.DesignSystem.ReviewCard.SurfaceConfirm true) "rechts eingerastet"
            Expect.equal (surface false 40.0 0.0) Client.DesignSystem.ReviewCard.SurfaceNoTarget "ohne Ziel rechts"
            Expect.equal (surface false 200.0 0.0) Client.DesignSystem.ReviewCard.SurfaceNoTarget "ohne Ziel rechts weit: kein Einrasten"
            Expect.equal (surface false -40.0 0.0) (Client.DesignSystem.ReviewCard.SurfaceSkip false) "links unter der Grenze"
            Expect.equal (surface true -91.0 0.0) (Client.DesignSystem.ReviewCard.SurfaceSkip true) "links eingerastet"

        // ledger-s2cg3: Tasten und Knöpfe bewegen die Karte wie ein Wisch; „Rückfrage zurücknehmen“ lässt sie stehen.
        testCase "S fliegt nach links mit Fläche, L nur beim Fragen ohne Fläche, Enter und Zurücknehmen nicht" <| fun () ->
            let leaveOf = Components.Ledger.View.reviewLeaveOf
            let model = reviewing [ entryOf 1 ]
            Expect.equal (leaveOf model KeySkip) (Some Client.DesignSystem.ReviewCard.LeaveSkip) "S"
            Expect.equal (leaveOf model KeyLena) (Some Client.DesignSystem.ReviewCard.LeaveQuiet) "L fragt Lena"
            Expect.equal (leaveOf model KeyConfirm) None "Enter fliegt über Confirming"
            Expect.equal (leaveOf model KeyOpenCategory) None "K"
            let asked = reviewing [ { entryOf 1 with Flag = AskLena } ]
            Expect.equal (leaveOf asked KeyLena) None "Rückfrage zurücknehmen: Karte bleibt"
            let noColor = { model with LenaFlagColor = Success None }
            Expect.equal (leaveOf noColor KeyLena) None "gesperrt: Karte bleibt"
            Expect.equal (leaveOf (reviewing []) KeySkip) None "ohne Karte nichts"

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
                Expect.equal (reviewKeyAction card false key) expected $"{key} auf {card}"

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

        // ledger-hg5m5: Über dem Regel-Formular (Dialog am PC) gelten die Tasten des Durchgehens nicht; ohne die Sperre
        // ginge die Karte weiter und das Formular schlösse mit dem Entwurf. Esc führt eine Stufe zurück zur Karte.
        testCase "über dem offenen Regel-Formular wirken die Tasten des Durchgehens nicht, Esc führt zur Karte zurück" <| fun () ->
            let model = reviewing [ entryOf 1 ]
            let route = Components.Ledger.View.reviewKeyRoute
            for k in [ "Enter"; "ArrowRight"; "s"; "l"; "k" ] do
                Expect.isNone (route model true "BUTTON" false false k) $"{k} bei offenem Formular"
            Expect.equal (route model true "BUTTON" false false "Escape") (Some RuleFormBack) "Esc"
            Expect.equal (route model false "BODY" false false "Enter") (Some (PageAction KeyConfirm)) "ohne Formular"
            Expect.equal (route model false "BODY" false false "Escape") (Some (PageAction KeyLeave)) "Esc ohne Formular"

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

/// Was „Rückgängig“ in der Zeile der letzten Aktion zurücknimmt (ledger-pz5wv), aus Sicht des Orakels.
type private WalkUndo =
    | WConfirm of Guid
    | WSkip of Guid
    | WLena of Guid
    | WDiscard

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
      /// Lena gefragt, die Antwort steht aus (jüngste zuerst).
      LenaAsked: Guid list
      /// Lena wurde für die Karte gefragt (sperrt ein zweites „Lena fragen“ im Orakel).
      FlagSending: Set<Guid>
      /// „Ist neu“ oder „Verwerfen“ wartet auf die Antwort für die gezeigte Karte (sperrt beide Knöpfe).
      Deciding: Guid option
      /// Gesendete Entscheidungen, deren Antwort noch aussteht (auch nach dem Weitergehen).
      Decisions: (Guid * Decision) list
      /// Das Rückgängig, das die Zeile anbietet.
      LastUndo: WalkUndo option
      /// Die Rücknahme nach „Passt“ läuft für diese Buchung (sperrt „Passt“, das Neulesen überspringt sie nicht).
      Restoring: Guid option }

let private startWalk =
    { Index = 0
      Pending = None
      InFlight = []
      Targets = set [ idOf cardA; idOf cardC ]
      DupHeld = true
      LenaAsked = []
      FlagSending = Set.empty
      Deciding = None
      Decisions = []
      LastUndo = None
      Restoring = None }

let private current (w: Walk) = if w.Index < ids.Length then Some ids.[w.Index] else None
let private isDup (w: Walk) = current w = Some (idOf dupD) && w.DupHeld
let private moveTo (index: int) (w: Walk) = { w with Index = index; Pending = None; Deciding = None }
/// Zurück zur Karte `id` (Rückgängig); steht sie schon da, bleibt alles.
let private backTo (id: Guid) (w: Walk) =
    let back = ids |> List.findIndex ((=) id)
    if back <> w.Index then moveTo back w else w

/// Entfernt das erste Vorkommen von `x`.
let rec private removeFirst (x: 'a) (xs: 'a list) : 'a list =
    match xs with
    | [] -> []
    | y :: rest when y = x -> rest
    | y :: rest -> y :: removeFirst x rest

let private always (msgs: Msg list) (_: Model) = msgs
let private unreviewed = { IsApproved = false; ClearedState = ReviewUncleared }

/// Alle Ereignisse, die im Zustand kommen können: Name, Nachrichten (aus dem Modell, für das Neulesen) und das Orakel
/// danach. Jede ausstehende Antwort kann als nächste kommen, nicht nur die älteste: Ok und Fehler je gesendetem Aufruf.
/// Seit ledger-pz5wv geht „Rückgängig“ über die Zeile der letzten Aktion: nach Passt, Später, Lena und Verwerfen.
let private events (w: Walk) : (string * (Model -> Msg list) * Walk) list =
    [ match current w with
      | Some id when not (isDup w) ->
          if w.Pending.IsNone && w.Targets.Contains id && w.Restoring <> Some id then
              "Passt", always [ ReviewConfirm ], { w with Pending = Some id; InFlight = w.InFlight @ [ id ]; LastUndo = None }
          "Später", always [ ReviewSkip ], { moveTo (w.Index + 1) w with LastUndo = Some (WSkip id) }
          if not (w.FlagSending.Contains id) then
              "Lena",
              always [ ReviewLena ],
              { moveTo (w.Index + 1) w with LenaAsked = id :: w.LenaAsked; FlagSending = w.FlagSending.Add id; LastUndo = None }
          if not (w.Targets.Contains id) then
              "Kategorie", always (categorySet id), { w with Targets = w.Targets.Add id }
      | Some id ->
          "Später", always [ ReviewSkip ], { moveTo (w.Index + 1) w with LastUndo = Some (WSkip id) }
          if w.Deciding.IsNone then
              "Verwerfen", always [ ReviewDiscard ], { w with Deciding = Some id; Decisions = w.Decisions @ [ id, DecideDiscard ]; LastUndo = None }
              "Ist neu", always [ ReviewConfirmNew ], { w with Deciding = Some id; Decisions = w.Decisions @ [ id, DecideNew ]; LastUndo = None }
      | None -> ()
      for id in List.distinct w.InFlight do
          let answered = { w with InFlight = removeFirst id w.InFlight; Pending = (if w.Pending = Some id then None else w.Pending) }
          $"Passt-Antwort {id}",
          always [ reviewed id ],
          { (if current w = Some id then moveTo (w.Index + 1) answered else answered) with LastUndo = Some (WConfirm id) }
          $"Passt-Fehler {id}", always [ ReviewConfirmed (id, Error "Netz") ], { answered with LastUndo = None }
      for (id, decision) in List.distinct w.Decisions do
          let answered = { w with Decisions = removeFirst (id, decision) w.Decisions; Deciding = (if w.Deciding = Some id then None else w.Deciding) }
          match decision with
          | DecideDiscard ->
              "Verwerfen-Antwort",
              always [ ReviewDiscarded (id, Ok ()) ],
              (let after = { answered with LastUndo = Some WDiscard }
               if current w = Some id then moveTo (w.Index + 1) after else after)
              "Verwerfen-Fehler", always [ ReviewDiscarded (id, Error "Netz") ], { answered with LastUndo = None }
          | DecideNew ->
              "Ist-neu-Antwort", always [ ReviewConfirmedNew (id, Ok PushToYnabSent) ], { answered with DupHeld = false; LastUndo = None }
              "Ist-neu-Fehler", always [ ReviewConfirmedNew (id, Error "Netz") ], { answered with LastUndo = None }
      for id in w.LenaAsked do
          let noFlag = { Color = None; Name = None }
          $"Lena-Antwort {id}",
          always [ LenaFlagSet (id, AssignLena, Ok { Previous = noFlag; Change = FlagChanged; WriteBack = FlagSent }) ],
          { w with LenaAsked = w.LenaAsked |> List.filter ((<>) id); LastUndo = Some (WLena id) }
      match w.LastUndo with
      | Some (WSkip id) -> "Später-Rückgängig", always [ ReviewUndoLast ], { backTo id w with LastUndo = None }
      // Die Karte kommt sofort zurück, die Rücknahme läuft (ledger-pz5wv).
      | Some (WConfirm id) -> "Passt-Rückgängig", always [ ReviewUndoLast ], { backTo id w with LastUndo = None; Restoring = Some id }
      | Some (WLena id) -> "Lena-Rückgängig", always [ ReviewUndoLast ], { backTo id w with LastUndo = None }
      | Some WDiscard ->
          "Verwerfen-Rückgängig",
          always [ ReviewUndoLast; DiscardedDuplicateRestored (idOf dupD, Ok ()) ],
          { backTo (idOf dupD) w with LastUndo = None }
          "Verwerfen-Rückgängig-Fehler", always [ ReviewUndoLast; DiscardedDuplicateRestored (idOf dupD, Error "Netz") ], { w with LastUndo = None }
      | None -> ()
      match w.Restoring with
      | Some id ->
          "Rücknahme-Antwort",
          always [ ReviewMarkRestored (id, unreviewed, Ok { Previous = { IsApproved = true; ClearedState = ReviewUncleared }; Change = ReviewMarkChanged; WriteBack = ReviewMarkSent }) ],
          { w with Restoring = None }
          // Der Fehler ersetzt die Zeile (rot mit „Erneut“), auch wenn inzwischen eine andere Aktion in ihr stand.
          "Rücknahme-Fehler", always [ ReviewMarkRestored (id, unreviewed, Error "Netz") ], { w with Restoring = None; LastUndo = None }
          // Das Neulesen der zurückgeholten Karte kommt vor der Antwort der Rücknahme: der Server meldet sie noch als
          // geprüft, sie wird trotzdem nicht übersprungen.
          if current w = Some id then
              "Neulesen-noch-geprüft",
              (fun m -> [ ReviewEntryRefreshed (id, m.DetailFor, Ok (m.DetailFor |> Option.map (fun e -> { e with NeedsReview = false }))) ]),
              w
      | None ->
          // Nach der Antwort der Rücknahme meldet das Neulesen sie wieder als zu prüfen: sie bleibt.
          match current w with
          | Some id when not (isDup w) ->
              "Neulesen-offen",
              (fun m -> [ ReviewEntryRefreshed (id, m.DetailFor, Ok (m.DetailFor |> Option.map (fun e -> { e with NeedsReview = true }))) ]),
              w
          | _ -> () ]

/// Das Rückgängig der Zeile, wie das Orakel es sieht.
let private offeredUndo (model: Model) : WalkUndo option =
    model.Review
    |> Option.bind reviewUndoOffered
    |> Option.map (function
        | UndoConfirmed (id, _) -> WConfirm id
        | UndoSkipped id -> WSkip id
        | UndoLenaAsked (id, _) -> WLena id
        | UndoDiscarded _ -> WDiscard
        | UndoPayeeChanged _ -> failwith "kein Payee im Orakel")

let private observe (model: Model) =
    let review = model.Review.Value
    review.Index, cardId model, review.Confirming, review.Deciding, (reviewCardOf model |> Option.map reviewCardKindOf), offeredUndo model

let private expected (w: Walk) =
    w.Index,
    current w,
    w.Pending,
    w.Deciding,
    current w |> Option.map (fun id -> if id = idOf dupD && w.DupHeld then DuplicateCard else NormalCard),
    w.LastUndo

[<Tests>]
let walkTests =
    testCase "Durchgehen: jede Reihenfolge bis Tiefe 6 landet auf der Karte, die das Orakel erwartet, auch nach Rückgängig" <| fun () ->
        let mutable visited = 0
        let mutable undone = Set.empty
        let rec walk (depth: int) (path: string list) (model: Model) (w: Walk) =
            visited <- visited + 1
            Expect.equal (observe model) (expected w) (String.concat " → " (List.rev path))
            if depth > 0 then
                for name, msgs, w' in events w do
                    if name.EndsWith "-Rückgängig" || name.StartsWith "Neulesen" then undone <- undone.Add name
                    let model' = msgs model |> List.fold (fun m msg -> step msg m) model
                    walk (depth - 1) (name :: path) model' w'
        walk 6 [] (reviewing queue) startWalk
        Expect.isGreaterThan visited 1000 "die Aufzählung läuft wirklich"
        for name in [ "Passt-Rückgängig"; "Später-Rückgängig"; "Lena-Rückgängig"; "Verwerfen-Rückgängig"; "Neulesen-noch-geprüft"; "Neulesen-offen" ] do
            Expect.contains undone name $"{name} kommt vor"

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

        // ledger-pz5wv: der Fehler steht rot in der Zeile unter „Passt, weiter“, nicht mehr als Toast.
        testCase "scheitert der Prüfvermerk, bleibt die Karte und die Zeile nennt rot den Grund" <| fun () ->
            let model = reviewing [ cardA; cardC ] |> step ReviewConfirm
            let failed, _, external = run (ReviewConfirmed (idOf cardA, Error "YNAB antwortet nicht")) model
            Expect.equal (cardId failed) (Some (idOf cardA)) "dieselbe Karte"
            Expect.equal external NoOp "kein Toast"
            Expect.equal (lineOf failed) (Some (FeedbackError, "Nicht geprüft", "YNAB antwortet nicht", Some "Erneut")) "Zeile"
            let ok, _, external = run (reviewed (idOf cardA)) (failed |> step ReviewConfirm)
            Expect.equal (cardId ok) (Some (idOf cardC)) "weiter"
            Expect.equal external NoOp "kein Toast"

        // ledger-s2cg3: ohne Ziel steht „Erst Kategorie wählen“ in der Zeile unter dem Knopf, kein Toast mehr.
        testCase "Wisch nach rechts ohne Ziel lässt die Karte stehen und meldet nichts nach außen" <| fun () ->
            let model = reviewing [ cardB; cardA ]
            let after, msgs, external = run (ReviewSwiped (120.0, 0.0)) model
            Expect.equal (cardId after) (Some (idOf cardB)) "bleibt"
            Expect.equal external NoOp "kein externer Befehl"
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
            let after, msgs, _ = run (ReviewSuggestionsLoaded (Guid.NewGuid (), Ok [])) broken
            Expect.equal (cardId after) (Some (idOf cardC)) "weiter zur nächsten bekannten Karte"
            Expect.isEmpty (msgs |> List.filter (function RunReviewRefresh id -> id = unknown | _ -> false)) "die unbekannte wird nicht gelesen"
            let _, again, _ = run (ReviewSuggestionsLoaded (Guid.NewGuid (), Ok [])) after
            Expect.isEmpty (again |> List.filter (function RunReviewRefresh _ -> true | _ -> false)) "kein Neulesen ohne Anlass"

        // ledger-pz5wv: „Rückgängig“ steht in der Zeile; die Karte steht sichtbar wieder da, kein Toast mehr.
        testCase "Rückgängig nach dem Verwerfen holt die Karte ohne Toast zurück und schickt nur einmal" <| fun () ->
            let model = reviewing [ dupD; cardA ] |> step ReviewDiscard |> step (ReviewDiscarded (idOf dupD, Ok ()))
            let started, msgs, _ = run ReviewUndoLast model
            Expect.contains msgs (RunRestoreDiscardedDuplicate (idOf dupD)) "gesendet"
            Expect.isNone (lineOf started) "die Zeile zeigt wieder den Hinweis"
            let _, twice, _ = run (StartRestoreDiscardedDuplicate (idOf dupD)) started
            Expect.isEmpty twice "ein zweiter Tipp schickt nichts"
            let restored, _, external = run (DiscardedDuplicateRestored (idOf dupD, Ok ())) started
            Expect.equal external NoOp "kein Toast"
            Expect.equal (cardId restored) (Some (idOf dupD)) "die Karte steht wieder da"
            Expect.equal (indexOf restored) 0 "an ihrer Stelle"
            let _, again, _ = run (StartRestoreDiscardedDuplicate (idOf dupD)) restored
            Expect.contains again (RunRestoreDiscardedDuplicate (idOf dupD)) "nach der Antwort wieder frei"
            let outside, _, external = run (DiscardedDuplicateRestored (idOf dupD, Ok ())) (started |> step ReviewLeft)
            Expect.equal external (ShowToast (reviewRestoredText, ToastSuccess)) "außerhalb des Durchgehens der Toast"
            Expect.isFalse (outside.RestoringDuplicates.Contains (idOf dupD)) "Sperre frei"
            let failed, _, external = run (DiscardedDuplicateRestored (idOf dupD, Error "Netz")) started
            Expect.equal external NoOp "Fehler ohne Toast"
            Expect.equal (lineOf failed) (Some (FeedbackError, "Rückgängig nicht möglich", "Netz", Some "Erneut")) "rot in der Zeile"
            Expect.isFalse (failed.RestoringDuplicates.Contains (idOf dupD)) "Sperre frei nach dem Fehler"
            let _, retried, _ = run ReviewRetryLast failed
            Expect.contains retried (RunRestoreDiscardedDuplicate (idOf dupD)) "Erneut schickt die Rücknahme noch einmal"

        // ledger-pz5wv: Verwerfen und Ist neu melden sich in der Zeile, Ist neu ohne Rückgängig (Entscheidung Roman 3).
        testCase "Verwerfen geht weiter und bietet Rückgängig in der Zeile an, Ist neu meldet sich ohne Rückgängig" <| fun () ->
            let model = reviewing [ dupD; cardA ] |> step ReviewDiscard
            let after, _, external = run (ReviewDiscarded (idOf dupD, Ok ())) model
            Expect.equal (cardId after) (Some (idOf cardA)) "weiter"
            Expect.equal external NoOp "kein Toast"
            Expect.equal (lineOf after) (Some (FeedbackOk, "Verworfen", "Payee v6s7c 1", Some "Rückgängig")) "Zeile"
            let newOne = reviewing [ dupD; cardA ] |> step ReviewConfirmNew
            let confirmed, _, external = run (ReviewConfirmedNew (idOf dupD, Ok PushToYnabSent)) newOne
            Expect.equal external NoOp "kein Toast"
            Expect.equal (lineOf confirmed) (Some (FeedbackOk, "Als neu übernommen", "Payee v6s7c 1", None)) "ohne Rückgängig"
            Expect.equal (reviewCardOf confirmed |> Option.map reviewCardKindOf) (Some NormalCard) "normale Karte an derselben Stelle"
            let warned, _, external = run (ReviewConfirmedNew (idOf dupD, Ok (PushToYnabFailed "Timeout"))) newOne
            Expect.equal external NoOp "auch die Warnung ohne Toast"
            Expect.equal (lineOf warned) (Some (FeedbackWarning, "Als neu übernommen, nicht in YNAB", "Timeout", None)) "gelb mit Grund"
            // Nach dem Verlassen bleibt der Toast (es gibt keine Zeile mehr).
            let _, _, outside = run (ReviewConfirmedNew (idOf dupD, Ok PushToYnabSent)) (newOne |> step ReviewLeft)
            Expect.equal outside (ShowToast (reviewConfirmedNewText, ToastSuccess)) "Toast außerhalb"

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

        // ledger-mf2bg: die Bestätigung steht am Feld, nicht mehr im Toast.
        testCase "die Kategorie auf der Karte meldet Name und Verfügbares" <| fun () ->
            let model = reviewing [ cardB ]
            let msgs = categorySet (idOf cardB)
            let started = step msgs.Head model
            let after, _, external = run msgs.[1] started
            Expect.equal external NoOp "kein Toast"
            let note = after.Review.Value.FieldNote
            Expect.equal (note |> Option.map (fun n -> n.Text)) (Some "Lebensmittel") "Name"
            Expect.equal (note |> Option.bind (fun n -> n.Available)) (Some "110,00 €") "Verfügbares"

        testCase "Details öffnet das Detail über der Karte, Schließen lässt die Karte stehen und liest sie neu" <| fun () ->
            let model = reviewing [ cardA; cardC ]
            let opened, msgs, _ = run ReviewShowDetails model
            Expect.isTrue opened.Review.Value.DetailShown "offen"
            Expect.contains msgs (LoadHistory (idOf cardA)) "Historie"
            let closed, msgs, _ = run CloseDetail opened
            Expect.isFalse closed.Review.Value.DetailShown "zu"
            Expect.equal (cardId closed) (Some (idOf cardA)) "Karte bleibt"
            Expect.contains msgs (RunReviewRefresh (idOf cardA)) "neu lesen"

        // Review-Fund zu ledger-enjex: „Details“ schließen übergab eine offene Bestätigung mit Rückgängig an Leiste und
        // PC-Toast (`handOverDetailNote`). Im Durchgehen gibt es keinen Toast (ledger-pz5wv Abschnitt 5): sie gehört in die
        // Zeile unter „Passt, weiter“, Rückgängig dort schickt dieselbe Rücknahme wie am Feld.
        testCase "Details schließen mit offener Bestätigung: sie wandert in die Zeile unter dem Knopf, nicht in die Leiste" <| fun () ->
            let details = reviewing [ cardA; cardC ] |> step ReviewShowDetails
            let changedInDetails =
                details
                |> step (StartSetEntryCategory (idOf cardA, Some (YnabCategoryId food)))
                |> step (EntryCategorySet (idOf cardA, Some (YnabCategoryId food), Ok { Previous = None; Change = CategoryChanged; WriteBack = CategorySent; PreviousTransferAccountId = None }))
            Expect.isSome changedInDetails.DetailNote "Bestätigung am Feld im Detail"
            let closed, _, external = run CloseDetail changedInDetails
            Expect.equal external NoOp "keine Leiste, kein Toast"
            Expect.isNone closed.DetailNote "am Feld weg"
            Expect.equal (lineOf closed) (Some (FeedbackOk, "Kategorie geändert", "Payee v6s7c 2", Some "Rückgängig")) "Zeile unter dem Knopf"
            let _, msgs, undoExternal = run ReviewUndoLast closed
            Expect.equal undoExternal NoOp "Rückgängig meldet nichts nach außen"
            Expect.contains msgs (RunUndoCategory (idOf cardA, None)) "dieselbe Rücknahme wie am Feld"

        testCase "Kartenwechsel mit offenen Details: die Zeile der neuen Aktion bleibt, die Bestätigung endet still" <| fun () ->
            let details = reviewing [ cardA; cardC ] |> step ReviewShowDetails
            let changedInDetails =
                details
                |> step (StartSetEntryCategory (idOf cardA, Some (YnabCategoryId food)))
                |> step (EntryCategorySet (idOf cardA, Some (YnabCategoryId food), Ok { Previous = None; Change = CategoryChanged; WriteBack = CategorySent; PreviousTransferAccountId = None }))
            let skipped, _, external = run ReviewSkip changedInDetails
            Expect.equal external NoOp "keine Leiste, kein Toast"
            Expect.isNone skipped.DetailNote "Bestätigung weg"
            Expect.equal (lineOf skipped |> Option.map (fun (_, action, _, _) -> action)) (Some "Später") "die Zeile meldet „Später“"

        // ledger-hme9k Befund 4: „Details“ schließen setzte die Felder des Details von Hand und vergaß die
        // aufgeklappte Historie; beim nächsten „Details“ stand sie schon offen da.
        testCase "„Details“ schließen klappt die Historie zu, beim nächsten Öffnen steht sie zugeklappt da" <| fun () ->
            let opened = reviewing [ cardA; cardC ] |> step ReviewShowDetails |> step ToggleHistory
            Expect.equal opened.HistoryOpenFor (Some (idOf cardA)) "aufgeklappt"
            let reopened = opened |> step CloseDetail |> step ReviewShowDetails
            Expect.isNone reopened.HistoryOpenFor "zugeklappt"

        testCase "„Details“ schließen leert alles Offene im Detail, die Karte bleibt und wird neu gelesen" <| fun () ->
            let opened =
                reviewing [ cardA; cardC ]
                |> step ReviewShowDetails
                |> step (StartMemoEdit (idOf cardA))
            Expect.isSome opened.MemoEdit "Memo offen"
            let withEdits =
                { opened with
                    AmountEdit = Some { AmountEntryId = idOf cardA; AmountDraft = "1"; AmountIsOutflow = true; AmountFieldError = None }
                    PayeeEdit = Some { PayeeEntryId = idOf cardA; PayeeFieldError = None }
                    CategoryPickerFor = Some (idOf cardA) }
            let closed, msgs, _ = run CloseDetail withEdits
            Expect.equal (closed.DetailFor |> Option.map idOf) (Some (idOf cardA)) "DetailFor bleibt die Karte"
            Expect.equal (cardId closed) (Some (idOf cardA)) "Karte A sichtbar"
            Expect.isNone closed.MemoEdit "Memo"
            Expect.isNone closed.AmountEdit "Betrag"
            Expect.isNone closed.PayeeEdit "Payee"
            Expect.isNone closed.SplitEditor "Aufteilen"
            Expect.isNone closed.CategoryPickerFor "Kategorie"
            Expect.contains msgs (RunReviewRefresh (idOf cardA)) "neu lesen"

        testCase "im Kassenbuch schließt CloseDetail das Detail ganz" <| fun () ->
            let model, _ = Components.Ledger.State.init ()
            let model = { model with Entries = Success [ cardA ]; DetailFor = Some cardA; HistoryOpenFor = Some (idOf cardA) }
            let closed = step CloseDetail model
            Expect.isNone closed.DetailFor "kein Detail"
            Expect.isNone closed.HistoryOpenFor "Historie zu"

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

        // ledger-pz5wv: der Fehler steht rot in der Zeile mit „Erneut“ statt als Toast.
        testCase "der Fehler holt Karte A zurück, Fehlerzeile an A und rot in der Zeile" <| fun () ->
            let asked = reviewing [ lenaA; lenaB ] |> step ReviewLena
            Expect.equal (cardId asked) (Some (idOf lenaB)) "nach „Lena fragen“ steht B"
            let failed, _, external = run (lenaFailed (idOf lenaA)) asked
            Expect.equal (cardId failed) (Some (idOf lenaA)) "Karte A ist wieder sichtbar"
            Expect.equal (failed.FlagRows |> Map.tryFind (idOf lenaA)) (Some (FlagCommandFailed "Netz")) "Fehler an A"
            Expect.equal external NoOp "kein Toast"
            Expect.equal (lineOf failed) (Some (FeedbackError, "Flag nicht geändert", "Netz", Some "Erneut")) "Zeile"

        // Regression Review a3f7eb1d..982bdf79 (ledger-nc708): der Sprung zurück lief über `closeDetail` und
        // verwarf das offene Memo auf Karte B samt Entwurf. Seit ledger-wg42w öffnet das Memo nur über „Details“.
        testCase "steht auf Karte B eine Eingabe offen, bleibt B samt Entwurf, Fehlerzeile und Zeile wie sonst" <| fun () ->
            let editing =
                reviewing [ lenaA; lenaB ]
                |> step ReviewLena
                |> step ReviewShowDetails
                |> step (StartMemoEdit (idOf lenaB))
                |> step (MemoEditChanged "Entwurf")
            let failed, msgs, external = run (lenaFailed (idOf lenaA)) editing
            Expect.equal (cardId failed) (Some (idOf lenaB)) "Karte B bleibt"
            Expect.equal failed.MemoEdit (Some { MemoEntryId = idOf lenaB; MemoDraft = "Entwurf" }) "der Entwurf bleibt offen"
            Expect.equal (failed.FlagRows |> Map.tryFind (idOf lenaA)) (Some (FlagCommandFailed "Netz")) "Fehler an A"
            Expect.isEmpty msgs "kein Neulesen einer Karte"
            Expect.equal external NoOp "kein Toast"
            Expect.equal (lineOf failed) (Some (FeedbackError, "Flag nicht geändert", "Netz", Some "Erneut")) "rot in der Zeile"

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

/// Was das Orakel zu „Lena im Durchgehen“ weiß: Stelle, ob „Lena fragen“ für A läuft, ob „Passt“ für B läuft,
/// ob auf B das Memo mit Entwurf offen steht.
type private LenaWalk = { At: int; LenaSent: bool; ConfirmSent: bool; MemoOpen: bool }

let private lenaCurrent (w: LenaWalk) = if w.At < lenaIds.Length then Some lenaIds.[w.At] else None

let private lenaEvents (w: LenaWalk) : (string * Msg list * LenaWalk) list =
    [ match lenaCurrent w with
      | Some id ->
          if id = idOf lenaA && not w.LenaSent then
              "Lena A", [ ReviewLena ], { w with At = w.At + 1; LenaSent = true }
          if id = idOf lenaB && not w.ConfirmSent then
              "Passt B", [ ReviewConfirm ], { w with ConfirmSent = true }
          if id = idOf lenaB && not w.MemoOpen then
              "Memo B", [ ReviewShowDetails; StartMemoEdit (idOf lenaB); MemoEditChanged "Entwurf" ], { w with MemoOpen = true }
          "Später", [ ReviewSkip ], { w with At = w.At + 1; MemoOpen = false }
      | None -> ()
      if w.ConfirmSent then
          let after = { w with ConfirmSent = false }
          "Passt-Antwort B", [ reviewed (idOf lenaB) ], (if lenaCurrent w = Some (idOf lenaB) then { after with At = w.At + 1; MemoOpen = false } else after)
      if w.LenaSent then
          // Ein offenes Memo hält die Karte fest (Review a3f7eb1d..982bdf79).
          "Lena-Fehler A", [ lenaFailed (idOf lenaA) ], (if w.MemoOpen then { w with LenaSent = false } else { w with LenaSent = false; At = 0 }) ]

[<Tests>]
let lenaWalkTests =
    testCase "Lena im Durchgehen: jede Reihenfolge bis Tiefe 6, der Fehler für A holt A zurück, auch aus der Endansicht, ein offenes Memo hält B fest" <| fun () ->
        let mutable visited = 0
        let mutable fromEnd = 0
        let mutable heldByMemo = 0
        let rec walk (depth: int) (path: string list) (model: Model) (w: LenaWalk) =
            visited <- visited + 1
            let shown = String.concat " → " (List.rev path)
            Expect.equal (indexOf model, cardId model) (w.At, lenaCurrent w) shown
            let expectedMemo = if w.MemoOpen then Some { MemoEntryId = idOf lenaB; MemoDraft = "Entwurf" } else None
            Expect.equal model.MemoEdit expectedMemo $"Memo: {shown}"
            if depth > 0 then
                for name, msgs, w' in lenaEvents w do
                    if name = "Lena-Fehler A" && w.At >= lenaIds.Length then fromEnd <- fromEnd + 1
                    if name = "Lena-Fehler A" && w.MemoOpen then heldByMemo <- heldByMemo + 1
                    let model' = msgs |> List.fold (fun m msg -> step msg m) model
                    walk (depth - 1) (name :: path) model' w'
        walk 6 [] (reviewing [ lenaA; lenaB ]) { At = 0; LenaSent = false; ConfirmSent = false; MemoOpen = false }
        Expect.isGreaterThan visited 100 "die Aufzählung läuft wirklich"
        Expect.isGreaterThan fromEnd 0 "der Fehler kommt auch in der Endansicht an"
        Expect.isGreaterThan heldByMemo 0 "der Fehler kommt auch bei offenem Memo auf B an"

/// ledger-pz028: Vorschläge und Treffer kommen als `Result`, ein Fehler entscheidet sich im `update`.
[<Tests>]
let answerResultTests =
    testList "Durchgehen: Antworten mit Result (ledger-pz028)" [
        testCase "scheitern die Vorschläge, bleiben sie leer" <| fun () ->
            let model = reviewing [ cardA ]
            let suggestionsOf (m: Model) = m.Review |> Option.map (fun r -> r.Suggestions)
            let loaded = model |> step (ReviewSuggestionsLoaded (idOf cardA, Ok [ YnabCategoryId food ]))
            Expect.equal (suggestionsOf loaded) (Some [ YnabCategoryId food ]) "Ok übernimmt die Vorschläge"
            let failed = loaded |> step (ReviewSuggestionsLoaded (idOf cardA, Error "x"))
            Expect.equal (suggestionsOf failed) (Some []) "Error: leer"

        testCase "scheitert das Lesen des Treffers, bleibt der Schnappschuss aus dem Ereignis" <| fun () ->
            let model = reviewing [ dupD; cardA ]
            let matchId = idOf cardC
            let matchesOf (m: Model) = m.Review |> Option.map (fun r -> r.Matches |> Map.tryFind matchId)
            let failed = model |> step (ReviewMatchLoaded (matchId, Error "x"))
            Expect.equal (matchesOf failed) (Some None) "kein gelesener Treffer, die Karte zeigt das Ereignis"
            let found = model |> step (ReviewMatchLoaded (matchId, Ok (Some cardC)))
            Expect.equal (matchesOf found) (Some (Some cardC)) "Ok übernimmt den Treffer"
    ]

// ============================================
// Zeile der letzten Aktion unter „Passt, weiter“ (ledger-pz5wv)
// ============================================

let private reviewMark (approved: bool) : ReviewMarkState = { IsApproved = approved; ClearedState = ReviewUncleared }
let private noFlag : FlagState = { Color = None; Name = None }
let private lenaSet (entryId: Guid) (writeBack: FlagWriteBack) : Msg =
    LenaFlagSet (entryId, AssignLena, Ok { Previous = noFlag; Change = FlagChanged; WriteBack = writeBack })
let private hasMsg (pick: Msg -> bool) (msgs: Msg list) = msgs |> List.exists pick

[<Tests>]
let lastLineTests =
    testList "Durchgehen: Zeile der letzten Aktion (ledger-pz5wv)" [

        testCase "jede Aktion hat ihren Text, Warnungen und Fehler tragen den Grund" <| fun () ->
            let id = idOf cardA
            let line kind outcome act = reviewLastLine { EntryId = id; Kind = kind; Payee = Some "REWE"; Outcome = outcome; Act = act }
            let undo = Some (LastUndo (UndoSkipped id))
            let retry = Some (LastRetry (RetryConfirm id))
            let cases =
                [ line LastConfirmed LastDone undo, (FeedbackOk, "Passt", "REWE", Some "Rückgängig")
                  line LastConfirmed (LastNotInYnab "Zeitüberschreitung") undo, (FeedbackWarning, "Passt, nicht in YNAB", "Zeitüberschreitung", Some "Rückgängig")
                  line LastSkipped LastDone undo, (FeedbackOk, "Später", "REWE", Some "Rückgängig")
                  line LastLenaAsked LastDone undo, (FeedbackOk, "Lena gefragt", "REWE", Some "Rückgängig")
                  line LastLenaAsked (LastNotInYnab "Timeout") undo, (FeedbackWarning, "Lena gefragt, nicht in YNAB", "Timeout", Some "Rückgängig")
                  line LastDiscarded LastDone undo, (FeedbackOk, "Verworfen", "REWE", Some "Rückgängig")
                  line LastConfirmedNew LastDone None, (FeedbackOk, "Als neu übernommen", "REWE", None)
                  line LastConfirmedNew (LastNotInYnab "Timeout") None, (FeedbackWarning, "Als neu übernommen, nicht in YNAB", "Timeout", None)
                  line LastPayeeChanged LastDone undo, (FeedbackOk, "Payee geändert", "REWE", Some "Rückgängig")
                  line LastPayeeChanged (LastNotInYnab "Timeout") undo, (FeedbackWarning, "Payee geändert, nicht in YNAB", "Timeout", Some "Rückgängig")
                  line LastConfirmed (LastFailed "Netz") retry, (FeedbackError, "Nicht geprüft", "Netz", Some "Erneut")
                  line LastConfirmedNew (LastFailed "Netz") retry, (FeedbackError, "Nicht übernommen", "Netz", Some "Erneut")
                  line LastDiscarded (LastFailed "Netz") retry, (FeedbackError, "Nicht verworfen", "Netz", Some "Erneut")
                  line LastLenaAsked (LastFailed "Netz") retry, (FeedbackError, "Flag nicht geändert", "Netz", Some "Erneut")
                  line LastPayeeChanged (LastFailed "Netz") retry, (FeedbackError, "Payee nicht geändert", "Netz", Some "Erneut")
                  line LastUndone (LastFailed "Netz") retry, (FeedbackError, "Rückgängig nicht möglich", "Netz", Some "Erneut") ]
            for actual, (tone, action, detail, button) in cases do
                Expect.equal (actual.Tone, actual.Action, actual.Detail, actual.Button) (tone, action, detail, button) action
            let noPayee = reviewLastLine { EntryId = id; Kind = LastConfirmed; Payee = None; Outcome = LastDone; Act = undo }
            Expect.equal noPayee.Detail "ohne Payee" "ohne Payee"

        testCase "Z nimmt die letzte Aktion zurück, nur mit Rückgängig, auch in der Endansicht" <| fun () ->
            let normal = Some (NormalCard, true)
            Expect.equal (reviewKeyAction normal true "z") (Some KeyUndo) "mit Rückgängig"
            Expect.equal (reviewKeyAction normal true "Z") (Some KeyUndo) "groß"
            Expect.equal (reviewKeyAction normal false "z") None "ohne Rückgängig nichts"
            Expect.equal (reviewKeyAction None true "z") (Some KeyUndo) "Endansicht"
            Expect.equal (reviewKeyAction None false "z") None "Endansicht ohne Rückgängig"
            Expect.equal (reviewKeyAction (Some (DuplicateCard, false)) true "z") (Some KeyUndo) "Duplikat-Karte"
            let skipped = reviewing [ cardA; cardC ] |> step ReviewSkip
            let key = Components.Ledger.View.reviewKeyFor
            Expect.equal (key skipped "BODY" false false "z") (Some KeyUndo) "nach Später"
            Expect.equal (key skipped "INPUT" false false "z") None "nicht aus einem Eingabefeld"
            Expect.equal (key skipped "BODY" false true "z") None "nicht mit Strg"
            Expect.equal (key (reviewing [ cardA ]) "BODY" false false "z") None "ohne letzte Aktion nichts"
            let atEnd = reviewing [ cardA ] |> step ReviewSkip
            Expect.equal (key atEnd "BODY" false false "z") (Some KeyUndo) "Endansicht"
            let back = atEnd |> step (ReviewKey KeyUndo)
            Expect.equal (cardId back) (Some (idOf cardA)) "Z holt die Karte zurück"

        testCase "Passt schreibt die Zeile ohne Toast, gelb mit Grund, wenn YNAB den Prüfvermerk nicht nimmt" <| fun () ->
            let confirming = reviewing [ cardA; cardC ] |> step ReviewConfirm
            let ok, _, external = run (reviewed (idOf cardA)) confirming
            Expect.equal external NoOp "kein Toast"
            Expect.equal (lineOf ok) (Some (FeedbackOk, "Passt", "Payee v6s7c 2", Some "Rückgängig")) "Zeile"
            let warned, _, external =
                run (ReviewConfirmed (idOf cardA, Ok { Previous = reviewMark false; Change = ReviewMarkChanged; WriteBack = ReviewMarkSendFailed "Zeitüberschreitung" })) confirming
            Expect.equal external NoOp "auch die Warnung ohne Toast"
            Expect.equal (lineOf warned) (Some (FeedbackWarning, "Passt, nicht in YNAB", "Zeitüberschreitung", Some "Rückgängig")) "gelb"
            Expect.equal (cardId warned) (Some (idOf cardC)) "weiter"
            // Eine neue Aktion nimmt die Zeile weg, die nächste Antwort setzt die neue.
            let next = ok |> step ReviewConfirm
            Expect.isNone (lineOf next) "„Passt“ auf der nächsten Karte leert die Zeile"

        testCase "Passt rückgängig holt die Karte von rechts zurück, schickt den Vorzustand, die Zeile zeigt den Hinweis" <| fun () ->
            let previous = { IsApproved = true; ClearedState = ReviewUncleared }
            let confirmed =
                reviewing [ cardA; cardC ]
                |> step ReviewConfirm
                |> step (ReviewConfirmed (idOf cardA, Ok { Previous = previous; Change = ReviewMarkChanged; WriteBack = ReviewMarkSent }))
            Expect.equal (cardId confirmed) (Some (idOf cardC)) "C steht"
            let undone, msgs, external = run ReviewUndoLast confirmed
            Expect.equal (cardId undone) (Some (idOf cardA)) "A ist zurück"
            Expect.equal (indexOf undone) 0 "i von n auf A"
            Expect.equal undone.Review.Value.ReturnFrom (Some ReturnFromRight) "von rechts"
            Expect.isNone (lineOf undone) "Hinweis"
            Expect.contains msgs (RunUndoReviewMark (idOf cardA, previous)) "genau der Vorzustand"
            Expect.equal external NoOp "kein Toast"
            let restored, _, external =
                run (ReviewMarkRestored (idOf cardA, previous, Ok { Previous = reviewMark true; Change = ReviewMarkChanged; WriteBack = ReviewMarkSent })) undone
            Expect.equal external NoOp "kein Toast nach der Rücknahme"
            Expect.equal (cardId restored) (Some (idOf cardA)) "A bleibt"
            Expect.isNone (lineOf restored) "weiter der Hinweis"
            let warned, _, external =
                run (ReviewMarkRestored (idOf cardA, previous, Ok { Previous = reviewMark true; Change = ReviewMarkChanged; WriteBack = ReviewMarkSendFailed "Timeout" })) undone
            Expect.equal external NoOp "Warnung ohne Toast"
            Expect.equal (lineOf warned) (Some (FeedbackWarning, "Rückgängig, nicht in YNAB", "Timeout", None)) "gelb in der Zeile"

        testCase "scheitert die Rücknahme nach Passt, bleibt die Karte, rot „Rückgängig nicht möglich“, Erneut schickt sie noch einmal" <| fun () ->
            let previous = reviewMark false
            let undone =
                reviewing [ cardA; cardC ]
                |> step ReviewConfirm
                |> step (reviewed (idOf cardA))
                |> step ReviewUndoLast
            let failed, _, external = run (ReviewMarkRestored (idOf cardA, previous, Error "Netz")) undone
            Expect.equal external NoOp "kein Toast"
            Expect.equal (cardId failed) (Some (idOf cardA)) "Karte bleibt"
            Expect.equal (lineOf failed) (Some (FeedbackError, "Rückgängig nicht möglich", "Netz", Some "Erneut")) "rot"
            let retried, msgs, _ = run ReviewRetryLast failed
            Expect.contains msgs (RunUndoReviewMark (idOf cardA, previous)) "dieselbe Rücknahme"
            Expect.isNone (lineOf retried) "die rote Zeile geht"
            // Außerhalb des Durchgehens meldet sich der gemeinsame Handler wie im Kassenbuch (Gegentest Prüfvermerk).
            let outside, _ = Components.Ledger.State.init ()
            let _, _, external = run (ReviewMarkRestored (idOf cardA, previous, Error "Netz")) outside
            Expect.equal external (PostFeedback (undoFailedFeedback "Netz")) "Meldung außerhalb"

        testCase "Erneut nach gescheitertem Passt schickt den Prüfvermerk für dieselbe Karte noch einmal" <| fun () ->
            let failed = reviewing [ cardA; cardC ] |> step ReviewConfirm |> step (ReviewConfirmed (idOf cardA, Error "Netz"))
            let retried, msgs, _ = run ReviewRetryLast failed
            Expect.contains msgs (RunReviewConfirm (idOf cardA)) "Prüfvermerk"
            Expect.equal retried.Review.Value.Confirming (Some (idOf cardA)) "läuft"
            Expect.isNone (lineOf retried) "die rote Zeile geht"

        testCase "Später schreibt die Zeile, Rückgängig holt die Karte von links ohne Aufruf zurück" <| fun () ->
            let skipped, _, external = run ReviewSkip (reviewing [ cardA; cardC ])
            Expect.equal external NoOp "kein Toast"
            Expect.equal (lineOf skipped) (Some (FeedbackOk, "Später", "Payee v6s7c 2", Some "Rückgängig")) "Zeile"
            let back, msgs, _ = run ReviewUndoLast skipped
            Expect.equal (cardId back) (Some (idOf cardA)) "A ist zurück"
            Expect.equal (indexOf back) 0 "i von n"
            Expect.equal back.Review.Value.ReturnFrom (Some ReturnFromLeft) "von links"
            Expect.isNone (lineOf back) "Hinweis"
            Expect.isFalse (msgs |> hasMsg (function RunUndoReviewMark _ | RunUndoFlag _ -> true | _ -> false)) "kein Aufruf"
            let _, again, _ = run ReviewUndoLast back
            Expect.isEmpty again "nur eine Stufe"
            Expect.equal (cardId (step ReviewUndoLast back)) (Some (idOf cardA)) "ein zweites Rückgängig tut nichts"

        testCase "eine offene Eingabe auf der Karte hält sie fest, Rückgängig tut dann nichts" <| fun () ->
            let editing =
                reviewing [ cardA; cardC ]
                |> step ReviewSkip
                |> step ReviewShowDetails
                |> step (StartMemoEdit (idOf cardC))
            let after, msgs, _ = run ReviewUndoLast editing
            Expect.equal (cardId after) (Some (idOf cardC)) "C bleibt"
            Expect.isSome after.MemoEdit "Entwurf bleibt"
            Expect.isEmpty msgs "nichts geschickt"
            Expect.isSome (lineOf after) "die Zeile bleibt"

        testCase "Lena fragen meldet sich in der Zeile, Rückgängig holt die Karte und schickt das Flag zurück, ohne Toast" <| fun () ->
            let asked = reviewing [ lenaA; lenaB ] |> step ReviewLena
            let answered, _, external = run (lenaSet (idOf lenaA) FlagSent) asked
            Expect.equal external NoOp "kein Toast"
            Expect.equal (lineOf answered) (Some (FeedbackOk, "Lena gefragt", "Payee v6s7c 5", Some "Rückgängig")) "Zeile"
            let warned, _, _ = run (lenaSet (idOf lenaA) (FlagSendFailed "Timeout")) asked
            Expect.equal (lineOf warned) (Some (FeedbackWarning, "Lena gefragt, nicht in YNAB", "Timeout", Some "Rückgängig")) "gelb"
            let undone, msgs, _ = run ReviewUndoLast answered
            Expect.equal (cardId undone) (Some (idOf lenaA)) "A ist zurück"
            Expect.equal undone.Review.Value.ReturnFrom (Some ReturnFromLeft) "von links"
            Expect.contains msgs (RunUndoFlag (idOf lenaA, noFlag)) "Flag zurück"
            let restored, _, external = run (FlagRestored (idOf lenaA, noFlag, Ok { Previous = noFlag; Change = FlagChanged; WriteBack = FlagSent })) undone
            Expect.equal external NoOp "kein Toast"
            Expect.isNone (lineOf restored) "Hinweis"
            let failed, _, external = run (FlagRestored (idOf lenaA, noFlag, Error "Netz")) undone
            Expect.equal external NoOp "Fehler ohne Toast"
            Expect.equal (lineOf failed) (Some (FeedbackError, "Rückgängig nicht möglich", "Netz", Some "Erneut")) "rot"
            let _, again, _ = run ReviewRetryLast failed
            Expect.contains again (RunUndoFlag (idOf lenaA, noFlag)) "Erneut"

        testCase "Erneut nach gescheitertem Lena fragen schickt das Flag für dieselbe Karte noch einmal" <| fun () ->
            let failed = reviewing [ lenaA; lenaB ] |> step ReviewLena |> step (lenaFailed (idOf lenaA))
            let retried, msgs, _ = run ReviewRetryLast failed
            Expect.contains msgs (RunSetLenaFlag (idOf lenaA, AssignLena)) "dasselbe Flag"
            Expect.equal (cardId retried) (Some (idOf lenaB)) "die Karte geht wieder weiter"

        testCase "Rückfrage zurücknehmen schreibt keine Zeile und meldet keinen Toast, die vorige Zeile bleibt" <| fun () ->
            let flagged = { lenaB with Flag = AskLena }
            let skipped = reviewing [ lenaA; flagged ] |> step ReviewSkip
            let before = lineOf skipped
            let withdrawing, msgs, _ = run ReviewLena skipped
            Expect.contains msgs (RunSetLenaFlag (idOf flagged, WithdrawLena)) "Rücknahme"
            Expect.equal (cardId withdrawing) (Some (idOf flagged)) "Karte bleibt"
            let answer = LenaFlagSet (idOf flagged, WithdrawLena, Ok { Previous = { Color = Some "purple"; Name = Some "Lena" }; Change = FlagChanged; WriteBack = FlagSent })
            let answered, _, external = run answer withdrawing
            Expect.equal external NoOp "kein Toast"
            Expect.equal (lineOf answered) before "die Zeile von „Später“ bleibt"

        testCase "Verwerfen und Ist neu scheitern rot in der Zeile, Erneut schickt dieselbe Entscheidung" <| fun () ->
            let discardFailed = reviewing [ dupD; cardA ] |> step ReviewDiscard |> step (ReviewDiscarded (idOf dupD, Error "Netz"))
            Expect.equal (lineOf discardFailed) (Some (FeedbackError, "Nicht verworfen", "Netz", Some "Erneut")) "Verwerfen"
            Expect.equal (cardId discardFailed) (Some (idOf dupD)) "Karte bleibt"
            let _, msgs, _ = run ReviewRetryLast discardFailed
            Expect.contains msgs (RunReviewDiscard (idOf dupD)) "Erneut verwirft"
            let newFailed, _, external = run (ReviewConfirmedNew (idOf dupD, Error "Netz")) (reviewing [ dupD; cardA ] |> step ReviewConfirmNew)
            Expect.equal external NoOp "kein Toast"
            Expect.equal (lineOf newFailed) (Some (FeedbackError, "Nicht übernommen", "Netz", Some "Erneut")) "Ist neu"
            let _, msgs, _ = run ReviewRetryLast newFailed
            Expect.contains msgs (RunReviewConfirmNew (idOf dupD)) "Erneut übernimmt"

        testCase "der Payee auf der Karte meldet sich in der Zeile mit Rückgängig, Fehler bei geschlossener Auswahl rot mit Erneut" <| fun () ->
            let editing = reviewing [ cardA; cardC ] |> step (OpenPayeeEdit (idOf cardA))
            Expect.isSome editing.PayeeEdit "Auswahl über der Karte offen"
            let sending, msgs, _ = run (StartSavePayee (idOf cardA, Some "Bäckerei Lange")) editing
            Expect.contains msgs (RunSetPayee (idOf cardA, Some "Bäckerei Lange")) "gesendet"
            let outcome = { PreviousPayee = cardA.Payee; CurrentPayee = Some "Bäckerei Lange"; PayeeChange = PayeeChanged; PayeeWriteBack = PayeeSent }
            let saved, _, external = run (PayeeSet (idOf cardA, Ok outcome)) sending
            Expect.equal external NoOp "kein Toast"
            Expect.equal (lineOf saved) (Some (FeedbackOk, "Payee geändert", "Bäckerei Lange", Some "Rückgängig")) "neuer Payee"
            let undone, msgs, _ = run ReviewUndoLast saved
            Expect.contains msgs (RunUndoPayee (idOf cardA, cardA.Payee)) "voriger Payee"
            let restored, _, external =
                run (PayeeRestored (idOf cardA, Ok { outcome with PreviousPayee = Some "Bäckerei Lange"; CurrentPayee = cardA.Payee })) undone
            Expect.equal external NoOp "kein Toast"
            Expect.isNone (lineOf restored) "Hinweis"
            let warned, _, _ = run (PayeeSet (idOf cardA, Ok { outcome with PayeeWriteBack = PayeeSendFailed "Timeout" })) sending
            Expect.equal (lineOf warned) (Some (FeedbackWarning, "Payee geändert, nicht in YNAB", "Timeout", Some "Rückgängig")) "gelb"
            // Offene Auswahl: der Fehler steht am Feld (Styleguide §5), nicht in der Zeile.
            let atField, _, external = run (PayeeSet (idOf cardA, Error "Netz")) sending
            Expect.equal external NoOp "kein Toast"
            Expect.isNone (lineOf atField) "keine Zeile, der Fehler steht am Feld"
            Expect.equal (atField.PayeeEdit |> Option.bind (fun e -> e.PayeeFieldError)) (Some "Netz") "am Feld"
            let closed, _, external = run (PayeeSet (idOf cardA, Error "Netz")) { sending with PayeeEdit = None }
            Expect.equal external NoOp "kein Toast"
            Expect.equal (lineOf closed) (Some (FeedbackError, "Payee nicht geändert", "Netz", Some "Erneut")) "rot"
            let _, msgs, _ = run ReviewRetryLast closed
            Expect.contains msgs (RunSetPayee (idOf cardA, Some "Bäckerei Lange")) "Erneut schickt denselben Payee"

        // ledger-enjex: in „Details“ gilt die Bestätigung am Feld, kein Toast und keine Zeile der Karte.
        testCase "der Payee in „Details“ schreibt keine Zeile, die Bestätigung steht unter „Payee“" <| fun () ->
            let editing = reviewing [ cardA; cardC ] |> step ReviewShowDetails |> step (OpenPayeeEdit (idOf cardA))
            let sending = editing |> step (StartSavePayee (idOf cardA, Some "Bäckerei Lange"))
            let outcome = { PreviousPayee = cardA.Payee; CurrentPayee = Some "Bäckerei Lange"; PayeeChange = PayeeChanged; PayeeWriteBack = PayeeSent }
            let saved, _, external = run (PayeeSet (idOf cardA, Ok outcome)) sending
            Expect.isNone (lineOf saved) "keine Zeile"
            Expect.equal external NoOp "kein Toast"
            Expect.equal (saved.DetailNote |> Option.map (fun n -> n.Field, n.Text)) (Some (AtPayee, "Geändert")) "am Feld im Detail"

        testCase "außerhalb des Durchgehens bleibt der Toast nach Lena fragen (Gegentest Flag)" <| fun () ->
            let model, _ = Components.Ledger.State.init ()
            let ledger = { model with Entries = Success [ lenaA ]; LenaFlagColor = Success (Some "purple") }
            let sending = ledger |> step (StartSetLenaFlag (idOf lenaA, AssignLena))
            let _, _, external = run (lenaSet (idOf lenaA) FlagSent) sending
            match external with
            | ShowToastWithAction (_, ToastSuccess, { Kind = UndoFlag (id, _) }) -> Expect.equal id (idOf lenaA) "Toast mit Rückgängig"
            | other -> failtestf "Toast erwartet: %A" other

        testCase "die Endansicht bietet „Letzte rückgängig“ über die Zeile an und holt die letzte Karte zurück" <| fun () ->
            let atEnd = reviewing [ cardA ] |> step ReviewConfirm |> step (reviewed (idOf cardA))
            Expect.isNone (reviewCardOf atEnd) "Endansicht"
            Expect.isSome (reviewUndoOffered atEnd.Review.Value) "Rückgängig angeboten"
            let back = atEnd |> step ReviewUndoLast
            Expect.equal (cardId back) (Some (idOf cardA)) "A ist zurück"
            let newOnly = reviewing [ dupD ] |> step ReviewConfirmNew |> step (ReviewConfirmedNew (idOf dupD, Ok PushToYnabSent))
            Expect.isNone (newOnly.Review |> Option.bind reviewUndoOffered) "nach Ist neu kein Rückgängig"

        testCase "Verlassen verwirft die Zeile mit der Sitzung, ein neues Durchgehen beginnt mit dem Hinweis" <| fun () ->
            let skipped = reviewing [ cardA; cardC ] |> step ReviewSkip
            let reopened = skipped |> step ReviewLeft |> step ReviewOpened
            Expect.isNone (lineOf reopened) "Hinweis"
    ]

// ============================================
// Kategorie und Übertrag auf der Karte am Feld bestätigt (ledger-mf2bg)
// ============================================

let private tagesgeld = YnabAccountId (Guid.Parse "0195e800-0000-7000-8000-0000000000bb")
let private fieldNoteOf (model: Model) = model.Review |> Option.bind (fun r -> r.FieldNote)
let private changed previous : CategoryChangeOutcome =
    { Previous = previous; Change = CategoryChanged; WriteBack = CategorySent; PreviousTransferAccountId = None }
let private foodChoice : CategoryChoice = { CategoryId = YnabCategoryId food; CategoryName = "Lebensmittel" }

/// Die Karte bekommt eine Kategorie über den Vorschlag, die Antwort ist da.
let private picked (outcome: CategoryChangeOutcome) (newId: YnabCategoryId option) (model: Model) : Model * Msg list * ExternalMsg =
    let id = (reviewCardOf model).Value.Id
    run (EntryCategorySet (id, newId, Ok outcome)) (step (StartSetEntryCategory (id, newId)) model)

/// Die Karte wird ein Übertrag auf Tagesgeld, die Antwort ist da.
let private transferred (outcome: CategoryChangeOutcome) (model: Model) : Model * Msg list * ExternalMsg =
    let id = (reviewCardOf model).Value.Id
    run (EntryTransferSet (id, tagesgeld, false, Ok outcome)) (step (StartSetEntryTransfer (id, tagesgeld, false)) model)

[<Tests>]
let fieldNoteTests =
    testList "Durchgehen: Kategorie am Feld der Karte (ledger-mf2bg)" [

        testCase "die Texte am Feld: Name mit Verfügbarem, nur Name, entfernt" <| fun () ->
            let name, available = reviewCategoryNoteText (Some "Bahn") (Some (eur 84.2m))
            Expect.equal name "Bahn" "Name"
            Expect.isSome available "Betrag"
            Expect.stringContains available.Value "84,20" "Betrag formatiert"
            Expect.equal (reviewCategoryNoteText (Some "Bahn") None) ("Bahn", None) "ohne Verfügbares"
            Expect.equal (reviewCategoryNoteText None (Some (eur 1m))) ("Kategorie entfernt", None) "entfernt"
            Expect.equal reviewTransferNoteText "Gegenbuchung angelegt" "Übertrag"

        testCase "Kategorie auf der Karte: Feld leuchtet, Name und Verfügbares am Feld mit Rückgängig, kein Toast" <| fun () ->
            let after, _, external = reviewing [ cardB ] |> picked (changed None) (Some (YnabCategoryId food))
            Expect.equal external NoOp "kein Toast"
            let note = (fieldNoteOf after).Value
            Expect.equal note.EntryId (idOf cardB) "Buchung"
            Expect.equal note.Text "Lebensmittel" "Name"
            Expect.equal note.Available (Some "110,00 €") "Verfügbares"
            Expect.isTrue note.Flashing "leuchtet"
            Expect.equal note.Undo (UndoCategory (idOf cardB, None)) "Rückgängig"
            Expect.isNone (lineOf after) "keine Zeile unter dem Knopf"

        testCase "ohne bekanntes Verfügbares nur der Name, ohne Kategorie „Kategorie entfernt“" <| fun () ->
            let model = { reviewing [ cardB ] with Categories = Success [ { categories.Head with Available = None } ] }
            let after, _, external = model |> picked (changed None) (Some (YnabCategoryId food))
            Expect.equal external NoOp "kein Toast"
            Expect.equal (fieldNoteOf after |> Option.map (fun n -> n.Text, n.Available)) (Some ("Lebensmittel", None)) "nur Name"
            let removed, _, external = reviewing [ cardA ] |> picked (changed (Some foodChoice)) None
            Expect.equal external NoOp "kein Toast"
            let note = (fieldNoteOf removed).Value
            Expect.equal (note.Text, note.Available) ("Kategorie entfernt", None) "entfernt"
            Expect.equal note.Undo (UndoCategory (idOf cardA, Some foodChoice)) "Rückgängig stellt sie wieder her"

        testCase "Übertrag auf der Karte: „Gegenbuchung angelegt“ am Feld, kein Toast" <| fun () ->
            let after, _, external = reviewing [ cardA ] |> transferred (changed (Some foodChoice))
            Expect.equal external NoOp "kein Toast"
            let note = (fieldNoteOf after).Value
            Expect.equal (note.Text, note.Available) ("Gegenbuchung angelegt", None) "Text"
            Expect.equal note.Undo (UndoCategory (idOf cardA, Some foodChoice)) "Rückgängig"

        testCase "das Leuchten endet nur für die eigene Bestätigung" <| fun () ->
            let after, _, _ = reviewing [ cardB ] |> picked (changed None) (Some (YnabCategoryId food))
            let stamp = (fieldNoteOf after).Value.Stamp
            Expect.isTrue (fieldNoteOf (step (ReviewFieldFlashEnded (stamp + 1)) after)).Value.Flashing "fremde Nummer"
            Expect.isFalse (fieldNoteOf (step (ReviewFieldFlashEnded stamp) after)).Value.Flashing "eigene Nummer"

        testCase "der Kartenwechsel und eine neue Kategorie nehmen die Bestätigung weg" <| fun () ->
            let after, _, _ = reviewing [ cardB; cardC ] |> picked (changed None) (Some (YnabCategoryId food))
            Expect.isNone (fieldNoteOf (step ReviewSkip after)) "Karte gewechselt"
            Expect.isNone (fieldNoteOf (step (StartSetEntryCategory (idOf cardB, None)) after)) "neue Kategorie"

        testCase "Rückgängig am Feld stellt die Kategorie zurück, ohne Meldung" <| fun () ->
            let after, _, _ = reviewing [ cardB ] |> picked (changed None) (Some (YnabCategoryId food))
            let undoing, msgs, _ = run ReviewUndoField after
            Expect.contains msgs (RunUndoCategory (idOf cardB, None)) "dieselbe Rücknahme wie früher im Toast"
            let restored, _, external = run (EntryCategoryRestored (idOf cardB, None, Ok (changed (Some foodChoice)))) undoing
            Expect.equal external NoOp "keine Meldung"
            Expect.isNone (fieldNoteOf restored) "Zeile weg"
            Expect.isNone (lineOf restored) "keine Zeile unter dem Knopf"
            Expect.isNone (reviewCardOf restored).Value.CategoryId "alter Wert im Feld"

        testCase "Rückgängig am Feld macht die Buchung wieder zum Übertrag, ohne Meldung" <| fun () ->
            let wasTransfer = { changed (Some foodChoice) with PreviousTransferAccountId = Some tagesgeld }
            let after, _, _ = reviewing [ cardA ] |> picked wasTransfer (Some (YnabCategoryId food))
            let undoing, msgs, _ = run ReviewUndoField after
            Expect.contains msgs (RunSetEntryTransfer (idOf cardA, tagesgeld, true)) "Übertrag zurück"
            let restored, _, external = run (EntryTransferSet (idOf cardA, tagesgeld, true, Ok (changed (Some foodChoice)))) undoing
            Expect.equal external NoOp "keine Meldung"
            Expect.isNone (fieldNoteOf restored) "Zeile weg"

        testCase "YNAB nimmt die Kategorie nicht: Zeile am Feld bleibt, gelb unter dem Knopf" <| fun () ->
            let failedSend = { changed None with WriteBack = CategorySendFailed "Zeitüberschreitung" }
            let after, _, external = reviewing [ cardB ] |> picked failedSend (Some (YnabCategoryId food))
            Expect.equal external NoOp "kein Toast"
            Expect.isSome (fieldNoteOf after) "Zeile am Feld bleibt"
            Expect.equal (lineOf after) (Some (FeedbackWarning, "Kategorie geändert, nicht in YNAB", "Zeitüberschreitung", None)) "gelb"
            let transfer, _, external = reviewing [ cardA ] |> transferred { failedSend with Previous = Some foodChoice }
            Expect.equal external NoOp "kein Toast"
            Expect.isSome (fieldNoteOf transfer) "Zeile am Feld"
            Expect.equal (lineOf transfer) (Some (FeedbackWarning, "Übertrag gesetzt, nicht in YNAB", "Zeitüberschreitung", None)) "gelb"

        testCase "Kategorie scheitert: alter Wert, rot unter dem Knopf, Erneut schickt sie noch einmal" <| fun () ->
            let model = reviewing [ cardB ] |> step (StartSetEntryCategory (idOf cardB, Some (YnabCategoryId food)))
            let failed, _, external = run (EntryCategorySet (idOf cardB, Some (YnabCategoryId food), Error "Netz")) model
            Expect.equal external NoOp "kein Toast"
            Expect.isNone (reviewCardOf failed).Value.CategoryId "alter Wert"
            Expect.isNone (fieldNoteOf failed) "keine Bestätigung"
            Expect.equal (lineOf failed) (Some (FeedbackError, "Kategorie nicht geändert", "Netz", Some "Erneut")) "rot"
            Expect.equal (cardId failed) (Some (idOf cardB)) "Karte bleibt"
            let retried, msgs, _ = run ReviewRetryLast failed
            Expect.contains msgs (RunSetEntryCategory (idOf cardB, Some (YnabCategoryId food))) "derselbe Befehl"
            Expect.isNone (lineOf retried) "Zeile geht"

        testCase "Übertrag scheitert: rot unter dem Knopf, Erneut schickt ihn noch einmal" <| fun () ->
            let model = reviewing [ cardA ] |> step (StartSetEntryTransfer (idOf cardA, tagesgeld, false))
            let failed, _, external = run (EntryTransferSet (idOf cardA, tagesgeld, false, Error "Netz")) model
            Expect.equal external NoOp "kein Toast"
            Expect.equal (lineOf failed) (Some (FeedbackError, "Übertrag nicht gesetzt", "Netz", Some "Erneut")) "rot"
            let _, msgs, _ = run ReviewRetryLast failed
            Expect.contains msgs (RunSetEntryTransfer (idOf cardA, tagesgeld, false)) "derselbe Befehl"

        testCase "Rückgängig am Feld scheitert: rot unter dem Knopf, Erneut schickt die Rücknahme noch einmal" <| fun () ->
            let after, _, _ = reviewing [ cardB ] |> picked (changed None) (Some (YnabCategoryId food))
            let undoing = step ReviewUndoField after
            let failed, _, external = run (EntryCategoryRestored (idOf cardB, None, Error "Netz")) undoing
            Expect.equal external NoOp "kein Toast"
            Expect.equal (lineOf failed) (Some (FeedbackError, "Rückgängig nicht möglich", "Netz", Some "Erneut")) "rot"
            let _, msgs, _ = run ReviewRetryLast failed
            Expect.contains msgs (RunUndoCategory (idOf cardB, None)) "dieselbe Rücknahme"
            let transfer =
                reviewing [ cardA ]
                |> picked { changed (Some foodChoice) with PreviousTransferAccountId = Some tagesgeld } (Some (YnabCategoryId food))
                |> fun (m, _, _) -> step ReviewUndoField m
            let failed, _, external = run (EntryTransferSet (idOf cardA, tagesgeld, true, Error "Netz")) transfer
            Expect.equal external NoOp "kein Toast"
            Expect.equal (lineOf failed) (Some (FeedbackError, "Rückgängig nicht möglich", "Netz", Some "Erneut")) "rot"
            let _, msgs, _ = run ReviewRetryLast failed
            Expect.contains msgs (RunSetEntryTransfer (idOf cardA, tagesgeld, true)) "dieselbe Rücknahme"

        testCase "Z nimmt die jüngste Aktion zurück: Feld oder Zeile (reine Funktion)" <| fun () ->
            let id = idOf cardA
            let note stamp =
                Some { EntryId = id; Text = "Bahn"; Available = None; Undo = UndoCategory (id, None); Flashing = false; Stamp = stamp }
            let line act = Some { EntryId = id; Kind = LastConfirmed; Payee = None; Outcome = LastDone; Act = act }
            let session fieldNote last lastStamp = { reviewStarting with FieldNote = fieldNote; Last = last; LastStamp = lastStamp }
            let undoLine = Some (LastUndo (UndoSkipped id))
            Expect.equal (reviewUndoTarget (session (note 3) (line undoLine) 2)) (Some (UndoAtField (UndoCategory (id, None)))) "Feld jünger"
            Expect.equal (reviewUndoTarget (session (note 1) (line undoLine) 2)) (Some (UndoAtLine (UndoSkipped id))) "Zeile jünger"
            Expect.equal (reviewUndoTarget (session (note 1) (line (Some (LastRetry (RetryConfirm id)))) 2)) (Some (UndoAtField (UndoCategory (id, None)))) "Zeile ohne Rückgängig"
            Expect.equal (reviewUndoTarget (session None (line undoLine) 2)) (Some (UndoAtLine (UndoSkipped id))) "nur Zeile"
            Expect.equal (reviewUndoTarget (session None None 0)) None "nichts"

        testCase "Z im Durchgehen: nach der Kategorie die Kategorie, nach Später auf der nächsten Karte die Zeile" <| fun () ->
            let after, _, _ = reviewing [ cardB; cardC ] |> picked (changed None) (Some (YnabCategoryId food))
            Expect.equal (Components.Ledger.View.reviewKeyFor after "" false false "z") (Some KeyUndo) "Z angeboten"
            let _, msgs, _ = run (ReviewKey KeyUndo) after
            Expect.contains msgs (RunUndoCategory (idOf cardB, None)) "Kategorie zurück"
            // Die Zeile unter dem Knopf ist jünger als die Kategorie: Z nimmt die Zeile.
            let skipped = after |> step ReviewSkip
            let lastFirst = { skipped with Review = skipped.Review |> Option.map (fun r -> { r with FieldNote = (fieldNoteOf after) |> Option.map (fun n -> { n with EntryId = idOf cardC }) }) }
            let back, _, _ = run (ReviewKey KeyUndo) lastFirst
            Expect.equal (cardId back) (Some (idOf cardB)) "Später zurück"

        testCase "außerhalb des Durchgehens bleibt die Rückmeldung der Kategorie wie bisher (ledger-p5y9s)" <| fun () ->
            let model, _ = Components.Ledger.State.init ()
            let model = { model with Categories = Success categories; Entries = Success [ cardB ] }
            let started = step (StartSetEntryCategory (idOf cardB, Some (YnabCategoryId food))) model
            let after, _, external = run (EntryCategorySet (idOf cardB, Some (YnabCategoryId food), Ok (changed None))) started
            match external with
            | PostFeedback note -> Expect.isSome note.Act "Leiste mit Rückgängig"
            | other -> failtestf "Leiste erwartet: %A" other
            Expect.isNone after.Review "kein Durchgehen"
            let failed, _, external = run (EntryCategorySet (idOf cardB, Some (YnabCategoryId food), Error "Netz")) started
            match external with
            | PostFeedback note -> Expect.equal note.Tone FeedbackError "rot in der Leiste"
            | other -> failtestf "Leiste erwartet: %A" other
            Expect.isNone failed.Review "kein Durchgehen"

        // Review-Fund zu ledger-enjex: eine gescheiterte Rücknahme der Aufteilung auf der Karte ohne „Details“ meldete
        // sich als Toast (`categoryCommandFailed`); im Durchgehen steht sie rot unter dem Knopf mit „Erneut“.
        testCase "gescheiterte Rücknahme der Aufteilung auf der Karte: rot unter dem Knopf mit Erneut, kein Toast" <| fun () ->
            let restore : TransactionSplit list =
                [ { Amount = eur -6m; Memo = None; Target = Uncategorized }; { Amount = eur -4m; Memo = None; Target = Uncategorized } ]
            let model = reviewing [ cardA; cardC ]
            let failed, _, external = run (SplitsRestored (idOf cardA, restore, Error "Netz")) model
            Expect.equal external NoOp "kein Toast"
            Expect.equal (lineOf failed) (Some (FeedbackError, "Rückgängig nicht möglich", "Netz", Some "Erneut")) "rot unter dem Knopf"
            let _, msgs, _ = run ReviewRetryLast failed
            Expect.contains msgs (RunUndoSplit (idOf cardA, restore)) "Erneut schickt dieselbe Rücknahme"
    ]
