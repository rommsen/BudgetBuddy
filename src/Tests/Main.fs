module Program

open System
open Expecto

[<EntryPoint>]
let main args =
    // CRITICAL: Set test mode BEFORE any persistence module is loaded
    // This ensures tests use in-memory SQLite instead of production database
    //
    // Deckt nur den Direktaufruf `dotnet exec Tests.dll` ab: `dotnet test`
    // (VSTest/YoloDev.Expecto.TestSdk) ruft `main` NIE auf, der Adapter laedt die
    // Assembly als Bibliothek. Die zweite, fuer `dotnet test` wirksame Absicherung
    // steht in `test.runsettings` (RunConfiguration/EnvironmentVariables), das
    // `Tests.fsproj` automatisch einbindet (Fund ledger-b1xwe, 2026-09-12: `dotnet
    // test` ohne diese Absicherung schrieb Testdaten in die echte Datei-Datenbank
    // unter `DATA_DIR`/`~/my_apps/budgetbuddy-dev/budgetbuddy.db`, siehe ADR 0030).
    Environment.SetEnvironmentVariable("USE_MEMORY_DB", "true")

    runTestsInAssemblyWithCLIArgs [] args
