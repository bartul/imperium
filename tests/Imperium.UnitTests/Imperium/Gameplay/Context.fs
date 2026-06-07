namespace Imperium.UnitTests.Gameplay

open System.Collections.Generic
open Imperium.Gameplay

type Context =
    { Deps: GameplayDependencies
      Events: ResizeArray<GameplayEvent>
      Commands: ResizeArray<GameplayOutboundCommand>
      Store: Dictionary<GameId, GameplayState>
      GameId: GameId
      GetGameplayStatus: unit -> GameplayStatusView option }

module Context =
    let create gameId =
        let store = Dictionary<GameId, GameplayState>()
        let events = ResizeArray<GameplayEvent>()
        let commands = ResizeArray<GameplayOutboundCommand>()

        let load id =
            async {
                return
                    match store.TryGetValue(id) with
                    | true, state -> Some state
                    | false, _ -> None
            }

        let commit effects =
            async {
                effects.State |> Option.iter (fun s -> store[s.GameId] <- s)
                effects.IntegrationEvents |> List.iter events.Add
                effects.OutboundCommands |> List.iter commands.Add
            }

        let loadStatus id =
            async {
                let! state = load id
                return state |> Option.map GameplayStatusProjection.fromState
            }

        let queryDeps: GameplayQueryDependencies = { LoadStatus = loadStatus }

        let getGameplayStatusForGame () =
            Gameplay.getGameplayStatus queryDeps { GameId = gameId }
            |> Async.RunSynchronously

        { Deps = { Load = load; Commit = commit }
          Events = events
          Commands = commands
          Store = store
          GameId = gameId
          GetGameplayStatus = getGameplayStatusForGame }
