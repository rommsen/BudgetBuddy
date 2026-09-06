/// Die Kassenbuch-Seite (ledger-q5w9c): Buchungen eines Kontos mit Herkunfts-Badge,
/// Kontostand, Historie je Buchung und dem manuellen "Jetzt spiegeln".
///
/// Ausschliesslich Design-System-Komponenten und Tokens (Styleguide-Gate
/// `design-system-001`); keine Inline-Farben, kein selbstgebautes Overlay — die
/// Historie liegt im `BottomSheet` des DS, das die Visual-Viewport- und
/// Click-Commit-Mechanik aus ADR 0005 bereits mitbringt.
module Components.Ledger.View

open System
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

/// Eine Zeile der Buchungsliste. Tippen oeffnet die Historie.
let private entryRow (dispatch: Msg -> unit) (entry: LedgerEntryView) =
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
                        Html.div [
                            prop.className "flex flex-wrap items-center gap-1.5 mt-1.5"
                            prop.children [
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

/// Die Konto-Auswahl. Ein einzelnes Konto braucht keine Auswahl, nur seinen Namen.
let private accountPicker (dispatch: Msg -> unit) (accounts: LedgerAccountView list) (selected: Guid option) =
    match accounts with
    | [] -> Html.none
    | [ single ] ->
        Input.groupSimple
            "Konto"
            (Html.div [
                prop.className $"{Tokens.FontSizes.body} {Tokens.Colors.textPrimary}"
                prop.text single.Name
            ])
    | many ->
        Input.groupSimple
            "Konto"
            (Input.selectSimple
                (selected |> Option.map string |> Option.defaultValue "")
                (fun value ->
                    match Guid.TryParse value with
                    | true, id -> dispatch (SelectAccount id)
                    | false, _ -> ())
                (many |> List.map (fun a -> string a.Id, a.Name)))

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
// Seite
// ============================================

let private entryList (model: Model) (dispatch: Msg -> unit) =
    match model.Entries with
    | NotAsked ->
        Card.emptyState
            (Icons.banknotes Icons.XL Icons.Default)
            "Noch kein Konto"
            "Sobald ein Spiegel-Lauf gelaufen ist, stehen hier die Konten und ihre Buchungen."
            (Some (Button.primary "Jetzt spiegeln" (fun () -> dispatch StartMirror)))

    | Loading -> Loading.txListSkeleton 6

    | Failure message ->
        ErrorDisplay.cardWithTitle
            "Buchungen konnten nicht geladen werden"
            message
            (Some (fun () -> dispatch LoadEntries))

    | Success [] ->
        Card.emptyState
            (Icons.banknotes Icons.XL Icons.Default)
            "Keine Buchungen"
            "Für dieses Konto steht noch nichts im Kassenbuch."
            (Some (Button.secondary "Jetzt spiegeln" (fun () -> dispatch StartMirror)))

    | Success entries ->
        Primitives.Stack.sm (entries |> List.map (entryRow dispatch))

let view (model: Model) (dispatch: Msg -> unit) =
    let accounts = model.Accounts |> RemoteData.toOption |> Option.map (fun a -> a.Accounts) |> Option.defaultValue []

    let balance =
        model.Entries
        |> RemoteData.toOption
        |> Option.map (ledgerBalance "EUR")

    Primitives.Container.medium [
        PageHeader.gradientWithActions
            "Kassenbuch"
            (lastRunSubtitle model.LastMirrorRun)
            [
                Button.primaryLoading
                    (if model.MirrorRunning then "Spiegel läuft…" else "Jetzt spiegeln")
                    model.MirrorRunning
                    (fun () -> dispatch StartMirror)
            ]

        Primitives.Stack.md [
            match model.Accounts with
            | Failure message ->
                ErrorDisplay.cardWithTitle
                    "Konten konnten nicht geladen werden"
                    message
                    (Some (fun () -> dispatch LoadAccounts))
            | Loading -> Loading.centered "Kassenbuch wird geöffnet…"
            | NotAsked | Success _ ->
                Card.standard [
                    Card.body [
                        Primitives.Stack.md [
                            accountPicker dispatch accounts model.SelectedAccountId

                            match balance with
                            | Some b -> Money.withLabel "Kontostand" b.Amount b.Currency
                            | None -> Html.none

                            if model.MirrorRunning then
                                // Der Lauf blockiert die Liste nicht: sie bleibt
                                // stehen, der Hinweis steht daneben (ADR 0014).
                                Loading.inlineWithText "Spiegel läuft — die Liste bleibt so lange stehen."
                        ]
                    ]
                ]

                entryList model dispatch
        ]

        historySheet model dispatch
    ]
