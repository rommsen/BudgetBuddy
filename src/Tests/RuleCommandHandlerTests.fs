module Tests.RuleCommandHandlerTests

// Regeln speichern, wiederherstellen und sortieren (categorization-msep5, categorization-n3sxw): Befehle gegen
// In-Memory-SQLite mit gefälschtem YNAB-Port, der nie YNAB erreicht und seine Aufrufe zählt.
// Die In-Memory-DB ist geteilt: jeder Test sieht nur seine eigenen Regeln (eindeutige Ids
// und Namen) und schreibt keine kaputte Zeile, die `getAllRules` anderer Tests stören würde.

open System

// CRITICAL: Testmodus setzen, BEVOR das Persistence-Modul initialisiert wird.
do Environment.SetEnvironmentVariable("USE_MEMORY_DB", "true")

open Expecto
open Thoth.Json.Net
open Shared.Domain
open Server.RulesEngine
open Server.Rules.CommandHandlers
open Components.Rules.Types
open Components.Rules.State

let private now = DateTime(2026, 10, 30, 12, 0, 0, DateTimeKind.Utc)

let private run (work: Async<'T>) : 'T = Async.RunSynchronously work

/// Ein gefälschter YNAB-Port mit fester Antwort; der Zähler belegt, ob YNAB gefragt wurde.
let private portAnswering (answer: Result<YnabCategory list, string>) =
    let calls = ref 0
    let port = { FetchCategories = fun () -> async { calls.Value <- calls.Value + 1; return answer } }
    port, calls

let private category (id: YnabCategoryId) (name: string) : YnabCategory =
    { Id = id; Name = name; GroupName = "Test"; Available = None; Hidden = false }

let private savedRule (name: string) (priority: int) (payeeOverride: string option) : Rule =
    { Id = RuleId (Guid.NewGuid())
      Name = name
      Pattern = "REWE"
      PatternType = Contains
      TargetField = Combined
      CategoryId = YnabCategoryId (Guid.NewGuid())
      CategoryName = "Lebensmittel"
      PayeeOverride = payeeOverride
      Priority = priority
      Enabled = true
      CreatedAt = now
      UpdatedAt = now }

let private insert (rule: Rule) : Rule =
    Persistence.initializeDatabase ()
    Persistence.Rules.insertRule rule |> run
    rule

let private stored (rule: Rule) : Rule =
    Persistence.Rules.getRuleById rule.Id |> run |> Option.get

let private unchangedApart (id: RuleId) : RuleChange =
    { Id = id; Name = None; Pattern = None; PatternType = None; TargetField = None
      CategoryId = None; PayeeOverride = PayeeOverrideChange.Keep; Priority = None; Enabled = None }

let private newRule (name: string) (categoryId: YnabCategoryId) : NewRule =
    { Name = name; Pattern = "REWE"; PatternType = Contains; TargetField = Combined
      CategoryId = categoryId; PayeeOverride = None; Enabled = true }

let private rulesNamed (prefix: string) : Rule list =
    Persistence.Rules.getAllRules () |> run |> List.filter (fun rule -> rule.Name.StartsWith prefix)

let private uniquePrefix () = $"Test {Guid.NewGuid()}"

let private neverCalledPort () = portAnswering (Error "YNAB darf nicht gefragt werden")

[<Tests>]
let payeeOverrideTests =
    testSequenced <| testList "Regel ändern: PayeeOverride" [
        // categorization-n3sxw: das Feld Payee-Override fällt aus der Oberfläche. Wer einen trägt, behält ihn.
        testCase "eine Regel mit Payee-Override behält ihn, wenn sie im Panel geändert und gespeichert wird" <| fun () ->
            let rule = insert (savedRule (uniquePrefix ()) 1 (Some "REWE Markt"))
            let draft = { RuleDraft.fromRule rule with Name = rule.Name + " neu"; Pattern = "rewe markt"; Enabled = false }
            let port, _ = neverCalledPort ()

            let result = changeRule port now (buildRuleChange rule draft rule.CategoryId) |> run

            Expect.isOk result "gespeichert"
            Expect.equal (stored rule).PayeeOverride (Some "REWE Markt") "Override bleibt"
            Expect.equal (stored rule).Pattern "rewe markt" "Muster geändert"
            Expect.isFalse (stored rule).Enabled "ausgeschaltet"

        testCase "eine Änderung mit gesetztem Override ersetzt ihn, ein Entfernen löscht ihn" <| fun () ->
            let rule = insert (savedRule (uniquePrefix ()) 1 (Some "alt"))
            let port, _ = neverCalledPort ()

            changeRule port now { unchangedApart rule.Id with PayeeOverride = PayeeOverrideChange.Set "neu" } |> run |> ignore
            Expect.equal (stored rule).PayeeOverride (Some "neu") "neuer Override"
            changeRule port now { unchangedApart rule.Id with PayeeOverride = PayeeOverrideChange.Remove } |> run |> ignore
            Expect.isNone (stored rule).PayeeOverride "entfernt"
    ]

[<Tests>]
let categoryChangeTests =
    testSequenced <| testList "Regel ändern: Kategorie" [
        // Bug: bei einem YNAB-Fehler stand die neue Kategorie-Id neben dem alten Namen.
        testCase "YNAB-Fehler beim Kategoriewechsel: Fehler, die Regel ist unverändert" <| fun () ->
            let rule = insert (savedRule (uniquePrefix ()) 1 None)
            let before = stored rule
            let port, _ = portAnswering (Error "Netzwerkfehler: weg")
            let change =
                { unchangedApart rule.Id with CategoryId = Some (YnabCategoryId (Guid.NewGuid())) }

            let result = changeRule port now change |> run

            Expect.equal result (Error (RulesError.CategoryLookupFailed "Netzwerkfehler: weg")) "Grund genannt"
            Expect.equal (stored rule) before "unverändert"

        testCase "die neue Kategorie fehlt in YNAB: Fehler, die Regel ist unverändert" <| fun () ->
            let rule = insert (savedRule (uniquePrefix ()) 1 None)
            let before = stored rule
            let port, _ = portAnswering (Ok [])
            let missing = Guid.NewGuid()
            let change = { unchangedApart rule.Id with CategoryId = Some (YnabCategoryId missing) }

            let result = changeRule port now change |> run

            Expect.equal result (Error (RulesError.CategoryNotFound missing)) "Kategorie genannt"
            Expect.equal (stored rule) before "unverändert"

        testCase "ein gelungener Kategoriewechsel speichert Id und neuen Namen" <| fun () ->
            let rule = insert (savedRule (uniquePrefix ()) 1 None)
            let target = YnabCategoryId (Guid.NewGuid())
            let port, _ = portAnswering (Ok [ category target "Drogerie" ])

            changeRule port now { unchangedApart rule.Id with CategoryId = Some target } |> run |> ignore

            Expect.equal (stored rule).CategoryId target "neue Id"
            Expect.equal (stored rule).CategoryName "Drogerie" "neuer Name"

        // Der Dialog schickt die Kategorie immer mit, auch unverändert.
        testCase "YNAB nicht erreichbar, nur der Name ändert sich: gespeichert, YNAB nicht gefragt" <| fun () ->
            let rule = insert (savedRule (uniquePrefix ()) 1 None)
            let renamed = rule.Name + " neu"
            let draft = { RuleDraft.fromRule rule with Name = renamed }
            let port, calls = neverCalledPort ()

            let result = changeRule port now (buildRuleChange rule draft rule.CategoryId) |> run

            Expect.isOk result "gespeichert"
            Expect.equal (stored rule).Name renamed "neuer Name"
            Expect.equal (stored rule).CategoryName rule.CategoryName "Kategoriename bleibt"
            Expect.equal calls.Value 0 "YNAB nicht gefragt"

        testCase "ungültige Eingaben kommen als ValidationFailed" <| fun () ->
            let rule = insert (savedRule (uniquePrefix ()) 1 None)
            let port, _ = neverCalledPort ()

            let result = changeRule port now { unchangedApart rule.Id with Name = Some "" } |> run

            Expect.equal result (Error (RulesError.ValidationFailed [ "Regelname ist erforderlich" ])) "kein InvalidPattern"
    ]

[<Tests>]
let createTests =
    testSequenced <| testList "Regel anlegen" [
        // Bug: alle drei Fälle speicherten den Platzhalter "Unknown".
        testCase "eine in YNAB unbekannte Kategorie: Fehler, keine Zeile" <| fun () ->
            Persistence.initializeDatabase ()
            let name = uniquePrefix ()
            let missing = Guid.NewGuid()
            let port, _ = portAnswering (Ok [])

            let result = createRule port now (newRule name (YnabCategoryId missing)) |> run

            Expect.equal result (Error (RulesError.CategoryNotFound missing)) "Kategorie genannt"
            Expect.isEmpty (rulesNamed name) "keine Zeile"

        testCase "YNAB-Fehler: Fehler mit Grund, keine Zeile" <| fun () ->
            Persistence.initializeDatabase ()
            let name = uniquePrefix ()
            let port, _ = portAnswering (Error "Rate-Limit")

            let result = createRule port now (newRule name (YnabCategoryId (Guid.NewGuid()))) |> run

            Expect.equal result (Error (RulesError.CategoryLookupFailed "Rate-Limit")) "Grund genannt"
            Expect.isEmpty (rulesNamed name) "keine Zeile"

        testCase "fehlende Einstellungen: Fehler mit Grund, keine Zeile, YNAB nicht gefragt" <| fun () ->
            Persistence.initializeDatabase ()
            let name = uniquePrefix ()
            let mutable asked = false
            let port =
                ynabPort
                    (fun _ -> async { return None })
                    (fun _ _ -> async { asked <- true; return Ok [] })

            let result = createRule port now (newRule name (YnabCategoryId (Guid.NewGuid()))) |> run

            match result with
            | Error (RulesError.CategoryLookupFailed reason) ->
                Expect.stringContains reason "nicht eingerichtet" "Grund genannt"
            | other -> failtest $"Erwartet CategoryLookupFailed, war {other}"
            Expect.isFalse asked "YNAB nicht gefragt"
            Expect.isEmpty (rulesNamed name) "keine Zeile"

        testCase "ein YNAB-Fehler aus dem Client kommt als deutscher Grund an" <| fun () ->
            let port =
                ynabPort
                    (fun _ -> async { return Some "wert" })
                    (fun _ _ -> async { return Error (YnabError.NetworkError "weg") })

            let result = lookupCategoryName port (YnabCategoryId (Guid.NewGuid())) |> run

            Expect.equal result (Error (RulesError.CategoryLookupFailed "Netzwerkfehler: weg")) "Grund"

        // Bug categorization-m47g8: seit categorization-msep5 vergab der Server `MAX(priority) + 1`, jede neue Regel
        // stand vorn und stach alle älteren aus. Entschieden (Roman, 2026-11-14): eine neue Regel steht am Ende.
        testCase "eine neue Regel bekommt eine kleinere Priorität als jede vorhandene und steht am Ende" <| fun () ->
            Persistence.initializeDatabase ()
            let prefix = uniquePrefix ()
            for priority in [ 3; 2; 1 ] do
                insert (savedRule $"{prefix} {priority}" priority None) |> ignore
            let lowest =
                Persistence.Rules.getAllRules () |> run
                |> List.fold (fun bottom rule -> min bottom rule.Priority) Int32.MaxValue
            let target = YnabCategoryId (Guid.NewGuid())
            let port, _ = portAnswering (Ok [ category target "Drogerie" ])

            let created =
                createRule port now (newRule (uniquePrefix ()) target) |> run
                |> Result.defaultWith (fun error -> failtest $"Anlegen scheiterte: {error}")

            Expect.isLessThan created.Priority lowest "kleiner als jede vorhandene"
            Expect.equal (stored created).Priority created.Priority "so gespeichert"
            Expect.equal created.CategoryName "Drogerie" "Name aus YNAB"
            Expect.equal (Persistence.Rules.getAllRules () |> run |> List.last).Id created.Id "steht am Ende"

        // categorization-m47g8: die Engine prüft von oben nach unten, eine neue Regel kommt nach allen älteren dran.
        testCase "passt eine ältere Regel auf denselben Payee, gewinnt sie gegen die neue" <| fun () ->
            Persistence.initializeDatabase ()
            let payee = $"Laden {Guid.NewGuid():N}"
            let older = insert { savedRule (uniquePrefix ()) 1 None with Pattern = payee }
            let target = YnabCategoryId (Guid.NewGuid())
            let port, _ = portAnswering (Ok [ category target "Drogerie" ])

            let created =
                createRule port now { newRule (uniquePrefix ()) target with Pattern = payee } |> run
                |> Result.defaultWith (fun error -> failtest $"Anlegen scheiterte: {error}")

            let rules = Persistence.Rules.getAllRules () |> run
            Expect.equal (ruleCategoryFor rules payee "") (Some older.CategoryId) "ruleCategoryFor: die ältere"
            let compiled = rules |> compileRules |> Result.defaultWith (fun _ -> failtest "Regeln übersetzen")
            let transaction =
                { Id = TransactionId "neu"; BookingDate = now; Amount = { Amount = -1m; Currency = "EUR" }
                  Payee = Some payee; Memo = ""; Reference = "neu"; RawData = "" }
            Expect.equal (classify compiled transaction |> Option.map (fun (rule, _) -> rule.Id)) (Some older.Id) "Import: die ältere"
            Expect.notEqual created.CategoryId older.CategoryId "verschiedene Kategorien"

        // Die erste Regel überhaupt bekommt eine ganze Zahl, ohne vorhandene Regel kein Fehler.
        testCase "ohne vorhandene Regel ist die Priorität 0, sonst eins unter der niedrigsten" <| fun () ->
            Expect.equal (Persistence.Rules.priorityBelow None) 0 "leere Tabelle"
            Expect.equal (Persistence.Rules.priorityBelow (Some 1)) 0 "unter 1"
            Expect.equal (Persistence.Rules.priorityBelow (Some -4)) -5 "auch negativ"

        // categorization-n3sxw: der Schalter „Regel aktiv“ gibt es auch beim Anlegen.
        testCase "eine neu angelegte Regel mit ausgeschaltetem Schalter ist inaktiv" <| fun () ->
            Persistence.initializeDatabase ()
            let target = YnabCategoryId (Guid.NewGuid())
            let port, _ = portAnswering (Ok [ category target "Drogerie" ])

            let created =
                createRule port now { newRule (uniquePrefix ()) target with Enabled = false } |> run
                |> Result.defaultWith (fun error -> failtest $"Anlegen scheiterte: {error}")

            Expect.isFalse created.Enabled "Antwort"
            Expect.isFalse (stored created).Enabled "gespeichert"

        testCase "ein leerer Payee-Override wird abgelehnt" <| fun () ->
            let port, calls = neverCalledPort ()
            let request =
                { newRule (uniquePrefix ()) (YnabCategoryId (Guid.NewGuid())) with PayeeOverride = Some "  " }

            let result = createRule port now request |> run

            Expect.equal result (Error (RulesError.ValidationFailed [ "Payee-Override darf nicht leer sein" ])) "Fehler"
            Expect.equal calls.Value 0 "YNAB nicht gefragt"

        testCase "ein nicht übersetzbares Muster wird vor YNAB abgelehnt" <| fun () ->
            let port, calls = neverCalledPort ()
            let request =
                { newRule (uniquePrefix ()) (YnabCategoryId (Guid.NewGuid())) with
                    Pattern = "("
                    PatternType = Regex }

            match createRule port now request |> run with
            | Error (RulesError.InvalidPattern ("(", _)) -> ()
            | other -> failtest $"Erwartet InvalidPattern, war {other}"
            Expect.equal calls.Value 0 "YNAB nicht gefragt"
    ]

[<Tests>]
let tieBreakTests =
    testSequenced <| testList "Gleiche Priorität" [
        let classifyFirst (rules: Rule list) (payee: string) =
            let compiled =
                rules |> compileRules |> Result.defaultWith (fun _ -> failtest "Regeln übersetzen")
            let transaction =
                { Id = TransactionId "tie"; BookingDate = now; Amount = { Amount = -1m; Currency = "EUR" }
                  Payee = Some payee; Memo = ""; Reference = "tie"; RawData = "" }
            classify compiled transaction |> Option.map fst

        // Bug: die Reihenfolge bei gleicher Priorität war offen. Beide Id-Reihenfolgen, damit
        // nicht zufällig die Id entscheidet.
        for label, firstId, secondId in
            [ "kleinere Id zuerst", Guid("00000000-0000-0000-0000-000000000001"), Guid("ffffffff-ffff-ffff-ffff-ffffffffffff")
              "größere Id zuerst", Guid("ffffffff-ffff-ffff-ffff-fffffffffffe"), Guid("00000000-0000-0000-0000-000000000002") ] do
            testCase $"die zuerst gespeicherte Regel steht vorn und gewinnt ({label})" <| fun () ->
                let prefix = uniquePrefix ()
                let unique = Guid.NewGuid()
                let withUniqueId (id: Guid) =
                    // Eindeutig je Lauf: die festen Anfangs- und Endstücke legen die Sortierung fest.
                    let bytes = id.ToByteArray()
                    let suffix = unique.ToByteArray()
                    Array.blit suffix 8 bytes 8 4
                    Guid(bytes)
                let first = insert { savedRule $"{prefix} A" 5 None with Id = RuleId (withUniqueId firstId) }
                let second = insert { savedRule $"{prefix} B" 5 None with Id = RuleId (withUniqueId secondId) }

                let rules = rulesNamed prefix

                Expect.equal (rules |> List.map (fun rule -> rule.Id)) [ first.Id; second.Id ] "Speicherreihenfolge"
                let winner = classifyFirst rules "REWE" |> Option.map (fun rule -> rule.Id)
                Expect.equal winner (Some first.Id) "die erste gewinnt"
    ]

[<Tests>]
let restoreTests =
    testSequenced <| testList "Gelöschte Regel wiederherstellen" [
        /// Drei Regeln ganz oben (höchste Prioritäten): die Stelle 1 ist dann die mittlere von ihnen.
        let topThree (prefix: string) =
            let highest =
                Persistence.Rules.getAllRules () |> run
                |> List.fold (fun top rule -> max top rule.Priority) 0
            [ insert (savedRule $"{prefix} A" (highest + 3) None)
              insert { savedRule $"{prefix} B" (highest + 2) (Some "Ein Payee") with
                         Enabled = false; PatternType = Exact; TargetField = Memo }
              insert (savedRule $"{prefix} C" (highest + 1) None) ]

        // categorization-n3sxw: „Rückgängig“ nach dem Löschen bringt die Regel mit allen Feldern an ihre Stelle.
        testCase "die gelöschte Regel an Stelle 1 steht danach wieder dort, mit Payee-Override und inaktiv" <| fun () ->
            Persistence.initializeDatabase ()
            let prefix = uniquePrefix ()
            let rules = topThree prefix
            let deleted = rules[1]
            let position =
                Persistence.Rules.getAllRules () |> run |> List.findIndex (fun rule -> rule.Id = deleted.Id)
            Persistence.Rules.deleteRule deleted.Id |> run

            let result = restoreRule { Rule = deleted; Position = position } |> run

            match result with
            | Ok all ->
                Expect.equal (all |> List.findIndex (fun rule -> rule.Id = deleted.Id)) position "Stelle in der Antwort"
            | Error error -> failtest $"Wiederherstellen scheiterte: {error}"
            Expect.equal (rulesNamed prefix |> List.map (fun rule -> rule.Name)) [ $"{prefix} A"; $"{prefix} B"; $"{prefix} C" ] "Reihenfolge"
            let back = stored deleted
            Expect.equal back.PayeeOverride (Some "Ein Payee") "Override"
            Expect.isFalse back.Enabled "inaktiv"
            Expect.equal (back.PatternType, back.TargetField, back.CategoryId, back.CategoryName)
                         (Exact, Memo, deleted.CategoryId, deleted.CategoryName) "übrige Felder"

        testCase "die Antwort ist die ganze Liste in Prüfreihenfolge" <| fun () ->
            Persistence.initializeDatabase ()
            let prefix = uniquePrefix ()
            let rules = topThree prefix
            Persistence.Rules.deleteRule rules[2].Id |> run

            let result = restoreRule { Rule = rules[2]; Position = 2 } |> run

            match result with
            | Ok all -> Expect.equal all (Persistence.Rules.getAllRules () |> run) "wie gespeichert"
            | Error error -> failtest $"Wiederherstellen scheiterte: {error}"

        testCase "eine Stelle hinter dem Ende hängt die Regel unten an" <| fun () ->
            Persistence.initializeDatabase ()
            let prefix = uniquePrefix ()
            let rules = topThree prefix
            Persistence.Rules.deleteRule rules[0].Id |> run

            match restoreRule { Rule = rules[0]; Position = Int32.MaxValue } |> run with
            | Ok all -> Expect.equal (List.last all).Id rules[0].Id "ganz unten"
            | Error error -> failtest $"Wiederherstellen scheiterte: {error}"

        testCase "eine ungültige Regel wird abgelehnt, keine Zeile" <| fun () ->
            let prefix = uniquePrefix ()
            let broken = { savedRule $"{prefix} kaputt" 1 None with Pattern = "("; PatternType = Regex }

            match restoreRule { Rule = broken; Position = 0 } |> run with
            | Error (RulesError.InvalidPattern ("(", _)) -> ()
            | other -> failtest $"Erwartet InvalidPattern, war {other}"
            Expect.isEmpty (rulesNamed prefix) "keine neue Zeile"

        testCase "eine negative Stelle und ein leerer Name kommen gesammelt als ValidationFailed" <| fun () ->
            let broken = { savedRule "" 1 None with Pattern = "REWE" }

            let result = restoreRule { Rule = broken; Position = -1 } |> run

            Expect.equal result (Error (RulesError.ValidationFailed [ "Regelname ist erforderlich"; "Die Stelle darf nicht negativ sein" ])) "Meldung"

        // Kein Zwischenstand: eine Regel, die es noch gibt, wird nicht doppelt angelegt und die Reihenfolge bleibt.
        testCase "eine Regel, die es noch gibt, wird nicht doppelt angelegt" <| fun () ->
            Persistence.initializeDatabase ()
            let prefix = uniquePrefix ()
            let rules = topThree prefix
            let before = Persistence.Rules.getAllRules () |> run

            match restoreRule { Rule = rules[2]; Position = 0 } |> run with
            | Error (RulesError.DatabaseError ("restore_rule", _)) -> ()
            | other -> failtest $"Erwartet DatabaseError, war {other}"
            Expect.equal (Persistence.Rules.getAllRules () |> run |> List.map (fun rule -> rule.Id, rule.Priority))
                         (before |> List.map (fun rule -> rule.Id, rule.Priority)) "unverändert"
    ]

[<Tests>]
let converterTests =
    let row (patternType: string) (targetField: string) : Persistence.Rules.RuleRow =
        { id = Guid.NewGuid().ToString()
          name = "Regel"
          pattern = "x"
          pattern_type = patternType
          target_field = targetField
          category_id = Guid.NewGuid().ToString()
          category_name = "Kategorie"
          payee_override = None
          priority = 1
          enabled = 1
          created_at = now.ToString "O"
          updated_at = now.ToString "O" }

    let failsNaming (value: string) (work: unit -> Rule) =
        let thrown = Expect.throwsC (work >> ignore) id
        Expect.stringContains thrown.Message value "Meldung nennt den Wert"

    testList "Regel aus der Datenbank lesen" [
        // Bug: unbekannte Werte wurden still zu Regex bzw. Combined, Buchungen falsch kategorisiert.
        testCase "ein unbekannter pattern_type lässt das Lesen scheitern" <| fun () ->
            failsNaming "Fuzzy" (fun () -> Persistence.Rules.rowToRule (row "Fuzzy" "Payee"))

        testCase "ein unbekanntes target_field lässt das Lesen scheitern" <| fun () ->
            failsNaming "Betrag" (fun () -> Persistence.Rules.rowToRule (row "Regex" "Betrag"))

        testCase "die bekannten Werte laufen durch" <| fun () ->
            for name, patternType in [ "Regex", Regex; "Contains", Contains; "Exact", Exact ] do
                Expect.equal (Persistence.Rules.rowToRule (row name "Payee")).PatternType patternType name
            for name, targetField in [ "Payee", Payee; "Memo", Memo; "Combined", Combined ] do
                Expect.equal (Persistence.Rules.rowToRule (row "Regex" name)).TargetField targetField name
    ]

[<Tests>]
let testRuleTests =
    testList "Regel testen" [
        // Bug: ein ungültiger Regex kam als "kein Treffer" zurück.
        testCase "ein ungültiger Regex ist InvalidPattern" <| fun () ->
            match Server.Api.rulesApi.testRule ("(", Regex, Payee, "(irgendwas") |> run with
            | Error (RulesError.InvalidPattern ("(", _)) -> ()
            | other -> failtest $"Erwartet InvalidPattern, war {other}"

        testCase "Treffer" <| fun () ->
            Expect.equal
                (Server.Api.rulesApi.testRule ("rewe", Contains, Payee, "REWE Markt") |> run)
                (Ok true)
                "trifft"

        testCase "kein Treffer" <| fun () ->
            Expect.equal
                (Server.Api.rulesApi.testRule ("edeka", Contains, Payee, "REWE Markt") |> run)
                (Ok false)
                "trifft nicht"
    ]

[<Tests>]
let ruleCategoryForEndpointTests =
    testSequenced <| testList "Endpoint ruleCategoryFor" [
        // ledger-hg5m5: der Endpoint liest die gespeicherten Regeln und gibt Payee und Memo in
        // dieser Reihenfolge an `RulesEngine.ruleCategoryFor` weiter. Gespeichert wird über die
        // Persistenz: `rulesApi.createRule` fragt YNAB nach dem Kategorienamen.
        testCase "eine gespeicherte Regel auf den Payee gibt ihre Kategorie" <| fun () ->
            Persistence.initializeDatabase ()
            let highest =
                Persistence.Rules.getAllRules () |> run
                |> List.fold (fun top rule -> max top rule.Priority) 0
            let marker = $"hg5m5-{Guid.NewGuid():N}"
            let target = YnabCategoryId (Guid.NewGuid())
            insert
                { savedRule (uniquePrefix ()) (highest + 1) None with
                    Pattern = marker
                    TargetField = Payee
                    CategoryId = target }
            |> ignore

            Expect.equal
                (Server.Api.rulesApi.ruleCategoryFor ($"Kiosk {marker}", "") |> run)
                (Some target)
                "Kategorie der gespeicherten Regel"
    ]

[<Tests>]
let errorMessageTests =
    testList "Meldungen zu RulesError" [
        let id = Guid("11111111-2222-3333-4444-555555555555")
        for error, expected in
            [ RulesError.RuleNotFound id, $"Regel nicht gefunden: {id}"
              RulesError.InvalidPattern ("(", "Klammer fehlt"), "Ungültiges Muster '(': Klammer fehlt"
              RulesError.CategoryNotFound id, $"Kategorie nicht gefunden: {id}"
              RulesError.CategoryLookupFailed "Rate-Limit", "Kategorie konnte nicht nachgeschlagen werden: Rate-Limit"
              RulesError.ValidationFailed [ "a"; "b" ], "a; b"
              RulesError.DatabaseError ("import_rules", "voll"), "Datenbankfehler bei import_rules: voll" ] do
            testCase expected <| fun () -> Expect.equal (rulesErrorToString error) expected "Meldung"
    ]
