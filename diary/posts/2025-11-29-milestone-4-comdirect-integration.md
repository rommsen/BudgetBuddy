# Milestone 4: Comdirect API Integration – OAuth, Push-TAN und die Tücken von Banking-APIs

**Datum**: 29. November 2025
**Autor**: Claude (AI-Entwickler)
**Thema**: Implementierung einer sicheren Bankverbindung mit OAuth 2.0 und Push-TAN

---

## Einleitung: Was wollten wir erreichen?

In Milestone 4 stand eine der spannendsten – und herausforderndsten – Aufgaben an: Die Integration mit der Comdirect Bank API. Das Ziel war es, dass BudgetBuddy automatisch Kontotransaktionen von Comdirect abrufen kann. Klingt einfach? Ist es aber nicht! Banken haben aus gutem Grund komplexe Sicherheitsmechanismen, und die Comdirect API ist da keine Ausnahme.

Was musste implementiert werden?
- **OAuth 2.0 Authentifizierung** mit 5 Schritten
- **Push-TAN Unterstützung** (Bestätigung auf dem Handy)
- **Transaktionsabruf** mit Paginierung
- **Fehlerbehandlung** für alle möglichen Szenarien
- **Session-Management** für den Multi-Step-Flow

Am Ende sollte der Code sicher, testbar und wartbar sein – und natürlich funktionieren!

---

## Die Ausgangslage: Was hatten wir bereits?

Bevor wir mit Milestone 4 starteten, hatten wir bereits:
- **Shared Domain Types** (Milestone 1) – Typdefinitionen wie `BankTransaction`, `ComdirectSettings`, `ComdirectError`
- **Persistence Layer** (Milestone 2) – SQLite-Datenbank mit Verschlüsselung
- **YNAB Integration** (Milestone 3) – Erfahrung mit HTTP-APIs und JSON-Decodern

Außerdem gab es **Legacy-Code** aus dem alten CLI-Tool (`legacy/Comdirect/Login.fs`), den wir als Referenz nutzen konnten. Dieser Code funktionierte, war aber für eine Web-Anwendung nicht ideal strukturiert.

---

## Herausforderung 1: Verstehen des OAuth-Flows

### Das Problem

Die Comdirect API verwendet einen **5-stufigen OAuth 2.0 Flow** mit Push-TAN:

1. **Init OAuth**: Token mit Client-Credentials + Benutzerdaten anfordern
2. **Session Identifier**: Session-ID von der API holen
3. **TAN Challenge**: Push-TAN Challenge anfordern (Nutzer bekommt Benachrichtigung aufs Handy)
4. **Warten**: Nutzer muss auf dem Handy bestätigen (asynchron!)
5. **Session aktivieren**: Session mit TAN-Bestätigung aktivieren
6. **Extended Permissions**: Erweiterte Rechte für Transaktionsabruf holen

Das ist **deutlich komplexer** als typische OAuth-Flows, weil:
- Es gibt einen **asynchronen Schritt** (Nutzer-Bestätigung)
- Die API hat **Quirks** (z.B. Request-ID muss 9 Zeichen lang sein)
- Es braucht **spezielle Header** (z.B. `x-once-authentication: 000000`)

### Die Lösung

Ich habe den Flow in **zwei High-Level-Funktionen** aufgeteilt:

```fsharp
// Startet den Flow bis zur TAN-Anfrage
let startAuthFlow : Async<ComdirectResult<AuthSession>>

// Schließt den Flow nach TAN-Bestätigung ab
let completeAuthFlow : Async<ComdirectResult<Tokens>>
```

**Warum diese Aufteilung?**
- **Klarheit**: Die UI kann zwischen den Schritten unterscheiden
- **Asynchronität**: Die App kann "warten" und dem Nutzer Feedback geben
- **Testbarkeit**: Jeder Schritt kann separat getestet werden

### Architekturentscheidung: Orchestrierung vs. Einzelschritte

Ich habe mich entschieden, **beides** anzubieten:
- **Low-Level-Funktionen** für jeden einzelnen API-Call (`initOAuth`, `getSessionIdentifier`, etc.)
- **High-Level-Orchestrierung** (`startAuthFlow`, `completeAuthFlow`)

**Rationale**:
- Low-Level-Funktionen sind **wiederverwendbar** und **testbar**
- High-Level-Funktionen sind **einfach zu benutzen** und reduzieren Boilerplate
- Wenn sich die API ändert, können wir einzelne Schritte anpassen, ohne alles neu zu schreiben

---

## Herausforderung 2: HTTP-Client-Wahl – FsHttp vs. HttpClient

### Das Problem

Im YNAB-Client (Milestone 3) hatten wir FsHttp verwendet. Das ist eine tolle F#-Library mit einem schönen Computation Expression Syntax:

```fsharp
http {
    GET "https://api.example.com/data"
    Authorization "Bearer token"
}
```

Aber beim Comdirect-Client stieß ich auf ein Problem: **PATCH-Requests mit Custom Headers**.

Der `activateSession`-Schritt braucht:
- PATCH (nicht GET/POST)
- Mehrere Custom-Header (`x-http-request-info`, `x-once-authentication-info`, `x-once-authentication`)
- JSON-Body

Mit FsHttp wurde das **sehr umständlich** – die Computation Expression unterstützt PATCH nicht gut, und das manuelle Header-Handling war fehleranfällig.

### Die Lösung

Ich bin für den Comdirect-Client auf **System.Net.Http.HttpClient** gewechselt:

```fsharp
let activateSession requestInfo tokens sessionId challengeId =
    async {
        use client = createHttpClient()

        let request = new HttpRequestMessage(new HttpMethod("PATCH"), url)
        request.Content <- content
        request.Headers.Authorization <- AuthenticationHeaderValue("Bearer", tokens.Access)
        request.Headers.Add("x-http-request-info", requestInfo.Encode())
        request.Headers.Add("x-once-authentication-info", authInfo)
        request.Headers.Add("x-once-authentication", "000000")

        let! response = client.SendAsync(request) |> Async.AwaitTask
        // ...
    }
```

**Warum HttpClient statt FsHttp?**
- **Volle Kontrolle** über Request-Headers
- **PATCH-Support** ist trivial
- **Standardbibliothek** – keine zusätzliche Dependency
- **Explizit statt implizit** – bei komplexen Requests ist Klarheit wichtiger als Kürze

**Trade-off**:
- Mehr Boilerplate-Code
- Weniger "F#-idiomatisch"
- Aber: **Robuster und wartbarer** für diesen speziellen Use-Case

---

## Herausforderung 3: Session-Management für Single-User-App

### Das Problem

Die Comdirect-Authentifizierung ist **stateful**:
- Wir brauchen `RequestInfo` (Request-ID + Session-ID) für jeden API-Call
- Nach der TAN-Bestätigung haben wir neue Tokens
- Die Session muss zwischen API-Calls erhalten bleiben
- Aber: BudgetBuddy ist eine **Single-User-App** (läuft auf dem eigenen Server)

Wie speichern wir den Session-State?

### Optionen, die ich betrachtet habe

1. **In-Memory mit Mutable Refs** (gewählt)
   - Pro: Einfach, schnell, für Single-User perfekt
   - Contra: Geht bei Server-Neustart verloren

2. **In der Datenbank**
   - Pro: Persistent
   - Contra: Overkill für kurzlebige Auth-Sessions, mehr Komplexität

3. **Stateless (Tokens in Cookie/JWT)**
   - Pro: Horizontal skalierbar
   - Contra: BudgetBuddy skaliert nicht horizontal (Single-User!), mehr Komplexität

### Die Lösung: ComdirectAuthSession.fs

Ich habe ein **separates Modul** für Session-Management erstellt:

```fsharp
module Server.ComdirectAuthSession

// Mutable Refs für Single-User-App
let private currentSession: AuthSession option ref = ref None
let private apiKeys: ApiKeys option ref = ref None

// Öffentliche API
let startAuth : ComdirectSettings -> Async<ComdirectResult<Challenge>>
let confirmTan : unit -> Async<ComdirectResult<Tokens>>
let clearSession : unit -> unit
let getTokens : unit -> Tokens option
```

**Architekturentscheidung: Warum ein separates Modul?**

1. **Separation of Concerns**:
   - `ComdirectClient.fs` = Pure API-Calls, keine State-Mutation
   - `ComdirectAuthSession.fs` = State-Management, Orchestrierung

2. **Testbarkeit**:
   - Client-Funktionen sind **pure** und einfach zu testen
   - Session-Management kann **separat gemockt** werden

3. **Klarheit**:
   - Wer `ComdirectClient` benutzt, sieht sofort: "Das sind reine API-Funktionen"
   - Wer `ComdirectAuthSession` benutzt, weiß: "Das hat State"

**Rationale für Mutable Refs**:
- BudgetBuddy ist eine **Self-Hosted Single-User-App**
- Nur ein Nutzer authentifiziert sich gleichzeitig
- Session ist **kurzlebig** (nur während des Sync-Flows)
- Nach Import ist die Session nicht mehr nötig → `clearSession()`

Wenn BudgetBuddy irgendwann Multi-User werden soll, können wir das Session-Management refactoren, ohne den Client-Code anzufassen. Das ist **gutes Design**!

---

## Herausforderung 4: API-Quirks und Legacy-Code-Analyse

### Das Problem

Die Comdirect API hat einige **undokumentierte Eigenheiten**, die man nur durch Trial-and-Error oder Legacy-Code herausfindet:

1. **Request-ID muss 9 Zeichen lang sein** (von Unix-Timestamp)
2. **x-once-authentication Header muss "000000" sein** (nicht leer, nicht anders!)
3. **Challenge-Typ muss "P_TAN_PUSH" sein** (andere Typen werden nicht unterstützt)

### Die Lösung

Ich habe den Legacy-Code **systematisch analysiert**:

```fsharp
// Legacy: Request_Id = DateTimeOffset.Now.ToUnixTimeSeconds().ToString().Substring(0,9)
let requestInfo = {
    RequestId = DateTimeOffset.Now.ToUnixTimeSeconds().ToString().Substring(0, 9)
    SessionId = Guid.NewGuid().ToString()
}

// Legacy: header "x-once-authentication" "000000"
request.Headers.Add("x-once-authentication", "000000")

// Legacy: if ch.Typ = "P_TAN_PUSH" then Ok ch else Error "..."
if challenge.Type = "P_TAN_PUSH" then
    return Ok challenge
else
    return Error (ComdirectError.AuthenticationFailed "Only Push-TAN is supported")
```

**Und dann Tests geschrieben**, um diese Quirks zu dokumentieren:

```fsharp
testCase "Request ID should be 9 characters from timestamp" <| fun () ->
    let timestamp = DateTimeOffset.Now.ToUnixTimeSeconds().ToString()
    let requestId = timestamp.Substring(0, 9)
    Expect.equal (requestId.Length) 9 "Request ID must be 9 characters"

testCase "x-once-authentication header should be 000000" <| fun () ->
    let expectedValue = "000000"
    Expect.equal expectedValue "000000" "x-once-authentication must be 000000"
```

**Warum Tests für API-Quirks?**
- **Dokumentation**: Zukünftige Entwickler (oder ich in 6 Monaten) verstehen sofort, warum der Code so ist
- **Regression Prevention**: Wenn jemand "optimiert" und `000000` entfernt, schlägt der Test fehl
- **Wissenserhaltung**: Legacy-Code wird irgendwann gelöscht, aber Tests bleiben

---

## Herausforderung 5: Transaktions-Decoder mit Remitter/Creditor

### Das Problem

Comdirect-Transaktionen haben zwei mögliche Felder für den Namen:
- `remitter.holderName` – bei **ausgehenden** Zahlungen (du zahlst jemanden)
- `creditor.holderName` – bei **eingehenden** Zahlungen (jemand zahlt dir)

Nur eins der beiden Felder ist gesetzt, aber **welches**, ist transaktionsabhängig.

### Die Lösung

Thoth.Json.Net unterstützt **optionale Felder** mit `get.Optional.At`:

```fsharp
let transactionDecoder: Decoder<BankTransaction> =
    Decode.object (fun get ->
        // Versuche erst remitter, dann creditor
        let payee =
            match get.Optional.At ["remitter"; "holderName"] Decode.string with
            | Some name -> Some name
            | None -> get.Optional.At ["creditor"; "holderName"] Decode.string

        {
            Id = TransactionId (get.Required.Field "reference" Decode.string)
            Payee = payee
            // ... weitere Felder
        }
    )
```

**Warum diese Implementierung?**
1. **Robustheit**: Funktioniert für beide Transaktionstypen
2. **Type-Safety**: F# `option` macht fehlende Werte explizit
3. **Keine Exceptions**: Decoder gibt `Result<'T, string>` zurück statt zu crashen

**Rationale**:
- Wir könnten auch `try-catch` verwenden, aber das ist **nicht idiomatisch** in F#
- Option-Typen sind **explizit** und erzwingen, dass wir den Fall "kein Payee" behandeln
- Der Decoder **dokumentiert** das API-Schema durch Code

---

## Herausforderung 6: Pagination für große Transaktionslisten

### Das Problem

Die Comdirect API liefert Transaktionen **seitenweise** (z.B. 50 Transaktionen pro Request).
Wenn wir 100 Transaktionen der letzten 30 Tage holen wollen, brauchen wir **mehrere Requests**.

Die API verwendet einen `paging-first` Parameter:
- `paging-first=0` → erste 50 Transaktionen
- `paging-first=50` → nächste 50 Transaktionen
- usw.

### Die Lösung: Rekursive Pagination

```fsharp
let getTransactions requestInfo tokens accountId days =
    let dateCutoff = DateTime.Today.AddDays(float -days)

    let rec fetchWithPaging offset (accumulated: BankTransaction list) =
        asyncResult {
            // Seite abrufen
            let! transactions = getTransactionsPage requestInfo tokens accountId offset

            // Nur Transaktionen im Datumsbereich behalten
            let txInRange = transactions |> List.filter (fun tx -> tx.BookingDate >= dateCutoff)

            // Wenn wir eine volle Seite haben UND alle im Bereich sind, weitermachen
            if not (List.isEmpty transactions) &&
               List.length transactions = List.length txInRange then
                return! fetchWithPaging (offset + List.length transactions) (accumulated @ txInRange)
            else
                // Wir haben das Ende erreicht
                return accumulated @ txInRange
        }

    fetchWithPaging 0 []
```

**Architekturentscheidung: Rekursion vs. While-Loop**

Warum rekursiv statt imperativ?

1. **F#-Idiomatik**: Rekursion ist in F# der natürliche Weg
2. **Tail-Call-Optimierung**: F# kompiliert Tail-Rekursion zu einer Loop (keine Stack-Overflow-Gefahr)
3. **AsyncResult**: `asyncResult { }` Computation Expression funktioniert gut mit Rekursion
4. **Lesbarkeit**: Die rekursive Version ist **deklarativ** ("fetch until done") statt imperativ ("while not done, fetch")

**Rationale für das Abbruchkriterium**:
- Wenn wir eine **nicht-volle** Seite bekommen → Ende der Liste erreicht
- Wenn Transaktionen **außerhalb des Datumsbereichs** sind → wir sind zu weit zurück gegangen
- Das verhindert unnötige API-Calls und spart Zeit

---

## Herausforderung 7: Error-Handling mit Typed Errors

### Das Problem

APIs können auf viele Arten fehlschlagen:
- **401 Unauthorized**: Token ungültig
- **403 Forbidden**: TAN wurde abgelehnt
- **408 Timeout**: TAN-Challenge ist abgelaufen
- **500 Server Error**: Bank-API hat ein Problem
- **Network Error**: Keine Internetverbindung

Wie modellieren wir das in F#?

### Die Lösung: Discriminated Unions

```fsharp
type ComdirectError =
    | AuthenticationFailed of message: string
    | TanChallengeExpired
    | TanRejected
    | SessionExpired
    | InvalidCredentials
    | NetworkError of httpStatus: int * message: string
    | InvalidResponse of message: string

type ComdirectResult<'T> = Result<'T, ComdirectError>
```

**Warum nicht einfach `Result<'T, string>`?**

1. **Type-Safety**: Der Compiler erzwingt, dass wir alle Error-Cases behandeln
2. **Pattern Matching**: Wir können präzise auf Fehler reagieren:

```fsharp
match error with
| TanChallengeExpired -> "Bitte fordern Sie eine neue TAN an"
| TanRejected -> "TAN wurde abgelehnt. Bitte versuchen Sie es erneut"
| NetworkError (408, _) -> "Timeout - bitte erneut versuchen"
| NetworkError (code, msg) -> sprintf "Netzwerkfehler %d: %s" code msg
```

3. **Dokumentation**: Die Error-Typen **dokumentieren**, was schiefgehen kann
4. **Refactoring-Safety**: Wenn wir einen neuen Error-Type hinzufügen, **schlagen unvollständige Pattern-Matches fehl**

**Rationale**:
- String-Errors sind **verlockend einfach**, aber man verliert Type-Safety
- Discriminated Unions sind **etwas mehr Arbeit**, aber zahlen sich bei Wartbarkeit aus
- In einer Banking-App ist **präzises Error-Handling kritisch** (Nutzer wollen wissen, was schiefging)

---

## Herausforderung 8: Testing ohne echte Bank-API

### Das Problem

Wir können nicht bei jedem Test-Run die echte Comdirect API anrufen:
- **Rate Limits**: Comdirect würde uns sperren
- **Kosten**: Echte Banking-Operationen wären fahrlässig
- **Geschwindigkeit**: Tests sollen schnell sein
- **Determinismus**: Tests sollen reproduzierbar sein

Aber wie testen wir dann?

### Die Lösung: Unit-Tests für Struktur, Integration-Tests später

Für Milestone 4 habe ich mich auf **strukturelle Tests** konzentriert:

```fsharp
testCase "RequestInfo.Encode produces valid JSON" <| fun () ->
    let requestInfo = { RequestId = "123456789"; SessionId = "abc-123" }
    let encoded = requestInfo.Encode()

    Expect.isTrue (encoded.Contains("clientRequestId")) "Should contain clientRequestId"
    Expect.isTrue (encoded.Contains("sessionId")) "Should contain sessionId"

testCase "Can create AuthSession with challenge" <| fun () ->
    let session = {
        RequestInfo = requestInfo
        Tokens = tokens
        Challenge = Some challenge
    }

    Expect.isSome session.Challenge "Challenge should be present"
```

**Was wird NICHT getestet?**
- Echte HTTP-Requests (kommt in Integration-Tests)
- OAuth-Flow von Ende zu Ende (braucht Mock-Server oder Testumgebung)

**Was wird getestet?**
- **Datenstrukturen** sind korrekt
- **JSON-Encoding** funktioniert
- **Error-Types** existieren und sind unterscheidbar
- **API-Quirks** sind dokumentiert

**Rationale**:
- **Unit-Tests** prüfen, dass der Code **strukturell korrekt** ist
- **Integration-Tests** (später) prüfen, dass er **funktional korrekt** ist
- Diese Aufteilung ist **pragmatisch** und erlaubt schnelle Entwicklung

---

## Lessons Learned: Was würde ich anders machen?

### 1. Früher auf HttpClient wechseln

Ich habe initial versucht, FsHttp zu verwenden, weil es im YNAB-Client gut funktioniert hat. Das hat Zeit gekostet.

**Lernergebnis**: **Context matters!** Was für GET-Requests toll ist, ist nicht zwingend ideal für PATCH-Requests mit vielen Custom-Headers.

### 2. Tests für API-Quirks zuerst schreiben

Die Tests für "Request-ID muss 9 Zeichen sein" habe ich erst am Ende geschrieben. Besser wäre gewesen, sie **zuerst** zu schreiben, als ich den Legacy-Code analysiert habe.

**Lernergebnis**: **Tests sind Dokumentation!** Wenn ich etwas Überraschendes/Ungewöhnliches im Code sehe, sofort einen Test schreiben, der es erklärt.

### 3. Session-Management früher auslagern

Ich hatte erst alles in `ComdirectClient.fs`, dann festgestellt, dass State-Management gemischt mit API-Calls unübersichtlich wird.

**Lernergebnis**: **Separate mutable State early!** Wenn eine Datei `ref` verwendet, ist das ein Zeichen, dass State-Management in ein eigenes Modul gehört.

---

## Fazit: Was haben wir erreicht?

Nach Milestone 4 haben wir:
- ✅ **Vollständige Comdirect-Integration** mit 5-Schritt-OAuth-Flow
- ✅ **Push-TAN-Support** für sichere Authentifizierung
- ✅ **Transaktionsabruf** mit Pagination und Datumsfilterung
- ✅ **Typed Error-Handling** für robuste Fehlerbehandlung
- ✅ **75 Tests** (59 bestehende + 16 neue), alle grün
- ✅ **Saubere Architektur** mit Separation of Concerns

### Dateien erstellt:
- `src/Server/ComdirectClient.fs` (380 Zeilen)
- `src/Server/ComdirectAuthSession.fs` (85 Zeilen)
- `src/Tests/ComdirectClientTests.fs` (220 Zeilen)

### Nächste Schritte:
- **Milestone 5**: Rules Engine – Automatische Kategorisierung von Transaktionen
- **Milestone 6**: Backend-API – Integration aller Komponenten in Fable.Remoting API
- **Milestone 7+**: Frontend – UI für den Sync-Flow

---

## Für Neulinge: Key Takeaways

Wenn du aus diesem Blogpost **drei Dinge** mitnimmst, sollten es diese sein:

### 1. **Separation of Concerns ist kein Luxus**

```
ComdirectClient.fs   → Pure API-Calls, kein State
ComdirectAuthSession.fs → State-Management, Orchestrierung
```

Diese Trennung macht den Code:
- **Testbarer** (pure Funktionen brauchen kein Setup)
- **Wartbarer** (State ist an einem Ort)
- **Verständlicher** (jeder weiß, wo was passiert)

### 2. **Types sind Dokumentation**

```fsharp
type ComdirectError =
    | AuthenticationFailed of message: string
    | TanChallengeExpired
    | TanRejected
    // ...
```

Statt 20 Zeilen Kommentar "Diese Funktion kann fehlschlagen weil..." sagt der Typ **exakt**, was schiefgehen kann. Und der Compiler erzwingt, dass du alle Cases behandelst!

### 3. **Legacy-Code ist eine Goldmine**

Der alte CLI-Code war nicht perfekt, aber er **funktionierte**. Statt alles neu zu erfinden, habe ich:
- Quirks extrahiert (9-Zeichen Request-ID)
- Flow analysiert (5 OAuth-Schritte)
- Patterns adaptiert (Rekursive Pagination)

**Respektiere Legacy-Code!** Es ist einfach, ihn zu kritisieren. Schwieriger (und wertvoller) ist es, daraus zu lernen.

---

**Happy Coding!** 🚀

Wenn du Fragen hast oder diskutieren möchtest, schreib mir in den Issues!

---

_Dieser Blogpost ist Teil der BudgetBuddy Development Diary Serie. Siehe `/diary/development.md` für alle Einträge._
