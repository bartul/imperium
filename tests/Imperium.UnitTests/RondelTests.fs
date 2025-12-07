module Imperium.UnitTests.Rondel

open System
open Expecto
open Imperium.Rondel
open Imperium.Contract.Rondel

// Test helpers for mock dependencies
let createMockStore () =
    let store = System.Collections.Generic.Dictionary<Guid, Dto.RondelState>()
    let load (gameId: Guid) : Dto.RondelState option =
        match store.TryGetValue(gameId) with
        | true, state -> Some state
        | false, _ -> None
    let save (state: Dto.RondelState) : Result<unit, string> =
        store.[state.GameId] <- state
        Ok ()
    load, save

let createMockPublisher () =
    let publishedEvents = ResizeArray<RondelEvent>()
    let publish event = publishedEvents.Add event
    publish, publishedEvents

[<Tests>]
let tests = 
    testList "Rondel" [
        // Tests for public Rondel API 
        testList "setToStartingPositions" [
            testCase "given no positions are set when instructed to set the initial positions for zero nations then should fail" <| fun _ ->
                let load, save = createMockStore ()
                let publish, _ = createMockPublisher ()
                let command = { GameId = Guid.NewGuid (); Nations = Set.empty }
                let result = setToStartingPositions load save publish command
                Expect.isError result "Expected error when setting starting positions with no nations"
            testCase "given no positions are set when instructed to set the initial positions for actual nations then should succeed and event PositionedAtStart is published" <| fun _ ->
                let load, save = createMockStore ()
                let publish, publishedEvents = createMockPublisher ()
                let command = { GameId = Guid.NewGuid (); Nations = Set.ofList ["France"; "Germany"] }
                let result = setToStartingPositions load save publish command
                Expect.isOk result "Expected success when setting starting positions with nations"
                Expect.isNonEmpty publishedEvents "Events should be published in this simplified implementation"
                Expect.contains publishedEvents (PositionedAtStart { GameId = command.GameId }) "Expected PositionedAtStart event to be published"
            testCase "given positions are already set when instructed to set the initial positions then no error is raised and no PositionedAtStart event is published again" <| fun _ ->
                let load, save = createMockStore ()
                let publish, publishedEvents = createMockPublisher ()
                let command = { GameId = Guid.NewGuid (); Nations = Set.ofList ["France"; "Germany"] }
                // First call to set positions
                let _ = setToStartingPositions load save publish command
                publishedEvents.Clear ()
                // Second call to set positions again
                let result = setToStartingPositions load save publish command
                Expect.isOk result "Expected no error when setting starting positions again"
                let positionedAtStartPublishedEvents = publishedEvents |> Seq.filter (function | PositionedAtStart _ -> true | _ -> false) |> Seq.toList
                Expect.equal (List.length positionedAtStartPublishedEvents) 0 "Expected no PositionedAtStart event to be published again"
        ]
    ] 