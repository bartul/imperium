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

let createMockCommandDispatcher () =
    let dispatchedCommands = ResizeArray<Imperium.Contract.Accounting.ChargeNationForRondelMovementCommand>()
    let chargeForMovement (command: Imperium.Contract.Accounting.ChargeNationForRondelMovementCommand) =
        dispatchedCommands.Add command
        Ok ()
    chargeForMovement, dispatchedCommands

let spaceToAction (space: string) : string =
    match space with
    | "Investor" -> "Investor"
    | "Factory" -> "Factory"
    | "Import" -> "Import"
    | "Taxation" -> "Taxation"
    | "ProductionOne" | "ProductionTwo" -> "Production"
    | "ManeuverOne" | "ManeuverTwo" -> "Maneuver"
    | _ -> failwith $"Unknown space: {space}"

[<Tests>]
let tests = 
    testList "Rondel" [
        // Tests for public Rondel API 
        testList "setToStartingPositions" [
            testCase "when instructed to set the starting positions for a game with an empty game id then should fail" <| fun _ ->
                let load, save = createMockStore ()
                let publish, _ = createMockPublisher ()
                let command = { GameId = Guid.Empty; Nations = [|"France"|] }
                let result = setToStartingPositions load save publish command
                Expect.isError result "Expected error when setting starting positions with empty game id"
            testCase "given starting positions are set when instructed to set the starting positions for zero nations then should fail" <| fun _ ->
                let load, save = createMockStore ()
                let publish, _ = createMockPublisher ()
                let command = { GameId = Guid.NewGuid (); Nations = [||] }
                let result = setToStartingPositions load save publish command
                Expect.isError result "Expected error when setting starting positions with no nations"
            testCase "when instructed to set the starting positions with duplicate nations then should succeed and duplicates are ignored" <| fun _ ->
                let load, save = createMockStore ()
                let publish, publishedEvents = createMockPublisher ()
                let command = { GameId = Guid.NewGuid (); Nations = [|"France"; "France"|] }
                let result = setToStartingPositions load save publish command
                Expect.isOk result "Expected success when setting starting positions with duplicate nations (duplicates ignored)"
                Expect.isNonEmpty publishedEvents "Events should be published"
                Expect.contains publishedEvents (PositionedAtStart { GameId = command.GameId }) "Expected PositionedAtStart event to be published"
            testCase "given starting positions are set when instructed to set the starting positions for actual nations then should succeed and event PositionedAtStart is published" <| fun _ ->
                let load, save = createMockStore ()
                let publish, publishedEvents = createMockPublisher ()
                let command = { GameId = Guid.NewGuid (); Nations = [|"France"; "Germany"|] }
                let result = setToStartingPositions load save publish command
                Expect.isOk result "Expected success when setting starting positions with nations"
                Expect.isNonEmpty publishedEvents "Events should be published in this simplified implementation"
                Expect.contains publishedEvents (PositionedAtStart { GameId = command.GameId }) "Expected PositionedAtStart event to be published"
            testCase "given starting positions are already set when instructed to set the starting positions then no error is raised and no PositionedAtStart event is published again" <| fun _ ->
                let load, save = createMockStore ()
                let publish, publishedEvents = createMockPublisher ()
                let command = { GameId = Guid.NewGuid (); Nations = [|"France"; "Germany"|] }
                // First call to set positions
                let _ = setToStartingPositions load save publish command
                publishedEvents.Clear ()
                // Second call to set positions again
                let result = setToStartingPositions load save publish command
                Expect.isOk result "Expected no error when setting starting positions again"
                let positionedAtStartPublishedEvents = publishedEvents |> Seq.filter (function | PositionedAtStart _ -> true | _ -> false) |> Seq.toList
                Expect.equal (List.length positionedAtStartPublishedEvents) 0 "Expected no PositionedAtStart event to be published again"
        ]
        testList "move" [
            testCase "given starting positions are not set when instructed to move a nation then should publish move rejected event and no charge command dispatched" <| fun _ ->
                let load, save = createMockStore ()
                let publish, publishedEvents = createMockPublisher ()
                let chargeForMovement, dispatchedCommands = createMockCommandDispatcher ()
                let moveCommand = { MoveCommand.GameId = Guid.NewGuid (); Nation = "France"; Space = "Factory" }
                let result = move load save publish chargeForMovement moveCommand
                Expect.isOk result "Expected success but with move rejected event published"
                Expect.isNonEmpty publishedEvents "Events should be published"
                Expect.contains publishedEvents (MoveToActionSpaceRejected { GameId = moveCommand.GameId; Nation = moveCommand.Nation; Space = moveCommand.Space }) "Expected MoveToActionSpaceRejected event to be published"
                Expect.isEmpty dispatchedCommands "No charge command should be dispatched when starting positions are not set"

            testPropertyWithConfig { FsCheckConfig.defaultConfig with maxTest = 15 } "given starting positions set when nation makes first move to any space then ActionDetermined published and no charge" <| fun (gameId: Guid) (nationIndex: int) (spaceIndex: int) ->
                let nations = [|"Austria"; "Britain"; "France"; "Germany"; "Italy"; "Russia"|]
                let allSpaces = ["Investor"; "Factory"; "Import"; "ManeuverOne"; "ProductionOne"; "ManeuverTwo"; "ProductionTwo"; "Taxation"]

                let nation = nations.[abs nationIndex % nations.Length]
                let space = allSpaces.[abs spaceIndex % allSpaces.Length]

                // Setup: initialize rondel
                let load, save = createMockStore()
                let publish, publishedEvents = createMockPublisher()
                let chargeForMovement, dispatchedCommands = createMockCommandDispatcher()

                let initCommand = { GameId = gameId; Nations = nations }
                setToStartingPositions load save publish initCommand |> ignore
                publishedEvents.Clear()

                // Execute: move one nation to target space
                let moveCommand = { MoveCommand.GameId = gameId; Nation = nation; Space = space }
                let result = move load save publish chargeForMovement moveCommand

                // Assert: move succeeds
                Expect.isOk result "First move to any space should succeed"

                // Assert: ActionDetermined event published with correct action
                let expectedAction = spaceToAction space
                Expect.contains publishedEvents (ActionDetermined { GameId = gameId; Nation = nation; Action = expectedAction }) (sprintf "ActionDetermined event should be published for nation %s moving to %s with action %s" nation space expectedAction)

                // Assert: No charge commands dispatched (first move is free)
                Expect.isEmpty dispatchedCommands "First move should be free - no charge commands dispatched"

            testCase "given starting positions set when instructed to move to invalid space then should return error and no events or commands published" <| fun _ ->
                let load, save = createMockStore ()
                let publish, publishedEvents = createMockPublisher ()
                let chargeForMovement, dispatchedCommands = createMockCommandDispatcher ()

                // Setup: initialize rondel with starting positions
                let gameId = Guid.NewGuid ()
                let initCommand = { GameId = gameId; Nations = [|"France"; "Germany"|] }
                setToStartingPositions load save publish initCommand |> ignore
                publishedEvents.Clear ()

                // Execute: attempt to move to an invalid space
                let moveCommand = { MoveCommand.GameId = gameId; Nation = "France"; Space = "InvalidSpace" }
                let result = move load save publish chargeForMovement moveCommand

                // Assert: operation should fail
                Expect.isError result "Expected error when moving to invalid space"

                // Assert: No events published
                Expect.isEmpty publishedEvents "No events should be published when space is invalid"

                // Assert: No charge commands dispatched
                Expect.isEmpty dispatchedCommands "No charge commands should be dispatched when space is invalid"

            testCase "when instructed to move with empty game id then should return error and no events or commands published" <| fun _ ->
                let load, save = createMockStore ()
                let publish, publishedEvents = createMockPublisher ()
                let chargeForMovement, dispatchedCommands = createMockCommandDispatcher ()

                // Execute: attempt to move with empty game id
                let moveCommand = { MoveCommand.GameId = Guid.Empty; Nation = "France"; Space = "Factory" }
                let result = move load save publish chargeForMovement moveCommand

                // Assert: operation should fail
                Expect.isError result "Expected error when moving with empty game id"

                // Assert: No events published
                Expect.isEmpty publishedEvents "No events should be published when game id is empty"

                // Assert: No charge commands dispatched
                Expect.isEmpty dispatchedCommands "No charge commands should be dispatched when game id is empty"
        ]
    ]
