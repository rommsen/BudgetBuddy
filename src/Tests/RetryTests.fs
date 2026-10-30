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

        test "die lesenden Start-Abfragen laufen über den Helfer, kein Schreibaufruf" {
            let settings = readClient "Components/Settings/State.fs"
            let ledger = readClient "Components/Ledger/State.fs"
            let app = readClient "State.fs"
            Expect.stringContains settings "Retry.reading Api.settings.getSettings" "getSettings"
            Expect.stringContains ledger "Retry.reading Api.ledger.getLedgerAccounts" "getLedgerAccounts"
            Expect.stringContains ledger "Retry.reading Api.ledger.getLastMirrorRun" "getLastMirrorRun"
            Expect.stringContains app "Retry.reading Api.ynab.getCategories" "getCategories im Kassenbuch-Zweig"
            let all = [ settings; ledger; app ] |> String.concat "\n"
            let wrapped =
                Regex.Matches(all, @"Retry\.reading\s+Api\.(\w+)\.(\w+)")
                |> Seq.map (fun m -> m.Groups[2].Value)
                |> List.ofSeq
            let expected =
                [ "getSettings"; "getLedgerAccounts"; "getLastMirrorRun"; "getCategories"
                  "getLenaFlagSettings"; "getLenaFlagSettings" ]
            Expect.equal (List.sort wrapped) (List.sort expected) "genau diese, kein Schreibaufruf"
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
