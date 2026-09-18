module TestDatabaseGuardTests

// Wächter gegen den Vorfall vom 2026-09-12 (siehe ADR 0030): ein Testlauf ohne
// USE_MEMORY_DB schrieb 106 Test-Konten und 365 Test-Buchungen in Romans echte
// Entwicklungsdatenbank. ADR 0030 setzt USE_MEMORY_DB über
// `src/Tests/test.runsettings`, aber zwei Wege umgehen das stillschweigend:
// `dotnet test --settings andere.runsettings` überschreibt die
// RunSettingsFilePath-Angabe aus der .fsproj, und ein direkter
// `dotnet vstest`-Aufruf liest die Projekt-Einstellung gar nicht. In beiden
// Fällen fällt der Lauf lautlos auf die Datei-Datenbank zurück.
//
// Dieser Test prüft deshalb nicht die Umgebungsvariable selbst, sondern den
// Zustand, den Persistence tatsächlich aufgelöst hat: `activeConnectionString`
// ruft denselben `dbConfig.Force()`-Pfad wie jede echte Datenbank-Operation auf,
// öffnet dabei aber keine eigene Connection. Zeigt der aktive Connection-String
// nicht auf eine In-Memory-Datenbank, scheitert dieser Test laut und nennt den
// Pfad, auf den er zeigen würde.
//
// Der Test liegt bewusst als erste Compile-Unit vor allen Tests, die die
// Datenbank anfassen (siehe Tests.fsproj), und setzt USE_MEMORY_DB selbst NICHT:
// das würde genau die Umgehung verdecken, gegen die er schützen soll.

open Expecto

[<Tests>]
let tests =
    testList "Test-Datenbank-Wächter" [
        testCase "Testlauf zeigt auf eine In-Memory-Datenbank" <| fun () ->
            let cs = Persistence.activeConnectionString ()
            Expect.stringContains cs ":memory:"
                $"Testlauf zeigt auf eine Datei-Datenbank: {cs}"
    ]
