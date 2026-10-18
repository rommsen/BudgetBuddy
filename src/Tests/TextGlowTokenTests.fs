module TextGlowTokenTests

// design-system-tg2wn: `TextGlows.orange`/`TextGlows.teal` waren wie vormals `green`
// Literale ("text-glow-orange"), die weder in styles.css definiert noch echte
// Tailwind-Utilities sind. Der Wächter meldet sie nicht, solange kein Aufrufer sie
// benutzt. Der Test verhindert den Rückfall auf ein solches Literal.

open Expecto
open Client.DesignSystem.Tokens

let private tests =
    testList "TextGlows" [
        test "green, orange und teal nutzen die text-shadow-Arbitrary-Property mit den Theme-Werten" {
            let cases =
                [ "green", TextGlows.green, "neon-green", "rgba(0,255,136,0.5)"
                  "orange", TextGlows.orange, "neon-orange", "rgba(255,107,44,0.5)"
                  "teal", TextGlows.teal, "neon-teal", "rgba(0,212,170,0.5)" ]
            for (name, cls, colour, rgba) in cases do
                Expect.isTrue (cls.StartsWith "[text-shadow:") $"{name}: Arbitrary-Property erwartet: {cls}"
                Expect.isTrue (cls.Contains $"theme(colors.{colour})") $"{name}: Themefarbe fehlt: {cls}"
                Expect.isTrue (cls.Contains rgba) $"{name}: rgba-Wert fehlt: {cls}"
        }
    ]

[<Tests>]
let allTests = tests
