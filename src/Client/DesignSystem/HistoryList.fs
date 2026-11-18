/// Die Historie eines Dings nach Styleguide §7.11 (ledger-zfj78, Prototyp Version 17 `.hist`, `.hi`, `.who`): je
/// Eintrag eine Zeile mit drei Spalten. Links der Zeitpunkt (`TT.MM. HH:MM`, Mono, gedämpft), in der Mitte die
/// Herkunft als neutrales Kürzel (Mono, Großbuchstaben, dünner Rand, ohne Farbe) und der Text, rechts der Betrag,
/// falls es einen gibt. Die Reihenfolge legt der Aufrufer fest (neueste oben). Buchung und Kategorie teilen diese
/// Form; Farbe für die Herkunft gibt es nicht.
module Client.DesignSystem.HistoryList

open Feliz
open Client.DesignSystem.Tokens

/// Ein Eintrag der Historie, schon als Text: `When` etwa „06.11. 18:40“, `Who` das Kürzel wie „ROMAN“ (der
/// Baustein setzt es nur in Großbuchstaben), `Amount` der fertig formatierte Betrag oder `None`.
type Item = {
    Key: string
    When: string
    Who: string
    Text: string
    Amount: string option
}

/// Das Kürzel der Herkunft (Prototyp `.who`): 10 px Mono in Großbuchstaben, gedämpft, dünner Rand, ohne Farbe.
let whoTag (who: string) : ReactElement =
    Html.span [
        prop.custom ("data-testid", "history-who")
        prop.className (
            $"mr-1.5 inline-block whitespace-nowrap rounded border {Borders.default'} px-1 align-baseline " +
            $"font-mono {FontSizes.micro} uppercase tracking-[0.06em] {Colors.textMuted}"
        )
        prop.text who
    ]

let private gridClass = "grid grid-cols-[auto_1fr_auto] items-baseline gap-2.5"

let private whenCell (text: string) =
    Html.span [
        prop.custom ("data-testid", "history-when")
        prop.className $"whitespace-nowrap font-mono {FontSizes.microPlus} {Colors.textMuted}"
        prop.text text
    ]

let private amountCell (amount: string option) =
    Html.span [
        prop.custom ("data-testid", "history-amount")
        prop.className $"whitespace-nowrap font-mono tabular-nums {Colors.textPrimary}"
        prop.text (amount |> Option.defaultValue "")
    ]

/// Der Zusatz unter dem Text (budgeting-yarty): eine eigene Zeile 12 px gedämpft, die Teile mit Abstand
/// nebeneinander, bei Platzmangel umbrochen. Ohne Teile steht nichts da.
let private noteLine (note: ReactElement list) =
    if List.isEmpty note then Html.none
    else
        Html.span [
            prop.custom ("data-testid", "history-note")
            prop.className $"mt-0.5 flex min-w-0 flex-wrap items-center gap-x-2 gap-y-1 {FontSizes.xs} {Colors.textMuted}"
            prop.children note
        ]

/// Kürzel und Text, darunter der Zusatz.
let private textCell (who: string) (text: string) (note: ReactElement list) =
    Html.span [
        prop.className $"min-w-0 break-words {Colors.textPrimary}"
        prop.children [
            whoTag who
            Html.span [ prop.custom ("data-testid", "history-text"); prop.text text ]
            noteLine note
        ]
    ]

/// Eine Zeile mit Zusatz (budgeting-yarty, Budget-Historie): wie `row`, unter dem Text eine Zeile mit dem, was die
/// Seite über §7.11 hinaus zeigt (etwa Monat und Stand eines Durchlaufs). Ohne Zusatz gleich `row`.
let rowWithNote (item: Item) (note: ReactElement list) : ReactElement =
    Html.li [
        prop.key item.Key
        prop.custom ("data-testid", "history-row")
        prop.className $"{gridClass} border-b {Borders.subtle} detail-line py-2 last:border-b-0 {FontSizes.sm}"
        prop.children [ whenCell item.When; textCell item.Who item.Text note; amountCell item.Amount ]
    ]

/// Eine Zeile der Historie (Prototyp `.hi`): Zeitpunkt, Kürzel und Text, Betrag; Linie unten außer bei der letzten.
let row (item: Item) : ReactElement = rowWithNote item []

/// Eine Zeile, die einen Block darunter aufklappt (budgeting-yarty, Durchlauf Targets anwenden): die ganze Zeile ist
/// ein Knopf mit `aria-expanded` und `aria-controls`, der Chevron (`text-muted`, dreht sich beim Öffnen) steht am
/// Ende des Zusatzes, damit die Beträge aller Zeilen bündig bleiben. Der Block (`subRow`) steht immer im DOM, zu ist
/// er `hidden`.
let disclosureRow
    (item: Item)
    (note: ReactElement list)
    (controlsId: string)
    (isOpen: bool)
    (onToggle: unit -> unit)
    (block: ReactElement list)
    : ReactElement =
    let turned = if isOpen then "rotate-180" else "rotate-0"
    let chevron =
        Html.span [
            prop.custom ("data-testid", "disclosure-chevron")
            prop.className $"ml-auto inline-flex shrink-0 {Transitions.fast} motion-reduce:transition-none {turned}"
            prop.children [ Icons.chevronDown Icons.SM Icons.Muted ]
        ]
    Html.li [
        prop.key item.Key
        prop.custom ("data-testid", "history-row")
        prop.className $"border-b {Borders.subtle} last:border-b-0 {FontSizes.sm}"
        prop.children [
            Html.button [
                prop.type' "button"
                prop.custom ("data-testid", "history-toggle")
                prop.ariaExpanded isOpen
                prop.ariaControls controlsId
                prop.className $"{gridClass} detail-line w-full py-2 text-left cursor-pointer touch-manipulation hover:bg-surface-hover"
                prop.onClick (fun _ -> onToggle ())
                prop.children [ whenCell item.When; textCell item.Who item.Text (note @ [ chevron ]); amountCell item.Amount ]
            ]
            Html.ol [
                prop.id controlsId
                prop.hidden (not isOpen)
                prop.className "flex flex-col pb-1"
                prop.children block
            ]
        ]
    ]

/// Eine Zeile im aufgeklappten Block einer `disclosureRow`: kein eigener Zeitpunkt und kein Kürzel. Der Text steht
/// unter dem Text der Kopfzeile (`alignWith` ist deren Zeitpunkt, unsichtbar als Platzhalter), der Betrag in Mono
/// rechts, darunter der Zusatz.
let subRow (key: string) (alignWith: string) (text: string) (amount: string) (note: ReactElement list) : ReactElement =
    Html.li [
        prop.key key
        prop.custom ("data-testid", "history-subrow")
        prop.className $"{gridClass} border-t {Borders.subtle} py-1.5 {FontSizes.sm}"
        prop.children [
            Html.span [
                prop.custom ("aria-hidden", true)
                prop.className $"invisible whitespace-nowrap font-mono {FontSizes.microPlus}"
                prop.text alignWith
            ]
            Html.span [
                prop.className $"min-w-0 break-words {Colors.textSecondary}"
                prop.children [ Html.span [ prop.custom ("data-testid", "history-text"); prop.text text ]; noteLine note ]
            ]
            amountCell (Some amount)
        ]
    ]

/// Fertige Zeilen (`row`, `rowWithNote`, `disclosureRow`) als Liste in der gegebenen Reihenfolge.
let listOf (testId: string) (rows: ReactElement list) : ReactElement =
    Html.ol [
        prop.custom ("data-testid", testId)
        prop.className "flex flex-col"
        prop.children rows
    ]

/// Die Liste der Einträge in der gegebenen Reihenfolge.
let list (testId: string) (items: Item list) : ReactElement = listOf testId (items |> List.map row)

/// Der Leerzustand der Historie: ein gedämpfter Satz.
let empty (text: string) : ReactElement =
    Html.p [
        prop.custom ("data-testid", "history-empty")
        prop.className $"py-3 {FontSizes.sm} {Colors.textMuted}"
        prop.text text
    ]
