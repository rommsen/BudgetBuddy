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

/// Eine Zeile der Historie (Prototyp `.hi`): Zeitpunkt, Kürzel und Text, Betrag; Linie unten außer bei der letzten.
let row (item: Item) : ReactElement =
    Html.li [
        prop.key item.Key
        prop.custom ("data-testid", "history-row")
        prop.className (
            $"grid grid-cols-[auto_1fr_auto] items-baseline gap-2.5 border-b {Borders.subtle} detail-line py-2 last:border-b-0 {FontSizes.sm}"
        )
        prop.children [
            Html.span [
                prop.custom ("data-testid", "history-when")
                prop.className $"whitespace-nowrap font-mono {FontSizes.microPlus} {Colors.textMuted}"
                prop.text item.When
            ]
            Html.span [
                prop.className $"min-w-0 break-words {Colors.textPrimary}"
                prop.children [ whoTag item.Who; Html.span [ prop.text item.Text ] ]
            ]
            Html.span [
                prop.className $"whitespace-nowrap font-mono tabular-nums {Colors.textPrimary}"
                prop.text (item.Amount |> Option.defaultValue "")
            ]
        ]
    ]

/// Die Liste der Einträge in der gegebenen Reihenfolge.
let list (testId: string) (items: Item list) : ReactElement =
    Html.ol [
        prop.custom ("data-testid", testId)
        prop.className "flex flex-col"
        prop.children (items |> List.map row)
    ]

/// Der Leerzustand der Historie: ein gedämpfter Satz.
let empty (text: string) : ReactElement =
    Html.p [
        prop.custom ("data-testid", "history-empty")
        prop.className $"py-3 {FontSizes.sm} {Colors.textMuted}"
        prop.text text
    ]
