open System
open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.DependencyInjection


[<EntryPoint>]
let main args =
    let builder = WebApplication.CreateBuilder(args)
    let app = builder.Build()
    builder.Services.AddApplicationInsightsTelemetry() |> ignore

    app.MapGet("/", Func<string>(fun () -> "Hello Imperium!")) |> ignore

    app.Run()

    0 // Exit code

