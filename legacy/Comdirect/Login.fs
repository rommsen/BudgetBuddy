module Comdirect.Login

open System
open Thoth.Json.Net
open FsHttp
open FsToolkit.ErrorHandling
open Config
open Comdirect.API

let private initOauth credentials apiKeys =
  let credentialsBody =
    sprintf "client_id=%s&client_secret=%s&username=%s&password=%s&grant_type=password"
      apiKeys.Client_Id
      apiKeys.Client_Secret
      credentials.Username
      credentials.Password
  // printfn "[initOauth] Credentials: %s" credentialsBody
  async {
    // printfn "[initOauth] Sending token request..."
    let! response =
      (http {
        POST (sprintf "%soauth/token" endpoint)
        Accept "application/json"
        body
        ContentType "application/x-www-form-urlencoded"
        text credentialsBody
      })
      |> Request.sendAsync

    // printfn "[initOauth] Received response: %A" response

    match response |> Response.toResult with
    | Ok resp ->
        let! jsonString = resp |> Response.toTextAsync
        // printfn "[initOauth] Response body: %s" jsonString
        return Decode.fromString Tokens.Decoder jsonString
    | Error resp ->
        // printfn "[initOauth] Error status code: %A" resp
        return! errorWithCodeAndMessage resp
  }

let private getTokens (requestInfo: RequestInfo) tokens =
  async {
    // printfn "[getTokens] Requesting session identifier..."
    let! response =
      (http {
        GET (sprintf "%sapi/session/clients/user/v1/sessions" endpoint)
        Accept "application/json"
        Authorization (sprintf "Bearer %s" tokens.Access)
        header "x-http-request-info" (requestInfo.Encode())
      })
      |> Request.sendAsync

    // printfn "[getTokens] Received response: %A" response

    let identifierDecoder =
      Decode.index 0 (Decode.field "identifier" Decode.string)

    match response |> Response.toResult with
    | Ok resp ->
        let! jsonString = resp |> Response.toTextAsync
        // printfn "[getTokens] Response body: %s" jsonString
        return Decode.fromString identifierDecoder jsonString
    | Error resp ->
        // printfn "[getTokens] Error status code: %A" resp
        return! errorWithCodeAndMessage resp
  }

let private validationChallenge (requestInfo: RequestInfo) tokens sessionIdentifier =
  let sessionPayload = {| identifier = sessionIdentifier; sessionTanActive = true; activated2FA = true |}
  async {
    // printfn "[validationChallenge] Validating session %s..." sessionIdentifier
    let! response =
      (http {
        POST (sprintf "%sapi/session/clients/user/v1/sessions/%s/validate" endpoint sessionIdentifier)
        Accept "application/json"
        Authorization (sprintf "Bearer %s" tokens.Access)
        header "x-http-request-info" (requestInfo.Encode())
        body
        ContentType "application/json"
        json (Encode.Auto.toString(0, sessionPayload))
      })
      |> Request.sendAsync

    // printfn "[validationChallenge] Received response: %A" response

    match response |> Response.toResult with
    | Ok resp ->
        let headerName = "x-once-authentication-info"
        let maybeHeader =
          if resp.headers.Contains(headerName) then resp.headers.GetValues(headerName) |> Seq.tryHead
          else None
        let resultChallenge =
          match maybeHeader with
          | Some json -> Decode.fromString Challenge.Decoder json
          | None -> Error (sprintf "Could not extract challenge header: %s" headerName)
        let bindPush ch =
          if ch.Typ = "P_TAN_PUSH" then Ok ch
          else Error "This client can only validate push tans (P_TAN_PUSH)"
        let final = resultChallenge |> Result.bind bindPush
        // printfn "[validationChallenge] Challenge result: %A" final
        return final
    | Error resp ->
        // printfn "[validationChallenge] Error status code: %A" resp
        return! errorWithCodeAndMessage resp
  }

let private activateSessionTan (requestInfo: RequestInfo) tokens sessionIdentifier challenge =
  let sessionPayload = {| identifier = sessionIdentifier; sessionTanActive = true; activated2FA = true |}
  let authInfo = {| id = challenge.Id |}
  async {
    // printfn "[activateSessionTan] Activating session TAN..."
    let! response =
      (http {
        PATCH (sprintf "%sapi/session/clients/user/v1/sessions/%s" endpoint sessionIdentifier)
        Accept "application/json"
        AuthorizationBearer tokens.Access
        header "x-http-request-info" (requestInfo.Encode())
        header "x-once-authentication-info" (Encode.Auto.toString(0, authInfo))
        header "x-once-authentication" "000000"
        body
        ContentType "application/json"
        json (Encode.Auto.toString(0, sessionPayload))
      })
      |> Request.sendAsync

    // printfn "[activateSessionTan] Received response: %A" response

    match response |> Response.toResult with
    | Ok resp -> return Ok resp
    | Error resp ->
        // printfn "[activateSessionTan] Error status code: %A" resp
        return! errorWithCodeAndMessage resp
  }

let private grantExtendedAccountPermission tokens apiKeys =
  let credentialsBody =
    sprintf "client_id=%s&client_secret=%s&token=%s&grant_type=cd_secondary"
      apiKeys.Client_Id
      apiKeys.Client_Secret
      tokens.Access
  // printfn "[grantExtendedAccountPermission] Credentials: %s" credentialsBody
  async {
    // printfn "[grantExtendedAccountPermission] Sending extended grant..."
    let! response =
      (http {
        POST (sprintf "%soauth/token" endpoint)
        Accept "application/json"
        body
        ContentType "application/x-www-form-urlencoded"
        text credentialsBody
      })
      |> Request.sendAsync

    // printfn "[grantExtendedAccountPermission] Received response: %A" response

    match response |> Response.toResult with
    | Ok resp ->
        let! jsonString = resp |> Response.toTextAsync
        // printfn "[grantExtendedAccountPermission] Body: %s" jsonString
        return Decode.fromString Tokens.Decoder jsonString
    | Error resp ->
        // printfn "[grantExtendedAccountPermission] Error status code: %A" resp
        return! errorWithCodeAndMessage resp
  }

let login credentials apiKeys =  
  let requestInfo =
    { Request_Id = DateTimeOffset.Now.ToUnixTimeSeconds().ToString().Substring(0,9)
      Session_Id = Guid.NewGuid().ToString() }
  asyncResult {
    // printfn "[login] Start login"
    // printfn "[login] Credentials: %A" credentials
    let! tokens = initOauth credentials apiKeys
    // printfn "[login] Tokens: %A" tokens
    let! sessionIdentifier = getTokens requestInfo tokens
    // printfn "[login] session_identifier: %s" sessionIdentifier
    let! challenge = validationChallenge requestInfo tokens sessionIdentifier
    // printfn "[login] Validation challenge: %A" challenge
    printfn "Press any key after push TAN accepted"
    Console.ReadKey() |> ignore
    let! _ = activateSessionTan requestInfo tokens sessionIdentifier challenge
    // printfn "[login] Granted session TAN"
    // printfn "[login] Extending account permission"
    let! newTokens = grantExtendedAccountPermission tokens apiKeys
    // printfn "[login] Extended tokens: %A" newTokens
    return (requestInfo, newTokens)
  }
