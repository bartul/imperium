module Imperium.UnitTests.GameplayContractTests

open System
open Expecto
open Imperium.Gameplay

module ContractGameplay = Imperium.Contract.Gameplay
module ContractRondel = Imperium.Contract.Rondel

[<Tests>]
let tests =
    testList
        "Gameplay.Contract"
        [ testList
              "StartGameCommand.fromContract"
              [ testCase "requires a valid game id"
                <| fun _ ->
                    let contractCommand: ContractGameplay.StartGameCommand =
                        { GameId = Guid.Empty; PlayerIds = [| Guid.NewGuid(); Guid.NewGuid() |] }

                    let result = StartGameCommand.fromContract contractCommand
                    Expect.isError result "start game command cannot have empty GameId"

                testCase "rejects an empty player id"
                <| fun _ ->
                    let contractCommand: ContractGameplay.StartGameCommand =
                        { GameId = Guid.NewGuid(); PlayerIds = [| Guid.NewGuid(); Guid.Empty |] }

                    let result = StartGameCommand.fromContract contractCommand
                    Expect.isError result "empty player id should be rejected"

                testCase "accepts a valid command and maps GameId and Players"
                <| fun _ ->
                    let gameId = Guid.NewGuid()
                    let playerIds = [| Guid.NewGuid(); Guid.NewGuid(); Guid.NewGuid() |]

                    let contractCommand: ContractGameplay.StartGameCommand =
                        { GameId = gameId; PlayerIds = playerIds }

                    let domain =
                        Expect.wantOk
                            (StartGameCommand.fromContract contractCommand)
                            "valid command should transform successfully"

                    Expect.equal (GameId.value domain.GameId) gameId "GameId should round-trip"

                    let rosterGuids = PlayerRoster.value domain.Players |> Set.map PlayerId.value
                    Expect.equal rosterGuids (Set.ofArray playerIds) "players should contain exactly the provided ids" ]

          testList
              "RondelPositionedAtStartInboundEvent.fromContract"
              [ testCase "requires a valid game id"
                <| fun _ ->
                    let contractEvent: ContractRondel.PositionedAtStart = { GameId = Guid.Empty }

                    let result = RondelPositionedAtStartInboundEvent.fromContract contractEvent
                    Expect.isError result "inbound event cannot have empty GameId"

                testCase "accepts a valid event and round-trips the GameId"
                <| fun _ ->
                    let gameId = Guid.NewGuid()
                    let contractEvent: ContractRondel.PositionedAtStart = { GameId = gameId }

                    let domain =
                        Expect.wantOk
                            (RondelPositionedAtStartInboundEvent.fromContract contractEvent)
                            "valid event should transform successfully"

                    Expect.equal (GameId.value domain.GameId) gameId "GameId should round-trip" ] ]
