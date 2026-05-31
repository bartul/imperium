module Imperium.UnitTests.GameplayContractTests

open System
open Expecto
open Imperium.Gameplay

module ContractGameplay = Imperium.Contract.Gameplay

[<Tests>]
let tests =
    testList
        "Gameplay.Contract"
        [ testList
              "StartGameCommand.fromContract"
              [ testCase "requires a valid game id"
                <| fun _ ->
                    let contractCommand: ContractGameplay.StartGameCommand =
                        { GameId = Guid.Empty
                          Nations = [| "Germany"; "France" |]
                          PlayerIds = [| Guid.NewGuid(); Guid.NewGuid() |] }

                    let result = StartGameCommand.fromContract contractCommand
                    Expect.isError result "start game command cannot have empty GameId" ] ]
