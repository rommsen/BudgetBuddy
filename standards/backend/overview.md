# Backend in BudgetBuddy

Was der Code nicht von selbst erklärt. Alles andere: den Code lesen.

## Aufbau

- `src/Server/Program.fs`: Start. Prüft die Zeitzone `Europe/Berlin` (Abbruch ohne tzdata), ruft `Persistence.initializeDatabase()`, liefert `dist/public` statisch aus, hängt `Server.Api.webApp()` per `UseGiraffe` ein, danach SPA-Fallback auf `index.html`. Port aus `ASPNETCORE_URLS`, sonst 5081 (`Server.ServerConfig.resolveUrls`); `app.Run()` ohne Argument.
- `src/Server/Api.fs`: die Remoting-Werte (`settingsApi`, `ynabApi`, `rulesApi`, `syncApi`, `ledgerApi`, `budgetApi`) und `webApp`. Routen: `/api/<Interface>/<Methode>`.
- Bounded Contexts mit eigener Schichtung: `src/Server/Ledger/` (`Domain.fs`, `Events.fs`, `CommandHandlers.fs`, `QueryHandlers.fs`, `Persistence.fs`) und `src/Server/Budgeting/`. Älterer Code (Sync, Regeln, Einstellungen) liegt flach in `src/Server/` mit `src/Server/Persistence.fs`.
- `src/Server/BotApi.fs`: HTTP-Schnittstelle unter `/bot/` für den Bot (ADR 0066), Bearer-Token aus `BOT_API_TOKEN`, Obergrenze aus `BOT_MAX_AMOUNT_EUR`.

## Giraffe-Pipeline: `choose` und `skipPipeline`

```fsharp
type HttpHandler = HttpFunc -> HttpContext -> Task<HttpContext option>
```

- `Some ctx` heißt: Anfrage ist beantwortet, die Pipeline endet hier. `next ctx` ruft den Rest der Kette auf und liefert am Ende `Some`, also ebenfalls "erledigt".
- `None` heißt: dieser Handler ist nicht zuständig. Giraffe schreibt das als `skipPipeline` (`Task.FromResult None`).
- `choose [ a; b; c ]` probiert die Handler der Reihe nach und nimmt den ersten, der `Some` liefert.

In `Server.Api.webApp` steht `BotApi.handler` vor den Remoting-Handlern im `choose`. Ein Handler, der weitergeben will, muss deshalb `skipPipeline` zurückgeben, nicht `next ctx`. Mit `next ctx` endete jede `/api/`-Anfrage mit 200 und leerem Rumpf, die App konnte nichts laden (Bug, Fix 2a5c229). `src/Tests/RemotingPipelineTests.fs` schickt je Interface eine Anfrage durch den echten Host und fängt das.

## Konventionen

- Validierung an der API-Grenze (`src/Server/Validation.fs`), alle Fehler sammeln, konkrete Meldungen auf Deutsch.
- Beträge sind YNAB-Milliunits als `int64`. Nach YNAB als JSON-Zahl kodieren (`Encode.int`), nicht als `Encode.int64`, das in Fable einen String erzeugt.
- Geheimnisse (YNAB-Token, Comdirect-Zugangsdaten) liegen verschlüsselt in der Settings-Tabelle. Der Schlüssel kommt aus `BUDGETBUDDY_ENCRYPTION_KEY`; ohne die Variable wird er aus dem Rechnernamen abgeleitet und ändert sich mit jedem neuen Container.
- Kein Login in der App: Zugriff nur über Tailscale. Einzige Authentifizierung ist das Bot-Token unter `/bot/`.
- Datenbank: `DATA_DIR` (Standard `~/my_apps/budgetbuddy-dev`). Tests laufen mit `USE_MEMORY_DB=true` (ADR 0030). Regeln für Dapper und SQL: `.claude/rules/persistence-safety.md`, `.claude/rules/query-safety.md`.
- Deployment: `Dockerfile`, `compose.yaml` und `scripts/deploy.sh` dokumentieren sich selbst.
