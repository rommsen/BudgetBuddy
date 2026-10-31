module SettingsSecretsTests

// Regression infrastructure-s1r95: `getSettings` lieferte YNAB-Token, Comdirect-Secret und
// PIN im Klartext an den Client. Regression infrastructure-mngfn: dasselbe für Benutzername und
// Client-ID. Die Serverfälle laufen über Funktionswerte statt über die
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

let private storedClientId = "gespeicherte-client-id-5150"
let private storedUsername = "gespeicherte-zugangsnummer-7788"

let private storedSettings =
    [ "ynab_token", token
      "ynab_default_budget_id", "budget-1"
      "comdirect_client_id", storedClientId
      "comdirect_client_secret", clientSecret
      "comdirect_username", storedUsername
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
    { ClientId = storedClientId
      ClientSecret = ""
      Username = "neuer-name"
      Password = ""
      AccountId = Some "konto-1" }

let private settingsWithEverything : AppSettings =
    { Ynab =
        Some { PersonalAccessToken = token
               DefaultBudgetId = Some (YnabBudgetId "budget-1")
               DefaultAccountId = None
               QuickAddAccountId = None }
      Comdirect =
        Some { ClientId = storedClientId
               ClientSecret = clientSecret
               Username = storedUsername
               Password = password
               AccountId = Some "konto-1" }
      Sync = { DaysToFetch = 30 } }

[<Tests>]
let settingsSecretsServerTests =
    testList "Einstellungen: Secrets bleiben auf dem Server" [
        test "Die Antwort an den Client enthält keine Zugangsdaten und nicht den Verbindungswert" {
            let probe = SettingsProbe storedSettings
            let answer = sprintf "%A" (readSettingsForClient probe.Get |> run)
            for secret in [ token; clientSecret; password; storedClientId; storedUsername; storedConnection ] do
                Expect.isFalse (answer.Contains secret) $"'{secret}' darf nicht in der Antwort stehen"
        }

        test "Die interne Sicht behält die echten Zugangsdaten für den Comdirect-Import" {
            let probe = SettingsProbe storedSettings
            let settings = loadAppSettingsFrom probe.Get |> run
            let comdirect = Expect.wantSome settings.Comdirect "Comdirect eingerichtet"
            Expect.equal comdirect.ClientSecret clientSecret "Secret"
            Expect.equal comdirect.Password password "PIN"
        }

        test "Die Sicht meldet jedes gesetzte Feld als gesetzt und reicht die Budget-Id durch" {
            let client = toSettingsView settingsWithEverything
            let ynab = Expect.wantSome client.Ynab "YNAB eingerichtet"
            let comdirect = Expect.wantSome client.Comdirect "Comdirect eingerichtet"
            Expect.isTrue ynab.HasToken "Token"
            Expect.equal ynab.DefaultBudgetId (Some (YnabBudgetId "budget-1")) "Budget-Id"
            Expect.isTrue comdirect.HasClientId "Client-ID"
            Expect.isTrue comdirect.HasClientSecret "Client-Secret"
            Expect.isTrue comdirect.HasUsername "Benutzername"
            Expect.isTrue comdirect.HasPassword "PIN"
        }

        test "Die Sicht meldet jedes leere Feld einzeln als nicht gesetzt" {
            let comdirectWith change =
                let settings = { settingsWithEverything with Comdirect = settingsWithEverything.Comdirect |> Option.map change }
                Expect.wantSome (toSettingsView settings).Comdirect "Comdirect eingerichtet"
            let flags (view: ComdirectSettingsView) =
                view.HasClientId, view.HasClientSecret, view.HasUsername, view.HasPassword
            Expect.equal (flags (comdirectWith (fun c -> { c with ClientId = "" }))) (false, true, true, true) "Client-ID"
            Expect.equal (flags (comdirectWith (fun c -> { c with ClientSecret = "" }))) (true, false, true, true) "Client-Secret"
            Expect.equal (flags (comdirectWith (fun c -> { c with Username = "" }))) (true, true, false, true) "Benutzername"
            Expect.equal (flags (comdirectWith (fun c -> { c with Password = "" }))) (true, true, true, false) "PIN"
        }

        test "Die Sicht meldet einen leeren Token als nicht gesetzt und eine fehlende Budget-Id als fehlend" {
            let settings =
                { settingsWithEverything with
                    Ynab = settingsWithEverything.Ynab |> Option.map (fun y -> { y with PersonalAccessToken = ""; DefaultBudgetId = None }) }
            let ynab = Expect.wantSome (toSettingsView settings).Ynab "YNAB eingerichtet"
            Expect.isFalse ynab.HasToken "Token"
            Expect.isNone ynab.DefaultBudgetId "Budget-Id"
        }

        test "Ohne Einrichtung liefert die Sicht weder YNAB noch Comdirect" {
            let client = toSettingsView { settingsWithEverything with Ynab = None; Comdirect = None }
            Expect.isNone client.Ynab "YNAB"
            Expect.isNone client.Comdirect "Comdirect"
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

        // Bug infrastructure-mngfn: der Client kennt Username und ClientId nicht mehr und schickt
        // sie ohne neue Eingabe leer. Sie dürfen dann nicht verloren gehen.
        test "Leere Felder für Benutzername und Client-ID behalten die gespeicherten Werte" {
            let probe = SettingsProbe storedSettings
            let result = save probe { formWithoutSecrets with ClientId = ""; Username = "" }
            Expect.isOk result "Speichern gelingt"
            Expect.equal (probe.Stored "comdirect_client_id") storedClientId "Client-ID behalten"
            Expect.equal (probe.Stored "comdirect_username") storedUsername "Benutzername behalten"
        }

        test "Ein neuer Wert ersetzt Benutzername und Client-ID" {
            let probe = SettingsProbe storedSettings
            let result = save probe { formWithoutSecrets with ClientId = "neue-client-id"; Username = "neuer-name" }
            Expect.isOk result "Speichern gelingt"
            Expect.equal (probe.Stored "comdirect_client_id") "neue-client-id" "Client-ID ersetzt"
            Expect.equal (probe.Stored "comdirect_username") "neuer-name" "Benutzername ersetzt"
        }

        test "Ein Formular ohne jede Eingabe trennt die Bankverbindung nicht" {
            let probe = SettingsProbe storedSettings
            let disconnects = ref 0
            let form = { formWithoutSecrets with ClientId = ""; Username = "" }
            let result =
                saveComdirectCredentialsWith probe.Get probe.Set (fun () -> async { disconnects.Value <- disconnects.Value + 1 }) form
                |> run
            Expect.isOk result "Speichern gelingt"
            Expect.equal disconnects.Value 0 "nichts geändert, also nichts getrennt"
        }

        test "Ohne gespeicherten Benutzernamen bleibt das leere Feld ein Validierungsfehler" {
            let probe = SettingsProbe (storedSettings |> List.filter (fun (key, _) -> key <> "comdirect_username"))
            Expect.isError (save probe { formWithoutSecrets with Username = "" }) "Validierungsfehler"
            Expect.isEmpty probe.Writes "nichts gespeichert"
        }

        test "Ohne gespeicherte Client-ID bleibt das leere Feld ein Validierungsfehler" {
            let probe = SettingsProbe (storedSettings |> List.filter (fun (key, _) -> key <> "comdirect_client_id"))
            Expect.isError (save probe { formWithoutSecrets with ClientId = "" }) "Validierungsfehler"
            Expect.isEmpty probe.Writes "nichts gespeichert"
        }

        // Bug infrastructure-mngfn: `loadAppSettingsFrom` fing jede Ausnahme und meldete leere
        // Einstellungen, ein Datenbankfehler sah aus wie "nichts gesetzt".
        test "Ein Lesefehler erreicht den Aufrufer als Fehler" {
            let probe = SettingsProbe storedSettings
            let failing key =
                if key = "comdirect_password" then raise (System.InvalidOperationException "Datenbank nicht lesbar")
                else probe.Get key
            Expect.throwsT<System.InvalidOperationException>
                (fun () -> loadAppSettingsFrom failing |> run |> ignore)
                "loadAppSettingsFrom"
            Expect.throwsT<System.InvalidOperationException>
                (fun () -> readSettingsForClient failing |> run |> ignore)
                "readSettingsForClient"
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
        Some { HasClientId = true
               HasClientSecret = true
               HasUsername = true
               HasPassword = true
               AccountId = Some "konto-1" }
      Sync = { DaysToFetch = 30 } }

let private loaded settings =
    let model, _ = Components.Settings.State.init ()
    updateSettings (SettingsLoaded (Ok settings)) model

[<Tests>]
let settingsSecretsClientTests =
    testList "Einstellungen: Formular mit gesetzten Secrets" [
        test "Gesetzte Zugangsdaten sind keine Pflichtfelder" {
            let required = comdirectRequiredFields (loaded view) |> List.map fst
            Expect.equal required [ "Konto-ID" ] "nur die Konto-ID"
            Expect.isEmpty (ynabRequiredFields (loaded view)) "Token muss nicht neu eingegeben werden"
        }

        test "Ohne Einrichtung bleiben Token und alle Zugangsdaten Pflichtfelder" {
            let empty = loaded { view with Ynab = None; Comdirect = None }
            let required = comdirectRequiredFields empty |> List.map fst
            Expect.equal required [ "Client-ID"; "Client-Secret"; "Benutzername"; "PIN"; "Konto-ID" ] "alle Felder"
            Expect.hasLength (ynabRequiredFields empty) 1 "Token"
        }

        test "Speichern ohne Token-Eingabe schließt den Dialog ohne Aufruf" {
            let model = { loaded view with EditingSection = Some YnabSection }
            let updated, cmd, _ = Components.Settings.State.update SaveYnabToken model
            Expect.isNone updated.EditingSection "Dialog zu"
            Expect.isEmpty cmd "kein saveYnabToken"
        }

        test "Nach dem Speichern merkt sich der Client nur, dass die Zugangsdaten gesetzt sind" {
            let model =
                { loaded { view with Comdirect = None } with
                    ComdirectClientIdInput = "neue-client-id"
                    ComdirectClientSecretInput = "neues-secret"
                    ComdirectUsernameInput = "neuer-name"
                    ComdirectPasswordInput = "neue-pin" }
            let updated = updateSettings (ComdirectCredentialsSaved (Ok ())) model
            Expect.equal updated.ComdirectPasswordInput "" "PIN-Eingabe geleert"
            Expect.equal updated.ComdirectClientSecretInput "" "Secret-Eingabe geleert"
            Expect.equal updated.ComdirectClientIdInput "" "Client-ID-Eingabe geleert"
            Expect.equal updated.ComdirectUsernameInput "" "Benutzername-Eingabe geleert"
            Expect.isTrue (comdirectPasswordIsSet updated) "PIN gesetzt"
            Expect.isTrue (comdirectClientSecretIsSet updated) "Secret gesetzt"
            Expect.isTrue (comdirectClientIdIsSet updated) "Client-ID gesetzt"
            Expect.isTrue (comdirectUsernameIsSet updated) "Benutzername gesetzt"
        }

        // Bug infrastructure-mngfn: `CancelEditing` ließ die Eingaben stehen. Der verworfene
        // Entwurf ging beim nächsten Speichern mit und überschrieb das gesetzte Secret.
        test "Ein abgebrochener Comdirect-Entwurf überschreibt beim nächsten Speichern nichts" {
            let draft =
                { loaded view with
                    EditingSection = Some ComdirectSection
                    ComdirectClientIdInput = "entwurf-client-id"
                    ComdirectClientSecretInput = "entwurf-secret"
                    ComdirectUsernameInput = "entwurf-name"
                    ComdirectPasswordInput = "entwurf-pin" }
            let reopened = draft |> updateSettings CancelEditing |> updateSettings (StartEditing ComdirectSection)
            let probe = SettingsProbe storedSettings
            Expect.isOk (save probe (comdirectCredentialsFromForm reopened)) "Speichern gelingt"
            Expect.equal (probe.Stored "comdirect_client_id") storedClientId "Client-ID unverändert"
            Expect.equal (probe.Stored "comdirect_client_secret") clientSecret "Secret unverändert"
            Expect.equal (probe.Stored "comdirect_username") storedUsername "Benutzername unverändert"
            Expect.equal (probe.Stored "comdirect_password") password "PIN unverändert"
        }

        test "Ein abgebrochener Token-Entwurf löst beim nächsten Speichern keinen Aufruf aus" {
            let draft = { loaded view with EditingSection = Some YnabSection; YnabTokenInput = "entwurf-token" }
            let reopened = draft |> updateSettings CancelEditing |> updateSettings (StartEditing YnabSection)
            let updated, cmd, _ = Components.Settings.State.update SaveYnabToken reopened
            Expect.isNone updated.EditingSection "Dialog zu"
            Expect.isEmpty cmd "kein saveYnabToken"
        }
    ]
