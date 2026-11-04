module ButtonSizeTests

// design-system-j6dnj: Button.Small ist am Handy mindestens 48 px hoch (Styleguide §4, Touch-Fläche),
// ab md ohne Mindesthöhe. Icon-only-Knöpfe sind mobil auch 48 px breit.
// Die Höhe misst der Browser-Wächter scripts/touch-target-small-button.test.mjs; hier steht nur
// die Verzweigung von iconOnlyClass (welche Knöpfe als Icon-only gelten), infrastructure-hdf7j.

open Expecto
open Client.DesignSystem

let private has (needle: string) (cls: string) = cls.Split(' ') |> Array.contains needle

let private tests =
    testList "Button-Größen (j6dnj)" [
        test "Icon-only Small: mobil 48 px Mindestbreite, ab md keine" {
            let cls = Button.iconOnlyClass Button.Small ""
            Expect.isTrue (has "min-w-[48px]" cls) cls
            Expect.isTrue (has "md:min-w-0" cls) cls
        }

        test "Small mit Text oder andere Größen: keine Mindestbreite" {
            Expect.equal (Button.iconOnlyClass Button.Small "Barabhebung") "" "Small mit Text"
            Expect.equal (Button.iconOnlyClass Button.Medium "") "" "Medium ohne Text"
        }
    ]

// budgeting-r3wq8: IsBusy sperrt einen Knopf, der den Fokus haben kann, nur über ARIA. Mit `disabled`
// verlöre er den Fokus, sobald der eigene Aufruf läuft.
let private busyTests =
    testList "Button: IsBusy" [
        test "IsBusy setzt aria-disabled und aria-busy und zeigt den Spinner, ohne disabled" {
            Expect.equal
                (Button.buttonState { Button.defaultProps with IsBusy = true })
                { Disabled = false; AriaDisabled = true; AriaBusy = true; ShowsSpinner = true }
                "busy"
        }
        test "IsLoading und IsDisabled setzen weiter disabled" {
            Expect.equal
                (Button.buttonState { Button.defaultProps with IsLoading = true })
                { Disabled = true; AriaDisabled = false; AriaBusy = true; ShowsSpinner = true }
                "loading"
            Expect.equal
                (Button.buttonState { Button.defaultProps with IsDisabled = true; IsBusy = true })
                { Disabled = true; AriaDisabled = false; AriaBusy = false; ShowsSpinner = false }
                "gesperrt geht vor"
            Expect.equal
                (Button.buttonState Button.defaultProps)
                { Disabled = false; AriaDisabled = false; AriaBusy = false; ShowsSpinner = false }
                "frei"
        }
        // budgeting-c6vn4 (P3): ein beschäftigter Knopf sah beim Überfahren aus, als ließe er sich tippen.
        test "Ein beschäftigter Knopf trägt keine Hover-Klassen, ein freier schon" {
            let hovers (props: Button.ButtonProps) =
                (Button.variantClassFor props).Split(' ') |> Array.exists (fun c -> c.StartsWith "hover:")
            for variant in [ Button.Primary; Button.Secondary; Button.Ghost; Button.Danger ] do
                Expect.isFalse (hovers { Button.defaultProps with Variant = variant; IsBusy = true }) $"{variant} beschäftigt"
                Expect.isTrue (hovers { Button.defaultProps with Variant = variant }) $"{variant} frei"
        }
    ]

[<Tests>]
let buttonSizeTests = testList "Button" [ tests; busyTests ]
