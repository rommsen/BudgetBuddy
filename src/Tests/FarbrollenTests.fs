module FarbrollenTests

// design-system-dak16 (ADR 0087 Punkt 2): Hauptaktion im Akzent mit dunklem Text. Geprüft wird über die
// Tokens, nicht über Hex-Werte: ändert jemand den Knopf zurück auf eine eigene Farbe (etwa den alten
// Orange-Verlauf mit weißem Text), wird der Test rot. Die Werte selbst prüft scripts/contrast-guard.test.mjs.

open Expecto
open Client.DesignSystem
open Client.DesignSystem.Tokens

let private classes (cls: string) = cls.Split(' ', System.StringSplitOptions.RemoveEmptyEntries)
let private has (needle: string) (cls: string) = classes cls |> Array.contains needle

let private tests =
    testList "Farbrollen: Hauptaktion" [
        test "Button.primary füllt im Akzent und schreibt den Text in bg-app" {
            let cls = Button.variantClassFor { Button.defaultProps with Variant = Button.Primary }
            Expect.isTrue (has Backgrounds.accent cls) cls
            Expect.isTrue (has Colors.onAccent cls) cls
            Expect.isFalse (classes cls |> Array.exists (fun c -> c = "text-white" || c.Contains "gradient")) cls
        }

        test "Button.hero füllt im Akzent und schreibt den Text in bg-app" {
            Expect.isTrue (has Backgrounds.accent Button.heroClass) Button.heroClass
            Expect.isTrue (has Colors.onAccent Button.heroClass) Button.heroClass
        }

        test "Ein Knopf ohne Füllung trägt den Rand border-strong" {
            let cls = Button.variantClassFor { Button.defaultProps with Variant = Button.Secondary }
            Expect.isTrue (has Borders.strong cls) cls
        }
    ]

/// Die Rolle hinter einem Text-Token: `text-attention` -> `attention`. So hängen die Prüfungen an `Tokens.fs`,
/// nicht an einem hingeschriebenen Farbnamen.
let private roleOf (textToken: string) = textToken.Substring("text-".Length)

/// Trägt eine Klassenliste die Rolle (Text, Fläche oder Rand, auch mit Deckkraft)?
let private carries (role: string) (cls: string) =
    classes cls
    |> Array.exists (fun c ->
        [ "text-"; "bg-"; "border-"; "border-l-" ]
        |> List.exists (fun prefix -> c = prefix + role || c.StartsWith(prefix + role + "/")))

// design-system-dak16 (ADR 0087 Punkt 3): „braucht dich“ ist Gelb, auf jeder Seite. Vor dieser Task zeichnete
// `Badge.Warning` Pink und `Toast.Warning` Orange (Fix 5850313 betraf genau dieses Pink auf der Budget-Seite).
let private warningTests =
    let attention = roleOf Colors.attention
    testList "Farbrollen: braucht dich" [
        test "Badge.Warning trägt Gelb und keine andere Rollenfarbe" {
            let cls = Badge.variantClass Badge.Warning
            Expect.isTrue (carries attention cls) cls
            for other in [ Colors.accent; Colors.error; Colors.success; Colors.info ] do
                Expect.isFalse (carries (roleOf other) cls) $"ohne {other}: {cls}"
        }

        test "ErrorDisplay.warning und notice: Fläche und Rand in Gelb, das Symbol in Gelb" {
            Expect.isTrue (carries attention ErrorDisplay.attentionBoxClass) ErrorDisplay.attentionBoxClass
            Expect.equal (Icons.colorToClass ErrorDisplay.attentionIconColor) Colors.attention "Symbolfarbe"
        }

        test "Toast.Warning trägt Gelb" {
            let cls = Toast.variantClass Toast.Warning
            Expect.isTrue (carries attention cls) cls
            Expect.isFalse (carries (roleOf Colors.error) cls) cls
        }

        test "Gegenprobe: eine Variante in Rot erkennt die Prüfung nicht als Gelb" {
            Expect.isFalse (carries attention (Badge.variantClass Badge.Error)) "Badge.Error"
            Expect.isFalse (carries attention (Toast.variantClass Toast.Error)) "Toast.Error"
            Expect.isTrue (carries (roleOf Colors.error) (Badge.variantClass Badge.Error)) "Rot erkannt"
        }
    ]

// design-system-dak16 (ADR 0087 Punkt 8): die Lena-Farbe erreicht das Flag-Symbol nur über `data-lena-flag`,
// und styles.css kennt dort nur klein geschriebene YNAB-Farben.
let private lenaTests =
    testList "Farbrollen: Lena-Flag" [
        test "Eine bekannte Flag-Farbe wird klein geschrieben zum Attributwert" {
            Expect.equal (Badge.lenaFlagAttr (Some "purple")) (Some "purple") "klein"
            Expect.equal (Badge.lenaFlagAttr (Some "Blue")) (Some "blue") "gemischt"
            Expect.equal (Badge.lenaFlagAttr (Some "RED")) (Some "red") "groß"
        }

        test "Ohne gespeicherte oder mit unbekannter Farbe gibt es kein Attribut" {
            Expect.equal (Badge.lenaFlagAttr None) None "keine Farbe"
            Expect.equal (Badge.lenaFlagAttr (Some "")) None "leer"
            Expect.equal (Badge.lenaFlagAttr (Some "pink")) None "unbekannt"
        }
    ]

[<Tests>]
let allTests = testList "Farbrollen" [ tests; warningTests; lenaTests ]
