/// Gemeinsame Bausteine der Kategorie-Auswahlen (design-system-zahv9, ADR 0049):
/// Zeile, Gruppenkopf, Suchfeld und Footer. `CategoryMultiSelect` (Popover, Mehrfach)
/// und `BottomSheet.categoryPicker` (Sheet, Einfach) bauen beide darauf auf. Das
/// Verhalten (Checkbox und Übernehmen, Tippen und Schließen, ADR-0005-Mechanik)
/// bleibt bei den Aufrufern.
module Client.DesignSystem.PickerParts

open Feliz
open Client.DesignSystem.Tokens

// ============================================
// Reine Klassenfunktionen und Texte
// ============================================

/// Zeile: einzeilig, Name links, Betrag rechts. Innenabstand setzt der Aufrufer.
let rowClass = "flex items-center justify-between gap-3 min-w-0"

/// Name in der Zeile: Größe und Gewicht der Listenrolle `primary`, einzeilig gekürzt
let nameClass = $"{ListRoles.primary} truncate min-w-0"

/// Betragsbereich: feste Breite, schrumpft nicht, rechtsbündig
let amountSlotClass = "shrink-0 w-24 text-right"

/// Gruppenkopf: Mono 10px, gesperrt, gedämpft
let groupHeaderClass = $"{ListRoles.groupHeader} pb-1"

/// Erscheint "Alle Kategorien" über der gegliederten Liste? Nur wenn darüber
/// "Vorgeschlagen" oder "Zuletzt verwendet" steht und keine Suche aktiv ist.
let showAllCategoriesHeader (hasSuggested: bool) (hasRecent: bool) (isSearching: bool) : bool =
    (hasSuggested || hasRecent) && not isSearching

/// Beschriftung des Übernehmen-Knopfs: die Zahl steht nur bei Auswahl
let applyLabel (selectedCount: int) : string =
    if selectedCount <= 0 then "Übernehmen" else $"Übernehmen ({selectedCount})"

/// Übernehmen: kompakter Primärknopf ohne Glow, einzeilig
let applyButtonClass =
    "inline-flex items-center justify-center whitespace-nowrap rounded-lg px-4 py-2 min-h-[36px] text-sm font-semibold text-white bg-neon-orange transition-colors hover:brightness-110 active:scale-[0.98]"

/// Übernehmen gesperrt (zum Beispiel bei ungültigem Datum): gedämpft, ohne Hover und
/// ohne Druckeffekt
let applyButtonDisabledClass =
    "inline-flex items-center justify-center whitespace-nowrap rounded-lg px-4 py-2 min-h-[36px] text-sm font-semibold text-white bg-neon-orange opacity-50 cursor-not-allowed"

/// Klasse des Übernehmen-Knopfs je Zustand
let applyButtonClassFor (disabled: bool) : string =
    if disabled then applyButtonDisabledClass else applyButtonClass

// ============================================
// Bausteine
// ============================================

/// Name einer Zeile
let name (text: string) : ReactElement =
    Html.span [
        prop.className nameClass
        prop.text text
    ]

/// Picker-Zeile. `nameElement` ist der Name (bei Mehrfachauswahl die Checkbox mit
/// Beschriftung), `available` der Verfügbar-Betrag. `extraClass` und `extraProps`
/// tragen Innenabstand und Verhalten des Aufrufers.
let row (extraClass: string) (extraProps: IReactProperty list) (nameElement: ReactElement) (available: (decimal * string) option) : ReactElement =
    Html.div [
        yield! extraProps
        prop.className $"{rowClass} {extraClass}"
        prop.children [
            nameElement
            match available with
            | Some (amount, currency) ->
                Html.span [
                    prop.className amountSlotClass
                    prop.children [ Money.available amount currency ]
                ]
            | None -> ()
        ]
    ]

/// Gruppenkopf
let groupHeader (text: string) : ReactElement =
    Html.div [
        prop.className groupHeaderClass
        prop.text text
    ]

/// Kompaktes Suchfeld des Popovers (Input.Small: 16px Handy, 14px PC, 36px hoch)
let searchField (value: string) (onChange: string -> unit) (placeholder: string) : ReactElement =
    Input.text { Input.textInputDefaults with
                    Value = value
                    OnChange = onChange
                    Placeholder = placeholder
                    Size = Input.Small }

/// Übernehmen-Knopf mit freier Beschriftung (Zahl nur bei Auswahl, siehe `applyLabel`)
let applyButton (selectedCount: int) (onApply: unit -> unit) : ReactElement =
    Html.button [
        prop.type' "button"
        prop.className applyButtonClass
        prop.onClick (fun _ -> onApply ())
        prop.text (applyLabel selectedCount)
    ]

/// Übernehmen mit Sperre: bei `disabled` ist der Knopf deaktiviert und tut nichts
let applyButtonWhen (disabled: bool) (selectedCount: int) (onApply: unit -> unit) : ReactElement =
    Html.button [
        prop.type' "button"
        prop.className (applyButtonClassFor disabled)
        prop.disabled disabled
        prop.onClick (fun _ -> if not disabled then onApply ())
        prop.text (applyLabel selectedCount)
    ]

/// Footer des Popovers: Zurücksetzen (Ghost) links, Übernehmen rechts
let footer (selectedCount: int) (onReset: unit -> unit) (onApply: unit -> unit) : ReactElement =
    Primitives.HStack.spaceBetween [
        Button.ghost "Zurücksetzen" onReset
        applyButton selectedCount onApply
    ]
