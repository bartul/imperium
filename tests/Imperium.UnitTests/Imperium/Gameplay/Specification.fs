module Imperium.UnitTests.Gameplay.Specification

open System
open Expecto
open Imperium.Gameplay
open Imperium.Testing.Spec
open Imperium.Testing.Spec.Specification
open Imperium.UnitTests.Gameplay.Assertions

// ────────────────────────────────────────────────────────────────────────────────
// Runner
// ────────────────────────────────────────────────────────────────────────────────

let private runner =
    { SpecRunner.empty with
        Execute = fun ctx cmd -> Gameplay.execute ctx.Deps cmd |> Async.RunSynchronously
        Handle = fun ctx evt -> Gameplay.handle ctx.Deps evt |> Async.RunSynchronously
        ClearEvents = fun ctx -> ctx.Events.Clear()
        ClearCommands = fun ctx -> ctx.Commands.Clear()
        SeedState = fun ctx state -> ctx.Store[ctx.GameId] <- state
        CaptureState =
            Some(fun ctx ->
                match ctx.Store.TryGetValue(ctx.GameId) with
                | true, state -> Some state
                | false, _ -> None) }

// ────────────────────────────────────────────────────────────────────────────────
// Specs
// ────────────────────────────────────────────────────────────────────────────────

let private specifications =
    let gameId = GameId.newId ()

    let players ids =
        match PlayerRoster.create ids with
        | Ok roster -> roster
        | Error e -> failwith e

    let spec = specOn (fun () -> Context.create gameId)

    [ spec "starting a new game asks the rondel to set its starting positions" {
          when_command (StartGame { GameId = gameId; Players = players [ Guid.NewGuid(); Guid.NewGuid() ] })

          expect
              "the rondel is asked to set its starting positions"
              (assertRondelAskedToSetStartingPositions gameId NationId.all)

          expect "no game events are published yet" assertNoEvents
      }

      spec "rondel confirming starting positions completes setup" {
          given_command (StartGame { GameId = gameId; Players = players [ Guid.NewGuid(); Guid.NewGuid() ] })

          when_event (RondelPositionedAtStart { GameId = gameId })

          expect "the gameplay announces that setup is complete" (assertSetupCompleted gameId)
          expect "no outbound commands are emitted" assertNoOutboundCommands
      }

      spec "rondel confirming starting positions again after setup is complete is ignored" {
          given_command (StartGame { GameId = gameId; Players = players [ Guid.NewGuid(); Guid.NewGuid() ] })
          given_event (RondelPositionedAtStart { GameId = gameId })

          when_event (RondelPositionedAtStart { GameId = gameId })

          expect "no game events are published" assertNoEvents
          expect "no outbound commands are emitted" assertNoOutboundCommands
      }

      spec "rondel confirming starting positions for an unknown game is ignored" {
          when_event (RondelPositionedAtStart { GameId = gameId })

          expect "no game events are published" assertNoEvents
          expect "no outbound commands are emitted" assertNoOutboundCommands
      }

      spec "starting an already-started game is ignored" {
          given_command (StartGame { GameId = gameId; Players = players [ Guid.NewGuid(); Guid.NewGuid() ] })

          when_command (StartGame { GameId = gameId; Players = players [ Guid.NewGuid(); Guid.NewGuid() ] })

          expect "the rondel is not asked again" assertNoOutboundCommands
          expect "no game events are published" assertNoEvents
      } ]

let markdown options filter rootPath =
    specifications
    |> SpecFilter.apply filter (rootPath @ [ "Gameplay" ])
    |> Markdown.render options "Gameplay" runner

// ────────────────────────────────────────────────────────────────────────────────
// Test Registration
// ────────────────────────────────────────────────────────────────────────────────

[<Tests>]
let all =
    testList "Gameplay" (specifications |> List.map (SpecRunner.toExpectoTestList runner))
