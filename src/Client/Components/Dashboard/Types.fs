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
    | CurrentSessionLoaded of Result<SyncSession option, string>
    | LoadRecentSessions
    | RecentSessionsLoaded of Result<SyncSession list, string>
    | LoadSettings
    | SettingsLoaded of Result<AppSettings, string>
