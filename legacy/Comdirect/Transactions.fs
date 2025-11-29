module Comdirect.Transactions

open System
open Thoth.Json.Net
open FsHttp
open FsToolkit.ErrorHandling
open Comdirect.API

type Transaction =
  {
    Reference : string 
    Booking_Date : DateTime
    Amount : decimal
    Name : string option
    Info: string
  }

let txDecoder : Decoder<Transaction> =
  Decode.object
    (fun get ->
      let name =
        match get.Optional.At ["remitter" ; "holderName"] Decode.string with 
        | Some x -> Some x 
        | None -> get.Optional.At ["creditor" ; "holderName"] Decode.string

      let bookingDate = 
        get.Required.Field "bookingDate" Decode.string
        |> DateTime.Parse
        |> fun dt -> dt.Date
      { 
        Reference = get.Required.Field "reference" Decode.string
        Booking_Date = bookingDate
        Name = name
        Amount = get.Required.At ["amount"; "value"] Decode.decimal
        Info = get.Required.Field "remittanceInfo" Decode.string
      }
    )

let txsDecoder : Decoder<Transaction list> =
  Decode.field "values" (Decode.list txDecoder)

let get (requestInfo: RequestInfo) tokens accountId startPagingAt =
  async {
    // printfn "[get] Fetching transactions at offset %i for account %s" startPagingAt accountId
    let! response =
      (http {
        GET (sprintf "%sapi/banking/v1/accounts/%s/transactions?transactionState=BOOKED&paging-first=%i" endpoint accountId startPagingAt)
        Accept "application/json"
        Authorization (sprintf "Bearer %s" tokens.Access)
        header "x-http-request-info" (requestInfo.Encode())
        body
        ContentType "application/json"
      }
      |> Request.sendAsync)

    // printfn "[get] Received response: %A" response
    match response |> Response.toResult with 
    | Ok resp ->
        let! json = resp |> Response.toTextAsync
        // printfn "[get] Response body: %s" json
        return Decode.fromString txsDecoder json
    | Error resp ->
        // printfn "[get] Error status code: %A" resp
        return! errorWithCodeAndMessage resp
  }

let getLastXDays (days : int) (requestInfo: RequestInfo) tokens accountId =
  let dateCutoff = DateTime.Today.Subtract(TimeSpan.FromDays(float days))
  printfn "[getLastXDays] Date cutoff: %A" dateCutoff
  let rec startWithPagingAt startPagingAt =
    asyncResult {
      printfn "[getLastXDays] Paging at %i" startPagingAt
      let! transactions = get requestInfo tokens accountId startPagingAt
      let txInUse = transactions |> List.takeWhile (fun tx -> tx.Booking_Date >= dateCutoff)
      printfn "[getLastXDays] Retrieved %i transactions, %i within cutoff" (List.length transactions) (List.length txInUse)
      if List.length transactions = List.length txInUse then
        let! more = startWithPagingAt (startPagingAt + List.length transactions)
        return txInUse @ more
      else
        return txInUse
    }
  startWithPagingAt 0
