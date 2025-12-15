module Components.Dashboard.Types

open Shared.Domain
open Types

/// Dashboard-specific model state
type Model = {
    CurrentSession: RemoteData<SyncSession option>
    LastSession: RemoteData<SyncSession option>
    Settings: RemoteData<AppSettings>
}

/// Dashboard-specific messages
type Msg =
    | LoadCurrentSession
    | CurrentSessionLoaded of Result<SyncSession option, string>
    | LoadLastSession
    | LastSessionLoaded of Result<SyncSession option, string>
    | LoadSettings
    | SettingsLoaded of Result<AppSettings, string>
