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

[<Tests>]
let tests =
    testList "Kein CORS (infrastructure-z9b5w)" [
        testCase "Program.fs registriert und nutzt keine CORS-Middleware" <| fun () ->
            // Verhindert die Rückkehr von AddCors/UseCors("AllowAll"), das in Produktion
            // access-control-allow-origin: * für die ganze API lieferte.
            let src = code (read [ "src"; "Server"; "Program.fs" ])
            for needle in [ "AddCors"; "UseCors"; "AllowAnyOrigin"; "AllowAll" ] do
                Expect.isFalse (src.Contains needle) $"Program.fs enthält {needle}"

        testCase "Server-Quelltexte kennen keine CORS-Policy" <| fun () ->
            let dir = Path.Combine(repoRoot, "src", "Server")
            let hits =
                Directory.GetFiles(dir, "*.fs", SearchOption.AllDirectories)
                |> Array.filter (fun f -> not (f.Contains "/obj/" || f.Contains "/bin/"))
                |> Array.filter (fun f -> (code (File.ReadAllText f)).Contains "Cors")
            Expect.isEmpty hits "kein Server-Quelltext darf CORS konfigurieren"

        testCase "Vite-Proxy leitet /api an das Backend (Same-Origin in Development)" <| fun () ->
            let cfg = read [ "vite.config.js" ]
            Expect.isTrue (cfg.Contains "proxy" && cfg.Contains "'/api'") "server.proxy für /api fehlt"
    ]
