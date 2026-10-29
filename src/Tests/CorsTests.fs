module CorsTests

// infrastructure-z9b5w (Security-Review M4): BB hat keine Anmeldung, deshalb darf der
// Server kein CORS anbieten. Eine Policy mit AllowAnyOrigin hätte jeder Webseite im
// Browser eines Tailnet-Geräts Lese- und Schreibzugriff auf /api gegeben. Der Vite-Dev-Server
// leitet /api per Proxy weiter, die PWA im Betrieb kommt vom Server selbst: beides ist
// Same-Origin. Ein Host-Test gegen die echte Middleware-Kette ist ohne Umbau von
// Program.fs (Einstieg als EntryPoint-Funktion, kein WebApplicationFactory-fähiger Typ)
// nicht möglich, deshalb gilt hier die Quelltext-Zusicherung.

open System.IO
open Expecto

let private repoRoot =
    Directory.GetParent(Directory.GetParent(__SOURCE_DIRECTORY__).FullName).FullName

let private read (parts: string list) =
    File.ReadAllText(Path.Combine(repoRoot :: parts |> List.toArray))

// Zeilenkommentare abschneiden, damit eine Erklärung im Kommentar nicht als Aufruf zählt.
let private code (text: string) =
    text.Split('\n')
    |> Array.map (fun l ->
        let i = l.IndexOf "//"
        if i >= 0 then l.Substring(0, i) else l)
    |> String.concat "\n"

let private corsNeedles = [ "AddCors"; "UseCors"; "AllowAnyOrigin"; "AllowAll" ]

/// Die CORS-Aufrufe, die im Code (ohne Zeilenkommentare) stehen.
let private corsCalls (text: string) =
    let src = code text
    corsNeedles |> List.filter (fun needle -> src.Contains needle)

let private sep = string Path.DirectorySeparatorChar

[<Tests>]
let tests =
    testList "Kein CORS (infrastructure-z9b5w)" [
        testCase "Program.fs registriert und nutzt keine CORS-Middleware" <| fun () ->
            // Verhindert die Rückkehr von AddCors/UseCors("AllowAll"), das in Produktion
            // access-control-allow-origin: * für die ganze API lieferte.
            Expect.isEmpty (corsCalls (read [ "src"; "Server"; "Program.fs" ])) "Program.fs enthält CORS-Aufrufe"

        testCase "Gegenprobe: die Suche erkennt einen CORS-Aufruf und übergeht einen Kommentar" <| fun () ->
            // infrastructure-hdf7j: der Wächter beweist, dass er anschlagen kann.
            Expect.equal (corsCalls "builder.Services.AddCors() |> ignore") [ "AddCors" ] "Aufruf erkannt"
            Expect.isEmpty (corsCalls "// früher stand hier app.UseCors(\"AllowAll\")") "Kommentar zählt nicht"

        testCase "Server-Quelltexte kennen keine CORS-Policy" <| fun () ->
            let dir = Path.Combine(repoRoot, "src", "Server")
            let hits =
                Directory.GetFiles(dir, "*.fs", SearchOption.AllDirectories)
                |> Array.filter (fun f -> not (f.Contains $"{sep}obj{sep}" || f.Contains $"{sep}bin{sep}"))
                |> Array.filter (fun f -> (code (File.ReadAllText f)).Contains "Cors")
            Expect.isEmpty hits "kein Server-Quelltext darf CORS konfigurieren"

        testCase "Vite-Proxy leitet /api an das Backend (Same-Origin in Development)" <| fun () ->
            let cfg = read [ "vite.config.js" ]
            Expect.isTrue (cfg.Contains "proxy" && cfg.Contains "'/api'") "server.proxy für /api fehlt"
    ]
