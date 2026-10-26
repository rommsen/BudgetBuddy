module Api

open Fable.Remoting.Client
open Shared.Api

/// Fable.Remoting proxy for Settings API
let settings : SettingsApi =
    Remoting.createApi()
    |> Remoting.withRouteBuilder (fun typeName methodName -> $"/api/{typeName}/{methodName}")
    |> Remoting.buildProxy<SettingsApi>

/// Fable.Remoting proxy for YNAB API
let ynab : YnabApi =
    Remoting.createApi()
    |> Remoting.withRouteBuilder (fun typeName methodName -> $"/api/{typeName}/{methodName}")
    |> Remoting.buildProxy<YnabApi>

/// Fable.Remoting proxy for Rules API
let rules : RulesApi =
    Remoting.createApi()
    |> Remoting.withRouteBuilder (fun typeName methodName -> $"/api/{typeName}/{methodName}")
    |> Remoting.buildProxy<RulesApi>

/// Fable.Remoting proxy for Sync API
let sync : SyncApi =
    Remoting.createApi()
    |> Remoting.withRouteBuilder (fun typeName methodName -> $"/api/{typeName}/{methodName}")
    |> Remoting.buildProxy<SyncApi>

/// Fable.Remoting proxy for Ledger API (Kassenbuch)
let ledger : LedgerApi =
    Remoting.createApi()
    |> Remoting.withRouteBuilder (fun typeName methodName -> $"/api/{typeName}/{methodName}")
    |> Remoting.buildProxy<LedgerApi>

/// Fable.Remoting proxy for Budget API (Budget-Seite)
let budget : BudgetApi =
    Remoting.createApi()
    |> Remoting.withRouteBuilder (fun typeName methodName -> $"/api/{typeName}/{methodName}")
    |> Remoting.buildProxy<BudgetApi>
