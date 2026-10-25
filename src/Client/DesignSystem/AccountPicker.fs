/// Die Konto-Auswahl mit Einfachauswahl (ledger-pafkp, ADR 0005, ADR 0006): wählt das Übertrag-Ziel
/// einer Split-Zeile. Ein Inhalt, zwei Hüllen wie beim `CategoryPicker`: am PC ein Popover am
/// Auslöser, am Handy ein zweites Sheet über dem Detail (`BottomSheet.accountPickerLayered`).
/// Ein Klick auf eine Zeile übernimmt sofort (Click-Commit). Der Baustein kennt weder Kassenbuch-
/// Model noch -Msg: der Aufrufer liefert die Konten (offene Budget-Konten, `openOnBudgetAccounts`).
module Client.DesignSystem.AccountPicker

open System
open Feliz
open Shared.Domain
open Client.DesignSystem.Tokens

type AccountPickerProps = {
    /// Die wählbaren Konten, vom Aufrufer vorgefiltert
    Accounts: YnabAccount list
    OnSelect: YnabAccountId -> unit
    OnClose: unit -> unit
}

let searchPlaceholder = "Konto suchen…"
let noMatchesText = "Keine passenden Konten"
let sheetTitle = "Übertrag-Ziel wählen"

/// Die Konten, deren Name den Suchtext enthält (ohne Beachtung der Groß- und Kleinschreibung);
/// leerer Suchtext lässt alle durch.
let filterAccounts (search: string) (accounts: YnabAccount list) : YnabAccount list =
    if String.IsNullOrWhiteSpace search then
        accounts
    else
        let needle = search.Trim().ToLowerInvariant()
        accounts |> List.filter (fun a -> a.Name.ToLowerInvariant().Contains needle)

[<ReactComponent>]
let private Content (props: AccountPickerProps) =
    let search, setSearch = React.useState ""
    let shown = filterAccounts search props.Accounts
    Html.div [
        prop.custom ("data-testid", "account-picker")
        prop.className "flex flex-col gap-3"
        prop.children [
            PickerParts.searchField search setSearch searchPlaceholder
            Html.div [
                prop.className "flex max-h-72 flex-col overflow-y-auto"
                prop.children [
                    for account in shown do
                        let (YnabAccountId id) = account.Id
                        PickerParts.row
                            "px-1 py-2 min-h-11 cursor-pointer rounded-lg hover:bg-surface-hover"
                            [ prop.key (id.ToString())
                              prop.custom ("data-testid", "account-picker-row")
                              // Der Fokus bleibt im Suchfeld, bis der Klick ankommt.
                              prop.onMouseDown (fun e -> e.preventDefault ())
                              prop.onClick (fun _ -> props.OnSelect account.Id) ]
                            (PickerParts.name account.Name)
                            None
                    if List.isEmpty shown then
                        Html.div [ prop.className $"py-2 {ListRoles.muted}"; prop.text noMatchesText ]
                ]
            ]
        ]
    ]

/// Hülle für den PC: Popover im engen Wrapper des Seitenpanels. Der Aufrufer stellt den
/// Auslöser in einen `position:relative`-Wrapper (siehe `FilterPopover`).
let popoverInPanel (isOpen: bool) (props: AccountPickerProps) : ReactElement =
    FilterPopover.view
        { IsOpen = isOpen; OnClose = props.OnClose; WidthClassName = FilterPopover.panelWidthClass }
        [ Content props ]

/// Hülle für das Handy: zweites Sheet über dem Detail-Sheet.
let sheetLayered (isOpen: bool) (props: AccountPickerProps) : ReactElement =
    let options = props.Accounts |> List.map (fun a -> (let (YnabAccountId id) = a.Id in id.ToString()), a.Name)
    BottomSheet.accountPickerLayered
        isOpen
        sheetTitle
        options
        (fun id ->
            match Guid.TryParse id with
            | true, guid -> props.OnSelect (YnabAccountId guid)
            | _ -> props.OnClose ())
        props.OnClose
