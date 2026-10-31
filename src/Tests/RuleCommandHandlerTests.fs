module Tests.RuleCommandHandlerTests

// Regeln speichern, importieren und sortieren (categorization-msep5): Befehle gegen
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
      CategoryId = categoryId; PayeeOverride = None }

let private rulesNamed (prefix: string) : Rule list =
    Persistence.Rules.getAllRules () |> run |> List.filter (fun rule -> rule.Name.StartsWith prefix)

let private uniquePrefix () = $"Test {Guid.NewGuid()}"

let private neverCalledPort () = portAnswering (Error "YNAB darf nicht gefragt werden")

[<Tests>]
let payeeOverrideTests =
    testSequenced <| testList "Regel ändern: PayeeOverride" [
        // Bug: `None` hieß immer "behalten", ein gesetzter Override ließ sich nicht entfernen.
        testCase "ein im Dialog geleerter Override ist nach dem Speichern entfernt" <| fun () ->
            let rule = insert (savedRule (uniquePrefix ()) 1 (Some "REWE Markt"))
            let form = { RuleFormState.fromRule rule with PayeeOverride = "" }
            let port, _ = neverCalledPort ()

            let result = changeRule port now (buildRuleChange rule form rule.CategoryId) |> run

            Expect.isOk result "gespeichert"
            Expect.isNone (stored rule).PayeeOverride "Override entfernt"

        testCase "ein geänderter Override wird gesetzt" <| fun () ->
            let rule = insert (savedRule (uniquePrefix ()) 1 (Some "alt"))
            let form = { RuleFormState.fromRule rule with PayeeOverride = "neu" }
            let port, _ = neverCalledPort ()

            changeRule port now (buildRuleChange rule form rule.CategoryId) |> run |> ignore

            Expect.equal (stored rule).PayeeOverride (Some "neu") "neuer Override"

        // Der Server lehnt einen leeren Override ab: der Dialog schickt beim Anlegen gar keinen.
        testCase "beim Anlegen wird ein leeres Override-Feld zu keinem Override" <| fun () ->
            let form = { RuleFormState.empty with PayeeOverride = "  " }
            let request = buildNewRule form (YnabCategoryId (Guid.NewGuid()))
            Expect.isNone request.PayeeOverride "kein Override"

        testCase "Enabled allein umschalten lässt den Override unverändert" <| fun () ->
            let rule = insert (savedRule (uniquePrefix ()) 1 (Some "REWE Markt"))
            let port, _ = neverCalledPort ()

            let result = changeRule port now (toggleEnabledChange rule) |> run

            Expect.isOk result "gespeichert"
            Expect.isFalse (stored rule).Enabled "ausgeschaltet"
            Expect.equal (stored rule).PayeeOverride (Some "REWE Markt") "Override bleibt"
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
            let form = { RuleFormState.fromRule rule with Name = renamed }
            let port, calls = neverCalledPort ()

            let result = changeRule port now (buildRuleChange rule form rule.CategoryId) |> run

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

        // Bug: der Zuweisungs-Dialog schickte Priorität 1, die bei `ORDER BY priority DESC`
        // die niedrigste ist. Die Regel landete ganz unten.
        testCase "eine neue Regel bekommt die höchste Priorität plus 1 und steht vorn" <| fun () ->
            Persistence.initializeDatabase ()
            let highest =
                Persistence.Rules.getAllRules () |> run
                |> List.fold (fun top rule -> max top rule.Priority) 0
            insert (savedRule (uniquePrefix ()) (highest + 7) None) |> ignore
            let target = YnabCategoryId (Guid.NewGuid())
            let port, _ = portAnswering (Ok [ category target "Drogerie" ])

            let created =
                createRule port now (newRule (uniquePrefix ()) target) |> run
                |> Result.defaultWith (fun error -> failtest $"Anlegen scheiterte: {error}")

            Expect.equal created.Priority (highest + 8) "höchste plus 1"
            Expect.equal created.CategoryName "Drogerie" "Name aus YNAB"
            Expect.equal (Persistence.Rules.getAllRules () |> run |> List.head).Id created.Id "steht vorn"

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
let importTests =
    testSequenced <| testList "Regeln importieren" [
        let fileOf (rules: Rule list) = Encode.Auto.toString (4, rules)

        testCase "eine gültige Datei legt alle Regeln an, gleiche Priorität in Dateireihenfolge" <| fun () ->
            Persistence.initializeDatabase ()
            let prefix = uniquePrefix ()
            let existing = insert (savedRule $"{prefix} bestehend" 1 None)
            let file =
                [ savedRule $"{prefix} Z" 3 (Some "Ein Payee"); savedRule $"{prefix} M" 3 None; savedRule $"{prefix} A" 3 None ]

            let result = importRules Guid.NewGuid now (fileOf file) |> run

            Expect.equal result (Ok 3) "drei importiert"
            Expect.equal
                (rulesNamed prefix |> List.map (fun rule -> rule.Name))
                [ $"{prefix} Z"; $"{prefix} M"; $"{prefix} A"; existing.Name ]
                "Dateireihenfolge, bestehende bleibt"
            Expect.equal (rulesNamed $"{prefix} Z").Head.PayeeOverride (Some "Ein Payee") "Override aus der Datei"
            Expect.equal (rulesNamed $"{prefix} Z").Head.CategoryName "Lebensmittel" "Kategoriename aus der Datei"

        // Bug: der Import prüfte nur das Muster, eine Regel ohne Namen kam ungeprüft durch.
        testCase "die dritte von vier Regeln hat keinen Namen: keine neue Zeile" <| fun () ->
            Persistence.initializeDatabase ()
            let prefix = uniquePrefix ()
            let file =
                [ savedRule $"{prefix} 1" 1 None
                  savedRule $"{prefix} 2" 1 None
                  savedRule "" 1 None
                  savedRule $"{prefix} 4" 1 None ]

            let result = importRules Guid.NewGuid now (fileOf file) |> run

            Expect.equal result (Error (RulesError.ValidationFailed [ "Regel 3: Regelname ist erforderlich" ])) "Meldung"
            Expect.isEmpty (rulesNamed prefix) "keine neue Zeile"

        testCase "die Meldung nennt alle fehlerhaften Regeln" <| fun () ->
            Persistence.initializeDatabase ()
            let prefix = uniquePrefix ()
            let file =
                [ { savedRule $"{prefix} Muster" 1 None with Pattern = "("; PatternType = Regex }
                  savedRule $"{prefix} gut" 1 None
                  savedRule $"{prefix} Override" 1 (Some " ")
                  savedRule $"{prefix} Priorität" -1 None ]

            match importRules Guid.NewGuid now (fileOf file) |> run with
            | Error (RulesError.ValidationFailed errors) ->
                Expect.equal errors.Length 3 "drei Fehler"
                Expect.stringStarts errors[0] $"{prefix} Muster: " "Muster"
                Expect.equal errors[1] $"{prefix} Override: Payee-Override darf nicht leer sein" "Override"
                Expect.equal errors[2] $"{prefix} Priorität: Priorität muss zwischen 0 und 10000 liegen" "Priorität"
            | other -> failtest $"Erwartet ValidationFailed, war {other}"
            Expect.isEmpty (rulesNamed prefix) "keine neue Zeile"

        // Bug: ohne Transaktion blieben die ersten beiden Regeln stehen.
        testCase "das Einfügen der dritten Regel scheitert: keine neue Zeile" <| fun () ->
            Persistence.initializeDatabase ()
            let prefix = uniquePrefix ()
            let file = [ for i in 1 .. 4 -> savedRule $"{prefix} {i}" 1 None ]
            let duplicate = Guid.NewGuid()
            let ids = Collections.Generic.Queue [ Guid.NewGuid(); duplicate; duplicate; Guid.NewGuid() ]

            let result = importRules ids.Dequeue now (fileOf file) |> run

            match result with
            | Error (RulesError.DatabaseError ("import_rules", _)) -> ()
            | other -> failtest $"Erwartet DatabaseError, war {other}"
            Expect.isEmpty (rulesNamed prefix) "keine neue Zeile"

        testCase "eine Datei ohne gültiges JSON wird abgelehnt" <| fun () ->
            match importRules Guid.NewGuid now "kein json" |> run with
            | Error (RulesError.ValidationFailed [ message ]) ->
                Expect.stringStarts message "Die Importdatei ist ungültig" "Meldung"
            | other -> failtest $"Erwartet ValidationFailed, war {other}"
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
