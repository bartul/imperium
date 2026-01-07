module Imperium.UnitTests.RondelContract

open System
open Expecto
open Imperium.Rondel
open Imperium.Primitives

module ContractRondel = Imperium.Contract.Rondel

[<Tests>]
let tests =
    testList
        "Rondel.Contract"
        [ testList
              "SetToStartingPositionsCommand.fromContract"
              [ testCase "requires a game id"
                <| fun _ ->
                    let contractCommand : ContractRondel.SetToStartingPositionsCommand =
                        { GameId = Guid.Empty
                          Nations = [| "France" |] }

                    // Transformation should fail with Guid.Empty
                    let transformResult = SetToStartingPositionsCommand.fromContract contractCommand
                    Expect.isError transformResult "starting positions cannot be chosen without a game id"
                testCase "requires at least one nation"
                <| fun _ ->
                    let contractCommand : ContractRondel.SetToStartingPositionsCommand =
                        { GameId = Guid.NewGuid()
                          Nations = [||] }

                    // Transformation should reject empty roster
                    let transformResult = SetToStartingPositionsCommand.fromContract contractCommand
                    Expect.isError transformResult "starting positions require at least one nation" ]
          testList
              "MoveCommand.fromContract"
              [ testCase "rejects an unknown rondel space"
                <| fun _ ->
                    // Execute: attempt to move to an invalid space
                    let contractMoveCommand : ContractRondel.MoveCommand =
                        { GameId = Guid.NewGuid()
                          Nation = "France"
                          Space = "InvalidSpace" }

                    // Transformation should fail for unknown space
                    let transformResult = MoveCommand.fromContract contractMoveCommand
                    Expect.isError transformResult "unknown rondel space is not allowed"

                testCase "requires a game id"
                <| fun _ ->
                    // Execute: attempt to move with empty game id
                    let contractMoveCommand : ContractRondel.MoveCommand =
                        { GameId = Guid.Empty
                          Nation = "France"
                          Space = "Factory" }

                    // Transformation should fail for invalid GameId
                    let transformResult = MoveCommand.fromContract contractMoveCommand
                    Expect.isError transformResult "a move cannot be taken without a game id" ] ]
