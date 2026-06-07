module Imperium.UnitTests.Gameplay.QuerySpecification

open System
open Expecto
open Imperium.Gameplay
open Imperium.Testing.Spec
open Imperium.Testing.Spec.Specification
open Imperium.UnitTests.Gameplay.Assertions

// ----------------------------------------------------------------------------
// Specs
// ----------------------------------------------------------------------------

let private specifications =
    let gameId = GameId.newId ()

    let players ids =
        match PlayerRoster.create ids with
        | Ok roster -> roster
        | Error e -> failwith e

    let spec = specOn (fun () -> Context.create gameId)

    [ spec "query gameplay status returns none for unknown game" {
          expect "no gameplay status is returned" assertNoGameplayStatus
      }

      spec "query gameplay status returns setup status after a game starts" {
          given_command (StartGame { GameId = gameId; Players = players (List.init 3 (fun _ -> Guid.NewGuid())) })

          expect "gameplay status is returned" assertGameplayStatus
          expect "query result belongs to current game" (assertGameplayStatusForGameId gameId)
          expect "game is not in play yet" (assertGameplayInPlay false)
          expect "player count is returned" (assertGameplayPlayerCount 3)
      }

      spec "query gameplay status returns in-play status after rondel setup completes" {
          given_command (StartGame { GameId = gameId; Players = players (List.init 4 (fun _ -> Guid.NewGuid())) })

          given_event (RondelPositionedAtStart { GameId = gameId })

          expect "gameplay status is returned" assertGameplayStatus
          expect "query result belongs to current game" (assertGameplayStatusForGameId gameId)
          expect "game is in play" (assertGameplayInPlay true)
          expect "player count is returned" (assertGameplayPlayerCount 4)
      } ]

let markdown options filter rootPath =
    specifications
    |> SpecFilter.apply filter (rootPath @ [ "Gameplay"; "Queries" ])
    |> Markdown.render options "Gameplay Queries" Specification.runner

// ----------------------------------------------------------------------------
// Test Registration
// ----------------------------------------------------------------------------

[<Tests>]
let all =
    testList "Gameplay Queries" (specifications |> List.map (SpecRunner.toExpectoTestList Specification.runner))
