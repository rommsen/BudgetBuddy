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

// ============================================
// Konten für den Übertrag im Import (ynab-sync-k3vq8)
// ============================================

let private cashAccountId = YnabAccountId (System.Guid.Parse "00000000-0000-4000-8000-000000000200")
let private otherAccountId = YnabAccountId (System.Guid.Parse "00000000-0000-4000-8000-000000000201")

let private ynabAccount (id: YnabAccountId) (name: string) : YnabAccount =
    { Id = id
      Name = name
      Balance = { Amount = 0m; Currency = "EUR" }
      OnBudget = true
      Closed = false }

let private cashAccount = ynabAccount cashAccountId "Bar"
let private cashTarget = Some (ToTransfer (cashAccountId, "Bar"))

let private reviewTx : SyncTransaction =
    { Transaction =
        { Id = TransactionId "k3vq8-1"
          BookingDate = System.DateTime(2026, 10, 31)
          Amount = { Amount = -48.12m; Currency = "EUR" }
          Payee = Some "Markt"
          Memo = "Kartenzahlung"
          Reference = "ref1"
          RawData = "" }
      Status = Pending
      CategoryId = None
      CategoryName = None
      MatchedRuleId = None
      PayeeOverride = None
      ExternalLinks = []
      UserNotes = None
      DuplicateStatus = NotDuplicate { TransactionReference = ""; ReferenceFoundInYnab = false; ImportIdFoundInYnab = false; FuzzyMatchDate = None; FuzzyMatchAmount = None; FuzzyMatchPayee = None }
      YnabImportStatus = NotAttempted
      Splits = None
      SuggestedByOrderId = None
      ReviewSteps = []
      AskLena = false }

let private reviewing (accounts: RemoteData<YnabAccount list>) (quickAdd: YnabAccountId option) : Model =
    { freshModel () with
        SyncTransactions = Success [ reviewTx ]
        Accounts = accounts
        QuickAddAccountId = quickAdd }

let private lineTargets (model: Model) =
    model.SplitEdit |> Option.map (fun edit -> edit.Lines |> List.map (fun line -> line.Target))

let private cashback = StartCashbackSplit reviewTx.Transaction.Id

[<Tests>]
let importAccountsTests =
    testList "Konten im Import" [

        // Bug: jeder Fehler wurde zu einer leeren Liste und verschwand ohne Meldung, die Quick-Add-Id gleich mit.
        test "scheitern die Konten, nennt ein Toast den Fehlertext und die Quick-Add-Id bleibt" {
            let start = { freshModel () with Accounts = Loading; QuickAddAccountId = Some cashAccountId }
            let model, cmd, external = update (AccountsLoaded (0, Error "YNAB antwortet nicht")) start
            Expect.equal model.Accounts (Failure "YNAB antwortet nicht") "Fehlerstand"
            Expect.equal model.QuickAddAccountId (Some cashAccountId) "Id bleibt"
            Expect.isEmpty (dispatched cmd) "kein Folgeaufruf"
            Expect.equal external (ShowLoadFailure "Konten konnten nicht geladen werden: YNAB antwortet nicht") "Toast"
        }

        test "scheitert ein Auffrischen, bleibt die geladene Liste nutzbar und ein Toast meldet es" {
            let start = { freshModel () with Accounts = Success [ cashAccount ] }
            let model, _, external = update (AccountsLoaded (0, Error "weg")) start
            Expect.equal model.Accounts (Success [ cashAccount ]) "Liste bleibt"
            Expect.equal external (ShowLoadFailure "Konten konnten nicht geladen werden: weg") "Toast"
        }

        test "scheitern die Einstellungen, bleibt eine bekannte Quick-Add-Id stehen" {
            let start = { freshModel () with QuickAddAccountId = Some cashAccountId }
            let model, _, external = update (QuickAddAccountLoaded (0, Error "Http error (0)")) start
            Expect.equal model.QuickAddAccountId (Some cashAccountId) "Id bleibt"
            Expect.equal external NoOp "kein Toast"
        }

        test "ein zweiter Ladeauftrag während des Abrufs startet nichts" {
            let start = { freshModel () with Accounts = Loading }
            let _, cmd, _ = update LoadAccounts start
            Expect.isEmpty cmd "kein zweiter Abruf"
        }

        test "ein Auffrischen lässt die geladene Liste stehen" {
            let loaded = { freshModel () with Accounts = Success [ cashAccount ] }
            Expect.equal (beginAccountsRead loaded).Accounts loaded.Accounts "bleibt"
            Expect.equal (beginAccountsRead (freshModel ())).Accounts Loading "sonst lädt es"
        }

        // Bug: scheiterte der Abruf beim Start, half nur ein Neuladen der App.
        for accounts, name in [ Failure "weg", "gescheitert"; NotAsked, "noch nicht angefragt" ] do
            test $"Barabhebung lädt Konten nach, wenn sie {name} sind" {
                let _, cmd, _ = update cashback (reviewing accounts (Some cashAccountId))
                Expect.equal (dispatched cmd) [ LoadAccounts ] "lädt die Konten"
            }

        test "Aufteilen lädt nach einem Fehlschlag die Konten und die Quick-Add-Id nach" {
            let _, cmd, _ = update (StartSplitEdit reviewTx.Transaction.Id) (reviewing (Failure "weg") None)
            Expect.equal (dispatched cmd) [ LoadAccounts; LoadQuickAddAccount ] "lädt beides"
        }

        for accounts, name in [ Success [ cashAccount ], "geladen"; Loading, "unterwegs" ] do
            test $"das Sheet lädt Konten nicht nach, wenn sie {name} sind" {
                let _, cmd, _ = update cashback (reviewing accounts (Some cashAccountId))
                Expect.isEmpty (dispatched cmd) "kein Abruf"
            }

        test "Barabhebung belegt die Übertrag-Zeile mit dem Quick-Add-Konto" {
            let model, _, _ = update cashback (reviewing (Success [ cashAccount ]) (Some cashAccountId))
            Expect.equal (lineTargets model) (Some [ None; cashTarget ]) "vorbelegt"
        }

        test "kommt die Liste nach dem Tipp an, wird die leere Übertrag-Zeile belegt" {
            let tapped = step cashback (reviewing Loading (Some cashAccountId))
            Expect.equal (lineTargets tapped) (Some [ None; None ]) "noch leer"
            let model = step (AccountsLoaded (0, Ok [ cashAccount ])) tapped
            Expect.equal (lineTargets model) (Some [ None; cashTarget ]) "nachgetragen"
        }

        test "kommt die Quick-Add-Id nach dem Tipp an, wird die leere Übertrag-Zeile belegt" {
            let tapped = step cashback (reviewing (Success [ cashAccount ]) None)
            let model = step (QuickAddAccountLoaded (0, Ok (Some cashAccountId))) tapped
            Expect.equal (lineTargets model) (Some [ None; cashTarget ]) "nachgetragen"
        }

        test "eine von Roman gesetzte Zeile ändert sich durch die späte Liste nicht" {
            let other = ynabAccount otherAccountId "Tagesgeld"
            let tapped = step cashback (reviewing (Success [ other ]) (Some cashAccountId))
            let chosen = step (SelectSplitAccount (1, otherAccountId)) tapped
            let model = step (AccountsLoaded (0, Ok [ cashAccount; other ])) chosen
            Expect.equal (lineTargets model) (Some [ None; Some (ToTransfer (otherAccountId, "Tagesgeld")) ]) "bleibt"
        }

        test "der normale Editor bekommt durch die späte Liste kein Konto" {
            let opened = step (StartSplitEdit reviewTx.Transaction.Id) (reviewing Loading (Some cashAccountId))
            let model = step (AccountsLoaded (0, Ok [ cashAccount ])) opened
            Expect.equal (lineTargets model) (Some [ None; None ]) "bleibt leer"
        }

        test "ohne Quick-Add-Konto bleibt die Zeile auch nach der Liste leer" {
            let tapped = step cashback (reviewing Loading None)
            let model = step (AccountsLoaded (0, Ok [ cashAccount ])) tapped
            Expect.equal (lineTargets model) (Some [ None; None ]) "leer"
        }

        test "entfernt Roman die Übertrag-Zeile, kommt sie mit der Liste nicht zurück" {
            let tapped = step cashback (reviewing Loading (Some cashAccountId))
            let extended = tapped |> step AddSplitLine |> step (RemoveSplit 1)
            let model = step (AccountsLoaded (0, Ok [ cashAccount ])) extended
            Expect.equal (lineTargets model) (Some [ None; None ]) "keine Zeile belegt"
        }

        test "entfernt Roman die Zeile davor, rückt die wartende Übertrag-Zeile mit" {
            let tapped = step cashback (reviewing Loading (Some cashAccountId))
            let shifted = step (RemoveSplit 0) tapped
            let model = step (AccountsLoaded (0, Ok [ cashAccount ])) shifted
            Expect.equal (lineTargets model) (Some [ cashTarget ]) "die verbliebene Zeile"
        }

        // Bug: scheiterten die Kategorien, blieb der Picker still leer.
        test "scheitern die Kategorien, erscheint ein Toast" {
            let start = { freshModel () with CategoriesLoad = Loading }
            let model, _, external = update (CategoriesLoaded (Error (YnabError.NetworkError "Http error (0)"))) start
            Expect.isFalse (RemoteData.isLoading model.CategoriesLoad) "Ladestand beendet"
            Expect.equal external (ShowLoadFailure "Kategorien konnten nicht geladen werden: Netzwerkfehler: Http error (0)") "Toast"
        }

        test "fehlt die Einrichtung, nennt der Toast das und keinen ungültigen Token" {
            let _, _, external = update (CategoriesLoaded (Error (YnabError.Unauthorized "YNAB ist nicht eingerichtet"))) (freshModel ())
            Expect.equal external (ShowLoadFailure "Kategorien konnten nicht geladen werden: YNAB ist nicht eingerichtet") "Toast"
        }

        test "ein zweiter Kategorie-Auftrag während des Abrufs startet nichts" {
            let _, cmd, _ = update LoadCategories { freshModel () with CategoriesLoad = Loading }
            Expect.isEmpty cmd "kein zweiter Abruf, also kein zweiter Toast"
        }

        test "der Seitenaufruf des Imports lädt Konten und Quick-Add-Id" {
            let msgs = dispatched (State.pageLoadCmd Types.Page.SyncFlow)
            Expect.contains msgs (State.SyncFlowMsg LoadAccounts) "Konten"
            Expect.contains msgs (State.SyncFlowMsg LoadQuickAddAccount) "Quick-Add-Id"
        }

        test "der App-Start lädt die Konten nicht, außerhalb des Imports braucht sie niemand" {
            Expect.isFalse (List.contains LoadAccounts (dispatched (snd (init ())))) "kein Abruf beim Start"
        }
    ]

// ============================================
// Veraltete Antworten (ynab-sync-k3vq8)
// ============================================
//
// `Retry.reading` hält eine Antwort bis zu 1,6 Sekunden zurück. In dieser Zeit kann ein Start, ein
// Abbruch oder ein neuerer Abruf den Stand überholt haben; die alte Antwort darf ihn nicht ersetzen.

let private awaitingTan = sessionWith AwaitingTan

let private failureToast (exiting: bool) : Toast =
    { Id = System.Guid.NewGuid(); Message = "Fehler"; Type = ToastError; Exiting = exiting; Action = None }

// Bug ynab-sync-k3vq8: dauerhafte Ladefehler stapelten je Seitenbesuch weitere Toasts.
[<Tests>]
let loadFailureToastTests =
    testList "Ladefehler im Import als Toast" [
        test "ein sichtbarer gleicher Toast reicht" {
            Expect.isFalse (State.shouldShowLoadFailure SyncFlow "Fehler" [ failureToast false ]) "kein zweiter"
        }
        test "ein abgehender Toast zählt nicht mehr" {
            Expect.isTrue (State.shouldShowLoadFailure SyncFlow "Fehler" [ failureToast true ]) "neuer Toast"
        }
        test "ein anderer Text erscheint neben dem sichtbaren Toast" {
            Expect.isTrue (State.shouldShowLoadFailure QuickAdd "Anderer Fehler" [ failureToast false ]) "beide sichtbar"
        }
    ]

[<Tests>]
let staleAnswerTests =
    testList "Veraltete Antworten im Import" [

        test "die Antwort des aktuellen Sitzungs-Lesens wird übernommen" {
            let reading = beginSessionRead (freshModel ())
            let _, cmd, _ = update (SessionReadAnswered (reading.SessionReadVersion, Ok None)) reading
            Expect.equal (dispatched cmd) [ CurrentSessionLoaded None ] "gilt"
        }

        test "ein Fehler des aktuellen Sitzungs-Lesens wird gemeldet" {
            let reading = beginSessionRead (freshModel ())
            let _, cmd, _ = update (SessionReadAnswered (reading.SessionReadVersion, Error "weg")) reading
            Expect.equal (dispatched cmd) [ CurrentSessionLoadFailed "weg" ] "gilt"
        }

        // Bug-Gefahr: ein Lesen von vor dem Start kam nach `TanRequired` an und zeigte wieder den Startknopf.
        test "ein Lesen von vor dem Start überschreibt die wartende Sitzung nicht" {
            let reading = beginSessionRead (freshModel ())
            let staleVersion = reading.SessionReadVersion
            let started = step (SyncStarted (Ok (TanRequired awaitingTan))) reading
            let model, cmd, external = update (SessionReadAnswered (staleVersion, Ok None)) started
            Expect.equal model.CurrentSession (Success (Some awaitingTan)) "Sitzung bleibt"
            Expect.isEmpty (dispatched cmd) "nichts folgt"
            Expect.equal external NoOp "kein Toast"
        }

        test "ein scheiterndes Lesen von vor dem Start legt keinen Fehler über die Sitzung" {
            let reading = beginSessionRead (freshModel ())
            let staleVersion = reading.SessionReadVersion
            let started = step (SyncStarted (Ok (ReviewReady awaitingTan))) reading
            let _, cmd, external = update (SessionReadAnswered (staleVersion, Error "Http error (0)")) started
            Expect.isEmpty (dispatched cmd) "kein Fehlerpfad"
            Expect.equal external NoOp "kein Toast"
        }

        test "ein gescheiterter Start hält ein altes Lesen ebenfalls ab" {
            let reading = beginSessionRead (freshModel ())
            let staleVersion = reading.SessionReadVersion
            let failed = step (SyncStarted (Error SyncError.TanTimeout)) reading
            let _, cmd, _ = update (SessionReadAnswered (staleVersion, Ok None)) failed
            Expect.isEmpty (dispatched cmd) "verworfen"
        }

        // Das Nachlesen (ADR 0071) darf nicht an einer Antwort von vor dem Abbruch enden.
        test "während des Nachlesens gilt nur das neueste Lesen" {
            let older = beginSessionRead (interrupted ())
            let newer = beginSessionRead older
            let model, cmd, external = update (SessionReadAnswered (older.SessionReadVersion, Ok None)) newer
            Expect.isSome model.InterruptedStart "Nachlesen läuft weiter"
            Expect.isEmpty (dispatched cmd) "kein Abschluss"
            Expect.equal external NoOp "kein Toast"
        }

        test "ein neuerer Lesevorgang verdrängt den älteren" {
            let older = beginSessionRead (freshModel ())
            let newer = beginSessionRead older
            let _, cmd, _ = update (SessionReadAnswered (older.SessionReadVersion, Ok None)) newer
            Expect.isEmpty (dispatched cmd) "verworfen"
        }

        test "ein Lesen von vor der TAN-Bestätigung ändert den Fortschritt nicht" {
            let waiting = { freshModel () with CurrentSession = Success (Some awaitingTan) }
            let reading = beginSessionRead { waiting with InterruptedStart = Some { Reason = "x"; ReadsLeft = 1; ReadPlanned = false } }
            let confirmed = beginTanConfirm awaitingTan reading
            let _, cmd, _ = update (SessionReadAnswered (reading.SessionReadVersion, Ok None)) confirmed
            Expect.isEmpty (dispatched cmd) "verworfen"
            Expect.isTrue confirmed.IsTanConfirming "Bestätigung läuft"
        }

        test "eine abgebrochene Sitzung kommt durch ein altes Lesen nicht zurück" {
            let reading = beginSessionRead { freshModel () with CurrentSession = Success (Some awaitingTan) }
            let staleVersion = reading.SessionReadVersion
            let cancelled = step (SyncCancelled (Ok ())) reading
            let _, cmd, _ = update (SessionReadAnswered (staleVersion, Ok (Some awaitingTan))) cancelled
            Expect.isEmpty (dispatched cmd) "verworfen"
        }

        test "die Antwort des aktuellen Buchungs-Abrufs wird übernommen" {
            let reading = beginTransactionsRead (freshModel ())
            let _, cmd, _ = update (TransactionsReadAnswered (reading.TransactionsReadVersion, Ok [ reviewTx ])) reading
            Expect.equal (dispatched cmd) [ TransactionsLoaded (Ok [ reviewTx ]) ] "gilt"
        }

        test "ein alter Buchungs-Abruf legt keinen Fehler über fertige Daten" {
            let older = beginTransactionsRead (freshModel ())
            let newer = beginTransactionsRead older
            let loaded = step (TransactionsLoaded (Ok [ reviewTx ])) newer
            let stale = TransactionsReadAnswered (older.TransactionsReadVersion, Error SyncError.TanTimeout)
            let model, cmd, external = update stale loaded
            Expect.equal model.SyncTransactions (Success [ reviewTx ]) "Daten bleiben"
            Expect.isEmpty (dispatched cmd) "kein Fehlerpfad"
            Expect.equal external NoOp "kein Toast"
        }

        test "nach dem Abbruch der Sitzung kommen keine Buchungen mehr an" {
            let reading = beginTransactionsRead (freshModel ())
            let cancelled = step (SyncCancelled (Ok ())) reading
            let _, cmd, _ = update (TransactionsReadAnswered (reading.TransactionsReadVersion, Ok [ reviewTx ])) cancelled
            Expect.isEmpty (dispatched cmd) "verworfen"
            Expect.equal cancelled.SyncTransactions NotAsked "nichts lädt mehr"
        }

        test "ein neuer Start verwirft den laufenden Buchungs-Abruf und lässt nichts im Ladezustand" {
            let reading = beginTransactionsRead (beginSessionRead (freshModel ()))
            let started = beginSyncStart reading
            Expect.equal started.SyncTransactions NotAsked "lädt nicht mehr"
            let _, cmd, _ = update (TransactionsReadAnswered (reading.TransactionsReadVersion, Ok [ reviewTx ])) started
            Expect.isEmpty (dispatched cmd) "Buchungen verworfen"
            let _, sessionCmd, _ = update (SessionReadAnswered (reading.SessionReadVersion, Ok None)) started
            Expect.isEmpty (dispatched sessionCmd) "Sitzung verworfen"
            let loaded = { freshModel () with SyncTransactions = Success [ reviewTx ] }
            Expect.equal (abandonTransactionsRead loaded).SyncTransactions (Success [ reviewTx ]) "fertige Daten bleiben"
        }

        test "ein altes Auffrischen der Konten meldet keinen Fehler über die neuere Liste" {
            let older = beginAccountsRead { freshModel () with Accounts = Success [ cashAccount ] }
            let newer = beginAccountsRead older
            let answered = step (AccountsLoaded (newer.AccountsReadVersion, Ok [ cashAccount ])) newer
            let model, _, external = update (AccountsLoaded (older.AccountsReadVersion, Error "weg")) answered
            Expect.equal model.Accounts (Success [ cashAccount ]) "Liste bleibt"
            Expect.equal external NoOp "kein Toast"
        }

        test "eine alte Kontenliste ersetzt die neuere nicht" {
            let other = ynabAccount otherAccountId "Tagesgeld"
            let older = beginAccountsRead { freshModel () with Accounts = Success [ other ] }
            let newer = beginAccountsRead older
            let answered = step (AccountsLoaded (newer.AccountsReadVersion, Ok [ cashAccount ])) newer
            let model = step (AccountsLoaded (older.AccountsReadVersion, Ok [ other ])) answered
            Expect.equal model.Accounts (Success [ cashAccount ]) "neuere Liste gilt"
        }

        test "eine alte Quick-Add-Id ersetzt die neuere nicht" {
            let older = beginQuickAddRead (freshModel ())
            let newer = beginQuickAddRead older
            let answered = step (QuickAddAccountLoaded (newer.QuickAddReadVersion, Ok (Some cashAccountId))) newer
            let model = step (QuickAddAccountLoaded (older.QuickAddReadVersion, Ok (Some otherAccountId))) answered
            Expect.equal model.QuickAddAccountId (Some cashAccountId) "neuere Id gilt"
        }
    ]
