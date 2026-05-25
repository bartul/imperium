module Imperium.UnitTests.Rondel.Context

open System.Collections.Generic
open Imperium.Rondel
open Imperium.Primitives
open Imperium.Testing.Spec
open Imperium.UnitTests.Rondel.StateFormatting

// ────────────────────────────────────────────────────────────────────────────────
// Context
// ────────────────────────────────────────────────────────────────────────────────

type RondelContext =
    { Deps: RondelDependencies
      Events: ResizeArray<RondelEvent>
      Commands: ResizeArray<RondelOutboundCommand>
      Store: Dictionary<Id, RondelState>
      GameId: Id
      GetNationPositions: unit -> RondelPositionsView option
      GetRondelOverview: unit -> RondelView option }

let createContext gameId =
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

// ────────────────────────────────────────────────────────────────────────────────
// Runner
// ────────────────────────────────────────────────────────────────────────────────

let runner =
    { SpecRunner.empty with
        Execute = fun ctx cmd -> Rondel.execute ctx.Deps cmd |> Async.RunSynchronously
        Handle = fun ctx evt -> Rondel.handle ctx.Deps evt |> Async.RunSynchronously
        ClearEvents = fun ctx -> ctx.Events.Clear()
        ClearCommands = fun ctx -> ctx.Commands.Clear()
        SeedState = fun ctx state -> ctx.Store[ctx.GameId] <- state
        CaptureState =
            Some(fun ctx ->
                match ctx.Store.TryGetValue(ctx.GameId) with
                | true, state -> Some state
                | false, _ -> None)
        FormatState = Some StateFormatting.format }
