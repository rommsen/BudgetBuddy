module RetryTests

// infrastructure-y6atr: Lesende Start-Abfragen wiederholen sich bei einem Netzwerkfehler.
// Verhindert den Befund vom 2026-10-24 (Firefox, "Http error (0)" nach einem Deploy): Der
// Browser brach die Verbindung ab, obwohl der Server mit 200 geantwortet hatte, und die
// Seite blieb nach einem einzigen Abbruch dauerhaft im Fehlerzustand.
// Der Helfer ist generisch über Async; Einordnung und Pause sind Parameter, die Tests
// laufen ohne echte Wartezeit.

open System.IO
open System.Text.RegularExpressions
open Expecto
open Fable.Remoting.Client
open Client

let private networkError = exn "Http error (0)"
let private serverError = exn "Http error (500)"

let private isNetwork (ex: exn) = obj.ReferenceEquals(ex, networkError)

/// Fake-Abfrage: scheitert die ersten `failures` Male, liefert danach `value`.
let private flaky (failures: int) (value: 'a) =
    let calls = ref 0
    let run () = async {
        calls.Value <- calls.Value + 1
        if calls.Value <= failures then return raise networkError
        else return value
    }
    calls, run

/// Liefert Ergebnis und die aufgezeichneten Pausen (je Aufruf eigene Liste, die Tests laufen parallel).
let private runRetryWithPauses isRetryable run =
    let pauses = ResizeArray<int>()
    let sleep (ms: int) = async { pauses.Add ms }
    let result = Retry.withRetry isRetryable [ 10; 20 ] sleep run |> Async.RunSynchronously
    result, List.ofSeq pauses

let private runRetry isRetryable run = runRetryWithPauses isRetryable run |> fst

let private repoRoot =
    Directory.GetParent(Directory.GetParent(__SOURCE_DIRECTORY__).FullName).FullName

let private readClient (relative: string) =
    File.ReadAllText(Path.Combine(repoRoot, "src", "Client", relative))

/// Quelltext ohne Kommentare, damit ein auskommentierter Aufruf nicht als Beleg zählt.
let private withoutComments (source: string) =
    Regex.Replace(Regex.Replace(source, @"\(\*[\s\S]*?\*\)", " "), @"//[^\n]*", " ")

/// Namen aller Abfragen, die im Quelltext über `Retry.reading` laufen, auch teilweise angewendet.
let private wrappedCalls (source: string) =
    Regex.Matches(withoutComments source, @"Retry\.reading\s+\(?Api\.\w+\.(\w+)")
    |> Seq.map (fun m -> m.Groups[1].Value)
    |> List.ofSeq

let private wrappedLenaFlagCall =
    Regex(@"Retry\.reading\s+Api\.settings\.getLenaFlagSettings")

let private unprotectedLenaFlagCalls (source: string) =
    Regex.Matches(source, @"(?<!Retry\.reading\s+)Api\.settings\.getLenaFlagSettings").Count

[<Tests>]
let tests =
    testList "Retry (infrastructure-y6atr)" [
        test "zwei Netzwerkfehler, dritter Versuch liefert: dessen Ergebnis kommt zurück" {
            let calls, run = flaky 2 "ok"
            let result, pauses = runRetryWithPauses isNetwork run
            Expect.equal result "ok" "Ergebnis des dritten Versuchs"
            Expect.equal calls.Value 3 "drei Aufrufe"
            Expect.equal pauses [ 10; 20 ] "Pausen aus dem Parameter, nacheinander"
        }

        test "alle drei Versuche scheitern: Fehler des letzten Versuchs unverändert" {
            let calls = ref 0
            let run () = async {
                calls.Value <- calls.Value + 1
                return raise (exn $"Versuch {calls.Value}")
            }
            let thrown =
                try runRetry (fun _ -> true) run |> ignore; ""
                with e -> e.Message
            Expect.equal calls.Value 3 "höchstens zwei Wiederholungen"
            Expect.equal thrown "Versuch 3" "Meldung stammt vom letzten Versuch"
        }

        test "Fehler ohne Netzwerkeinordnung wird nicht wiederholt" {
            let calls = ref 0
            let run () = async {
                calls.Value <- calls.Value + 1
                return raise serverError
            }
            Expect.throws (fun () -> runRetryWithPauses isNetwork run |> ignore) "wirft sofort"
            Expect.equal calls.Value 1 "genau ein Aufruf"
        }

        test "fachliches Error-Ergebnis wird nicht wiederholt" {
            let calls = ref 0
            let run () = async {
                calls.Value <- calls.Value + 1
                return (Error "Budget unbekannt" : Result<int, string>)
            }
            let result = runRetry (fun _ -> true) run
            Expect.equal result (Error "Budget unbekannt") "Ergebnis unverändert"
            Expect.equal calls.Value 1 "genau ein Aufruf"
        }

        test "Ok-Ergebnis wird nicht wiederholt" {
            let calls, run = flaky 0 (Ok 5 : Result<int, string>)
            let result = runRetry (fun _ -> true) run
            Expect.equal result (Ok 5) "Ergebnis unverändert"
            Expect.equal calls.Value 1 "genau ein Aufruf"
        }

        test "Standardpausen summieren sich auf höchstens 2 Sekunden, höchstens zwei Wiederholungen" {
            Expect.isLessThanOrEqual (List.sum Retry.defaultDelaysMs) 2000 "Summe"
            Expect.equal (List.length Retry.defaultDelaysMs) 2 "zwei Pausen"
        }

        test "isNetworkError erkennt Status 0, nicht aber 500 oder fremde Ausnahmen" {
            let mk status =
                ProxyRequestException({ StatusCode = status; ResponseBody = "" }, "x", "") :> exn
            Expect.isTrue (Retry.isNetworkError (mk 0)) "Status 0"
            Expect.isFalse (Retry.isNetworkError (mk 500)) "Status 500"
            Expect.isFalse (Retry.isNetworkError (mk 404)) "Status 404"
            Expect.isFalse (Retry.isNetworkError (exn "anders")) "andere Ausnahme"
        }

        // infrastructure-r3tw7: Verhindert den Befund vom 2026-10-31 (Fehlerseite im Kassenbuch
        // nach einem Verbindungsabbruch, die Buchungsliste war nicht geschützt). Der
        // Fable.Remoting-Proxy läuft unter .NET nicht, deshalb hält der Test das Inventar je
        // Datei im Quelltext fest. Die Listen sind vollständig: fehlt `Retry.reading` an einer
        // Aufrufstelle oder kommt ein Schreibaufruf dazu, weicht die Datei ab. Das Inventar ist die
        // einzige Absicherung der Aufrufstellen, ein Browser-Wächter je Aufrufstelle besteht nicht
        // (infrastructure-h4q8v).
        test "die Inventare der lesenden Abfragen laufen über den Helfer, kein Schreibaufruf" {
            // Gegenbeweis: der Abgleich erkennt beide Schreibweisen und lässt rohe Aufrufe aus.
            Expect.equal (wrappedCalls "(Retry.reading\n  Api.ledger.a)") [ "a" ] "direkt umhüllt"
            Expect.equal (wrappedCalls "(Retry.reading (Api.ledger.b payee))") [ "b" ] "teilweise angewendet"
            Expect.equal (wrappedCalls "Cmd.OfAsync.either Api.ledger.c") [] "ungeschützt zählt nicht"
            Expect.equal (wrappedCalls "(* Retry.reading Api.ledger.d *) Api.ledger.d") [] "Blockkommentar zählt nicht"
            Expect.equal (wrappedCalls "// Retry.reading Api.ledger.e\nApi.ledger.e") [] "Zeilenkommentar zählt nicht"

            let inventory =
                [ "Components/Settings/State.fs", [ "getSettings"; "getLenaFlagSettings" ]
                  "Components/Rules/State.fs", [ "getAllRules" ]
                  "State.fs", [ "getCategories"; "getCategories"; "getRecentQuickAddTemplates" ]
                  "Components/Ledger/State.fs",
                  [ "getLedgerAccounts"; "getLastMirrorRun"; "getLenaFlagSettings"
                    "getLedgerAccountBalances"; "getLedgerEntriesPage"; "getLedgerEntriesPage"
                    "getEntryHistory"; "getLedgerPayeeSuggestions"; "getPayeeCategorySuggestions"
                    "getBudgetAccounts"; "getSettings"; "getLedgerEntry"; "getLedgerEntry" ]
                  // infrastructure-t5m2b: der Import kam dazu, als seine Browser-Wächter je Abbruch
                  // (getSettings, getTransactions, getCategories, getPayees, getBudgetAccounts) fielen.
                  "Components/SyncFlow/State.fs",
                  [ "getCurrentSession"; "getTransactions"; "getLenaFlagSettings"; "getSettings"; "getSettings"
                    "getSettings"; "getCategories"; "getBudgetAccounts"; "getPayees"; "testRule" ]
                  "Components/Budget/State.fs",
                  [ "getBudgetFrame"; "getBudgetFrame"; "getBudgetMonth"; "previewTargets"; "getCategoryMonthEntries"; "getCategoryHistory"; "getRecentMoveCategories"; "getBudgetHistory" ] ]
            for file, expected in inventory do
                Expect.equal
                    (List.sort (wrappedCalls (readClient file)))
                    (List.sort expected)
                    $"{file}: genau diese, kein Schreibaufruf"
        }

        // infrastructure-awstw: Verhindert den Befund vom 2026-10-29 (Firefox, Toast "Failed to
        // load Lena flag colour: Http error (0)" auf der Settings-Seite). Das ist derselbe
        // Browser-Abbruch wie in infrastructure-y6atr, nur an einer Abfrage, die y6atr nicht
        // umhüllt hatte. Der Fable.Remoting-Proxy läuft unter .NET nicht, deshalb prüft der Test
        // die Verdrahtung im Quelltext.
        test "die Lena-Flag-Farbe wird an allen Aufrufstellen über den Helfer geladen" {
            for file in [ "Settings"; "Ledger"; "SyncFlow" ] do
                let source = readClient $"Components/{file}/State.fs"
                Expect.isTrue (wrappedLenaFlagCall.IsMatch source) $"{file}: über Retry.reading"
        }

        test "im Client steht kein Aufruf der Lena-Flag-Farbe ohne den Helfer" {
            // Gegenbeweis: die Zählung erkennt einen ungeschützten Aufruf und lässt einen
            // umhüllten durch.
            Expect.equal (unprotectedLenaFlagCalls "Api.settings.getLenaFlagSettings ()") 1 "ungeschützt zählt"
            Expect.equal (unprotectedLenaFlagCalls "(Retry.reading\n  Api.settings.getLenaFlagSettings)") 0 "umhüllt zählt nicht"

            let clientRoot = Path.Combine(repoRoot, "src", "Client")
            let isBuildOutput (path: string) =
                path.Contains $"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"
                || path.Contains $"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"
                || path.Contains "fable_modules"
            let sources =
                Directory.GetFiles(clientRoot, "*.fs", SearchOption.AllDirectories)
                |> Array.filter (isBuildOutput >> not)
            Expect.isNonEmpty sources "Client-Quelltext gefunden"
            let unprotected = sources |> Array.sumBy (File.ReadAllText >> unprotectedLenaFlagCalls)
            Expect.equal unprotected 0 "kein Aufruf ohne Retry.reading"
        }
    ]

// budgeting-qm5x9, ADR 0084 Punkt 8: Budget-Entscheidungen tragen eine Befehls-Id und dürfen nach Status 0
// einmal wiederholt werden. Die Pause ist injiziert, die Tests warten nicht.
let private proxyError (status: int) : exn =
    ProxyRequestException({ StatusCode = status; ResponseBody = "" }, "x", "") :> exn

/// Scheitert bei jedem Aufruf mit `error` und zählt die Aufrufe samt Argument.
let private failingWith (error: exn) =
    let seen = ResizeArray<System.Guid>()
    let call (id: System.Guid) : Async<int> = async {
        seen.Add id
        return raise error
    }
    seen, call

let private noSleep (_: int) = async { return () }

[<Tests>]
let writingTests =
    testList "Schreibende Aufrufe mit Befehls-Id" [
        test "Nach Status 0 wiederholt Retry.commandWith genau einmal" {
            let seen, call = failingWith (proxyError 0)
            Expect.throws (fun () -> Retry.commandWith noSleep System.Guid.NewGuid call |> Async.RunSynchronously |> ignore) "der zweite Fehler kommt durch"
            Expect.equal seen.Count 2 "zwei Versuche"
        }
        test "Nach einem anderen Fehler wiederholt Retry.commandWith nie" {
            for error in [ proxyError 500; exn "anders" ] do
                let seen, call = failingWith error
                Expect.throws (fun () -> Retry.commandWith noSleep System.Guid.NewGuid call |> Async.RunSynchronously |> ignore) "wirft sofort"
                Expect.equal seen.Count 1 $"ein Versuch bei {error.Message}"
        }
        test "Beide Versuche einer Absicht tragen dieselbe Id, zwei Absichten zwei Ids aus der Quelle" {
            let ids = [ System.Guid.NewGuid(); System.Guid.NewGuid() ]
            let source = System.Collections.Generic.Queue<System.Guid>(ids)
            let newId () = source.Dequeue()
            let seen, call = failingWith (proxyError 0)
            for _ in 1..2 do
                try Retry.commandWith noSleep newId call |> Async.RunSynchronously |> ignore with _ -> ()
            Expect.equal (List.ofSeq seen) [ ids[0]; ids[0]; ids[1]; ids[1] ] "je Absicht eine Id, zweimal verwendet"
        }
    ]
