module TextGlowTokenTests

// design-system-tg2wn: `TextGlows.orange`/`TextGlows.teal` (heute `success`/`accent`) waren wie vormals `green`
// Literale ("text-glow-orange"), die weder in styles.css definiert noch echte
// Tailwind-Utilities sind. Der Wächter meldet sie nicht, solange kein Aufrufer sie
// benutzt. Der Test verhindert den Rückfall auf ein solches Literal.

open Expecto
open Client.DesignSystem.Tokens

let private tests =
    testList "TextGlows" [
        test "success und accent nutzen die text-shadow-Arbitrary-Property mit dem Theme-Token" {
            let cases =
                [ "success", TextGlows.success, "--color-success"
                  "accent", TextGlows.accent, "--color-accent" ]
            for (name, cls, token) in cases do
                Expect.isTrue (cls.StartsWith "[text-shadow:") $"{name}: Arbitrary-Property erwartet: {cls}"
                Expect.isTrue (cls.Contains $"var({token})") $"{name}: Themefarbe fehlt: {cls}"
                Expect.isFalse (cls.Contains "rgba") $"{name}: kein eigener rgba-Wert neben dem Token: {cls}"
        }
    ]

[<Tests>]
let allTests = tests
