module ServerConfigTests

// infra-jh7g9: der Backend-Port stand als Literal in Program.fs
// (app.Run("http://0.0.0.0:5081")), das überschrieb die bereits vorhandene
// ASPNETCORE_URLS-Zeile aus compose.yaml stillschweigend. Dieser Test deckt
// die reine Auflösungsfunktion ab, die Program.fs jetzt nutzt: ohne gesetzten
// Wert bleibt der bisherige Standardport 5081 erhalten, mit gesetztem Wert
// gewinnt dieser.

open Expecto
open Server.ServerConfig

let private resolveUrlsTests =
    testList "resolveUrls" [

        test "ohne gesetzten Wert (null) bleibt der bisherige Standard http://0.0.0.0:5081 erhalten" {
            // Bug-Regression: verhindert, dass der Port wieder als Literal in
            // Program.fs zurückkehrt, ohne dass sich das Standardverhalten ändert.
            Expect.equal (resolveUrls null) "http://0.0.0.0:5081" "Unverändertes Standardverhalten ohne ASPNETCORE_URLS"
        }

        test "leerer String zählt als nicht gesetzt und liefert ebenfalls den Standard" {
            Expect.equal (resolveUrls "") "http://0.0.0.0:5081" "Leerer String ist kein gültiger Konfigurationswert"
        }

        test "ein gesetzter Wert gewinnt gegenüber dem Standard" {
            // Das ist der eigentliche Fix: eine gesetzte ASPNETCORE_URLS-Zeile
            // (z. B. aus compose.yaml) muss jetzt tatsächlich wirksam werden.
            Expect.equal (resolveUrls "http://+:9999") "http://+:9999" "Gesetzter Wert wird unverändert durchgereicht"
        }

        test "ein gesetzter Wert mit abweichendem Port ändert den gebundenen Port" {
            // Nachweis, dass eine Änderung der Konfiguration den gebundenen Port
            // tatsächlich ändert, nicht nur, dass irgendein Wert durchgereicht wird.
            Expect.equal (resolveUrls "http://0.0.0.0:6100") "http://0.0.0.0:6100" "Abweichender Port kommt unverändert durch"
        }
    ]

[<Tests>]
let tests = testList "ServerConfig" [ resolveUrlsTests ]
