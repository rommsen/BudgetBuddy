module Tests.TargetsApplyClientTests

// budgeting-h2zfn: Zustandsübergänge des Dialogs "Targets anwenden" auf der Budget-Seite.
// Der Aufruf an den Server (Fable.Remoting-Proxy) ist unter .NET nicht testbar; die
// Übergänge liegen deshalb als reine Funktionen in `Components.Budget.Types`.

open System
open Expecto
open Shared.Domain
open Shared.Budgeting
open Types
open Components.Budget.Types

let private eur (amount: decimal) : Money = { Amount = amount; Currency = "EUR" }

let private line name (oldA: decimal) (under: decimal) : TargetLine =
    { CategoryId = Guid.NewGuid()
      CategoryName = name
      GroupName = "Alltag"
      OldAssigned = eur oldA
      Underfunded = eur under
      NewAssigned = eur (oldA + under) }

let private previewOf (after: decimal) (lines: TargetLine list) : TargetPreview =
    { Month = "2026-11-01"
      Lines = lines
      Skipped = []
      Total = eur (lines |> List.sumBy (fun l -> l.Underfunded.Amount))
      ToBeBudgeted = eur 100m
      ToBeBudgetedAfter = eur after }

let private month : BudgetMonth = { Month = "2026-11-01"; ToBeBudgeted = eur 100m; Groups = [] }
let private loaded = { init () with Month = Success month }
let private preview = previewOf 50m [ line "Essen" 0m 30m; line "Kleidung" 20m 20m ]

let private reportOf (outcomes: LineOutcome list) (actual: Money option) : ApplyReport =
    { ApplicationId = Guid.NewGuid().ToString()
      Month = "2026-11-01"
      Lines =
        outcomes
        |> List.mapi (fun i o ->
            { CategoryId = Guid.NewGuid()
              CategoryName = $"K{i}"
              OldAssigned = eur 0m
              NewAssigned = eur 10m
              Outcome = o })
      ActualToBeBudgeted = actual }

let private openTests =
    testList "Dialog öffnen (h2zfn)" [
        test "Mit geladenem Monat öffnet der Dialog und verlangt die Vorschau" {
            let m, load = openTargets loaded
            Expect.equal m.Targets PreviewLoading "lädt"
            Expect.isTrue load "Vorschau holen"
        }
        test "Ohne geladenen Monat oder bei offenem Dialog passiert nichts" {
            let _, load1 = openTargets (init ())
            Expect.isFalse load1 "Monat nicht geladen"
            let open' = { loaded with Targets = PreviewReady (preview, false) }
            let m, load2 = openTargets open'
            Expect.equal m open' "unverändert"
            Expect.isFalse load2 "kein zweites Laden"
        }
        test "Die Vorschau wird eingenommen, eine späte Antwort zu anderer Wahl oder geschlossenem Dialog nicht" {
            let waiting = { loaded with Targets = PreviewLoading }
            Expect.equal (applyPreviewLoaded CurrentMonth (Ok preview) waiting).Targets (PreviewReady (preview, false)) "eingenommen"
            Expect.equal (applyPreviewLoaded NextMonth (Ok preview) waiting) waiting "andere Monatswahl"
            Expect.equal (applyPreviewLoaded CurrentMonth (Ok preview) loaded) loaded "Dialog geschlossen"
            Expect.equal (applyPreviewLoaded CurrentMonth (Error "kaputt") waiting).Targets (PreviewFailed "kaputt") "Fehler"
        }
    ]

let private confirmTests =
    testList "Übernehmen (h2zfn)" [
        test "Übernehmen schickt die gezeigte Vorschau und sperrt weitere Klicks" {
            let ready = { loaded with Targets = PreviewReady (preview, false) }
            let m, sent = confirmTargets ready
            Expect.equal sent (Some preview) "gezeigte Vorschau geht an den Server"
            Expect.equal m.Targets (Applying preview) "läuft"
            let _, again = confirmTargets m
            Expect.isNone again "kein zweiter Aufruf während des Laufs"
        }
        test "Ein negatives Zu verteilen danach sperrt Übernehmen nicht" {
            let negative = previewOf -25m [ line "Essen" 0m 30m ]
            let _, sent = confirmTargets { loaded with Targets = PreviewReady (negative, false) }
            Expect.equal sent (Some negative) "bleibt aktiv"
        }
        test "Ohne Zeilen gibt es nichts zu übernehmen" {
            let empty = previewOf 100m []
            let _, sent = confirmTargets { loaded with Targets = PreviewReady (empty, false) }
            Expect.isNone sent "kein Aufruf"
        }
        test "Hat sich YNABs Stand geändert, zeigt der Dialog die neue Vorschau mit Hinweis" {
            let applying = { loaded with Targets = Applying preview }
            let fresh = previewOf 40m [ line "Essen" 10m 30m ]
            let m = applyApplied (Ok (PreviewChanged fresh)) applying
            Expect.equal m.Targets (PreviewReady (fresh, true)) "neue Vorschau, changed = true"
        }
        test "Der Ausgang wird gezeigt, ein Aufruffehler bleibt sichtbar" {
            let applying = { loaded with Targets = Applying preview }
            let report = reportOf [ Written; Written ] (Some preview.ToBeBudgetedAfter)
            Expect.equal (applyApplied (Ok (Applied report)) applying).Targets (ApplyDone (preview, report, false, None)) "fertig"
            Expect.equal (applyApplied (Error "Netz weg") applying).Targets (ApplyFailed (preview, "Netz weg")) "Fehler"
        }
    ]

let private retryTests =
    testList "Erneut senden und Schließen (h2zfn)" [
        test "Erneut senden nennt die Anwendung und läuft nur, wenn etwas fehlgeschlagen ist" {
            let failed = reportOf [ Written; Failed "Rate-Limit" ] None
            let done' = { loaded with Targets = ApplyDone (preview, failed, false, None) }
            let m, id = retryFailed done'
            Expect.equal id (Some failed.ApplicationId) "Anwendung"
            Expect.equal m.Targets (ApplyDone (preview, failed, true, None)) "läuft"
            let _, again = retryFailed m
            Expect.isNone again "nicht doppelt"
            let ok = reportOf [ Written ] None
            let _, none = retryFailed { loaded with Targets = ApplyDone (preview, ok, false, None) }
            Expect.isNone none "nichts Fehlgeschlagenes"
        }
        test "Das Ergebnis des erneuten Sendens ersetzt den Bericht, ein Fehler lässt ihn stehen" {
            let failed = reportOf [ Failed "Rate-Limit" ] None
            let running = { loaded with Targets = ApplyDone (preview, failed, true, None) }
            let better = { failed with Lines = failed.Lines |> List.map (fun l -> { l with Outcome = Written }) }
            Expect.equal (applyRetried (Ok better) running).Targets (ApplyDone (preview, better, false, None)) "neuer Bericht"
            Expect.equal (applyRetried (Error "weg") running).Targets (ApplyDone (preview, failed, false, Some "weg")) "alter Bericht, Fehler"
        }
        test "Schließen nach einer Anwendung lädt den Monat neu, davor nicht" {
            let report = reportOf [ Written ] None
            let _, reload = closeTargets { loaded with Targets = ApplyDone (preview, report, false, None) }
            Expect.isTrue reload "nach Anwendung neu laden"
            let _, reloadFailed = closeTargets { loaded with Targets = ApplyFailed (preview, "x") }
            Expect.isTrue reloadFailed "auch nach unklarem Ausgang"
            let m, reloadPreview = closeTargets { loaded with Targets = PreviewReady (preview, false) }
            Expect.equal m.Targets TargetsClosed "geschlossen"
            Expect.isFalse reloadPreview "Abbrechen schreibt nichts, kein Neuladen"
        }
        test "Während des Schreibens lässt sich der Dialog nicht schließen" {
            let applying = { loaded with Targets = Applying preview }
            Expect.equal (closeTargets applying) (applying, false) "Applying bleibt"
            let report = reportOf [ Failed "x" ] None
            let retrying = { loaded with Targets = ApplyDone (preview, report, true, None) }
            Expect.equal (closeTargets retrying) (retrying, false) "erneutes Senden bleibt"
        }
    ]

let private actualTests =
    testList "Zu verteilen nach dem Lauf (h2zfn)" [
        test "Gleich der Vorschau, abweichend oder unbekannt" {
            let same = reportOf [ Written ] (Some (eur 50m))
            Expect.equal (actualCheck preview same) SameAsPreview "gleich"
            let differs = reportOf [ Written ] (Some (eur 47m))
            Expect.equal (actualCheck preview differs) (DiffersFromPreview (eur 47m)) "weicht ab"
            Expect.equal (actualCheck preview (reportOf [ Written ] None)) ActualUnknown "unbekannt"
        }
    ]

let private viewSourceTests =
    // Die Ansicht nutzt nur Design-System-Bausteine (Styleguide-Gate); eigene Farben, Größen
    // und rohe HTML-Knöpfe wären ein Verstoß.
    let source =
        let rec up (dir: string) =
            let candidate = IO.Path.Combine(dir, "src", "Client", "Components", "Budget", "View.fs")
            if IO.File.Exists candidate then candidate
            else up (IO.Path.GetDirectoryName dir)
        IO.File.ReadAllText(up AppContext.BaseDirectory)
    testList "Ansicht des Dialogs (h2zfn)" [
        test "Der Dialog benutzt Modal, Button, Money, Badge und ErrorDisplay" {
            for literal in [ "Modal.view"; "Button.primary \"Übernehmen\""; "Button.ghost \"Abbrechen\""; "Money.view"; "Badge.neutral"; "ErrorDisplay.warning"; "Fehlgeschlagene erneut senden" ] do
                Expect.stringContains source literal $"{literal} fehlt"
        }
        test "Die Ansicht enthält keine Hex-Farben und keine rohen Knöpfe" {
            Expect.isFalse (Text.RegularExpressions.Regex.IsMatch(source, "#[0-9a-fA-F]{3,8}\\b")) "keine Hex-Werte"
            Expect.isFalse (source.Contains "Html.button") "kein roher Html.button"
        }
    ]

[<Tests>]
let tests =
    testList "Targets anwenden, Client (budgeting-h2zfn)" [ openTests; confirmTests; retryTests; actualTests; viewSourceTests ]
