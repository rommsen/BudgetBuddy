module Program

open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.FileProviders
open Giraffe
open System.IO

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

    // Initialize data directory
    Persistence.ensureDataDir()

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

        // Serve static files
        app.UseStaticFiles(StaticFileOptions(
            FileProvider = fileProvider
        )) |> ignore

        printfn $"📁 Serving static files from: {publicPath}"
    else
        printfn $"⚠️ Static files directory not found: {publicPath}"

    // API routes
    app.UseGiraffe(Api.webApp())

    // SPA fallback: serve index.html for non-API routes
    if Directory.Exists(publicPath) then
        let indexPath = Path.Combine(publicPath, "index.html")
        if File.Exists(indexPath) then
            app.MapFallbackToFile("index.html", StaticFileOptions(
                FileProvider = new PhysicalFileProvider(publicPath)
            )) |> ignore

    printfn "🚀 Server starting on http://localhost:5001"
    printfn "📊 Counter API ready at /api/ICounterApi/*"

    app.Run("http://0.0.0.0:5001")
    0
