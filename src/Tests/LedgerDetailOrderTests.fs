module LedgerDetailOrderTests

// ledger-zfj78: das Detail einer Buchung nach Prototyp Version 17. Rein und ohne Proxy: Kürzel der Herkunft je
// Akteur, Zeitpunkt-Text und Reihenfolge der Historie (§7.11), die aufklappbare Historie (beim Öffnen zu), Text,
// Art und Wirkung des Prüf-Knopfs, der Kopf („<Tag> · <Konto>“) und „alle zeigen“ am Payee (setzt die Suche,
// schließt nur unter 1024 px). Was nur der Browser zeigt, prüfen die Flows im Detail und der Layout-Sweep
// (`kassenbuch-detail` mit aufgeklappter Historie bei 412 und 1440 px).

open System
open Expecto
open Shared.Domain
open Types
open Components.Ledger.Types
open Components.Ledger.View

let private entry (n: int) (needsReview: bool) : LedgerEntryView =
    { LedgerSelectionTests.entryOf n (-10m * decimal n) needsReview false with LinkedToYnab = true }

let private modelWith (loaded: LedgerEntryView list) : Model =
    let model, _ = Components.Ledger.State.init ()
    { model with
        Entries = Success loaded
        NextPage = 1
        TotalCount = List.length loaded
        HasMoreEntries = false }

let private step (msg: Msg) (model: Model) : Model =
    let model', _, _ = Components.Ledger.State.update msg model
    model'

let private line (sequence: int) (origin: LedgerOrigin) (at: DateTime) (text: string) : LedgerHistoryLine =
    { Sequence = sequence; OccurredAt = at; Origin = origin; Description = text; Amount = None }

[<Tests>]
let historyTests =
    testList "Detail: Historie nach §7.11" [

        testCase "jeder Akteur hat sein Kürzel, in BB und über Quick Add ist es Roman" <| fun () ->
            let tags =
                [ DecidedInBb; AddedViaQuickAdd; AddedByBob; MirroredFromYnab; DecidedByRule; ImportedFromComdirect; MatchedByOrderId ]
                |> List.map historyWhoTag
            Expect.equal tags [ "ROMAN"; "ROMAN"; "BOB"; "YNAB"; "REGEL"; "IMPORT"; "ABGLEICH" ] "Kürzel je Herkunft"

        testCase "der Zeitpunkt steht als TT.MM. HH:MM in Ortszeit" <| fun () ->
            Expect.equal (historyWhenText (DateTime(2026, 11, 6, 18, 40, 0, DateTimeKind.Local))) "06.11. 18:40" "Ortszeit"
            // TZ=Europe/Berlin im Testlauf: 17:05 UTC im November ist 18:05 in Berlin.
            Expect.equal (historyWhenText (DateTime(2026, 11, 6, 17, 5, 0, DateTimeKind.Utc))) "06.11. 18:05" "UTC wird umgerechnet"

        testCase "die Einträge stehen neueste oben, mit Zeitpunkt, Kürzel und Text" <| fun () ->
            let at h = DateTime(2026, 11, 6, h, 0, 0, DateTimeKind.Local)
            let items =
                historyItems
                    [ line 1 ImportedFromComdirect (at 8) "Comdirect-Import: Buchung erfasst"
                      line 2 DecidedByRule (at 9) "Regel: Kategorie gesetzt"
                      line 3 DecidedInBb (at 10) "In BB als geprüft markiert" ]
            Expect.equal
                (items |> List.map (fun i -> i.Key, i.When, i.Who, i.Text, i.Amount))
                [ "3", "06.11. 10:00", "ROMAN", "In BB als geprüft markiert", None
                  "2", "06.11. 09:00", "REGEL", "Regel: Kategorie gesetzt", None
                  "1", "06.11. 08:00", "IMPORT", "Comdirect-Import: Buchung erfasst", None ]
                "neueste oben"

        testCase "ein Eintrag mit Betrag zeigt ihn rechts mit Vorzeichen und Währung" <| fun () ->
            let at = DateTime(2026, 11, 6, 18, 39, 0, DateTimeKind.Local)
            let created = { line 1 MirroredFromYnab at "In YNAB gesehen" with Amount = Some { Amount = -34.99m; Currency = "EUR" } }
            let items = historyItems [ created; line 2 DecidedInBb at "In BB als geprüft markiert" ]
            Expect.equal (items |> List.map (fun i -> i.Amount)) [ None; Some "-34,99 €" ] "Betrag nur beim Anlegen"

        testCase "die aufklappbare Zeile nennt die Zahl der Einträge, solange sie nicht geladen ist keine" <| fun () ->
            let at = DateTime(2026, 11, 6, 8, 0, 0, DateTimeKind.Local)
            Expect.equal (historyToggleLabel (Success [ line 1 DecidedInBb at "a"; line 2 DecidedInBb at "b" ])) "Historie (2)" "geladen"
            Expect.equal (historyToggleLabel (Success [])) "Historie (0)" "leer"
            Expect.equal (historyToggleLabel Loading) "Historie" "lädt"

        testCase "beim Öffnen des Details ist die Historie zu, ein Tipp klappt sie auf und wieder zu" <| fun () ->
            let opened = modelWith [ entry 1 true; entry 2 true ] |> step (OpenDetail (entry 1 true))
            Expect.isFalse (historyOpen opened (entry 1 true)) "beim Öffnen zu"
            let expanded = opened |> step ToggleHistory
            Expect.isTrue (historyOpen expanded (entry 1 true)) "aufgeklappt"
            Expect.isFalse (historyOpen (expanded |> step ToggleHistory) (entry 1 true)) "wieder zu"

        testCase "eine andere Buchung und ein neues Öffnen zeigen die Historie wieder zu" <| fun () ->
            let expanded =
                modelWith [ entry 1 true; entry 2 true ]
                |> step (OpenDetail (entry 1 true))
                |> step ToggleHistory
            let other = expanded |> step (OpenDetail (entry 2 true))
            Expect.isFalse (historyOpen other (entry 2 true)) "die andere Buchung beginnt zu"
            let reopened = expanded |> step CloseDetail |> step (OpenDetail (entry 1 true))
            Expect.isFalse (historyOpen reopened (entry 1 true)) "neu geöffnet zu"

        testCase "ohne offenes Detail klappt nichts auf" <| fun () ->
            let model = modelWith [ entry 1 true ] |> step ToggleHistory
            Expect.equal model.HistoryOpenFor None "nichts aufgeklappt"
    ]

[<Tests>]
let reviewButtonTests =
    testList "Detail: Prüf-Knopf statt Schalter" [

        testCase "ungeprüft heißt der Knopf Geprüft und ist die Hauptaktion, er setzt den Prüfvermerk" <| fun () ->
            let button = detailReviewButtonFor (entry 1 true)
            Expect.equal button.Label "Geprüft" "Text"
            Expect.isTrue button.Primary "Hauptaktion"
            Expect.equal button.Msg (StartSetReviewMark ((entry 1 true).Id, Reviewed)) "derselbe Befehl wie der Schalter an"

        testCase "geprüft heißt der Knopf Als ungeprüft markieren, neutral, er nimmt den Prüfvermerk zurück" <| fun () ->
            let button = detailReviewButtonFor (entry 1 false)
            Expect.equal button.Label "Als ungeprüft markieren" "Text"
            Expect.isFalse button.Primary "neutral"
            Expect.equal button.Msg (StartSetReviewMark ((entry 1 false).Id, Unreviewed)) "derselbe Befehl wie der Schalter aus"
    ]

[<Tests>]
let headTests =
    testList "Detail: Kopf mit Tag und Konto" [

        testCase "der Tag heißt Heute, Gestern oder Wochentag und Datum, mit Jahr nur aus einem anderen Jahr" <| fun () ->
            let today = DateTime(2026, 11, 7)
            let on (d: DateTime) = { entry 1 true with Date = d }
            Expect.equal (detailSubtitleFor today "Kreditkarte" (on today)) "Heute · Kreditkarte" "heute"
            Expect.equal (detailSubtitleFor today "Kreditkarte" (on (today.AddDays -1.0))) "Gestern · Kreditkarte" "gestern"
            Expect.equal (detailSubtitleFor today "Girokonto" (on (DateTime(2026, 11, 3)))) "Di., 03.11. · Girokonto" "dieses Jahr"
            Expect.equal (detailSubtitleFor today "Girokonto" (on (DateTime(2026, 1, 1)))) "Do., 01.01. · Girokonto" "Jahresanfang"
            Expect.equal (detailSubtitleFor today "Girokonto" (on (DateTime(2025, 12, 31)))) "Mi., 31.12.2025 · Girokonto" "Vorjahr mit Jahr"
            // Gestern über den Jahreswechsel bleibt Gestern.
            Expect.equal (detailDayText (DateTime(2026, 1, 1)) (DateTime(2025, 12, 31))) "Gestern" "gestern im Vorjahr"
    ]

[<Tests>]
let payeeShowAllTests =
    testList "Detail: alle zeigen am Payee" [

        let opened (form: DetailForm) =
            { modelWith [ entry 1 true; entry 2 true ] with DetailForm = form }
            |> step (OpenDetail (entry 1 true))

        testCase "am Handy setzt alle zeigen die Suche auf den Payee und schließt das Sheet" <| fun () ->
            let model = opened DetailSheet |> step (FilterByRowPayee "REWE")
            Expect.equal model.Filters.Search (Some "REWE") "Suche gesetzt"
            Expect.equal model.SearchDraft "REWE" "steht im Suchfeld"
            Expect.isNone model.DetailFor "das Sheet ist zu"

        testCase "am PC setzt alle zeigen die Suche und das Panel bleibt mit derselben Buchung offen" <| fun () ->
            let model = opened DetailPanel |> step (FilterByRowPayee "REWE")
            Expect.equal model.Filters.Search (Some "REWE") "Suche gesetzt"
            Expect.equal (model.DetailFor |> Option.map (fun e -> e.Id)) (Some (entry 1 true).Id) "Panel bleibt"

        testCase "alle zeigen an der Kategorie schließt auch am Handy nicht" <| fun () ->
            let categoryId = Guid.Parse "0195c1f0-0000-7000-8000-000000000301"
            let model = opened DetailSheet |> step (FilterByRowCategory categoryId)
            Expect.equal model.Filters.CategoryIds (Set.singleton categoryId) "Kategorie-Filter gesetzt"
            Expect.isSome model.DetailFor "das Sheet bleibt"
    ]
