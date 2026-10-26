module Tests.ToastLifecycleTests

// Pure-logic tests for the toast two-phase removal (design-system-004).
// The lifecycle is: a toast is first marked "exiting" (which triggers the CSS
// exit animation in the view), then removed from the list after the exit
// duration. The pure helpers in Types.Toast model that exiting→removed
// transition so it is testable without an Elmish runtime. The critical
// invariant is the double-fire guard: a rapid second dismiss (auto-dismiss
// timer firing while the user also clicks close, or a double click) must NOT
// restart the lifecycle or it would schedule a duplicate removal timer and a
// leaked / double removal.

open System
open Expecto
open Types

// ============================================
// Helpers
// ============================================

let private mkToast (id: Guid) : Toast =
    { Id = id; Message = "msg"; Type = ToastSuccess; Exiting = false; Action = None }

let private idA = Guid.Parse "00000000-0000-0000-0000-000000000001"
let private idB = Guid.Parse "00000000-0000-0000-0000-000000000002"

module private Quelltext =
    let root =
        let rec find (dir: string) =
            if System.IO.Directory.Exists(System.IO.Path.Combine(dir, "src", "Client")) then dir
            else find (System.IO.Directory.GetParent(dir).FullName)
        find System.AppContext.BaseDirectory

[<Tests>]
let toastLifecycleTests =
    testList "Toast lifecycle" [

        testCase "markExiting flips only the matching toast's Exiting flag" <| fun () ->
            let toasts = [ mkToast idA; mkToast idB ]
            let result = Toast.markExiting idA toasts

            let a = result |> List.find (fun t -> t.Id = idA)
            let b = result |> List.find (fun t -> t.Id = idB)
            Expect.isTrue a.Exiting "Dismissed toast should be marked exiting"
            Expect.isFalse b.Exiting "Untouched toast should NOT be marked exiting"

        testCase "markExiting keeps the toast in the list (phase 1 does not remove)" <| fun () ->
            let toasts = [ mkToast idA ]
            let result = Toast.markExiting idA toasts
            Expect.equal (List.length result) 1 "Marking exiting must NOT remove the toast yet"

        testCase "markExiting is idempotent — re-marking changes nothing" <| fun () ->
            // This guards the double-fire case: auto-dismiss timer + manual close
            // both hit the same toast. The second mark must be a no-op so no
            // duplicate removal timer is scheduled.
            let toasts = [ mkToast idA ]
            let once = Toast.markExiting idA toasts
            let twice = Toast.markExiting idA once
            Expect.equal twice once "Re-marking an already-exiting toast must leave the list unchanged"

        testCase "isExiting is false before marking, true after" <| fun () ->
            // isExiting is the guard the MVU update uses to decide whether to
            // schedule the removal timer.
            let toasts = [ mkToast idA ]
            Expect.isFalse (Toast.isExiting idA toasts) "Fresh toast is not exiting"
            let marked = Toast.markExiting idA toasts
            Expect.isTrue (Toast.isExiting idA marked) "After marking, the toast is exiting"

        testCase "isExiting is false for an unknown id" <| fun () ->
            let toasts = [ mkToast idA ]
            Expect.isFalse (Toast.isExiting idB toasts) "Unknown id is never exiting"

        testCase "remove drops only the matching toast (phase 2)" <| fun () ->
            let toasts = [ mkToast idA; mkToast idB ]
            let result = Toast.remove idA toasts
            Expect.equal (List.map (fun t -> t.Id) result) [ idB ] "Only the removed id is gone"

        testCase "remove of an already-gone id is a safe no-op" <| fun () ->
            // Guards double removal: if the timer fires after a toast is already
            // gone, removing again must not throw or corrupt the list.
            let toasts = [ mkToast idB ]
            let result = Toast.remove idA toasts
            Expect.equal (List.map (fun t -> t.Id) result) [ idB ] "Removing an absent id leaves the list intact"

        testCase "exitDurationMs is positive" <| fun () ->
            Expect.isGreaterThan Toast.exitDurationMs 0 "Exit duration must be a real positive delay"
    

        testCase "Ein Toast ohne Aktion verhält sich wie bisher (Aktion fehlt, Lebenszyklus gleich)" <| fun () ->
            let t = mkToast idA
            Expect.isNone t.Action "ohne Aktion"
            let result = Toast.markExiting idA [ t ] |> Toast.remove idA
            Expect.isEmpty result "Abgang und Entfernen wie zuvor"

        testCase "Ein Toast kann eine Aktion mit Beschriftung und Beschreibung der Nachricht tragen" <| fun () ->
            let previous : Shared.Domain.ReviewMarkState =
                { IsApproved = true; ClearedState = Shared.Domain.ReviewUncleared }
            let t = { mkToast idA with Action = Some { Label = "Rückgängig"; Kind = UndoReviewMark (idB, previous) } }
            Expect.equal (t.Action |> Option.map (fun a -> a.Label)) (Some "Rückgängig") "Beschriftung"
            Expect.equal (t.Action |> Option.map (fun a -> a.Kind)) (Some (UndoReviewMark (idB, previous))) "Nachricht"

        testCase "Die Wurzel übersetzt die Aktion in die Rückgängig-Nachricht mit genau diesem Vorzustand" <| fun () ->
            let previous : Shared.Domain.ReviewMarkState =
                { IsApproved = true; ClearedState = Shared.Domain.ReviewReconciled }
            match State.toastActionMsg (UndoReviewMark (idB, previous)) with
            | State.LedgerMsg (Components.Ledger.Types.StartUndoReviewMark (id, p)) ->
                Expect.equal id idB "Buchung"
                Expect.equal p previous "Vorzustand unverändert, auch Reconciled"
            | other -> failtestf "unerwartete Nachricht: %A" other

        testCase "Klick auf die Aktion schließt den Toast und löst die Aktion genau einmal aus" <| fun () ->
            let previous : Shared.Domain.ReviewMarkState =
                { IsApproved = false; ClearedState = Shared.Domain.ReviewUncleared }
            let toast = { mkToast idA with Action = Some { Label = "Rückgängig"; Kind = UndoReviewMark (idB, previous) } }
            let ledger, _ = Components.Ledger.State.init ()
            let settings, _ = Components.Settings.State.init ()
            let syncFlow, _ = Components.SyncFlow.State.init ()
            let rules, _ = Components.Rules.State.init ()
            let model : State.Model =
                { CurrentPage = Ledger; Toasts = [ toast ]; Pwa = Pwa.initialState; Settings = settings; SyncFlow = syncFlow
                  Rules = rules; Ledger = ledger; Budget = Components.Budget.State.init (); QuickAdd = None; QuickAddTemplates = NotAsked }
            let after, cmd = State.update (State.ToastActionClicked idA) model
            Expect.isTrue (Toast.isExiting idA after.Toasts) "Toast geht ab"
            Expect.isNonEmpty cmd "Aktion und Entfernen sind geplant"
            let again, cmd2 = State.update (State.ToastActionClicked idA) after
            Expect.equal again after "zweiter Klick ändert nichts"
            Expect.isEmpty cmd2 "zweiter Klick löst nichts mehr aus"

        testCase "Toast.fs rendert die Aktion als Schaltfläche, die OnAction auslöst" <| fun () ->
            let source = System.IO.File.ReadAllText(System.IO.Path.Combine(Quelltext.root, "src", "Client", "DesignSystem", "Toast.fs"))
            Expect.stringContains source "match props.Action with" "Aktion wird gerendert"
            Expect.stringContains source "props.OnAction props.Id" "Klick löst OnAction aus"
            Expect.stringContains source "toast-above-nav" "Toasts mit Aktion sitzen über der Tab-Bar"

        testCase "Die Galerie /styleguide zeigt den Toast mit Aktion" <| fun () ->
            let source = System.IO.File.ReadAllText(System.IO.Path.Combine(Quelltext.root, "src", "Client", "Components", "Styleguide", "View.fs"))
            Expect.stringContains source "Toast.renderItems undoItems" "Galerie rendert die Toast-Liste mit Aktion"
            Expect.stringContains source "Label = \"Rückgängig\"" "Beispiel trägt Rückgängig"
    ]
