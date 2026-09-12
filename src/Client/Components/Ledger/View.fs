/// Die Kassenbuch-Seite (ledger-q5w9c, seitenweise Liste und Kontostaende-Leiste
/// ledger-b1xwe): Kontostaende oben, darunter die Buchungsliste eines Kontos oder
/// aller Konten, mit Herkunfts-Badge, Historie je Buchung und dem manuellen
/// "Jetzt spiegeln".
///
/// Ausschliesslich Design-System-Komponenten und Tokens (Styleguide-Gate
/// `design-system-001`); keine Inline-Farben, kein selbstgebautes Overlay — die
/// Historie liegt im `BottomSheet` des DS, das die Visual-Viewport- und
/// Click-Commit-Mechanik aus ADR 0005 bereits mitbringt.
module Components.Ledger.View

open System
open Fable.Core
open Feliz
open Shared.Domain
open Types
open Components.Ledger.Types
open Client.DesignSystem

// ============================================
// Formatierung
// ============================================

let private de = System.Globalization.CultureInfo("de-DE")

let private formatDate (d: DateTime) = d.ToString("dd.MM.yyyy", de)

let private formatDateTime (d: DateTime) = d.ToLocalTime().ToString("dd.MM.yyyy HH:mm", de)

let private formatMoneyText (m: Money) = $"""{m.Amount.ToString("N2", de)} {m.Currency}"""

// ============================================
// Reine Bausteine (unit-testbar ohne DOM, siehe LedgerViewTests)
// ============================================

/// Ein kurzes Kuerzel aus dem Kontonamen fuer die Gesamtsicht (AC4): das erste Wort,
/// bis zu vier Zeichen, gross geschrieben. Es gibt kein eigenes Kuerzel-Feld im
/// Modell — dieses Kuerzel ist eine Anzeige-Ableitung der Seite, kein Fachbegriff.
let accountAbbreviation (name: string) : string =
    let trimmed = name.Trim()
    if trimmed = "" then
        "?"
    else
        let firstWord =
            trimmed.Split([| ' ' |], StringSplitOptions.RemoveEmptyEntries)
            |> Array.tryHead
            |> Option.defaultValue trimmed
        firstWord.ToUpperInvariant().Substring(0, min 4 firstWord.Length)

/// Fasst aufeinanderfolgende Buchungen desselben Datums zu einer Gruppe zusammen, in
/// der Reihenfolge der Liste (neueste zuerst) — die Datums-Gruppenkoepfe der
/// Handy-Ansicht (AC6). Bewusst kein globales `List.groupBy`: das wuerde gleiche
/// Datumswerte zusammenziehen, auch wenn sie in der Liste nicht benachbart sind, und
/// die Sortierung der Server-Seite (neueste zuerst) verlassen.
let groupByDate (entries: LedgerEntryView list) : (DateTime * LedgerEntryView list) list =
    entries
    |> List.fold
        (fun acc entry ->
            match acc with
            | (date, items) :: rest when date = entry.Date -> (date, entry :: items) :: rest
            | _ -> (entry.Date, [ entry ]) :: acc)
        []
    |> List.rev
    |> List.map (fun (date, items) -> date, List.rev items)

/// Eine Karte der Kontostaende-Leiste: ein Filter (Konto oder "Alle Konten"), ihr
/// Anzeigename und ihr Stand.
type AccountCard = {
    Filter: AccountFilter
    Label: string
    Balance: Money
}

/// Baut die Karten der Kontostaende-Leiste (AC3): "Alle Konten" steht immer zuerst
/// und traegt die Summe aller Konten, danach jedes Konto mit seinem eigenen Stand.
/// Ein Konto ohne (noch geladenen) Stand zeigt Null statt eines Fehlers.
let buildAccountCards (accounts: LedgerAccountView list) (balances: LedgerAccountBalanceView list) : AccountCard list =
    let balanceOf (accountId: Guid) =
        balances
        |> List.tryFind (fun b -> b.AccountId = accountId)
        |> Option.map (fun b -> b.Balance)
        |> Option.defaultValue { Amount = 0m; Currency = "EUR" }

    let total = sumMoney "EUR" (balances |> List.map (fun b -> b.Balance))

    let accountCards =
        accounts
        |> List.map (fun a -> { Filter = OneAccount a.Id; Label = a.Name; Balance = balanceOf a.Id })

    { Filter = AllAccounts; Label = "Alle Konten"; Balance = total } :: accountCards

// ============================================
// Bausteine
// ============================================

/// Die Herkunft einer Buchung als Badge. Der Spiegel ist die haeufigste Herkunft und
/// bleibt deshalb ruhig (Info); die uebrigen Herkuenfte sind selten und duerfen
/// auffallen.
let private originBadge (origin: LedgerOrigin) =
    let variant =
        match origin with
        | MirroredFromYnab -> Badge.Info
        | ImportedFromComdirect -> Badge.Neutral
        | DecidedByRule -> Badge.Purple
        | DecidedInBb -> Badge.Success
        | AddedViaQuickAdd -> Badge.Orange
        | MatchedByOrderId -> Badge.Warning

    Badge.view {
        Badge.defaultProps with
            Text = originLabel origin
            Variant = variant
            Size = Badge.Small
    }

/// Kennzeichen einer in YNAB geloeschten Buchung. Sie bleibt stehen, zaehlt aber
/// nicht mehr zum Kontostand (ADR 0013).
let private deletedBadge =
    Badge.view {
        Badge.defaultProps with
            Text = "in YNAB gelöscht"
            Variant = Badge.Error
            Size = Badge.Small
    }

/// Das Konto-Kuerzel einer Zeile in der Gesamtsicht (AC4).
let private accountBadge (abbreviation: string) =
    Badge.view {
        Badge.defaultProps with
            Text = abbreviation
            Variant = Badge.Neutral
            Size = Badge.Small
    }

/// Eine Zeile der Buchungsliste am Handy. `accountAbbreviationOf` ist nur in der
/// Gesamtsicht gesetzt (AC4): das Konto steht dann als Kuerzel-Badge in der Zeile,
/// in der Konto-Sicht fehlt es ganz.
let private entryRow
    (dispatch: Msg -> unit)
    (accountAbbreviationOf: (Guid -> string) option)
    (entry: LedgerEntryView)
    =
    Html.button [
        prop.key (string entry.Id)
        prop.className (
            "w-full text-left rounded-lg px-3 py-3 transition-colors touch-manipulation " +
            // `hover:bg-surface-hover` steht bewusst als Literal: Tailwind scannt den
            // Quelltext nach ganzen Klassennamen, eine zusammengesetzte Variante
            // (`hover:` + Token) fiele beim Scan durch und wuerde nie erzeugt.
            $"{Tokens.Backgrounds.dark} hover:bg-surface-hover " +
            $"border {Tokens.Borders.subtle}"
        )
        prop.onClick (fun _ -> dispatch (OpenHistory entry))
        prop.children [
            Primitives.HStack.spaceBetween [
                Html.div [
                    prop.className "min-w-0 flex-1"
                    prop.children [
                        Html.div [
                            prop.className $"{Tokens.FontSizes.body} {Tokens.Colors.textPrimary} truncate"
                            prop.text (entry.Payee |> Option.defaultValue "ohne Payee")
                        ]
                        Html.div [
                            prop.className $"{Tokens.FontSizes.xs} {Tokens.Colors.textMuted} {Tokens.Fonts.mono} mt-0.5"
                            prop.text (
                                match entry.CategoryName with
                                | Some name -> $"{formatDate entry.Date} · {name}"
                                | None when not (List.isEmpty entry.Splits) ->
                                    $"{formatDate entry.Date} · {List.length entry.Splits} Split-Zeilen"
                                | None -> $"{formatDate entry.Date} · ohne Kategorie"
                            )
                        ]
                        if not (String.IsNullOrWhiteSpace(entry.Memo |> Option.defaultValue "")) then
                            Html.div [
                                prop.className $"{Tokens.FontSizes.xs} {Tokens.Colors.textMuted} mt-0.5 truncate"
                                prop.text (entry.Memo |> Option.defaultValue "")
                            ]
                        Html.div [
                            prop.className "flex flex-wrap items-center gap-1.5 mt-1.5"
                            prop.children [
                                match accountAbbreviationOf with
                                | Some abbreviationOf -> accountBadge (abbreviationOf entry.AccountId)
                                | None -> ()
                                originBadge entry.Origin
                                if entry.DeletedInYnab then deletedBadge
                            ]
                        ]
                    ]
                ]
                Html.div [
                    prop.className "shrink-0 pl-3"
                    prop.children [
                        Money.transactionWithCurrency entry.Amount.Amount entry.Amount.Currency
                    ]
                ]
            ]
        ]
    ]

/// Der Gruppenkopf eines Datums in der Handy-Liste (AC6).
let private dateGroupHeader (date: DateTime) =
    Html.div [
        prop.className $"{Tokens.FontSizes.xs} {Tokens.Colors.textMuted} {Tokens.Fonts.mono} uppercase tracking-wider px-3 pt-3 pb-1"
        prop.text (formatDate date)
    ]

/// Eine Zeile der Buchungsliste am PC, als Tabellenzeile mit denselben Feldern.
let private entryTableRow
    (dispatch: Msg -> unit)
    (showAccountColumn: bool)
    (accountAbbreviationOf: Guid -> string)
    (entry: LedgerEntryView)
    =
    Table.trClickable
        (fun () -> dispatch (OpenHistory entry))
        [
            Table.td (formatDate entry.Date)
            if showAccountColumn then Table.td (accountAbbreviationOf entry.AccountId)
            Table.td (entry.Payee |> Option.defaultValue "ohne Payee")
            Table.td (
                match entry.CategoryName with
                | Some name -> name
                | None when not (List.isEmpty entry.Splits) -> $"{List.length entry.Splits} Split-Zeilen"
                | None -> "ohne Kategorie"
            )
            Table.td (entry.Memo |> Option.defaultValue "")
            Table.tdRight (formatMoneyText entry.Amount)
            Table.tdContent [
                Html.div [
                    prop.className "flex flex-wrap items-center gap-1.5"
                    prop.children [
                        originBadge entry.Origin
                        if entry.DeletedInYnab then deletedBadge
                    ]
                ]
            ]
        ]

/// Die Kontostaende-Leiste (AC3): Handy waagerecht scrollbar, PC linke Spalte.
/// "Alle Konten" ist immer der erste Eintrag, das aktive Konto ist hervorgehoben.
let private accountCardView (dispatch: Msg -> unit) (selected: AccountFilter option) (card: AccountCard) =
    Stats.view {
        Stats.defaultProps with
            Label = card.Label
            Value = formatMoneyText card.Balance
            Size = Stats.Compact
            IsActive = selected = Some card.Filter
            OnClick = Some (fun () -> dispatch (SelectFilter card.Filter))
    }

let private accountStrip
    (dispatch: Msg -> unit)
    (accounts: LedgerAccountView list)
    (balances: LedgerAccountBalanceView list)
    (selected: AccountFilter option)
    =
    if List.isEmpty accounts then
        Html.none
    else
        let cards = buildAccountCards accounts balances
        Html.div [
            prop.className "md:w-56 md:shrink-0"
            prop.children [
                Primitives.Responsive.mobileOnlyFlex [
                    Primitives.ScrollContainer.horizontal [
                        Html.div [
                            prop.className "flex gap-3 pb-1"
                            prop.children (
                                cards
                                |> List.map (fun card ->
                                    Html.div [
                                        prop.key (string card.Label)
                                        prop.className "min-w-[9.5rem]"
                                        prop.children [ accountCardView dispatch selected card ]
                                    ])
                            )
                        ]
                    ]
                ]
                Primitives.Responsive.desktopOnly [
                    Primitives.Stack.sm (cards |> List.map (accountCardView dispatch selected))
                ]
            ]
        ]

/// Der Status des letzten Spiegel-Laufs als Unterzeile der Seite. Ein Fehler steht
/// hier genauso sichtbar wie ein Erfolg (ADR 0014).
let private lastRunSubtitle (status: RemoteData<MirrorRunStatus option>) : string option =
    match status with
    | Success (Some run) ->
        let outcome =
            match run.Outcome with
            | MirrorRunSucceeded summary ->
                $"{summary.NewEntries} neu, {summary.ChangedEntries} geändert, {summary.DeletedEntries} gelöscht"
            | MirrorRunFailed message -> $"gescheitert: {message}"
        Some $"Letzter Spiegel: {formatDateTime run.RanAt} — {outcome}"
    | Success None -> Some "Noch kein Spiegel-Lauf."
    | Failure message -> Some $"Lauf-Status nicht lesbar: {message}"
    | Loading | NotAsked -> None

// ============================================
// Historie (BottomSheet nach ADR 0005)
// ============================================

let private historyLine (line: LedgerHistoryLine) =
    Html.div [
        prop.key (string line.Sequence)
        prop.className $"py-3 border-b {Tokens.Borders.subtle} last:border-b-0"
        prop.children [
            Primitives.HStack.spaceBetween [
                Html.span [
                    prop.className $"{Tokens.FontSizes.xs} {Tokens.Colors.textMuted} {Tokens.Fonts.mono}"
                    prop.text (formatDateTime line.OccurredAt)
                ]
                originBadge line.Origin
            ]
            Html.p [
                prop.className $"{Tokens.FontSizes.body} {Tokens.Colors.textPrimary} mt-1"
                prop.text line.Description
            ]
        ]
    ]

let private historySheet (model: Model) (dispatch: Msg -> unit) =
    let title =
        model.HistoryFor
        |> Option.bind (fun e -> e.Payee)
        |> Option.defaultValue "Buchung"

    let subtitle =
        model.HistoryFor
        |> Option.map (fun e -> $"{formatDate e.Date} · Historie")

    BottomSheet.view
        {
            IsOpen = model.HistoryFor.IsSome
            OnClose = fun () -> dispatch CloseHistory
            Title = title
            Subtitle = subtitle
            Footer = None
        }
        [
            match model.History with
            | NotAsked -> Html.none
            | Loading -> Loading.centered "Historie wird geladen…"
            | Failure message ->
                ErrorDisplay.cardCompact
                    $"Historie konnte nicht geladen werden: {message}"
                    (model.HistoryFor |> Option.map (fun e -> fun () -> dispatch (LoadHistory e.Id)))
            | Success [] ->
                Html.p [
                    prop.className $"{Tokens.FontSizes.body} {Tokens.Colors.textMuted} py-6 text-center"
                    prop.text "Für diese Buchung ist noch nichts festgehalten."
                ]
            | Success lines ->
                Html.div [
                    prop.children (lines |> List.map historyLine)
                ]
        ]

// ============================================
// Nachladen beim Scrollen (AC5)
// ============================================
// Minimale IntersectionObserver-Bindung (nicht Teil von Fable.Browser.Dom, analog zu
// `Viewport.fs`s visualViewport-Bindung): sobald der Beobachter am Listenende
// sichtbar wird, ruft er `onIntersect`. Kein `root`-Parameter — die Seite scrollt im
// Fenster, nicht in einem eigenen Container.
[<Emit(
    "(function(el, cb) { var o = new IntersectionObserver(function(entries) { entries.forEach(function(e){ if (e.isIntersecting) cb(); }); }); o.observe(el); return function() { o.disconnect(); }; })($0, $1)"
)>]
let private observeIntersection (element: Browser.Types.HTMLElement) (onIntersect: unit -> unit) : (unit -> unit) =
    jsNative

/// Ein unsichtbarer Beobachter am Listenende. `active` steuert, ob gerade beobachtet
/// wird: waehrend eines laufenden Nachladens oder ohne weitere Seiten wird nicht
/// beobachtet, damit kein zweiter Aufruf ausgeloest wird, bevor der erste fertig ist.
[<ReactComponent>]
let private ScrollSentinel (onReachEnd: unit -> unit) (active: bool) =
    let elementRef = React.useRef<Browser.Types.HTMLElement option> None

    React.useEffect (
        (fun () ->
            match elementRef.current with
            | Some element when active ->
                let disconnect = observeIntersection element onReachEnd
                { new IDisposable with
                    member _.Dispose() = disconnect () }
            | _ ->
                { new IDisposable with
                    member _.Dispose() = () }),
        [| box active |]
    )

    Html.div [ prop.ref elementRef; prop.className "h-1" ]

// ============================================
// Seite
// ============================================

let private entryListMobile
    (dispatch: Msg -> unit)
    (accountAbbreviationOf: (Guid -> string) option)
    (entries: LedgerEntryView list)
    =
    Primitives.Responsive.mobileOnly [
        Html.div [
            prop.children (
                groupByDate entries
                |> List.collect (fun (date, group) ->
                    dateGroupHeader date :: (group |> List.map (entryRow dispatch accountAbbreviationOf)))
            )
        ]
    ]

let private entryListDesktop
    (dispatch: Msg -> unit)
    (showAccountColumn: bool)
    (accountAbbreviationOf: Guid -> string)
    (entries: LedgerEntryView list)
    =
    Primitives.Responsive.desktopOnly [
        Table.view
            { Table.defaultTableProps with Sticky = true }
            [
                Table.thead [
                    Table.th "Datum"
                    if showAccountColumn then Table.th "Konto"
                    Table.th "Payee"
                    Table.th "Kategorie"
                    Table.th "Memo"
                    Table.thRight "Betrag"
                    Table.th "Status"
                ]
                Table.tbody (
                    entries |> List.map (entryTableRow dispatch showAccountColumn accountAbbreviationOf)
                )
            ]
    ]

let private entryList
    (dispatch: Msg -> unit)
    (accounts: LedgerAccountView list)
    (isAllAccounts: bool)
    (model: Model)
    =
    let abbreviationOf =
        let byId =
            accounts |> List.map (fun a -> a.Id, accountAbbreviation a.Name) |> Map.ofList
        fun (id: Guid) -> byId |> Map.tryFind id |> Option.defaultValue "?"

    match model.Entries with
    | NotAsked ->
        Card.emptyState
            (Icons.banknotes Icons.XL Icons.Default)
            "Noch kein Konto"
            "Sobald ein Spiegel-Lauf gelaufen ist, stehen hier die Konten und ihre Buchungen."
            (Some (Button.primary "Jetzt spiegeln" (fun () -> dispatch StartMirror)))

    | Loading ->
        Html.div [
            Primitives.Responsive.mobileOnly [ Loading.txListSkeleton 6 ]
            Primitives.Responsive.desktopOnly [ Loading.tableSkeleton 6 6 ]
        ]

    | Failure message ->
        ErrorDisplay.cardWithTitle
            "Buchungen konnten nicht geladen werden"
            message
            (Some (fun () -> dispatch LoadEntries))

    | Success [] ->
        Card.emptyState
            (Icons.banknotes Icons.XL Icons.Default)
            "Keine Buchungen"
            "Für diesen Filter steht noch nichts im Kassenbuch."
            (Some (Button.secondary "Jetzt spiegeln" (fun () -> dispatch StartMirror)))

    | Success entries ->
        Html.div [
            entryListMobile dispatch (if isAllAccounts then Some abbreviationOf else None) entries
            entryListDesktop dispatch isAllAccounts abbreviationOf entries

            if model.LoadingMore then
                Html.div [
                    prop.className "mt-2"
                    prop.children [
                        Primitives.Responsive.mobileOnly [ Loading.txListSkeleton 2 ]
                        Primitives.Responsive.desktopOnly [ Loading.tableSkeleton (if isAllAccounts then 7 else 6) 2 ]
                    ]
                ]

            if model.HasMoreEntries then
                ScrollSentinel (fun () -> dispatch LoadMoreEntries) (model.HasMoreEntries && not model.LoadingMore)
        ]

let view (model: Model) (dispatch: Msg -> unit) =
    let accounts = model.Accounts |> RemoteData.toOption |> Option.map (fun a -> a.Accounts) |> Option.defaultValue []
    let balances = model.Balances |> RemoteData.toOption |> Option.defaultValue []
    let isAllAccounts = model.SelectedFilter = Some AllAccounts

    Primitives.Container.view [
        PageHeader.gradientWithActions
            "Kassenbuch"
            (lastRunSubtitle model.LastMirrorRun)
            [
                Button.primaryLoading
                    (if model.MirrorRunning then "Spiegel läuft…" else "Jetzt spiegeln")
                    model.MirrorRunning
                    (fun () -> dispatch StartMirror)
            ]

        match model.Accounts with
        | Failure message ->
            ErrorDisplay.cardWithTitle "Konten konnten nicht geladen werden" message (Some (fun () -> dispatch LoadAccounts))
        | Loading -> Loading.centered "Kassenbuch wird geöffnet…"
        | NotAsked | Success _ ->
            Html.div [
                prop.className "flex flex-col md:flex-row md:items-start md:gap-6 gap-4"
                prop.children [
                    accountStrip dispatch accounts balances model.SelectedFilter

                    Html.div [
                        prop.className "flex-1 min-w-0"
                        prop.children [
                            Primitives.Stack.md [
                                if model.MirrorRunning then
                                    // Der Lauf blockiert die Liste nicht: sie bleibt
                                    // stehen, der Hinweis steht daneben (ADR 0014).
                                    Loading.inlineWithText "Spiegel läuft — die Liste bleibt so lange stehen."

                                entryList dispatch accounts isAllAccounts model
                            ]
                        ]
                    ]
                ]
            ]

        historySheet model dispatch
    ]
