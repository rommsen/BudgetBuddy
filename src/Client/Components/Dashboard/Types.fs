module Components.Dashboard.Types

open Shared.Domain
open Types

/// Dashboard-specific model state
type Model = {
    CurrentSession: RemoteData<SyncSession option>
    RecentSessions: RemoteData<SyncSession list>
    Settings: RemoteData<AppSettings>
}

/// Dashboard-specific messages
type Msg =
    | LoadCurrentSession
    | CurrentSessionLoaded of SyncSession option
    | LoadRecentSessions
    | RecentSessionsLoaded of SyncSession list
    | LoadSettings
    | SettingsLoaded of Result<AppSettings, string>
