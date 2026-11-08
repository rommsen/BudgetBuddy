module CategoryAdminClientTests

// Die Seite Kategorien im Client (budgeting-pscgb): echtes `update` unter .NET. Die Cmds mit `Api` werden nicht
// ausgeführt, gezählt wird nur, ob ein Cmd entsteht.

open System
open Expecto
open Shared.Budgeting
open Types
open Components.Budget.Categories

let private alltag = Guid.Parse "11111111-0000-0000-0000-000000000001"
let private kinder = Guid.Parse "11111111-0000-0000-0000-000000000002"
let private drogerie = Guid.Parse "22222222-0000-0000-0000-000000000002"
let private tiere = Guid.Parse "11111111-0000-0000-0000-000000000009"

let private groups : ManagedCategoryGroup list =
    [ { Id = alltag; Name = "Alltag"; Categories = [ { Id = drogerie; Name = "Drogerie"; Active = true } ] }
      { Id = kinder; Name = "Kinder"; Categories = [] } ]

let private loaded : Model = { init () with Groups = Success groups }

let private step (model: Model) (msg: Msg) = update msg model
let private model' (m, _, _) = m
let private run (start: Model) (msgs: Msg list) : Model = msgs |> List.fold (fun m msg -> model' (update msg m)) start

/// Wie `Expect.equal`, die Meldung entsteht erst beim Fehlschlag (die Orakel prüfen Tausende Folgen).
let private expectSame (actual: 'a) (expected: 'a) (message: unit -> string) =
    if actual <> expected then failtestf "%s\nerwartet: %A\nbekam: %A" (message ()) expected actual

/// Alle Folgen der Länge 1 bis `n` aus `alphabet`.
let rec private sequences (alphabet: 'a list) (n: int) : 'a list list =
    if n = 0 then [ [] ]
    else
        let shorter = sequences alphabet (n - 1)
        shorter @ [ for s in shorter do if s.Length = n - 1 then for a in alphabet do s @ [ a ] ]
        |> List.distinct
        |> List.filter (fun s -> not s.IsEmpty)

[<Tests>]
let tests =
    testList "Kategorien verwalten im Client" [
        // Doktrin Punkt 20: alle Folgen aus Eingaben und Antworten gegen ein eigenes Orakel. Eine Antwort gibt die
        // Sperre nur frei, wenn das Formular auf genau sie wartet; fremde Antworten ändern das Formular nicht.
        testCase "Speichern und Schließen sind gesperrt, bis die eigene Antwort kommt, in jeder Reihenfolge" <| fun () ->
            let forms =
                [ run loaded [ NewCategory; NameChanged "Haustier" ]
                  run loaded [ EditCategory drogerie; NameChanged "Bad" ]
                  run loaded [ RenameGroup kinder; NameChanged "Familie" ]
                  run loaded [ NewCategory; GroupChosen NewGroupChoice; NameChanged "Tiere" ]
                  // budgeting-sn2sn: Deaktivieren aus dem Formular sperrt ebenso.
                  run loaded [ EditCategory drogerie; ActiveToggled false ] ]
            let answers =
                [ CategorySaved (Ok drogerie); CategorySaved (Error "x")
                  GroupSaved ("Tiere", Ok tiere); GroupSaved ("Tiere", Error "x")
                  ActiveSaved (Ok ()); ActiveSaved (Error "x") ]
            let inputs =
                [ Save; CloseForm; NameChanged "X"; GroupChosen (string kinder); GroupChosen NewGroupChoice; ActiveToggled true; ActiveToggled false ]
            // Welcher Aufruf geht beim Speichern hinaus, und kehrt eine neue Gruppe ins Kategorie-Formular zurück?
            let callOf (editor: Editor) =
                match editor with
                | EditingCategory { CategoryId = Some _; Active = a; WasActive = w } when a <> w -> SavingActive
                | EditingCategory _ -> SavingCategory
                | EditingGroup _ -> SavingGroup
            let fieldsDiffer (editor: Editor) =
                match editor with
                | EditingCategory { CategoryId = Some id; Name = name; GroupId = groupId } ->
                    groups |> List.exists (fun g -> g.Categories |> List.exists (fun c -> c.Id = id && (c.Name <> name.Trim() || Some g.Id <> groupId)))
                | _ -> true
            for start in forms do
                for msgs in sequences (Save :: answers @ inputs) 3 do
                    // Orakel ohne den getesteten Code: offen, wartet auf welchen Aufruf, mit welchem Formular.
                    let mutable isOpen, awaiting, sentEditor = true, None, None
                    let mutable model = start
                    for msg in Save :: msgs do
                        let before = model.Form
                        let editor = before |> Option.map (fun f -> f.Editor)
                        let mutable expectSend = false
                        match msg, awaiting with
                        | Save, None when isOpen ->
                            awaiting <- Some (callOf editor.Value)
                            sentEditor <- editor
                            expectSend <- true
                        | CloseForm, None when isOpen ->
                            match editor with
                            | Some (EditingGroup { ReturnTo = Some _ }) -> ()
                            | _ -> isOpen <- false
                        | CategorySaved (Ok _), Some SavingCategory ->
                            isOpen <- false
                            awaiting <- None
                        | GroupSaved (_, Ok _), Some SavingGroup ->
                            match sentEditor with
                            | Some (EditingGroup { ReturnTo = Some _ }) -> ()
                            | _ -> isOpen <- false
                            awaiting <- None
                        | ActiveSaved (Ok ()), Some SavingActive ->
                            if fieldsDiffer sentEditor.Value then
                                awaiting <- Some SavingCategory
                            else
                                isOpen <- false
                                awaiting <- None
                        | CategorySaved (Error _), Some SavingCategory
                        | GroupSaved (_, Error _), Some SavingGroup
                        | ActiveSaved (Error _), Some SavingActive -> awaiting <- None
                        | _ -> ()
                        let next, cmd, _ = update msg model
                        model <- next
                        let way () = $"{msgs}, bei {msg}"
                        if msg = Save then expectSame (not cmd.IsEmpty) expectSend (fun () -> $"Aufruf beim Speichern: {way ()}")
                        expectSame model.Form.IsSome isOpen (fun () -> $"offen oder zu: {way ()}")
                        expectSame (model.Form |> Option.bind (fun f -> f.Saving)) awaiting (fun () -> $"Sperre: {way ()}")
                        // Solange gewartet wird, ändert nur die eigene Antwort das Formular.
                        match before with
                        | Some { Saving = Some call } when awaiting = Some call ->
                            expectSame model.Form before (fun () -> $"Formular unverändert: {way ()}")
                        | _ -> ()

        testCase "ein Fehler des Servers bleibt im Formular, die Liste bleibt, eine Eingabe nimmt ihn weg" <| fun () ->
            let saving = run loaded [ NewCategory; NameChanged "Drogerie"; Save ]
            let failed, cmd, external = update (CategorySaved (Error "In der Gruppe Alltag gibt es schon eine Kategorie Drogerie.")) saving
            Expect.isEmpty cmd "kein Neuladen"
            Expect.equal external NoOp "kein Toast"
            Expect.equal failed.Groups loaded.Groups "Liste unverändert"
            Expect.equal failed.Form.Value.Error (Some "In der Gruppe Alltag gibt es schon eine Kategorie Drogerie.") "Meldung im Formular"
            Expect.isFalse failed.Form.Value.IsSaving "wieder bedienbar"
            Expect.isNone (model' (update (NameChanged "Bad") failed)).Form.Value.Error "Eingabe nimmt die Meldung weg"
            let groupFailed = run loaded [ RenameGroup kinder; NameChanged "Alltag"; Save; GroupSaved ("Alltag", Error "Eine Gruppe Alltag gibt es schon.") ]
            Expect.equal groupFailed.Form.Value.Error (Some "Eine Gruppe Alltag gibt es schon.") "auch im Gruppen-Formular"
            Expect.equal groupFailed.Groups loaded.Groups "Liste unverändert"

        testCase "Neue Gruppe im Auswahlfeld öffnet das Gruppen-Formular und kehrt mit der neuen Gruppe zurück" <| fun () ->
            let inGroupForm = run loaded [ NewCategory; NameChanged "Haustier"; GroupChosen NewGroupChoice ]
            match inGroupForm.Form with
            | Some { Editor = EditingGroup { GroupId = None; ReturnTo = Some { Name = "Haustier" } } } -> ()
            | other -> failtestf "Gruppen-Formular erwartet, bekam %A" other
            let back, cmd, external = update (GroupSaved ("Tiere", Ok tiere)) (run inGroupForm [ NameChanged "Tiere"; Save ])
            Expect.equal back.Form.Value.Editor (EditingCategory { CategoryId = None; Name = "Haustier"; GroupId = Some tiere; Active = true; WasActive = true }) "zurück mit neuer Gruppe"
            Expect.isTrue (match back.Groups with Success gs -> gs |> List.exists (fun g -> g.Id = tiere && g.Name = "Tiere") | _ -> false) "die Gruppe steht im Auswahlfeld"
            Expect.isNonEmpty cmd "die Liste lädt still neu"
            Expect.equal external (ShowToast ("Gruppe Tiere angelegt", ToastSuccess)) "Rückmeldung"
            let cancelled = run inGroupForm [ CloseForm ]
            Expect.equal cancelled.Form.Value.Editor (EditingCategory { CategoryId = None; Name = "Haustier"; GroupId = Some alltag; Active = true; WasActive = true }) "Abbrechen kehrt unverändert zurück"

        testCase "gespeichert: das Formular schließt, die Liste lädt neu, eine späte Antwort ohne Formular wird ein Toast" <| fun () ->
            let saved, cmd, external = update (CategorySaved (Ok drogerie)) (run loaded [ EditCategory drogerie; NameChanged "Bad"; Save ])
            Expect.isNone saved.Form "zu"
            Expect.isNonEmpty cmd "Neuladen"
            Expect.equal external (ShowToast ("Gespeichert", ToastSuccess)) "Rückmeldung"
            let created = run loaded [ NewCategory; NameChanged " Haustier "; Save ]
            let _, _, toast = update (CategorySaved (Ok drogerie)) created
            Expect.equal toast (ShowToast ("Haustier angelegt", ToastSuccess)) "Anlegen nennt den Namen"
            // Schließen ist während des Speicherns gesperrt; ohne Formular ist man nur nach "Zurück".
            let closed = run loaded [ NewCategory; NameChanged "Haustier"; Save; Leave ]
            let _, _, late = update (CategorySaved (Error "YNAB hat abgelehnt")) closed
            Expect.equal late (ShowToast ("Nicht gespeichert: YNAB hat abgelehnt", ToastError)) "Fehler ohne Formular"

        testCase "eine überholte Antwort beim Laden ersetzt nichts, ein Fehler beim stillen Neuladen lässt die Liste" <| fun () ->
            let entered = model' (update Entered loaded)
            let stale = model' (update (Loaded (entered.Generation - 1, Ok [])) entered)
            Expect.equal stale.Groups Loading "überholt"
            let reloadFailed, _, warn = update (Reloaded (loaded.Generation, Error "weg")) loaded
            Expect.equal reloadFailed.Groups loaded.Groups "Liste bleibt"
            Expect.equal warn (ShowToast ("Liste nicht neu geladen: weg", ToastWarning)) "Hinweis"

        testCase "ohne Gruppe ist Speichern gesperrt" <| fun () ->
            let empty = run { init () with Groups = Success [] } [ NewCategory; NameChanged "Haustier" ]
            Expect.isFalse (canSave empty.Form.Value) "keine Gruppe"
            let _, cmd, _ = update Save empty
            Expect.isEmpty cmd "nichts gesendet"

        testCase "Kategorien verwalten im Menü der Budget-Seite führt zur Seite Kategorien" <| fun () ->
            let budget = { Components.Budget.State.init () with MenuOpen = true }
            let after, _, external = Components.Budget.State.update Components.Budget.Types.OpenCategoryAdmin budget
            Expect.isFalse after.MenuOpen "Menü zu"
            Expect.equal external Components.Budget.Types.ShowCategoryAdmin "Sprung"
            Expect.equal (Routing.parseUrl [ "budget"; "categories" ]) BudgetCategories "Route"
            Expect.equal (Routing.toUrlSegments BudgetCategories) [ "budget"; "categories" ] "Rückweg"
    
        // ---------------- budgeting-sn2sn: Deaktivieren und Reihenfolge ----------------

        testCase "Deaktivieren im Formular: zuerst BB, dann zu, die Kategorie wandert in Deaktiviert, mit Hinweis auf YNAB" <| fun () ->
            let toggled = run loaded [ EditCategory drogerie; ActiveToggled false ]
            let saving, cmd, _ = update Save toggled
            Expect.isNonEmpty cmd "sendet"
            Expect.isTrue saving.Form.Value.IsSaving "läuft"
            let done', reload, toast = update (ActiveSaved (Ok ())) saving
            Expect.isNone done'.Form "zu"
            Expect.isNonEmpty reload "lädt still neu"
            Expect.equal toast (ShowToast ("Drogerie deaktiviert. In YNAB noch sichtbar, dort ausblenden.", ToastSuccess)) "Hinweis"
            match done'.Groups with
            | Success gs ->
                Expect.isEmpty (activeCategories (gs |> List.find (fun g -> g.Id = alltag))) "nicht mehr in der Karte"
                Expect.equal (inactiveCategories gs |> List.map (fun (g, c) -> g.Name, c.Name)) [ "Alltag", "Drogerie" ] "im Abschnitt"
            | other -> failtestf "Liste erwartet, bekam %A" other

        testCase "Deaktivieren und Umbenennen: erst BB, dann YNAB, am Ende der Hinweis auf YNAB" <| fun () ->
            let saving = run loaded [ EditCategory drogerie; ActiveToggled false; NameChanged "Bad"; Save ]
            let between, cmd, external = update (ActiveSaved (Ok ())) saving
            Expect.isNonEmpty cmd "jetzt Name nach YNAB"
            Expect.equal external NoOp "noch kein Toast"
            Expect.isTrue between.Form.Value.IsSaving "läuft weiter"
            let _, _, toast = update (CategorySaved (Ok drogerie)) between
            Expect.equal toast (ShowToast ("Bad deaktiviert. In YNAB noch sichtbar, dort ausblenden.", ToastSuccess)) "Hinweis"

        testCase "abgelehntes Deaktivieren: die Meldung mit Betrag bleibt im Formular, nichts ändert sich" <| fun () ->
            let saving = run loaded [ EditCategory drogerie; ActiveToggled false; Save ]
            let message = "Drogerie hat noch 24,50 €. Erst verschieben, dann deaktivieren."
            let failed, cmd, external = update (ActiveSaved (Error message)) saving
            Expect.isEmpty cmd "kein weiterer Aufruf"
            Expect.equal external NoOp "kein Toast"
            Expect.equal failed.Form.Value.Error (Some message) "im Formular"
            Expect.isFalse failed.Form.Value.IsSaving "wieder bedienbar"
            Expect.equal failed.Groups loaded.Groups "Liste unverändert"

        testCase "eine neue Kategorie hat keinen Schalter Aktiv" <| fun () ->
            let created = run loaded [ NewCategory; ActiveToggled false ]
            match created.Form.Value.Editor with
            | EditingCategory editor -> Expect.isTrue editor.Active "bleibt aktiv"
            | other -> failtestf "Kategorie-Formular erwartet, bekam %A" other

        testCase "Pfeile: am Rand nichts, sonst neue Reihenfolge sofort und an BB, deaktivierte bleiben dahinter" <| fun () ->
            let a, b, c, off = Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()
            let start =
                { init () with
                    Groups =
                        Success
                            [ { Id = alltag
                                Name = "Alltag"
                                Categories =
                                  [ { Id = a; Name = "A"; Active = true }
                                    { Id = off; Name = "Aus"; Active = false }
                                    { Id = b; Name = "B"; Active = true }
                                    { Id = c; Name = "C"; Active = true } ] } ] }
            let order (m: Model) = match m.Groups with Success [ g ] -> g.Categories |> List.map (fun x -> x.Id) | _ -> []
            let top, topCmd, _ = update (MoveCategory (a, StepUp)) start
            Expect.isEmpty topCmd "oben hoch sendet nichts"
            Expect.equal top start "oben hoch ändert nichts"
            let bottom, bottomCmd, _ = update (MoveCategory (c, StepDown)) start
            Expect.isEmpty bottomCmd "unten runter sendet nichts"
            Expect.equal bottom start "unten runter ändert nichts"
            let moved, cmd, _ = update (MoveCategory (b, StepUp)) start
            Expect.isNonEmpty cmd "an BB"
            Expect.equal (order moved) [ b; a; c; off ] "B vor A, die deaktivierte am Ende"
            Expect.equal (movedOrder StepDown a (match start.Groups with Success [ g ] -> g | _ -> failwith "Gruppe")) (Some [ b; a; c; off ]) "Auftrag"
            let failed, reload, toast = update (OrderSaved (alltag, Error "weg")) moved
            Expect.isNonEmpty reload "lädt den Stand neu"
            Expect.equal toast (ShowToast ("Reihenfolge nicht gespeichert: weg", ToastError)) "Meldung"
            Expect.equal (order failed) (order moved) "bis zum Neuladen bleibt die Liste"

        testCase "am PC verschiebt Alt mit Pfeil hoch oder runter, ohne Alt nicht" <| fun () ->
            Expect.equal (moveKey "ArrowUp" true) (Some StepUp) "hoch"
            Expect.equal (moveKey "ArrowDown" true) (Some StepDown) "runter"
            Expect.isNone (moveKey "ArrowUp" false) "ohne Alt"
            Expect.isNone (moveKey "Enter" true) "andere Taste"

        testCase "Aktivieren im Abschnitt Deaktiviert: sofort zurück in der Karte, an BB, Rückmeldung wie im Prototyp" <| fun () ->
            let withOff =
                { init () with Groups = Success [ { Id = alltag; Name = "Alltag"; Categories = [ { Id = drogerie; Name = "Drogerie"; Active = false } ] } ] }
            let opened = model' (update ToggleInactive withOff)
            Expect.isTrue opened.InactiveOpen "aufgeklappt"
            let active, cmd, _ = update (Reactivate drogerie) opened
            Expect.isNonEmpty cmd "an BB"
            Expect.isEmpty (match active.Groups with Success gs -> inactiveCategories gs | _ -> []) "nicht mehr deaktiviert"
            let _, reload, toast = update (Reactivated ("Drogerie", Ok ())) active
            Expect.isNonEmpty reload "lädt still neu"
            Expect.equal toast (ShowToast ("Drogerie ist wieder aktiv", ToastSuccess)) "Rückmeldung"
            let _, _, failed = update (Reactivated ("Drogerie", Error "weg")) active
            Expect.equal failed (ShowToast ("Drogerie nicht aktiviert: weg", ToastError)) "Fehler"

        // Bug (Review 2026-11-08): "Aktiv" und Name geändert, Formular geschlossen, solange das Deaktivieren lief:
        // `ActiveSaved` fiel in den fremden Zweig, Name und Gruppe gingen nie hinaus, ohne Toast und Neuladen.
        testCase "Schließen ist gesperrt, solange gespeichert wird; nach dem Deaktivieren geht der Name hinaus" <| fun () ->
            let saving = run loaded [ EditCategory drogerie; ActiveToggled false; NameChanged "Bad"; Save ]
            let closed = run saving [ CloseForm ]
            Expect.equal closed.Form saving.Form "Formular bleibt offen und gesperrt"
            let _, cmd, _ = update (ActiveSaved (Ok ())) closed
            Expect.isNonEmpty cmd "Name nach YNAB"

        // Bug (Review 2026-11-08): eine Antwort auf Deaktivieren ohne eigenes Formular lud die Liste nicht neu.
        testCase "eine späte Antwort auf Deaktivieren ohne Formular lädt die Liste neu" <| fun () ->
            let left = run loaded [ EditCategory drogerie; ActiveToggled false; Save; Leave ]
            let after, cmd, _ = update (ActiveSaved (Ok ())) left
            Expect.isNonEmpty cmd "Neuladen"
            Expect.equal after.Generation (left.Generation + 1) "neue Generation"

        // Bug (Review 2026-11-08): ein Neuladen, das vor dem Pfeil oder dem Aktivieren lief, stellte den alten Stand
        // wieder her, weil `MoveCategory` und `Reactivate` die Generation nicht hochzählten.
        testCase "ein Neuladen von vor dem Pfeil oder dem Aktivieren ersetzt die neue Liste nicht" <| fun () ->
            let a, b = Guid.NewGuid(), Guid.NewGuid()
            let before =
                [ { Id = alltag; Name = "Alltag"; Categories = [ { Id = a; Name = "A"; Active = true }; { Id = b; Name = "B"; Active = false } ] } ]
            // Generation 1: ein stilles Neuladen läuft noch.
            let start = { init () with Groups = Success before; Generation = 1 }
            let ids (m: Model) = match m.Groups with Success [ g ] -> g.Categories |> List.map (fun c -> c.Id, c.Active) | _ -> []
            let reactivated = model' (update (Reactivate b) start)
            let afterReactivate = model' (update (Reloaded (1, Ok before)) reactivated)
            Expect.equal (ids afterReactivate) [ a, true; b, true ] "B bleibt aktiv"
            let moved = model' (update (MoveCategory (b, StepUp)) afterReactivate)
            Expect.equal (ids moved) [ b, true; a, true ] "B vor A"
            let afterMove = model' (update (Reloaded (moved.Generation - 1, Ok before)) moved)
            Expect.equal (ids afterMove) [ b, true; a, true ] "die neue Reihenfolge bleibt"

        // Bug (Review 2026-11-08): schnelle Pfeile schickten je einen Aufruf, die Antworten konnten sich überholen.
        // Doktrin Punkt 20: alle Folgen aus Pfeilen, Antworten und einem Neuladen von vorher gegen ein eigenes Orakel.
        testCase "Pfeile in einer Gruppe: ein Aufruf zur Zeit, danach die zuletzt gewünschte Reihenfolge, in jeder Reihenfolge" <| fun () ->
            let a, b, c = Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()
            let before = [ { Id = alltag; Name = "Alltag"; Categories = [ for id, n in [ a, "A"; b, "B"; c, "C" ] -> { Id = id; Name = n; Active = true } ] } ]
            // Generation 1: ein stilles Neuladen von vor den Pfeilen läuft noch.
            let start = { init () with Groups = Success before; Generation = 1 }
            let ids (m: Model) = match m.Groups with Success [ g ] -> g.Categories |> List.map (fun x -> x.Id) | _ -> []
            let alphabet =
                [ MoveCategory (b, StepUp); MoveCategory (b, StepDown); MoveCategory (a, StepUp)
                  OrderSaved (alltag, Ok ()); OrderSaved (alltag, Error "weg"); Reloaded (1, Ok before) ]
            for msgs in sequences alphabet 4 do
                // Orakel ohne den getesteten Code: die Liste selbst tauschen, Aufruf und Warteschlange mitzählen.
                let mutable order, running, waiting, moved = [ a; b; c ], false, None, false
                let mutable model = start
                for msg in msgs do
                    let mutable expectCmd = false
                    match msg with
                    | MoveCategory (id, step) ->
                        let i = List.findIndex ((=) id) order
                        let j = if step = StepUp then i - 1 else i + 1
                        if j >= 0 && j < order.Length then
                            order <- order |> List.permute (fun k -> if k = i then j elif k = j then i else k)
                            moved <- true
                            if running then waiting <- Some order
                            else
                                running <- true
                                expectCmd <- true
                    | OrderSaved (_, Ok ()) ->
                        match waiting with
                        | Some _ ->
                            waiting <- None
                            expectCmd <- true
                        | None -> running <- false
                    | OrderSaved (_, Error _) ->
                        running <- false
                        waiting <- None
                        expectCmd <- true // Neuladen
                    | Reloaded _ -> if not moved then order <- [ a; b; c ]
                    | _ -> ()
                    let next, cmd, _ = update msg model
                    model <- next
                    let way () = $"{msgs}, bei {msg}"
                    expectSame (not cmd.IsEmpty) expectCmd (fun () -> $"Aufruf: {way ()}")
                    expectSame (ids model) order (fun () -> $"Liste: {way ()}")
                    expectSame (Map.tryFind alltag model.OrderCalls) (if running then Some { Next = waiting } else None) (fun () -> $"Warteschlange: {way ()}")

        testCase "die Pfeile der ersten Zeile nach oben und der letzten nach unten haben keine Wirkung" <| fun () ->
            let a, b, off = Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()
            let group =
                { Id = alltag; Name = "Alltag"; Categories = [ { Id = a; Name = "A"; Active = true }; { Id = b; Name = "B"; Active = true }; { Id = off; Name = "Aus"; Active = false } ] }
            Expect.equal [ canMove StepUp a group; canMove StepDown a group ] [ false; true ] "erste Zeile"
            Expect.equal [ canMove StepUp b group; canMove StepDown b group ] [ true; false ] "letzte Zeile, die deaktivierte zählt nicht"
    ]
