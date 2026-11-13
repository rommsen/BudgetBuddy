module Client.DesignSystem.Form

open Feliz
open Client.DesignSystem.Button
open Client.DesignSystem.Icons

/// Checks if a required field is filled
let isRequiredValid (value: string) =
    not (System.String.IsNullOrWhiteSpace value)

/// Returns list of missing required field labels
let getMissingFields (fields: (string * string) list) =
    fields
    |> List.filter (fun (_, value) -> not (isRequiredValid value))
    |> List.map fst

/// Hinweistext unter dem Absenden-Knopf, wenn Pflichtfelder fehlen
let missingFieldsText (missingFields: string list) : string =
    let fields = String.concat ", " missingFields
    $"Bitte ausfüllen: {fields}"

/// Der gemeinsame Kern der Absenden-Knöpfe: gesperrt, solange Pflichtfelder fehlen oder der Aufruf läuft, darunter
/// der gelbe Hinweis, was fehlt. `block`: der Knopf über die volle Breite, auch ab `md` (Raster statt Stapel).
let private submitWith
    (block: bool)
    (variant: ButtonVariant)
    (icon: ReactElement option)
    (text: string)
    (onClick: unit -> unit)
    (isLoading: bool)
    (requiredFields: (string * string) list)
    =
    let missingFields = getMissingFields requiredFields
    let isDisabled = isLoading || not (List.isEmpty missingFields)

    Html.div [
        prop.className (if block then "grid gap-2" else "space-y-2")
        prop.children [
            Button.view {
                Button.defaultProps with
                    Text = text
                    OnClick = onClick
                    Variant = variant
                    IsLoading = isLoading
                    IsDisabled = isDisabled
                    Icon = icon
                    FullWidth = block
            }
            if isDisabled && not isLoading && not (List.isEmpty missingFields) then
                Html.div [
                    prop.className "flex items-center gap-2 text-sm text-attention"
                    prop.children [
                        Icons.warning SM Warning
                        Html.span [ prop.text (missingFieldsText missingFields) ]
                    ]
                ]
        ]
    ]

/// Submit button with validation message below
/// Shows yellow warning when required fields are missing
let submitButton (text: string) (onClick: unit -> unit) (isLoading: bool) (requiredFields: (string * string) list) =
    submitWith false Button.Primary (Some (Icons.check SM Icons.Primary)) text onClick isLoading requiredFields

/// Wie `submitButton`, aber über die volle Breite und ohne Symbol: die rechte Hälfte eines Paars „Abbrechen“ und
/// Speichern im Fuß von Panel und Sheet (Prototyp Version 17 `.btnrow`, Regel-Formular, categorization-n3sxw). Das
/// Raster streckt den Knopf auch ab `md`, wo `FullWidth` zu `w-auto` wird.
let submitButtonBlock (text: string) (onClick: unit -> unit) (isLoading: bool) (requiredFields: (string * string) list) =
    submitWith true Button.Primary None text onClick isLoading requiredFields

/// Submit button with custom icon
let submitButtonWithIcon (text: string) (icon: ReactElement) (onClick: unit -> unit) (isLoading: bool) (requiredFields: (string * string) list) =
    submitWith false Button.Primary (Some icon) text onClick isLoading requiredFields

/// Secondary submit button with validation message
let submitButtonSecondary (text: string) (onClick: unit -> unit) (isLoading: bool) (requiredFields: (string * string) list) =
    submitWith false Button.Secondary None text onClick isLoading requiredFields
