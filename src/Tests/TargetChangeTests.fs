module Tests.TargetChangeTests

// budgeting-k8wq3, ADR 0072, ADR 0080: Target ändern und entfernen. Reine Planung, Encoder und Decoder des
// Target-PATCH, die Abläufe gegen In-Memory-SQLite mit einem Fake-YNAB, der nie das Netz erreicht, die
// Persistenz, die Grenze und die Historie.

open System
open System.Collections.Generic

// CRITICAL: Testmodus setzen, BEVOR das Persistence-Modul initialisiert wird.
do Environment.SetEnvironmentVariable("USE_MEMORY_DB", "true")

open Expecto
open Thoth.Json.Net
open Shared.Domain
open Shared.Budgeting
open Server.Budgeting
open Server.Budgeting.CommandHandlers

let private eur (amount: decimal) : Money = { Amount = amount; Currency = "EUR" }

let private run x = Async.RunSynchronously x

let private targetOf (kind: TargetKind) (goal: decimal) (date: string option) : Target =
    { Goal = eur goal
      Rhythm = Months 1
      Kind = kind
      Date = date
      NeedsWholeAmount = Some true
      Day = Some 15 }

let private draft (goal: decimal) (date: string option) : TargetDraft =
    { NewGoal = eur goal; NewDate = date; NewRhythm = KeepRhythm }

let private patch goal date : TargetPatch = { GoalTarget = goal; TargetDate = date; Frequency = None }

// ============================================
// Reine Planung
// ============================================

let private planTests =
    let need = targetOf Need 50m None
    let plan shown wanted =
        match Domain.planTargetWrite (Some shown) wanted with
        | Ok patches -> patches
        | Error e -> failtest e
    testList "Target ändern: Planung" [
        test "Nur der Betrag ergibt genau goal_target, das Datum bleibt weg" {
            Expect.equal (plan need (Some (draft 60m None))) [ patch (SetTo 60000L) Keep ] "Betrag"
        }
        test "Nur das Datum gesetzt oder gelöscht ergibt nur goal_target_date" {
            Expect.equal (plan need (Some (draft 50m (Some "2027-06-01")))) [ patch Keep (SetTo "2027-06-01") ] "gesetzt"
            let dated = targetOf Need 50m (Some "2027-06-01")
            Expect.equal (plan dated (Some (draft 50m None))) [ patch Keep Clear ] "gelöscht"
        }
        test "Betrag und Datum zugleich ergeben einen Patch mit beiden Feldern" {
            Expect.equal (plan need (Some (draft 60m (Some "2027-06-01")))) [ patch (SetTo 60000L) (SetTo "2027-06-01") ] "beide"
        }
        test "Entfernen ergibt goal_target null allein" {
            Expect.equal (plan need None) [ patch Clear Keep ] "entfernen"
        }
        test "Ohne Abweichung entsteht kein Patch" {
            let dated = targetOf Need 50m (Some "2027-06-01")
            Expect.isEmpty (plan dated (Some (draft 50m (Some "2027-06-01")))) "gleich"
            Expect.isEmpty (plan need (Some (draft 50.000m None))) "50,000 ist 50"
        }
        test "Ein Patch ohne gesetztes Feld ist leer, jeder mit einem Feld nicht" {
            Expect.isTrue (Domain.targetPatchIsEmpty (patch Keep Keep)) "leer"
            for p in [ patch (SetTo 1L) Keep; patch Clear Keep; patch Keep Clear; { patch Keep Keep with Frequency = Some Monthly } ] do
                Expect.isFalse (Domain.targetPatchIsEmpty p) $"{p}"
        }
        test "Ohne Rhythmuswahl trägt kein Patch goal_frequency" {
            for wanted in [ None; Some (draft 60m None); Some (draft 50m (Some "2027-01-01")); Some (draft 70m (Some "2027-01-01")) ] do
                for p in plan need wanted do
                    Expect.equal p.Frequency None $"{wanted}"
        }
        test "Betrag und Datum bei TB, TBD und MF ergeben dieselben Patches wie bei NEED" {
            let wishes = [ Some (draft 60m None); Some (draft 50m (Some "2027-06-01")); Some (draft 70m (Some "2027-06-01")); None ]
            for kind in [ TargetBalance; TargetBalanceByDate; MonthlyFunding ] do
                for wanted in wishes do
                    Expect.equal (plan (targetOf kind 50m None) wanted) (plan need wanted) $"{kind} {wanted}"
        }
        test "Target ändern geht nur im laufenden Monat" {
            Expect.isTrue (targetEditAllowed CurrentMonth) "laufend"
            Expect.isFalse (targetEditAllowed PastMonth) "vergangen"
            Expect.isFalse (targetEditAllowed FutureMonth) "künftig"
        }
        test "Die Art einer Target-Änderung geht als Text hin und zurück, ein unbekannter wird gemeldet" {
            let kinds =
                [ TargetEdited; TargetRemoved ]
                @ [ for f in [ Weekly; Monthly; Yearly ] do
                        TargetCreated f
                        TargetKindChanged f ]
            for kind in kinds do
                let text, frequency = Domain.targetChangeKindText kind
                Expect.equal (Domain.targetChangeKindOfText text frequency) (Ok kind) $"{kind}"
            Expect.isError (Domain.targetChangeKindOfText "Quatsch" None) "kein Standard"
            Expect.isError (Domain.targetChangeKindOfText "Angelegt" None) "angelegt ohne Rhythmus"
            Expect.isError (Domain.targetChangeKindOfText "Art gewechselt" (Some "daily")) "unbekannter Rhythmus"
        }
    ]

// ============================================
// Historie: Texte
// ============================================

let private historyTextTests =
    let change kind goals dates : HistoryTargetChange =
        { ChangeId = Guid.NewGuid()
          OccurredAt = DateTime(2026, 11, 4, 9, 0, 0, DateTimeKind.Utc)
          Month = "2026-11-01"
          CategoryId = Guid.NewGuid()
          CategoryName = "Urlaub"
          ChangeKind = kind
          GoalBefore = fst goals |> Option.map eur
          GoalAfter = snd goals |> Option.map eur
          DateBefore = fst dates
          DateAfter = snd dates
          Latest = Some Written
          DateStep = None }
    testList "Target ändern: Texte der Historie" [
        test "Betrag, Datum gesetzt, Datum entfernt und Entfernen haben ihren Text, ohne Betrag in der Spalte" {
            let line c = historyEntryLine (TargetChanged c)
            let amount = line (change TargetEdited (Some 50m, Some 60m) (None, None))
            Expect.equal (amount.Text, amount.Change) ("Target 50,00 € → 60,00 €", None) "Betrag"
            Expect.equal (line (change TargetEdited (Some 50m, Some 50m) (None, Some "2027-06-01"))).Text "Datum 01.06.2027" "Datum"
            Expect.equal (line (change TargetEdited (Some 50m, Some 50m) (Some "2027-06-01", None))).Text "Datum entfernt" "Datum weg"
            Expect.equal
                (line (change TargetEdited (Some 1200m, Some 1500.5m) (None, Some "2027-06-01"))).Text
                "Target 1.200,00 € → 1.500,50 €, Datum 01.06.2027"
                "beides"
            Expect.equal (line (change TargetRemoved (Some 50m, None) (None, None))).Text "Target entfernt" "entfernt"
        }
        test "Der Stand folgt dem jüngsten Ergebnis wie bei Bewegungen" {
            let line latest = (historyEntryLine (TargetChanged { change TargetRemoved (Some 50m, None) (None, None) with Latest = latest })).Status
            Expect.equal (line (Some Written)) (Some HistoryWritten) "geschrieben"
            Expect.equal (line (Some (Failed "kaputt"))) (Some (HistoryFailed "kaputt")) "fehlgeschlagen"
            Expect.equal (line None) (Some HistoryOpen) "offen"
        }
        // budgeting-g2ty9 (X7): eine Änderung in zwei Schritten hat einen Stand aus beiden.
        test "Anlegen mit Datum: Schritt 1 geschrieben und Schritt 2 fehlgeschlagen ist teilweise geschrieben, ohne Ergebnis von Schritt 2 noch offen" {
            let dated = { change (TargetCreated Yearly) (None, Some 600m) (None, Some "2027-06-01") with Latest = Some Written }
            let line c = historyEntryLine (TargetChanged c)
            let partly = line { dated with DateStep = Some (Failed "kaputt") }
            Expect.equal
                (partly.Text, partly.Status)
                ("Target angelegt, 600,00 € jährlich, Datum 01.06.2027 nicht gesetzt", Some (HistoryPartlyWritten "kaputt"))
                "teilweise"
            Expect.equal (line { dated with DateStep = Some Written }).Status (Some HistoryWritten) "beide geschrieben"
            Expect.equal (line dated).Status (Some HistoryOpen) "Schritt 2 ohne Ergebnis"
            Expect.equal
                (line { dated with Latest = Some (Failed "kaputt"); DateStep = Some (Failed "Schritt 1 fehlgeschlagen") }).Status
                (Some (HistoryFailed "kaputt"))
                "Schritt 1 fehlgeschlagen"
            let undated = { dated with DateAfter = None }
            Expect.equal (line undated).Status (Some HistoryWritten) "ohne Datum ein Schritt"
            let kindChanged =
                { change (TargetKindChanged Yearly) (Some 50m, Some 600m) (Some "2027-06-01", Some "2027-06-01") with
                    DateStep = Some (Failed "kaputt") }
            Expect.equal
                (line kindChanged).Text
                "Rhythmus jährlich, Target 50,00 € → 600,00 €, Datum 01.06.2027 nicht gesetzt"
                "Wechsel mit gleichem Datum nennt das fehlende Datum"
        }
    ]

// ============================================
// YNAB: Encoder und Decoder
// ============================================

let private ynabTests =
    let id1 = Guid.Parse "11111111-1111-1111-1111-111111111111"
    let body p =
        match Server.YnabClient.buildTargetBody p with
        | Ok text -> text
        | Error e -> failtest e
    let categoryJson (goalFields: string) =
        $"""{{ "data": {{ "category": {{ "id": "{id1}", "name": "x", "budgeted": 0, "activity": 0, "balance": 0, {goalFields} }} }} }}"""
    testList "Target ändern: YNAB" [
        test "goal_target geht als JSON-Zahl hinaus, nicht als String" {
            // Verhindert, dass der Betrag als String ("60000") gesendet wird: YNAB würde ihn still verwerfen
            // (CLAUDE.md, wie beim Zuweisen).
            let text = body (patch (SetTo 60000L) Keep)
            Expect.equal text """{"category":{"goal_target":60000}}""" "exakter Body, Datum fehlt"
            Expect.equal (Decode.fromString (Decode.at [ "category"; "goal_target" ] Decode.int64) text) (Ok 60000L) "als Zahl"
        }
        test "Die Obergrenze 1.000.000,00 € (1e9 Milliunits) geht als JSON-Zahl hinaus" {
            Expect.equal (body (patch (SetTo 1000000000L) Keep)) """{"category":{"goal_target":1000000000}}""" "als Zahl"
        }
        test "Clear ist null, Keep fehlt, das Datum steht als YYYY-MM-DD" {
            Expect.equal (body (patch Clear Keep)) """{"category":{"goal_target":null}}""" "entfernen"
            Expect.equal (body (patch Keep (SetTo "2027-06-01"))) """{"category":{"goal_target_date":"2027-06-01"}}""" "Datum"
            Expect.equal (body (patch Keep Clear)) """{"category":{"goal_target_date":null}}""" "Datum löschen"
        }
        test "Ein leerer Patch wird abgelehnt und nichts gesendet" {
            Expect.isError (Server.YnabClient.buildTargetBody (patch Keep Keep)) "leer"
            let sent = ref 0
            let send _ _ = async { sent.Value <- sent.Value + 1; return 200, "{}" }
            match Server.YnabClient.setCategoryTargetWith send "bud" id1 (patch Keep Keep) |> run with
            | Error (YnabError.InvalidResponse _) -> ()
            | other -> failtest $"Ablehnung erwartet: {other}"
            Expect.equal sent.Value 0 "nichts hinaus"
        }
        test "setCategoryTargetWith schickt den PATCH an die Kategorie ohne Monat und liest das Target danach" {
            let seen = ref ("", "")
            let answer = categoryJson """ "goal_type": "NEED", "goal_target": 60000, "goal_cadence": 1, "goal_cadence_frequency": 1, "goal_day": 15, "goal_needs_whole_amount": true """
            let send url text = async { seen.Value <- (url, text); return 200, answer }
            let result = Server.YnabClient.setCategoryTargetWith send "bud" id1 (patch (SetTo 60000L) Keep) |> run
            Expect.stringEnds (fst seen.Value) $"/budgets/bud/categories/{id1}" "Pfad ohne Monat"
            Expect.equal (snd seen.Value) """{"category":{"goal_target":60000}}""" "gesendeter Body"
            match result with
            | Ok (Some t) -> Expect.equal (t.Goal, t.Day, t.Kind) (eur 60m, Some 15, Need) "neuer Stand"
            | other -> failtest $"Target erwartet: {other}"
            let boom _ _ = async { return failwith "kaputt" }
            match Server.YnabClient.setCategoryTargetWith boom "bud" id1 (patch Clear Keep) |> run with
            | Error (YnabError.NetworkError m) -> Expect.stringContains m "kaputt" "Netzfehler"
            | other -> failtest $"Netzfehler erwartet: {other}"
        }
        test "Antworten: 400 trägt error.detail, 404 Kategorie nicht gefunden, 429 Rate-Limit, 200 die Kategorie" {
            let map status text = Server.YnabClient.interpretTargetResponse id1 status text
            let detail = """{"error":{"id":"400","name":"bad_request","detail":"goal_target is invalid"}}"""
            match map 400 detail with
            | Error e -> Expect.equal (ynabErrorText e) "YNAB hat abgelehnt: goal_target is invalid" "Meldung mit Detail"
            | other -> failtest $"400: {other}"
            match map 400 "kein json" with
            | Error e -> Expect.stringContains (ynabErrorText e) "kein json" "Rückfall auf den Body"
            | other -> failtest $"400: {other}"
            match map 404 "{}" with
            | Error e -> Expect.equal (ynabErrorText e) "Kategorie in YNAB nicht gefunden" "404"
            | other -> failtest $"404: {other}"
            Expect.equal (map 429 "{}") (Error (YnabError.RateLimitExceeded 60)) "429"
            match map 401 "{}" with Error (YnabError.Unauthorized _) -> () | other -> failtest $"401: {other}"
            Expect.equal (map 200 (categoryJson """ "goal_type": null, "goal_target": null """)) (Ok None) "200 ohne Target nach dem Entfernen"
        }
        test "Das Target trägt Art, Datum, Auffüllen und Tag, auch mit fehlenden Feldern und unbekannter Art" {
            let decode fields =
                match Decode.fromString Server.YnabClient.Decoders.categoryTargetDecoder (categoryJson fields) with
                | Ok t -> t
                | Error e -> failtest e
            let full =
                decode """ "goal_type": "TBD", "goal_target": 120000, "goal_target_date": "2027-06-01", "goal_needs_whole_amount": false, "goal_day": 3 """
            Expect.equal
                full
                (Some { Goal = eur 120m; Rhythm = NoRhythm; Kind = TargetBalanceByDate; Date = Some "2027-06-01"; NeedsWholeAmount = Some false; Day = Some 3 })
                "alle Felder"
            let bare = decode """ "goal_type": "XYZ", "goal_target": 1000, "goal_target_date": null """
            Expect.equal
                bare
                (Some { Goal = eur 1m; Rhythm = NoRhythm; Kind = OtherKind "XYZ"; Date = None; NeedsWholeAmount = None; Day = None })
                "fehlende Felder, unbekannte Art"
            for goalType, kind in [ "NEED", Need; "TB", TargetBalance; "MF", MonthlyFunding; "DEBT", Debt ] do
                Expect.equal (Server.YnabClient.Decoders.targetKind goalType) kind goalType
        }
    ]

// ============================================
// Abläufe gegen Fake-YNAB und In-Memory-SQLite
// ============================================

/// Ein Fake-YNAB mit einem Target je Kategorie. `SetTarget` wendet den Patch an wie YNAB, zählt jedes
/// Lesen und jeden Schreibaufruf und ruft `OnWrite` beim Schreiben (etwa, um die Datenbank anzusehen).
type private FakeYnab(initial: (Guid * Target option) list) =
    let targets = Dictionary<Guid, Target option>(dict initial)
    member val Reads = 0 with get, set
    member val Patches = ResizeArray<Guid * TargetPatch>()
    member val Failure : YnabError option = None with get, set
    /// Nur der n-te Schreibaufruf (ab 1) scheitert mit `Failure` (budgeting-xp46a). `None`: jeder.
    member val FailOnlyCall : int option = None with get, set
    /// Die Antwort zeigt diesen Rhythmus statt dem gesendeten (budgeting-xp46a).
    member val AnswerRhythm : TargetRhythm option = None with get, set
    member val OnWrite : Guid -> unit = ignore with get, set
    member val WriteDelayMs = 0 with get, set
    member _.Target (id: Guid) = targets[id]
    member _.SetDirectly (id: Guid) (target: Target option) = targets[id] <- target
    member this.Port : YnabPort =
        { FetchMonth =
            fun monthKey ->
                async {
                    this.Reads <- this.Reads + 1
                    return
                        Ok
                            { Month = monthKey
                              ToBeBudgeted = eur 0m
                              Categories =
                                [ for KeyValue (id, target) in targets ->
                                    { Id = id
                                      Name = "Urlaub "
                                      GroupName = "Rücklagen"
                                      Assigned = eur 0m
                                      Activity = eur 0m
                                      Available = eur 0m
                                      Underfunded = target |> Option.map (fun _ -> eur 10m)
                                      Target = target
                                      Snoozed = false
                                      Hidden = false
                                      Deleted = false
                                      Internal = false } ] }
                }
          SetAssigned = fun _ _ _ -> async { return failwith "Target ändern weist nichts zu" }
          SetTarget =
            fun id p ->
                async {
                    this.Patches.Add((id, p))
                    this.OnWrite id
                    if this.WriteDelayMs > 0 then do! Async.Sleep this.WriteDelayMs
                    let fails =
                        match this.FailOnlyCall with
                        | Some n -> this.Patches.Count = n
                        | None -> true
                    match this.Failure with
                    | Some e when fails -> return Error e
                    | _ ->
                        let after =
                            match targets[id], p.GoalTarget, p.Frequency with
                            | _, Clear, _ -> None
                            // goal_frequency ersetzt das Target: Tag und Datum weg, Art NEED (ADR 0072 Punkt 4).
                            | _, SetTo v, Some f ->
                                Some
                                    { Goal = euroOfMilliunits v
                                      Rhythm = this.AnswerRhythm |> Option.defaultValue (rhythmOfFrequency f)
                                      Kind = Need
                                      Date = None
                                      NeedsWholeAmount = Some true
                                      Day = None }
                            | None, _, _ -> None
                            | Some t, goal, _ ->
                                let goal' = match goal with SetTo v -> euroOfMilliunits v | _ -> t.Goal
                                let date' = match p.TargetDate with Keep -> t.Date | SetTo d -> Some d | Clear -> None
                                Some { t with Goal = goal'; Date = date' }
                        targets[id] <- after
                        return Ok after
                } }

let private current = "2026-11-01"
let private now = DateTime(2026, 11, 4, 9, 0, 0, DateTimeKind.Utc)

let private fifty = targetOf Need 50m None

let private fakeWith (target: Target option) =
    let k = Guid.NewGuid()
    FakeYnab([ k, target ]), k

let private request (k: Guid) (shown: Target option) (wanted: TargetDraft option) : TargetChangeRequest =
    { CategoryId = k; Shown = shown; Wanted = wanted }

let private change (f: FakeYnab) (month: string) (r: TargetChangeRequest) =
    changeTarget f.Port now current month r |> run

/// Zeilen in `target_changes` der Kategorie: Art, alter und neuer Betrag in Milliunits, Status.
let private changeRows (id: Guid) : string list =
    use conn = global.Persistence.getConnection ()
    Dapper.SqlMapper.Query<string>(
        conn,
        "SELECT c.month || '|' || c.change_kind || '|' || IFNULL(c.old_goal_target, '-') || '|' || IFNULL(c.new_goal_target, '-') \
                || '|' || IFNULL(r.status, 'offen') || '|' || IFNULL(r.reason, '') \
         FROM target_changes c LEFT JOIN target_change_results r ON r.change_id = c.id \
         WHERE c.category_id = @id ORDER BY c.rowid, r.sequence",
        {| id = string id |})
    |> Seq.toList

let private flowTests =
    let testCase name body =
        testCase name (fun () ->
            Budgeting.Persistence.initializeBudgetingSchema ()
            body ())

    testSequenced
    <| testList "Target ändern: Abläufe" [
        testCase "GIVEN 50 € in YNAB und gezeigt WHEN ändern auf 60 € THEN erst festgehalten, dann nur goal_target gesendet, geschrieben" <| fun () ->
            let f, k = fakeWith (Some fifty)
            let seenAtWrite = ref []
            f.OnWrite <- fun id -> seenAtWrite.Value <- changeRows id
            match change f current (request k (Some fifty) (Some (draft 60m None))) with
            | Ok (Ok (TargetWritten (Some t))) -> Expect.equal t.Goal (eur 60m) "neues Target"
            | other -> failtest $"geschrieben erwartet: {other}"
            Expect.equal (f.Patches |> List.ofSeq) [ k, patch (SetTo 60000L) Keep ] "ein PATCH nur mit goal_target"
            Expect.equal seenAtWrite.Value [ $"{current}|Geändert|50000|60000|offen|" ] "vor dem PATCH festgehalten"
            Expect.equal (changeRows k) [ $"{current}|Geändert|50000|60000|written|" ] "geschrieben"

        testCase "GIVEN YNAB lehnt mit 400 ab WHEN ändern THEN fehlgeschlagen mit YNABs Text und dieselbe Meldung als Error" <| fun () ->
            let f, k = fakeWith (Some fifty)
            f.Failure <- Some (YnabError.InvalidResponse "goal_target is invalid")
            let expected = "YNAB hat abgelehnt: goal_target is invalid"
            Expect.equal (change f current (request k (Some fifty) (Some (draft 60m None)))) (Ok (Error expected)) "Error"
            Expect.equal (changeRows k) [ $"{current}|Geändert|50000|60000|failed|{expected}" ] "Ergebnis"

        // budgeting-r3wq8, Bug: scheiterte das Anhängen des Ergebnisses nach einem Ok von YNAB, ging die
        // Ausnahme durch Remoting, obwohl YNAB geschrieben hatte. Jetzt Best-Effort: geloggt, das YNAB-Ergebnis
        // kommt zurück. Ein Trigger nur für diese Kategorie lässt das Anhängen scheitern.
        testCase "GIVEN YNAB schreibt, das Ergebnis lässt sich nicht anhängen WHEN ändern THEN trotzdem geschrieben, die Zeile bleibt offen" <| fun () ->
            let f, k = fakeWith (Some fifty)
            let trigger = $"fail_result_{k:N}"
            use conn = global.Persistence.getConnection ()
            Dapper.SqlMapper.Execute(
                conn,
                $"CREATE TRIGGER {trigger} BEFORE INSERT ON target_change_results \
                  WHEN NEW.change_id IN (SELECT id FROM target_changes WHERE category_id = '{k}') \
                  BEGIN SELECT RAISE(ABORT, 'kaputt'); END;")
            |> ignore
            try
                match change f current (request k (Some fifty) (Some (draft 60m None))) with
                | Ok (Ok (TargetWritten (Some t))) -> Expect.equal t.Goal (eur 60m) "YNABs Ergebnis"
                | other -> failtest $"geschrieben erwartet: {other}"
                Expect.equal (changeRows k) [ $"{current}|Geändert|50000|60000|offen|" ] "festgehalten, ohne Ergebnis"
            finally
                Dapper.SqlMapper.Execute(conn, $"DROP TRIGGER {trigger}") |> ignore

        testCase "GIVEN YNAB steht inzwischen auf 55 €, gezeigt 50 € WHEN ändern THEN kein PATCH, keine Zeile, TargetChangedInYnab mit 55 €" <| fun () ->
            let f, k = fakeWith (Some (targetOf Need 55m None))
            Expect.equal
                (change f current (request k (Some fifty) (Some (draft 60m None))))
                (Ok (Ok (TargetChangedInYnab (Some (targetOf Need 55m None)))))
                "YNAB gewinnt"
            Expect.isEmpty f.Patches "kein PATCH"
            Expect.isEmpty (changeRows k) "keine Zeile"

        testCase "GIVEN YNAB steht schon auf dem Wunsch THEN kein PATCH, keine Zeile, geschrieben" <| fun () ->
            let f, k = fakeWith (Some fifty)
            Expect.equal (change f current (request k (Some fifty) (Some (draft 50m None)))) (Ok (Ok (TargetWritten (Some fifty)))) "wie geschrieben"
            Expect.isEmpty f.Patches "kein PATCH"
            Expect.isEmpty (changeRows k) "keine Zeile"

        testCase "GIVEN Entfernen WHEN YNAB nimmt an THEN PATCH goal_target null, die Historie zeigt Target entfernt" <| fun () ->
            let f, k = fakeWith (Some fifty)
            Expect.equal (change f current (request k (Some fifty) None)) (Ok (Ok (TargetWritten None))) "entfernt"
            Expect.equal (f.Patches |> List.ofSeq) [ k, patch Clear Keep ] "goal_target null"
            let port = { QueryHandlers.liveHistoryPort with LoadMonthlyActivity = fun _ _ -> async { return [] } }
            match QueryHandlers.categoryHistory port k (DateTime(2026, 11, 1)) |> run with
            | Ok history ->
                let lines = history.Entries |> List.map historyEntryLine
                Expect.equal (lines |> List.map (fun l -> l.Text, l.Status)) [ "Target entfernt", Some HistoryWritten ] "Historie"
            | Error e -> failtest e

        testCase "Vergangener und künftiger Monat und Anlegen ohne Rhythmus: Fehler ohne YNAB-Aufruf und ohne Zeile" <| fun () ->
            let f, k = fakeWith (Some fifty)
            let reason month r =
                match change f month r with
                | Ok (Error message) -> message
                | other -> failtest $"Ablehnung erwartet: {other}"
            Expect.equal (reason "2026-10-01" (request k (Some fifty) (Some (draft 60m None)))) TargetEditMonthText "vergangen"
            Expect.equal (reason "2026-12-01" (request k (Some fifty) (Some (draft 60m None)))) TargetEditMonthText "künftig"
            Expect.equal (reason current (request k None (Some (draft 60m None)))) TargetRhythmMissingText "anlegen ohne Rhythmus"
            Expect.equal f.Reads 0 "kein YNAB-Aufruf"
            Expect.isEmpty (changeRows k) "keine Zeile"

        testCase "Eine ausgeblendete oder unbekannte Kategorie wird abgelehnt" <| fun () ->
            let f, _ = fakeWith (Some fifty)
            match change f current (request (Guid.NewGuid()) (Some fifty) None) with
            | Ok (Error message) -> Expect.stringContains message "nicht sichtbar" "Grund"
            | other -> failtest $"Ablehnung erwartet: {other}"
            Expect.isEmpty f.Patches "kein PATCH"

        testCase "Zwei Änderungen derselben Kategorie laufen nacheinander, die zweite sieht die erste als Abweichung" <| fun () ->
            let f, k = fakeWith (Some fifty)
            f.WriteDelayMs <- 50
            let first = changeTarget f.Port now current current (request k (Some fifty) (Some (draft 60m None))) |> Async.StartAsTask
            let second = changeTarget f.Port now current current (request k (Some fifty) (Some (draft 70m None))) |> Async.StartAsTask
            let results = [ first.Result; second.Result ]
            Expect.equal f.Patches.Count 1 "nur ein PATCH"
            let written = results |> List.filter (function Ok (Ok (TargetWritten _)) -> true | _ -> false)
            let changed = results |> List.filter (function Ok (Ok (TargetChangedInYnab _)) -> true | _ -> false)
            Expect.equal (written.Length, changed.Length) (1, 1) $"eine geschrieben, eine abgewichen: {results}"
            Expect.equal (changeRows k |> List.length) 1 "eine Zeile"

        testCase "Zwei Kategorien laufen unabhängig: die Sperre der einen hält die andere nicht auf" <| fun () ->
            let a, b = Guid.NewGuid(), Guid.NewGuid()
            let f = FakeYnab([ a, Some fifty; b, Some fifty ])
            let holder = new System.Threading.SemaphoreSlim(0, 1)
            let release = new System.Threading.SemaphoreSlim(0, 1)
            let held =
                TargetGuard.exclusive a (async {
                    holder.Release() |> ignore
                    do! release.WaitAsync() |> Async.AwaitTask
                })
                |> Async.StartAsTask
            holder.Wait()
            let blocked = changeTarget f.Port now current current (request a (Some fifty) (Some (draft 60m None))) |> Async.StartAsTask
            // Mit Zeitgrenze: eine globale Sperre macht den Test rot, statt ihn hängen zu lassen.
            let freeTask = changeTarget f.Port now current current (request b (Some fifty) (Some (draft 60m None))) |> Async.StartAsTask
            let finished = freeTask.Wait(TimeSpan.FromSeconds 10.0)
            if not finished then release.Release() |> ignore
            Expect.isTrue finished "b endet, obwohl a gesperrt ist"
            let free = freeTask.Result
            Expect.equal free (Ok (Ok (TargetWritten (Some (targetOf Need 60m None))))) "b läuft"
            Expect.isFalse blocked.IsCompleted "a wartet auf seine Sperre"
            Expect.equal (f.Patches |> Seq.map fst |> List.ofSeq) [ b ] "nur b gesendet"
            release.Release() |> ignore
            held.Wait()
            Expect.equal blocked.Result (Ok (Ok (TargetWritten (Some (targetOf Need 60m None))))) "danach a"

        testCase "Die Historie liefert Target-Änderungen des Monats mit Bewegungen und Pausen, neueste zuerst, mit jüngstem Ergebnis" <| fun () ->
            let k = Guid.NewGuid()
            let at hour = DateTime(2026, 11, 4, hour, 0, 0, DateTimeKind.Utc)
            let movement : Budgeting.Persistence.Movement =
                { Id = Guid.NewGuid()
                  ApplicationId = Guid.NewGuid()
                  OccurredAt = at 1
                  Origin = MoneyAssigned
                  Month = current
                  CategoryId = k
                  CategoryName = "Urlaub"
                  OldAssigned = 0L
                  NewAssigned = 10000L }
            let pause : Budgeting.Persistence.PauseEvent =
                { Id = Guid.NewGuid(); OccurredAt = at 3; Month = current; CategoryId = k; CategoryName = "Urlaub"; Kind = Domain.PauseKind.Paused }
            let targetChange hour (month: string) : Budgeting.Persistence.TargetChange =
                { Id = Guid.NewGuid()
                  OccurredAt = at hour
                  Month = month
                  CategoryId = k
                  CategoryName = "Urlaub"
                  Kind = TargetEdited
                  OldGoalTarget = Some 50000L
                  NewGoalTarget = Some 60000L
                  OldDate = None
                  NewDate = None }
            let c2, c4 = targetChange 2 current, targetChange 4 current
            Budgeting.Persistence.appendMovements [ movement ] |> run
            Budgeting.Persistence.appendPauseEvent pause |> run
            Budgeting.Persistence.appendTargetChanges [ c2; c4; targetChange 5 "2026-12-01" ] |> run
            Budgeting.Persistence.appendTargetChangeStepResult c2.Id 1 now (Failed "Rate-Limit") |> run
            Budgeting.Persistence.appendTargetChangeStepResult c2.Id 1 now Written |> run
            let port = { QueryHandlers.liveHistoryPort with LoadMonthlyActivity = fun _ _ -> async { return [] } }
            match QueryHandlers.categoryHistory port k (DateTime(2026, 11, 1)) |> run with
            | Ok history ->
                let ids =
                    history.Entries
                    |> List.map (function
                        | Movement m -> "Bewegung", m.MovementId, None
                        | TargetPaused p
                        | PauseLifted p -> "Pause", p.EventId, None
                        | TargetChanged c -> "Target", c.ChangeId, c.Latest)
                Expect.equal
                    ids
                    [ "Target", c4.Id, None; "Pause", pause.Id, None; "Target", c2.Id, Some Written; "Bewegung", movement.Id, None ]
                    "gemischt, neueste zuerst, jüngstes Ergebnis"
            | Error e -> failtest e
    ]

let private persistenceTests =
    let tryExec (sql: string) =
        use conn = global.Persistence.getConnection ()
        try
            Dapper.SqlMapper.Execute(conn, sql) |> ignore
            None
        with ex -> Some ex.Message
    testSequenced
    <| testList "Target ändern: Persistenz" [
        testCase "Beide Tabellen sind append-only, das Schema läuft zweimal" <| fun () ->
            Budgeting.Persistence.initializeBudgetingSchema ()
            Budgeting.Persistence.initializeBudgetingSchema ()
            let k = Guid.NewGuid()
            let c : Budgeting.Persistence.TargetChange =
                { Id = Guid.NewGuid(); OccurredAt = now; Month = current; CategoryId = k; CategoryName = "A"; Kind = TargetRemoved
                  OldGoalTarget = Some 1000L; NewGoalTarget = None; OldDate = Some "2027-01-01"; NewDate = None }
            Budgeting.Persistence.appendTargetChanges [ c ] |> run
            Budgeting.Persistence.appendTargetChangeStepResult c.Id 1 now Written |> run
            Expect.isSome (tryExec $"UPDATE target_changes SET change_kind = 'Geändert' WHERE category_id = '{k}'") "UPDATE Änderung"
            Expect.isSome (tryExec $"DELETE FROM target_changes WHERE category_id = '{k}'") "DELETE Änderung"
            Expect.isSome (tryExec $"UPDATE target_change_results SET status = 'failed' WHERE change_id = '{c.Id}'") "UPDATE Ergebnis"
            Expect.isSome (tryExec $"DELETE FROM target_change_results WHERE change_id = '{c.Id}'") "DELETE Ergebnis"
            match Budgeting.Persistence.loadTargetChanges k current |> run with
            | Ok [ read ] ->
                Expect.equal
                    (read.ChangeKind, read.GoalBefore, read.GoalAfter, read.DateBefore, read.DateAfter, read.Latest)
                    (TargetRemoved, Some (eur 1m), None, Some "2027-01-01", None, Some Written)
                    "nullbare Werte gelesen"
            | other -> failtest $"eine Zeile erwartet: {other}"

        // budgeting-g2ty9 (X7): die Historie las nur das jüngste Ergebnis über alle Schritte, ein gescheitertes
        // Datum in Schritt 2 ließ die ganze Änderung fehlgeschlagen aussehen.
        testCase "Ergebnisse werden je Schritt gelesen, ein Ergebnis ohne Schritt zählt als Schritt 1" <| fun () ->
            Budgeting.Persistence.initializeBudgetingSchema ()
            let k = Guid.NewGuid()
            let c : Budgeting.Persistence.TargetChange =
                { Id = Guid.NewGuid(); OccurredAt = now; Month = current; CategoryId = k; CategoryName = "A"; Kind = TargetCreated Yearly
                  OldGoalTarget = None; NewGoalTarget = Some 600000L; OldDate = None; NewDate = Some "2027-06-01" }
            Budgeting.Persistence.appendTargetChanges [ c ] |> run
            Expect.isNone
                (tryExec $"INSERT INTO target_change_results (change_id, occurred_at, status) VALUES ('{c.Id}', '2026-11-04T09:00:00.0000000Z', 'written')")
                "Ergebnis ohne Schritt wie aus budgeting-k8wq3"
            Budgeting.Persistence.appendTargetChangeStepResult c.Id 2 now (Failed "kaputt") |> run
            match Budgeting.Persistence.loadTargetChanges k current |> run with
            | Ok [ read ] -> Expect.equal (read.Latest, read.DateStep) (Some Written, Some (Failed "kaputt")) "je Schritt"
            | other -> failtest $"eine Zeile erwartet: {other}"

        testCase "Eine unbekannte Art beim Lesen wird gemeldet, nicht auf einen Standard gelegt" <| fun () ->
            Budgeting.Persistence.initializeBudgetingSchema ()
            let k = Guid.NewGuid()
            Expect.isNone
                (tryExec $"INSERT INTO target_changes (id, occurred_at, month, category_id, category_name, change_kind) VALUES ('{Guid.NewGuid()}', '2026-11-04T09:00:00.0000000Z', '{current}', '{k}', 'X', 'Quatsch')")
                "eingefügt"
            Expect.equal
                (Budgeting.Persistence.loadTargetChanges k current |> run)
                (Error Budgeting.Persistence.TargetChangesUnreadableText)
                "unbekannte Art"
    ]

// ============================================
// Grenze
// ============================================

let private boundaryTests =
    let wanted goal date = Some { NewGoal = goal; NewDate = date; NewRhythm = KeepRhythm }
    let req goal date : TargetChangeRequest = { CategoryId = Guid.NewGuid(); Shown = Some fifty; Wanted = wanted goal date }
    let errorsOf month r =
        match Server.Validation.validateTargetChange current month r with
        | Error errors -> errors
        | Ok () -> []
    testList "Target ändern: Grenze" [
        test "Ungültiger, vergangener und künftiger Monat werden abgelehnt" {
            Expect.equal
                (errorsOf "kein-monat" (req (eur 1m) None))
                [ "Ungültiger Monat: 'kein-monat'. Erwartet wird der Monatserste als JJJJ-MM-01." ]
                "ungültig"
            Expect.equal (errorsOf "2026-10-01" (req (eur 1m) None)) [ TargetEditMonthText ] "vergangen"
            Expect.equal (errorsOf "2026-12-01" (req (eur 1m) None)) [ TargetEditMonthText ] "künftig"
            Expect.isOk (Server.Validation.validateTargetChange current current (req (eur 1m) None)) "laufend"
            // budgeting-c2t9w, Bug: Anlegen im Vormonat meldete den Satz zum Ändern.
            let create = { req (eur 1m) None with Shown = None; Wanted = Some { NewGoal = eur 1m; NewDate = None; NewRhythm = SetRhythm Monthly } }
            Expect.equal (errorsOf "2026-10-01" create) [ TargetCreateMonthText ] "anlegen im Vormonat"
            Expect.equal (errorsOf "2026-10-01" { req (eur 1m) None with Wanted = None }) [ TargetEditMonthText ] "entfernen im Vormonat"
            Expect.isOk (Server.Validation.validateTargetChange current current { req (eur 1m) None with Wanted = None }) "entfernen"
        }
        test "Alle Fehler werden gesammelt" {
            let r = { req { Amount = -1m; Currency = "USD" } (Some "01.06.2027") with CategoryId = Guid.Empty }
            let errors = errorsOf "2026-10-01" r
            Expect.equal errors.Length 5 $"Monat, Kategorie, Währung, Betrag, Datum: {errors}"
        }
        test "Betrag negativ, nicht auf den Cent, über der Obergrenze und Datum falsch werden je gemeldet" {
            let one goal date = errorsOf current (req goal date)
            Expect.stringContains (one (eur -0.01m) None |> String.concat "") "negativ" "negativ"
            Expect.stringContains (one (eur 1.005m) None |> String.concat "") "Cent" "Cent"
            Expect.stringContains (one (eur 1000000.01m) None |> String.concat "") "zu groß" "Obergrenze"
            Expect.stringContains (one (eur Decimal.MaxValue) None |> String.concat "") "zu groß" "Rand von decimal"
            Expect.stringContains (one (eur 1m) (Some "2027-02-30") |> String.concat "") "Datum" "kein Kalendertag"
            Expect.stringContains (one { Amount = 1m; Currency = "USD" } None |> String.concat "") "EUR" "Währung"
            Expect.isEmpty (one (eur 0m) (Some "2027-02-28")) "0 und ein echtes Datum gehen"
            Expect.isEmpty (one (eur 1000000m) None) "1.000.000,00 € gehen genau noch"
        }
        // budgeting-g2ty9 (X8): beides fiel bisher erst im Ablauf auf (Rhythmus) oder gar nicht (0 €).
        test "Anlegen ohne Rhythmus und Anlegen mit 0 € werden an der Grenze mitgesammelt" {
            let create goal rhythm : TargetChangeRequest =
                { CategoryId = Guid.NewGuid(); Shown = None; Wanted = Some { NewGoal = goal; NewDate = None; NewRhythm = rhythm } }
            Expect.equal (errorsOf current (create (eur 0m) KeepRhythm)) [ TargetRhythmMissingText; TargetCreateZeroText ] "beide"
            Expect.equal (errorsOf current (create (eur 50m) KeepRhythm)) [ TargetRhythmMissingText ] "ohne Rhythmus"
            Expect.equal (errorsOf current (create (eur 0m) (SetRhythm Monthly))) [ TargetCreateZeroText ] "0 €"
            Expect.equal (errorsOf "2026-12-01" (create (eur -1m) KeepRhythm)).Length 3 "mit Monat und negativem Betrag gesammelt"
            Expect.isEmpty (errorsOf current (create (eur 50m) (SetRhythm Monthly))) "50 € monatlich geht"
        }
        test "Der Live-Anschluss liest Token und Budget für Target ändern aus den Settings" {
            // Ohne Budget endet der Weg vor dem Netz: die Meldung zeigt, dass das Token ankam.
            let setting key = async { return (if key = "ynab_token" then Some "das-token" else None) }
            let zone = TimeZoneInfo.FindSystemTimeZoneById "Europe/Berlin"
            let api = Server.Api.liveBudgetApi setting zone
            let month = Domain.currentMonthKey (Server.Api.berlinToday zone DateTime.UtcNow)
            Expect.equal (api.changeCategoryTarget month (req (eur 1m) None) |> run) (Error "Kein Standard-Budget konfiguriert (Settings)") "bis zum Budget gekommen"
        }
        test "Über die BudgetApi: ändern mit Uhr und Monatsart aus Berlin" {
            Budgeting.Persistence.initializeBudgetingSchema ()
            let f, k = fakeWith (Some fifty)
            let settings key = async { return Some $"wert-{key}" }
            let framePort _ _ : QueryHandlers.BudgetFramePort =
                { FetchMonths = (fun () -> async { return Ok [] }); FetchCategoryOrder = (fun () -> async { return Ok [] }) }
            // In Berlin schon der 1. Dezember: der November ist vergangen.
            let lateUtc = DateTime(2026, 11, 30, 23, 30, 0, DateTimeKind.Utc)
            let api = Server.Api.budgetApiWith settings (fun _ _ -> f.Port) framePort (fun () -> lateUtc) (TimeZoneInfo.FindSystemTimeZoneById "Europe/Berlin")
            let r = { req (eur 60m) None with CategoryId = k }
            Expect.equal (api.changeCategoryTarget "2026-11-01" r |> run) (Error TargetEditMonthText) "November vergangen"
            Expect.equal (api.changeCategoryTarget "2026-12-01" r |> run) (Ok (TargetWritten (Some (targetOf Need 60m None)))) "Dezember geschrieben"
            use conn = global.Persistence.getConnection ()
            let stamped =
                Dapper.SqlMapper.ExecuteScalar<string>(conn, "SELECT occurred_at || '|' || month FROM target_changes WHERE category_id = @id", {| id = string k |})
            Expect.equal stamped "2026-11-30T23:30:00.0000000Z|2026-12-01" "mit der Uhr gestempelt, Monat der laufende"
        }
    ]

// ============================================
// Anlegen und Wechsel der Art (budgeting-xp46a)
// ============================================

let private created (frequency: TargetFrequency) (goal: decimal) (date: string option) : TargetDraft =
    { NewGoal = eur goal; NewDate = date; NewRhythm = SetRhythm frequency }

let private withRhythm (frequency: TargetFrequency) goal date : TargetPatch =
    { GoalTarget = SetTo goal; TargetDate = date; Frequency = Some frequency }

let private createPlanTests =
    let plan shown wanted =
        match Domain.planTargetWrite shown wanted with
        | Ok patches -> patches
        | Error e -> failtest e
    let need = targetOf Need 50m None
    let yearlyDated = { targetOf Need 600m (Some "2027-06-01") with Rhythm = Years 1; Day = None }
    testList "Target anlegen und Art wechseln: Planung" [
        test "Anlegen ohne Datum: je Rhythmus ein Patch mit goal_target und goal_frequency" {
            for f in [ Monthly; Weekly; Yearly ] do
                Expect.equal (plan None (Some (created f 50m None))) [ withRhythm f 50000L Keep ] $"{f}"
        }
        test "Anlegen mit Datum: zwei Patches, der erste ohne Datum, der zweite nur mit Datum, für alle drei Rhythmen" {
            for f in [ Monthly; Weekly; Yearly ] do
                Expect.equal
                    (plan None (Some (created f 50m (Some "2027-06-01"))))
                    [ withRhythm f 50000L Keep; patch Keep (SetTo "2027-06-01") ]
                    $"{f}"
        }
        test "Anlegen ohne Rhythmus ist ein Fehler, ohne Target und ohne Wunsch gibt es nichts zu tun" {
            Expect.equal (Domain.planTargetWrite None (Some (draft 50m None))) (Error TargetRhythmMissingText) "ohne Rhythmus"
            Expect.equal (Domain.planTargetWrite None None) (Ok []) "nichts"
        }
        test "Monatlich auf wöchentlich ist ein Patch mit goal_frequency, der Betrag geht mit" {
            Expect.equal (plan (Some need) (Some (created Weekly 50m None))) [ withRhythm Weekly 50000L Keep ] "Wechsel"
        }
        test "Jährlich mit Datum auf jährlich mit neuem Betrag ist nur goal_target, ohne goal_frequency" {
            Expect.equal
                (plan (Some yearlyDated) (Some (created Yearly 700m (Some "2027-06-01"))))
                [ patch (SetTo 700000L) Keep ]
                "kein Wechsel"
        }
        test "TB und MF auf monatlich sind ein Wechsel, auch wenn MF schon monatlich ist" {
            Expect.equal (plan (Some { need with Kind = TargetBalance; Rhythm = NoRhythm }) (Some (created Monthly 50m None))) [ withRhythm Monthly 50000L Keep ] "TB"
            Expect.equal (plan (Some { need with Kind = MonthlyFunding }) (Some (created Monthly 50m None))) [ withRhythm Monthly 50000L Keep ] "MF"
        }
        test "Ein Wechsel mit Datum im Entwurf ergibt zwei Patches" {
            Expect.equal
                (plan (Some need) (Some (created Yearly 600m (Some "2027-06-01"))))
                [ withRhythm Yearly 600000L Keep; patch Keep (SetTo "2027-06-01") ]
                "zwei"
        }
        test "Nach Schritt 1 ergibt dasselbe Speichern gegen den neuen Stand nur das Datum" {
            let afterStep1 = { yearlyDated with Date = None; Goal = eur 600m }
            Expect.equal (plan (Some afterStep1) (Some (created Yearly 600m (Some "2027-06-01")))) [ patch Keep (SetTo "2027-06-01") ] "nur Datum"
        }
        test "Schritt 2 geht nur nach einer Antwort mit dem neuen Rhythmus hinaus, ohne goal_frequency immer" {
            let step1 = withRhythm Yearly 600000L Keep
            let answer rhythm = Some { need with Rhythm = rhythm; Day = None }
            Expect.isTrue (Domain.continueAfter (answer (Years 1)) step1) "jährlich"
            Expect.isFalse (Domain.continueAfter (answer (Months 1)) step1) "monatlich statt jährlich"
            Expect.isFalse (Domain.continueAfter (Some { need with Kind = TargetBalance; Rhythm = Years 1 }) step1) "nicht NEED"
            Expect.isFalse (Domain.continueAfter None step1) "kein Target"
            Expect.isTrue (Domain.continueAfter None (patch (SetTo 1L) Keep)) "ohne goal_frequency"
        }
        // budgeting-c2t9w, Bug: der Grund hieß immer "anderer Rhythmus", auch bei MF statt NEED ("monatlich
        // statt monatlich") und ohne Target.
        test "Der Grund für einen zurückgehaltenen Schritt 2 unterscheidet kein Target, eine andere Art und einen anderen Rhythmus" {
            Expect.equal (Domain.heldBackCause None Weekly) NoTargetAfterFirst "kein Target"
            Expect.equal (Domain.heldBackCause (Some { need with Kind = MonthlyFunding; Rhythm = Months 1 }) Monthly) (OtherKindAfterFirst MonthlyFunding) "MF statt NEED"
            Expect.equal (Domain.heldBackCause (Some { need with Rhythm = Months 1 }) Yearly) (OtherRhythmAfterFirst (Months 1, Yearly)) "anderer Rhythmus"
        }
    ]

// budgeting-c2t9w, Bug: der Client hängte "Speichern setzt das Datum erneut." ohne Satzzeichen an, auch wenn BB
// Schritt 2 selbst zurückgehalten hatte, und der zurückgehaltene Fall hieß immer "anderer Rhythmus".
let private partlyTextTests =
    let date = "2027-06-01"
    testList "Target anlegen und Art wechseln: Meldung beim Teilergebnis" [
        test "Datum abgelehnt mit Grund ohne Punkt: der Satz bekommt einen Punkt und den Handlungssatz" {
            Expect.equal
                (targetPartlyText (TargetCreated Yearly) date (DateRefused "YNAB hat abgelehnt: kaputt"))
                "Target angelegt, Datum 01.06.2027 nicht gesetzt: YNAB hat abgelehnt: kaputt. Speichern setzt das Datum erneut."
                "angelegt"
        }
        test "Datum abgelehnt mit Grund mit Punkt: kein doppelter Punkt" {
            Expect.equal
                (targetPartlyText (TargetKindChanged Yearly) date (DateRefused "Das Datum liegt in der Vergangenheit."))
                "Target geändert, Datum 01.06.2027 nicht gesetzt: Das Datum liegt in der Vergangenheit. Speichern setzt das Datum erneut."
                "geändert"
        }
        test "Zurückgehalten ohne Target in YNABs Antwort: eigener Satz ohne Handlungssatz" {
            Expect.equal
                (targetPartlyText (TargetCreated Yearly) date (DateHeldBack NoTargetAfterFirst))
                "Target angelegt, Datum 01.06.2027 nicht gesetzt: YNAB zeigt nach Schritt 1 kein Target."
                "kein Target"
        }
        test "Zurückgehalten mit anderer Art: der Satz nennt die Art" {
            Expect.equal
                (targetPartlyText (TargetKindChanged Monthly) date (DateHeldBack (OtherKindAfterFirst MonthlyFunding)))
                "Target geändert, Datum 01.06.2027 nicht gesetzt: YNAB zeigt nach Schritt 1 die Art MF statt NEED."
                "MF"
        }
        test "Zurückgehalten mit anderem Rhythmus: der Satz nennt beide Rhythmen" {
            Expect.equal
                (targetPartlyText (TargetKindChanged Yearly) date (DateHeldBack (OtherRhythmAfterFirst (Months 1, Yearly))))
                "Target geändert, Datum 01.06.2027 nicht gesetzt: YNAB zeigt nach Schritt 1 monatlich statt jährlich."
                "Rhythmus"
        }
        test "Außerhalb des laufenden Monats: beim Anlegen der Satz zum Anlegen, sonst der zum Ändern" {
            let shown = Some (targetOf Need 50m None)
            let wanted = Some (created Monthly 50m None)
            let refusal s w = targetMonthRefusalText { CategoryId = Guid.NewGuid(); Shown = s; Wanted = w }
            Expect.equal (refusal None wanted) TargetCreateMonthText "anlegen"
            Expect.equal (refusal shown wanted) TargetEditMonthText "ändern"
            Expect.equal (refusal shown None) TargetEditMonthText "entfernen"
        }
    ]

let private lossTests =
    let need = { targetOf Need 50m None with Day = None }
    testList "Target anlegen und Art wechseln: Verluste" [
        test "Tag gesetzt und Wechsel ergibt LosesDay" {
            Expect.equal (targetChangeLosses { need with Day = Some 15 } (created Weekly 50m None)) [ LosesDay ] "Tag"
        }
        test "Datum gesetzt und Wechsel ohne Datum ergibt LosesDate, auch bei gleichem Rhythmus, wenn goal_frequency hinausgeht" {
            let dated = { need with Date = Some "2027-06-01" }
            Expect.equal (targetChangeLosses dated (created Yearly 50m None)) [ LosesDate ] "anderer Rhythmus"
            Expect.equal (targetChangeLosses { dated with Kind = MonthlyFunding } (created Monthly 50m None)) [ LosesDate; LosesKind ] "gleicher Rhythmus bei MF"
            Expect.isEmpty (targetChangeLosses dated (created Yearly 50m (Some "2027-07-01"))) "neues Datum im Entwurf"
        }
        test "MF ergibt LosesKind, TB auch" {
            Expect.equal (targetChangeLosses { need with Kind = MonthlyFunding } (created Weekly 50m (Some "2027-06-01"))) [ LosesKind ] "MF"
            Expect.equal (targetChangeLosses { need with Kind = TargetBalance; Rhythm = NoRhythm } (created Monthly 50m None)) [ LosesKind ] "TB"
        }
        test "Alle 3 Monate ergibt LosesRhythm, ein NEED ohne Kadenz auch" {
            Expect.equal (targetChangeLosses { need with Rhythm = Months 3 } (created Monthly 50m None)) [ LosesRhythm ] "alle 3 Monate"
            Expect.equal (targetChangeLosses { need with Rhythm = Years 2 } (created Yearly 50m None)) [ LosesRhythm ] "alle 2 Jahre"
            Expect.equal (targetChangeLosses { need with Rhythm = NoRhythm } (created Monthly 50m None)) [ LosesRhythm ] "ohne Kadenz"
        }
        test "Reine Betragsänderung und gleicher Rhythmus ergeben eine leere Liste" {
            let full = { need with Day = Some 15; Date = Some "2027-06-01" }
            Expect.isEmpty (targetChangeLosses full (draft 60m None)) "nur Betrag, sogar ohne Datum"
            Expect.isEmpty (targetChangeLosses full (created Monthly 60m None)) "gleicher Rhythmus"
        }
    ]

let private createEncoderTests =
    let id1 = Guid.Parse "11111111-1111-1111-1111-111111111111"
    testList "Target anlegen und Art wechseln: YNAB" [
        test "goal_frequency geht mit goal_target als JSON-Zahl hinaus" {
            Expect.equal
                (Server.YnabClient.buildTargetBody (withRhythm Monthly 50000L Keep))
                (Ok """{"category":{"goal_target":50000,"goal_frequency":"monthly"}}""")
                "monatlich"
            for f, text in [ Weekly, "weekly"; Yearly, "yearly" ] do
                Expect.equal
                    (Server.YnabClient.buildTargetBody { patch Keep Keep with Frequency = Some f })
                    (Ok $"""{{"category":{{"goal_frequency":"{text}"}}}}""")
                    text
        }
        test "Ein Body mit goal_frequency und goal_target_date zugleich wird abgelehnt und nicht gesendet" {
            Expect.isError (Server.YnabClient.buildTargetBody (withRhythm Yearly 50000L (SetTo "2027-06-01"))) "Datum gesetzt"
            Expect.isError (Server.YnabClient.buildTargetBody (withRhythm Yearly 50000L Clear)) "Datum null"
            let sent = ref 0
            let send _ _ = async { sent.Value <- sent.Value + 1; return 200, "{}" }
            match Server.YnabClient.setCategoryTargetWith send "bud" id1 (withRhythm Yearly 50000L (SetTo "2027-06-01")) |> run with
            | Error (YnabError.InvalidResponse _) -> ()
            | other -> failtest $"Ablehnung erwartet: {other}"
            Expect.equal sent.Value 0 "nichts hinaus"
        }
    ]

/// Ergebnisse der Änderungen einer Kategorie je Schritt: "Schritt|Status|Grund".
let private stepRows (id: Guid) : string list =
    use conn = global.Persistence.getConnection ()
    Dapper.SqlMapper.Query<string>(
        conn,
        "SELECT IFNULL(r.step, '-') || '|' || r.status || '|' || IFNULL(r.reason, '') \
         FROM target_change_results r JOIN target_changes c ON r.change_id = c.id \
         WHERE c.category_id = @id ORDER BY r.sequence",
        {| id = string id |})
    |> Seq.toList

let private historyOf (k: Guid) : (string * HistoryStatus option) list =
    let port = { QueryHandlers.liveHistoryPort with LoadMonthlyActivity = fun _ _ -> async { return [] } }
    match QueryHandlers.categoryHistory port k (DateTime(2026, 11, 1)) |> run with
    | Ok history -> history.Entries |> List.map historyEntryLine |> List.map (fun l -> l.Text, l.Status)
    | Error e -> failtest e

let private createFlowTests =
    let testCase name body =
        testCase name (fun () ->
            Budgeting.Persistence.initializeBudgetingSchema ()
            body ())
    let yearly600 = { Goal = eur 600m; Rhythm = Years 1; Kind = Need; Date = None; NeedsWholeAmount = Some true; Day = None }
    let rejected = "YNAB hat abgelehnt: goal_target_date is invalid"
    testSequenced
    <| testList "Target anlegen und Art wechseln: Abläufe" [
        testCase "GIVEN Kategorie ohne Target WHEN anlegen monatlich 50 € THEN eine Target-Änderung, ein PATCH mit goal_target und goal_frequency monthly, geschrieben" <| fun () ->
            let f, k = fakeWith None
            match change f current (request k None (Some (created Monthly 50m None))) with
            | Ok (Ok (TargetWritten (Some t))) -> Expect.equal (t.Goal, t.Rhythm, t.Kind) (eur 50m, Months 1, Need) "neues Target"
            | other -> failtest $"geschrieben erwartet: {other}"
            Expect.equal (f.Patches |> List.ofSeq) [ k, withRhythm Monthly 50000L Keep ] "ein PATCH"
            Expect.equal (changeRows k) [ $"{current}|Angelegt|-|50000|written|" ] "eine Änderung, geschrieben"
            Expect.equal (stepRows k) [ "1|written|" ] "Ergebnis zu Schritt 1"
            Expect.equal (historyOf k) [ "Target angelegt, 50,00 € monatlich", Some HistoryWritten ] "Historie"

        testCase "GIVEN anlegen jährlich mit Datum THEN zwei PATCHes in dieser Reihenfolge, je ein Ergebnis, die Historie zeigt eine Änderung" <| fun () ->
            let f, k = fakeWith None
            match change f current (request k None (Some (created Yearly 600m (Some "2027-06-01")))) with
            | Ok (Ok (TargetWritten (Some t))) -> Expect.equal t { yearly600 with Date = Some "2027-06-01" } "Target mit Datum"
            | other -> failtest $"geschrieben erwartet: {other}"
            Expect.equal
                (f.Patches |> List.ofSeq)
                [ k, withRhythm Yearly 600000L Keep; k, patch Keep (SetTo "2027-06-01") ]
                "erst Rhythmus, dann Datum"
            Expect.equal (stepRows k) [ "1|written|"; "2|written|" ] "je Schritt ein Ergebnis"
            Expect.equal (historyOf k) [ "Target angelegt, 600,00 € jährlich, Datum 01.06.2027", Some HistoryWritten ] "eine Änderung"

        testCase "GIVEN Schritt 1 scheitert THEN Schritt 2 geht nicht hinaus und ist fehlgeschlagen mit Schritt 1 fehlgeschlagen, die Meldung nennt YNABs Grund" <| fun () ->
            let f, k = fakeWith None
            f.Failure <- Some (YnabError.InvalidResponse "goal_target is invalid")
            let expected = "YNAB hat abgelehnt: goal_target is invalid"
            Expect.equal (change f current (request k None (Some (created Yearly 600m (Some "2027-06-01"))))) (Ok (Error expected)) "Meldung"
            Expect.equal f.Patches.Count 1 "nur Schritt 1 hinaus"
            Expect.equal (stepRows k) [ $"1|failed|{expected}"; "2|failed|Schritt 1 fehlgeschlagen" ] "beide fehlgeschlagen, wie beim Ziel einer Verschiebung"
            Expect.equal
                (historyOf k)
                [ "Target angelegt, 600,00 € jährlich, Datum 01.06.2027", Some (HistoryFailed expected) ]
                "Historie: fehlgeschlagen mit YNABs Grund"

        testCase "GIVEN Schritt 2 scheitert THEN Schritt 1 geschrieben, Antwort mit dem Stand ohne Datum und Datum nicht gesetzt, ein folgender Aufruf sendet nur goal_target_date" <| fun () ->
            let f, k = fakeWith None
            f.Failure <- Some (YnabError.InvalidResponse "goal_target_date is invalid")
            f.FailOnlyCall <- Some 2
            let wish = created Yearly 600m (Some "2027-06-01")
            let expected = $"Target angelegt, Datum 01.06.2027 nicht gesetzt: {rejected}. Speichern setzt das Datum erneut."
            Expect.equal (change f current (request k None (Some wish))) (Ok (Ok (TargetPartlyWritten (Some yearly600, expected)))) "Teilergebnis"
            Expect.equal (stepRows k) [ "1|written|"; $"2|failed|{rejected}" ] "Schritt 2 fehlgeschlagen"
            // budgeting-g2ty9 (X7): vorher stand die Änderung ganz als fehlgeschlagen in der Historie.
            Expect.equal
                (historyOf k)
                [ "Target angelegt, 600,00 € jährlich, Datum 01.06.2027 nicht gesetzt", Some (HistoryPartlyWritten rejected) ]
                "Historie: Teilergebnis mit eigenem Stand"
            f.Failure <- None
            let before = f.Patches.Count
            Expect.equal
                (change f current (request k (Some yearly600) (Some wish)))
                (Ok (Ok (TargetWritten (Some { yearly600 with Date = Some "2027-06-01" }))))
                "Datum nachgetragen"
            Expect.equal (f.Patches |> Seq.skip before |> List.ofSeq) [ k, patch Keep (SetTo "2027-06-01") ] "nur goal_target_date"

        testCase "GIVEN YNABs Antwort auf Schritt 1 zeigt nicht den erwarteten Rhythmus THEN Schritt 2 geht nicht hinaus, Meldung konkret" <| fun () ->
            let f, k = fakeWith (Some fifty)
            f.AnswerRhythm <- Some (Months 1)
            let reason = "YNAB zeigt nach Schritt 1 monatlich statt jährlich"
            let after = { yearly600 with Rhythm = Months 1 }
            Expect.equal
                (change f current (request k (Some fifty) (Some (created Yearly 600m (Some "2027-06-01")))))
                (Ok (Ok (TargetPartlyWritten (Some after, $"Target geändert, Datum 01.06.2027 nicht gesetzt: {reason}."))))
                "gemeldet mit eigenem Text (budgeting-g2ty9, X9)"
            Expect.equal (f.Patches |> List.ofSeq) [ k, withRhythm Yearly 600000L Keep ] "Schritt 2 nicht gesendet"
            Expect.equal (stepRows k) [ "1|written|"; $"2|failed|{reason}" ] "Schritt 2 zurückgehalten"
            Expect.equal (changeRows k) [ $"{current}|Art gewechselt|50000|600000|written|"; $"{current}|Art gewechselt|50000|600000|failed|{reason}" ] "eine Änderung"

        // budgeting-g2ty9 (X9), Bug: nach geschriebenem Schritt 1 ging ein Fehler beim Anhängen des Ergebnisses
        // von Schritt 2 als Ausnahme durch Remoting, obwohl YNAB das Target schon angelegt hatte. Ein Trigger nur
        // für Schritt 2 dieser Kategorie lässt das Anhängen scheitern.
        testCase "GIVEN Schritt 1 geschrieben, das Ergebnis von Schritt 2 lässt sich nicht anhängen THEN kommt das Teilergebnis trotzdem zurück" <| fun () ->
            let blockStep2 (k: Guid) (body: unit -> unit) =
                let trigger = $"fail_step2_{k:N}"
                use conn = global.Persistence.getConnection ()
                Dapper.SqlMapper.Execute(
                    conn,
                    $"CREATE TRIGGER {trigger} BEFORE INSERT ON target_change_results \
                      WHEN NEW.step = 2 AND NEW.change_id IN (SELECT id FROM target_changes WHERE category_id = '{k}') \
                      BEGIN SELECT RAISE(ABORT, 'kaputt'); END;")
                |> ignore
                try body () finally Dapper.SqlMapper.Execute(conn, $"DROP TRIGGER {trigger}") |> ignore
            let wish = created Yearly 600m (Some "2027-06-01")
            // YNAB lehnt das Datum ab.
            let f, k = fakeWith None
            f.Failure <- Some (YnabError.InvalidResponse "goal_target_date is invalid")
            f.FailOnlyCall <- Some 2
            blockStep2 k (fun () ->
                Expect.equal
                    (change f current (request k None (Some wish)))
                    (Ok (Ok (TargetPartlyWritten (Some yearly600, $"Target angelegt, Datum 01.06.2027 nicht gesetzt: {rejected}. Speichern setzt das Datum erneut."))))
                    "abgelehnt: Teilergebnis")
            Expect.equal (stepRows k) [ "1|written|" ] "Schritt 2 ohne Ergebnis"
            // YNABs Antwort zeigt einen anderen Rhythmus, Schritt 2 bleibt zurück.
            let f, k = fakeWith None
            f.AnswerRhythm <- Some (Months 1)
            blockStep2 k (fun () ->
                match change f current (request k None (Some wish)) with
                | Ok (Ok (TargetPartlyWritten (_, message))) ->
                    Expect.equal message "Target angelegt, Datum 01.06.2027 nicht gesetzt: YNAB zeigt nach Schritt 1 monatlich statt jährlich." "zurückgehalten"
                | other -> failtest $"Teilergebnis erwartet: {other}")

        testCase "GIVEN in YNAB inzwischen ein Target, gezeigt war keins THEN nichts gesendet, TargetChangedInYnab" <| fun () ->
            let f, k = fakeWith (Some fifty)
            Expect.equal (change f current (request k None (Some (created Monthly 50m None)))) (Ok (Ok (TargetChangedInYnab (Some fifty)))) "YNAB gewinnt"
            Expect.isEmpty f.Patches "kein PATCH"
            Expect.isEmpty (changeRows k) "keine Zeile"

        testCase "GIVEN künftiger oder vergangener Monat WHEN anlegen oder Art wechseln THEN Fehler ohne YNAB-Aufruf, keine Zeile" <| fun () ->
            let f, k = fakeWith None
            for month in [ "2026-10-01"; "2026-12-01" ] do
                // budgeting-c2t9w: Anlegen meldete "Target ändern geht nur im laufenden Monat.".
                Expect.equal (change f month (request k None (Some (created Monthly 50m None)))) (Ok (Error TargetCreateMonthText)) $"{month} anlegen"
                Expect.equal (change f month (request k (Some fifty) (Some (created Weekly 50m None)))) (Ok (Error TargetEditMonthText)) $"{month} Art wechseln"
            Expect.equal f.Reads 0 "kein YNAB-Aufruf"
            Expect.isEmpty (changeRows k) "keine Zeile"

        testCase "Die Historie nennt einen Wechsel der Art mit Rhythmus und Betrag" <| fun () ->
            let f, k = fakeWith (Some fifty)
            Expect.isOk (change f current (request k (Some fifty) (Some (created Weekly 12m None)))) "geschrieben"
            Expect.equal (historyOf k) [ "Rhythmus wöchentlich, Target 50,00 € → 12,00 €", Some HistoryWritten ] "Text"
    ]

let private createPersistenceTests =
    testList "Target anlegen und Art wechseln: Persistenz" [
        test "Auf einer Datenbank ohne die neuen Spalten ergänzt das Schema sie, ein zweiter Lauf ändert nichts" {
            // Eigene Verbindung: die Tabellen in der Form aus budgeting-k8wq3, dann die Migration.
            use conn = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:")
            conn.Open()
            Dapper.SqlMapper.Execute(
                conn,
                "CREATE TABLE target_changes (id TEXT PRIMARY KEY, change_kind TEXT NOT NULL); \
                 CREATE TABLE target_change_results (sequence INTEGER PRIMARY KEY AUTOINCREMENT, change_id TEXT NOT NULL)")
            |> ignore
            Budgeting.Persistence.migrateTargetChangeColumns conn
            Budgeting.Persistence.migrateTargetChangeColumns conn
            let columns table =
                Dapper.SqlMapper.Query<string>(conn, $"SELECT name FROM pragma_table_info('{table}')") |> Seq.toList
            Expect.contains (columns "target_changes") "new_frequency" "Rhythmus"
            Expect.contains (columns "target_change_results") "step" "Schritt"
        }
    ]

// budgeting-k8wq3
[<Tests>]
let tests =
    testList "Target ändern und entfernen" [ planTests; historyTextTests; ynabTests; flowTests; persistenceTests; boundaryTests ]

// budgeting-xp46a, Korrekturen aus budgeting-g2ty9
[<Tests>]
let createTests =
    testList "Target anlegen und Art wechseln" [
        createPlanTests; partlyTextTests; lossTests; createEncoderTests; createFlowTests; createPersistenceTests ]
