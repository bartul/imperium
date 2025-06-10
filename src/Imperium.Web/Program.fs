open System
open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Logging

[<EntryPoint>]
let main args =
    let builder = WebApplication.CreateBuilder(args)
    builder.Logging.AddConsole() |> ignore
    
    let app = builder.Build()
    
    let logger = app.Services.GetRequiredService<ILogger<obj>>()
    app.Services.GetRequiredService<IHostApplicationLifetime>()
        .ApplicationStarted
        .Register(fun () -> logger.LogInformation("Web application Imperium started")) |> ignore

    app.MapGet("/", Func<string>(fun () -> "Hello Imperium!")) |> ignore
    
    app.Run()
    
    0 // Exit code

