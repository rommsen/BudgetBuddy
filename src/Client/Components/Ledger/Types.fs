/// Modell und Nachrichten der Kassenbuch-Seite (ledger-q5w9c, seitenweise Liste und
/// Kontostaende-Leiste ledger-b1xwe).
///
/// Die Seite liest nur: Kontostaende, Buchungen seitenweise (ein Konto oder alle),
/// die Historie einer Buchung und den Status des letzten Spiegel-Laufs. Geschrieben
/// wird über den manuellen Auslöser "Jetzt spiegeln" (ADR 0014, Punkt 3) und über das
/// Frische-Kriterium (ADR 0014, Punkt 2, ledger-h4p6c): lädt die Seite einen zu alten
/// Lauf-Status (Seitenaufruf oder PWA-Vordergrund), stößt sie denselben Hintergrund-
/// Lauf an, den auch "Jetzt spiegeln" nutzt. Der Lauf nach Push gehört nicht hierher:
/// er passiert serverseitig (`Ledger.CommandHandlers.triggerMirrorInBackground`), ohne
/// dass diese Seite geöffnet sein muss.
module Components.Ledger.Types

open System
open Shared.Domain
open Types

/// Wie viele Buchungen eine Seite traegt (AC5, ledger-b1xwe). Ein Modul-Wert statt
/// eines Literals: `RunLoadEntriesPage` und die Tests teilen sich diese eine Zahl.
let pageSize = 100

/// Der Konto-Filter der Liste: "Alle Konten" ist ein eigener Eintrag, kein Schalter
/// (Entscheidung bei der Verfeinerung, siehe Task-Notes) — er traegt die Summe aller
/// Konten und steht immer zuerst in der Kontostaende-Leiste.
type AccountFilter =
    | AllAccounts
    | OneAccount of Guid

type Model = {
    /// Die Konten samt Vorauswahl des Servers.
    Accounts: RemoteData<LedgerAccountsView>

    /// Die Kontostaende aller Konten in einem Aufruf (ledger-b1xwe AC2). Unabhaengig
    /// von `Entries`: sie kommen nie aus der geladenen Liste.
    Balances: RemoteData<LedgerAccountBalanceView list>

    /// Der gerade aktive Filter. `None`, solange nichts geladen ist oder das
    /// Kassenbuch leer ist (kein Konto zum Filtern).
    SelectedFilter: AccountFilter option

    /// Die bereits geladenen Buchungen des aktiven Filters, neueste zuerst, ueber
    /// alle bisher geladenen Seiten hinweg zusammengefuehrt. In YNAB geloeschte
    /// Buchungen sind enthalten und werden gekennzeichnet, nie herausgefiltert.
    Entries: RemoteData<LedgerEntryView list>

    /// Der Seitenzeiger der naechsten zu ladenden Seite (0-basiert).
    NextPage: int

    /// Die Gesamtzahl der Treffer des aktiven Filters, wie die zuletzt geladene Seite
    /// sie gemeldet hat.
    TotalCount: int

    /// Gibt es noch weitere Seiten zu laden? Reine Ableitung aus geladener Anzahl
    /// gegen `TotalCount`, nach jeder Seite neu gesetzt.
    HasMoreEntries: bool

    /// Zeigt die Liste in YNAB gelöschte Buchungen mit (ledger-p3n8k)? Standardmäßig
    /// `false`: die Liste blendet sie aus, ihre Summe entspricht dann dem
    /// Kontostand. Geht in die nächste `getLedgerEntriesPage`-Abfrage ein.
    IncludeDeleted: bool

    /// Die Zahl der in YNAB gelöschten Buchungen im aktiven Filter, wie die zuletzt
    /// geladene Seite sie gemeldet hat (unabhängig von `IncludeDeleted`, AC2).
    DeletedCount: int

    /// Zeigt die Liste nur zu prüfende Buchungen (ADR 0032)? Standardmäßig `false`.
    /// Geht in die nächste `getLedgerEntriesPage`-Abfrage ein, wie `IncludeDeleted`.
    OnlyNeedsReview: bool

    /// Die Zahl der zu prüfenden Buchungen im aktiven Filter, wie die zuletzt
    /// geladene Seite sie gemeldet hat (unabhängig von `OnlyNeedsReview`).
    NeedsReviewCount: int

    /// Laeuft gerade das Nachladen einer weiteren Seite? Anders als `Entries = Loading`
    /// (Erstladen): waehrend des Nachladens bleibt die bisherige Liste stehen, nur ein
    /// Skeleton steht am Ende (AC5).
    LoadingMore: bool

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
/// Jeder Ausloeser, der den Fable.Remoting-Proxy anfasst, ist absichtlich in zwei
/// Nachrichten geteilt: eine **reine**, die nur den Zustand setzt, und eine
/// **unreine**, die den Aufruf absetzt. Damit ist der interessante Uebergang
/// testbar, ohne den Proxy anzufassen, der unter .NET nicht existiert.
type Msg =
    // Konten
    | LoadAccounts
    | AccountsLoaded of Result<LedgerAccountsView, string>

    // Kontostaende
    | LoadBalances
    | BalancesLoaded of Result<LedgerAccountBalanceView list, string>

    // Konto-Filter
    | SelectFilter of AccountFilter

    // Buchungen, seitenweise
    | LoadEntries
    | RunLoadEntriesPage of page: int
    | EntriesPageLoaded of page: int * Result<LedgerEntryPage, string>
    /// Der Beobachter am Listenende ist sichtbar geworden: naechste Seite nachladen,
    /// wenn gerade keine laeuft und es noch etwas zu holen gibt.
    | LoadMoreEntries
    /// Der Schalter "gelöschte Buchungen zeigen" wurde betätigt (ledger-p3n8k):
    /// kippt `IncludeDeleted` und lädt die Liste ab Seite 0 neu.
    | ToggleIncludeDeleted
    /// Der Chip "Zu prüfen (n)" wurde angetippt (ADR 0032): kippt `OnlyNeedsReview`
    /// und lädt die Liste ab Seite 0 neu. Derselbe rein/unrein-Schnitt wie
    /// `ToggleIncludeDeleted` (ADR 0020): der Proxy-Aufruf steckt hinter
    /// `RunLoadEntriesPage`, nicht in diesem Zweig.
    | ToggleOnlyNeedsReview

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

/// Wie lange ein Spiegel-Lauf gilt, bevor er beim Seitenaufruf oder beim PWA-Vordergrund
/// einen neuen Hintergrund-Lauf auslöst (ADR 0014, Frische-Kriterium).
let freshnessThreshold = TimeSpan.FromMinutes 5.0

/// Reine Entscheidung: ist der letzte Lauf älter als die Frische-Schwelle, oder gab es
/// noch nie einen? `now` kommt von außen (der Aufrufer trägt `DateTime.UtcNow`), damit
/// diese Funktion ohne Uhr testbar bleibt.
let isMirrorStale (now: DateTime) (lastRun: MirrorRunStatus option) : bool =
    match lastRun with
    | None -> true
    | Some status -> now - status.RanAt > freshnessThreshold
