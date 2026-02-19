module Imperium.UnitTests.RondelTests

open System
open Expecto
open Imperium.Rondel
open Imperium.Primitives

type private Rondel =
    { Execute: RondelCommand -> unit
      Handle: RondelInboundEvent -> unit
      GetNationPositions: GetNationPositionsQuery -> RondelPositionsView option
      GetRondelOverview: GetRondelOverviewQuery -> RondelView option }

let private createRondel () =
    let store = Collections.Generic.Dictionary<Id, RondelState>()

    let load (gameId: Id) : Async<RondelState option> =
        async {
            return
                match store.TryGetValue(gameId) with
                | true, state -> Some state
                | false, _ -> None
        }

    let save (state: RondelState) : Async<Result<unit, string>> =
        async {
            store.[state.GameId] <- state
            return Ok()
        }

    let publishedEvents = ResizeArray<RondelEvent>()

    let publish (event: RondelEvent) : Async<unit> = async { publishedEvents.Add event }

    let dispatchedCommands = ResizeArray<RondelOutboundCommand>()

    let dispatch (command: RondelOutboundCommand) : Async<Result<unit, string>> =
        async {
            dispatchedCommands.Add command
            return Ok()
        }

    let deps = { Load = load; Save = save; Publish = publish; Dispatch = dispatch }

    let queryDeps: RondelQueryDependencies = { Load = load }

    // Wrap async routers in synchronous interface for test convenience
    { Execute = fun cmd -> execute deps cmd |> Async.RunSynchronously
      Handle = fun evt -> handle deps evt |> Async.RunSynchronously
      GetNationPositions = fun q -> getNationPositions queryDeps q |> Async.RunSynchronously
      GetRondelOverview = fun q -> getRondelOverview queryDeps q |> Async.RunSynchronously },
    publishedEvents,
    dispatchedCommands

/// Helper to extract ChargeMovement commands from dispatched commands
let getChargeCommands (commands: ResizeArray<RondelOutboundCommand>) =
    commands
    |> Seq.choose (function
        | ChargeMovement cmd -> Some cmd
        | _ -> None)
    |> Seq.toList

/// Helper to extract VoidCharge commands from dispatched commands
let getVoidCommands (commands: ResizeArray<RondelOutboundCommand>) =
    commands
    |> Seq.choose (function
        | VoidCharge cmd -> Some cmd
        | _ -> None)
    |> Seq.toList

// Independent reference implementation for test verification
// This provides an alternate path to verify Space -> Action mapping
// without using the production Space.toAction function
let spaceToExpectedAction (space: Space) =
    match space with
    | Space.Investor -> Action.Investor
    | Space.Factory -> Action.Factory
    | Space.Import -> Action.Import
    | Space.Taxation -> Action.Taxation
    | Space.ProductionOne
    | Space.ProductionTwo -> Action.Production
    | Space.ManeuverOne
    | Space.ManeuverTwo -> Action.Maneuver

// All rondel spaces in clockwise order
let allSpaces =
    [ Space.Investor
      Space.Import
      Space.ProductionOne
      Space.ManeuverOne
      Space.Taxation
      Space.Factory
      Space.ProductionTwo
      Space.ManeuverTwo ]

[<Tests>]
let tests =
    testList
        "Rondel"
        [ testList
              "getRondelOverview"
              [ testCase "returns None for unknown game"
                <| fun _ ->
                    let rondel, _, _ = createRondel ()
                    let gameId = Guid.NewGuid() |> Id

                    let result = rondel.GetRondelOverview { GameId = gameId }

                    Expect.isNone result "Should return None for unknown game"

                testCase "returns overview for initialized game"
                <| fun _ ->
                    let rondel, _, _ = createRondel ()
                    let gameId = Guid.NewGuid() |> Id
                    let nations = Set.ofList [ "France"; "Germany"; "Austria" ]

                    rondel.Execute <| SetToStartingPositions { GameId = gameId; Nations = nations }

                    let result = rondel.GetRondelOverview { GameId = gameId }

                    Expect.isSome result "Should return Some for initialized game"
                    let r = result.Value
                    Expect.equal r.GameId gameId "GameId should match"
                    Expect.equal (r.NationNames |> List.sort) [ "Austria"; "France"; "Germany" ] "Nations should match" ] ]
