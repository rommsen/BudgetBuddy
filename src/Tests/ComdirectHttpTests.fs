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

let private withClient (bank: FakeBank) (call: HttpClient -> Async<'T>) =
    use client = new HttpClient(bank, disposeHandler = false)
    call client |> run

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
    ]
