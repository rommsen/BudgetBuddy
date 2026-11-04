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
        match Domain.planTargetWrite shown wanted with
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
        test "Kein Patch trägt goal_frequency" {
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
        test "Ein Rhythmuswechsel wird abgelehnt" {
            let wanted = Some { draft 60m None with NewRhythm = SetRhythm Yearly }
            Expect.equal (Domain.planTargetWrite need wanted) (Error TargetRhythmLaterText) "später"
        }
        test "Anlegen und Rhythmuswechsel lehnt die Vorprüfung ab, Ändern und Entfernen nicht" {
            let request shown wanted : TargetChangeRequest = { CategoryId = Guid.NewGuid(); Shown = shown; Wanted = wanted }
            Expect.equal (Domain.targetChangeSupported (request None (Some (draft 10m None)))) (Error TargetCreateLaterText) "anlegen"
            Expect.equal
                (Domain.targetChangeSupported (request (Some need) (Some { draft 10m None with NewRhythm = SetRhythm Weekly })))
                (Error TargetRhythmLaterText)
                "Rhythmus"
            Expect.isOk (Domain.targetChangeSupported (request (Some need) (Some (draft 10m None)))) "ändern"
            Expect.isOk (Domain.targetChangeSupported (request (Some need) None)) "entfernen"
        }
        test "Target ändern geht nur im laufenden Monat" {
            Expect.isTrue (targetEditAllowed CurrentMonth) "laufend"
            Expect.isFalse (targetEditAllowed PastMonth) "vergangen"
            Expect.isFalse (targetEditAllowed FutureMonth) "künftig"
        }
        test "Die Art einer Target-Änderung geht als Text hin und zurück, ein unbekannter wird gemeldet" {
            for kind in [ TargetEdited; TargetRemoved ] do
                Expect.equal (Domain.targetChangeKindOfText (Domain.targetChangeKindText kind)) (Ok kind) $"{kind}"
            Expect.isError (Domain.targetChangeKindOfText "Quatsch") "kein Standard"
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
          Latest = Some Written }
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
            Expect.equal (line (Some Written)) HistoryWritten "geschrieben"
            Expect.equal (line (Some (Failed "kaputt"))) (HistoryFailed "kaputt") "fehlgeschlagen"
            Expect.equal (line None) HistoryOpen "offen"
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
                    match this.Failure with
                    | Some e -> return Error e
                    | None ->
                        let after =
                            match targets[id], p.GoalTarget with
                            | _, Clear -> None
                            | None, _ -> None
                            | Some t, goal ->
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
                Expect.equal (lines |> List.map (fun l -> l.Text, l.Status)) [ "Target entfernt", HistoryWritten ] "Historie"
            | Error e -> failtest e

        testCase "Vergangener und künftiger Monat, Rhythmuswechsel und Anlegen: Fehler ohne YNAB-Aufruf und ohne Zeile" <| fun () ->
            let f, k = fakeWith (Some fifty)
            let reason month r =
                match change f month r with
                | Ok (Error message) -> message
                | other -> failtest $"Ablehnung erwartet: {other}"
            Expect.equal (reason "2026-10-01" (request k (Some fifty) (Some (draft 60m None)))) TargetEditMonthText "vergangen"
            Expect.equal (reason "2026-12-01" (request k (Some fifty) (Some (draft 60m None)))) TargetEditMonthText "künftig"
            Expect.equal
                (reason current (request k (Some fifty) (Some { draft 60m None with NewRhythm = SetRhythm Monthly })))
                TargetRhythmLaterText
                "Rhythmus"
            Expect.equal (reason current (request k None (Some (draft 60m None)))) TargetCreateLaterText "anlegen"
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
            let free = changeTarget f.Port now current current (request b (Some fifty) (Some (draft 60m None))) |> run
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
                { Id = Guid.NewGuid(); OccurredAt = at 3; Month = current; CategoryId = k; CategoryName = "Urlaub"; Kind = Domain.Paused }
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
            Budgeting.Persistence.appendTargetChangeResult c2.Id now (Failed "Rate-Limit") |> run
            Budgeting.Persistence.appendTargetChangeResult c2.Id now Written |> run
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
            Budgeting.Persistence.appendTargetChangeResult c.Id now Written |> run
            Expect.isSome (tryExec $"UPDATE target_changes SET change_kind = 'Geändert' WHERE category_id = '{k}'") "UPDATE Änderung"
            Expect.isSome (tryExec $"DELETE FROM target_changes WHERE category_id = '{k}'") "DELETE Änderung"
            Expect.isSome (tryExec $"UPDATE target_change_results SET status = 'failed' WHERE change_id = '{c.Id}'") "UPDATE Ergebnis"
            Expect.isSome (tryExec $"DELETE FROM target_change_results WHERE change_id = '{c.Id}'") "DELETE Ergebnis"
            match Budgeting.Persistence.loadTargetChanges k current |> run with
            | [ read ] ->
                Expect.equal
                    (read.ChangeKind, read.GoalBefore, read.GoalAfter, read.DateBefore, read.DateAfter, read.Latest)
                    (TargetRemoved, Some (eur 1m), None, Some "2027-01-01", None, Some Written)
                    "nullbare Werte gelesen"
            | other -> failtest $"eine Zeile erwartet: {other}"

        testCase "Eine unbekannte Art beim Lesen wird gemeldet, nicht auf einen Standard gelegt" <| fun () ->
            Budgeting.Persistence.initializeBudgetingSchema ()
            let k = Guid.NewGuid()
            Expect.isNone
                (tryExec $"INSERT INTO target_changes (id, occurred_at, month, category_id, category_name, change_kind) VALUES ('{Guid.NewGuid()}', '2026-11-04T09:00:00.0000000Z', '{current}', '{k}', 'X', 'Quatsch')")
                "eingefügt"
            Expect.throws (fun () -> Budgeting.Persistence.loadTargetChanges k current |> run |> ignore) "unbekannte Art"
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
            Expect.equal (errorsOf "kein-monat" (req (eur 1m) None)).Length 1 "ungültig"
            Expect.equal (errorsOf "2026-10-01" (req (eur 1m) None)) [ TargetEditMonthText ] "vergangen"
            Expect.equal (errorsOf "2026-12-01" (req (eur 1m) None)) [ TargetEditMonthText ] "künftig"
            Expect.isOk (Server.Validation.validateTargetChange current current (req (eur 1m) None)) "laufend"
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

[<Tests>]
let tests =
    testList "Target ändern und entfernen (budgeting-k8wq3)" [ planTests; historyTextTests; ynabTests; flowTests; persistenceTests; boundaryTests ]
