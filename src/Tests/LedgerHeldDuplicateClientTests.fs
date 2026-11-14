module LedgerHeldDuplicateClientTests

// Eine wartende Buchung im Kassenbuch, Client-Seite (banking-import-d0fey, ADR 0093 Punkt 3 und 6): „doppelt?“
// in der Zeile statt „Nicht in YNAB“, die gelbe Leiste im Detail und jede Aktion gesperrt. Sichtbarkeit und
// Sperre sind reine Funktionen, die View ruft sie nur auf (Doktrin Punkt 1).

open System
open Expecto
open Shared.Domain
open Types
open Components.Ledger.Types
open Components.Ledger.View

let private eur (amount: decimal) : Money = { Amount = amount; Currency = "EUR" }

let private entryOf (n: int) : LedgerEntryView =
    { Id = Guid.Parse $"0195f810-0000-7000-8000-{n:D12}"
      AccountId = Guid.Parse "0195e800-0000-7000-8000-000000000001"
      Date = DateTime.Today
      Amount = eur -42.80m
      Payee = Some $"Payee d0fey {n}"
      Memo = None
      CategoryName = Some "Lebensmittel"
      CategoryId = Some (Guid.Parse "0195d800-0000-7000-8000-000000000001")
      CategoryGroupName = Some "Alltag"
      Reconciled = false
      SplitCategoryGroupNames = Map.empty
      Splits = []
      Origin = MirroredFromYnab
      DeletedInYnab = false
      NeedsReview = true
      Flag = NoFlag
      IsTransfer = false
      TransferAccountId = None
      ReviewMarkWriteBackFailure = None
      CategorizationWriteBackFailure = None
      FlagWriteBackFailure = None
      MemoWriteBackFailure = None
      AmountWriteBackFailure = None
      AmountLockReason = None
      LinkedToYnab = false
      PushToYnabFailure = None; SplitLocks = None; ExternalLinks = []; PayeeWriteBackFailure = None
      PossibleDuplicate = None; CategoryFromOrderId = false }

let private held (n: int) : LedgerEntryView =
    { entryOf n with
        PossibleDuplicate =
            Some { Reason = "In YNAB steht eine Buchung mit gleichem Betrag am 10.11.2026 bei Markt."
                   MatchDate = DateTime(2026, 11, 10)
                   MatchAmount = eur -42.80m
                   MatchPayee = Some "Markt"
                   MatchCategoryName = Some "Lebensmittel"
                   MatchEntryId = None } }

let private lenaColor : RemoteData<string option> = Success (Some "purple")

[<Tests>]
let tests =
    testList "Wartende Buchung im Kassenbuch (Client)" [

        testCase "die Zeile trägt doppelt? statt Nicht in YNAB" <| fun () ->
            Expect.equal (rowStatusMark (held 1)) RowPossibleDuplicate "Handy-Zeile"
            Expect.equal (payeeMarks (held 1)) [ PayeePossibleDuplicate ] "Payee-Zelle am PC"
            Expect.equal (rowStatusMark (entryOf 1)) RowNotInYnab "ohne Wartemarke wie bisher"
            Expect.equal (rowStatusMark { held 1 with DeletedInYnab = true }) RowDeleted "gelöscht hat Vorrang"

        testCase "das Detail zeigt die gelbe Leiste ohne Aktion" <| fun () ->
            Expect.equal (detailNoticeFor None NotAsked (held 1)) (Some PossibleDuplicateNotice) "Leiste"
            Expect.equal (detailNoticeActionMsg (held 1).Id PossibleDuplicateNotice) None "kein Erneut senden"
            Expect.equal possibleDuplicateNoticeBadge "Vielleicht doppelt." "Wort"
            Expect.equal possibleDuplicateNoticeReason "Geht erst nach deiner Entscheidung nach YNAB." "Satz"
            match detailNoticeFor None NotAsked (entryOf 1) with
            | Some (NotInYnabNotice _) -> ()
            | other -> failtestf "ohne Wartemarke Noch nicht in YNAB: %A" other

        testCase "jede Aktion im Detail und an der Zeile ist mit dem Satz aus ADR 0093 gesperrt" <| fun () ->
            let entry = held 1
            Expect.equal (heldLockReason entry) (Some possibleDuplicateLockedText) "Grund"
            Expect.equal (categoryLockFor entry) CategoryLockedHeld "Kategorie"
            Expect.equal (categoryLockHint CategoryLockedHeld) (Some possibleDuplicateLockedText) "Kategorie-Hinweis"
            Expect.equal (splitLockTextFor entry) (Some possibleDuplicateLockedText) "Aufteilen"
            Expect.equal (splitActionMsg entry) None "Aufteilen tut nichts"
            Expect.equal (lenaActionFor lenaColor entry).DisabledReason (Some possibleDuplicateLockedText) "Lena"
            Expect.equal (memoActionFor entry).DisabledReason (Some possibleDuplicateLockedText) "Memo"
            Expect.equal (amountLockFor entry) (Some possibleDuplicateLockedText) "Betrag"
            Expect.equal (payeeLockFor entry) (Some possibleDuplicateLockedText) "Payee"
            Expect.isFalse (detailReviewSwitchVisible entry) "kein Geprüft-Schalter"
            Expect.isFalse (swipeAppliesTo entry) "kein Wisch"
            Expect.isFalse (detailDeleteVisible entry) "kein Löschen"

        testCase "ohne Wartemarke bleiben die Aktionen frei" <| fun () ->
            let entry = { entryOf 1 with LinkedToYnab = true }
            Expect.equal (heldLockReason entry) None "kein Grund"
            Expect.equal (categoryLockFor entry) CategoryEditable "Kategorie"
            Expect.equal (lenaActionFor lenaColor entry).DisabledReason None "Lena"
            Expect.equal (memoActionFor entry).DisabledReason None "Memo"
            Expect.isTrue (detailReviewSwitchVisible entry) "Schalter"
            Expect.isTrue (swipeAppliesTo entry) "Wisch"

        testCase "Sammelbefehle nennen übersprungene wartende Buchungen im Toast" <| fun () ->
            let reviewOutcome : ReviewMarkBatchOutcome = { Changed = []; UnchangedCount = 0; FailedCount = 0; HeldCount = 2 }
            Expect.equal (reviewBatchToastText reviewOutcome) "Keine Buchung geprüft (2 vielleicht doppelt)" "Geprüft"
            Expect.equal (reviewBatchToastType reviewOutcome) ToastWarning "Warnung"
            let flagOutcome : FlagBatchOutcome = { Changed = []; UnchangedCount = 0; FailedCount = 0; HeldCount = 1 }
            Expect.equal (flagBatchToastText AssignLena flagOutcome) "Keine Buchung an Lena gefragt (1 vielleicht doppelt)" "Lena"
            Expect.equal (flagBatchToastType flagOutcome) ToastWarning "Warnung"
            let categoryOutcome : CategoryBatchOutcome = { Changed = []; UnchangedCount = 0; Skipped = [ SkipPossibleDuplicate ] }
            Expect.contains (categoryBatchSkipReasons categoryOutcome) "vielleicht doppelt" "Kategorie"
    ]
