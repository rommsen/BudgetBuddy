/// Modell und Nachrichten der Kassenbuch-Seite (ledger-q5w9c).
///
/// Die Seite liest nur: Konten, Buchungen eines Kontos, Historie einer Buchung und
/// den Status des letzten Spiegel-Laufs. Geschrieben wird ausschliesslich ueber den
/// manuellen Ausloeser "Jetzt spiegeln" (ADR 0014, Punkt 3); ein automatischer
/// Ausloeser (Frische-Kriterium, Lauf nach Push) gehoert bewusst nicht hierher.
module Components.Ledger.Types

open System
open Shared.Domain
open Types

type Model = {
    /// Die Konten samt Vorauswahl des Servers.
    Accounts: RemoteData<LedgerAccountsView>

    /// Das gerade gezeigte Konto. `None`, solange nichts geladen ist oder das
    /// Kassenbuch leer ist.
    SelectedAccountId: Guid option

    /// Die Buchungen des gewaehlten Kontos, neueste zuerst. In YNAB geloeschte
    /// Buchungen sind enthalten und werden gekennzeichnet, nie herausgefiltert.
    Entries: RemoteData<LedgerEntryView list>

    /// Die Buchung, deren Historie gerade im Sheet steht.
    HistoryFor: LedgerEntryView option

    /// Die Historie der offenen Buchung.
    History: RemoteData<LedgerHistoryLine list>

    /// Zeitpunkt und Ergebnis des zuletzt gelaufenen Spiegels.
    LastMirrorRun: RemoteData<MirrorRunStatus option>

    /// Laeuft gerade ein Spiegel-Lauf? Die Liste bleibt waehrenddessen stehen:
    /// Roman wartet nie auf den Spiegel (ADR 0014).
    MirrorRunning: bool
}

/// Die Nachrichten der Seite.
///
/// Jeder Ausloeser ist absichtlich in zwei Nachrichten geteilt: eine **reine**, die
/// nur den Zustand setzt (`StartMirror`), und eine **unreine**, die den Aufruf
/// absetzt (`RunMirror`). Damit ist der interessante Uebergang testbar, ohne den
/// Fable.Remoting-Proxy anzufassen, der unter .NET nicht existiert.
type Msg =
    // Konten
    | LoadAccounts
    | AccountsLoaded of Result<LedgerAccountsView, string>
    | SelectAccount of Guid

    // Buchungen
    | LoadEntries
    | EntriesLoaded of Result<LedgerEntryView list, string>

    // Historie einer Buchung
    | OpenHistory of LedgerEntryView
    | CloseHistory
    | LoadHistory of Guid
    | HistoryLoaded of Result<LedgerHistoryLine list, string>

    // Spiegel
    | LoadMirrorStatus
    | MirrorStatusLoaded of Result<MirrorRunStatus option, string>
    /// "Jetzt spiegeln" gedrueckt: setzt den Hinweis, stoesst den Lauf an.
    | StartMirror
    | RunMirror
    | MirrorFinished of Result<MirrorRunSummary, string>

/// Was die Seite nach oben meldet.
type ExternalMsg =
    | NoOp
    | ShowToast of string * ToastType

/// Die Zusammenfassung eines Spiegel-Laufs als Satz.
let mirrorSummaryText (summary: MirrorRunSummary) : string =
    $"Spiegel-Lauf fertig: {summary.NewEntries} neu, {summary.ChangedEntries} geändert, {summary.DeletedEntries} gelöscht"
