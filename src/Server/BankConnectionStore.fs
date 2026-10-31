module Server.BankConnectionStore

open Thoth.Json.Net
open Server.ComdirectClient

// Der gespeicherte Wert, mit dem die Bankverbindung einen Neustart übersteht (ADR 0071 Punkt 4):
// ein verschlüsselter JSON-Wert unter einem Settings-Schlüssel. Er ist ein Token auf der Platte und
// geht deshalb nie an den Client, in Logs oder Fehlermeldungen. `Api.readSettingsForClient` liest
// nur benannte Schlüssel, dieser gehört nicht dazu.

/// Settings-Schlüssel des gespeicherten Werts.
[<Literal>]
let connectionSettingKey = "comdirect_bank_connection"

/// Was eine Wiederaufnahme braucht. Die Zugangsdaten bleiben in den Einstellungen.
type StoredConnection = {
    RequestInfo: RequestInfo
    SessionId: string
    SessionIdentifier: string
    Tokens: Tokens
}

type StoredConnectionRead =
    | NothingStored
    | StoredReadable of StoredConnection
    /// Vorhanden, aber nicht verwendbar. Der Wert selbst bleibt hier draußen.
    | StoredUnreadable

type ConnectionStore = {
    Load: unit -> Async<StoredConnectionRead>
    Save: StoredConnection -> Async<unit>
    Clear: unit -> Async<unit>
}

let storedConnectionOf (session: AuthSession) : StoredConnection =
    { RequestInfo = session.RequestInfo
      SessionId = session.SessionId
      SessionIdentifier = session.SessionIdentifier
      Tokens = session.Tokens }

/// Ohne gesetzten Schlüssel wird nichts gespeichert und nichts gelesen: der Maschinen-Fallback gilt
/// für diesen Wert nicht. Ein Rest aus der Zeit mit Schlüssel zählt dann als unlesbar. Die Meldung
/// des Decoders fällt weg, weil Thoth darin den gelesenen Wert zitiert.
let settingsConnectionStore (keyConfigured: unit -> bool) (settingKey: string) : ConnectionStore =
    { Load = fun () -> async {
        match! Persistence.Settings.readEncryptedSetting settingKey with
        | Persistence.Settings.SettingAbsent -> return NothingStored
        | Persistence.Settings.SettingUndecryptable -> return StoredUnreadable
        | Persistence.Settings.SettingDecrypted _ when not (keyConfigured ()) -> return StoredUnreadable
        | Persistence.Settings.SettingDecrypted json ->
            match Decode.Auto.fromString<StoredConnection> json with
            | Ok stored -> return StoredReadable stored
            | Error _ -> return StoredUnreadable
      }
      Save = fun stored -> async {
        if keyConfigured () then
            do! Persistence.Settings.setSetting settingKey (Encode.Auto.toString (0, stored)) true
      }
      Clear = fun () -> Persistence.Settings.deleteSetting settingKey }

/// Die Schlüssel für den Refresh aus den Einstellungen.
let loadApiKeysFrom (getSetting: string -> Async<string option>) : Async<ApiKeys option> =
    async {
        let! clientId = getSetting "comdirect_client_id"
        let! clientSecret = getSetting "comdirect_client_secret"
        return
            match clientId, clientSecret with
            | Some id, Some secret -> Some { ClientId = id; ClientSecret = secret }
            | _ -> None
    }

/// Der Speicher mit dem echten Schlüsselzugang. Umgebung und Settings-Schlüssel sind eingespeist,
/// damit Tests ihn ohne die Umgebung des Prozesses und ohne geteilten Schlüssel prüfen können. Er
/// liegt hier und nicht in `ComdirectAuthSession.livePorts`, weil dieser Record auch die Netzports trägt.
let connectionStoreFor (readEnvironment: string -> string) (settingKey: string) : ConnectionStore =
    settingsConnectionStore (fun () -> Persistence.Encryption.hasConfiguredKey readEnvironment) settingKey

/// Der Speicher des laufenden Servers.
let liveConnectionStore: ConnectionStore =
    connectionStoreFor System.Environment.GetEnvironmentVariable connectionSettingKey

/// Die Schlüssel des laufenden Servers.
let loadLiveApiKeys () : Async<ApiKeys option> =
    loadApiKeysFrom Persistence.Settings.getSetting
