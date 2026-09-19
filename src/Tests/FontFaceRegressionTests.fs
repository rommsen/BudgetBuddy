module FontFaceRegressionTests

open System
open System.Diagnostics
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

// design-system-hadjy: Bug: der Export (scripts/export-design-system.mjs) kopierte das
// gebaute App-CSS unverändert ins Claude-Design-Bundle, also mit absolutem
// `src: url(/fonts/...)`. Weil styles.css im Bundle nach colors_and_type.css eingebunden
// ist, gewinnt die absolute Regel gegenüber der relativen aus FONT_FACE_CSS
// (`url('fonts/...')`), und die Previews (ausgeliefert unter .../serve/preview/...)
// fragen die Fonts an der Bundle-Origin-Wurzel an: 404, Schriften laden nicht.
//
// Fix: scripts/export-design-system.mjs exportiert eine reine Funktion
// `rewriteAbsoluteAssetUrls(css)`, die jedes `url(/...)` relativ zur Bundle-Wurzel
// umschreibt; `copyBuiltCss` liest das gebaute CSS, schreibt es damit um und schreibt es
// erst dann ins Bundle (statt es per copyFile unverändert zu kopieren).
//
// Diese Tests rufen die REALE Funktion aus dem Export-Skript per Node-Subprozess auf
// (kein Playwright/vite nötig, s. design-system-t82sg: "kein Playwright-Harness in
// dotnet test") - keine in F# nachgebaute Kopie der Rewrite-Logik mit Drift-Risiko. Sie
// fallen vor dem Fix fehl (die Funktion existiert nicht, der Node-Aufruf schlägt fehl).

let private exportScriptPath =
    Path.Combine(repoRoot, "scripts", "export-design-system.mjs")

/// Ruft `rewriteAbsoluteAssetUrls` aus scripts/export-design-system.mjs per
/// Node-Subprozess auf: CSS geht über stdin rein, das Ergebnis kommt über stdout
/// zurück. Ein eigenes, temporäres Harness-Skript importiert die Funktion aus dem
/// echten Export-Skript - es testet also dessen tatsächliches Verhalten.
let private runRewriteAbsoluteAssetUrls (css: string) : string =
    let scriptUrl = "file://" + exportScriptPath.Replace("\\", "/")

    let harnessSource =
        String.concat
            "\n"
            [ sprintf "import { rewriteAbsoluteAssetUrls } from '%s';" scriptUrl
              "let input = '';"
              "process.stdin.setEncoding('utf8');"
              "process.stdin.on('data', (chunk) => { input += chunk; });"
              "process.stdin.on('end', () => { process.stdout.write(rewriteAbsoluteAssetUrls(input)); });" ]

    let harnessPath =
        Path.Combine(Path.GetTempPath(), sprintf "rewrite-absolute-asset-urls-%s.mjs" (Guid.NewGuid().ToString("N")))

    File.WriteAllText(harnessPath, harnessSource)

    try
        let psi = ProcessStartInfo("node", harnessPath)
        psi.RedirectStandardInput <- true
        psi.RedirectStandardOutput <- true
        psi.RedirectStandardError <- true
        psi.UseShellExecute <- false

        use proc = Process.Start(psi)
        proc.StandardInput.Write(css: string)
        proc.StandardInput.Close()
        let output = proc.StandardOutput.ReadToEnd()
        let errOutput = proc.StandardError.ReadToEnd()
        proc.WaitForExit()

        if proc.ExitCode <> 0 then
            failwithf
                "node-Aufruf für rewriteAbsoluteAssetUrls (scripts/export-design-system.mjs) schlug mit Exit-Code %d fehl: %s"
                proc.ExitCode
                errOutput

        output
    finally
        File.Delete(harnessPath)

/// Prüft, ob im CSS-Text noch ein absoluter Asset-Pfad der Form `url(/...)` steht
/// (aber nicht `url(//...)`, protokoll-relativ, und nicht `url(data:...)`).
let private hasAbsoluteAssetUrl (css: string) =
    Regex.IsMatch(css, @"url\(\s*['""]?/(?!/)")

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

        testCase "rewriteAbsoluteAssetUrls schreibt url(/fonts/...) relativ zur Bundle-Wurzel um" <| fun () ->
            let input =
                "@font-face {\n  src: url(/fonts/inter-latin-wght-normal.woff2) format('woff2-variations');\n}"

            let output = runRewriteAbsoluteAssetUrls input

            Expect.isFalse
                (hasAbsoluteAssetUrl output)
                "rewriteAbsoluteAssetUrls muss jeden absoluten url(/...)-Pfad relativ zur Bundle-Wurzel machen."
            Expect.stringContains
                output
                "url(fonts/inter-latin-wght-normal.woff2)"
                "rewriteAbsoluteAssetUrls muss url(/fonts/...) zu url(fonts/...) machen (relativ, wie FONT_FACE_CSS in colors_and_type.css)."

        testCase "rewriteAbsoluteAssetUrls lässt bereits relative und gequotete url(...)-Pfade unverändert" <| fun () ->
            let input =
                "@font-face { src: url('fonts/inter-latin-wght-normal.woff2') format('woff2-variations'); }"

            let output = runRewriteAbsoluteAssetUrls input

            Expect.equal
                output
                input
                "rewriteAbsoluteAssetUrls darf bereits relative url(...)-Pfade nicht verändern."

        testCase "rewriteAbsoluteAssetUrls lässt protokoll-relative url(//...)-Pfade unverändert" <| fun () ->
            let input = "@font-face { src: url(//cdn.example.com/fonts/inter.woff2); }"

            let output = runRewriteAbsoluteAssetUrls input

            Expect.equal
                output
                input
                "rewriteAbsoluteAssetUrls darf protokoll-relative url(//...)-Pfade nicht anfassen (kein root-relativer Bug)."

        testCase "rewriteAbsoluteAssetUrls macht das gebaute App-CSS (src/Client/styles.css) frei von absoluten url(/...)-Pfaden" <| fun () ->
            // Regressions-Reproduktion des gemeldeten Bugs: src/Client/styles.css enthält
            // die drei absoluten @font-face-Regeln, die die App als von der Origin-Wurzel
            // ausgelieferte App braucht (korrekt für die App selbst); im Claude-Design-Bundle
            // müssen dieselben Regeln aber relativ zur Bundle-Wurzel sein.
            let css = readStylesCss ()

            let output = runRewriteAbsoluteAssetUrls css

            Expect.isFalse
                (hasAbsoluteAssetUrl output)
                "Bundle-CSS darf nach rewriteAbsoluteAssetUrls keine absoluten url(/...)-Pfade mehr enthalten."

            for fontFile in
                [ "inter-latin-wght-normal.woff2"
                  "jetbrains-mono-latin-wght-normal.woff2"
                  "space-grotesk-latin-wght-normal.woff2" ] do
                // src/Client/styles.css bindet die Fonts gequotet ein (url('/fonts/...')),
                // die Umschreibung erhält das Quoting und schneidet nur den führenden "/" weg.
                Expect.stringContains
                    output
                    (sprintf "url('fonts/%s')" fontFile)
                    (sprintf "erwartet relative Einbindung url('fonts/%s') im umgeschriebenen Bundle-CSS." fontFile)
    ]
