module Tests.ComdirectHttpTests

// Comdirect-Client gegen einen Fake-`HttpMessageHandler` (banking-import-cx4b7): Refresh-Urteil,
// Formular-Kodierung und Fehlertexte ohne Antwortrumpf. Kein echter Aufruf, alle Werte künstlich.

open System
open System.Net
open System.Net.Http
open Expecto
open Server.ComdirectClient
open Server.Api
open Shared.Domain

type private FakeBank(respond: unit -> HttpResponseMessage) =
    inherit HttpMessageHandler()
    member val Calls = ResizeArray<string * string>()
    override this.SendAsync(request: HttpRequestMessage, _ct: Threading.CancellationToken) =
        task {
            let! body =
                if isNull request.Content then Threading.Tasks.Task.FromResult ""
                else request.Content.ReadAsStringAsync()
            this.Calls.Add((request.RequestUri.AbsolutePath, body))
            return respond ()
        }

let private bankAnswering (status: int) (body: string) =
    new FakeBank(fun () ->
        new HttpResponseMessage(enum<HttpStatusCode> status, Content = new StringContent(body)))

let private bankFailingWith (ex: exn) = new FakeBank(fun () -> raise ex)

let private run (work: Async<'T>) = Async.RunSynchronously work

let private now = DateTimeOffset(2026, 10, 30, 12, 0, 0, TimeSpan.Zero)
let private keys = { ClientId = "kuenstliche-id"; ClientSecret = "kuenstliches-secret" }
let private oldTokens = { Access = "alt-access"; Refresh = "alt-refresh"; ExpiresAt = None }
let private requestInfo = { RequestId = "123456789"; SessionId = "sitzung-kuenstlich" }

/// Erkennbar künstlicher Text, der in keiner Meldung und keinem Log landen darf.
let private tokenText = "GEHEIMES-TOKEN-KUENSTLICH-4711"
let private bodyWithToken =
    $$"""{"error":"invalid_grant","error_description":"token {{tokenText}}","access_token":"{{tokenText}}"}"""

let private credentials (password: string) : ComdirectSettings =
    { ClientId = keys.ClientId; ClientSecret = keys.ClientSecret
      Username = "kunde"; Password = password; AccountId = None }

let private refreshWith (bank: FakeBank) =
    use client = new HttpClient(bank, disposeHandler = false)
    refreshTokens client keys now oldTokens |> run

let private pagingToday = DateTime(2026, 1, 14)

/// Eine Umsatzseite aus (Buchungsdatum, Reference).
let private pageOf (bookings: (string * string) list) : string =
    let booking (date: string, reference: string) =
        $"""{{"bookingDate":"{date}","amount":{{"value":"-1.00","unit":"EUR"}},"reference":"{reference}","remittanceInfo":"01Text"}}"""
    let values = bookings |> List.map booking |> String.concat ","
    $"""{{"values":[{values}]}}"""

let private withClient (bank: FakeBank) (call: HttpClient -> Async<'T>) =
    use client = new HttpClient(bank, disposeHandler = false)
    call client |> run

/// Eine Bank, die die Seiten der Reihe nach ausliefert.
let private bankWithPages (pages: string list) =
    let remaining = Collections.Generic.Queue pages
    new FakeBank(fun () ->
        new HttpResponseMessage(HttpStatusCode.OK, Content = new StringContent(remaining.Dequeue ())))

let private fetchPaged (bank: FakeBank) =
    withClient bank (fun c -> getTransactionsWith c requestInfo oldTokens "konto" 7 pagingToday)

let private expectReferences (bank: FakeBank) (expected: string list) =
    match fetchPaged bank with
    | Ok transactions -> Expect.equal (transactions |> List.map (fun t -> t.Reference)) expected "Buchungen im Zeitraum"
    | other -> failtestf "Buchungen erwartet: %A" other

let private expectInvalidResponse (bank: FakeBank) =
    match fetchPaged bank with
    | Error (ComdirectError.InvalidResponse _) -> ()
    | other -> failtestf "InvalidResponse erwartet: %A" other

[<Tests>]
let refreshTests =
    testList "Comdirect-Client gegen Fake-Bank: Refresh" [
        testCase "200 liefert neues Access-Token, neues Refresh-Token und die Ablaufzeit" <| fun () ->
            use bank = bankAnswering 200 """{"access_token":"neu-access","refresh_token":"neu-refresh","expires_in":599}"""
            match refreshWith bank with
            | Ok tokens ->
                Expect.equal tokens.Access "neu-access" "Access-Token"
                Expect.equal tokens.Refresh "neu-refresh" "Refresh-Token"
                Expect.equal tokens.ExpiresAt (Some (now.AddSeconds 599.)) "Ablaufzeit aus der Uhr und expires_in"
            | other -> failtestf "Ok erwartet: %A" other
            let path, form = Seq.exactlyOne bank.Calls
            Expect.equal path "/oauth/token" "Token-Endpunkt"
            Expect.stringContains form "grant_type=refresh_token" "Grant"
            Expect.stringContains form "refresh_token=alt-refresh" "das bisherige Refresh-Token geht hinaus"

        for status in [ 400; 401; 404 ] do
            testCase $"Status {status} heißt neu anmelden" <| fun () ->
                use bank = bankAnswering status bodyWithToken
                match refreshWith bank with
                | Error (ReloginRequired reason) ->
                    Expect.stringContains reason (string status) "Status in der Meldung"
                    Expect.stringContains reason "invalid_grant" "error-Code in der Meldung"
                | other -> failtestf "ReloginRequired erwartet: %A" other

        for status in [ 429; 500; 503 ] do
            testCase $"Status {status} heißt später nochmal" <| fun () ->
                use bank = bankAnswering status "<html>Wartung</html>"
                match refreshWith bank with
                | Error (RetryLater reason) -> Expect.stringContains reason (string status) "Status in der Meldung"
                | other -> failtestf "RetryLater erwartet: %A" other

        testCase "Abbruch der Verbindung heißt später nochmal" <| fun () ->
            use bank = bankFailingWith (HttpRequestException "keine Verbindung")
            match refreshWith bank with
            | Error (RetryLater _) -> ()
            | other -> failtestf "RetryLater erwartet: %A" other

        testCase "Zeitüberschreitung heißt später nochmal" <| fun () ->
            use bank = bankFailingWith (Threading.Tasks.TaskCanceledException "Zeit abgelaufen")
            match refreshWith bank with
            | Error (RetryLater _) -> ()
            | other -> failtestf "RetryLater erwartet: %A" other

        for body in [ "kein JSON"; """{"access_token":"nur-access"}"""; """{"access_token":"a","refresh_token":"r"}""" ] do
            testCase $"200 mit nicht verwertbarem Rumpf heißt später nochmal: {body}" <| fun () ->
                use bank = bankAnswering 200 body
                match refreshWith bank with
                | Error (RetryLater _) -> ()
                | other -> failtestf "RetryLater erwartet: %A" other

        testCase "kein Urteil enthält den Antwortrumpf" <| fun () ->
            for status in [ 200; 400; 503 ] do
                use bank = bankAnswering status bodyWithToken
                let text = sprintf "%A" (refreshWith bank |> Result.map ignore)
                Expect.isFalse (text.Contains tokenText) $"kein Token-Text bei Status {status}"
    ]

[<Tests>]
let formTests =
    testList "Comdirect-Client gegen Fake-Bank: Token-Formular" [
        // Bug: das Formular entstand per sprintf, `&` oder `+` im Passwort zerlegte es.
        testCase "Passwort mit & und + wird korrekt kodiert" <| fun () ->
            use bank = bankAnswering 200 """{"access_token":"a","refresh_token":"r"}"""
            withClient bank (fun client -> initOAuthWith client (credentials "ab&grant_type=x+1") keys) |> ignore
            let _, form = Seq.exactlyOne bank.Calls
            Expect.stringContains form "password=ab%26grant_type%3Dx%2B1" "Passwort bleibt ein Wert"
            Expect.stringContains form "&grant_type=password" "Grant bleibt unverändert"

        testCase "Secret mit & und + wird beim Zweitfaktor-Token korrekt kodiert" <| fun () ->
            use bank = bankAnswering 200 """{"access_token":"a","refresh_token":"r"}"""
            let trickyKeys = { keys with ClientSecret = "s&e+c" }
            withClient bank (fun client -> getExtendedTokensWith client trickyKeys oldTokens) |> ignore
            let _, form = Seq.exactlyOne bank.Calls
            Expect.stringContains form "client_secret=s%26e%2Bc" "Secret bleibt ein Wert"

        testCase "alphanumerische Werte bleiben im Formular unverändert" <| fun () ->
            use bank = bankAnswering 200 """{"access_token":"a","refresh_token":"r"}"""
            withClient bank (fun client -> initOAuthWith client (credentials "abc123") keys) |> ignore
            let _, form = Seq.exactlyOne bank.Calls
            Expect.equal
                form
                "client_id=kuenstliche-id&client_secret=kuenstliches-secret&username=kunde&password=abc123&grant_type=password"
                "dieselbe Anfrage wie bisher"
    ]

[<Tests>]
let statusTests =
    testList "Comdirect-Client gegen Fake-Bank: Status beim Lesen" [
        // Bug: getAccounts probierte bei 401 und 403 alle Pfade durch und meldete am Ende 404.
        testCase "Konten: 401 ist eine gescheiterte Anmeldung, kein 404" <| fun () ->
            use bank = bankAnswering 401 bodyWithToken
            match withClient bank (fun c -> getAccountsWith c requestInfo oldTokens) with
            | Error (ComdirectError.AuthenticationFailed _) -> ()
            | other -> failtestf "AuthenticationFailed erwartet: %A" other
            Expect.equal bank.Calls.Count 1 "kein weiterer Pfad nach 401"

        testCase "Konten: 403 ist eine abgelaufene Sitzung, kein 404" <| fun () ->
            use bank = bankAnswering 403 bodyWithToken
            match withClient bank (fun c -> getAccountsWith c requestInfo oldTokens) with
            | Error ComdirectError.SessionExpired -> ()
            | other -> failtestf "SessionExpired erwartet: %A" other

        testCase "Konten: 404 probiert weiter den nächsten Pfad" <| fun () ->
            use bank = bankAnswering 404 ""
            match withClient bank (fun c -> getAccountsWith c requestInfo oldTokens) with
            | Error (ComdirectError.NetworkError (404, _)) -> ()
            | other -> failtestf "404 erwartet: %A" other
            Expect.equal bank.Calls.Count 3 "alle drei Pfade probiert"

        // Bug: getSessionIdentifier meldete 401 und 403 als NetworkError.
        testCase "Sitzungskennung: 401 ist eine gescheiterte Anmeldung" <| fun () ->
            use bank = bankAnswering 401 bodyWithToken
            match withClient bank (fun c -> getSessionIdentifierWith c requestInfo oldTokens) with
            | Error (ComdirectError.AuthenticationFailed _) -> ()
            | other -> failtestf "AuthenticationFailed erwartet: %A" other

        testCase "Sitzungskennung: 403 ist eine abgelaufene Sitzung" <| fun () ->
            use bank = bankAnswering 403 bodyWithToken
            match withClient bank (fun c -> getSessionIdentifierWith c requestInfo oldTokens) with
            | Error ComdirectError.SessionExpired -> ()
            | other -> failtestf "SessionExpired erwartet: %A" other
    ]

[<Tests>]
let errorTextTests =
    testList "Comdirect-Client gegen Fake-Bank: Fehlertexte ohne Antwortrumpf" [
        // Sequentiell: `Console.SetOut` gilt prozessweit.
        testSequenced <| testList "Rumpf" [
            for status in [ 200; 400; 401; 403; 500 ] do
                testCase $"Status {status}: weder Fehlerwert noch Meldung noch Konsole enthalten den Rumpf" <| fun () ->
                    let console = new IO.StringWriter()
                    let original = Console.Out
                    // Der Rumpf kommt hier als gültiges JSON ohne Pflichtfelder: so zitiert der
                    // Decoder bei 200 den Wert in seiner Fehlermeldung.
                    use bank = bankAnswering status bodyWithToken
                    Console.SetOut console
                    let results =
                        try
                            [ sprintf "%A" (withClient bank (fun c -> getAccountsWith c requestInfo oldTokens))
                              sprintf "%A" (withClient bank (fun c -> getSessionIdentifierWith c requestInfo oldTokens))
                              sprintf "%A" (withClient bank (fun c -> initOAuthWith c (credentials "abc") keys)) ]
                        finally
                            Console.SetOut original
                    for result in results do
                        Expect.isFalse (result.Contains tokenText) "kein Token-Text im Fehlerwert"
                    Expect.isFalse (console.ToString().Contains tokenText) "kein Token-Text auf der Konsole"
        ]

        testCase "die Meldung nennt Status und error-Code" <| fun () ->
            use bank = bankAnswering 500 bodyWithToken
            match withClient bank (fun c -> getSessionIdentifierWith c requestInfo oldTokens) with
            | Error err ->
                let text = comdirectErrorToString err
                Expect.stringContains text "500" "Status"
                Expect.stringContains text "invalid_grant" "error-Code"
            | other -> failtestf "Fehler erwartet: %A" other

        testCase "ein error-Wert, der wie ein Token aussieht, wird nicht als Code übernommen" <| fun () ->
            use bank = bankAnswering 500 $$"""{"error":"{{tokenText}}"}"""
            match withClient bank (fun c -> getSessionIdentifierWith c requestInfo oldTokens) with
            | Error err -> Expect.isFalse ((comdirectErrorToString err).Contains tokenText) "kein Token-Text"
            | other -> failtestf "Fehler erwartet: %A" other

        testCase "comdirectErrorToString gibt auch einen fremden Text im NetworkError nicht aus" <| fun () ->
            let text = comdirectErrorToString (ComdirectError.NetworkError (500, bodyWithToken))
            Expect.isFalse (text.Contains tokenText) "kein Token-Text"
            Expect.stringContains text "500" "Status bleibt"

        // Bewahrt den früheren Fix: Comdirect-Fehler erschienen als rohes JSON in der Oberfläche.
        testCase "TAN noch nicht freigegeben bleibt eine verständliche Meldung" <| fun () ->
            use bank = bankAnswering 422 """{"code":"TAN_UNGUELTIG","messages":[{"message":"Text der Bank"}]}"""
            match withClient bank (fun c -> getSessionIdentifierWith c requestInfo oldTokens) with
            | Error err ->
                Expect.equal (comdirectErrorToString err) "Please confirm the TAN in your banking app first." "feste Meldung zum Code"
            | other -> failtestf "Fehler erwartet: %A" other

        // ADR 0071 Punkt 5 und banking-import-cyg94: feste Werte. Wer Decoder oder `generateImportId`
        // ändert, importiert bereits gebuchte Umsätze doppelt nach YNAB.
        testCase "TransactionId und import_id bleiben für eine feste Reference exakt gleich" <| fun () ->
            let page =
                """{"values":[{"bookingDate":"2026-01-10","amount":{"value":"-48.12","unit":"EUR"},"reference":"3F2A-91C0-XK7Z","remittanceInfo":"01Text"},{"bookingDate":"2025-12-01","amount":{"value":"-1.00","unit":"EUR"},"reference":"zu-alt","remittanceInfo":"01Text"}]}"""
            use bank = bankAnswering 200 page
            match withClient bank (fun c -> getTransactionsWith c requestInfo oldTokens "konto" 7 (DateTime(2026, 1, 14))) with
            | Ok [ transaction ] ->
                Expect.equal transaction.Id (TransactionId "3F2A-91C0-XK7Z") "TransactionId ist die Reference, unverändert"
                Expect.equal transaction.Reference "3F2A-91C0-XK7Z" "Reference unverändert"
                Expect.equal (generateImportId transaction.Id) "BB:3F2A91C0XK7Z" "Präfix BB, Bindestriche entfernt"
            | other -> failtestf "eine Buchung erwartet: %A" other

        testCase "der Abruf zählt die Tage ab dem übergebenen Datum" <| fun () ->
            let booking (date: string) (reference: string) =
                $"""{{"bookingDate":"{date}","amount":{{"value":"-1.00","unit":"EUR"}},"reference":"{reference}","remittanceInfo":"01Text"}}"""
            let page = $"""{{"values":[{booking "2026-01-10" "innen"},{booking "2026-01-07" "grenze"},{booking "2026-01-06" "aussen"}]}}"""
            use bank = bankAnswering 200 page
            let today = DateTime(2026, 1, 14)
            match withClient bank (fun c -> getTransactionsWith c requestInfo oldTokens "konto" 7 today) with
            | Ok transactions ->
                Expect.equal (transactions |> List.map (fun t -> t.Reference)) [ "innen"; "grenze" ] "nur Buchungen im Zeitraum"
                Expect.equal bank.Calls.Count 1 "eine Seite, ältere Buchungen beenden das Blättern"
            | other -> failtestf "Buchungen erwartet: %A" other

        // Bug: liefert die Bank immer dieselbe Seite im Zeitraum, blätterte die Schleife ohne Ende und
        // hielt das Bank-Gate, bis das Token ablief und ein falscher Verlust samt Challenge entstand.
        testCase "Kaputt: die Bank liefert immer dieselbe Seite: der Abruf endet mit einem Fehler ohne Ablehnung" <| fun () ->
            use bank = bankAnswering 200 (pageOf [ "2026-01-10", "gleich-1"; "2026-01-09", "gleich-2" ])
            match withClient bank (fun c -> getTransactionsWith c requestInfo oldTokens "konto" 7 pagingToday) with
            | Error (ComdirectError.InvalidResponse _) -> ()
            | other -> failtestf "InvalidResponse erwartet: %A" other
            Expect.equal bank.Calls.Count 2 "die zweite gleiche Seite beendet den Abruf"

        testCase "Kaputt: die Bank liefert ohne Ende neue Seiten: der Abruf endet an der Obergrenze" <| fun () ->
            let served = ref 0
            use bank =
                new FakeBank(fun () ->
                    served.Value <- served.Value + 1
                    let body = pageOf [ "2026-01-10", $"neu-{served.Value}" ]
                    new HttpResponseMessage(HttpStatusCode.OK, Content = new StringContent(body)))
            match withClient bank (fun c -> getTransactionsWith c requestInfo oldTokens "konto" 7 pagingToday) with
            | Error (ComdirectError.InvalidResponse _) -> ()
            | other -> failtestf "InvalidResponse erwartet: %A" other
            Expect.equal bank.Calls.Count MaxTransactionPages "Obergrenze"

        testCase "Intakt: mehrere echte Seiten kommen vollständig an" <| fun () ->
            let pages =
                Collections.Generic.Queue [
                    pageOf [ "2026-01-13", "a"; "2026-01-12", "b" ]
                    pageOf [ "2026-01-11", "c"; "2026-01-10", "d" ]
                    pageOf [ "2026-01-09", "e"; "2025-12-01", "zu-alt" ] ]
            use bank =
                new FakeBank(fun () ->
                    new HttpResponseMessage(HttpStatusCode.OK, Content = new StringContent(pages.Dequeue ())))
            match withClient bank (fun c -> getTransactionsWith c requestInfo oldTokens "konto" 7 pagingToday) with
            | Ok transactions ->
                Expect.equal (transactions |> List.map (fun t -> t.Reference)) [ "a"; "b"; "c"; "d"; "e" ] "alle Buchungen im Zeitraum"
            | other -> failtestf "Buchungen erwartet: %A" other

        // Datenende: Das träfe jedes Konto, dessen Daten im Zeitraum enden, bei jedem Sync.
        testCase "Intakt: eine leere Folgeseite beendet den Abruf mit allen Buchungen" <| fun () ->
            use bank = bankWithPages [ pageOf [ "2026-01-13", "a"; "2026-01-12", "b" ]; """{"values":[]}""" ]
            expectReferences bank [ "a"; "b" ]
            Expect.equal bank.Calls.Count 2 "die leere Seite ist die letzte"

        testCase "Intakt: ein Konto ohne Buchungen liefert ein leeres Ergebnis" <| fun () ->
            use bank = bankWithPages [ """{"values":[]}""" ]
            expectReferences bank []
            Expect.equal bank.Calls.Count 1 "eine leere erste Seite beendet das Blättern"

        // Fehlalarm: Eine neue Buchung verschiebt zwischen zwei Abrufen die letzte Buchung auf die
        // nächste Seite, die Bank liefert dort nur noch Bekanntes.
        testCase "Intakt: eine kürzere Seite ohne neue Reference ist das Datenende, ohne Dopplung" <| fun () ->
            use bank = bankWithPages [ pageOf [ "2026-01-13", "a"; "2026-01-12", "b" ]; pageOf [ "2026-01-12", "b" ] ]
            expectReferences bank [ "a"; "b" ]
            Expect.equal bank.Calls.Count 2 "die kürzere Seite ist die letzte"

        testCase "Intakt: das Datenende misst die Länge an der ersten Seite, nicht an der vorigen" <| fun () ->
            let shortPage = pageOf [ "2026-01-11", "d"; "2026-01-10", "e" ]
            use bank =
                bankWithPages [
                    pageOf [ "2026-01-13", "a"; "2026-01-12", "b"; "2026-01-12", "c" ]
                    shortPage
                    shortPage ]
            expectReferences bank [ "a"; "b"; "c"; "d"; "e" ]

        testCase "Intakt: eine kürzere Seite mit neuer Reference ist kein Datenende, die Buchung kommt an" <| fun () ->
            use bank =
                bankWithPages [
                    pageOf [ "2026-01-13", "a"; "2026-01-12", "b"; "2026-01-12", "c" ]
                    pageOf [ "2026-01-12", "c"; "2026-01-11", "d" ]
                    """{"values":[]}""" ]
            match fetchPaged bank with
            | Ok transactions ->
                Expect.contains (transactions |> List.map (fun t -> t.Reference)) "d" "die neue Buchung fehlt nicht"
            | other -> failtestf "Buchungen erwartet: %A" other
            Expect.equal bank.Calls.Count 3 "es wird weitergeblättert"

        testCase "Intakt: eine doppelte Reference innerhalb einer Seite bleibt am Datenende erhalten" <| fun () ->
            use bank =
                bankWithPages [
                    pageOf [ "2026-01-13", "a"; "2026-01-13", "a"; "2026-01-12", "b" ]
                    pageOf [ "2026-01-12", "b" ] ]
            expectReferences bank [ "a"; "a"; "b" ]

        testCase "Kaputt: eine gleich lange Seite ohne neue Reference bleibt ein Fehler" <| fun () ->
            use bank =
                bankWithPages [
                    pageOf [ "2026-01-13", "a"; "2026-01-12", "b" ]
                    pageOf [ "2026-01-12", "b"; "2026-01-13", "a" ] ]
            expectInvalidResponse bank

        testCase "Kaputt: eine längere Seite ohne neue Reference bleibt ein Fehler" <| fun () ->
            use bank =
                bankWithPages [
                    pageOf [ "2026-01-13", "a"; "2026-01-12", "b" ]
                    pageOf [ "2026-01-13", "a"; "2026-01-12", "b"; "2026-01-12", "b" ] ]
            expectInvalidResponse bank
    ]
