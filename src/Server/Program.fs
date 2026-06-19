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
    let builder = WebApplication.CreateBuilder(args)

    // Add Giraffe
    builder.Services.AddGiraffe() |> ignore

    // Configure CORS for development
    builder.Services.AddCors(fun options ->
        options.AddPolicy("AllowAll", fun policy ->
            policy
                .AllowAnyOrigin()
                .AllowAnyMethod()
                .AllowAnyHeader()
            |> ignore
        )
    ) |> ignore

    let app = builder.Build()

    // Initialize database (creates tables if they don't exist)
    Persistence.initializeDatabase()
    printfn "✅ Database initialized"

    if app.Environment.IsDevelopment() then
        app.UseDeveloperExceptionPage() |> ignore

    app.UseCors("AllowAll") |> ignore
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
    app.UseGiraffe(Server.Api.webApp())

    // SPA fallback: serve index.html for non-API routes
    if Directory.Exists(publicPath) then
        let indexPath = Path.Combine(publicPath, "index.html")
        if File.Exists(indexPath) then
            app.MapFallbackToFile("index.html", StaticFileOptions(
                FileProvider = new PhysicalFileProvider(publicPath)
            )) |> ignore

    printfn "🚀 BudgetBuddy server starting on http://localhost:5081"

    app.Run("http://0.0.0.0:5081")
    0
