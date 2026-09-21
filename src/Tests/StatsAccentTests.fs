module StatsAccentTests

// `accentBarClassName` (design-system-j9xnd): die Akzentstreifen-Entscheidung aus
// `Stats.view` als reine Funktion, testbar unter echtem .NET (kein Fable/React-
// Laufzeitkontext, siehe `ledger-w9skm`s `entryRowClassName`-Technik). `NoAccent`
// unterdrückt den dekorativen Streifen vollständig (`None`, ADR 0035); jeder der
// sechs farbigen Fälle liefert den vollständigen Klassen-String des Streifens.

open Expecto
open Client.DesignSystem.Stats

let private accentTests =
    testList "accentBarClassName" [

        test "NoAccent liefert gar keine Klasse" {
            // Fängt sowohl ein `Some ""` (das den unsichtbaren Streifen-Knoten
            // zurückbrächte, den ADR 0035 entfernt hat) als auch einen stillen
            // Rückfall auf `Gradient`.
            Expect.equal (accentBarClassName NoAccent) None "NoAccent muss None ergeben"
        }

        test "Alle sechs farbigen Fälle liefern eine Klasse mit Gradient-Grundgerüst" {
            let coloured = [ Teal; Green; Orange; Purple; Pink; Gradient ]
            for accent in coloured do
                match accentBarClassName accent with
                | None -> failtestf "%A muss eine Klasse liefern, nicht None" accent
                | Some cls ->
                    Expect.isTrue
                        (cls.Contains "bg-gradient-to-r")
                        $"Klasse für {accent} muss 'bg-gradient-to-r' enthalten: {cls}"
                    Expect.isTrue
                        (cls.Contains "from-neon-")
                        $"Klasse für {accent} muss mindestens einmal 'from-neon-' enthalten: {cls}"
        }

        test "Jeder farbige Fall trägt seine eigene, unterscheidbare Farbe" {
            // Fängt ein vertauschtes Paar in `accentToGradient`, das ein reiner
            // Contains-Check oben nicht bemerken würde.
            let expectedFragment =
                [ Teal, "from-neon-teal"
                  Green, "from-neon-green"
                  Orange, "from-neon-orange"
                  Purple, "from-neon-purple"
                  Pink, "from-neon-pink"
                  Gradient, "from-neon-teal via-neon-green to-neon-orange" ]

            for accent, fragment in expectedFragment do
                match accentBarClassName accent with
                | None -> failtestf "%A muss eine Klasse liefern, nicht None" accent
                | Some cls ->
                    Expect.isTrue
                        (cls.Contains fragment)
                        $"Klasse für {accent} muss '{fragment}' enthalten: {cls}"

            let results =
                expectedFragment
                |> List.map (fun (accent, _) -> accentBarClassName accent)
            let distinctCount = results |> List.distinct |> List.length
            Expect.equal distinctCount (List.length results) "Alle sechs Klassen-Strings müssen paarweise verschieden sein"
        }

        test "Kein Hex-Farbliteral im Ergebnis (ADR 0031: Farben laufen über den Token-Layer)" {
            let allAccents = [ Teal; Green; Orange; Purple; Pink; Gradient; NoAccent ]
            for accent in allAccents do
                match accentBarClassName accent with
                | None -> ()
                | Some cls -> Expect.isFalse (cls.Contains "#") $"Klasse für {accent} darf kein Hex-Literal enthalten: {cls}"
        }
    ]

[<Tests>]
let statsAccentTests = testList "Stats: Akzentstreifen-Entscheidung (design-system-j9xnd)" [ accentTests ]
