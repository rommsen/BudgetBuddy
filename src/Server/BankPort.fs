module Server.BankPort

open System
open Shared.Domain
open Server.ComdirectClient
open Server.BankConnection
open Server.ComdirectAuthSession

// Schmaler Bank-Port (ADR 0071 Punkt 5). Heute trägt er die Testnaht für den Abruf, später die ING.
// Nur Comdirect implementiert ihn, deshalb bleiben die `Comdirect*`-Typen in der Signatur.

/// Was der Abruf über die Bankverbindung wissen muss. Eine offene Challenge gibt keine Daten frei.
type BankLinkStatus =
    | BankReady
    | BankAuthRequired

type BankPort = {
    Status: unit -> BankLinkStatus
    /// Startet eine Challenge, solange die Bedingung gilt, sonst `Ok None` ohne Bankkontakt. Geprüft
    /// wird direkt vor der Anmeldung. Nur eine Handlung von Roman ruft das auf.
    StartAuth: (unit -> bool) -> ComdirectSettings -> Async<ComdirectResult<Challenge option>>
    CompleteAuth: unit -> Async<ComdirectResult<unit>>
    FetchAccounts: unit -> Async<ComdirectResult<ComdirectAccount list>>
    /// Konto-Id, Tage, heutiger Kalendertag.
    FetchTransactions: string -> int -> DateTime -> Async<ComdirectResult<BankTransaction list>>
}

let comdirectBankPort (ports: ConnectionPorts) (clock: unit -> DateTimeOffset) : BankPort =
    { Status = fun () ->
        match connectionState ports with
        | Connected _ -> BankReady
        | Disconnected
        | ChallengePending _ -> BankAuthRequired
      StartAuth = startChallengeWhileWith ports
      CompleteAuth = fun () -> confirmChallengeWith ports
      FetchAccounts = fun () -> fetchAccountsWith ports (clock ())
      FetchTransactions = fun accountId days today -> fetchTransactionsWith ports (clock ()) accountId days today }
