module TestGroups

// Namen der Gruppen für `testSequencedGroup` (infrastructure-x7p2e). Expecto führt die Fälle einer Gruppe
// nacheinander aus, die Gruppe als Ganzes aber parallel zu allen anderen Tests. `testSequenced` ohne Gruppe hält
// dagegen jeden Fall an, bis alles andere fertig ist, und läuft danach allein: das kostete im November 2026 rund
// die Hälfte der Laufzeit. Ein Name hier statt eines Textes am Aufruf, damit ein Tippfehler keine zweite Gruppe
// aufmacht.

/// Alle Listen auf der geteilten In-Memory-DB (`USE_MEMORY_DB`, `Cache=Shared`). Gleichzeitig bremsen sie sich über
/// die Tabellensperren des Shared Cache aus: Microsoft.Data.Sqlite wiederholt eine gesperrte Anweisung erst nach
/// einer Pause. Alle DB-Listen parallel brauchten 140 s statt 65 s, und einige lesen über alle Zeilen einer
/// Tabelle (Zählungen, Sortierung, Kontostände, Einstellungen).
///
/// Dazu gehören auch die Listen der Schreib-Sperre (`LedgerWriteGuardProbe`): ihre Fälle halten einen Spiegel-Lauf
/// fest und schreiben Buchungen. In einer eigenen Gruppe sah die Sortierung im Lesepfad deren Buchungen im Zähler
/// „Zu prüfen“ (zwei von zwei Probeläufen rot), und die Befehle der DB-Gruppe warteten an der Sperre.
///
/// Auch die Listen, die das Server-Log über `Console.SetError` fangen (`UnderfundedTests`, `BudgetLeadTests`), stehen
/// hier: sie lesen und schreiben die geteilte DB, und `Console.SetError` gilt prozessweit, darf also nicht zweimal
/// gleichzeitig umgeleitet sein. Eine eigene Gruppe „Server-Log“ lief parallel zur DB-Gruppe; seit beide Bedürfnisse
/// in dieser Gruppe liegen, leitet keine andere Liste das Fehler-Log um, solange eine davon läuft.
[<Literal>]
let InMemoryDb = "In-Memory-DB"
