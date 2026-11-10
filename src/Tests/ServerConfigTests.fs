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

// ledger-mj3a9 (ADR 0068, Nachtrag 2026-11-10): ein gesetzter, aber ungültiger
// BOT_MAX_AMOUNT_EUR fiel auf 500 EUR zurück, also auf den harten Deckel. Ein
// Tippfehler beim Absenken ("50,00") gab Bob so die höchste Grenze.
let private botMaxAmountTests =
    let blocked raw = Invalid raw
    testList "BOT_MAX_AMOUNT_EUR" [

        // Regression: vor dem Fix lieferte jeder dieser Werte 500 EUR.
        test "Komma \"50,00\" sperrt Bob statt auf 500 Euro zurückzufallen" {
            Expect.equal (resolveBotMaxAmountEur "50,00") (blocked "50,00") "Komma"
            Expect.equal (effectiveBotMaxAmountEur (blocked "50,00")) None "keine wirksame Grenze"
        }

        test "\"0\" sperrt Bob" {
            Expect.equal (resolveBotMaxAmountEur "0") (blocked "0") "0 ist keine Grenze"
        }

        test "Vorzeichen \"-5\" sperrt Bob" {
            Expect.equal (resolveBotMaxAmountEur "-5") (blocked "-5") "negativ"
        }

        test "Tausendertrennzeichen \"1,000\" sperrt Bob" {
            Expect.equal (resolveBotMaxAmountEur "1,000") (blocked "1,000") "Tausendertrennzeichen"
        }

        test "Text \"abc\" sperrt Bob" {
            Expect.equal (resolveBotMaxAmountEur "abc") (blocked "abc") "Text"
        }

        test "nicht gesetzt (null, leer, nur Leerraum) ergibt die Vorgabe 500 Euro" {
            Expect.equal (resolveBotMaxAmountEur null) Default "null"
            Expect.equal (resolveBotMaxAmountEur "") Default "leer"
            Expect.equal (resolveBotMaxAmountEur "   ") Default "Leerraum"
            Expect.equal (effectiveBotMaxAmountEur Default) (Some 500m) "wirksam 500 Euro"
        }

        test "ein gültiger Wert senkt die Grenze: \"100.00\" ergibt 100 Euro" {
            Expect.equal (resolveBotMaxAmountEur "100.00") (Lowered 100m) "100 Euro"
            Expect.equal (resolveBotMaxAmountEur " 120.5 ") (Lowered 120.5m) "Leerraum wird getrimmt"
            Expect.equal (effectiveBotMaxAmountEur (Lowered 100m)) (Some 100m) "wirksam 100 Euro"
        }

        test "harter Deckel: \"900\" ergibt 500 Euro, die Umgebung darf nur absenken" {
            Expect.equal (resolveBotMaxAmountEur "900") (Capped "900") "gedeckelt"
            Expect.equal (resolveBotMaxAmountEur "500.01") (Capped "500.01") "knapp darüber"
            Expect.equal (resolveBotMaxAmountEur "500") (Lowered 500m) "genau der Deckel"
            Expect.equal (effectiveBotMaxAmountEur (Capped "900")) (Some 500m) "wirksam 500 Euro"
        }

        test "Startlog: ein ungültiger Wert nennt Rohwert und Folge" {
            let line = botMaxAmountLogLine "50,00"
            Expect.stringContains line "BOT_MAX_AMOUNT_EUR=\"50,00\"" "Rohwert"
            Expect.stringContains line "Bob ist gesperrt, bis der Wert korrigiert ist" "Folge"
            Expect.isFalse (line.Contains "500.00 EUR") "keine wirksame Grenze genannt"
        }

        test "Startlog: nicht gesetzt, gültig und gedeckelt wie bisher" {
            Expect.equal (botMaxAmountLogLine null) "Bot-Schnittstelle: Obergrenze je Buchung 500.00 EUR (Vorgabe)." "Vorgabe"
            Expect.equal (botMaxAmountLogLine "250.50") "Bot-Schnittstelle: Obergrenze je Buchung 250.50 EUR." "gültig"
            Expect.equal
                (botMaxAmountLogLine "900")
                "Bot-Schnittstelle: Obergrenze je Buchung 500.00 EUR. BOT_MAX_AMOUNT_EUR=\"900\" liegt über dem harten Deckel und wird gedeckelt."
                "gedeckelt"
        }
    ]

[<Tests>]
let tests = testList "ServerConfig" [ resolveUrlsTests; botMaxAmountTests ]
