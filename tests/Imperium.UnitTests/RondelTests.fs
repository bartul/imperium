module Imperium.UnitTests.Rondel

open System
open Expecto
open Imperium.Rondel
open Imperium.Contract.Rondel

// Test helpers for mock dependencies
let createMockStore () =
    let store = System.Collections.Generic.Dictionary<Guid, Imperium.Contract.Rondel.RondelState>()

    let load (gameId: Guid) : Imperium.Contract.Rondel.RondelState option =
        match store.TryGetValue(gameId) with
        | true, state -> Some state
        | false, _ -> None

    let save (state: Imperium.Contract.Rondel.RondelState) : Result<unit, string> =
        store.[state.GameId] <- state
        Ok()

    load, save

let createMockPublisher () =
    let publishedEvents = ResizeArray<Imperium.Rondel.RondelEvent>()
    let publish event = publishedEvents.Add event
    publish, publishedEvents

let createMockCommandDispatcher () =
    let dispatchedCommands =
        ResizeArray<Imperium.Contract.Accounting.ChargeNationForRondelMovementCommand>()

    let chargeForMovement (command: Imperium.Contract.Accounting.ChargeNationForRondelMovementCommand) =
        dispatchedCommands.Add command
        Ok()

    chargeForMovement, dispatchedCommands

let createMockVoidCharge () =
    let voidedCommands =
        ResizeArray<Imperium.Contract.Accounting.VoidRondelChargeCommand>()

    let voidCharge (command: Imperium.Contract.Accounting.VoidRondelChargeCommand) =
        voidedCommands.Add command
        Ok()

    voidCharge, voidedCommands

// Independent reference implementation for test verification
// This provides an alternate path to verify Space -> Action mapping
// without using the production Space.toAction function
let spaceNameToExpectedAction (spaceName: string) : Imperium.Rondel.Action =
    match spaceName with
    | "Investor" -> Imperium.Rondel.Action.Investor
    | "Factory" -> Imperium.Rondel.Action.Factory
    | "Import" -> Imperium.Rondel.Action.Import
    | "Taxation" -> Imperium.Rondel.Action.Taxation
    | "ProductionOne"
    | "ProductionTwo" -> Imperium.Rondel.Action.Production
    | "ManeuverOne"
    | "ManeuverTwo" -> Imperium.Rondel.Action.Maneuver
    | _ -> failwith $"Unknown rondel space: {spaceName}"

[<Tests>]
let tests =
    testList
        "Rondel"
        [
          // Tests for public Rondel API
          testList
              "starting positions"
              [ testCase "requires a game id"
                <| fun _ ->
                    let contractCommand =
                        { GameId = Guid.Empty
                          Nations = [| "France" |] }

                    // Transformation should fail with Guid.Empty
                    let transformResult = SetToStartingPositionsCommand.toDomain contractCommand
                    Expect.isError transformResult "starting positions cannot be chosen without a game id"
                testCase "requires at least one nation"
                <| fun _ ->
                    let contractCommand =
                        { GameId = Guid.NewGuid()
                          Nations = [||] }

                    // Transformation should reject empty roster
                    let transformResult = SetToStartingPositionsCommand.toDomain contractCommand
                    Expect.isError transformResult "starting positions require at least one nation"
                testCase "ignores duplicate nations"
                <| fun _ ->
                    let load, save = createMockStore ()
                    let publish, publishedEvents = createMockPublisher ()

                    let contractCommand =
                        { GameId = Guid.NewGuid()
                          Nations = [| "France"; "France" |] }

                    // Transformation should succeed (Set automatically deduplicates)
                    let transformResult = SetToStartingPositionsCommand.toDomain contractCommand
                    Expect.isOk transformResult "starting positions accept the roster (duplicates ignored)"

                    let domainCommand = Result.defaultWith failwith transformResult

                    // Handler succeeds (Set has 1 nation, not empty)
                    setToStartingPositions load save publish domainCommand
                    Expect.isNonEmpty publishedEvents "the rondel should signal that starting positions are set"

                    Expect.contains
                        publishedEvents
                        (Imperium.Rondel.RondelEvent.PositionedAtStart { GameId = domainCommand.GameId })
                        "the rondel should signal that starting positions are set"
                testCase "signals setup for the roster"
                <| fun _ ->
                    let load, save = createMockStore ()
                    let publish, publishedEvents = createMockPublisher ()

                    let contractCommand =
                        { GameId = Guid.NewGuid()
                          Nations = [| "France"; "Germany" |] }

                    // Transformation should succeed
                    let transformResult = SetToStartingPositionsCommand.toDomain contractCommand
                    Expect.isOk transformResult "starting positions should be accepted"

                    let domainCommand = Result.defaultWith failwith transformResult

                    // Handler succeeds
                    setToStartingPositions load save publish domainCommand
                    Expect.isNonEmpty publishedEvents "the rondel should signal that starting positions are set"

                    Expect.contains
                        publishedEvents
                        (Imperium.Rondel.RondelEvent.PositionedAtStart { GameId = domainCommand.GameId })
                        "the rondel should signal that starting positions are set"
                testCase "setting twice does not signal again"
                <| fun _ ->
                    let load, save = createMockStore ()
                    let publish, publishedEvents = createMockPublisher ()

                    let contractCommand =
                        { GameId = Guid.NewGuid()
                          Nations = [| "France"; "Germany" |] }

                    // Transformation should succeed
                    let transformResult = SetToStartingPositionsCommand.toDomain contractCommand
                    Expect.isOk transformResult "starting positions should be accepted"

                    let domainCommand = Result.defaultWith failwith transformResult

                    // First call to set positions
                    setToStartingPositions load save publish domainCommand
                    publishedEvents.Clear()
                    // Second call to set positions again
                    setToStartingPositions load save publish domainCommand

                    let positionedAtStartPublishedEvents =
                        publishedEvents
                        |> Seq.filter (function
                            | Imperium.Rondel.RondelEvent.PositionedAtStart _ -> true
                            | _ -> false)
                        |> Seq.toList

                    Expect.equal
                        (List.length positionedAtStartPublishedEvents)
                        0
                        "setting starting positions twice should not signal starting positions again" ]
          testList
              "move"
              [ testCase "cannot begin before starting positions are chosen"
                <| fun _ ->
                    let load, save = createMockStore ()
                    let publish, publishedEvents = createMockPublisher ()
                    let chargeForMovement, dispatchedCommands = createMockCommandDispatcher ()
                    let voidCharge, voidedCommands = createMockVoidCharge ()

                    let contractMoveCommand =
                        { Imperium.Contract.Rondel.MoveCommand.GameId = Guid.NewGuid()
                          Nation = "France"
                          Space = "Factory" }

                    // Transform Contract → Domain
                    let transformResult = MoveCommand.toDomain contractMoveCommand
                    Expect.isOk transformResult "transformation should succeed with valid inputs"

                    let domainMoveCommand = Result.defaultWith failwith transformResult

                    // Handler returns unit
                    move load save publish chargeForMovement voidCharge domainMoveCommand
                    Expect.isNonEmpty publishedEvents "the rondel should signal why the move was denied"

                    Expect.contains
                        publishedEvents
                        (Imperium.Rondel.RondelEvent.MoveToActionSpaceRejected
                            { GameId = domainMoveCommand.GameId
                              Nation = domainMoveCommand.Nation
                              Space = domainMoveCommand.Space })
                        "the rondel should signal that the move was denied"

                    Expect.isEmpty dispatchedCommands "no movement fee is due when the move is denied"

                testPropertyWithConfig
                    { FsCheckConfig.defaultConfig with
                        maxTest = 15 }
                    "nation's first move may choose any rondel space (free)"
                <| fun (gameId: Guid) (nationIndex: int) (spaceIndex: int) ->

                    let nations = [| "Austria"; "Britain"; "France"; "Germany"; "Italy"; "Russia" |]

                    let allSpaces =
                        [ "Investor"
                          "Factory"
                          "Import"
                          "ManeuverOne"
                          "ProductionOne"
                          "ManeuverTwo"
                          "ProductionTwo"
                          "Taxation" ]

                    let nation = nations.[abs nationIndex % nations.Length]
                    let space = allSpaces.[abs spaceIndex % allSpaces.Length]

                    // Setup: initialize rondel
                    let load, save = createMockStore ()
                    let publish, publishedEvents = createMockPublisher ()
                    let chargeForMovement, dispatchedCommands = createMockCommandDispatcher ()
                    let voidCharge, voidedCommands = createMockVoidCharge ()

                    let contractInitCommand = { GameId = gameId; Nations = nations }

                    let domainInitCommand =
                        SetToStartingPositionsCommand.toDomain contractInitCommand
                        |> Result.defaultWith failwith

                    setToStartingPositions load save publish domainInitCommand
                    publishedEvents.Clear()

                    // Execute: move one nation to target space
                    let contractMoveCommand =
                        { Imperium.Contract.Rondel.MoveCommand.GameId = gameId
                          Nation = nation
                          Space = space }

                    let transformResult = MoveCommand.toDomain contractMoveCommand
                    Expect.isOk transformResult "first move should allow choosing any rondel space"

                    let domainMoveCommand = Result.defaultWith failwith transformResult

                    move load save publish chargeForMovement voidCharge domainMoveCommand

                    // Assert: ActionDetermined event published with correct action
                    // Using independent reference implementation to avoid testing transformation with itself
                    let expectedAction = spaceNameToExpectedAction space

                    Expect.contains
                        publishedEvents
                        (Imperium.Rondel.RondelEvent.ActionDetermined
                            { GameId = domainMoveCommand.GameId
                              Nation = nation
                              Action = expectedAction })
                        (sprintf "the rondel space %s determines %s's action: %A" space nation expectedAction)

                    // Assert: No charge commands dispatched (first move is free)
                    Expect.isEmpty dispatchedCommands "first move is free (no movement fee)"

                testCase "rejects an unknown rondel space"
                <| fun _ ->
                    // Execute: attempt to move to an invalid space
                    let contractMoveCommand =
                        { Imperium.Contract.Rondel.MoveCommand.GameId = Guid.NewGuid()
                          Nation = "France"
                          Space = "InvalidSpace" }

                    // Transformation should fail for unknown space
                    let transformResult = MoveCommand.toDomain contractMoveCommand
                    Expect.isError transformResult "unknown rondel space is not allowed"

                testCase "requires a game id"
                <| fun _ ->
                    // Execute: attempt to move with empty game id
                    let contractMoveCommand =
                        { Imperium.Contract.Rondel.MoveCommand.GameId = Guid.Empty
                          Nation = "France"
                          Space = "Factory" }

                    // Transformation should fail for invalid GameId
                    let transformResult = MoveCommand.toDomain contractMoveCommand
                    Expect.isError transformResult "a move cannot be taken without a game id"

                testPropertyWithConfig
                    { FsCheckConfig.defaultConfig with
                        maxTest = 15 }
                    "rejects move to nation's current position repeatedly"
                <| fun (gameId: Guid) (nationIndex: int) (spaceIndex: int) ->

                    let nations = [| "Austria"; "Britain"; "France"; "Germany"; "Italy"; "Russia" |]

                    let allSpaces =
                        [ "Investor"
                          "Factory"
                          "Import"
                          "ManeuverOne"
                          "ProductionOne"
                          "ManeuverTwo"
                          "ProductionTwo"
                          "Taxation" ]

                    let nation = nations.[abs nationIndex % nations.Length]
                    let space = allSpaces.[abs spaceIndex % allSpaces.Length]

                    // Setup: initialize rondel
                    let load, save = createMockStore ()
                    let publish, publishedEvents = createMockPublisher ()
                    let chargeForMovement, dispatchedCommands = createMockCommandDispatcher ()
                    let voidCharge, voidedCommands = createMockVoidCharge ()

                    let contractInitCommand = { GameId = gameId; Nations = nations }

                    let domainInitCommand =
                        SetToStartingPositionsCommand.toDomain contractInitCommand
                        |> Result.defaultWith failwith

                    setToStartingPositions load save publish domainInitCommand
                    publishedEvents.Clear()

                    // Execute: move nation to target space (first move)
                    let contractMoveCommand =
                        { Imperium.Contract.Rondel.MoveCommand.GameId = gameId
                          Nation = nation
                          Space = space }

                    let transformResult = MoveCommand.toDomain contractMoveCommand
                    Expect.isOk transformResult "first move should succeed"

                    let domainMoveCommand = Result.defaultWith failwith transformResult

                    move load save publish chargeForMovement voidCharge domainMoveCommand

                    // Assert: first move succeeds
                    // Using independent reference implementation to avoid testing transformation with itself
                    let expectedAction = spaceNameToExpectedAction space

                    Expect.contains
                        publishedEvents
                        (Imperium.Rondel.RondelEvent.ActionDetermined
                            { GameId = domainMoveCommand.GameId
                              Nation = nation
                              Action = expectedAction })
                        (sprintf "the rondel space %s determines %s's action: %A" space nation expectedAction)

                    publishedEvents.Clear()
                    dispatchedCommands.Clear()

                    // Execute: attempt to move to same position (first rejection)
                    move load save publish chargeForMovement voidCharge domainMoveCommand

                    // Assert: second move is rejected
                    Expect.isNonEmpty publishedEvents "the rondel should signal why the move was denied"

                    Expect.contains
                        publishedEvents
                        (Imperium.Rondel.RondelEvent.MoveToActionSpaceRejected
                            { GameId = domainMoveCommand.GameId
                              Nation = nation
                              Space = domainMoveCommand.Space })
                        (sprintf "%s's move to current position %s should be rejected" nation space)

                    Expect.isFalse
                        (publishedEvents
                         |> Seq.contains (
                             Imperium.Rondel.RondelEvent.ActionDetermined
                                 { GameId = domainMoveCommand.GameId
                                   Nation = nation
                                   Action = expectedAction }
                         ))
                        "no action should be determined when move to current position is rejected"

                    Expect.isEmpty dispatchedCommands "no movement fee is due when moving to current position"
                    publishedEvents.Clear()
                    dispatchedCommands.Clear()

                    // Execute: attempt to move to same position again (second rejection)
                    move load save publish chargeForMovement voidCharge domainMoveCommand

                    // Assert: third move is also rejected
                    Expect.isNonEmpty publishedEvents "the rondel should signal why the move was denied"

                    Expect.contains
                        publishedEvents
                        (Imperium.Rondel.RondelEvent.MoveToActionSpaceRejected
                            { GameId = domainMoveCommand.GameId
                              Nation = nation
                              Space = domainMoveCommand.Space })
                        (sprintf "%s's repeated move to current position %s should be rejected" nation space)

                    Expect.isFalse
                        (publishedEvents
                         |> Seq.contains (
                             Imperium.Rondel.RondelEvent.ActionDetermined
                                 { GameId = domainMoveCommand.GameId
                                   Nation = nation
                                   Action = expectedAction }
                         ))
                        "no action should be determined when move to current position is rejected repeatedly"

                    Expect.isEmpty
                        dispatchedCommands
                        "no movement fee is due when moving to current position repeatedly"

                testPropertyWithConfig
                    { FsCheckConfig.defaultConfig with
                        maxTest = 15 }
                    "multiple consecutive moves of 1-3 spaces are free"
                <| fun (gameId: Guid) (nationIndex: int) (startSpaceIndex: int) (distance1: int) (distance2: int) ->

                    let nations = [| "Austria"; "Britain"; "France"; "Germany"; "Italy"; "Russia" |]

                    let allSpaces =
                        [ "Investor"
                          "Import"
                          "ProductionOne"
                          "ManeuverOne"
                          "Taxation"
                          "Factory"
                          "ProductionTwo"
                          "ManeuverTwo" ]

                    let nation = nations.[abs nationIndex % nations.Length]
                    let startIndex = abs startSpaceIndex % allSpaces.Length
                    let dist1 = abs distance1 % 3 + 1 // 1, 2, or 3
                    let dist2 = abs distance2 % 3 + 1 // 1, 2, or 3

                    // Setup: initialize rondel
                    let load, save = createMockStore ()
                    let publish, publishedEvents = createMockPublisher ()
                    let chargeForMovement, dispatchedCommands = createMockCommandDispatcher ()
                    let voidCharge, voidedCommands = createMockVoidCharge ()

                    let contractInitCommand = { GameId = gameId; Nations = nations }

                    let domainInitCommand =
                        SetToStartingPositionsCommand.toDomain contractInitCommand
                        |> Result.defaultWith failwith

                    setToStartingPositions load save publish domainInitCommand
                    publishedEvents.Clear()

                    // First move: to starting position
                    let startSpace = allSpaces.[startIndex]

                    let contractMoveCommand1 =
                        { Imperium.Contract.Rondel.MoveCommand.GameId = gameId
                          Nation = nation
                          Space = startSpace }

                    let transformResult1 = MoveCommand.toDomain contractMoveCommand1
                    Expect.isOk transformResult1 "first move should succeed"

                    let domainMoveCommand1 = Result.defaultWith failwith transformResult1

                    move load save publish chargeForMovement voidCharge domainMoveCommand1
                    // Using independent reference implementation to avoid testing transformation with itself
                    let expectedAction1 = spaceNameToExpectedAction startSpace

                    Expect.contains
                        publishedEvents
                        (Imperium.Rondel.RondelEvent.ActionDetermined
                            { GameId = domainMoveCommand1.GameId
                              Nation = nation
                              Action = expectedAction1 })
                        (sprintf "%s's first move to %s should determine action %A" nation startSpace expectedAction1)

                    publishedEvents.Clear()
                    dispatchedCommands.Clear()

                    // Second move: 1-3 spaces forward
                    let secondIndex = (startIndex + dist1) % allSpaces.Length
                    let secondSpace = allSpaces.[secondIndex]

                    let contractMoveCommand2 =
                        { Imperium.Contract.Rondel.MoveCommand.GameId = gameId
                          Nation = nation
                          Space = secondSpace }

                    let transformResult2 = MoveCommand.toDomain contractMoveCommand2
                    Expect.isOk transformResult2 (sprintf "second move (distance %d) should succeed" dist1)

                    let domainMoveCommand2 = Result.defaultWith failwith transformResult2

                    move load save publish chargeForMovement voidCharge domainMoveCommand2
                    // Using independent reference implementation to avoid testing transformation with itself
                    let expectedAction2 = spaceNameToExpectedAction secondSpace

                    Expect.contains
                        publishedEvents
                        (Imperium.Rondel.RondelEvent.ActionDetermined
                            { GameId = domainMoveCommand2.GameId
                              Nation = nation
                              Action = expectedAction2 })
                        (sprintf
                            "%s's move from %s to %s (distance %d) should determine action %A"
                            nation
                            startSpace
                            secondSpace
                            dist1
                            expectedAction2)

                    Expect.isEmpty dispatchedCommands (sprintf "move of %d spaces should be free" dist1)
                    publishedEvents.Clear()
                    dispatchedCommands.Clear()

                    // Third move: 1-3 spaces forward from second position
                    let thirdIndex = (secondIndex + dist2) % allSpaces.Length
                    let thirdSpace = allSpaces.[thirdIndex]

                    let contractMoveCommand3 =
                        { Imperium.Contract.Rondel.MoveCommand.GameId = gameId
                          Nation = nation
                          Space = thirdSpace }

                    let transformResult3 = MoveCommand.toDomain contractMoveCommand3
                    Expect.isOk transformResult3 (sprintf "third move (distance %d) should succeed" dist2)

                    let domainMoveCommand3 = Result.defaultWith failwith transformResult3

                    move load save publish chargeForMovement voidCharge domainMoveCommand3
                    // Using independent reference implementation to avoid testing transformation with itself
                    let expectedAction3 = spaceNameToExpectedAction thirdSpace

                    Expect.contains
                        publishedEvents
                        (Imperium.Rondel.RondelEvent.ActionDetermined
                            { GameId = domainMoveCommand3.GameId
                              Nation = nation
                              Action = expectedAction3 })
                        (sprintf
                            "%s's move from %s to %s (distance %d) should determine action %A"
                            nation
                            secondSpace
                            thirdSpace
                            dist2
                            expectedAction3)

                    Expect.isEmpty dispatchedCommands (sprintf "move of %d spaces should be free" dist2)

                testPropertyWithConfig
                    { FsCheckConfig.defaultConfig with
                        maxTest = 15 }
                    "rejects moves of 7 spaces as exceeding maximum distance"
                <| fun (gameId: Guid) (nationIndex: int) (startSpaceIndex: int) ->

                    let nations = [| "Austria"; "Britain"; "France"; "Germany"; "Italy"; "Russia" |]

                    let allSpaces =
                        [ "Investor"
                          "Import"
                          "ProductionOne"
                          "ManeuverOne"
                          "Taxation"
                          "Factory"
                          "ProductionTwo"
                          "ManeuverTwo" ]

                    let nation = nations.[abs nationIndex % nations.Length]
                    let startIndex = abs startSpaceIndex % allSpaces.Length

                    // Setup: initialize rondel
                    let load, save = createMockStore ()
                    let publish, publishedEvents = createMockPublisher ()
                    let chargeForMovement, dispatchedCommands = createMockCommandDispatcher ()
                    let voidCharge, voidedCommands = createMockVoidCharge ()

                    let contractInitCommand = { GameId = gameId; Nations = nations }

                    let domainInitCommand =
                        SetToStartingPositionsCommand.toDomain contractInitCommand
                        |> Result.defaultWith failwith

                    setToStartingPositions load save publish domainInitCommand
                    publishedEvents.Clear()

                    // First move: establish starting position
                    let startSpace = allSpaces.[startIndex]

                    let contractMoveCommand1 =
                        { Imperium.Contract.Rondel.MoveCommand.GameId = gameId
                          Nation = nation
                          Space = startSpace }

                    let transformResult1 = MoveCommand.toDomain contractMoveCommand1
                    Expect.isOk transformResult1 "first move should succeed"

                    let domainMoveCommand1 = Result.defaultWith failwith transformResult1

                    move load save publish chargeForMovement voidCharge domainMoveCommand1
                    // Using independent reference implementation to avoid testing transformation with itself
                    let expectedAction1 = spaceNameToExpectedAction startSpace

                    Expect.contains
                        publishedEvents
                        (Imperium.Rondel.RondelEvent.ActionDetermined
                            { GameId = domainMoveCommand1.GameId
                              Nation = nation
                              Action = expectedAction1 })
                        (sprintf "%s's first move to %s should succeed" nation startSpace)

                    publishedEvents.Clear()
                    dispatchedCommands.Clear()

                    // Second move: attempt to move 7 spaces (invalid distance)
                    let targetIndex = (startIndex + 7) % allSpaces.Length
                    let targetSpace = allSpaces.[targetIndex]

                    let contractMoveCommand2 =
                        { Imperium.Contract.Rondel.MoveCommand.GameId = gameId
                          Nation = nation
                          Space = targetSpace }

                    let transformResult2 = MoveCommand.toDomain contractMoveCommand2
                    Expect.isOk transformResult2 "transformation should succeed"

                    let domainMoveCommand2 = Result.defaultWith failwith transformResult2

                    move load save publish chargeForMovement voidCharge domainMoveCommand2

                    // Assert: move should be rejected (7 spaces exceeds maximum of 6)
                    Expect.isNonEmpty publishedEvents "the rondel should signal why the move was denied"

                    Expect.contains
                        publishedEvents
                        (Imperium.Rondel.RondelEvent.MoveToActionSpaceRejected
                            { GameId = domainMoveCommand2.GameId
                              Nation = nation
                              Space = domainMoveCommand2.Space })
                        (sprintf
                            "%s's move from %s to %s (7 spaces) should be rejected as exceeding maximum distance"
                            nation
                            startSpace
                            targetSpace)

                    // Assert: No action determined for invalid distance
                    // Using independent reference implementation to avoid testing transformation with itself
                    let expectedAction2 = spaceNameToExpectedAction targetSpace

                    Expect.isFalse
                        (publishedEvents
                         |> Seq.contains (
                             Imperium.Rondel.RondelEvent.ActionDetermined
                                 { GameId = domainMoveCommand2.GameId
                                   Nation = nation
                                   Action = expectedAction2 }
                         ))
                        "no action should be determined when move exceeds maximum distance"

                    // Assert: No charge commands dispatched
                    Expect.isEmpty dispatchedCommands "no movement fee is due for invalid moves"

                testPropertyWithConfig
                    { FsCheckConfig.defaultConfig with
                        maxTest = 15 }
                    "moves of 4-6 spaces require payment (2M per additional space beyond 3)"
                <| fun (gameId: Guid) (nationIndex: int) (startSpaceIndex: int) (distanceRaw: int) ->

                    let nations = [| "Austria"; "Britain"; "France"; "Germany"; "Italy"; "Russia" |]

                    let allSpaces =
                        [ "Investor"
                          "Import"
                          "ProductionOne"
                          "ManeuverOne"
                          "Taxation"
                          "Factory"
                          "ProductionTwo"
                          "ManeuverTwo" ]

                    let nation = nations.[abs nationIndex % nations.Length]
                    let startIndex = abs startSpaceIndex % allSpaces.Length
                    let dist = abs distanceRaw % 3 + 4 // 4, 5, or 6

                    // Setup: initialize rondel
                    let load, save = createMockStore ()
                    let publish, publishedEvents = createMockPublisher ()
                    let chargeForMovement, dispatchedCommands = createMockCommandDispatcher ()
                    let voidCharge, voidedCommands = createMockVoidCharge ()

                    let contractInitCommand = { GameId = gameId; Nations = nations }

                    let domainInitCommand =
                        SetToStartingPositionsCommand.toDomain contractInitCommand
                        |> Result.defaultWith failwith

                    setToStartingPositions load save publish domainInitCommand
                    publishedEvents.Clear()

                    // First move: establish starting position
                    let startSpace = allSpaces.[startIndex]

                    let contractMoveCommand1 =
                        { Imperium.Contract.Rondel.MoveCommand.GameId = gameId
                          Nation = nation
                          Space = startSpace }

                    let transformResult1 = MoveCommand.toDomain contractMoveCommand1
                    Expect.isOk transformResult1 "first move should succeed"

                    let domainMoveCommand1 = Result.defaultWith failwith transformResult1

                    move load save publish chargeForMovement voidCharge domainMoveCommand1
                    publishedEvents.Clear()
                    dispatchedCommands.Clear()

                    // Second move: 4-6 spaces forward (requires payment)
                    let targetIndex = (startIndex + dist) % allSpaces.Length
                    let targetSpace = allSpaces.[targetIndex]

                    let contractMoveCommand2 =
                        { Imperium.Contract.Rondel.MoveCommand.GameId = gameId
                          Nation = nation
                          Space = targetSpace }

                    let transformResult2 = MoveCommand.toDomain contractMoveCommand2

                    Expect.isOk
                        transformResult2
                        (sprintf "move of %d spaces should be accepted (payment required)" dist)

                    let domainMoveCommand2 = Result.defaultWith failwith transformResult2

                    // Assert: move command succeeds
                    move load save publish chargeForMovement voidCharge domainMoveCommand2

                    // Assert: charge command dispatched with correct amount
                    Expect.hasLength
                        dispatchedCommands
                        1
                        (sprintf "exactly one charge command should be dispatched for %d-space move" dist)

                    let chargeCmd = dispatchedCommands.[0]
                    Expect.equal chargeCmd.GameId gameId "charge command should have correct GameId"
                    Expect.equal chargeCmd.Nation nation "charge command should have correct Nation"

                    let expectedAmount = Imperium.Primitives.Amount.unsafe ((dist - 3) * 2)

                    Expect.equal
                        chargeCmd.Amount
                        expectedAmount
                        (sprintf "charge for %d spaces should be %dM ((distance - 3) * 2)" dist ((dist - 3) * 2))

                    Expect.notEqual chargeCmd.BillingId Guid.Empty "charge command should have valid BillingId"

                    // Assert: NO ActionDetermined event (payment pending)
                    let actionDeterminedEvents =
                        publishedEvents
                        |> Seq.filter (function
                            | Imperium.Rondel.RondelEvent.ActionDetermined _ -> true
                            | _ -> false)
                        |> Seq.toList

                    Expect.isEmpty actionDeterminedEvents "no action should be determined until payment is confirmed"

                    // Assert: NO MoveToActionSpaceRejected event (move not rejected)
                    let rejectedEvents =
                        publishedEvents
                        |> Seq.filter (function
                            | Imperium.Rondel.RondelEvent.MoveToActionSpaceRejected _ -> true
                            | _ -> false)
                        |> Seq.toList

                    Expect.isEmpty rejectedEvents "move requiring payment should not be rejected"

                testCase "superseding pending paid move with another paid move voids old charge and rejects old move"
                <| fun _ ->
                    // Setup
                    let load, save = createMockStore ()
                    let publish, publishedEvents = createMockPublisher ()
                    let chargeForMovement, dispatchedCommands = createMockCommandDispatcher ()
                    let voidCharge, voidedCommands = createMockVoidCharge ()

                    let gameId = Guid.NewGuid()
                    let nations = [| "France" |]

                    // Initialize rondel
                    let domainInitCmd =
                        SetToStartingPositionsCommand.toDomain { GameId = gameId; Nations = nations }
                        |> Result.defaultWith failwith

                    setToStartingPositions load save publish domainInitCmd

                    publishedEvents.Clear()

                    // First move: Establish starting position (2 spaces, free)
                    let firstMoveCmd =
                        MoveCommand.toDomain
                            { Imperium.Contract.Rondel.MoveCommand.GameId = gameId
                              Nation = "France"
                              Space = "ProductionOne" }
                        |> Result.defaultWith failwith

                    move load save publish chargeForMovement voidCharge firstMoveCmd

                    Expect.contains
                        publishedEvents
                        (Imperium.Rondel.RondelEvent.ActionDetermined
                            { GameId = firstMoveCmd.GameId
                              Nation = "France"
                              Action = Imperium.Rondel.Action.Production })
                        "first move should determine action"

                    publishedEvents.Clear()
                    dispatchedCommands.Clear()

                    // Second move: 4 spaces (pending payment) - ProductionOne to ProductionTwo
                    let secondMove =
                        MoveCommand.toDomain
                            { Imperium.Contract.Rondel.MoveCommand.GameId = gameId
                              Nation = "France"
                              Space = "ProductionTwo" }
                        |> Result.defaultWith failwith

                    move load save publish chargeForMovement voidCharge secondMove

                    Expect.hasLength dispatchedCommands 1 "first pending move should dispatch charge"
                    let firstBillingId = dispatchedCommands.[0].BillingId

                    Expect.equal
                        dispatchedCommands.[0].Amount
                        (Imperium.Primitives.Amount.unsafe 2)
                        "charge for 4 spaces should be 2M"

                    let actionEvents1 =
                        publishedEvents
                        |> Seq.filter (function
                            | Imperium.Rondel.RondelEvent.ActionDetermined _ -> true
                            | _ -> false)
                        |> Seq.toList

                    Expect.isEmpty actionEvents1 "no action should be determined for pending move"

                    publishedEvents.Clear()
                    dispatchedCommands.Clear()

                    // Third move: 5 spaces (should supersede pending move) - ProductionOne to ManeuverTwo
                    move
                        load
                        save
                        publish
                        chargeForMovement
                        voidCharge
                        (MoveCommand.toDomain
                            { Imperium.Contract.Rondel.MoveCommand.GameId = gameId
                              Nation = "France"
                              Space = "ManeuverTwo" }
                         |> Result.defaultWith failwith)

                    // Assert: old charge voided
                    Expect.hasLength voidedCommands 1 "exactly one void command should be dispatched"
                    Expect.equal voidedCommands.[0].BillingId firstBillingId "should void the first billing"
                    Expect.equal voidedCommands.[0].GameId gameId "void command should have correct GameId"

                    // Assert: old move rejected
                    Expect.contains
                        publishedEvents
                        (Imperium.Rondel.RondelEvent.MoveToActionSpaceRejected
                            { GameId = secondMove.GameId
                              Nation = "France"
                              Space = secondMove.Space })
                        "first pending move should be rejected"

                    // Assert: new charge created
                    Expect.hasLength dispatchedCommands 1 "exactly one new charge should be dispatched"
                    let secondCharge = dispatchedCommands.[0]

                    Expect.equal
                        secondCharge.Amount
                        (Imperium.Primitives.Amount.unsafe 4)
                        "charge for 5 spaces should be 4M"

                    Expect.notEqual secondCharge.BillingId firstBillingId "new charge should have different billing id"
                    Expect.equal secondCharge.GameId gameId "new charge should have correct GameId"
                    Expect.equal secondCharge.Nation "France" "new charge should have correct Nation"

                    // Assert: no action determined (new move still pending payment)
                    let actionEvents2 =
                        publishedEvents
                        |> Seq.filter (function
                            | Imperium.Rondel.RondelEvent.ActionDetermined _ -> true
                            | _ -> false)
                        |> Seq.toList

                    Expect.isEmpty actionEvents2 "no action should be determined for new pending move"

                testCase "superseding pending paid move with free move voids charge and completes immediately"
                <| fun _ ->
                    // Setup
                    let load, save = createMockStore ()
                    let publish, publishedEvents = createMockPublisher ()
                    let chargeForMovement, dispatchedCommands = createMockCommandDispatcher ()
                    let voidCharge, voidedCommands = createMockVoidCharge ()

                    let gameId = Guid.NewGuid()
                    let nations = [| "Germany" |]

                    // Initialize rondel
                    let domainInitCmd =
                        SetToStartingPositionsCommand.toDomain { GameId = gameId; Nations = nations }
                        |> Result.defaultWith failwith

                    setToStartingPositions load save publish domainInitCmd

                    publishedEvents.Clear()

                    // First move: Establish starting position (3 spaces, free)
                    let firstMoveCmd =
                        MoveCommand.toDomain
                            { Imperium.Contract.Rondel.MoveCommand.GameId = gameId
                              Nation = "Germany"
                              Space = "ManeuverOne" }
                        |> Result.defaultWith failwith

                    move load save publish chargeForMovement voidCharge firstMoveCmd

                    Expect.contains
                        publishedEvents
                        (Imperium.Rondel.RondelEvent.ActionDetermined
                            { GameId = firstMoveCmd.GameId
                              Nation = "Germany"
                              Action = Imperium.Rondel.Action.Maneuver })
                        "first move should determine action"

                    publishedEvents.Clear()
                    dispatchedCommands.Clear()

                    // Second move: 5 spaces (pending payment) - ManeuverOne to Investor
                    let secondMove =
                        MoveCommand.toDomain
                            { Imperium.Contract.Rondel.MoveCommand.GameId = gameId
                              Nation = "Germany"
                              Space = "Investor" }
                        |> Result.defaultWith failwith

                    move load save publish chargeForMovement voidCharge secondMove

                    Expect.hasLength dispatchedCommands 1 "first pending move should dispatch charge"
                    let firstBillingId = dispatchedCommands.[0].BillingId

                    Expect.equal
                        dispatchedCommands.[0].Amount
                        (Imperium.Primitives.Amount.unsafe 4)
                        "charge for 5 spaces should be 4M"

                    let actionEvents1 =
                        publishedEvents
                        |> Seq.filter (function
                            | Imperium.Rondel.RondelEvent.ActionDetermined _ -> true
                            | _ -> false)
                        |> Seq.toList

                    Expect.isEmpty actionEvents1 "no action should be determined for pending move"

                    publishedEvents.Clear()
                    dispatchedCommands.Clear()

                    // Third move: 2 spaces (free, should supersede and complete immediately) - ManeuverOne to Factory
                    let thirdMove =
                        MoveCommand.toDomain
                            { Imperium.Contract.Rondel.MoveCommand.GameId = gameId
                              Nation = "Germany"
                              Space = "Factory" }
                        |> Result.defaultWith failwith

                    move load save publish chargeForMovement voidCharge thirdMove

                    // Assert: old charge voided
                    Expect.hasLength voidedCommands 1 "exactly one void command should be dispatched"
                    Expect.equal voidedCommands.[0].BillingId firstBillingId "should void the first billing"
                    Expect.equal voidedCommands.[0].GameId gameId "void command should have correct GameId"

                    // Assert: old move rejected
                    Expect.contains
                        publishedEvents
                        (Imperium.Rondel.RondelEvent.MoveToActionSpaceRejected
                            { GameId = secondMove.GameId
                              Nation = "Germany"
                              Space = secondMove.Space })
                        "first pending move should be rejected"

                    // Assert: no new charge (free move)
                    Expect.isEmpty dispatchedCommands "free move should not dispatch charge command"

                    // Assert: action determined for new move (free move completes immediately)
                    Expect.contains
                        publishedEvents
                        (Imperium.Rondel.RondelEvent.ActionDetermined
                            { GameId = thirdMove.GameId
                              Nation = "Germany"
                              Action = Imperium.Rondel.Action.Factory })
                        "free move should determine action immediately despite superseding" ]
          testList
              "onInvoicePaid"
              [ testCase "completes pending movement and publishes ActionDetermined event"
                <| fun _ ->
                    // Setup: create mocks
                    let load, save = createMockStore ()
                    let publish, publishedEvents = createMockPublisher ()
                    let chargeForMovement, dispatchedCommands = createMockCommandDispatcher ()
                    let voidCharge, voidedCommands = createMockVoidCharge ()

                    let gameId = Guid.NewGuid()
                    let nations = [| "Austria" |]

                    // Setup: initialize rondel
                    let domainInitCmd =
                        SetToStartingPositionsCommand.toDomain { GameId = gameId; Nations = nations }
                        |> Result.defaultWith failwith

                    setToStartingPositions load save publish domainInitCmd

                    publishedEvents.Clear()

                    // Setup: establish starting position with first move (free to any space)
                    let firstMoveCmd =
                        MoveCommand.toDomain
                            { Imperium.Contract.Rondel.MoveCommand.GameId = gameId
                              Nation = "Austria"
                              Space = "ManeuverOne" }
                        |> Result.defaultWith failwith

                    move load save publish chargeForMovement voidCharge firstMoveCmd

                    Expect.contains
                        publishedEvents
                        (Imperium.Rondel.RondelEvent.ActionDetermined
                            { GameId = firstMoveCmd.GameId
                              Nation = "Austria"
                              Action = Imperium.Rondel.Action.Maneuver })
                        "first move should complete immediately"

                    publishedEvents.Clear()
                    dispatchedCommands.Clear()

                    // Setup: initiate paid move (5 spaces: ManeuverOne to Investor) - creates pending movement
                    let secondMoveCmd =
                        MoveCommand.toDomain
                            { Imperium.Contract.Rondel.MoveCommand.GameId = gameId
                              Nation = "Austria"
                              Space = "Investor" }
                        |> Result.defaultWith failwith

                    move load save publish chargeForMovement voidCharge secondMoveCmd

                    Expect.hasLength dispatchedCommands 1 "paid move should dispatch charge command"
                    let billingId = dispatchedCommands.[0].BillingId

                    Expect.equal
                        dispatchedCommands.[0].Amount
                        (Imperium.Primitives.Amount.unsafe 4)
                        "charge for 5 spaces should be 4M"

                    let actionEvents =
                        publishedEvents
                        |> Seq.filter (function
                            | Imperium.Rondel.RondelEvent.ActionDetermined _ -> true
                            | _ -> false)
                        |> Seq.toList

                    Expect.isEmpty actionEvents "no action should be determined until payment confirmed"

                    publishedEvents.Clear()
                    dispatchedCommands.Clear()

                    // Execute: process payment confirmation
                    let paymentEvent: Imperium.Contract.Accounting.RondelInvoicePaid =
                        { GameId = gameId
                          BillingId = billingId }

                    let result = onInvoicedPaid load save publish paymentEvent

                    // Assert: operation succeeds
                    Expect.isOk result "payment confirmation should succeed"

                    // Assert: ActionDetermined event published for target space
                    Expect.contains
                        publishedEvents
                        (Imperium.Rondel.RondelEvent.ActionDetermined
                            { GameId = secondMoveCmd.GameId
                              Nation = "Austria"
                              Action = Imperium.Rondel.Action.Investor })
                        "ActionDetermined event should be published after payment confirmation"

                    // Assert: only one event published
                    Expect.hasLength publishedEvents 1 "only ActionDetermined event should be published"

                    // Assert: no additional charges or voids
                    Expect.isEmpty dispatchedCommands "no new charges after payment confirmation"
                    Expect.isEmpty voidedCommands "no voids after successful payment"

                    // Assert: verify state updated - pending movement cleared, position updated
                    // Subsequent move from Investor (new position) should succeed
                    publishedEvents.Clear()

                    let thirdMoveCmd =
                        MoveCommand.toDomain
                            { Imperium.Contract.Rondel.MoveCommand.GameId = gameId
                              Nation = "Austria"
                              Space = "Import" }
                        |> Result.defaultWith failwith

                    move load save publish chargeForMovement voidCharge thirdMoveCmd

                    Expect.contains
                        publishedEvents
                        (Imperium.Rondel.RondelEvent.ActionDetermined
                            { GameId = thirdMoveCmd.GameId
                              Nation = "Austria"
                              Action = Imperium.Rondel.Action.Import })
                        "subsequent move from Investor to Import (1 space) should succeed, confirming position was updated" ] ]
