module PinnedCategoriesTests

// Angepinnte Kategorien der Übersicht (budgeting-w9yt4, ADR 0087 Punkt 14): Zustand, kein Ereignis (ADR 0083). Die
// Validierung an der Grenze, Speichern und Lesen über die Handler mit In-Memory-SQLite und die echte Verdrahtung
// im Budget-API (Datenbank der Anwendung, Kategorie-Spiegel als Quelle der bekannten Ids). Kein Netz.

open System
open Expecto
open Dapper
open Microsoft.Data.Sqlite
open Shared.Domain
open Server.Budgeting.Categories

do Environment.SetEnvironmentVariable("USE_MEMORY_DB", "true")

let private run (a: Async<'T>) = Async.RunSynchronously a
let private now = DateTime(2026, 11, 11, 9, 0, 0, DateTimeKind.Utc)

let private lebensmittel = Guid.Parse "cccccccc-0000-0000-0000-000000000001"
let private restaurant = Guid.Parse "cccccccc-0000-0000-0000-000000000002"
let private kinderkram = Guid.Parse "cccccccc-0000-0000-0000-000000000003"
let private drogerie = Guid.Parse "cccccccc-0000-0000-0000-000000000004"
let private fremd = Guid.Parse "cccccccc-0000-0000-0000-0000000000ff"

let private mirror = Set.ofList [ lebensmittel; restaurant; kinderkram; drogerie ]
let private knownFromMirror () = async { return mirror }

/// Eine eigene In-Memory-Datenbank mit der Tabelle der Pins; die erste Verbindung hält sie am Leben.
let private isolated () : SqliteConnection * (unit -> SqliteConnection) =
    let connectionString = $"Data Source=pinned-{Guid.NewGuid():N};Mode=Memory;Cache=Shared"
    let anchor = new SqliteConnection(connectionString)
    anchor.Open()
    Budgeting.Persistence.createPinnedCategoriesSchemaOn anchor
    anchor, (fun () -> new SqliteConnection(connectionString))

/// Eine Datenbank ohne die Tabelle: Lesen und Schreiben scheitern.
let private broken () : SqliteConnection * (unit -> SqliteConnection) =
    let connectionString = $"Data Source=pinned-broken-{Guid.NewGuid():N};Mode=Memory;Cache=Shared"
    let anchor = new SqliteConnection(connectionString)
    anchor.Open()
    anchor, (fun () -> new SqliteConnection(connectionString))

[<Tests>]
let validationTests =
    testList "Angepinnte Kategorien: Validierung an der Grenze" [
        testCase "Bekannte Ids ohne Doppel sind erlaubt, auch die leere Liste" <| fun () ->
            Expect.equal (Server.Validation.validatePinnedCategories mirror [ restaurant; lebensmittel ]) (Ok ()) "erlaubt"
            Expect.equal (Server.Validation.validatePinnedCategories mirror []) (Ok ()) "nichts angepinnt"

        testCase "Eine doppelte Id wird mit Meldung abgelehnt" <| fun () ->
            Expect.equal
                (Server.Validation.validatePinnedCategories mirror [ restaurant; lebensmittel; restaurant ])
                (Error [ Server.Validation.PinnedCategoryDuplicateText ])
                "doppelt"

        testCase "Eine leere Id wird abgelehnt" <| fun () ->
            Expect.equal
                (Server.Validation.validatePinnedCategories mirror [ Guid.Empty ])
                (Error [ Server.Validation.PinnedCategoryDuplicateText ])
                "ohne Id"

        testCase "Unbekannte Ids werden je mit ihrer Id abgelehnt, zusammen mit dem Doppel" <| fun () ->
            let other = Guid.NewGuid()
            match Server.Validation.validatePinnedCategories mirror [ fremd; restaurant; restaurant; other ] with
            | Error errors ->
                Expect.equal
                    errors
                    [ Server.Validation.PinnedCategoryDuplicateText
                      Server.Validation.pinnedCategoryUnknownText fremd
                      Server.Validation.pinnedCategoryUnknownText other ]
                    "alle Gründe"
                Expect.stringContains (Server.Validation.pinnedCategoryUnknownText fremd) (string fremd) "die Id steht in der Meldung"
            | Ok () -> failtest "hätte abgelehnt werden müssen"
    ]

[<Tests>]
let handlerTests =
    testList "Angepinnte Kategorien: Handler mit In-Memory-SQLite" [
        testCase "Ohne Pins liest BB eine leere Liste" <| fun () ->
            let _anchor, connect = isolated ()
            Expect.equal (pinnedCategoriesWith connect |> run) (Ok []) "leer"

        testCase "Gespeicherte Pins kommen in ihrer Folge zurück, ein neues Setzen ersetzt die Liste" <| fun () ->
            let _anchor, connect = isolated ()
            let first = setPinnedCategoriesWith connect knownFromMirror now [ lebensmittel; restaurant; kinderkram ] |> run
            Expect.equal first (Ok ()) "gespeichert"
            Expect.equal (pinnedCategoriesWith connect |> run) (Ok [ lebensmittel; restaurant; kinderkram ]) "Folge"
            // Ankreuzen, nach oben schieben, lösen: der Client schickt die ganze neue Liste.
            let second = setPinnedCategoriesWith connect knownFromMirror now [ restaurant; lebensmittel; drogerie ] |> run
            Expect.equal second (Ok ()) "gespeichert"
            Expect.equal (pinnedCategoriesWith connect |> run) (Ok [ restaurant; lebensmittel; drogerie ]) "ersetzt"

        testCase "Eine Ablehnung schreibt nichts" <| fun () ->
            let _anchor, connect = isolated ()
            setPinnedCategoriesWith connect knownFromMirror now [ lebensmittel ] |> run |> ignore
            match setPinnedCategoriesWith connect knownFromMirror now [ restaurant; fremd ] |> run with
            | Error message -> Expect.stringContains message (string fremd) "konkrete Meldung"
            | Ok () -> failtest "unbekannte Kategorie hätte abgelehnt werden müssen"
            match setPinnedCategoriesWith connect knownFromMirror now [ restaurant; restaurant ] |> run with
            | Error message -> Expect.equal message Server.Validation.PinnedCategoryDuplicateText "doppelt"
            | Ok () -> failtest "doppelte Kategorie hätte abgelehnt werden müssen"
            Expect.equal (pinnedCategoriesWith connect |> run) (Ok [ lebensmittel ]) "unverändert"

        testCase "Eine schon angepinnte Kategorie, die der Spiegel nicht mehr kennt, sperrt das Speichern nicht" <| fun () ->
            let _anchor, connect = isolated ()
            let withGone () = async { return Set.add fremd mirror }
            setPinnedCategoriesWith connect withGone now [ fremd; lebensmittel ] |> run |> ignore
            // Jetzt kennt der Spiegel `fremd` nicht mehr; die Id bleibt an ihrem Platz, ein weiterer Pin kommt dazu.
            let result = setPinnedCategoriesWith connect knownFromMirror now [ fremd; lebensmittel; restaurant ] |> run
            Expect.equal result (Ok ()) "gespeichert"
            Expect.equal (pinnedCategoriesWith connect |> run) (Ok [ fremd; lebensmittel; restaurant ]) "Platz bleibt"

        testCase "Lese- und Schreibfehler kommen als kurze Meldung" <| fun () ->
            let _anchor, connect = broken ()
            Expect.equal (pinnedCategoriesWith connect |> run) (Error PinnedCategoriesUnreadableText) "lesen"
            Expect.equal
                (setPinnedCategoriesWith connect knownFromMirror now [ lebensmittel ] |> run)
                (Error PinnedCategoriesUnreadableText)
                "ohne lesbaren Stand wird nicht geschrieben"

        testCase "Scheitert nur das Schreiben, meldet BB „nicht gespeichert“" <| fun () ->
            let _anchor, connect = isolated ()
            use conn = connect ()
            conn.Open()
            // Ein Trigger lässt jedes Einfügen scheitern, das Lesen geht weiter.
            conn.Execute(
                "CREATE TRIGGER pinned_refuse BEFORE INSERT ON budget_pinned_categories BEGIN SELECT RAISE(ABORT, 'nein'); END"
            )
            |> ignore
            Expect.equal
                (setPinnedCategoriesWith connect knownFromMirror now [ lebensmittel ] |> run)
                (Error PinnedCategoriesNotSavedText)
                "schreiben"
            Expect.equal (pinnedCategoriesWith connect |> run) (Ok []) "die Transaktion lässt nichts zurück"
    ]

/// Doktrin Punkt 3: die echte Verdrahtung im Budget-API. Bekannt ist eine Kategorie über den Kategorie-Spiegel
/// (`ledger_categories`), gespeichert wird in der Datenbank der Anwendung. Sequenziell: die Pins sind eine Liste je
/// Datenbank, kein anderer Test schreibt sie.
[<Tests>]
let liveWiringTests =
    testSequenced <| testList "Angepinnte Kategorien: echte Verdrahtung im Budget-API" [
        testCase "budgetApi speichert eine im Spiegel bekannte Kategorie und liest sie zurück; eine unbekannte lehnt es ab" <| fun () ->
            Persistence.initializeDatabase ()
            Ledger.Persistence.initializeLedgerSchema ()
            Budgeting.Persistence.initializeBudgetingSchema ()
            let mirrored = Guid.NewGuid()
            Ledger.Persistence.upsertCategories
                [ { Id = YnabCategoryId mirrored
                    Name = "Pin-Test"
                    GroupName = "Alltag"
                    Available = None
                    Hidden = false } ]
            |> run
            let api = Server.Api.budgetApi (TimeZoneInfo.FindSystemTimeZoneById "Europe/Berlin")
            Expect.equal (api.setPinnedCategories [ mirrored ] |> run) (Ok ()) "gespeichert"
            Expect.equal (api.getPinnedCategories () |> run) (Ok [ mirrored ]) "zurückgelesen"
            let unknown = Guid.NewGuid()
            match api.setPinnedCategories [ mirrored; unknown ] |> run with
            | Error message -> Expect.stringContains message (string unknown) "nicht im Spiegel"
            | Ok () -> failtest "hätte abgelehnt werden müssen"
            Expect.equal (api.setPinnedCategories [] |> run) (Ok ()) "wieder leer"
    ]
