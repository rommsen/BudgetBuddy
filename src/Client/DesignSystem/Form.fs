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

/// Submit button with validation message below
/// Shows orange warning when required fields are missing
let submitButton (text: string) (onClick: unit -> unit) (isLoading: bool) (requiredFields: (string * string) list) =
    let missingFields = getMissingFields requiredFields
    let isDisabled = isLoading || not (List.isEmpty missingFields)

    Html.div [
        prop.className "space-y-2"
        prop.children [
            // The button
            Button.view {
                Button.defaultProps with
                    Text = text
                    OnClick = onClick
                    Variant = Button.Primary
                    IsLoading = isLoading
                    IsDisabled = isDisabled
                    Icon = Some (Icons.check SM Icons.Primary)
            }

            // Validation message below button when disabled and fields are missing
            if isDisabled && not isLoading && not (List.isEmpty missingFields) then
                Html.div [
                    prop.className "flex items-center gap-2 text-sm text-neon-orange"
                    prop.children [
                        Icons.warning SM NeonOrange
                        Html.span [
                            let fields = String.concat ", " missingFields
                            prop.text $"Please fill in: {fields}"
                        ]
                    ]
                ]
        ]
    ]

/// Submit button with custom icon
let submitButtonWithIcon (text: string) (icon: ReactElement) (onClick: unit -> unit) (isLoading: bool) (requiredFields: (string * string) list) =
    let missingFields = getMissingFields requiredFields
    let isDisabled = isLoading || not (List.isEmpty missingFields)

    Html.div [
        prop.className "space-y-2"
        prop.children [
            Button.view {
                Button.defaultProps with
                    Text = text
                    OnClick = onClick
                    Variant = Button.Primary
                    IsLoading = isLoading
                    IsDisabled = isDisabled
                    Icon = Some icon
            }

            if isDisabled && not isLoading && not (List.isEmpty missingFields) then
                Html.div [
                    prop.className "flex items-center gap-2 text-sm text-neon-orange"
                    prop.children [
                        Icons.warning SM NeonOrange
                        Html.span [
                            let fields = String.concat ", " missingFields
                            prop.text $"Please fill in: {fields}"
                        ]
                    ]
                ]
        ]
    ]

/// Secondary submit button with validation message
let submitButtonSecondary (text: string) (onClick: unit -> unit) (isLoading: bool) (requiredFields: (string * string) list) =
    let missingFields = getMissingFields requiredFields
    let isDisabled = isLoading || not (List.isEmpty missingFields)

    Html.div [
        prop.className "space-y-2"
        prop.children [
            Button.view {
                Button.defaultProps with
                    Text = text
                    OnClick = onClick
                    Variant = Button.Secondary
                    IsLoading = isLoading
                    IsDisabled = isDisabled
            }

            if isDisabled && not isLoading && not (List.isEmpty missingFields) then
                Html.div [
                    prop.className "flex items-center gap-2 text-sm text-neon-orange"
                    prop.children [
                        Icons.warning SM NeonOrange
                        Html.span [
                            let fields = String.concat ", " missingFields
                            prop.text $"Please fill in: {fields}"
                        ]
                    ]
                ]
        ]
    ]
