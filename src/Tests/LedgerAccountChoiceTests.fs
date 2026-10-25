module LedgerAccountChoiceTests

// ledger-ybmqc: gemerkte Konto-Wahl und Konto-Wechsler am Handy. Reine Funktionen und der
// Reducer ohne DOM. Neuladen und Sheet im echten Browser prüft
// `scripts/ledger-account-choice.test.mjs`.

open System
open System.IO
open Expecto
open Shared.Domain
open Components.Ledger.Types
open Components.Ledger.State

let private repoRoot =
    Directory.GetParent(Directory.GetParent(__SOURCE_DIRECTORY__).FullName).FullName

let private readSource (parts: string list) =
    File.ReadAllText(Path.Combine(Array.ofList (repoRoot :: parts)))

let private idA = Guid.Parse "0195e900-0000-7000-8000-00000000ab01"
let private idB = Guid.Parse "0195e900-0000-7000-8000-00000000ab02"
let private idGone = Guid.Parse "0195e900-0000-7000-8000-00000000ab99"

let private account (id: Guid) (name: string) : LedgerAccountView =
    { Id = id; Name = name; Kind = LedgerBankAccount; YnabAccountId = None; Closed = false }

let private accountsView : LedgerAccountsView =
    { Accounts = [ account idA "Konto ybmqc A"; account idB "Konto ybmqc B" ]
      DefaultAccountId = Some idA }

let private fresh () : Model =
    let model, _ = init ()
    { model with RememberedFilter = None }

let private loaded (remembered: AccountFilter option) : Model =
    let model, _, _ = update (AccountsLoaded (Ok accountsView)) { fresh () with RememberedFilter = remembered }
    model

[<Tests>]
let tests =
    testList "LedgerAccountChoice (ledger-ybmqc)" [
        testList "Speicherwert" [
            testCase "Gesamtsicht und Konto laufen hin und zurück" <| fun () ->
                Expect.equal (decodeAccountFilter (encodeAccountFilter AllAccounts)) (Some AllAccounts) "all"
                Expect.equal (decodeAccountFilter (encodeAccountFilter (OneAccount idB))) (Some (OneAccount idB)) "konto"

            testCase "Kaputtes oder Leeres ergibt keine Wahl" <| fun () ->
                Expect.isNone (decodeAccountFilter null) "null"
                Expect.isNone (decodeAccountFilter "") "leer"
                Expect.isNone (decodeAccountFilter "account:kein-guid") "kein Guid"
                Expect.isNone (decodeAccountFilter "irgendwas") "unbekannt"
        ]

        testList "Vorauswahl beim Öffnen" [
            testCase "gespeichertes, vorhandenes Konto wird gewählt" <| fun () ->
                Expect.equal (loaded (Some (OneAccount idB))).SelectedFilter (Some (OneAccount idB)) "Konto B"

            testCase "gespeichertes, nicht mehr vorhandenes Konto fällt auf das Standardkonto" <| fun () ->
                Expect.equal (loaded (Some (OneAccount idGone))).SelectedFilter (Some (OneAccount idA)) "Standard"

            testCase "ohne Speicher gilt das Standardkonto" <| fun () ->
                Expect.equal (loaded None).SelectedFilter (Some (OneAccount idA)) "Standard"

            testCase "gespeicherte Gesamtsicht wird gewählt" <| fun () ->
                Expect.equal (loaded (Some AllAccounts)).SelectedFilter (Some AllAccounts) "Gesamtsicht"

            testCase "eine laufende Wahl schlägt die gemerkte beim Neuladen der Konten" <| fun () ->
                let model = { fresh () with SelectedFilter = Some (OneAccount idB); RememberedFilter = Some (OneAccount idA) }
                let model', _, _ = update (AccountsLoaded (Ok accountsView)) model
                Expect.equal model'.SelectedFilter (Some (OneAccount idB)) "laufende Wahl"
        ]

        testList "Wechsel" [
            testCase "SelectFilter merkt die Wahl im Modell und schließt das Sheet" <| fun () ->
                let model = { loaded None with AccountSheetOpen = true }
                let model', _, _ = update (SelectFilter (OneAccount idB)) model
                Expect.equal model'.RememberedFilter (Some (OneAccount idB)) "gemerkt"
                Expect.equal model'.SelectedFilter (Some (OneAccount idB)) "gewählt"
                Expect.isFalse model'.AccountSheetOpen "Sheet zu"

            testCase "SelectFilter leert die Auswahl wie jeder Kontowechsel" <| fun () ->
                let model = { loaded None with Selection = Picked (Set.singleton (Guid.NewGuid())) }
                let model', _, _ = update (SelectFilter AllAccounts) model
                Expect.equal model'.Selection NoSelection "Auswahl leer"

            testCase "OpenAccountSheet und CloseAccountSheet schalten das Sheet" <| fun () ->
                let opened, _, _ = update OpenAccountSheet (loaded None)
                Expect.isTrue opened.AccountSheetOpen "offen"
                let closed, _, _ = update CloseAccountSheet opened
                Expect.isFalse closed.AccountSheetOpen "zu"
        ]

        testList "Quelltext" [
            testCase "der Wechsler nutzt den BottomSheet-Baustein und den Speicher-Schlüssel" <| fun () ->
                let view = readSource [ "src"; "Client"; "Components"; "Ledger"; "View.fs" ]
                Expect.stringContains view "accountSwitcher" "Wechsler"
                Expect.stringContains view "Title = \"Konto wechseln\"" "Sheet-Titel"
                let state = readSource [ "src"; "Client"; "Components"; "Ledger"; "State.fs" ]
                Expect.stringContains state "localStorage" "Gerätespeicher"
        ]
    ]
