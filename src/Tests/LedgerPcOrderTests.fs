module LedgerPcOrderTests

// ledger-cnaz3: das Kassenbuch am PC nach Prototyp Version 17. Rein und ohne Proxy: Tasten L und „/“,
// Umschalt+Pfeil und die Pfeile aus der Suche (`keyCommandFor`), Strg- und Umschalt-Klick, das Panel, das der
// Auswahl folgt, die Zusammenfassung der Auswahl, Kopfzeile und Kopf-Aktionen, die Leiste unten. Was nur der
// Browser zeigt (Kästchen, Panel, Tastenleiste), prüfen `[flow:mehrfachauswahl]` und `[tastatur]` in
// `scripts/ledger-selection.test.mjs` und der Layout-Sweep.

open System
open Expecto
open Shared.Domain
open Types
open Components.Ledger.Types
open Components.Ledger.View

let private eur (amount: decimal) : Money = { Amount = amount; Currency = "EUR" }

let private entry (n: int) (amount: decimal) : LedgerEntryView =
    { LedgerSelectionTests.entryOf n amount true false with LinkedToYnab = true }

let private idOf (n: int) = (entry n 0m).Id

let private rows (count: int) = [ for n in 1..count -> entry n (-10m * decimal n) ]

let private modelWith (loaded: LedgerEntryView list) : Model =
    let model, _ = Components.Ledger.State.init ()
    { model with
        Entries = Success loaded
        NextPage = 1
        TotalCount = List.length loaded
        TotalAmount = eur (loaded |> List.sumBy (fun e -> e.Amount.Amount))
        HasMoreEntries = false
        LenaFlagColor = Success (Some "purple") }

/// Am PC steht das Detail im Panel (die Ansicht meldet es mit `DetailFormChanged`).
let private asPanel (model: Model) = { model with DetailForm = DetailPanel }

/// Ein Schritt ohne die Befehle auszuführen: manche greifen auf das DOM zu (Fokus in die Suche).
let private step (msg: Msg) (model: Model) : Model =
    let model', _, _ = Components.Ledger.State.update msg model
    model'

let private picked (ns: int list) = Picked (ns |> List.map idOf |> Set.ofList)

[<Tests>]
let keyTests =
    testList "Kassenbuch am PC: Tasten" [

        testCase "L fragt Lena, „/“ springt in die Suche" <| fun () ->
            let key k = keyCommandFor "BODY" false false false false false k
            Expect.equal (key "l") (Some LenaKeyPressed) "l"
            Expect.equal (key "L") (Some LenaKeyPressed) "L"
            Expect.equal (key "/") (Some FocusSearch) "/"
            for tag in [ "INPUT"; "TEXTAREA" ] do
                Expect.isNone (keyCommandFor tag false false false false false "l") $"l im Feld {tag}"
                Expect.isNone (keyCommandFor tag false false false false false "/") $"/ im Feld {tag}"
            Expect.isNone (keyCommandFor "BODY" false false true false false "l") "Strg+L bleibt dem Browser"

        testCase "Umschalt+Pfeil erweitert die Auswahl, ohne Umschalt bewegt er" <| fun () ->
            Expect.equal (keyCommandFor "BODY" false false false false true "ArrowDown") (Some (ExtendSelection 1)) "⇧+↓"
            Expect.equal (keyCommandFor "BODY" false false false false true "ArrowUp") (Some (ExtendSelection -1)) "⇧+↑"
            Expect.equal (keyCommandFor "BODY" false false false false false "ArrowDown") (Some (MoveFocus 1)) "↓"

        testCase "aus der Suche wirken nur die Pfeile, jede andere Taste gehört dem Feld" <| fun () ->
            let inSearch shift k = keyCommandFor "INPUT" false true false false shift k
            Expect.equal (inSearch false "ArrowDown") (Some (MoveFocus 1)) "↓"
            Expect.equal (inSearch false "ArrowUp") (Some (MoveFocus -1)) "↑"
            Expect.equal (inSearch true "ArrowDown") (Some (ExtendSelection 1)) "⇧+↓"
            for k in [ "g"; "G"; "l"; "j"; "k"; "/"; " "; "Enter"; "Escape"; "a" ] do
                Expect.isNone (inSearch false k) $"{k} schreibt in die Suche"
            Expect.isNone (keyCommandFor "INPUT" false true true false false "ArrowDown") "Strg+↓ bleibt dem Feld"
            Expect.isNone (keyCommandFor "INPUT" false false false false false "ArrowDown") "in einem anderen Feld nichts"

        testCase "die Taste L fragt Lena für die markierte Zeile, mit Auswahl für die Auswahl" <| fun () ->
            let focused = { modelWith (rows 3) with FocusedRow = Some (idOf 2) }
            let asked = step LenaKeyPressed focused
            Expect.equal (asked.FlagRows |> Map.tryFind (idOf 2)) (Some FlagSending) "die markierte Zeile wird gefragt"
            let selecting = { focused with Selection = picked [ 1; 3 ] }
            let batch = step LenaKeyPressed selecting
            Expect.equal batch.BatchRunning (Some LenaBatchRun) "die Auswahl wird gefragt"
            Expect.isTrue (Map.isEmpty batch.FlagRows) "nicht die markierte Zeile"

        testCase "L nimmt keine Rückfrage zurück und tut ohne Markierung nichts" <| fun () ->
            let asked = { modelWith [ { entry 1 -10m with Flag = AskLena } ] with FocusedRow = Some (idOf 1) }
            Expect.isTrue (Map.isEmpty (step LenaKeyPressed asked).FlagRows) "schon gefragt: nichts"
            Expect.equal (step LenaKeyPressed (modelWith (rows 2))) (modelWith (rows 2)) "ohne Markierung nichts"

        // Bug aus dem Review zu ledger-cnaz3: ohne Lena-Farbe oder auf einer gesperrten Zeile verwarf L still. Der Knopf
        // nennt den Grund, die Taste jetzt auch, als Rückmeldung.
        testCase "L meldet den Grund, wenn „Lena fragen“ gesperrt ist" <| fun () ->
            let reasonOf (model: Model) =
                let model', _, external = Components.Ledger.State.update LenaKeyPressed model
                Expect.isTrue (Map.isEmpty model'.FlagRows) "nichts gesendet"
                Expect.isNone model'.BatchRunning "kein Sammelaufruf"
                match external with
                | ShowToast (text, _) -> Some text
                | _ -> None
            let noColor = { modelWith (rows 2) with LenaFlagColor = Success None; FocusedRow = Some (idOf 1) }
            Expect.equal (reasonOf noColor) (selectionLenaDisabledReason noColor.LenaFlagColor) "Zeile ohne Farbe"
            Expect.isSome (reasonOf noColor) "ein Grund steht da"
            Expect.equal
                (reasonOf { noColor with Selection = picked [ 1; 2 ] })
                (selectionLenaDisabledReason noColor.LenaFlagColor)
                "Auswahl ohne Farbe"
            let deleted = modelWith [ { entry 1 -10m with DeletedInYnab = true } ]
            Expect.equal (reasonOf { deleted with FocusedRow = Some (idOf 1) }) (Some lenaDeletedReason) "in YNAB gelöscht"
            let _, _, fine = Components.Ledger.State.update LenaKeyPressed { modelWith (rows 2) with FocusedRow = Some (idOf 1) }
            Expect.equal fine NoOp "mit Farbe keine Meldung"

        testCase "„/“ setzt einen Befehl ab, bei offenem Popover nicht" <| fun () ->
            let _, cmd, _ = Components.Ledger.State.update FocusSearch (modelWith (rows 2))
            Expect.isNonEmpty cmd "Fokus in die Suche"
            let _, blocked, _ = Components.Ledger.State.update FocusSearch { modelWith (rows 2) with OpenPopover = Some DateChip }
            Expect.isEmpty blocked "das Popover hat die Tasten"
    ]

[<Tests>]
let selectionTests =
    testList "Kassenbuch am PC: Strg-, Umschalt-Klick und Umschalt+Pfeil (§7.7)" [

        testCase "ein Klick ohne Taste öffnet das Detail, Strg/Cmd und Umschalt wählen" <| fun () ->
            let e = entry 2 -20m
            Expect.equal (rowClickMsg e { CtrlOrMeta = false; Shift = false }) (OpenDetail e) "ohne Taste"
            Expect.equal (rowClickMsg e { CtrlOrMeta = true; Shift = false }) (ClickRowWithModifier (e.Id, ToggleRow)) "Strg/Cmd"
            Expect.equal (rowClickMsg e { CtrlOrMeta = false; Shift = true }) (ClickRowWithModifier (e.Id, RangeToRow)) "Umschalt"
            Expect.equal (rowClickMsg e { CtrlOrMeta = true; Shift = true }) (ClickRowWithModifier (e.Id, ToggleRow)) "beide: Strg gewinnt"

        testCase "Strg-Klick nimmt eine Zeile in die Auswahl und wieder heraus" <| fun () ->
            let one = modelWith (rows 4) |> step (ClickRowWithModifier (idOf 2, ToggleRow))
            Expect.equal one.Selection (picked [ 2 ]) "dazu"
            let two = one |> step (ClickRowWithModifier (idOf 4, ToggleRow))
            Expect.equal two.Selection (picked [ 2; 4 ]) "einzeln, keine Spanne"
            Expect.equal (two |> step (ClickRowWithModifier (idOf 2, ToggleRow))).Selection (picked [ 4 ]) "heraus"

        testCase "Umschalt-Klick wählt den Bereich ab der zuletzt gewählten Zeile" <| fun () ->
            let model = modelWith (rows 5) |> step (ClickRowWithModifier (idOf 2, ToggleRow)) |> step (ClickRowWithModifier (idOf 4, RangeToRow))
            Expect.equal model.Selection (picked [ 2; 3; 4 ]) "2 bis 4"
            Expect.equal model.SelectionAnchor (Some (idOf 2)) "der Anker bleibt"
            let back = model |> step (ClickRowWithModifier (idOf 1, RangeToRow))
            Expect.equal back.Selection (picked [ 1; 2; 3; 4 ]) "auch nach oben"

        testCase "Umschalt-Klick ohne Anker wählt nur die Zeile" <| fun () ->
            let model = modelWith (rows 3) |> step (ClickRowWithModifier (idOf 3, RangeToRow))
            Expect.equal model.Selection (picked [ 3 ]) "nur die Zeile"

        testCase "ist eine Buchung im Panel offen, nimmt Strg-Klick sie mit in die Auswahl" <| fun () ->
            let opened = modelWith (rows 4) |> asPanel |> step (OpenDetail (entry 1 -10m))
            let model = opened |> step (ClickRowWithModifier (idOf 3, ToggleRow))
            Expect.equal model.Selection (picked [ 1; 3 ]) "offene und geklickte Zeile"
            let range = opened |> step (ClickRowWithModifier (idOf 3, RangeToRow))
            Expect.equal range.Selection (picked [ 1; 2; 3 ]) "Bereich ab der offenen Zeile"

        testCase "Umschalt+↓ nimmt die markierte und die nächste Zeile in die Auswahl" <| fun () ->
            let start = { modelWith (rows 4) with FocusedRow = Some (idOf 1) }
            let once = step (ExtendSelection 1) start
            Expect.equal once.Selection (picked [ 1; 2 ]) "1 und 2"
            Expect.equal once.FocusedRow (Some (idOf 2)) "die Markierung rückt"
            let twice = step (ExtendSelection 1) once
            Expect.equal twice.Selection (picked [ 1; 2; 3 ]) "dazu 3"
            let up = step (ExtendSelection -1) { modelWith (rows 4) with FocusedRow = Some (idOf 3) }
            Expect.equal up.Selection (picked [ 2; 3 ]) "nach oben"

        // Bug aus dem Review zu ledger-cnaz3: stand die Markierung auf einer ausgegrauten Zeile, fand `movedFocus` sie in
        // den wählbaren Zeilen nicht und sprang mit Umschalt+↓ auf die erste Zeile der Liste.
        testCase "Umschalt+↓ auf einer ausgegrauten Zeile wählt die nächste wählbare darunter" <| fun () ->
            let greyed = { modelWith (rows 5) with FilteredOut = Set.ofList [ idOf 3; idOf 4 ]; FocusedRow = Some (idOf 3) }
            let down = step (ExtendSelection 1) greyed
            Expect.equal down.Selection (picked [ 5 ]) "die nächste wählbare, nicht die erste"
            Expect.equal down.FocusedRow (Some (idOf 5)) "die Markierung rückt dorthin"
            let up = step (ExtendSelection -1) greyed
            Expect.equal up.Selection (picked [ 2 ]) "nach oben die nächste wählbare"
            let atEnd = step (ExtendSelection 1) { greyed with FilteredOut = Set.ofList [ idOf 5 ]; FocusedRow = Some (idOf 5) }
            Expect.equal atEnd.Selection NoSelection "darunter keine wählbare: nichts"
            Expect.equal atEnd.FocusedRow (Some (idOf 5)) "die Markierung bleibt"

        testCase "Umschalt+↓ ohne Markierung wählt die erste Zeile, bei offenem Popover nichts" <| fun () ->
            Expect.equal (step (ExtendSelection 1) (modelWith (rows 3))).Selection (picked [ 1 ]) "erste Zeile"
            let blocked = { modelWith (rows 3) with OpenPopover = Some CategoryChip; FocusedRow = Some (idOf 1) }
            Expect.equal (step (ExtendSelection 1) blocked).Selection NoSelection "das Popover hat die Tasten"
    ]

[<Tests>]
let panelTests =
    testList "Kassenbuch am PC: das Panel folgt der Auswahl (ADR 0087 Punkt 17)" [

        testCase "bei genau einer gewählten Buchung zeigt das Panel ihr Detail" <| fun () ->
            let model = modelWith (rows 3) |> asPanel |> step (ClickRowCheckbox (idOf 2, false))
            Expect.equal (model.DetailFor |> Option.map (fun d -> d.Id)) (Some (idOf 2)) "Detail der gewählten"
            Expect.equal (panelContentFor model) PanelDetail "Panel: Detail"
            Expect.equal model.History Loading "die Historie lädt"

        testCase "ab zwei gewählten wird das Panel zur Zusammenfassung, das Detail schließt" <| fun () ->
            let model =
                modelWith (rows 3) |> asPanel |> step (ClickRowCheckbox (idOf 2, false)) |> step (ClickRowCheckbox (idOf 3, false))
            Expect.isNone model.DetailFor "Detail zu"
            Expect.equal (panelContentFor model) PanelSelectionSummary "Zusammenfassung"
            let back = model |> step (ClickRowCheckbox (idOf 3, false))
            Expect.equal (back.DetailFor |> Option.map (fun d -> d.Id)) (Some (idOf 2)) "zurück auf eine: wieder ihr Detail"

        testCase "Esc schließt das Detail der einen gewählten, die Auswahl bleibt" <| fun () ->
            let model = modelWith (rows 3) |> asPanel |> step (ClickRowCheckbox (idOf 2, false)) |> step EscapePressed
            Expect.isNone model.DetailFor "Detail zu"
            Expect.equal model.Selection (picked [ 2 ]) "Auswahl bleibt"
            Expect.equal (panelContentFor model) PanelEmpty "leeres Panel"

        testCase "mit Sheet (unter 1024 px) schließt eine beginnende Auswahl das Detail wie bisher" <| fun () ->
            let opened = modelWith (rows 3) |> step (OpenDetail (entry 1 -10m))
            let model = opened |> step (ClickRowCheckbox (idOf 2, false))
            Expect.isNone model.DetailFor "Detail zu (ADR 0053 Punkt 2 bleibt am Handy)"

        testCase "ohne Auswahl und Detail ist das Panel leer" <| fun () ->
            Expect.equal (panelContentFor (modelWith (rows 2))) PanelEmpty "leer"
            Expect.equal emptyPanelText "Buchung wählen, um Details zu sehen." "Satz"
    ]

[<Tests>]
let summaryTests =
    testList "Kassenbuch am PC: Zusammenfassung der Auswahl (Prototyp selSummary)" [

        let category (n: int) (name: string) (amount: decimal) (needsReview: bool) =
            { entry n amount with CategoryName = Some name; NeedsReview = needsReview }

        testCase "Summe, Ausgaben, Einnahmen und Anzahl zu prüfen" <| fun () ->
            let summary =
                selectionSummaryOf [
                    category 1 "Lebensmittel" -42.80m true
                    category 2 "Gehalt" 3950m false
                    category 3 "Lebensmittel" -18.40m true
                ]
            Expect.equal summary.Count 3 "Anzahl"
            Expect.equal summary.Sum 3888.80m "Summe"
            Expect.equal summary.Outflows -61.20m "Ausgaben"
            Expect.equal summary.Inflows 3950m "Einnahmen"
            Expect.equal summary.ToReview 2 "zu prüfen"

        testCase "nach Kategorie: zusammengefasst, von der größten Ausgabe zur größten Einnahme" <| fun () ->
            let summary =
                selectionSummaryOf [
                    category 1 "Geschenke" -34.99m false
                    category 2 "Lebensmittel" -42.80m false
                    category 3 "Gehalt" 3950m false
                    { entry 4 -18.40m with CategoryName = None }
                    category 5 "Lebensmittel" -8.60m false
                ]
            Expect.equal
                (summary.ByCategory |> List.map (fun l -> l.Label, l.Amount, l.WithoutCategory))
                [ "Lebensmittel", -51.40m, false
                  "Geschenke", -34.99m, false
                  "ohne Kategorie", -18.40m, true
                  "Gehalt", 3950m, false ]
                "Reihenfolge und Summen"

        testCase "eine Split-Buchung geht mit ihren Zeilen ein" <| fun () ->
            let split (target: SplitTarget) (amount: decimal) = { Target = target; Amount = eur amount; Memo = None }
            let catId = YnabCategoryId (Guid.Parse "0195f900-1111-7000-8000-000000000001")
            let otherId = YnabCategoryId (Guid.Parse "0195f900-1111-7000-8000-000000000002")
            let splitEntry =
                { entry 1 -100m with
                    Splits = [ split (ToCategory (catId, "Drogerie")) -60m; split (ToCategory (otherId, "Haushalt")) -30m; split Uncategorized -10m ] }
            let summary = selectionSummaryOf [ splitEntry; category 2 "Haushalt" -5m false ]
            Expect.equal
                (summary.ByCategory |> List.map (fun l -> l.Label, l.Amount))
                [ "Drogerie", -60m; "Haushalt", -35m; "ohne Kategorie", -10m ]
                "Zeilen statt Buchung"
            Expect.equal summary.Outflows -105m "Ausgaben je Buchung"

        testCase "in YNAB gelöschte Buchungen zählen, gehen aber in keine Summe ein" <| fun () ->
            let summary = selectionSummaryOf [ category 1 "Strom" -95m false; { category 2 "Strom" -10m false with DeletedInYnab = true } ]
            Expect.equal summary.Count 2 "Anzahl"
            Expect.equal summary.Sum -95m "Summe ohne die gelöschte"

        testCase "Kopf, Hinweis und der Satz, wenn nicht alle gewählten geladen sind" <| fun () ->
            Expect.equal (selectionSummaryTitle 3) "3 Buchungen ausgewählt" "Kopf"
            Expect.equal selectionSummaryHint "Umschalt-Klick wählt einen Bereich, Strg- oder Cmd-Klick einzelne" "Hinweis"
            Expect.isNone (selectionPartialNote 3 3) "alle geladen"
            Expect.equal (selectionPartialNote 34 12) (Some "Ausgaben, Einnahmen und Kategorien aus den 12 geladenen Buchungen.") "nur die geladenen"

        testCase "„Rückfrage zurücknehmen“ nur, wenn eine gewählte Buchung gefragt ist" <| fun () ->
            let model = modelWith [ entry 1 -1m; { entry 2 -1m with Flag = AskLena }; entry 3 -1m ]
            Expect.isFalse (selectionOffersWithdraw { model with Selection = picked [ 1; 3 ] }) "keine gefragt"
            Expect.isTrue (selectionOffersWithdraw { model with Selection = picked [ 1; 2 ] }) "eine gefragt"

        // Bug aus dem Review zu ledger-cnaz3: unter „alle“ sah die Zusammenfassung nur die geladenen Buchungen. War die
        // gefragte noch nicht geladen, fehlte „Rückfrage zurücknehmen“, obwohl der Zähler sie kennt.
        testCase "„Rückfrage zurücknehmen“ unter „alle“ auch für eine gefragte, die noch nicht geladen ist" <| fun () ->
            let all = { modelWith (rows 2) with Selection = AllExcept Set.empty; TotalCount = 40 }
            Expect.isTrue (selectionOffersWithdraw { all with AskLenaCount = 1 }) "der Zähler kennt eine gefragte"
            Expect.isFalse (selectionOffersWithdraw { all with AskLenaCount = 0 }) "keine gefragt"

        // Bug aus dem Review zu ledger-cnaz3: unter „alle“ lieferte `singleSelectedEntry` nie eine Buchung. Blieb nach
        // dem Abwählen genau eine übrig, zeigte die Leiste eine Buchung, das Panel aber weder Detail noch Zusammenfassung.
        testCase "unter „alle“ mit genau einer übrigen geladenen Buchung ist sie die eine gewählte" <| fun () ->
            let all = { modelWith (rows 3) with Selection = AllExcept (Set.ofList [ idOf 1; idOf 2 ]) }
            Expect.equal (singleSelectedEntry all |> Option.map (fun e -> e.Id)) (Some (idOf 3)) "die übrige"
            Expect.isNone (singleSelectedEntry { all with TotalCount = 5 }) "es fehlen noch nicht geladene"
            let unloaded = { modelWith (rows 3) with Selection = AllExcept (Set.ofList [ idOf 1; idOf 2; idOf 3 ]); TotalCount = 4 }
            Expect.isNone (singleSelectedEntry unloaded) "die eine übrige ist nicht geladen"

        testCase "unter „alle“ zeigt das Panel das Detail, wenn nach dem Abwählen eine übrig bleibt" <| fun () ->
            let all = modelWith (rows 2) |> asPanel |> step ToggleSelectAll
            Expect.equal (panelContentFor all) PanelSelectionSummary "zwei: Zusammenfassung"
            let one = all |> step (ClickRowCheckbox (idOf 1, false))
            Expect.equal (one.DetailFor |> Option.map (fun d -> d.Id)) (Some (idOf 2)) "eine übrig: ihr Detail"
            Expect.equal (panelContentFor one) PanelDetail "Detail im Panel"

        testCase "die gewählten Buchungen kommen aus der Liste, unter AllExcept ohne die Ausnahmen" <| fun () ->
            let model = { modelWith (rows 3) with Selection = AllExcept (Set.singleton (idOf 2)) }
            Expect.equal (selectedEntries model |> List.map (fun e -> e.Id)) [ idOf 1; idOf 3 ] "ohne Ausnahme"
    ]

[<Tests>]
let barTests =
    testList "Kassenbuch am PC: die Leiste unten" [

        testCase "ohne Auswahl die Tastenleiste, bei einer Buchung alle Aktionen, ab zwei nur Anzahl, Summe, Aufheben" <| fun () ->
            let model = modelWith (rows 3)
            Expect.equal (desktopBarFor model) KeysBar "ohne Auswahl"
            Expect.equal (desktopBarFor { model with Selection = picked [ 1 ] }) SingleSelectionBar "eine"
            Expect.equal (desktopBarFor { model with Selection = picked [ 1; 2 ] }) CompactSelectionBar "zwei"
            Expect.equal (desktopBarFor { model with Entries = Success [] }) NoDesktopBar "leere Liste: nichts"
    ]

[<Tests>]
let headerTests =
    testList "Kassenbuch am PC: Kopf" [

        testCase "„n durchgehen“ steht nur bei n > 0, zwischen „Abrufen“ und „+ Buchung“" <| fun () ->
            let pc (actions: LedgerHeaderAction list) =
                actions |> List.filter (fun a -> a <> FetchIcon && a <> MirrorIcon)
            Expect.equal
                (pc (headerActionsFor false 4))
                [ MirrorButton; FetchButton; ReviewButton 4; CaptureBooking ]
                "Jetzt spiegeln, Abrufen, 4 durchgehen, + Buchung"
            Expect.equal (pc (headerActionsFor false 0)) [ MirrorButton; FetchButton; CaptureBooking ] "ohne Zu prüfen kein Knopf"
            Expect.isFalse (headerActionsFor true 4 |> List.exists (function ReviewButton _ -> true | _ -> false)) "nicht auf „Regeln“"
            Expect.equal (reviewButtonLabel 4) "4 durchgehen" "Text"

        testCase "die Zahl kommt in „Alle Konten“ aus dem Kassenbuch, sonst von der Übersicht" <| fun () ->
            Expect.equal (headerReviewCount true true 3 7) 3 "Alle Konten, geladen"
            Expect.equal (headerReviewCount true false 3 7) 7 "Alle Konten, lädt noch"
            Expect.equal (headerReviewCount false true 1 7) 7 "ein Konto: das ganze Kassenbuch"

        testCase "die Zeile unter dem Titel: Konto oder Alle Konten, Saldo, Stand des Spiegels" <| fun () ->
            let accounts: LedgerAccountView list =
                [ { Id = Guid.Parse "0195e900-0000-7000-8000-000000000001"; Name = "Girokonto"; YnabAccountId = None; Closed = false; Kind = LedgerBankAccount }
                  { Id = Guid.Parse "0195e900-0000-7000-8000-000000000002"; Name = "Bar"; YnabAccountId = None; Closed = false; Kind = LedgerBankAccount } ]
            let balances =
                Success [
                    { AccountId = accounts.[0].Id; Balance = eur 2418.07m }
                    { AccountId = accounts.[1].Id; Balance = eur 244.33m } ]
            let all = ledgerHeaderLine accounts balances (Some AllAccounts) (Some "Gespiegelt heute 10:12")
            Expect.equal all { Account = "Alle Konten"; Balance = Some (eur 2662.40m); Mirror = Some "Gespiegelt heute 10:12" } "Alle Konten"
            let one = ledgerHeaderLine accounts balances (Some (OneAccount accounts.[1].Id)) None
            Expect.equal one { Account = "Bar"; Balance = Some (eur 244.33m); Mirror = None } "ein Konto"
            let loading = ledgerHeaderLine accounts Loading (Some AllAccounts) None
            Expect.isNone loading.Balance "beim Laden kein Saldo"
    ]

[<Tests>]
let filterBarTests =
    testList "Kassenbuch am PC: Filterleiste" [

        testCase "Reihenfolge: Zu prüfen, Lena, Ohne Kategorie, Kategorie, Zeitraum, Betrag" <| fun () ->
            Expect.equal fixedChipOrder [ NeedsReviewChip; AskLenaChip; WithoutCategoryChip; CategoryChip; DateChip; AmountChip ] "Prototyp"

        testCase "Umschalter mit Zahl, Kategorie mit Namen" <| fun () ->
            let model = { modelWith (rows 1) with NeedsReviewCount = 4; AskLenaCount = 2; WithoutCategoryCount = 1 }
            Expect.equal (pcChipLabel model [] NeedsReviewChip, pcChipCount model NeedsReviewChip) ("Zu prüfen", Some 4) "Zu prüfen 4"
            Expect.equal (pcChipLabel model [] AskLenaChip, pcChipCount model AskLenaChip) ("Lena", Some 2) "Lena 2"
            Expect.equal (pcChipLabel model [] WithoutCategoryChip, pcChipCount model WithoutCategoryChip) ("Ohne Kategorie", Some 1) "Ohne Kategorie 1"
            Expect.equal (pcChipLabel model [] CategoryChip) "Kategorie" "ohne Auswahl"
            Expect.equal (pcChipLabel model [ "Strom" ] CategoryChip) "Kategorie: Strom" "eine"
            Expect.equal (pcChipLabel model [ "Strom"; "Wasser"; "Gas" ] CategoryChip) "Kategorie: Strom +2" "mehrere"
            Expect.isNone (pcChipCount model DateChip) "Zeitraum ohne Zahl"

        testCase "„Filter löschen“ steht, sobald ein Filter, ein Umschalter oder die Suche gesetzt ist" <| fun () ->
            let none = noFilters
            Expect.isFalse (clearFiltersChipVisible false none) "nichts gesetzt"
            Expect.isTrue (clearFiltersChipVisible true none) "Zu prüfen"
            Expect.isTrue (clearFiltersChipVisible false { none with OnlyAskLena = true }) "Lena"
            Expect.isTrue (clearFiltersChipVisible false { none with WithoutCategory = true }) "Ohne Kategorie"
            Expect.isTrue (clearFiltersChipVisible false { none with Search = Some "rewe" }) "Suche"
            Expect.isTrue (clearFiltersChipVisible false { none with AmountDirection = OnlyOutflows }) "Richtung"
    ]

// ============================================
// Die Auswahl hält nur sichtbare, wählbare Buchungen (ledger-cnaz3, Verifier-FAIL Iteration 1)
// ============================================

// Fehler aus Iteration 1: `ClickRowWithModifier` nahm die im Panel offene Buchung ungeprüft als erste gewählte mit,
// auch wenn sie ausgegraut war (per Bearbeitung aus dem Filter gefallen) oder gar nicht in der geladenen Liste stand
// (über `#/ledger/entry/<id>` geöffnet). Ebenso blieb eine gewählte Buchung gewählt, wenn sie im Detail aus dem Filter
// fiel. Dann zeigte die Leiste eine Auswahl ohne sichtbares Häkchen, und eine Sammelaktion („Alle geprüft“,
// „Kategorie setzen“, „Lena fragen“) traf auch diese Buchung.

let private withoutCategory (model: Model) = { model with Filters = { model.Filters with WithoutCategory = true } }

/// Die Kategorie, die eine Buchung im Detail bekommt: bei aktivem Chip „Ohne Kategorie“ fällt sie aus dem Filter.
let private categorySet (n: int) : Msg =
    let outcome : CategoryChangeOutcome =
        { Previous = None; Change = CategoryChanged; WriteBack = CategorySent; PreviousTransferAccountId = None }
    EntryCategorySet (idOf n, Some (YnabCategoryId (Guid.Parse "0195c100-0000-7000-8000-000000000002")), Ok outcome)

/// „Rückfrage zurücknehmen“ im Detail: bei aktivem Chip „Lena“ fällt die Buchung aus dem Filter.
let private lenaWithdrawn (n: int) : Msg =
    let outcome : FlagOutcome =
        { Previous = { Color = Some "purple"; Name = Some "Lena" }; Change = FlagChanged; WriteBack = FlagSent }
    LenaFlagSet (idOf n, WithdrawLena, Ok outcome)

/// Vier Buchungen ohne Kategorie bei aktivem Chip „Ohne Kategorie“, Buchung 1 im Panel offen und dort mit einer
/// Kategorie versehen: sie steht ausgegraut, das Detail bleibt offen.
let private greyedOpen () : Model =
    modelWith (rows 4) |> withoutCategory |> asPanel |> step (OpenDetail (entry 1 -10m)) |> step (categorySet 1)

[<Tests>]
let visibleSelectionTests =
    testList "Kassenbuch am PC: die Auswahl hält nur sichtbare Buchungen" [

        testCase "ausgegraute offene Buchung plus Strg-Klick: nur die geklickte ist gewählt" <| fun () ->
            let start = greyedOpen ()
            Expect.isTrue (start.FilteredOut.Contains (idOf 1)) "Vorbedingung: 1 ist ausgegraut"
            Expect.equal (start.DetailFor |> Option.map (fun d -> d.Id)) (Some (idOf 1)) "Vorbedingung: 1 ist offen"
            let model = start |> step (ClickRowWithModifier (idOf 3, ToggleRow))
            Expect.equal model.Selection (picked [ 3 ]) "die ausgegraute 1 kommt nicht mit"

        testCase "ausgegraute offene Buchung plus Umschalt-Klick: kein Bereich ab ihr" <| fun () ->
            let model = greyedOpen () |> step (ClickRowWithModifier (idOf 3, RangeToRow))
            Expect.equal model.Selection (picked [ 3 ]) "ohne brauchbaren Anker nur die geklickte Zeile"

        testCase "nicht geladene Route-Buchung plus Strg-Klick: nur die geklickte ist gewählt" <| fun () ->
            let routed = entry 9 -90m
            let start =
                { modelWith (rows 3) with SelectedFilter = Some AllAccounts; PendingRouteEntry = Some routed.Id }
                |> asPanel
                |> step (RouteEntryLoaded (routed.Id, Ok (Some routed)))
            Expect.equal (start.DetailFor |> Option.map (fun d -> d.Id)) (Some routed.Id) "Vorbedingung: Route-Buchung offen"
            let model = start |> step (ClickRowWithModifier (idOf 2, ToggleRow))
            Expect.equal model.Selection (picked [ 2 ]) "die nicht geladene Buchung kommt nicht mit"
            let range = start |> step (ClickRowWithModifier (idOf 2, RangeToRow))
            Expect.equal range.Selection (picked [ 2 ]) "auch Umschalt nimmt sie nicht"

        testCase "einzeln gewählte Buchung, im Detail ausgegraut: die Auswahl endet, das Detail bleibt" <| fun () ->
            let chosen = modelWith (rows 3) |> withoutCategory |> asPanel |> step (ClickRowCheckbox (idOf 1, false))
            Expect.equal (chosen.DetailFor |> Option.map (fun d -> d.Id)) (Some (idOf 1)) "Vorbedingung: ihr Detail"
            let model = chosen |> step (categorySet 1)
            Expect.equal model.Selection NoSelection "keine Auswahl ohne Häkchen"
            Expect.isFalse (hasSelection model) "keine Leiste"
            Expect.equal (model.DetailFor |> Option.map (fun d -> d.Id)) (Some (idOf 1)) "das Detail bleibt offen"
            Expect.isNone (Components.Ledger.State.reviewMarkBatchSelection model) "keine Sammelaktion"

        testCase "nicht geladene Route-Buchung plus Leertaste: nichts gewählt, danach nur die geklickte" <| fun () ->
            // Fehler aus Iteration 2: `RouteEntryLoaded` setzt `FocusedRow` auf die Route-Buchung, auch wenn sie nicht
            // in `Entries` steht. Die Leertaste (`ToggleFocusedSelected` → `ClickRowCheckbox`) nahm sie in `Picked`
            // (Anzahl 0), ein Strg-Klick danach ergab „1 ausgewählt“ bei `BatchPicked [2; 9]`.
            let routed = entry 9 -90m
            let start =
                { modelWith (rows 3) with SelectedFilter = Some AllAccounts; PendingRouteEntry = Some routed.Id }
                |> asPanel
                |> step (RouteEntryLoaded (routed.Id, Ok (Some routed)))
            let spaced = start |> step ToggleFocusedSelected
            Expect.equal spaced.Selection NoSelection "die Leertaste wählt die nicht geladene Buchung nicht"
            for name, msg in [ "Strg-Klick", ClickRowWithModifier (idOf 2, ToggleRow); "Kästchen", ClickRowCheckbox (idOf 2, false) ] do
                let model = spaced |> step msg
                Expect.equal model.Selection (picked [ 2 ]) $"{name}: nur die geklickte"
                Expect.equal (Components.Ledger.State.reviewMarkBatchSelection model) (Some (BatchPicked [ idOf 2 ])) $"{name}: Sammelauftrag"

        testCase "nicht geladene oder ausgegraute Buchung: langes Drücken und Tippen wählen sie nicht" <| fun () ->
            let greyed = modelWith (rows 3) |> withoutCategory |> step (categorySet 1)
            Expect.equal (greyed |> step (StartSelectionMode (idOf 1))).Selection NoSelection "ausgegraut: langes Drücken"
            Expect.equal (greyed |> step (ToggleRowSelected (idOf 1))).Selection NoSelection "ausgegraut: Tippen"
            let unknown = (entry 9 0m).Id
            Expect.equal (modelWith (rows 3) |> step (StartSelectionMode unknown)).Selection NoSelection "nicht geladen: langes Drücken"
            Expect.equal (modelWith (rows 3) |> step (ToggleRowSelected unknown)).Selection NoSelection "nicht geladen: Tippen"
            Expect.equal (modelWith (rows 3) |> step (ClickRowCheckbox (unknown, false))).Selection NoSelection "nicht geladen: Kästchen"
            Expect.equal (modelWith (rows 3) |> step (ClickRowWithModifier (unknown, ToggleRow))).Selection NoSelection "nicht geladen: Strg-Klick"

        testCase "Umschalt-Klick über eine ausgegraute Zeile lässt sie aus" <| fun () ->
            let start = modelWith (rows 4) |> withoutCategory |> step (categorySet 2)
            let model = start |> step (ClickRowCheckbox (idOf 1, false)) |> step (ClickRowCheckbox (idOf 4, true))
            Expect.equal model.Selection (picked [ 1; 3; 4 ]) "2 ist ausgegraut und bleibt draußen"

        testCase "alle gewählt, eine abgewählt und dann ausgegraut: Anzahl und Summe zählen sie nicht doppelt ab" <| fun () ->
            let model =
                modelWith (rows 3) |> withoutCategory |> step ToggleHeaderCheckbox
                |> step (ClickRowCheckbox (idOf 2, false)) |> step (categorySet 2)
            let totals = selectionTotals model
            Expect.equal totals.Count 2 "1 und 3"
            Expect.equal totals.Amount.Amount -40m "-10 und -30"
    ]

// Aufzählung (Test-Doktrin Punkt 20): aus vier Lagen jede Folge von bis zu drei Klicks, Tasten und Bearbeitungen durch das
// echte `update`. Nach jedem Schritt gilt: jede gewählte Buchung ist geladen und nicht ausgegraut, und Anzahl, Liste der
// Zusammenfassung und Sammelauftrag meinen genau sie. Welche Buchungen ausgegraut sind, führt die Aufzählung selbst mit,
// sie glaubt nicht `FilteredOut`.

type private SelectionStart = {
    Name: string
    Model: unit -> Model
    /// Die ausgegrauten Buchungen der Lage.
    Greyed: Set<int>
    /// Bearbeitung, die eine Buchung im Detail aus dem Filter wirft; ohne Filter keine.
    EditOut: (int -> Msg) option
}

let private lenaRows () = rows 4 |> List.map (fun e -> { e with Flag = AskLena })

let private selectionStarts : SelectionStart list =
    let routed = entry 9 -90m
    [ { Name = "ausgegraute offene Buchung"; Model = greyedOpen; Greyed = Set.singleton 1; EditOut = Some categorySet }
      { Name = "Route-Buchung nicht geladen"
        Model =
          fun () ->
              { modelWith (rows 4) with SelectedFilter = Some AllAccounts; PendingRouteEntry = Some routed.Id }
              |> asPanel
              |> step (RouteEntryLoaded (routed.Id, Ok (Some routed)))
        Greyed = Set.empty
        EditOut = None }
      { Name = "gewählt und im Detail ausgegraut"
        Model = fun () -> modelWith (rows 4) |> withoutCategory |> asPanel |> step (ClickRowCheckbox (idOf 1, false))
        Greyed = Set.empty
        EditOut = Some categorySet }
      { Name = "Chip Lena, offene Buchung"
        Model =
          fun () ->
              { modelWith (lenaRows ()) with Filters = { noFilters with OnlyAskLena = true } }
              |> asPanel
              |> step (OpenDetail (entry 1 -10m))
        Greyed = Set.empty
        EditOut = Some lenaWithdrawn } ]

/// Alle Schritte einer Lage, mit den Buchungen, die der Schritt ausgraut.
let private selectionSteps (start: SelectionStart) : (string * Msg * Set<int>) list =
    [ for n in 1..4 do
        yield $"Strg-Klick {n}", ClickRowWithModifier (idOf n, ToggleRow), Set.empty
        yield $"Umschalt-Klick {n}", ClickRowWithModifier (idOf n, RangeToRow), Set.empty
        yield $"Kästchen {n}", ClickRowCheckbox (idOf n, false), Set.empty
        yield $"Umschalt-Kästchen {n}", ClickRowCheckbox (idOf n, true), Set.empty
      yield "Umschalt+↓", ExtendSelection 1, Set.empty
      yield "Leertaste", ToggleFocusedSelected, Set.empty
      yield "Kopf-Kästchen", ToggleHeaderCheckbox, Set.empty
      match start.EditOut with
      | Some edit ->
          for n in 1..2 do
              yield $"Bearbeitung wirft {n} hinaus", edit n, Set.singleton n
      | None -> () ]

let private checkSelection (path: string list) (greyed: Set<int>) (model: Model) =
    let visible = [ 1..4 ] |> List.filter (fun n -> not (greyed.Contains n)) |> List.map idOf |> Set.ofList
    let where = String.Join(" → ", List.rev path)
    let expected =
        match model.Selection with
        | NoSelection -> Set.empty
        | Picked ids ->
            Expect.isTrue (Set.isSubset ids visible) $"{where}: gewählt ist nur Sichtbares, {ids}"
            ids
        | AllExcept excluded -> Set.difference visible excluded
    Expect.equal (selectionTotals model).Count (Set.count expected) $"{where}: Anzahl"
    Expect.equal (selectedEntries model |> List.map (fun e -> e.Id) |> Set.ofList) expected $"{where}: Zusammenfassung"
    match model.Selection, Components.Ledger.State.reviewMarkBatchSelection model with
    | Picked _, Some (BatchPicked ids) -> Expect.equal (Set.ofList ids) expected $"{where}: Sammelauftrag"
    | Picked _, other -> failtestf "%s: Sammelauftrag %A" where other
    | _ -> ()

[<Tests>]
let visibleSelectionWalk =
    testList "Kassenbuch am PC: Aufzählung, die Auswahl hält nur sichtbare Buchungen" [
        for start in selectionStarts ->
            testCase start.Name <| fun () ->
                let rec walk depth path greyed model =
                    checkSelection path greyed model
                    if depth > 0 then
                        for name, msg, out in selectionSteps start do
                            walk (depth - 1) (name :: path) (Set.union greyed out) (step msg model)
                walk 3 [ start.Name ] start.Greyed (start.Model ())
    ]
