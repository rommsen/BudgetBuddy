module Comdirect.API

open Thoth.Json.Net
open FsHttp

type RequestInfo =
  {
    Request_Id : string 
    Session_Id : string
  }

  member __.Encode () =
    let request_info = {|clientRequestId = {|sessionId = __.Session_Id; requestId = __.Request_Id |} |}
    Encode.Auto.toString(0, request_info)


type Credentials =
  {
    Username : string
    Password : string
  }

  static member Create username password =
    {
    Username = username
    Password = password
  }

type Tokens =
    { Access : string
      Refresh : string }

    static member Decoder : Decoder<Tokens> =
      Decode.map2
        (fun access refresh -> { Access = access ;  Refresh = refresh } )
        (Decode.field "access_token" Decode.string)
        (Decode.field "refresh_token" Decode.string)


type Challenge =
  {
    Id : string 
    Typ : string
  }

  static member Decoder : Decoder<Challenge> =
      Decode.map2
        (fun challenge_id challenge_typ -> { Id = challenge_id ;  Typ = challenge_typ } )
        (Decode.field "id" Decode.string)
        (Decode.field "typ" Decode.string)


[<Literal>]
let endpoint = "https://api.comdirect.de/"

let errorWithCodeAndMessage response =
  async {
    let! error = response |> Response.toTextAsync
    return Error (sprintf "Code: %i; Message: %s" (int response.statusCode) error) 
  }