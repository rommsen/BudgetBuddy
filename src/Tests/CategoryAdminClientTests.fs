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
    [ { Id = alltag; Name = "Alltag"; Categories = [ { Id = drogerie; Name = "Drogerie" } ] }
      { Id = kinder; Name = "Kinder"; Categories = [] } ]

let private loaded : Model = { init () with Groups = Success groups }

let private step (model: Model) (msg: Msg) = update msg model
let private model' (m, _, _) = m
let private run (start: Model) (msgs: Msg list) : Model = msgs |> List.fold (fun m msg -> model' (update msg m)) start

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
        testCase "Speichern ist gesperrt, solange der Aufruf läuft, in jeder Reihenfolge" <| fun () ->
            let forms =
                [ run loaded [ NewCategory; NameChanged "Haustier" ]
                  run loaded [ EditCategory drogerie; NameChanged "Bad" ]
                  run loaded [ RenameGroup kinder; NameChanged "Familie" ]
                  run loaded [ NewCategory; GroupChosen NewGroupChoice; NameChanged "Tiere" ] ]
            let alphabet = [ Save; NameChanged "X"; GroupChosen (string kinder); GroupChosen NewGroupChoice ]
            for start in forms do
                let saving, firstCmd, _ = update Save start
                Expect.isNonEmpty firstCmd "der erste Druck sendet"
                Expect.isTrue saving.Form.Value.Saving "läuft"
                for msgs in sequences alphabet 3 do
                    let final, sent =
                        msgs |> List.fold (fun (m, sent) msg -> let m2, cmd, _ = update msg m in m2, sent + (if cmd.IsEmpty then 0 else 1)) (saving, 0)
                    // Orakel: solange keine Antwort kam, entsteht kein zweiter Aufruf und das Formular bleibt, wie es gesendet wurde.
                    Expect.equal sent 0 $"kein zweiter Aufruf nach {msgs}"
                    Expect.equal final.Form saving.Form $"Formular unverändert nach {msgs}"

        testCase "ein Fehler des Servers bleibt im Formular, die Liste bleibt, eine Eingabe nimmt ihn weg" <| fun () ->
            let saving = run loaded [ NewCategory; NameChanged "Drogerie"; Save ]
            let failed, cmd, external = update (CategorySaved (Error "In der Gruppe Alltag gibt es schon eine Kategorie Drogerie.")) saving
            Expect.isEmpty cmd "kein Neuladen"
            Expect.equal external NoOp "kein Toast"
            Expect.equal failed.Groups loaded.Groups "Liste unverändert"
            Expect.equal failed.Form.Value.Error (Some "In der Gruppe Alltag gibt es schon eine Kategorie Drogerie.") "Meldung im Formular"
            Expect.isFalse failed.Form.Value.Saving "wieder bedienbar"
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
            Expect.equal back.Form.Value.Editor (EditingCategory { CategoryId = None; Name = "Haustier"; GroupId = Some tiere }) "zurück mit neuer Gruppe"
            Expect.isTrue (match back.Groups with Success gs -> gs |> List.exists (fun g -> g.Id = tiere && g.Name = "Tiere") | _ -> false) "die Gruppe steht im Auswahlfeld"
            Expect.isNonEmpty cmd "die Liste lädt still neu"
            Expect.equal external (ShowToast ("Gruppe Tiere angelegt", ToastSuccess)) "Rückmeldung"
            let cancelled = run inGroupForm [ CloseForm ]
            Expect.equal cancelled.Form.Value.Editor (EditingCategory { CategoryId = None; Name = "Haustier"; GroupId = Some alltag }) "Abbrechen kehrt unverändert zurück"

        testCase "gespeichert: das Formular schließt, die Liste lädt neu, eine späte Antwort ohne Formular wird ein Toast" <| fun () ->
            let saved, cmd, external = update (CategorySaved (Ok drogerie)) (run loaded [ EditCategory drogerie; NameChanged "Bad"; Save ])
            Expect.isNone saved.Form "zu"
            Expect.isNonEmpty cmd "Neuladen"
            Expect.equal external (ShowToast ("Gespeichert", ToastSuccess)) "Rückmeldung"
            let created = run loaded [ NewCategory; NameChanged " Haustier "; Save ]
            let _, _, toast = update (CategorySaved (Ok drogerie)) created
            Expect.equal toast (ShowToast ("Haustier angelegt", ToastSuccess)) "Anlegen nennt den Namen"
            let closed = run loaded [ NewCategory; NameChanged "Haustier"; Save; CloseForm ]
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
    ]
