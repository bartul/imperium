namespace Imperium.UnitTests.Rondel

open System.Collections.Generic
open Imperium.Rondel
open Imperium.Primitives

type Context =
    { Deps: RondelDependencies
      Events: ResizeArray<RondelEvent>
      Commands: ResizeArray<RondelOutboundCommand>
      Store: Dictionary<Id, RondelState>
      GameId: Id
      GetNationPositions: unit -> RondelPositionsView option
      GetRondelOverview: unit -> RondelView option }

module Context =
    let create gameId =
        let store = Dictionary<Id, RondelState>()
        let events = ResizeArray<RondelEvent>()
        let commands = ResizeArray<RondelOutboundCommand>()

        let load id =
            async {
                return
                    match store.TryGetValue(id) with
                    | true, state -> Some state
                    | false, _ -> None
            }

        let commit (effects: RondelEffects) =
            async {
                effects.State |> Option.iter (fun s -> store[s.GameId] <- s)
                effects.IntegrationEvents |> List.iter events.Add
                effects.OutboundCommands |> List.iter commands.Add
            }

        let queryDeps: RondelQueryDependencies = { Load = load }

        let getNationPositionsForGame () =
            Rondel.getNationPositions queryDeps { GameId = gameId }
            |> Async.RunSynchronously

        let getRondelOverviewForGame () =
            Rondel.getRondelOverview queryDeps { GameId = gameId } |> Async.RunSynchronously

        { Deps = { Load = load; Commit = commit }
          Events = events
          Commands = commands
          Store = store
          GameId = gameId
          GetNationPositions = getNationPositionsForGame
          GetRondelOverview = getRondelOverviewForGame }
