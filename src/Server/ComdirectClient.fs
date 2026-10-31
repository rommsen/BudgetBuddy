module Server.ComdirectClient

open System
open System.Net.Http
open System.Net.Http.Headers
open Thoth.Json.Net
open FsToolkit.ErrorHandling
open Shared.Domain

// ============================================
// Types
// ============================================

/// OAuth tokens from Comdirect API
type Tokens = {
    Access: string
    Refresh: string
    /// Ablaufzeit des Access-Tokens. None, wenn Comdirect beim Login kein `expires_in` schickt.
    ExpiresAt: DateTimeOffset option
}

/// Urteil über einen gescheiterten Refresh (ADR 0071 Punkt 3). `reason` trägt nur Status und
/// `error`-Code, nie den Antwortrumpf.
type RefreshFailure =
    /// 400, 401 oder ein anderes 4xx: das Refresh-Token ist verbraucht, es braucht eine neue TAN.
    | ReloginRequired of reason: string
    /// Status 0, 429, 5xx oder nicht lesbare Antwort: die Verbindung bleibt, später erneut versuchen.
    | RetryLater of reason: string

/// Push-TAN challenge from Comdirect
type Challenge = {
    Id: string
    Type: string  // "P_TAN_PUSH"
}

/// Request info for Comdirect API calls
type RequestInfo = {
    RequestId: string
    SessionId: string
}
with
    member this.Encode() =
        let requestInfo = {|
            clientRequestId = {|
                sessionId = this.SessionId
                requestId = this.RequestId
            |}
        |}
        Encode.Auto.toString(0, requestInfo)

/// API keys for Comdirect OAuth
type ApiKeys = {
    ClientId: string
    ClientSecret: string
}

/// Complete auth session state
type AuthSession = {
    RequestInfo: RequestInfo
    SessionId: string
    Tokens: Tokens
    SessionIdentifier: string
    Challenge: Challenge option
}

// ============================================
// Constants
// ============================================

let private endpoint = "https://api.comdirect.de/"

// ============================================
// JSON Decoders
// ============================================

/// `expires_in` ist relativ, die Uhr kommt deshalb als Parameter.
let private tokensDecoderAt (now: DateTimeOffset) : Decoder<Tokens> =
    Decode.object (fun get -> {
        Access = get.Required.Field "access_token" Decode.string
        Refresh = get.Required.Field "refresh_token" Decode.string
        ExpiresAt =
            get.Optional.Field "expires_in" Decode.int
            |> Option.map (fun seconds -> now.AddSeconds(float seconds))
    })

let private challengeDecoder: Decoder<Challenge> =
    Decode.object (fun get -> {
        Id = get.Required.Field "id" Decode.string
        Type = get.Required.Field "typ" Decode.string
    })

/// Removes Comdirect line number prefixes from remittance info.
/// Comdirect formats memo lines as "01TEXT", "02TEXT", etc.
let internal removeLineNumberPrefixes (text: string) : string =
    System.Text.RegularExpressions.Regex.Replace(
        text,
        @"(^|\n)\d{2}(?=[A-Za-zÄÖÜäöüß0-9])",
        "$1"
    ).Trim()

/// Detects whether a memo originates from a Comdirect Visa-Debitkarte/Kartenzahlung
/// transaction. These memos have no remitter/creditor in the API response, so the
/// merchant name must be extracted from the memo itself.
let internal isCardPaymentMemo (memo: string) : bool =
    memo.Contains("Kartenzahlung") || memo.Contains("Visa-Debitkarte")

/// Extracts the merchant name from a Comdirect card-payment memo.
/// Convention: merchant appears at the very beginning of the memo, followed by a
/// comma and metadata (phone, location, "Karte Nr.", etc.). Without this fallback
/// the YNAB upload sends payee_name="Unknown" which YNAB may convert to a transfer.
let internal extractCardMerchant (memo: string) : string option =
    if not (isCardPaymentMemo memo) then
        None
    else
        let firstComma = memo.IndexOf(',')
        let candidate =
            if firstComma >= 0 then memo.Substring(0, firstComma)
            else memo
        let trimmed = candidate.Trim()
        if String.IsNullOrWhiteSpace(trimmed) then None
        elif trimmed |> Seq.forall (fun c -> Char.IsDigit(c) || Char.IsWhiteSpace(c)) then None
        else Some trimmed

let private transactionDecoder: Decoder<BankTransaction> =
    Decode.object (fun get ->
        // Extract name (can be remitter or creditor)
        let payee =
            match get.Optional.At ["remitter"; "holderName"] Decode.string with
            | Some name -> Some name
            | None -> get.Optional.At ["creditor"; "holderName"] Decode.string

        // Parse booking date
        let bookingDate =
            get.Required.Field "bookingDate" Decode.string
            |> DateTime.Parse

        // Get amount
        let amountValue = get.Required.At ["amount"; "value"] Decode.decimal
        let currency = get.Optional.At ["amount"; "unit"] Decode.string |> Option.defaultValue "EUR"

        // Get reference and memo
        let reference = get.Required.Field "reference" Decode.string
        let memo =
            get.Required.Field "remittanceInfo" Decode.string
            |> removeLineNumberPrefixes

        // For Visa-Debitkarte transactions Comdirect omits remitter/creditor —
        // fall back to merchant name from the memo so YNAB doesn't see "Unknown".
        let effectivePayee =
            match payee with
            | Some _ -> payee
            | None -> extractCardMerchant memo

        // Create transaction ID from reference
        let transactionId = TransactionId reference

        {
            Id = transactionId
            BookingDate = bookingDate
            Amount = { Amount = amountValue; Currency = currency }
            Payee = effectivePayee
            Memo = memo
            Reference = reference
            RawData = ""  // TODO: Store raw JSON if needed for debugging
        }
    )

let private transactionsDecoder: Decoder<BankTransaction list> =
    Decode.field "values" (Decode.list transactionDecoder)

// Account decoder for Comdirect accounts endpoint
let private accountDecoder: Decoder<ComdirectAccount> =
    Decode.object (fun get ->
        let accountId = get.Required.Field "accountId" Decode.string
        let iban = get.Optional.Field "iban" Decode.string |> Option.defaultValue ""
        let accountDisplayId = get.Optional.Field "accountDisplayId" Decode.string |> Option.defaultValue accountId
        let accountType = get.Optional.At ["accountType"; "text"] Decode.string |> Option.defaultValue "UNKNOWN"
        let currency = get.Optional.At ["balance"; "unit"] Decode.string |> Option.defaultValue "EUR"
        let balanceAmount = get.Optional.At ["balance"; "value"] Decode.decimal

        {
            AccountId = accountId
            Iban = iban
            AccountDisplayId = accountDisplayId
            AccountType = accountType
            Currency = currency
            Balance = balanceAmount |> Option.map (fun amt -> { Amount = amt; Currency = currency })
        }
    )

let private accountsDecoder: Decoder<ComdirectAccount list> =
    Decode.field "values" (Decode.list accountDecoder)

// ============================================
// HTTP Helper Functions
// ============================================

let private createHttpClient() =
    let client = new HttpClient()
    client.DefaultRequestHeaders.Accept.Add(MediaTypeWithQualityHeaderValue("application/json"))
    client

/// Ein Fehlercode ist ein kurzes Wort aus Buchstaben und Unterstrichen (`invalid_grant`,
/// `TAN_UNGUELTIG`). Alles andere, etwa ein Wert mit Ziffern, könnte ein Token sein.
let internal isSafeErrorCode (text: string) : bool =
    System.Text.RegularExpressions.Regex.IsMatch(text, @"^[A-Za-z_]{1,40}$")

/// Zieht aus einem Fehlerrumpf nur den Code (OAuth `error`, Comdirect-API `code`). Der Rumpf
/// selbst verlässt diese Funktion nie: er kann Tokens und Kontodaten tragen (ADR 0071 Punkt 3).
let private errorCodeOf (content: string) : string option =
    let decoder = Decode.oneOf [ Decode.field "error" Decode.string; Decode.field "code" Decode.string ]
    match Decode.fromString decoder content with
    | Ok code when isSafeErrorCode code -> Some code
    | _ -> None

/// Status und, falls vorhanden, `error`-Code für Meldung und Log.
let private describeFailure (status: int) (content: string) : string =
    match errorCodeOf content with
    | Some code -> sprintf "HTTP %d: %s" status code
    | None -> sprintf "HTTP %d" status

/// Fester Text statt der Decoder-Meldung: Thoth zitiert darin den gelesenen Wert.
let private unreadableResponse (status: int) : string =
    sprintf "HTTP %d: Antwort nicht lesbar" status

/// Ausnahmen tragen nur ihren Typnamen weiter, ihr Text kann die Anfrage-URL enthalten.
let private transportError (ex: exn) : ComdirectError =
    ComdirectError.NetworkError (0, ex.GetType().Name)

let private httpError (status: int) (content: string) : ComdirectError =
    match status with
    | 401 -> ComdirectError.AuthenticationFailed (describeFailure status content)
    | 403 -> ComdirectError.SessionExpired
    | _ -> ComdirectError.NetworkError (status, errorCodeOf content |> Option.defaultValue "")

let private handleResponse<'T> (decoder: Decoder<'T>) (response: HttpResponseMessage) : Async<ComdirectResult<'T>> =
    async {
        let! content = response.Content.ReadAsStringAsync() |> Async.AwaitTask
        let status = int response.StatusCode

        if response.IsSuccessStatusCode then
            match Decode.fromString decoder content with
            | Ok value -> return Ok value
            | Error _ -> return Error (ComdirectError.InvalidResponse (unreadableResponse status))
        else
            return Error (httpError status content)
    }

/// Anfrage an den OAuth-Endpunkt. `FormUrlEncodedContent` kodiert jeden Wert, ein `&` oder `+`
/// im Passwort bleibt damit Teil des Werts.
let private tokenRequest (fields: (string * string) list) : HttpRequestMessage =
    let request = new HttpRequestMessage(HttpMethod.Post, endpoint + "oauth/token")
    request.Headers.Accept.Add(MediaTypeWithQualityHeaderValue("application/json"))
    request.Content <-
        new FormUrlEncodedContent(fields |> List.map System.Collections.Generic.KeyValuePair)
    request

/// GET mit Sitzungs-Headern je Anfrage: ein eingespeister Client bleibt unverändert und
/// lässt sich wiederverwenden.
let private sessionGet (requestInfo: RequestInfo) (tokens: Tokens) (url: string) : HttpRequestMessage =
    let request = new HttpRequestMessage(HttpMethod.Get, url)
    request.Headers.Accept.Add(MediaTypeWithQualityHeaderValue("application/json"))
    request.Headers.Authorization <- AuthenticationHeaderValue("Bearer", tokens.Access)
    request.Headers.Add("x-http-request-info", requestInfo.Encode())
    request

// ============================================
// OAuth Flow Functions
// ============================================

/// Step 1: Initialize OAuth with client credentials and user credentials
let initOAuthWith (client: HttpClient) (credentials: ComdirectSettings) (apiKeys: ApiKeys) : Async<ComdirectResult<Tokens>> =
    async {
        use request =
            tokenRequest [
                "client_id", apiKeys.ClientId
                "client_secret", apiKeys.ClientSecret
                "username", credentials.Username
                "password", credentials.Password
                "grant_type", "password"
            ]

        try
            let! response = client.SendAsync(request) |> Async.AwaitTask
            return! handleResponse (tokensDecoderAt DateTimeOffset.UtcNow) response
        with
        | ex -> return Error (transportError ex)
    }

let initOAuth (credentials: ComdirectSettings) (apiKeys: ApiKeys) : Async<ComdirectResult<Tokens>> =
    async {
        use client = createHttpClient()
        return! initOAuthWith client credentials apiKeys
    }

/// Step 2: Get session identifier
let getSessionIdentifierWith (client: HttpClient) (requestInfo: RequestInfo) (tokens: Tokens) : Async<ComdirectResult<string>> =
    async {
        // Decode the first item in the array and get the identifier field
        let identifierDecoder = Decode.index 0 (Decode.field "identifier" Decode.string)

        try
            use request = sessionGet requestInfo tokens (endpoint + "api/session/clients/user/v1/sessions")
            let! response = client.SendAsync(request) |> Async.AwaitTask
            return! handleResponse identifierDecoder response
        with
        | ex -> return Error (transportError ex)
    }

let getSessionIdentifier (requestInfo: RequestInfo) (tokens: Tokens) : Async<ComdirectResult<string>> =
    async {
        use client = createHttpClient()
        return! getSessionIdentifierWith client requestInfo tokens
    }

/// Erneuert die Tokens ohne TAN (ADR 0071 Punkt 3). Der Client kommt von außen, die Uhr ebenso.
/// Eine 200-Antwort ohne `expires_in` gilt als nicht lesbar: ohne Ablaufzeit lässt sich der
/// nächste Refresh nicht planen.
let refreshTokens (client: HttpClient) (apiKeys: ApiKeys) (now: DateTimeOffset) (tokens: Tokens) : Async<Result<Tokens, RefreshFailure>> =
    async {
        try
            use request =
                tokenRequest [
                    "client_id", apiKeys.ClientId
                    "client_secret", apiKeys.ClientSecret
                    "grant_type", "refresh_token"
                    "refresh_token", tokens.Refresh
                ]

            let! response = client.SendAsync(request) |> Async.AwaitTask
            let! content = response.Content.ReadAsStringAsync() |> Async.AwaitTask
            let status = int response.StatusCode

            if response.IsSuccessStatusCode then
                match Decode.fromString (tokensDecoderAt now) content with
                | Ok refreshed when refreshed.ExpiresAt.IsSome -> return Ok refreshed
                | _ -> return Error (RetryLater (unreadableResponse status))
            elif status >= 400 && status < 500 && status <> 429 then
                return Error (ReloginRequired (describeFailure status content))
            else
                return Error (RetryLater (describeFailure status content))
        with
        | ex -> return Error (RetryLater (sprintf "Status 0: %s" (ex.GetType().Name)))
    }

let refreshTokensLive (apiKeys: ApiKeys) (now: DateTimeOffset) (tokens: Tokens) : Async<Result<Tokens, RefreshFailure>> =
    async {
        use client = createHttpClient()
        return! refreshTokens client apiKeys now tokens
    }

/// Step 3: Request TAN challenge (Push-TAN)
let requestTanChallenge (requestInfo: RequestInfo) (tokens: Tokens) (sessionIdentifier: string) : Async<ComdirectResult<Challenge>> =
    async {
        use client = createHttpClient()

        client.DefaultRequestHeaders.Authorization <- AuthenticationHeaderValue("Bearer", tokens.Access)
        client.DefaultRequestHeaders.Add("x-http-request-info", requestInfo.Encode())

        let sessionPayload = {| identifier = sessionIdentifier; sessionTanActive = true; activated2FA = true |}
        let json = Encode.Auto.toString(0, sessionPayload)

        use content = new StringContent(json)
        content.Headers.ContentType <- MediaTypeHeaderValue("application/json")

        try
            let url = sprintf "%sapi/session/clients/user/v1/sessions/%s/validate" endpoint sessionIdentifier
            let! response = client.PostAsync(url, content) |> Async.AwaitTask

            if response.IsSuccessStatusCode then
                // Challenge info is in the x-once-authentication-info header
                let headerName = "x-once-authentication-info"

                if response.Headers.Contains(headerName) then
                    let headerValue = response.Headers.GetValues(headerName) |> Seq.head

                    match Decode.fromString challengeDecoder headerValue with
                    | Ok challenge ->
                        // Verify it's a Push-TAN challenge
                        if challenge.Type = "P_TAN_PUSH" then
                            return Ok challenge
                        else
                            return Error (ComdirectError.AuthenticationFailed "Only Push-TAN (P_TAN_PUSH) is supported")
                    | Error _ -> return Error (ComdirectError.InvalidResponse (unreadableResponse (int response.StatusCode)))
                else
                    return Error (ComdirectError.InvalidResponse (sprintf "Missing %s header" headerName))
            else
                let! content = response.Content.ReadAsStringAsync() |> Async.AwaitTask
                return Error (ComdirectError.NetworkError (int response.StatusCode, errorCodeOf content |> Option.defaultValue ""))
        with
        | ex -> return Error (transportError ex)
    }

/// Step 4: Activate session after TAN confirmation
let activateSession (requestInfo: RequestInfo) (tokens: Tokens) (sessionIdentifier: string) (challengeId: string) : Async<ComdirectResult<unit>> =
    async {
        use client = createHttpClient()

        client.DefaultRequestHeaders.Authorization <- AuthenticationHeaderValue("Bearer", tokens.Access)
        client.DefaultRequestHeaders.Add("x-http-request-info", requestInfo.Encode())

        // Add TAN challenge headers
        let authInfo = {| id = challengeId |}
        client.DefaultRequestHeaders.Add("x-once-authentication-info", Encode.Auto.toString(0, authInfo))
        client.DefaultRequestHeaders.Add("x-once-authentication", "000000")

        let sessionPayload = {| identifier = sessionIdentifier; sessionTanActive = true; activated2FA = true |}
        let json = Encode.Auto.toString(0, sessionPayload)

        use content = new StringContent(json)
        content.Headers.ContentType <- MediaTypeHeaderValue("application/json")

        try
            let url = sprintf "%sapi/session/clients/user/v1/sessions/%s" endpoint sessionIdentifier
            let request = new HttpRequestMessage(new HttpMethod("PATCH"), url)
            request.Content <- content

            // Copy headers manually for PATCH request
            request.Headers.Authorization <- AuthenticationHeaderValue("Bearer", tokens.Access)
            request.Headers.Add("x-http-request-info", requestInfo.Encode())
            request.Headers.Add("x-once-authentication-info", Encode.Auto.toString(0, authInfo))
            request.Headers.Add("x-once-authentication", "000000")

            let! response = client.SendAsync(request) |> Async.AwaitTask

            if response.IsSuccessStatusCode then
                return Ok ()
            else
                let! content = response.Content.ReadAsStringAsync() |> Async.AwaitTask

                match int response.StatusCode with
                | 403 -> return Error ComdirectError.TanRejected
                | 408 -> return Error ComdirectError.TanChallengeExpired
                | code -> return Error (ComdirectError.NetworkError (code, errorCodeOf content |> Option.defaultValue ""))
        with
        | ex -> return Error (transportError ex)
    }

/// Step 5: Get extended permissions (needed for transaction access)
let getExtendedTokensWith (client: HttpClient) (apiKeys: ApiKeys) (tokens: Tokens) : Async<ComdirectResult<Tokens>> =
    async {
        use request =
            tokenRequest [
                "client_id", apiKeys.ClientId
                "client_secret", apiKeys.ClientSecret
                "token", tokens.Access
                "grant_type", "cd_secondary"
            ]

        try
            let! response = client.SendAsync(request) |> Async.AwaitTask
            return! handleResponse (tokensDecoderAt DateTimeOffset.UtcNow) response
        with
        | ex -> return Error (transportError ex)
    }

let getExtendedTokens (tokens: Tokens) (apiKeys: ApiKeys) : Async<ComdirectResult<Tokens>> =
    async {
        use client = createHttpClient()
        return! getExtendedTokensWith client apiKeys tokens
    }

// ============================================
// Transaction Fetching
// ============================================

/// Fetch transactions for an account starting at a specific offset
let private getTransactionsPage (client: HttpClient) (requestInfo: RequestInfo) (tokens: Tokens) (accountId: string) (offset: int) : Async<ComdirectResult<BankTransaction list>> =
    async {
        try
            let url = sprintf "%sapi/banking/v1/accounts/%s/transactions?transactionState=BOOKED&paging-first=%d" endpoint accountId offset
            use request = sessionGet requestInfo tokens url
            let! response = client.SendAsync(request) |> Async.AwaitTask

            return! handleResponse transactionsDecoder response
        with
        | ex -> return Error (transportError ex)
    }

/// Obergrenze je Abruf. Die Schleife hält das Bank-Gate, ein Abruf ohne Ende sperrte jeden Sync.
let [<Literal>] MaxTransactionPages = 100

/// Fetch all transactions for the last N days, gezählt ab `today`. Eine vollständig im Zeitraum
/// liegende Seite ohne neue Reference, die kürzer ist als die erste Seite, ist das Datenende: Sie
/// fällt weg, das bisher Gesammelte ist das Ergebnis. Ist sie nicht kürzer, ist das ein Fehler, ebenso
/// das Erreichen der Obergrenze: Der Zeitraum wäre sonst still unvollständig. Beide Fehler zählen
/// nicht als Ablehnung, die Bankverbindung bleibt also unberührt.
let getTransactionsWith (client: HttpClient) (requestInfo: RequestInfo) (tokens: Tokens) (accountId: string) (days: int) (today: DateTime) : Async<ComdirectResult<BankTransaction list>> =
    let dateCutoff = today.AddDays(float -days)

    let rec fetchWithPaging pageNumber firstPageLength offset (accumulated: BankTransaction list) =
        asyncResult {
            let! transactions = getTransactionsPage client requestInfo tokens accountId offset

            // Take transactions that are within the date range
            let txInRange = transactions |> List.filter (fun tx -> tx.BookingDate >= dateCutoff)
            let firstPageLength = if pageNumber = 1 then List.length transactions else firstPageLength

            // If we got a full page and all transactions are within range, fetch more
            if not (List.isEmpty transactions) && List.length transactions = List.length txInRange then
                let known = accumulated |> List.map (fun tx -> tx.Reference) |> Set.ofList
                if txInRange |> List.forall (fun tx -> known.Contains tx.Reference) then
                    if List.length transactions < firstPageLength then
                        return accumulated
                    else
                        return! Error (ComdirectError.InvalidResponse "Die Bank liefert dieselbe Umsatzseite erneut, der Abruf wäre unvollständig.")
                elif pageNumber >= MaxTransactionPages then
                    return! Error (ComdirectError.InvalidResponse "Der Zeitraum umfasst zu viele Umsatzseiten, der Abruf wäre unvollständig.")
                else
                    return! fetchWithPaging (pageNumber + 1) firstPageLength (offset + List.length transactions) (accumulated @ txInRange)
            else
                // We've reached transactions outside our range, stop
                return accumulated @ txInRange
        }

    fetchWithPaging 1 0 0 []

let getTransactions (requestInfo: RequestInfo) (tokens: Tokens) (accountId: string) (days: int) (today: DateTime) : Async<ComdirectResult<BankTransaction list>> =
    async {
        use client = createHttpClient()
        return! getTransactionsWith client requestInfo tokens accountId days today
    }

// ============================================
// Account Fetching
// ============================================

/// Fetch all available accounts (requires extended tokens after TAN)
let getAccountsWith (client: HttpClient) (requestInfo: RequestInfo) (tokens: Tokens) : Async<ComdirectResult<ComdirectAccount list>> =
    async {
        // Try multiple endpoint variants
        let endpoints = [
            "api/banking/v1/accounts"
            "api/banking/clients/user/v1/accounts"
            "banking/v1/accounts"
        ]

        let rec tryEndpoints (eps: string list) =
            async {
                match eps with
                | [] -> return Error (ComdirectError.NetworkError (404, "No valid accounts endpoint found"))
                | ep :: rest ->
                    try
                        use request = sessionGet requestInfo tokens (endpoint + ep)
                        let! response = client.SendAsync(request) |> Async.AwaitTask

                        match int response.StatusCode with
                        // Anmeldefehler gelten für jeden Pfad, ein weiterer Versuch ändert nichts.
                        | 401 | 403 -> return! handleResponse accountsDecoder response
                        | _ when response.IsSuccessStatusCode -> return! handleResponse accountsDecoder response
                        | _ -> return! tryEndpoints rest
                    with
                    | _ -> return! tryEndpoints rest
            }

        return! tryEndpoints endpoints
    }

let getAccounts (requestInfo: RequestInfo) (tokens: Tokens) : Async<ComdirectResult<ComdirectAccount list>> =
    async {
        use client = createHttpClient()
        return! getAccountsWith client requestInfo tokens
    }

// ============================================
// High-Level Auth Flow
// ============================================

/// Complete OAuth flow up to TAN challenge
let startAuthFlow (credentials: ComdirectSettings) (apiKeys: ApiKeys) : Async<ComdirectResult<AuthSession>> =
    asyncResult {
        // Create request info with timestamp-based request ID
        let requestInfo = {
            RequestId = DateTimeOffset.Now.ToUnixTimeSeconds().ToString().Substring(0, 9)
            SessionId = Guid.NewGuid().ToString()
        }

        // Step 1: Get initial tokens
        let! tokens = initOAuth credentials apiKeys

        // Step 2: Get session identifier
        let! sessionIdentifier = getSessionIdentifier requestInfo tokens

        // Step 3: Request TAN challenge
        let! challenge = requestTanChallenge requestInfo tokens sessionIdentifier

        return {
            RequestInfo = requestInfo
            SessionId = requestInfo.SessionId
            Tokens = tokens
            SessionIdentifier = sessionIdentifier
            Challenge = Some challenge
        }
    }

/// Complete auth flow after TAN confirmation
let completeAuthFlow (session: AuthSession) (apiKeys: ApiKeys) : Async<ComdirectResult<Tokens>> =
    asyncResult {
        match session.Challenge with
        | None -> return! Error (ComdirectError.AuthenticationFailed "No challenge found in session")
        | Some challenge ->
            // Step 4: Activate session
            do! activateSession session.RequestInfo session.Tokens session.SessionIdentifier challenge.Id

            // Step 5: Get extended permissions
            return! getExtendedTokens session.Tokens apiKeys
    }
