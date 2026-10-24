module Pwa

// Installierte PWA übernimmt einen neuen Deploy (infrastructure-bnbdc, ADR 0057).
//
// Zwei Teile in einer Datei:
//  1. Reine Entscheidungslogik (`shouldCheck`, `decide`): ohne Browser testbar.
//  2. Interop zu `virtual:pwa-register` (vite-plugin-pwa, `registerType: 'prompt'`).
//
// Ablauf: Kommt die PWA in den Vordergrund, fragt der Client höchstens alle fünf Minuten
// nach einem neuen Service Worker (`registration.update()`). Wartet einer, lädt die Seite
// sofort neu, außer es läuft eine Eingabe; dann steht ein Hinweis mit Knopf da.

open Fable.Core
open Fable.Core.JsInterop

// ============================================
// Reine Entscheidungslogik
// ============================================

/// Mindestabstand zwischen zwei Prüfungen auf einen neuen Service Worker: fünf Minuten.
[<Literal>]
let checkIntervalMs = 300000L

/// Der kleine Zustand der PWA-Aktualisierung im Root-Modell.
type PwaState = {
    /// Zeitpunkt der letzten Prüfung (`Date.now()` in ms), `None` vor der ersten.
    LastCheckMs: int64 option
    /// Wartet ein neuer Service Worker auf Freigabe (`onNeedRefresh` hat gefeuert)?
    UpdateWaiting: bool
    /// Läuft das Neuladen schon? Verhindert einen zweiten Reload (keine Schleife).
    Reloading: bool
    /// Steht der Hinweis "Neue Version, neu laden" gerade da?
    NoticeShown: bool
}

let initialState : PwaState = {
    LastCheckMs = None
    UpdateWaiting = false
    Reloading = false
    NoticeShown = false
}

/// Was nach einer Zustandsänderung zu tun ist.
type PwaAction =
    | Nothing
    /// Sofort freigeben und neu laden.
    | ReloadNow
    /// Hinweis mit Knopf zeigen, weil gerade eine Eingabe läuft.
    | ShowNotice

/// Ist eine Prüfung fällig? `true` ohne letzte Prüfung oder ab `checkIntervalMs` Abstand.
/// Springt die Uhr rückwärts (`now < last`), gilt das nicht als fällig.
let shouldCheck (lastCheckMs: int64 option) (nowMs: int64) : bool =
    match lastCheckMs with
    | None -> true
    | Some last -> nowMs >= last && nowMs - last >= checkIntervalMs

/// Die Entscheidungstabelle (ADR 0057 Punkt 3): neu laden genau dann, wenn ein Worker
/// wartet, keine Eingabe läuft und noch kein Neuladen läuft; Hinweis, wenn ein Worker
/// wartet, eine Eingabe läuft und noch kein Hinweis steht; sonst nichts.
let decide (s: PwaState) (busy: bool) : PwaAction =
    if not s.UpdateWaiting || s.Reloading then Nothing
    elif not busy then ReloadNow
    elif not s.NoticeShown then ShowNotice
    else Nothing

// ============================================
// Interop zu virtual:pwa-register
// ============================================

/// `registerSW` aus dem virtuellen Modul von vite-plugin-pwa. Gibt `updateSW` zurück:
/// `updateSW(true)` gibt den wartenden Worker frei, die Seite lädt dann bei `controlling`
/// neu. Im Dev-Server (PWA aus) liefert Vite ein Modul, dessen Funktionen nichts tun.
[<Import("registerSW", "virtual:pwa-register")>]
let private registerSW (options: obj) : (bool -> JS.Promise<unit>) = jsNative

let mutable private registration : obj = null
let mutable private updateSW : (bool -> JS.Promise<unit>) option = None
let mutable private registered = false

/// Registriert den Service Worker einmal beim Start. `onNeedRefresh` feuert nur, wenn
/// ein Worker auf einen schon installierten folgt und wartet (im Modus 'prompt' ruft
/// `register.js` es bei `waiting` oder bei `installed` mit `isExternal`). Beim ersten
/// Installieren gibt es keinen vorherigen Worker: das Ereignis `installed` hat dann
/// `isUpdate: false` und löst nur `onOfflineReady` aus, `onNeedRefresh` bleibt still,
/// `UpdateWaiting` also `false`.
let register (onNeedRefresh: unit -> unit) : unit =
    if not registered then
        registered <- true
        let options =
            createObj [
                "onNeedRefresh" ==> (fun () -> onNeedRefresh ())
                "onRegisteredSW" ==> (fun (_swUrl: string) (r: obj) -> registration <- r)
            ]
        updateSW <- Some (registerSW options)

/// Fragt den Browser nach einem neuen Service Worker. Fehler (offline, Server weg)
/// werden geschluckt. Ohne Registrierung (Dev, Browser ohne Service Worker) ein No-op.
let checkForUpdate () : unit =
    if not (isNull registration) then
        try
            let p: JS.Promise<obj> = registration?update ()
            p.catch (fun _ -> null) |> ignore
        with _ -> ()

/// Gibt den wartenden Worker frei; die Seite lädt dann neu. Ohne Registrierung ein No-op.
let applyUpdate () : unit =
    match updateSW with
    | Some f -> f true |> ignore
    | None -> ()
