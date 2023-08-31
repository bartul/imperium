open System
open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.Hosting
open Azure.Monitor.OpenTelemetry.AspNetCore
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Logging

[<EntryPoint>]
let main args =
    let builder = WebApplication.CreateBuilder(args)
    builder.Services.AddOpenTelemetry().UseAzureMonitor() |> ignore
    
    let app = builder.Build()
    
    app.Services.GetService<IHostApplicationLifetime>()
        .ApplicationStarted
        .Register(fun () -> app.Services.GetService<ILogger>().LogInformation("Web application Imperium started") |> ignore) |> ignore  

    app.MapGet("/", Func<string>(fun () -> "Hello Imperium!")) |> ignore


    app.Run()

    0 // Exit code

