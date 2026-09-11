module FontFaceRegressionTests

open System.IO
open System.Text.RegularExpressions
open Expecto

// design-system-f7k2p: Regressionstest für den Font-Bug aus ADR 0024/0026.
//
// Bug: der Google-Fonts-@import in src/Client/styles.css stand hinter dem
// @theme-Block. CSS verlangt @import vor allen Regeln (außer @charset/@layer);
// der Tailwind-4-Build hat den @import deshalb verworfen ("@import rules must
// precede all rules"). Die gebaute App rendert seitdem in Browser-Ersatzschriften,
// weil styles.css am Ende weder @import noch @font-face enthält.
//
// Fix: kein Google-Fonts-Import mehr, stattdessen @font-face mit lokalem
// url(...) für Inter, JetBrains Mono und Space Grotesk (Dateien unter
// src/Client/public/fonts/, siehe LICENSE.md dort).
//
// Dieser Test liest src/Client/styles.css als Text (nicht das gebaute CSS) und
// prüft: kein fonts.googleapis.com/fonts.gstatic.com-Verweis mehr, und für
// jede der drei Familien ein @font-face-Block mit lokalem url(...). Er fällt
// vor dem Fix fehl (der @import auf fonts.googleapis.com existiert noch, es
// gibt keine @font-face-Blöcke).

let private repoRoot =
    let testDir = __SOURCE_DIRECTORY__
    Directory.GetParent(Directory.GetParent(testDir).FullName).FullName

let private stylesCssPath = Path.Combine(repoRoot, "src", "Client", "styles.css")

let private readStylesCss () = File.ReadAllText(stylesCssPath)

/// Prüft, ob ein @font-face-Block existiert, dessen font-family (in Anführungszeichen
/// oder ohne) den gegebenen Namen enthält und dessen src eine lokale
/// url(...) referenziert (kein http(s)://).
let private hasLocalFontFace (css: string) (familyName: string) =
    let fontFaceBlocks =
        Regex.Matches(css, @"@font-face\s*\{[^}]*\}", RegexOptions.Singleline)

    fontFaceBlocks
    |> Seq.cast<Match>
    |> Seq.exists (fun m ->
        let block = m.Value

        let mentionsFamily =
            Regex.IsMatch(
                block,
                Regex.Escape(familyName),
                RegexOptions.IgnoreCase
            )

        let hasLocalUrl =
            Regex.IsMatch(block, @"url\(\s*['""]?(?!https?:)[^'"")]+\.woff2?['""]?\s*\)")

        mentionsFamily && hasLocalUrl)

[<Tests>]
let fontFaceRegressionTests =
    testList "FontFaceRegression" [

        testCase "styles.css does not import fonts from fonts.googleapis.com" <| fun () ->
            let css = readStylesCss ()

            Expect.isFalse
                (css.Contains("fonts.googleapis.com"))
                "styles.css darf keinen Verweis mehr auf fonts.googleapis.com enthalten (App muss die Schriften selbst ausliefern, ADR 0026)."

        testCase "styles.css does not reference fonts.gstatic.com" <| fun () ->
            let css = readStylesCss ()

            Expect.isFalse
                (css.Contains("fonts.gstatic.com"))
                "styles.css darf keinen Verweis mehr auf fonts.gstatic.com enthalten (App muss die Schriften selbst ausliefern, ADR 0026)."

        testCase "styles.css defines a local @font-face for Inter" <| fun () ->
            let css = readStylesCss ()

            Expect.isTrue
                (hasLocalFontFace css "Inter")
                "styles.css muss ein @font-face mit lokalem url(...) für Inter enthalten."

        testCase "styles.css defines a local @font-face for JetBrains Mono" <| fun () ->
            let css = readStylesCss ()

            Expect.isTrue
                (hasLocalFontFace css "JetBrains Mono")
                "styles.css muss ein @font-face mit lokalem url(...) für JetBrains Mono enthalten."

        testCase "styles.css defines a local @font-face for Space Grotesk" <| fun () ->
            let css = readStylesCss ()

            Expect.isTrue
                (hasLocalFontFace css "Space Grotesk")
                "styles.css muss ein @font-face mit lokalem url(...) für Space Grotesk enthalten."

        testCase "font files referenced by @font-face exist under src/Client/public/fonts" <| fun () ->
            let css = readStylesCss ()

            let urls =
                Regex.Matches(css, @"@font-face\s*\{[^}]*\}", RegexOptions.Singleline)
                |> Seq.cast<Match>
                |> Seq.collect (fun m ->
                    Regex.Matches(m.Value, @"url\(\s*['""]?([^'"")]+\.woff2?)['""]?\s*\)")
                    |> Seq.cast<Match>
                    |> Seq.map (fun um -> um.Groups.[1].Value))
                |> Seq.toList

            Expect.isNonEmpty urls "es sollten @font-face-Blöcke mit url(...) auf Font-Dateien existieren."

            for url in urls do
                // url ist im Repo z. B. "/fonts/inter-latin-wght-normal.woff2" (root-relativ,
                // aufgelöst gegen public/); relative Pfade werden nicht erwartet.
                let relativePath = url.TrimStart('/')
                let filePath = Path.Combine(repoRoot, "src", "Client", "public", relativePath)

                Expect.isTrue
                    (File.Exists(filePath))
                    (sprintf "Font-Datei fehlt: %s (erwartet unter %s)" url filePath)
    ]
