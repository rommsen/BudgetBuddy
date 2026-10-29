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

[<Tests>]
let buttonSizeTests = tests
