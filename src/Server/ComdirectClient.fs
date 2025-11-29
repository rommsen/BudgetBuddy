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
}

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

let private tokensDecoder: Decoder<Tokens> =
    Decode.object (fun get -> {
        Access = get.Required.Field "access_token" Decode.string
        Refresh = get.Required.Field "refresh_token" Decode.string
    })

let private challengeDecoder: Decoder<Challenge> =
    Decode.object (fun get -> {
        Id = get.Required.Field "id" Decode.string
        Type = get.Required.Field "typ" Decode.string
    })

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
        let memo = get.Required.Field "remittanceInfo" Decode.string

        // Create transaction ID from reference
        let transactionId = TransactionId reference

        {
            Id = transactionId
            BookingDate = bookingDate
            Amount = { Amount = amountValue; Currency = currency }
            Payee = payee
            Memo = memo
            Reference = reference
            RawData = ""  // TODO: Store raw JSON if needed for debugging
        }
    )

let private transactionsDecoder: Decoder<BankTransaction list> =
    Decode.field "values" (Decode.list transactionDecoder)

// ============================================
// HTTP Helper Functions
// ============================================

let private createHttpClient() =
    let client = new HttpClient()
    client.DefaultRequestHeaders.Accept.Add(MediaTypeWithQualityHeaderValue("application/json"))
    client

let private handleResponse<'T> (decoder: Decoder<'T>) (response: HttpResponseMessage) : Async<ComdirectResult<'T>> =
    async {
        let! content = response.Content.ReadAsStringAsync() |> Async.AwaitTask

        if response.IsSuccessStatusCode then
            match Decode.fromString decoder content with
            | Ok value -> return Ok value
            | Error err -> return Error (ComdirectError.InvalidResponse err)
        else
            let statusCode = int response.StatusCode

            // Handle specific error codes
            match statusCode with
            | 401 -> return Error (ComdirectError.AuthenticationFailed content)
            | 403 -> return Error (ComdirectError.SessionExpired)
            | _ -> return Error (ComdirectError.NetworkError (statusCode, content))
    }

// ============================================
// OAuth Flow Functions
// ============================================

/// Step 1: Initialize OAuth with client credentials and user credentials
let initOAuth (credentials: ComdirectSettings) (apiKeys: ApiKeys) : Async<ComdirectResult<Tokens>> =
    async {
        use client = createHttpClient()

        let body =
            sprintf "client_id=%s&client_secret=%s&username=%s&password=%s&grant_type=password"
                apiKeys.ClientId
                apiKeys.ClientSecret
                credentials.Username
                credentials.Password

        use content = new StringContent(body)
        content.Headers.ContentType <- MediaTypeHeaderValue("application/x-www-form-urlencoded")

        try
            let! response = client.PostAsync(endpoint + "oauth/token", content) |> Async.AwaitTask
            return! handleResponse tokensDecoder response
        with
        | ex -> return Error (ComdirectError.NetworkError (0, ex.Message))
    }

/// Step 2: Get session identifier
let getSessionIdentifier (requestInfo: RequestInfo) (tokens: Tokens) : Async<ComdirectResult<string>> =
    async {
        use client = createHttpClient()

        client.DefaultRequestHeaders.Authorization <- AuthenticationHeaderValue("Bearer", tokens.Access)
        client.DefaultRequestHeaders.Add("x-http-request-info", requestInfo.Encode())

        try
            let! response = client.GetAsync(endpoint + "api/session/clients/user/v1/sessions") |> Async.AwaitTask

            if response.IsSuccessStatusCode then
                let! content = response.Content.ReadAsStringAsync() |> Async.AwaitTask

                // Decode the first item in the array and get the identifier field
                let identifierDecoder = Decode.index 0 (Decode.field "identifier" Decode.string)

                match Decode.fromString identifierDecoder content with
                | Ok identifier -> return Ok identifier
                | Error err -> return Error (ComdirectError.InvalidResponse err)
            else
                let! content = response.Content.ReadAsStringAsync() |> Async.AwaitTask
                return Error (ComdirectError.NetworkError (int response.StatusCode, content))
        with
        | ex -> return Error (ComdirectError.NetworkError (0, ex.Message))
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
                    | Error err -> return Error (ComdirectError.InvalidResponse err)
                else
                    return Error (ComdirectError.InvalidResponse (sprintf "Missing %s header" headerName))
            else
                let! content = response.Content.ReadAsStringAsync() |> Async.AwaitTask
                return Error (ComdirectError.NetworkError (int response.StatusCode, content))
        with
        | ex -> return Error (ComdirectError.NetworkError (0, ex.Message))
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
                | code -> return Error (ComdirectError.NetworkError (code, content))
        with
        | ex -> return Error (ComdirectError.NetworkError (0, ex.Message))
    }

/// Step 5: Get extended permissions (needed for transaction access)
let getExtendedTokens (tokens: Tokens) (apiKeys: ApiKeys) : Async<ComdirectResult<Tokens>> =
    async {
        use client = createHttpClient()

        let body =
            sprintf "client_id=%s&client_secret=%s&token=%s&grant_type=cd_secondary"
                apiKeys.ClientId
                apiKeys.ClientSecret
                tokens.Access

        use content = new StringContent(body)
        content.Headers.ContentType <- MediaTypeHeaderValue("application/x-www-form-urlencoded")

        try
            let! response = client.PostAsync(endpoint + "oauth/token", content) |> Async.AwaitTask
            return! handleResponse tokensDecoder response
        with
        | ex -> return Error (ComdirectError.NetworkError (0, ex.Message))
    }

// ============================================
// Transaction Fetching
// ============================================

/// Fetch transactions for an account starting at a specific offset
let private getTransactionsPage (requestInfo: RequestInfo) (tokens: Tokens) (accountId: string) (offset: int) : Async<ComdirectResult<BankTransaction list>> =
    async {
        use client = createHttpClient()

        client.DefaultRequestHeaders.Authorization <- AuthenticationHeaderValue("Bearer", tokens.Access)
        client.DefaultRequestHeaders.Add("x-http-request-info", requestInfo.Encode())

        try
            let url = sprintf "%sapi/banking/v1/accounts/%s/transactions?transactionState=BOOKED&paging-first=%d" endpoint accountId offset
            let! response = client.GetAsync(url) |> Async.AwaitTask

            return! handleResponse transactionsDecoder response
        with
        | ex -> return Error (ComdirectError.NetworkError (0, ex.Message))
    }

/// Fetch all transactions for the last N days
let getTransactions (requestInfo: RequestInfo) (tokens: Tokens) (accountId: string) (days: int) : Async<ComdirectResult<BankTransaction list>> =
    let dateCutoff = DateTime.Today.AddDays(float -days)

    let rec fetchWithPaging offset (accumulated: BankTransaction list) =
        asyncResult {
            let! transactions = getTransactionsPage requestInfo tokens accountId offset

            // Take transactions that are within the date range
            let txInRange = transactions |> List.filter (fun tx -> tx.BookingDate >= dateCutoff)

            // If we got a full page and all transactions are within range, fetch more
            if not (List.isEmpty transactions) && List.length transactions = List.length txInRange then
                return! fetchWithPaging (offset + List.length transactions) (accumulated @ txInRange)
            else
                // We've reached transactions outside our range, stop
                return accumulated @ txInRange
        }

    fetchWithPaging 0 []

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
