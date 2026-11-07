module Tests.BudgetAssignmentsTests

// budgeting-dgrp7, ADR 0084 (Nachtrag zur Verfeinerung von qm5x9 und dgrp7): BB führt das Zugewiesen. Die reinen
// Regeln: Faltung zu Soll, bestätigtem Stand und offener Rückschreibung, Übernahme, Echo-Regel und die Planung
// von "erneut senden". Kein I/O.

open System
open Expecto
open Shared.Domain
open Shared.Budgeting
open Server.Budgeting
open Server.Budgeting.Domain
open Server.Budgeting.Assignments
open Budgeting.Events

let private monthId = "2026-11"

let private eur (milliunits: int64) : Money =
    { Amount = decimal milliunits / 1000m; Currency = "EUR" }

let private raw (categories: (Guid * int64) list) : YnabMonth =
    { Month = "2026-11-01"
      ToBeBudgeted = eur 0L
      Categories =
        categories
        |> List.map (fun (id, assigned) ->
            { Id = id
              Name = "K"
              GroupName = "G"
              Assigned = eur assigned
              Activity = eur 0L
              Available = eur assigned
              Target = None
              Hidden = false
              Deleted = false
              Internal = false }) }

let private takeover (categories: (Guid * int64) list) : BudgetEvent =
    BudgetEvent.BudgetTakenOverFromYnab
        { Month = monthId
          Categories = categories |> List.map (fun (id, v) -> { CategoryId = id; CategoryName = "K"; Assigned = v }) }

let private assigned (category: Guid) (previous: int64) (value: int64) : BudgetEvent =
    BudgetEvent.AssignmentChangedInBb
        { Month = monthId
          Line = { LineId = Guid.NewGuid(); CategoryId = category; CategoryName = "K"; PreviousAssigned = previous; Assigned = value } }

let private ref' (decision: Guid) (category: Guid) : WriteBackRef = { DecisionEventId = decision; CategoryId = category }

let private withIds (events: BudgetEvent list) : (Guid * BudgetEvent) list =
    events |> List.map (fun e -> Guid.NewGuid(), e)

let private state (soll: int64) (confirmed: int64) (openId: Guid option) : AssignmentState =
    { Assigned = soll; Confirmed = confirmed; Open = openId }

let private typesOf (events: BudgetEvent list) = events |> List.map eventTypeName

let private echoTests =
    let c = Guid.NewGuid()
    let decision = Guid.NewGuid()
    let echo s ynab = echoOf monthId c "K" s ynab
    testList "Echo-Regel" [
        test "YNAB zeigt das Soll ohne offene Rückschreibung: nichts" {
            Expect.isEmpty (echo (state 20000L 20000L None) 20000L) "kein Ereignis"
        }
        test "YNAB zeigt das Soll mit offener Rückschreibung: sie ist geschrieben" {
            Expect.equal
                (echo (state 20000L 10000L (Some decision)) 20000L)
                [ BudgetEvent.AssignmentWrittenBackToYnab (ref' decision c) ]
                "geschrieben"
        }
        test "YNAB zeigt den bestätigten Stand mit offener Rückschreibung: sie bleibt offen, ausstehend wie fehlgeschlagen" {
            // Ausstehend (festgehalten, nie gesendet) und fehlgeschlagen sind dieselbe Lage: Open gesetzt, YNAB alt.
            Expect.isEmpty (echo (state 20000L 10000L (Some decision)) 10000L) "kein Ereignis"
        }
        test "Ein anderer Wert ohne offene Rückschreibung ist eine Beobachtung vom Soll zum YNAB-Wert" {
            Expect.equal
                (echo (state 20000L 20000L None) 35000L)
                [ BudgetEvent.AssignmentChangeObservedInYnab
                      { Month = monthId; CategoryId = c; CategoryName = "K"; PreviousAssigned = 20000L; Assigned = 35000L } ]
                "Beobachtung"
        }
        test "Ein anderer Wert mit offener Rückschreibung ist eine Beobachtung, die Rückschreibung ist in YNAB ersetzt" {
            Expect.equal
                (echo (state 20000L 10000L (Some decision)) 35000L)
                [ BudgetEvent.AssignmentChangeObservedInYnab
                      { Month = monthId; CategoryId = c; CategoryName = "K"; PreviousAssigned = 20000L; Assigned = 35000L }
                  BudgetEvent.AssignmentWriteBackSupersededInYnab (ref' decision c) ]
                "Beobachtung und ersetzt"
        }
        // Review-Fund dgrp7 (QA 1): Soll gleich bestätigtem Stand bei offener Rückschreibung. Prüfte die Regel den
        // bestätigten Stand vor dem Soll, bliebe B offen, obwohl YNAB ihren Wert zeigt.
        test "A (10 auf 20) offen, B (20 auf 10) zieht A zurück, YNAB zeigt 10: B ist geschrieben" {
            let a, b = Guid.NewGuid(), Guid.NewGuid()
            let fold =
                foldMonth
                    [ Guid.Empty, takeover [ c, 10000L ]
                      a, assigned c 10000L 20000L
                      b, assigned c 20000L 10000L
                      Guid.Empty, BudgetEvent.AssignmentWriteBackWithdrawn (ref' a c) ]
            Expect.equal (stateOf fold c) (state 10000L 10000L (Some b)) "B offen, Soll gleich bestätigtem Stand"
            Expect.equal (echo (stateOf fold c) 10000L) [ BudgetEvent.AssignmentWrittenBackToYnab (ref' b c) ] "geschrieben"
        }
        test "Eine Kategorie, die in der YNAB-Antwort fehlt, ergibt nichts" {
            let fold = foldMonth (withIds [ takeover [ c, 10000L ] ])
            Expect.isEmpty (echoEvents monthId fold (raw [])) "kein Ereignis"
        }
        test "Eine interne Kategorie vergleicht BB nicht" {
            let inflow = Guid.NewGuid()
            let month = raw [ inflow, 50000L ]
            let month = { month with Categories = month.Categories |> List.map (fun x -> { x with Internal = true }) }
            Expect.isEmpty (echoEvents monthId (foldMonth (withIds [ takeover [] ])) month) "kein Ereignis"
        }
    ]

let private foldTests =
    let c = Guid.NewGuid()
    let decide value previous = Guid.NewGuid(), assigned c previous value
    let after events = stateOf (foldMonth events) c
    let start = Guid.Empty, takeover [ c, 10000L ]
    testList "Faltung" [
        test "Übernahme: Soll und bestätigter Stand sind der übernommene Wert, nichts offen" {
            Expect.equal (after [ start ]) (state 10000L 10000L None) "Startstand"
        }
        // Regressionstest (Review hbhzf): seit budgeting-eh8xe gibt es Target-Ereignisse, `applyEvent` kannte sie nicht
        // und brach mit MatchFailureException ab. Sie gehören in den Target-Stream und ändern das Zugewiesen nicht.
        test "Target-Ereignisse ändern das Zugewiesen nicht" {
            let rule : Target = { Goal = { Amount = 50m; Currency = "EUR" }; Rhythm = Months 1; Kind = Need; Date = None; NeedsWholeAmount = None; Day = None; CreatedIn = None }
            let targetEvents =
                [ BudgetEvent.TargetTakenOverFromYnab { CategoryName = "K"; Rule = rule; YnabGoalType = "NEED" }
                  BudgetEvent.TargetSet { Rule = rule; EffectiveFrom = monthId }
                  BudgetEvent.TargetRemoved { EffectiveFrom = monthId } ]
            Expect.equal (after (start :: withIds targetEvents)) (state 10000L 10000L None) "unverändert"
        }
        test "Entscheidung: Soll ist der neue Wert, sie ist die offene Rückschreibung" {
            let id, e = decide 20000L 10000L
            Expect.equal (after [ start; id, e ]) (state 20000L 10000L (Some id)) "offen"
        }
        test "Verschieben wirkt je Seite, Targets anwenden je Zeile" {
            let d = Guid.NewGuid()
            let move =
                BudgetEvent.MoneyMoved
                    { Month = monthId
                      From = { LineId = Guid.NewGuid(); CategoryId = c; CategoryName = "K"; PreviousAssigned = 10000L; Assigned = 5000L }
                      To = { LineId = Guid.NewGuid(); CategoryId = d; CategoryName = "L"; PreviousAssigned = 0L; Assigned = 5000L }
                      Amount = 5000L }
            let moveId = Guid.NewGuid()
            let fold = foldMonth [ start; moveId, move ]
            Expect.equal (stateOf fold c) (state 5000L 10000L (Some moveId)) "Quelle"
            Expect.equal (stateOf fold d) (state 5000L 0L (Some moveId)) "Ziel"
            let applyId = Guid.NewGuid()
            let applied =
                BudgetEvent.TargetsApplied
                    { Month = monthId
                      Lines = [ { LineId = Guid.NewGuid(); CategoryId = d; CategoryName = "L"; PreviousAssigned = 0L; Assigned = 7000L } ]
                      SkippedPaused = [] }
            Expect.equal (stateOf (foldMonth [ start; applyId, applied ]) d) (state 7000L 0L (Some applyId)) "Zeile"
        }
        test "Geschrieben für die offene: bestätigt ist das Soll, nichts offen" {
            let id, e = decide 20000L 10000L
            Expect.equal (after [ start; id, e; Guid.Empty, BudgetEvent.AssignmentWrittenBackToYnab (ref' id c) ]) (state 20000L 20000L None) "bestätigt"
        }
        test "Fehlgeschlagen ändert nichts" {
            let id, e = decide 20000L 10000L
            Expect.equal
                (after [ start; id, e; Guid.Empty, BudgetEvent.AssignmentWriteBackFailed (ref' id c, "weg") ])
                (state 20000L 10000L (Some id))
                "bleibt offen"
        }
        test "Beobachtung: Soll und bestätigter Stand sind der YNAB-Wert, nichts offen" {
            let id, e = decide 20000L 10000L
            let observed =
                BudgetEvent.AssignmentChangeObservedInYnab
                    { Month = monthId; CategoryId = c; CategoryName = "K"; PreviousAssigned = 20000L; Assigned = 33000L }
            Expect.equal (after [ start; id, e; Guid.Empty, observed ]) (state 33000L 33000L None) "YNAB gewinnt"
        }
        test "Ersetzt in YNAB hat keine eigene Wirkung" {
            let id, e = decide 20000L 10000L
            Expect.equal
                (after [ start; id, e; Guid.Empty, BudgetEvent.AssignmentWriteBackSupersededInYnab (ref' id c) ])
                (state 20000L 10000L (Some id))
                "unverändert"
        }
        test "Zurückgezogen für die offene: das Soll fällt auf den bestätigten Stand" {
            let id, e = decide 20000L 10000L
            Expect.equal (after [ start; id, e; Guid.Empty, BudgetEvent.AssignmentWriteBackWithdrawn (ref' id c) ]) (state 10000L 10000L None) "zurück"
        }
        test "Ein Ergebnis zu einer anderen als der offenen Rückschreibung wirkt nicht" {
            let a, ea = decide 20000L 10000L
            let b, eb = decide 30000L 20000L
            let results =
                [ BudgetEvent.AssignmentWrittenBackToYnab (ref' a c)
                  BudgetEvent.AssignmentWriteBackWithdrawn (ref' a c)
                  BudgetEvent.AssignmentWriteBackFailed (ref' a c, "x") ]
            for r in results do
                Expect.equal (after [ start; a, ea; b, eb; Guid.Empty, r ]) (state 30000L 10000L (Some b)) (eventTypeName r)
        }
        test "Eine Kategorie ohne Eintrag hat Soll und bestätigten Stand 0" {
            Expect.equal (stateOf (foldMonth [ start ]) (Guid.NewGuid())) (state 0L 0L None) "leer"
        }
        test "Ereignisse vor der Übernahme sind Historie und wirken nicht" {
            let id, e = decide 50000L 0L
            Expect.equal (after [ id, e; start ]) (state 10000L 10000L None) "nur ab der Übernahme"
            Expect.isEmpty (foldMonth [ id, e ]).Categories "leer"
        }
    ]

let private ruleTests =
    let c = Guid.NewGuid()
    testList "Eine offene Rückschreibung je Kategorie" [
        test "A (10 auf 20) offen, dann B (20 auf 30): B zieht A zurück, Echo und erneut senden passen dazu" {
            let a = Guid.NewGuid()
            let afterA = foldMonth [ Guid.Empty, takeover [ c, 10000L ]; a, assigned c 10000L 20000L ]
            Expect.equal (withdrawalsFor afterA [ c ]) [ ref' a c ] "B hängt Withdrawn(A) an"
            let b = Guid.NewGuid()
            let afterB =
                foldMonth
                    [ Guid.Empty, takeover [ c, 10000L ]
                      a, assigned c 10000L 20000L
                      b, assigned c 20000L 30000L
                      Guid.Empty, BudgetEvent.AssignmentWriteBackWithdrawn (ref' a c) ]
            Expect.equal (stateOf afterB c) (state 30000L 10000L (Some b)) "B ist offen"
            Expect.isEmpty (echoEvents monthId afterB (raw [ c, 10000L ])) "YNAB 10: nichts"
            Expect.equal
                (echoEvents monthId afterB (raw [ c, 30000L ]))
                [ BudgetEvent.AssignmentWrittenBackToYnab (ref' b c) ]
                "YNAB 30: nur B geschrieben"
        }
        test "Ohne offene Rückschreibung zieht eine Entscheidung nichts zurück" {
            Expect.isEmpty (withdrawalsFor (foldMonth [ Guid.Empty, takeover [ c, 10000L ] ]) [ c ]) "nichts"
        }
        test "Eine Verschiebung in YNAB ergibt genau zwei Beobachtungen, beim nächsten Vergleich keine" {
            let d = Guid.NewGuid()
            let before = withIds [ takeover [ c, 50000L; d, 10000L ] ]
            let moved = raw [ c, 30000L; d, 30000L ]
            let added, fold = observe monthId before moved
            Expect.equal (typesOf added) [ "AssignmentChangeObservedInYnab"; "AssignmentChangeObservedInYnab" ] "zwei Beobachtungen"
            let again, _ = observe monthId (before @ withIds added) moved
            Expect.isEmpty again "beim zweiten Vergleich nichts"
            Expect.equal (stateOf fold c, stateOf fold d) (state 30000L 30000L None, state 30000L 30000L None) "Soll ist YNAB"
        }
        test "Ein YNAB-Wert neben dem 10-Milliunits-Raster ergibt höchstens beim ersten Vergleich ein Ereignis" {
            let before = withIds [ takeover [ c, 10000L ] ]
            let offGrid = raw [ c, 12345L ]
            let added, _ = observe monthId before offGrid
            Expect.equal (typesOf added) [ "AssignmentChangeObservedInYnab" ] "einmal"
            let again, fold = observe monthId (before @ withIds added) offGrid
            Expect.isEmpty again "danach nichts: ungerundet übernommen"
            Expect.equal (stateOf fold c).Assigned 12345L "ungerundet"
            let takenOff, takenFold = observe monthId [] offGrid
            Expect.equal (typesOf takenOff) [ "BudgetTakenOverFromYnab" ] "Übernahme neben dem Raster ohne Beobachtung"
            Expect.equal (stateOf takenFold c).Assigned 12345L "ungerundet übernommen"
        }
    ]

let private takeoverTests =
    testList "Übernahme" [
        test "Alle Kategorien außer internal, auch ausgeblendete und gelöschte, Namen ohne Leerraum" {
            let visible, hidden, deleted, inflow = Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()
            let month = raw [ visible, 1000L; hidden, 2000L; deleted, 3000L; inflow, 0L ]
            let month =
                { month with
                    Categories =
                        month.Categories
                        |> List.map (fun x ->
                            { x with
                                Name = " K "
                                Hidden = (x.Id = hidden)
                                Deleted = (x.Id = deleted)
                                Internal = (x.Id = inflow) }) }
            match takeoverEvents monthId [] month with
            | [ BudgetEvent.BudgetTakenOverFromYnab p ] ->
                Expect.equal p.Month monthId "Monat"
                Expect.equal
                    (p.Categories |> List.map (fun x -> x.CategoryId, x.CategoryName, x.Assigned))
                    [ visible, "K", 1000L; hidden, "K", 2000L; deleted, "K", 3000L ]
                    "ohne internal"
            | other -> failtest $"eine Übernahme erwartet: {other}"
        }
        test "Offene Rückschreibungen migrierter Entscheidungen schließt die Übernahme mit Zurückziehen" {
            let c, d, e = Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()
            let pending, failed, written = Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()
            let migrated =
                [ pending, assigned c 0L 10000L
                  failed, assigned d 0L 20000L
                  Guid.NewGuid(), BudgetEvent.AssignmentWriteBackFailed (ref' failed d, "Rate-Limit")
                  written, assigned e 0L 30000L
                  Guid.NewGuid(), BudgetEvent.AssignmentWrittenBackToYnab (ref' written e) ]
            let events = takeoverEvents monthId migrated (raw [ c, 0L; d, 0L; e, 30000L ])
            Expect.equal
                (events |> List.tail)
                [ BudgetEvent.AssignmentWriteBackWithdrawn (ref' pending c)
                  BudgetEvent.AssignmentWriteBackWithdrawn (ref' failed d) ]
                "ausstehend und fehlgeschlagen zurückgezogen, geschrieben bleibt"
            Expect.equal (typesOf events |> List.head) "BudgetTakenOverFromYnab" "Übernahme zuerst"
        }
        test "Liegt schon eine Übernahme im Stream, entsteht keine zweite" {
            Expect.isEmpty (takeoverEvents monthId (withIds [ takeover [] ]) (raw [ Guid.NewGuid(), 1000L ])) "nichts"
        }
        test "Nach der Übernahme im selben Vergleich gibt es keine Beobachtung" {
            let c = Guid.NewGuid()
            let added, fold = observe monthId [] (raw [ c, 40000L ])
            Expect.equal (typesOf added) [ "BudgetTakenOverFromYnab" ] "nur die Übernahme"
            Expect.equal (stateOf fold c) (state 40000L 40000L None) "Startstand"
        }
    ]

let private retryTests =
    let line openNow latest inYnab = { CategoryId = Guid.NewGuid(); Latest = latest; Open = openNow; InYnab = inYnab }
    let failed = Some (Failed "weg")
    testList "Erneut senden planen" [
        test "Eine offene Zeile wird gesendet, eine geschlossene bleibt, eine fehlende schließt fehlgeschlagen" {
            Expect.equal
                (planRetry false [ line true failed true; line false (Some Written) true; line true None false ])
                [ Send; Leave; Close (WriteBackFailed CategoryNotFoundText) ]
                "je Zeile"
        }
        test "Verschiebung: Quelle offen, Ziel offen: beide senden, das Ziel nur nach geschriebener Quelle" {
            Expect.equal (planRetry true [ line true failed true; line true failed true ]) [ Send; Send ] "beide"
        }
        test "Verschiebung: Ziel in YNAB ersetzt, die Quelle wird zurückgezogen" {
            Expect.equal
                (planRetry true [ line true failed true; line false (Some ChangedInYnab) true ])
                [ Close Withdrawn; Leave ]
                "Quelle zurück"
        }
        // budgeting-my8p8: vorher las sich ein zurückgezogenes Ziel als "in YNAB geändert", die Paarregel griff
        // über diesen Stand. Mit eigenem Stand muss sie ihn ausdrücklich prüfen.
        test "Verschiebung: Ziel in BB zurückgezogen, die Quelle wird zurückgezogen" {
            Expect.equal
                (planRetry true [ line true failed true; line false (Some WithdrawnInBb) true ])
                [ Close Withdrawn; Leave ]
                "Quelle zurück"
        }
        test "Verschiebung: Quelle in BB zurückgezogen, das Ziel wird zurückgezogen" {
            Expect.equal
                (planRetry true [ line false (Some WithdrawnInBb) true; line true None true ])
                [ Leave; Close Withdrawn ]
                "Ziel zurück"
        }
        test "Eine in BB zurückgezogene Zeile ist geschlossen wie eine in YNAB geänderte" {
            Expect.isFalse (Domain.isOpenLine (Some WithdrawnInBb)) "zurückgezogen"
            Expect.isFalse (Domain.isOpenLine (Some ChangedInYnab)) "in YNAB geändert"
            Expect.isTrue (Domain.isOpenLine (Some (Failed "weg"))) "fehlgeschlagen bleibt offen"
        }
        test "Verschiebung: Quelle in YNAB ersetzt, das Ziel wird zurückgezogen" {
            Expect.equal
                (planRetry true [ line false (Some ChangedInYnab) true; line true None true ])
                [ Leave; Close Withdrawn ]
                "Ziel zurück, sein Soll fällt auf den bestätigten Stand"
        }
        test "Verschiebung: Quelle geschrieben, Ziel offen: das Ziel geht hinaus" {
            Expect.equal (planRetry true [ line false (Some Written) true; line true failed true ]) [ Leave; Send ] "Ziel"
        }
        test "Verschiebung: Ziel fehlt in YNAB, die Quelle bleibt ungesendet fehlgeschlagen" {
            Expect.equal
                (planRetry true [ line true failed true; line true failed false ])
                [ Close (WriteBackFailed SourceHeldBackText); Close (WriteBackFailed CategoryNotFoundText) ]
                "beide fehlgeschlagen"
        }
        test "Verschiebung: Quelle fehlt in YNAB, das Ziel bleibt unberührt" {
            Expect.equal
                (planRetry true [ line true failed false; line true failed true ])
                [ Close (WriteBackFailed CategoryNotFoundText); Leave ]
                "Ziel unberührt"
        }
    ]

let private monthTests =
    let category (id: Guid) : YnabMonthCategory =
        (raw [ id, 10000L ]).Categories.Head
    testList "Monat mit BBs Soll" [
        test "Zugewiesen ist das Soll, die Markierung trägt die offene Rückschreibung; Verfügbar bleibt YNABs" {
            let c, d = Guid.NewGuid(), Guid.NewGuid()
            let decision = Guid.NewGuid()
            let fold = Map.ofList [ c, state 25000L 10000L (Some decision) ]
            let month = toBudgetMonthWith (Some fold) (Some Map.empty) Map.empty Set.empty { raw [] with Categories = [ category c; category d ] }
            let byId id = month.Groups |> List.collect (fun g -> g.Categories) |> List.find (fun x -> x.Id = id)
            Expect.equal ((byId c).Assigned, (byId c).OpenWriteBack, (byId c).Available) (eur 25000L, Some decision, eur 10000L) "offen"
            Expect.equal ((byId d).Assigned, (byId d).OpenWriteBack) (eur 0L, None) "ohne Eintrag 0, keine Markierung"
            let plain = toBudgetMonth Map.empty Set.empty { raw [] with Categories = [ category c ] }
            Expect.equal (plain.Groups.Head.Categories.Head.Assigned, plain.Groups.Head.Categories.Head.OpenWriteBack) (eur 10000L, None) "ohne Faltung YNABs Wert"
        }
    ]

[<Tests>]
let tests =
    testList "Budget führen (dgrp7)" [ echoTests; foldTests; ruleTests; takeoverTests; retryTests; monthTests ]
