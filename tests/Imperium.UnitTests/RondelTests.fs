module Imperium.UnitTests.Rondel

open System
open Expecto
open Imperium.Rondel
open Imperium.Contract.Rondel

[<Tests>]
let tests =
    testList "Rondel" [
        // Tests for public Rondel API will go here
        testList "setToStartingPositions" [
            testCase "given no positions are set when instructed to set the initial positions for zero nations then should fail" <| fun _ ->
                let publishedEvents = ResizeArray<RondelEvent>()            
                let publish event = publishedEvents.Add event
                let command = { GameId = Guid.NewGuid(); Nations = Set.empty }
                let result = setToStartingPositions publish command
                Expect.isError result "Expected error when setting starting positions with no nations"
            testCase "given no positions are set when instructed to set the initial positions for actual nations then should succeed and event PositionedAtStart is published" <| fun _ ->
                let publishedEvents = ResizeArray<RondelEvent>()            
                let publish event = publishedEvents.Add event
                let command = { GameId = Guid.NewGuid(); Nations = Set.ofList ["France"; "Germany"] }
                let result = setToStartingPositions publish command
                Expect.isOk result "Expected success when setting starting positions with nations"
                Expect.isNonEmpty publishedEvents "Events should be published in this simplified implementation"
                Expect.contains publishedEvents (PositionedAtStart { GameId = command.GameId }) "Expected PositionedAtStart event to be published"
        ]
    ] 