module Program

open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.FileProviders
open Microsoft.AspNetCore.StaticFiles
open Giraffe
open System.IO

/// Content-type provider that knows the PWA-specific extensions the .NET default
/// map omits. Without this, .webmanifest is served as application/octet-stream and
/// browsers reject the manifest (infra-002): SW + manifest must be served at the
/// root with correct MIME. sw.js is plain .js (already mapped) — listed here only
/// for clarity of intent.
let pwaContentTypeProvider () =
    let provider = FileExtensionContentTypeProvider()
    provider.Mappings[".webmanifest"] <- "application/manifest+json"
    provider

[<EntryPoint>]
let main args =
    // ASPNETCORE_URLS legt fest, worauf Kestrel bindet (app.Run() unten wird
    // ohne Argument aufgerufen und respektiert die Variable von selbst). Muss
    // VOR WebApplication.CreateBuilder gesetzt werden, weil die Konfiguration
    // Umgebungsvariablen dort einliest; ein späteres Setzen käme zu spät.
    // Server.ServerConfig.resolveUrls (rein, siehe dort) hält den bisherigen
    // Standardport 5081, wenn nichts gesetzt ist (infra-jh7g9: der Fehler war,
    // dass ein explizites URL-Argument an app.Run ASPNETCORE_URLS bisher
    // überschrieb, compose.yamls Zeile also wirkungslos war).
    let urls = Server.ServerConfig.resolveUrls (System.Environment.GetEnvironmentVariable("ASPNETCORE_URLS"))
    System.Environment.SetEnvironmentVariable("ASPNETCORE_URLS", urls)

    // Ohne Europe/Berlin (Image ohne tzdata) gäbe es keine verlässliche Bob-Tagesgrenze:
    // lieber beim Start abbrechen als später falsch rechnen.
    // Die aufgelöste Zone geht als Abhängigkeit an die Bot-Schnittstelle (ledger-ct981).
    let berlinZone =
        match Server.ServerConfig.tryResolveBerlinZone () with
        | Error msg ->
            eprintfn "❌ %s" msg
            exit 1
        | Ok zone -> zone

    let builder = WebApplication.CreateBuilder(args)

    // Add Giraffe
    builder.Services.AddGiraffe() |> ignore

    // Erneuert die Bankverbindung. Nur hier registriert, im TestServer (`Api.webApp`) läuft der
    // Dienst nie (ADR 0071 Punkt 8, Wächter in StructureGuardTests.fs).
    builder.Services.AddHostedService(fun (_: System.IServiceProvider) ->
        new Server.BankConnectionWorker.BankConnectionWorker(
            Server.ComdirectAuthSession.runTick,
            Server.BankConnection.tickPeriod))
    |> ignore

    let app = builder.Build()

    // Initialize database (creates tables if they don't exist)
    Persistence.initializeDatabase()
    Ledger.Persistence.initializeLedgerSchema()
    Budgeting.Persistence.initializeBudgetingSchema()
    printfn "✅ Database initialized"

    if app.Environment.IsDevelopment() then
        app.UseDeveloperExceptionPage() |> ignore

    app.UseRouting() |> ignore

    // Serve static files from dist/public
    let publicPath = Path.Combine(Directory.GetCurrentDirectory(), "dist", "public")
    if Directory.Exists(publicPath) then
        let fileProvider = new PhysicalFileProvider(publicPath)

        // Serve index.html for root path
        app.UseDefaultFiles(DefaultFilesOptions(
            FileProvider = fileProvider
        )) |> ignore

        // Serve static files (with PWA-aware MIME map so .webmanifest is valid)
        app.UseStaticFiles(StaticFileOptions(
            FileProvider = fileProvider,
            ContentTypeProvider = pwaContentTypeProvider ()
        )) |> ignore

        printfn $"📁 Serving static files from: {publicPath}"
    else
        printfn $"⚠️ Static files directory not found: {publicPath}"

    // API routes
    app.UseGiraffe(Server.Api.webApp berlinZone)

    // SPA fallback: serve index.html for non-API routes
    if Directory.Exists(publicPath) then
        let indexPath = Path.Combine(publicPath, "index.html")
        if File.Exists(indexPath) then
            app.MapFallbackToFile("index.html", StaticFileOptions(
                FileProvider = new PhysicalFileProvider(publicPath)
            )) |> ignore

    // Nennt den tatsächlich gebundenen Wert (urls, oben aus ASPNETCORE_URLS
    // aufgelöst), nicht einen festen Port: app.Run() unten bindet auf genau
    // diesen Wert, weil er ohne Argument aufgerufen wird.
    printfn $"🚀 BudgetBuddy server starting on {urls}"

    app.Run()
    0
