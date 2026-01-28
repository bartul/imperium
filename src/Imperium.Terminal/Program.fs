module Imperium.Terminal.Program

#nowarn "40" // Recursive object references checked at runtime (intentional lazy pattern)

open System
open Imperium.Primitives
open Imperium.Rondel
open Imperium.Terminal
open Imperium.Terminal.Rondel
open Imperium.Terminal.Accounting

// ──────────────────────────────────────────────────────────────────────────
// Composition Root
// ──────────────────────────────────────────────────────────────────────────

let private createHosts () =
    let bus = Bus.create ()
    let store = InMemoryRondelStore.create ()

    let rec rondelHost: Lazy<RondelHost> =
        lazy
            (RondelHost.create store bus (fun () cmd ->
                async {
                    try
                        do! accountingHost.Value.Execute cmd
                        return Ok()
                    with ex ->
                        printfn "Error dispatching to Accounting: %s" ex.Message
                        return Error ex.Message
                }))

    and accountingHost: Lazy<AccountingHost> = lazy (AccountingHost.create bus)

    // Force initialization to fail fast on configuration errors
    let rondel = rondelHost.Force()
    let accounting = accountingHost.Force()

    rondel, accounting

// ──────────────────────────────────────────────────────────────────────────
// REPL
// ──────────────────────────────────────────────────────────────────────────

let private parseSpace (s: string) =
    match s.ToLowerInvariant() with
    | "investor" -> Some Space.Investor
    | "import" -> Some Space.Import
    | "productionone" | "production1" -> Some Space.ProductionOne
    | "maneuverone" | "maneuver1" -> Some Space.ManeuverOne
    | "taxation" -> Some Space.Taxation
    | "factory" -> Some Space.Factory
    | "productiontwo" | "production2" -> Some Space.ProductionTwo
    | "maneuvertwo" | "maneuver2" -> Some Space.ManeuverTwo
    | _ -> None

let private printHelp () =
    printfn ""
    printfn "Commands:"
    printfn "  init <nation1> [nation2] ...  - Start new game with nations"
    printfn "  move <nation> <space>         - Move nation to space"
    printfn "  pos                           - Show nation positions"
    printfn "  overview                      - Show game overview"
    printfn "  help                          - Show this help"
    printfn "  quit                          - Exit"
    printfn ""
    printfn "Spaces: Investor, Import, ProductionOne, ManeuverOne,"
    printfn "        Taxation, Factory, ProductionTwo, ManeuverTwo"
    printfn ""

let private runRepl (rondelHost: RondelHost) =
    let mutable currentGameId: Id option = None
    let mutable running = true

    printfn "Imperium Terminal - REPL"
    printfn "Type 'help' for commands"
    printfn ""

    while running do
        printf "> "
        let input = Console.ReadLine()

        if isNull input then
            running <- false
        else
            let parts = input.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries)

            if parts.Length > 0 then
                try
                    match parts.[0].ToLowerInvariant() with
                    | "quit" | "exit" -> running <- false

                    | "help" -> printHelp ()

                    | "init" when parts.Length >= 2 ->
                        let nations = parts |> Array.skip 1 |> Set.ofArray
                        let gameId = Id.newId ()
                        currentGameId <- Some gameId

                        SetToStartingPositions { GameId = gameId; Nations = nations }
                        |> rondelHost.Execute
                        |> Async.RunSynchronously

                        printfn "Game initialized with nations: %s" (String.Join(", ", nations))
                        printfn "GameId: %s" (Id.toString gameId)

                    | "init" -> printfn "Usage: init <nation1> [nation2] ..."

                    | "move" when parts.Length = 3 ->
                        match currentGameId with
                        | None -> printfn "No game initialized. Use 'init' first."
                        | Some gameId ->
                            let nation = parts.[1]

                            match parseSpace parts.[2] with
                            | None -> printfn "Unknown space: %s" parts.[2]
                            | Some space ->
                                Move { GameId = gameId; Nation = nation; Space = space }
                                |> rondelHost.Execute
                                |> Async.RunSynchronously

                                printfn "Move command sent: %s -> %A" nation space

                    | "move" -> printfn "Usage: move <nation> <space>"

                    | "pos" ->
                        match currentGameId with
                        | None -> printfn "No game initialized. Use 'init' first."
                        | Some gameId ->
                            let result =
                                rondelHost.QueryPositions { GameId = gameId } |> Async.RunSynchronously

                            match result with
                            | None -> printfn "Game not found"
                            | Some view ->
                                printfn "Positions:"

                                for p in view.Positions do
                                    let current =
                                        match p.CurrentSpace with
                                        | Some s -> sprintf "%A" s
                                        | None -> "(start)"

                                    let pending =
                                        match p.PendingSpace with
                                        | Some s -> sprintf " [pending: %A]" s
                                        | None -> ""

                                    printfn "  %s: %s%s" p.Nation current pending

                    | "overview" ->
                        match currentGameId with
                        | None -> printfn "No game initialized. Use 'init' first."
                        | Some gameId ->
                            let result =
                                rondelHost.QueryOverview { GameId = gameId } |> Async.RunSynchronously

                            match result with
                            | None -> printfn "Game not found"
                            | Some view ->
                                printfn "Game: %s" (Id.toString view.GameId)
                                printfn "Nations: %s" (String.Join(", ", view.NationNames))

                    | cmd -> printfn "Unknown command: %s (type 'help' for commands)" cmd
                with ex ->
                    printfn "Error: %s" ex.Message

    printfn "Goodbye!"

// ──────────────────────────────────────────────────────────────────────────
// Entry Point
// ──────────────────────────────────────────────────────────────────────────

[<EntryPoint>]
let main _ =
    let rondelHost, _ = createHosts ()
    runRepl rondelHost
    0
