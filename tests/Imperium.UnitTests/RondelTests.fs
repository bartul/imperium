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

let createMockVoidCharge () =
    let voidedCommands = ResizeArray<Imperium.Contract.Accounting.VoidRondelChargeCommand>()
    let voidCharge (command: Imperium.Contract.Accounting.VoidRondelChargeCommand) =
        voidedCommands.Add command
        Ok ()
    voidCharge, voidedCommands

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
                let voidCharge, voidedCommands = createMockVoidCharge ()
                let moveCommand = { MoveCommand.GameId = Guid.NewGuid (); Nation = "France"; Space = "Factory" }
                let result = move load save publish chargeForMovement voidCharge moveCommand
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
                let voidCharge, voidedCommands = createMockVoidCharge()

                let initCommand = { GameId = gameId; Nations = nations }
                setToStartingPositions load save publish initCommand |> ignore
                publishedEvents.Clear()

                // Execute: move one nation to target space
                let moveCommand = { MoveCommand.GameId = gameId; Nation = nation; Space = space }
                let result = move load save publish chargeForMovement voidCharge moveCommand

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
                let voidCharge, voidedCommands = createMockVoidCharge ()

                // Setup: initialize rondel with starting positions
                let gameId = Guid.NewGuid ()
                let initCommand = { GameId = gameId; Nations = [|"France"; "Germany"|] }
                setToStartingPositions load save publish initCommand |> ignore
                publishedEvents.Clear ()

                // Execute: attempt to move to an invalid space
                let moveCommand = { MoveCommand.GameId = gameId; Nation = "France"; Space = "InvalidSpace" }
                let result = move load save publish chargeForMovement voidCharge moveCommand

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
                let voidCharge, voidedCommands = createMockVoidCharge ()

                // Execute: attempt to move with empty game id
                let moveCommand = { MoveCommand.GameId = Guid.Empty; Nation = "France"; Space = "Factory" }
                let result = move load save publish chargeForMovement voidCharge moveCommand

                // Assert: operation should fail
                Expect.isError result "a move cannot be taken without a game id"

                // Assert: No events published
                Expect.isEmpty publishedEvents "without a game id, the rondel should not signal anything"

                // Assert: No charge commands dispatched
                Expect.isEmpty dispatchedCommands "no movement fee is due without a game id"

            testPropertyWithConfig { FsCheckConfig.defaultConfig with maxTest = 15 } "move: rejects move to nation's current position repeatedly" <| fun (gameId: Guid) (nationIndex: int) (spaceIndex: int) ->

                let nations = [|"Austria"; "Britain"; "France"; "Germany"; "Italy"; "Russia"|]
                let allSpaces = ["Investor"; "Factory"; "Import"; "ManeuverOne"; "ProductionOne"; "ManeuverTwo"; "ProductionTwo"; "Taxation"]

                let nation = nations.[abs nationIndex % nations.Length]
                let space = allSpaces.[abs spaceIndex % allSpaces.Length]

                // Setup: initialize rondel
                let load, save = createMockStore()
                let publish, publishedEvents = createMockPublisher()
                let chargeForMovement, dispatchedCommands = createMockCommandDispatcher()
                let voidCharge, voidedCommands = createMockVoidCharge()

                let initCommand = { GameId = gameId; Nations = nations }
                setToStartingPositions load save publish initCommand |> ignore
                publishedEvents.Clear()

                // Execute: move nation to target space (first move)
                let moveCommand = { MoveCommand.GameId = gameId; Nation = nation; Space = space }
                let firstMoveResult = move load save publish chargeForMovement voidCharge moveCommand

                // Assert: first move succeeds
                Expect.isOk firstMoveResult "first move should succeed"
                let expectedAction = spaceToAction space
                Expect.contains publishedEvents (ActionDetermined { GameId = gameId; Nation = nation; Action = expectedAction }) (sprintf "the rondel space %s determines %s's action: %s" space nation expectedAction)
                publishedEvents.Clear()
                dispatchedCommands.Clear()

                // Execute: attempt to move to same position (first rejection)
                let secondMoveResult = move load save publish chargeForMovement voidCharge moveCommand

                // Assert: second move is rejected
                Expect.isOk secondMoveResult "the move should be denied without breaking the game flow"
                Expect.isNonEmpty publishedEvents "the rondel should signal why the move was denied"
                Expect.contains publishedEvents (MoveToActionSpaceRejected { GameId = gameId; Nation = nation; Space = space }) (sprintf "%s's move to current position %s should be rejected" nation space)
                Expect.isFalse (publishedEvents |> Seq.contains (ActionDetermined { GameId = gameId; Nation = nation; Action = expectedAction })) "no action should be determined when move to current position is rejected"
                Expect.isEmpty dispatchedCommands "no movement fee is due when moving to current position"
                publishedEvents.Clear()
                dispatchedCommands.Clear()

                // Execute: attempt to move to same position again (second rejection)
                let thirdMoveResult = move load save publish chargeForMovement voidCharge moveCommand

                // Assert: third move is also rejected
                Expect.isOk thirdMoveResult "the move should be denied without breaking the game flow"
                Expect.isNonEmpty publishedEvents "the rondel should signal why the move was denied"
                Expect.contains publishedEvents (MoveToActionSpaceRejected { GameId = gameId; Nation = nation; Space = space }) (sprintf "%s's repeated move to current position %s should be rejected" nation space)
                Expect.isFalse (publishedEvents |> Seq.contains (ActionDetermined { GameId = gameId; Nation = nation; Action = expectedAction })) "no action should be determined when move to current position is rejected repeatedly"
                Expect.isEmpty dispatchedCommands "no movement fee is due when moving to current position repeatedly"

            testPropertyWithConfig { FsCheckConfig.defaultConfig with maxTest = 15 } "move: multiple consecutive moves of 1-3 spaces are free" <| fun (gameId: Guid) (nationIndex: int) (startSpaceIndex: int) (distance1: int) (distance2: int) ->

                let nations = [|"Austria"; "Britain"; "France"; "Germany"; "Italy"; "Russia"|]
                let allSpaces = ["Investor"; "Import"; "ProductionOne"; "ManeuverOne"; "Taxation"; "Factory"; "ProductionTwo"; "ManeuverTwo"]

                let nation = nations.[abs nationIndex % nations.Length]
                let startIndex = abs startSpaceIndex % allSpaces.Length
                let dist1 = abs distance1 % 3 + 1  // 1, 2, or 3
                let dist2 = abs distance2 % 3 + 1  // 1, 2, or 3

                // Setup: initialize rondel
                let load, save = createMockStore()
                let publish, publishedEvents = createMockPublisher()
                let chargeForMovement, dispatchedCommands = createMockCommandDispatcher()
                let voidCharge, voidedCommands = createMockVoidCharge()

                let initCommand = { GameId = gameId; Nations = nations }
                setToStartingPositions load save publish initCommand |> ignore
                publishedEvents.Clear()

                // First move: to starting position
                let startSpace = allSpaces.[startIndex]
                let moveCommand1 = { MoveCommand.GameId = gameId; Nation = nation; Space = startSpace }
                let result1 = move load save publish chargeForMovement voidCharge moveCommand1

                Expect.isOk result1 "first move should succeed"
                let expectedAction1 = spaceToAction startSpace
                Expect.contains publishedEvents (ActionDetermined { GameId = gameId; Nation = nation; Action = expectedAction1 }) (sprintf "%s's first move to %s should determine action %s" nation startSpace expectedAction1)
                publishedEvents.Clear()
                dispatchedCommands.Clear()

                // Second move: 1-3 spaces forward
                let secondIndex = (startIndex + dist1) % allSpaces.Length
                let secondSpace = allSpaces.[secondIndex]
                let moveCommand2 = { MoveCommand.GameId = gameId; Nation = nation; Space = secondSpace }
                let result2 = move load save publish chargeForMovement voidCharge moveCommand2

                Expect.isOk result2 (sprintf "second move (distance %d) should succeed" dist1)
                let expectedAction2 = spaceToAction secondSpace
                Expect.contains publishedEvents (ActionDetermined { GameId = gameId; Nation = nation; Action = expectedAction2 }) (sprintf "%s's move from %s to %s (distance %d) should determine action %s" nation startSpace secondSpace dist1 expectedAction2)
                Expect.isEmpty dispatchedCommands (sprintf "move of %d spaces should be free" dist1)
                publishedEvents.Clear()
                dispatchedCommands.Clear()

                // Third move: 1-3 spaces forward from second position
                let thirdIndex = (secondIndex + dist2) % allSpaces.Length
                let thirdSpace = allSpaces.[thirdIndex]
                let moveCommand3 = { MoveCommand.GameId = gameId; Nation = nation; Space = thirdSpace }
                let result3 = move load save publish chargeForMovement voidCharge moveCommand3

                Expect.isOk result3 (sprintf "third move (distance %d) should succeed" dist2)
                let expectedAction3 = spaceToAction thirdSpace
                Expect.contains publishedEvents (ActionDetermined { GameId = gameId; Nation = nation; Action = expectedAction3 }) (sprintf "%s's move from %s to %s (distance %d) should determine action %s" nation secondSpace thirdSpace dist2 expectedAction3)
                Expect.isEmpty dispatchedCommands (sprintf "move of %d spaces should be free" dist2)

            testPropertyWithConfig { FsCheckConfig.defaultConfig with maxTest = 15 } "move: rejects moves of 7 spaces as exceeding maximum distance" <| fun (gameId: Guid) (nationIndex: int) (startSpaceIndex: int) ->

                let nations = [|"Austria"; "Britain"; "France"; "Germany"; "Italy"; "Russia"|]
                let allSpaces = ["Investor"; "Import"; "ProductionOne"; "ManeuverOne"; "Taxation"; "Factory"; "ProductionTwo"; "ManeuverTwo"]

                let nation = nations.[abs nationIndex % nations.Length]
                let startIndex = abs startSpaceIndex % allSpaces.Length

                // Setup: initialize rondel
                let load, save = createMockStore()
                let publish, publishedEvents = createMockPublisher()
                let chargeForMovement, dispatchedCommands = createMockCommandDispatcher()
                let voidCharge, voidedCommands = createMockVoidCharge()

                let initCommand = { GameId = gameId; Nations = nations }
                setToStartingPositions load save publish initCommand |> ignore
                publishedEvents.Clear()

                // First move: establish starting position
                let startSpace = allSpaces.[startIndex]
                let moveCommand1 = { MoveCommand.GameId = gameId; Nation = nation; Space = startSpace }
                let result1 = move load save publish chargeForMovement voidCharge moveCommand1

                Expect.isOk result1 "first move should succeed"
                let expectedAction1 = spaceToAction startSpace
                Expect.contains publishedEvents (ActionDetermined { GameId = gameId; Nation = nation; Action = expectedAction1 }) (sprintf "%s's first move to %s should succeed" nation startSpace)
                publishedEvents.Clear()
                dispatchedCommands.Clear()

                // Second move: attempt to move 7 spaces (invalid distance)
                let targetIndex = (startIndex + 7) % allSpaces.Length
                let targetSpace = allSpaces.[targetIndex]
                let moveCommand2 = { MoveCommand.GameId = gameId; Nation = nation; Space = targetSpace }
                let result2 = move load save publish chargeForMovement voidCharge moveCommand2

                // Assert: move should be rejected (7 spaces exceeds maximum of 6)
                Expect.isOk result2 "the move should be denied without breaking the game flow"
                Expect.isNonEmpty publishedEvents "the rondel should signal why the move was denied"
                Expect.contains publishedEvents (MoveToActionSpaceRejected { GameId = gameId; Nation = nation; Space = targetSpace }) (sprintf "%s's move from %s to %s (7 spaces) should be rejected as exceeding maximum distance" nation startSpace targetSpace)

                // Assert: No action determined for invalid distance
                let expectedAction2 = spaceToAction targetSpace
                Expect.isFalse (publishedEvents |> Seq.contains (ActionDetermined { GameId = gameId; Nation = nation; Action = expectedAction2 })) "no action should be determined when move exceeds maximum distance"

                // Assert: No charge commands dispatched
                Expect.isEmpty dispatchedCommands "no movement fee is due for invalid moves"

            testPropertyWithConfig { FsCheckConfig.defaultConfig with maxTest = 15 } "move: moves of 4-6 spaces require payment (2M per additional space beyond 3)" <| fun (gameId: Guid) (nationIndex: int) (startSpaceIndex: int) (distanceRaw: int) ->

                let nations = [|"Austria"; "Britain"; "France"; "Germany"; "Italy"; "Russia"|]
                let allSpaces = ["Investor"; "Import"; "ProductionOne"; "ManeuverOne"; "Taxation"; "Factory"; "ProductionTwo"; "ManeuverTwo"]

                let nation = nations.[abs nationIndex % nations.Length]
                let startIndex = abs startSpaceIndex % allSpaces.Length
                let dist = abs distanceRaw % 3 + 4  // 4, 5, or 6

                // Setup: initialize rondel
                let load, save = createMockStore()
                let publish, publishedEvents = createMockPublisher()
                let chargeForMovement, dispatchedCommands = createMockCommandDispatcher()
                let voidCharge, voidedCommands = createMockVoidCharge()

                let initCommand = { GameId = gameId; Nations = nations }
                setToStartingPositions load save publish initCommand |> ignore
                publishedEvents.Clear()

                // First move: establish starting position
                let startSpace = allSpaces.[startIndex]
                let moveCommand1 = { MoveCommand.GameId = gameId; Nation = nation; Space = startSpace }
                let result1 = move load save publish chargeForMovement voidCharge moveCommand1

                Expect.isOk result1 "first move should succeed"
                publishedEvents.Clear()
                dispatchedCommands.Clear()

                // Second move: 4-6 spaces forward (requires payment)
                let targetIndex = (startIndex + dist) % allSpaces.Length
                let targetSpace = allSpaces.[targetIndex]
                let moveCommand2 = { MoveCommand.GameId = gameId; Nation = nation; Space = targetSpace }
                let result2 = move load save publish chargeForMovement voidCharge moveCommand2

                // Assert: move command succeeds
                Expect.isOk result2 (sprintf "move of %d spaces should be accepted (payment required)" dist)

                // Assert: charge command dispatched with correct amount
                Expect.hasLength dispatchedCommands 1 (sprintf "exactly one charge command should be dispatched for %d-space move" dist)
                let chargeCmd = dispatchedCommands.[0]
                Expect.equal chargeCmd.GameId gameId "charge command should have correct GameId"
                Expect.equal chargeCmd.Nation nation "charge command should have correct Nation"

                let expectedAmount = Imperium.Primitives.Amount.unsafe ((dist - 3) * 2)
                Expect.equal chargeCmd.Amount expectedAmount (sprintf "charge for %d spaces should be %dM ((distance - 3) * 2)" dist ((dist - 3) * 2))

                Expect.notEqual chargeCmd.BillingId Guid.Empty "charge command should have valid BillingId"

                // Assert: NO ActionDetermined event (payment pending)
                let actionDeterminedEvents = publishedEvents |> Seq.filter (function | ActionDetermined _ -> true | _ -> false) |> Seq.toList
                Expect.isEmpty actionDeterminedEvents "no action should be determined until payment is confirmed"

                // Assert: NO MoveToActionSpaceRejected event (move not rejected)
                let rejectedEvents = publishedEvents |> Seq.filter (function | MoveToActionSpaceRejected _ -> true | _ -> false) |> Seq.toList
                Expect.isEmpty rejectedEvents "move requiring payment should not be rejected"

            testCase "move: superseding pending paid move with another paid move voids old charge and rejects old move" <| fun _ ->
                // Setup
                let load, save = createMockStore()
                let publish, publishedEvents = createMockPublisher()
                let chargeForMovement, dispatchedCommands = createMockCommandDispatcher()
                let voidCharge, voidedCommands = createMockVoidCharge()

                let gameId = Guid.NewGuid()
                let nations = [|"France"|]

                // Initialize rondel
                setToStartingPositions load save publish { GameId = gameId; Nations = nations } |> ignore
                publishedEvents.Clear()

                // First move: Establish starting position (2 spaces, free)
                move load save publish chargeForMovement voidCharge
                    { GameId = gameId; Nation = "France"; Space = "ProductionOne" } |> ignore

                Expect.contains publishedEvents (ActionDetermined { GameId = gameId; Nation = "France"; Action = "Production" })
                    "first move should determine action"
                publishedEvents.Clear()
                dispatchedCommands.Clear()

                // Second move: 4 spaces (pending payment) - ProductionOne to ProductionTwo
                move load save publish chargeForMovement voidCharge
                    { GameId = gameId; Nation = "France"; Space = "ProductionTwo" } |> ignore

                Expect.hasLength dispatchedCommands 1 "first pending move should dispatch charge"
                let firstBillingId = dispatchedCommands.[0].BillingId
                Expect.equal dispatchedCommands.[0].Amount (Imperium.Primitives.Amount.unsafe 2) "charge for 4 spaces should be 2M"

                let actionEvents1 = publishedEvents |> Seq.filter (function | ActionDetermined _ -> true | _ -> false) |> Seq.toList
                Expect.isEmpty actionEvents1 "no action should be determined for pending move"

                publishedEvents.Clear()
                dispatchedCommands.Clear()

                // Third move: 5 spaces (should supersede pending move) - ProductionOne to ManeuverTwo
                move load save publish chargeForMovement voidCharge
                    { GameId = gameId; Nation = "France"; Space = "ManeuverTwo" } |> ignore

                // Assert: old charge voided
                Expect.hasLength voidedCommands 1 "exactly one void command should be dispatched"
                Expect.equal voidedCommands.[0].BillingId firstBillingId "should void the first billing"
                Expect.equal voidedCommands.[0].GameId gameId "void command should have correct GameId"

                // Assert: old move rejected
                Expect.contains publishedEvents
                    (MoveToActionSpaceRejected { GameId = gameId; Nation = "France"; Space = "ProductionTwo" })
                    "first pending move should be rejected"

                // Assert: new charge created
                Expect.hasLength dispatchedCommands 1 "exactly one new charge should be dispatched"
                let secondCharge = dispatchedCommands.[0]
                Expect.equal secondCharge.Amount (Imperium.Primitives.Amount.unsafe 4) "charge for 5 spaces should be 4M"
                Expect.notEqual secondCharge.BillingId firstBillingId "new charge should have different billing id"
                Expect.equal secondCharge.GameId gameId "new charge should have correct GameId"
                Expect.equal secondCharge.Nation "France" "new charge should have correct Nation"

                // Assert: no action determined (new move still pending payment)
                let actionEvents2 = publishedEvents |> Seq.filter (function | ActionDetermined _ -> true | _ -> false) |> Seq.toList
                Expect.isEmpty actionEvents2 "no action should be determined for new pending move"

            testCase "move: superseding pending paid move with free move voids charge and completes immediately" <| fun _ ->
                // Setup
                let load, save = createMockStore()
                let publish, publishedEvents = createMockPublisher()
                let chargeForMovement, dispatchedCommands = createMockCommandDispatcher()
                let voidCharge, voidedCommands = createMockVoidCharge()

                let gameId = Guid.NewGuid()
                let nations = [|"Germany"|]

                // Initialize rondel
                setToStartingPositions load save publish { GameId = gameId; Nations = nations } |> ignore
                publishedEvents.Clear()

                // First move: Establish starting position (3 spaces, free)
                move load save publish chargeForMovement voidCharge
                    { GameId = gameId; Nation = "Germany"; Space = "ManeuverOne" } |> ignore

                Expect.contains publishedEvents (ActionDetermined { GameId = gameId; Nation = "Germany"; Action = "Maneuver" })
                    "first move should determine action"
                publishedEvents.Clear()
                dispatchedCommands.Clear()

                // Second move: 5 spaces (pending payment) - ManeuverOne to Investor
                move load save publish chargeForMovement voidCharge
                    { GameId = gameId; Nation = "Germany"; Space = "Investor" } |> ignore

                Expect.hasLength dispatchedCommands 1 "first pending move should dispatch charge"
                let firstBillingId = dispatchedCommands.[0].BillingId
                Expect.equal dispatchedCommands.[0].Amount (Imperium.Primitives.Amount.unsafe 4) "charge for 5 spaces should be 4M"

                let actionEvents1 = publishedEvents |> Seq.filter (function | ActionDetermined _ -> true | _ -> false) |> Seq.toList
                Expect.isEmpty actionEvents1 "no action should be determined for pending move"

                publishedEvents.Clear()
                dispatchedCommands.Clear()

                // Third move: 2 spaces (free, should supersede and complete immediately) - ManeuverOne to Factory
                move load save publish chargeForMovement voidCharge
                    { GameId = gameId; Nation = "Germany"; Space = "Factory" } |> ignore

                // Assert: old charge voided
                Expect.hasLength voidedCommands 1 "exactly one void command should be dispatched"
                Expect.equal voidedCommands.[0].BillingId firstBillingId "should void the first billing"
                Expect.equal voidedCommands.[0].GameId gameId "void command should have correct GameId"

                // Assert: old move rejected
                Expect.contains publishedEvents
                    (MoveToActionSpaceRejected { GameId = gameId; Nation = "Germany"; Space = "Investor" })
                    "first pending move should be rejected"

                // Assert: no new charge (free move)
                Expect.isEmpty dispatchedCommands "free move should not dispatch charge command"

                // Assert: action determined for new move (free move completes immediately)
                Expect.contains publishedEvents (ActionDetermined { GameId = gameId; Nation = "Germany"; Action = "Factory" })
                    "free move should determine action immediately despite superseding"
        ]
    ]
