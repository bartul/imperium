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
    | _ -> failwith $"Unknown rondel space: {space}"

[<Tests>]
let tests = 
    testList "Rondel" [
        // Tests for public Rondel API 
        testList "starting positions" [
            testCase "starting positions: requires a game id" <| fun _ ->
                let load, save = createMockStore ()
                let publish, _ = createMockPublisher ()
                let command = { GameId = Guid.Empty; Nations = [|"France"|] }
                let result = setToStartingPositions load save publish command
                Expect.isError result "starting positions cannot be chosen without a game id"
            testCase "starting positions: requires at least one nation" <| fun _ ->
                let load, save = createMockStore ()
                let publish, _ = createMockPublisher ()
                let command = { GameId = Guid.NewGuid (); Nations = [||] }
                let result = setToStartingPositions load save publish command
                Expect.isError result "starting positions require at least one nation"
            testCase "starting positions: ignores duplicate nations" <| fun _ ->
                let load, save = createMockStore ()
                let publish, publishedEvents = createMockPublisher ()
                let command = { GameId = Guid.NewGuid (); Nations = [|"France"; "France"|] }
                let result = setToStartingPositions load save publish command
                Expect.isOk result "starting positions accept the roster (duplicates ignored)"
                Expect.isNonEmpty publishedEvents "the rondel should signal that starting positions are set"
                Expect.contains publishedEvents (PositionedAtStart { GameId = command.GameId }) "the rondel should signal that starting positions are set"
            testCase "starting positions: signals setup for the roster" <| fun _ ->
                let load, save = createMockStore ()
                let publish, publishedEvents = createMockPublisher ()
                let command = { GameId = Guid.NewGuid (); Nations = [|"France"; "Germany"|] }
                let result = setToStartingPositions load save publish command
                Expect.isOk result "starting positions should be accepted"
                Expect.isNonEmpty publishedEvents "the rondel should signal that starting positions are set"
                Expect.contains publishedEvents (PositionedAtStart { GameId = command.GameId }) "the rondel should signal that starting positions are set"
            testCase "starting positions: setting twice does not signal again" <| fun _ ->
                let load, save = createMockStore ()
                let publish, publishedEvents = createMockPublisher ()
                let command = { GameId = Guid.NewGuid (); Nations = [|"France"; "Germany"|] }
                // First call to set positions
                let _ = setToStartingPositions load save publish command
                publishedEvents.Clear ()
                // Second call to set positions again
                let result = setToStartingPositions load save publish command
                Expect.isOk result "setting starting positions twice should not fail"
                let positionedAtStartPublishedEvents = publishedEvents |> Seq.filter (function | PositionedAtStart _ -> true | _ -> false) |> Seq.toList
                Expect.equal (List.length positionedAtStartPublishedEvents) 0 "setting starting positions twice should not signal starting positions again"
        ]
        testList "move" [
            testCase "move: cannot begin before starting positions are chosen" <| fun _ ->
                let load, save = createMockStore ()
                let publish, publishedEvents = createMockPublisher ()
                let chargeForMovement, dispatchedCommands = createMockCommandDispatcher ()
                let moveCommand = { MoveCommand.GameId = Guid.NewGuid (); Nation = "France"; Space = "Factory" }
                let result = move load save publish chargeForMovement moveCommand
                Expect.isOk result "the move should be denied without breaking the game flow"
                Expect.isNonEmpty publishedEvents "the rondel should signal why the move was denied"
                Expect.contains publishedEvents (MoveToActionSpaceRejected { GameId = moveCommand.GameId; Nation = moveCommand.Nation; Space = moveCommand.Space }) "the rondel should signal that the move was denied"
                Expect.isEmpty dispatchedCommands "no movement fee is due when the move is denied"

            testPropertyWithConfig { FsCheckConfig.defaultConfig with maxTest = 15 } "move: nation’s first move may choose any rondel space (free)" <| fun (gameId: Guid) (nationIndex: int) (spaceIndex: int) ->
            
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
                Expect.isOk result "first move should allow choosing any rondel space"

                // Assert: ActionDetermined event published with correct action
                let expectedAction = spaceToAction space
                Expect.contains publishedEvents (ActionDetermined { GameId = gameId; Nation = nation; Action = expectedAction }) (sprintf "the rondel space %s determines %s’s action: %s" space nation expectedAction)

                // Assert: No charge commands dispatched (first move is free)
                Expect.isEmpty dispatchedCommands "first move is free (no movement fee)"

            testCase "move: rejects an unknown rondel space" <| fun _ ->
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
                Expect.isError result "unknown rondel space is not allowed"

                // Assert: No events published
                Expect.isEmpty publishedEvents "an unknown space should not trigger any rondel signal"

                // Assert: No charge commands dispatched
                Expect.isEmpty dispatchedCommands "no movement fee is due for an invalid move"

            testCase "move: requires a game id" <| fun _ ->
                let load, save = createMockStore ()
                let publish, publishedEvents = createMockPublisher ()
                let chargeForMovement, dispatchedCommands = createMockCommandDispatcher ()

                // Execute: attempt to move with empty game id
                let moveCommand = { MoveCommand.GameId = Guid.Empty; Nation = "France"; Space = "Factory" }
                let result = move load save publish chargeForMovement moveCommand

                // Assert: operation should fail
                Expect.isError result "a move cannot be taken without a game id"

                // Assert: No events published
                Expect.isEmpty publishedEvents "without a game id, the rondel should not signal anything"

                // Assert: No charge commands dispatched
                Expect.isEmpty dispatchedCommands "no movement fee is due without a game id"

            testCase "move: rejects move to nation's current position" <| fun _ ->
                let load, save = createMockStore ()
                let publish, publishedEvents = createMockPublisher ()
                let chargeForMovement, dispatchedCommands = createMockCommandDispatcher ()

                // Setup: initialize rondel with starting positions
                let gameId = Guid.NewGuid ()
                let initCommand = { GameId = gameId; Nations = [|"France"|] }
                setToStartingPositions load save publish initCommand |> ignore
                publishedEvents.Clear ()

                // Execute: move France to Factory
                let moveCommand = { MoveCommand.GameId = gameId; Nation = "France"; Space = "Factory" }
                let firstMoveResult = move load save publish chargeForMovement moveCommand
                Expect.isOk firstMoveResult "first move to Factory should succeed"
                Expect.contains publishedEvents (ActionDetermined { GameId = gameId; Nation = "France"; Action = "Factory" }) "the rondel should determine Factory action for France"
                publishedEvents.Clear ()
                dispatchedCommands.Clear ()

                // Execute: attempt to move France to Factory again (same position)
                let secondMoveResult = move load save publish chargeForMovement moveCommand

                // Assert: move should be rejected
                Expect.isOk secondMoveResult "the move should be denied without breaking the game flow"
                Expect.isNonEmpty publishedEvents "the rondel should signal why the move was denied"
                Expect.contains publishedEvents (MoveToActionSpaceRejected { GameId = gameId; Nation = "France"; Space = "Factory" }) "the rondel should signal that the move was rejected"

                // Assert: No action determined for rejected move
                Expect.isFalse (publishedEvents |> Seq.contains (ActionDetermined { GameId = gameId; Nation = "France"; Action = "Factory" })) "no action should be determined when move to current position is rejected"

                // Assert: No charge commands dispatched
                Expect.isEmpty dispatchedCommands "no movement fee is due when moving to current position"
        ]
    ]
