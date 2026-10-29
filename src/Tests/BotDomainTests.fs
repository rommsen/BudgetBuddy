module BotDomainTests

// Fachregeln der Bot-Schnittstelle in `Ledger.Bot.Domain` (ledger-ct981, ADR 0066 bis 0068):
// Datumsfenster, Tagesgrenze, Berliner Kalendertag, Kategorie-Auflösung. Rein, die Zeitzone
// kommt als Parameter.

open System
open Expecto
open Shared.Domain
open Ledger.Bot.Domain

/// Europe/Berlin wie in der App, einmal aufgelöst über `Server.ServerConfig`.
let private zone () : TimeZoneInfo =
    match Server.ServerConfig.tryResolveBerlinZone () with
    | Ok z -> z
    | Error msg -> failtest msg

let private category (name: string) (group: string) (hidden: bool) : YnabCategory =
    { Id = YnabCategoryId (Guid.NewGuid())
      Name = name
      GroupName = group
      Available = None
      Hidden = hidden }

let private haushalt = category "Haushaltshilfe" "Haushalt" false
let private haushaltWaesche = category "Wäsche" "Haushalt" false
let private waescheZwei = category "wäsche" "Kleidung" false
let private versteckt = category "Altlast" "Haushalt" true
let private intern = category "Inflow: Ready to Assign" internalMasterGroupName false
let private all = [ haushalt; haushaltWaesche; waescheZwei; versteckt; intern ]

[<Tests>]
let botBookingDateTests =
    let today = DateTime(2026, 10, 28, 12, 0, 0, DateTimeKind.Utc)
    let day (y: int) (m: int) (d: int) = DateTime(y, m, d)
    testList "Bot Datumsfenster" [

        testCase "heute und heute minus 31 Tage sind erlaubt" <| fun () ->
            Expect.isOk (checkBotBookingDate today (day 2026 10 28)) "heute"
            Expect.isOk (checkBotBookingDate today (day 2026 9 27)) "heute minus 31 Tage"

        testCase "morgen liegt in der Zukunft, heute minus 32 Tage ist zu alt, die Meldungen sagen warum" <| fun () ->
            Expect.equal (checkBotBookingDate today (day 2026 10 29)) (Error "Das Datum liegt in der Zukunft") "Zukunft"
            Expect.equal
                (checkBotBookingDate today (day 2026 9 26))
                (Error "Das Datum darf höchstens 31 Tage zurückliegen")
                "heute minus 32 Tage"

        testCase "nur das Datum von today zählt, nicht die Uhrzeit" <| fun () ->
            let lateToday = DateTime(2026, 10, 28, 23, 59, 0, DateTimeKind.Utc)
            Expect.isError (checkBotBookingDate lateToday (day 2026 10 29)) "morgen bleibt Zukunft"
            Expect.isOk (checkBotBookingDate (day 2026 10 28) (day 2026 10 28)) "heute um Mitternacht"
    ]

[<Tests>]
let botCategoryTests =
    testList "Bot Vorschau: Kategorie-Auflösung" [

        testCase "genau ein Namenstreffer, ohne Beachtung der Groß-/Kleinschreibung" <| fun () ->
            match resolveBotCategory all "  haushaltshilfe " with
            | BotCategoryResolved c -> Expect.equal c.Id haushalt.Id "Haushaltshilfe"
            | other -> failtest $"{other}"

        testCase "die Id löst auf" <| fun () ->
            let (YnabCategoryId g) = haushalt.Id
            match resolveBotCategory all (g.ToString()) with
            | BotCategoryResolved c -> Expect.equal c.Id haushalt.Id "per Id"
            | other -> failtest $"{other}"

        testCase "mehrere Namenstreffer ergeben Kandidaten, BB rät nicht" <| fun () ->
            match resolveBotCategory all "WÄSCHE" with
            | BotCategoryAmbiguous candidates ->
                Expect.equal (candidates |> List.map (fun c -> c.GroupName) |> List.sort) [ "Haushalt"; "Kleidung" ] "beide Gruppen"
            | other -> failtest $"{other}"

        testCase "kein Treffer: Kandidaten sind Kategorien mit ähnlichem Namen" <| fun () ->
            match resolveBotCategory all "Wäsche waschen" with
            | BotCategoryUnknown candidates -> Expect.equal (candidates.Length) 2 "beide Wäsche"
            | other -> failtest $"{other}"
            match resolveBotCategory all "Gibt es nicht" with
            | BotCategoryUnknown candidates -> Expect.isEmpty candidates "keine Kandidaten"
            | other -> failtest $"{other}"

        testCase "interne und versteckte Kategorien sind nicht wählbar" <| fun () ->
            match resolveBotCategory all "Inflow: Ready to Assign" with
            | BotCategoryResolved _ -> failtest "interne Kategorie darf nicht wählbar sein"
            | _ -> ()
            match resolveBotCategory all "Altlast" with
            | BotCategoryResolved _ -> failtest "versteckte Kategorie darf nicht wählbar sein"
            | _ -> ()
            Expect.equal (selectableBotCategories all |> List.length) 3 "drei wählbare"
    ]

[<Tests>]
let botDailyLimitTests =
    let utc y m d h mi = DateTime(y, m, d, h, mi, 0, DateTimeKind.Utc)
    testList "Bot Tagesgrenze und Berliner Tag" [

        testCase "unter der Grenze ok, genau 500 Euro und genau 10 Buchungen sind erlaubt" <| fun () ->
            Expect.isOk (checkBotDailyLimit 450m 1 50m) "450 plus 50"
            Expect.isOk (checkBotDailyLimit 0m 9 1m) "die zehnte Buchung"

        testCase "über 500 Euro oder die elfte Buchung wird abgelehnt, die Meldung nennt Summe und Anzahl" <| fun () ->
            match checkBotDailyLimit 450m 1 60m with
            | Error msg ->
                Expect.stringContains msg "450.00 EUR in 1 Buchungen" "Summe und Anzahl"
                Expect.stringContains msg "500.00 EUR" "Grenze"
            | Ok () -> failtest "sollte abgelehnt werden"
            Expect.isError (checkBotDailyLimit 10m 10 1m) "elfte Buchung"

        testCase "Berliner Tag im Winter: 23:30 UTC ist schon der Folgetag, 22:30 UTC noch nicht" <| fun () ->
            Expect.equal (berlinDayOf (zone ()) (utc 2026 11 11 23 30)) (DateTime(2026, 11, 12)) "00:30 Berlin"
            Expect.equal (berlinDayOf (zone ()) (utc 2026 11 11 22 30)) (DateTime(2026, 11, 11)) "23:30 Berlin"

        testCase "Berliner Tag im Sommer: 22:30 UTC ist schon der Folgetag (Offset +2)" <| fun () ->
            Expect.equal (berlinDayOf (zone ()) (utc 2026 7 14 22 30)) (DateTime(2026, 7, 15)) "00:30 Berlin"
            Expect.equal (berlinDayOf (zone ()) (utc 2026 7 14 21 30)) (DateTime(2026, 7, 14)) "23:30 Berlin"

        testCase "Tagesgrenzen in UTC: Winter 23:00 bis 23:00, Sommer 22:00 bis 22:00" <| fun () ->
            Expect.equal (berlinDayBoundsUtc (zone ()) (utc 2026 11 12 9 0)) (utc 2026 11 11 23 0, utc 2026 11 12 23 0) "Winter"
            Expect.equal (berlinDayBoundsUtc (zone ()) (utc 2026 7 15 9 0)) (utc 2026 7 14 22 0, utc 2026 7 15 22 0) "Sommer"

        testCase "Umstellungstage haben 23 und 25 Stunden" <| fun () ->
            let s1, e1 = berlinDayBoundsUtc (zone ()) (utc 2026 3 29 12 0)
            Expect.equal (e1 - s1) (TimeSpan.FromHours 23.0) "Frühjahr"
            let s2, e2 = berlinDayBoundsUtc (zone ()) (utc 2026 10 25 12 0)
            Expect.equal (e2 - s2) (TimeSpan.FromHours 25.0) "Herbst"

        testCase "die Berliner Zeitzone ist auflösbar und liefert die Offsets +1 und +2" <| fun () ->
            // Verhindert den stillen Rückfall auf eigene EU-Regeln, wenn tzdata im Image fehlt.
            match Server.ServerConfig.tryResolveBerlinZone () with
            | Error msg -> failtest msg
            | Ok z ->
                Expect.equal (berlinOffsetAt z (utc 2026 1 15 12 0)) (TimeSpan.FromHours 1.0) "Winter"
                Expect.equal (berlinOffsetAt z (utc 2026 7 1 12 0)) (TimeSpan.FromHours 2.0) "Sommer"
    ]
