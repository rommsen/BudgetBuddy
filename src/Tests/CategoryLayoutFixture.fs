/// Baustein für Tests zu Deaktivieren und Reihenfolge der Kategorien (budgeting-sn2sn): eine eigene In-Memory-Datenbank
/// mit `events` und `budget_category_layout`, getrennt von der gemeinsamen Testdatenbank. Die erste Verbindung hält
/// sie am Leben, `connect` liefert je Aufruf eine neue.
module CategoryLayoutFixture

open Microsoft.Data.Sqlite

let isolated () : SqliteConnection * (unit -> SqliteConnection) =
    let connectionString = $"Data Source=category-layout-{System.Guid.NewGuid():N};Mode=Memory;Cache=Shared"
    let anchor = new SqliteConnection(connectionString)
    anchor.Open()
    EventStore.createSchemaOn anchor
    Budgeting.Persistence.createCategoryLayoutSchemaOn anchor
    anchor, (fun () -> new SqliteConnection(connectionString))
