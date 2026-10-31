module SyncFlowStateTests

// Regressionstest für ledger-mshd8: seit ledger-qvt84 Iteration 3 kann
// `importToYnab` bei einem Teilfehlschlag `Error` liefern, obwohl andere
// Buchungen im selben Batch schon erfolgreich importiert und gespeichert
// wurden (`src/Server/Api.fs`, Kommentar "Persisted regardless of a ledger
// write failure"). `ImportCompleted (Error err)` lud die Session bislang
// nicht neu, der Client zeigte die bereits importierten Buchungen weiterhin
// mit ihrem alten Stand an.
//
// Getestet wird direkt gegen die reine `update`-Funktion (ADR 0020: Reduzierer
// testbar), ohne den Fable.Remoting-Proxy anzufassen, dasselbe Muster wie
// `LedgerPageTests.fs`s `dispatched`-Helfer.

open Expecto
open Shared.Domain
open Shared.Api
open Types
open Components.SyncFlow.Types
open Components.SyncFlow.State

/// Führt ein `Cmd` aus und sammelt die abgeschickten Nachrichten ein. Zulässig
/// nur für Kommandos aus `Cmd.ofMsg` / `Cmd.batch` / `Cmd.map` (ein `Cmd.OfAsync`
/// würde hier den Fable-only-Proxy anfassen und unter .NET werfen, ADR 0020).
let private dispatched (cmd: Elmish.Cmd<'msg>) : 'msg list =
    let collected = ResizeArray<'msg>()
    cmd |> List.iter (fun effect -> effect collected.Add)
    List.ofSeq collected

let private freshModel () : Model = fst (init ())

let private sessionWith (status: SyncSessionStatus) : SyncSession =
    { Id = SyncSessionId (System.Guid.Parse "00000000-0000-4000-8000-000000000001")
      StartedAt = System.DateTime(2026, 10, 31)
      CompletedAt = None
      Status = status
      TransactionCount = 0
      ImportedCount = 0
      SkippedCount = 0 }

let private interrupted () : Model =
    { freshModel () with
        CurrentSession = Loading
        InterruptedStart = Some { Reason = "Http error (0)"; ReadsLeft = interruptedStartReadLimit; ReadPlanned = false } }

[<Tests>]
let syncFlowStateTests =
    testList "SyncFlow-Reduzierer" [

        // banking-import-cyg94: der Server entscheidet über die TAN, der Client folgt nur.
        test "ReviewReady lädt die Transaktionen direkt, ohne TAN-Schritt" {
            let session = sessionWith ReviewingTransactions
            let model, cmd, external = update (SyncStarted (Ok (ReviewReady session))) (freshModel ())
            Expect.equal model.CurrentSession (Success (Some session)) "Sitzung übernommen"
            Expect.equal (dispatched cmd) [ LoadTransactions ] "lädt die Transaktionen"
            Expect.equal external (ShowToast ("Umsätze bereit zur Prüfung", ToastSuccess)) "Hinweis"
        }

        test "TanRequired zeigt die wartende Sitzung und lädt nichts" {
            let session = sessionWith AwaitingTan
            let model, cmd, external = update (SyncStarted (Ok (TanRequired session))) (freshModel ())
            Expect.equal model.CurrentSession (Success (Some session)) "Sitzung übernommen"
            Expect.isEmpty (dispatched cmd) "kein weiterer Aufruf"
            Expect.equal external (ShowToast ("Bitte bestätige die TAN auf deinem Handy", ToastInfo)) "Hinweis"
        }

        // Bug (Live-Befund): die Antwort von `startSync` ging als "Http error (0)" verloren, die Push-TAN
        // kam trotzdem an. Der Client zeigte Failure, obwohl der Server in AwaitingTan stand.
        test "verlorene Antwort von startSync liest den Serverzustand neu statt zu scheitern" {
            let start = { freshModel () with CurrentSession = Loading }
            let model, cmd, external = update (SyncStartInterrupted "Http error (0)") start
            Expect.equal (dispatched cmd) [ LoadCurrentSession ] "liest neu, kein zweiter Start"
            Expect.equal model.CurrentSession Loading "noch kein Fehler"
            Expect.equal external NoOp "noch kein Hinweis"
        }

        test "ein fachlicher Fehler von startSync bleibt ein Fehler ohne erneutes Lesen" {
            let error = SyncError.ComdirectAuthFailed "abgelehnt"
            let model, cmd, _ = update (SyncStarted (Error error)) (freshModel ())
            Expect.equal model.CurrentSession (Failure "abgelehnt") "Fehler sichtbar"
            Expect.isEmpty (dispatched cmd) "kein Nachladen"
        }

        test "nach verlorener Antwort wird eine wartende TAN vom Server übernommen" {
            let session = sessionWith AwaitingTan
            let model, cmd, _ = update (CurrentSessionLoaded (Some session)) (interrupted ())
            Expect.equal model.CurrentSession (Success (Some session)) "TAN-Schritt"
            Expect.equal model.InterruptedStart None "erledigt"
            Expect.isEmpty (dispatched cmd) "keine neue Challenge"
        }

        test "nach verlorener Antwort wird eine fertige Prüfung vom Server übernommen" {
            let session = sessionWith ReviewingTransactions
            let model, cmd, _ = update (CurrentSessionLoaded (Some session)) (interrupted ())
            Expect.equal model.InterruptedStart None "erledigt"
            Expect.equal (dispatched cmd) [ LoadTransactions ] "lädt die Prüfung"
        }

        test "nach verlorener Antwort ohne Sitzung auf dem Server bleibt ein Fehler sichtbar" {
            let model, cmd, external = update (CurrentSessionLoaded None) (interrupted ())
            Expect.equal model.CurrentSession (Failure (interruptedStartMessage "Http error (0)")) "kein stilles Zurückfallen"
            Expect.equal external (ShowToast (interruptedStartMessage "Http error (0)", ToastError)) "Hinweis"
            Expect.isEmpty (dispatched cmd) "kein automatischer Neustart"
        }

        // Eine abgeschlossene Sitzung stammt aus einem früheren Sync, der neue kam nie an.
        test "nach verlorener Antwort zählt eine alte abgeschlossene Sitzung als keine Sitzung" {
            let model, _, _ = update (CurrentSessionLoaded (Some (sessionWith Completed))) (interrupted ())
            Expect.equal model.CurrentSession (Failure (interruptedStartMessage "Http error (0)")) "Fehler sichtbar"
        }

        test "nach verlorener Antwort wartet ein laufender Abruf weiter, ohne neuen Start" {
            let session = sessionWith FetchingTransactions
            let model, cmd, _ = update (CurrentSessionLoaded (Some session)) (interrupted ())
            Expect.equal model.CurrentSession (Success (Some session)) "Fortschritt sichtbar"
            Expect.isSome model.InterruptedStart "wartet weiter"
            Expect.isNonEmpty cmd "liest verzögert neu"
            Expect.equal (markSessionLoading model).CurrentSession (Success (Some session)) "der Fortschritt flackert nicht"
        }

        test "der Takt nach der Pause liest nur neu und startet nichts" {
            let session = sessionWith FetchingTransactions
            let waiting, _, _ = update (CurrentSessionLoaded (Some session)) (interrupted ())
            let model, cmd, _ = update (InterruptedStartReadDue (interruptedStartReadLimit - 1)) waiting
            Expect.equal (dispatched cmd) [ LoadCurrentSession ] "nur lesen, kein neuer Start"
            Expect.isSome model.InterruptedStart "wartet weiter"
        }

        // Bug: das Nachlesen lief ohne Grenze, eine stehengebliebene Sitzung zeigte ewig den Fortschritt.
        test "läuft der Abruf über die Frist hinaus, bleibt ein Fehler sichtbar und nichts startet neu" {
            let fetching = CurrentSessionLoaded (Some (sessionWith FetchingTransactions))
            let rec read (reads: int) (model: Model) =
                let next, cmd, external = update fetching model
                match next.InterruptedStart with
                | None -> reads + 1, next, cmd, external
                | Some recovery when reads <= interruptedStartReadLimit ->
                    let due, _, _ = update (InterruptedStartReadDue recovery.ReadsLeft) next
                    read (reads + 1) due
                | Some _ -> failtest "das Nachlesen endet nicht"
            let reads, model, cmd, external = read 0 (interrupted ())
            let message = interruptedStartTimeoutMessage "Http error (0)"
            Expect.equal reads (interruptedStartReadLimit + 1) "die Frist wird ausgeschöpft"
            Expect.equal model.CurrentSession (Failure message) "Fehler sichtbar"
            Expect.equal external (ShowToast (message, ToastError)) "Hinweis"
            Expect.isEmpty (dispatched cmd) "kein automatischer Neustart"
        }

        // Bug: jeder Routenwechsel lädt die Sitzung und startete so eine weitere Kette.
        test "ein Lesen vom Routenwechsel startet keine zweite Kette" {
            let loaded = CurrentSessionLoaded (Some (sessionWith FetchingTransactions))
            let waiting, _, _ = update loaded (interrupted ())
            let model, cmd, _ = update loaded waiting
            Expect.isEmpty cmd "keine zweite Pause"
            Expect.equal model.InterruptedStart waiting.InterruptedStart "die Frist läuft nicht schneller"
        }

        test "ein veralteter Takt liest nicht" {
            let waiting, _, _ = update (CurrentSessionLoaded (Some (sessionWith FetchingTransactions))) (interrupted ())
            let model, cmd, _ = update (InterruptedStartReadDue interruptedStartReadLimit) waiting
            Expect.isEmpty (dispatched cmd) "der Takt gehört zu einem früheren Abbruch"
            Expect.equal model.InterruptedStart waiting.InterruptedStart "unverändert"
        }

        test "ein Takt nach dem Ende des Nachlesens liest nicht" {
            let _, cmd, _ = update (InterruptedStartReadDue 3) (freshModel ())
            Expect.isEmpty (dispatched cmd) "nichts mehr zu tun"
        }

        test "schlägt auch das Neulesen fehl, bleibt ein Fehler sichtbar" {
            let model, cmd, external = update (CurrentSessionLoadFailed "Http error (0)") (interrupted ())
            Expect.equal model.CurrentSession (Failure (interruptedStartMessage "Http error (0)")) "kein ewiges Laden"
            Expect.equal model.InterruptedStart None "erledigt"
            Expect.equal external (ShowToast (interruptedStartMessage "Http error (0)", ToastError)) "Hinweis"
            Expect.isEmpty (dispatched cmd) "kein automatischer Neustart"
        }

        test "ImportCompleted (Error) lädt die Session neu" {
            // Bug: vor dem Fix lieferte dieser Zweig nur `Cmd.none`, also keine
            // Nachricht. Ein Teilfehlschlag ließ bereits gespeicherte Buchungen
            // im Client auf altem Stand stehen, bis Roman manuell neu lud.
            let error = SyncError.YnabImportFailed (1, "Kassenbuch-Schreiben fehlgeschlagen")
            let _, cmd, _ = update (ImportCompleted (Error error)) (freshModel ())
            Expect.contains (dispatched cmd) LoadCurrentSession
                "Ein Teilfehlschlag muss die Session neu laden, sonst zeigt der Client veraltete Daten"
        }

        test "ImportCompleted (Ok) lädt weiterhin die Session neu (unverändertes Verhalten)" {
            // Intakt-Szenario: der Erfolgspfad lud die Session schon vor diesem
            // Fix neu und darf sich durch die Änderung am Fehlerpfad nicht ändern.
            let result: ImportResult = { CreatedCount = 2; DuplicateTransactionIds = []; LenaFlagNotSentCount = 0 }
            let _, cmd, _ = update (ImportCompleted (Ok result)) (freshModel ())
            Expect.contains (dispatched cmd) LoadCurrentSession
                "Der Erfolgspfad lädt die Session weiterhin neu"
        }
    ]

/// Formular an einem gegebenen Teststand, wie es nach dem Tippen im Model steht.
let private withInlineForm (version: int) (test: RemoteData<bool>) (pattern: string) : Model =
    { freshModel () with
        InlineRuleTestVersion = version
        InlineRuleForm =
            Some {
                TransactionId = TransactionId "f955d"
                Pattern = pattern
                PatternType = Shared.Domain.Contains
                TargetField = Combined
                CategoryId = YnabCategoryId (System.Guid.Empty)
                CategoryName = "Lebensmittel"
                PayeeOverride = ""
                RuleName = "Auto: Test"
                IsSaving = false
                TransactionText = "REWE Kartenzahlung"
                InlineRuleTest = test
            } }

let private inlineTest (model: Model) =
    model.InlineRuleForm |> Option.map (fun form -> form.InlineRuleTest)

let private step (msg: Msg) (model: Model) =
    let next, _, _ = update msg model
    next

[<Tests>]
let inlineRuleTestTests =
    testList "Muster-Test im Regelformular" [
        // Bug categorization-f955d: die Antworten liefen ohne Stand in die View, die letzte Antwort
        // gewann, auch wenn sie zu einem älteren Muster gehörte.
        testCase "die späte Antwort einer älteren Anfrage überschreibt das neuere Ergebnis nicht" <| fun () ->
            let model =
                withInlineForm 2 Loading "rewe"
                |> step (InlineRuleTested (2, Ok true))
                |> step (InlineRuleTested (1, Ok false))
            Expect.equal (inlineTest model) (Some (Success true)) "das neuere Ergebnis bleibt"

        testCase "eine späte Antwort ohne Verbindung überschreibt das neuere Ergebnis nicht" <| fun () ->
            let model =
                withInlineForm 2 (Success true) "rewe"
                |> step (InlineRuleTestLost 1)
            Expect.equal (inlineTest model) (Some (Success true)) "das neuere Ergebnis bleibt"

        testCase "die Antwort zum aktuellen Stand wird übernommen" <| fun () ->
            let model = withInlineForm 2 Loading "rewe" |> step (InlineRuleTested (2, Ok false))
            Expect.equal (inlineTest model) (Some (Success false)) "passt nicht"

        testCase "eine Antwort aus einem geschlossenen Formular gilt nicht für das nächste" <| fun () ->
            let closed = withInlineForm 2 Loading "rewe" |> step CloseInlineRuleForm
            let reopened = { closed with InlineRuleForm = (withInlineForm 0 Loading "edeka").InlineRuleForm }
            Expect.equal (inlineTest (step (InlineRuleTested (2, Ok true)) reopened)) (Some Loading) "Antwort verworfen"

        // Bug categorization-f955d: der Timer im useEffect wurde nie abgebrochen, jeder Tastendruck
        // löste eine Anfrage aus.
        testCase "jede Änderung am Muster macht den wartenden Test ungültig" <| fun () ->
            let model = withInlineForm 2 (Success true) "rewe"
            for change in [ UpdateInlineRulePattern "rewe m"
                            UpdateInlineRulePatternType Exact
                            UpdateInlineRuleTargetField Payee ] do
                let next, cmd, _ = update change model
                Expect.equal next.InlineRuleTestVersion 3 "Stand zählt hoch"
                Expect.equal (inlineTest next) (Some Loading) "läuft"
                Expect.isFalse cmd.IsEmpty "der Timer startet"
                let _, staleCmd, _ = update (InlineRuleTestDue 2) next
                Expect.isTrue staleCmd.IsEmpty "der alte Timer verfällt"

        testCase "ohne Muster gibt es keinen Test und die laufende Antwort verfällt" <| fun () ->
            let next, cmd, _ = update (UpdateInlineRulePattern "  ") (withInlineForm 2 Loading "rewe")
            Expect.equal (inlineTest next) (Some NotAsked) "kein Test"
            Expect.isTrue cmd.IsEmpty "kein Timer"
            Expect.equal (inlineTest (step (InlineRuleTested (2, Ok true)) next)) (Some NotAsked) "Antwort verworfen"

        // Bug categorization-f955d: ein Netzfehler erschien als "passt nicht", ein ungültiges
        // Muster gar nicht.
        testCase "ungültiges Muster und Netzfehler melden je einen eigenen Text und sind kein passt nicht" <| fun () ->
            let invalid =
                withInlineForm 2 Loading "("
                |> step (InlineRuleTested (2, Error (RulesError.InvalidPattern ("(", "Klammer fehlt"))))
            let lost = withInlineForm 2 Loading "(" |> step (InlineRuleTestLost 2)
            Expect.equal (inlineTest invalid) (Some (RemoteData.Failure "Ungültiges Muster '(': Klammer fehlt")) "ungültig"
            Expect.equal (inlineTest lost) (Some (RemoteData.Failure ruleTestLostText)) "Netz"
            Expect.notEqual (inlineTest invalid) (inlineTest lost) "verschieden"
    ]
