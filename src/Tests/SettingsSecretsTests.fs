module SettingsSecretsTests

// Regression infrastructure-s1r95: `getSettings` lieferte YNAB-Token, Comdirect-Secret und
// PIN im Klartext an den Client. Die Serverfälle laufen über Funktionswerte statt über die
// geteilte In-Memory-DB: `RemotingEndpointNegativeTests` verlangt, dass dort keine
// Zugangsdaten stehen.

open Expecto
open Shared.Domain
open Server.Api

let private run (a: Async<'T>) = Async.RunSynchronously a

let private token = "ynab-geheimes-token-1234"
let private clientSecret = "comdirect-geheimes-secret"
let private password = "geheime-pin-9876"
let private storedConnection = "gespeicherter-verbindungswert-aus-adr-0071"

let private storedSettings =
    [ "ynab_token", token
      "ynab_default_budget_id", "budget-1"
      "comdirect_client_id", "client-id"
      "comdirect_client_secret", clientSecret
      "comdirect_username", "alter-name"
      "comdirect_password", password
      "comdirect_account_id", "konto-1"
      Server.BankConnectionStore.connectionSettingKey, storedConnection ]

/// Port-Paar auf einem Wörterbuch. `writes` hält Schlüssel, Wert und Verschlüsselungs-Flag fest.
type private SettingsProbe(initial: (string * string) list) =
    let values = System.Collections.Generic.Dictionary<string, string>()
    do for key, value in initial do values[key] <- value
    member val Writes = ResizeArray<string * string * bool>()
    member _.Get(key: string) =
        async { return (match values.TryGetValue key with | true, v -> Some v | _ -> None) }
    member this.Set (key: string) (value: string) (encrypted: bool) =
        async {
            values[key] <- value
            this.Writes.Add((key, value, encrypted))
        }
    member _.Stored(key: string) = values[key]

let private save (probe: SettingsProbe) credentials =
    saveComdirectCredentialsWith probe.Get probe.Set (fun () -> async.Return ()) credentials |> run

let private formWithoutSecrets : ComdirectSettings =
    { ClientId = "client-id"
      ClientSecret = ""
      Username = "neuer-name"
      Password = ""
      AccountId = Some "konto-1" }

[<Tests>]
let settingsSecretsServerTests =
    testList "Einstellungen: Secrets bleiben auf dem Server" [
        test "Die Antwort an den Client enthält weder Token noch Client-Secret noch PIN noch den Verbindungswert" {
            let probe = SettingsProbe storedSettings
            let answer = sprintf "%A" (readSettingsForClient probe.Get |> run)
            for secret in [ token; clientSecret; password; storedConnection ] do
                Expect.isFalse (answer.Contains secret) $"'{secret}' darf nicht in der Antwort stehen"
        }

        test "Die interne Sicht behält die echten Zugangsdaten für den Comdirect-Import" {
            let probe = SettingsProbe storedSettings
            let settings = loadAppSettingsFrom probe.Get |> run
            let comdirect = Expect.wantSome settings.Comdirect "Comdirect eingerichtet"
            Expect.equal comdirect.ClientSecret clientSecret "Secret"
            Expect.equal comdirect.Password password "PIN"
        }

        test "Speichern ohne Secret-Eingabe ändert die anderen Felder und behält die Secrets" {
            let probe = SettingsProbe storedSettings
            Expect.isOk (save probe formWithoutSecrets) "Speichern gelingt"
            Expect.equal (probe.Stored "comdirect_username") "neuer-name" "Benutzername geändert"
            Expect.equal (probe.Stored "comdirect_client_secret") clientSecret "Secret behalten"
            Expect.equal (probe.Stored "comdirect_password") password "PIN behalten"
            Expect.contains probe.Writes ("comdirect_client_secret", clientSecret, true) "Secret bleibt verschlüsselt"
            Expect.contains probe.Writes ("comdirect_password", password, true) "PIN bleibt verschlüsselt"
        }

        test "Eine neue Eingabe ersetzt nur das eingegebene Secret" {
            let probe = SettingsProbe storedSettings
            Expect.isOk (save probe { formWithoutSecrets with Password = "neue-pin" }) "Speichern gelingt"
            Expect.equal (probe.Stored "comdirect_password") "neue-pin" "PIN ersetzt"
            Expect.equal (probe.Stored "comdirect_client_secret") clientSecret "Secret behalten"
        }

        test "Ohne gespeichertes Secret bleibt die leere Eingabe ein Validierungsfehler und schreibt nichts" {
            let probe = SettingsProbe []
            Expect.isError (save probe formWithoutSecrets) "Validierungsfehler"
            Expect.isEmpty probe.Writes "nichts gespeichert"
        }
    ]

// ---------------- Client ----------------

open Types
open Components.Settings.Types

let private updateSettings msg model =
    let updated, _, _ = Components.Settings.State.update msg model
    updated

let private view : AppSettingsView =
    { Ynab =
        Some { HasToken = true
               DefaultBudgetId = None
               DefaultAccountId = None
               QuickAddAccountId = None }
      Comdirect =
        Some { ClientId = "client-id"
               HasClientSecret = true
               Username = "alter-name"
               HasPassword = true
               AccountId = Some "konto-1" }
      Sync = { DaysToFetch = 30 } }

let private loaded settings =
    let model, _ = Components.Settings.State.init ()
    updateSettings (SettingsLoaded (Ok settings)) model

[<Tests>]
let settingsSecretsClientTests =
    testList "Einstellungen: Formular mit gesetzten Secrets" [
        test "Mit gesetzten Secrets sind Client-Secret und PIN keine Pflichtfelder" {
            let required = comdirectRequiredFields (loaded view) |> List.map fst
            Expect.equal required [ "Client-ID"; "Benutzername"; "Konto-ID" ] "nur die Felder ohne Secret"
            Expect.isEmpty (ynabRequiredFields (loaded view)) "Token muss nicht neu eingegeben werden"
        }

        test "Ohne Einrichtung bleiben Token, Client-Secret und PIN Pflichtfelder" {
            let empty = loaded { view with Ynab = None; Comdirect = None }
            Expect.contains (comdirectRequiredFields empty |> List.map fst) "Client-Secret" "Client-Secret"
            Expect.contains (comdirectRequiredFields empty |> List.map fst) "PIN" "PIN"
            Expect.hasLength (ynabRequiredFields empty) 1 "Token"
        }

        test "Speichern ohne Token-Eingabe schließt den Dialog ohne Aufruf" {
            let model = { loaded view with EditingSection = Some YnabSection }
            let updated, cmd, _ = Components.Settings.State.update SaveYnabToken model
            Expect.isNone updated.EditingSection "Dialog zu"
            Expect.isEmpty cmd "kein saveYnabToken"
        }

        test "Nach dem Speichern merkt sich der Client nur, dass das Secret gesetzt ist" {
            let model = { loaded { view with Comdirect = None } with ComdirectPasswordInput = "neue-pin"; ComdirectClientSecretInput = "neues-secret" }
            let updated = updateSettings (ComdirectCredentialsSaved (Ok ())) model
            Expect.equal updated.ComdirectPasswordInput "" "PIN-Eingabe geleert"
            Expect.equal updated.ComdirectClientSecretInput "" "Secret-Eingabe geleert"
            Expect.isTrue (comdirectPasswordIsSet updated) "PIN gesetzt"
            Expect.isTrue (comdirectClientSecretIsSet updated) "Secret gesetzt"
        }
    ]
