# Backend Overview

> Giraffe + Fable.Remoting basics for F# web applications.

## Overview

Giraffe uses a functional composition model with `HttpHandler` as the core abstraction. Fable.Remoting provides type-safe RPC communication between client and server.

## When to Use This

- Starting a new backend project
- Understanding Giraffe architecture
- Setting up Fable.Remoting
- Configuring application startup

## HttpHandler Basics

```fsharp
type HttpHandler = HttpContext -> Task<HttpContext option>
```

- Returns `Some context` to continue pipeline
- Returns `None` to short-circuit

### Composition Operator

```fsharp
let (>=>) = compose

// Example
let handler = validateRequest >=> processData >=> returnJson
```

## Project Structure

```
src/Server/
├── Program.fs          # Entry point, configuration
├── Api.fs              # Fable.Remoting implementation
├── Domain.fs           # Pure business logic
├── Persistence.fs      # Database/file I/O
└── Validation.fs       # Input validation
```

## Program.fs Setup

```fsharp
module Program

open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.DependencyInjection
open Giraffe

let configureApp (app: IApplicationBuilder) =
    Persistence.ensureDataDir()
    Persistence.initializeDatabase()

    app.UseStaticFiles() |> ignore
    app.UseRouting() |> ignore
    app.UseGiraffe(Api.webApp)

let configureServices (services: IServiceCollection) =
    services.AddGiraffe() |> ignore
    services.AddLogging(...) |> ignore

[<EntryPoint>]
let main args =
    let builder = WebApplication.CreateBuilder(args)
    builder.WebHost.UseUrls("http://0.0.0.0:5000") |> ignore

    configureServices builder.Services

    let app = builder.Build()

    if app.Environment.IsDevelopment() then
        app.UseDeveloperExceptionPage() |> ignore

    app.UseStaticFiles() |> ignore
    configureApp app

    app.Run()
    0
```

## Middleware Order

Order matters:

1. Static files (serve before routing)
2. Routing
3. Giraffe/Fable.Remoting handlers

## Checklist

- [ ] Program.fs configured
- [ ] Database initialized at startup
- [ ] Static files served
- [ ] Fable.Remoting routes configured
- [ ] Logging enabled
- [ ] Port configured (default: 5000)

## See Also

- `api-implementation.md` - Implementing APIs
- `domain-logic.md` - Business logic patterns
- `../shared/api-contracts.md` - API contracts
