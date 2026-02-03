module Imperium.UnitTests.RondelTests

open System
open Expecto
open Imperium.Rondel
open Imperium.Primitives

type private Rondel =
    { Execute: RondelCommand -> unit
      Handle: RondelInboundEvent -> unit
      GetNationPositions: GetNationPositionsQuery -> RondelPositionsView option
      GetRondelOverview: GetRondelOverviewQuery -> RondelView option }

let private createRondel () =
    let store = Collections.Generic.Dictionary<Id, RondelState>()

    let load (gameId: Id) : Async<RondelState option> =
        async {
            return
                match store.TryGetValue(gameId) with
                | true, state -> Some state
                | false, _ -> None
        }

    let save (state: RondelState) : Async<Result<unit, string>> =
        async {
            store.[state.GameId] <- state
            return Ok()
        }

    let publishedEvents = ResizeArray<RondelEvent>()

    let publish (event: RondelEvent) : Async<unit> = async { publishedEvents.Add event }

    let dispatchedCommands = ResizeArray<RondelOutboundCommand>()

    let dispatch (command: RondelOutboundCommand) : Async<Result<unit, string>> =
        async {
            dispatchedCommands.Add command
            return Ok()
        }

    let deps = { Load = load; Save = save; Publish = publish; Dispatch = dispatch }

    let queryDeps: RondelQueryDependencies = { Load = load }

    // Wrap async routers in synchronous interface for test convenience
    { Execute = fun cmd -> execute deps cmd |> Async.RunSynchronously
      Handle = fun evt -> handle deps evt |> Async.RunSynchronously
      GetNationPositions = fun q -> getNationPositions queryDeps q |> Async.RunSynchronously
      GetRondelOverview = fun q -> getRondelOverview queryDeps q |> Async.RunSynchronously },
    publishedEvents,
    dispatchedCommands

/// Helper to extract ChargeMovement commands from dispatched commands
let getChargeCommands (commands: ResizeArray<RondelOutboundCommand>) =
    commands
    |> Seq.choose (function
        | ChargeMovement cmd -> Some cmd
        | _ -> None)
    |> Seq.toList

/// Helper to extract VoidCharge commands from dispatched commands
let getVoidCommands (commands: ResizeArray<RondelOutboundCommand>) =
    commands
    |> Seq.choose (function
        | VoidCharge cmd -> Some cmd
        | _ -> None)
    |> Seq.toList

// Independent reference implementation for test verification
// This provides an alternate path to verify Space -> Action mapping
// without using the production Space.toAction function
let spaceToExpectedAction (space: Space) =
    match space with
    | Space.Investor -> Action.Investor
    | Space.Factory -> Action.Factory
    | Space.Import -> Action.Import
    | Space.Taxation -> Action.Taxation
    | Space.ProductionOne
    | Space.ProductionTwo -> Action.Production
    | Space.ManeuverOne
    | Space.ManeuverTwo -> Action.Maneuver

// All rondel spaces in clockwise order
let allSpaces =
    [ Space.Investor
      Space.Import
      Space.ProductionOne
      Space.ManeuverOne
      Space.Taxation
      Space.Factory
      Space.ProductionTwo
      Space.ManeuverTwo ]

[<Tests>]
let tests =
    testList
        "Rondel"
        [
          // Tests for public Rondel API
          testList
              "starting positions"
              [ testCase "signals setup for the roster"
                <| fun _ ->
                    let rondel, publishedEvents, _ = createRondel ()

                    let command =
                        { GameId = Guid.NewGuid() |> Id; Nations = Set.ofList [ "France"; "Germany" ] }
                    // Handler succeeds
                    rondel.Execute <| SetToStartingPositions command
                    Expect.isNonEmpty publishedEvents "the rondel should signal that starting positions are set"

                    Expect.contains
                        publishedEvents
                        (PositionedAtStart { GameId = command.GameId })
                        "the rondel should signal that starting positions are set"
                testCase "setting twice does not signal again"
                <| fun _ ->
                    let rondel, publishedEvents, _ = createRondel ()

                    let command =
                        { GameId = Guid.NewGuid() |> Id; Nations = Set.ofList [ "France"; "Germany" ] }

                    // First call to set positions
                    rondel.Execute <| SetToStartingPositions command
                    publishedEvents.Clear()
                    // Second call to set positions again
                    rondel.Execute <| SetToStartingPositions command

                    let positionedAtStartPublishedEvents =
                        publishedEvents
                        |> Seq.filter (function
                            | PositionedAtStart _ -> true
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
                    let rondel, publishedEvents, dispatchedCommands = createRondel ()

                    let command: MoveCommand =
                        { GameId = Guid.NewGuid() |> Id; Nation = "France"; Space = Space.Factory }

                    rondel.Execute <| Move command
                    Expect.isNonEmpty publishedEvents "the rondel should signal why the move was denied"

                    Expect.contains
                        publishedEvents
                        (MoveToActionSpaceRejected
                            { GameId = command.GameId; Nation = command.Nation; Space = command.Space })
                        "the rondel should signal that the move was denied"

                    Expect.isEmpty dispatchedCommands "no movement fee is due when the move is denied"

                testPropertyWithConfig
                    { FsCheckConfig.defaultConfig with maxTest = 15 }
                    "nation's first move may choose any rondel space (free)"
                <| fun (gameId: Guid) (nationIndex: int) (spaceIndex: int) ->

                    let nations = [| "Austria"; "Britain"; "France"; "Germany"; "Italy"; "Russia" |]

                    let game = gameId |> Id
                    let nation = nations.[abs nationIndex % nations.Length]
                    let space = allSpaces.[abs spaceIndex % allSpaces.Length]

                    // Setup: initialize rondel
                    let rondel, publishedEvents, dispatchedCommands = createRondel ()

                    let initCommand = { GameId = gameId |> Id; Nations = Set.ofArray nations }

                    rondel.Execute <| SetToStartingPositions initCommand
                    publishedEvents.Clear()

                    // Execute: move one nation to target space
                    let moveCommand: MoveCommand = { GameId = game; Nation = nation; Space = space }

                    rondel.Execute <| Move moveCommand

                    // Assert: ActionDetermined event published with correct action
                    // Using independent reference implementation to avoid testing transformation with itself
                    let expectedAction = spaceToExpectedAction space

                    Expect.contains
                        publishedEvents
                        (ActionDetermined { GameId = moveCommand.GameId; Nation = nation; Action = expectedAction })
                        (sprintf "the rondel space %A determines %s's action: %A" space nation expectedAction)

                    // Assert: No charge commands dispatched (first move is free)
                    Expect.isEmpty dispatchedCommands "first move is free (no movement fee)"

                testPropertyWithConfig
                    { FsCheckConfig.defaultConfig with maxTest = 15 }
                    "rejects move to nation's current position repeatedly"
                <| fun (gameId: Guid) (nationIndex: int) (spaceIndex: int) ->

                    let game = gameId |> Id
                    let nations = [| "Austria"; "Britain"; "France"; "Germany"; "Italy"; "Russia" |]
                    let nation = nations.[abs nationIndex % nations.Length]
                    let space = allSpaces.[abs spaceIndex % allSpaces.Length]

                    // Setup: initialize rondel
                    let rondel, publishedEvents, dispatchedCommands = createRondel ()

                    let initCommand = { GameId = game; Nations = Set.ofArray nations }

                    rondel.Execute <| SetToStartingPositions initCommand
                    publishedEvents.Clear()

                    // Execute: move nation to target space (first move)
                    let moveCommand: MoveCommand = { GameId = game; Nation = nation; Space = space }

                    rondel.Execute <| Move moveCommand

                    // Assert: first move succeeds
                    // Using independent reference implementation to avoid testing transformation with itself
                    let expectedAction = spaceToExpectedAction space

                    Expect.contains
                        publishedEvents
                        (ActionDetermined { GameId = moveCommand.GameId; Nation = nation; Action = expectedAction })
                        (sprintf "the rondel space %A determines %s's action: %A" space nation expectedAction)

                    publishedEvents.Clear()
                    dispatchedCommands.Clear()

                    // Execute: attempt to move to same position (first rejection)
                    rondel.Execute <| Move moveCommand

                    // Assert: second move is rejected
                    Expect.isNonEmpty publishedEvents "the rondel should signal why the move was denied"

                    Expect.contains
                        publishedEvents
                        (MoveToActionSpaceRejected
                            { GameId = moveCommand.GameId; Nation = nation; Space = moveCommand.Space })
                        (sprintf "%s's move to current position %A should be rejected" nation space)

                    Expect.isFalse
                        (publishedEvents
                         |> Seq.contains (
                             ActionDetermined { GameId = moveCommand.GameId; Nation = nation; Action = expectedAction }
                         ))
                        "no action should be determined when move to current position is rejected"

                    Expect.isEmpty dispatchedCommands "no movement fee is due when moving to current position"
                    publishedEvents.Clear()
                    dispatchedCommands.Clear()

                    // Execute: attempt to move to same position again (second rejection)
                    rondel.Execute <| Move moveCommand

                    // Assert: third move is also rejected
                    Expect.isNonEmpty publishedEvents "the rondel should signal why the move was denied"

                    Expect.contains
                        publishedEvents
                        (MoveToActionSpaceRejected
                            { GameId = moveCommand.GameId; Nation = nation; Space = moveCommand.Space })
                        (sprintf "%s's repeated move to current position %A should be rejected" nation space)

                    Expect.isFalse
                        (publishedEvents
                         |> Seq.contains (
                             ActionDetermined { GameId = moveCommand.GameId; Nation = nation; Action = expectedAction }
                         ))
                        "no action should be determined when move to current position is rejected repeatedly"

                    Expect.isEmpty
                        dispatchedCommands
                        "no movement fee is due when moving to current position repeatedly"

                testPropertyWithConfig
                    { FsCheckConfig.defaultConfig with maxTest = 15 }
                    "multiple consecutive moves of 1-3 spaces are free"
                <| fun (gameId: Guid) (nationIndex: int) (startSpaceIndex: int) (distance1: int) (distance2: int) ->

                    let game = gameId |> Id
                    let nations = [| "Austria"; "Britain"; "France"; "Germany"; "Italy"; "Russia" |]
                    let nation = nations.[abs nationIndex % nations.Length]
                    let startIndex = abs startSpaceIndex % allSpaces.Length
                    let dist1 = abs distance1 % 3 + 1 // 1, 2, or 3
                    let dist2 = abs distance2 % 3 + 1 // 1, 2, or 3

                    // Setup: initialize rondel
                    let rondel, publishedEvents, dispatchedCommands = createRondel ()

                    let initCommand = { GameId = game; Nations = Set.ofArray nations }

                    rondel.Execute <| SetToStartingPositions initCommand
                    publishedEvents.Clear()

                    // First move: to starting position
                    let startSpace = allSpaces.[startIndex]

                    let moveCommand1: MoveCommand =
                        { GameId = game; Nation = nation; Space = startSpace }

                    rondel.Execute <| Move moveCommand1
                    // Using independent reference implementation to avoid testing transformation with itself
                    let expectedAction1 = spaceToExpectedAction startSpace

                    Expect.contains
                        publishedEvents
                        (ActionDetermined { GameId = moveCommand1.GameId; Nation = nation; Action = expectedAction1 })
                        (sprintf "%s's first move to %A should determine action %A" nation startSpace expectedAction1)

                    publishedEvents.Clear()
                    dispatchedCommands.Clear()

                    // Second move: 1-3 spaces forward
                    let secondIndex = (startIndex + dist1) % allSpaces.Length
                    let secondSpace = allSpaces.[secondIndex]

                    let moveCommand2: MoveCommand =
                        { GameId = game; Nation = nation; Space = secondSpace }

                    rondel.Execute <| Move moveCommand2
                    // Using independent reference implementation to avoid testing transformation with itself
                    let expectedAction2 = spaceToExpectedAction secondSpace

                    Expect.contains
                        publishedEvents
                        (ActionDetermined { GameId = moveCommand2.GameId; Nation = nation; Action = expectedAction2 })
                        (sprintf
                            "%s's move from %A to %A (distance %d) should determine action %A"
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

                    let moveCommand3: MoveCommand =
                        { GameId = game; Nation = nation; Space = thirdSpace }

                    rondel.Execute <| Move moveCommand3
                    // Using independent reference implementation to avoid testing transformation with itself
                    let expectedAction3 = spaceToExpectedAction thirdSpace

                    Expect.contains
                        publishedEvents
                        (ActionDetermined { GameId = moveCommand3.GameId; Nation = nation; Action = expectedAction3 })
                        (sprintf
                            "%s's move from %A to %A (distance %d) should determine action %A"
                            nation
                            secondSpace
                            thirdSpace
                            dist2
                            expectedAction3)

                    Expect.isEmpty dispatchedCommands (sprintf "move of %d spaces should be free" dist2)

                testPropertyWithConfig
                    { FsCheckConfig.defaultConfig with maxTest = 15 }
                    "rejects moves of 7 spaces as exceeding maximum distance"
                <| fun (gameId: Guid) (nationIndex: int) (startSpaceIndex: int) ->

                    let game = gameId |> Id
                    let nations = [| "Austria"; "Britain"; "France"; "Germany"; "Italy"; "Russia" |]
                    let nation = nations.[abs nationIndex % nations.Length]
                    let startIndex = abs startSpaceIndex % allSpaces.Length

                    // Setup: initialize rondel
                    let rondel, publishedEvents, dispatchedCommands = createRondel ()

                    let initCommand = { GameId = game; Nations = Set.ofArray nations }

                    rondel.Execute <| SetToStartingPositions initCommand
                    publishedEvents.Clear()

                    // First move: establish starting position
                    let startSpace = allSpaces.[startIndex]

                    let moveCommand1: MoveCommand =
                        { GameId = game; Nation = nation; Space = startSpace }

                    rondel.Execute <| Move moveCommand1
                    // Using independent reference implementation to avoid testing transformation with itself
                    let expectedAction1 = spaceToExpectedAction startSpace

                    Expect.contains
                        publishedEvents
                        (ActionDetermined { GameId = moveCommand1.GameId; Nation = nation; Action = expectedAction1 })
                        (sprintf "%s's first move to %A should succeed" nation startSpace)

                    publishedEvents.Clear()
                    dispatchedCommands.Clear()

                    // Second move: attempt to move 7 spaces (invalid distance)
                    let targetIndex = (startIndex + 7) % allSpaces.Length
                    let targetSpace = allSpaces.[targetIndex]

                    let moveCommand2: MoveCommand =
                        { GameId = game; Nation = nation; Space = targetSpace }

                    rondel.Execute <| Move moveCommand2

                    // Assert: move should be rejected (7 spaces exceeds maximum of 6)
                    Expect.isNonEmpty publishedEvents "the rondel should signal why the move was denied"

                    Expect.contains
                        publishedEvents
                        (MoveToActionSpaceRejected
                            { GameId = moveCommand2.GameId; Nation = nation; Space = moveCommand2.Space })
                        (sprintf
                            "%s's move from %A to %A (7 spaces) should be rejected as exceeding maximum distance"
                            nation
                            startSpace
                            targetSpace)

                    // Assert: No action determined for invalid distance
                    // Using independent reference implementation to avoid testing transformation with itself
                    let expectedAction2 = spaceToExpectedAction targetSpace

                    Expect.isFalse
                        (publishedEvents
                         |> Seq.contains (
                             ActionDetermined
                                 { GameId = moveCommand2.GameId; Nation = nation; Action = expectedAction2 }
                         ))
                        "no action should be determined when move exceeds maximum distance"

                    // Assert: No charge commands dispatched
                    Expect.isEmpty dispatchedCommands "no movement fee is due for invalid moves"

                testPropertyWithConfig
                    { FsCheckConfig.defaultConfig with maxTest = 15 }
                    "moves of 4-6 spaces require payment (2M per additional space beyond 3)"
                <| fun (gameId: Guid) (nationIndex: int) (startSpaceIndex: int) (distanceRaw: int) ->

                    let game = gameId |> Id
                    let nations = [| "Austria"; "Britain"; "France"; "Germany"; "Italy"; "Russia" |]
                    let nation = nations.[abs nationIndex % nations.Length]
                    let startIndex = abs startSpaceIndex % allSpaces.Length
                    let dist = abs distanceRaw % 3 + 4 // 4, 5, or 6

                    // Setup: initialize rondel
                    let rondel, publishedEvents, dispatchedCommands = createRondel ()

                    let initCommand = { GameId = gameId |> Id; Nations = Set.ofArray nations }

                    rondel.Execute <| SetToStartingPositions initCommand
                    publishedEvents.Clear()

                    // First move: establish starting position
                    let startSpace = allSpaces.[startIndex]

                    let moveCommand1: MoveCommand =
                        { GameId = game; Nation = nation; Space = startSpace }

                    rondel.Execute <| Move moveCommand1
                    publishedEvents.Clear()
                    dispatchedCommands.Clear()

                    // Second move: 4-6 spaces forward (requires payment)
                    let targetIndex = (startIndex + dist) % allSpaces.Length
                    let targetSpace = allSpaces.[targetIndex]

                    let moveCommand2: MoveCommand =
                        { GameId = game; Nation = nation; Space = targetSpace }

                    // Assert: move command succeeds
                    rondel.Execute <| Move moveCommand2

                    // Assert: charge command dispatched with correct amount
                    let chargeCommands = getChargeCommands dispatchedCommands

                    Expect.hasLength
                        chargeCommands
                        1
                        (sprintf "exactly one charge command should be dispatched for %d-space move" dist)

                    let chargeCmd = chargeCommands.[0]

                    Expect.equal (Id.value chargeCmd.GameId) gameId "charge command should have correct GameId"

                    Expect.equal chargeCmd.Nation nation "charge command should have correct Nation"

                    let expectedAmount = Amount.unsafe ((dist - 3) * 2)

                    Expect.equal
                        chargeCmd.Amount
                        expectedAmount
                        (sprintf "charge for %d spaces should be %dM ((distance - 3) * 2)" dist ((dist - 3) * 2))

                    Expect.notEqual
                        (RondelBillingId.value chargeCmd.BillingId)
                        Guid.Empty
                        "charge command should have valid BillingId"

                    // Assert: NO ActionDetermined event (payment pending)
                    let actionDeterminedEvents =
                        publishedEvents
                        |> Seq.filter (function
                            | ActionDetermined _ -> true
                            | _ -> false)
                        |> Seq.toList

                    Expect.isEmpty actionDeterminedEvents "no action should be determined until payment is confirmed"

                    // Assert: NO MoveToActionSpaceRejected event (move not rejected)
                    let rejectedEvents =
                        publishedEvents
                        |> Seq.filter (function
                            | MoveToActionSpaceRejected _ -> true
                            | _ -> false)
                        |> Seq.toList

                    Expect.isEmpty rejectedEvents "move requiring payment should not be rejected"

                testCase "superseding pending paid move with another paid move voids old charge and rejects old move"
                <| fun _ ->
                    // Setup
                    let rondel, publishedEvents, dispatchedCommands = createRondel ()

                    let game = Guid.NewGuid() |> Id
                    let nations = [| "France" |]

                    // Initialize rondel
                    let initCommand = { GameId = game; Nations = Set.ofArray nations }

                    rondel.Execute <| SetToStartingPositions initCommand

                    publishedEvents.Clear()

                    // First move: Establish starting position (2 spaces, free)
                    let firstMoveCmd: MoveCommand =
                        { GameId = game; Nation = "France"; Space = Space.ProductionOne }

                    rondel.Execute <| Move firstMoveCmd

                    Expect.contains
                        publishedEvents
                        (ActionDetermined
                            { GameId = firstMoveCmd.GameId; Nation = "France"; Action = Action.Production })
                        "first move should determine action"

                    publishedEvents.Clear()
                    dispatchedCommands.Clear()

                    // Second move: 4 spaces (pending payment) - ProductionOne to ProductionTwo
                    let secondMove: MoveCommand =
                        { GameId = game; Nation = "France"; Space = Space.ProductionTwo }

                    rondel.Execute <| Move secondMove

                    let chargeCommands1 = getChargeCommands dispatchedCommands
                    Expect.hasLength chargeCommands1 1 "first pending move should dispatch charge"
                    let firstBillingId = chargeCommands1.[0].BillingId

                    Expect.equal chargeCommands1.[0].Amount (Amount.unsafe 2) "charge for 4 spaces should be 2M"

                    let actionEvents1 =
                        publishedEvents
                        |> Seq.filter (function
                            | ActionDetermined _ -> true
                            | _ -> false)
                        |> Seq.toList

                    Expect.isEmpty actionEvents1 "no action should be determined for pending move"

                    publishedEvents.Clear()
                    dispatchedCommands.Clear()

                    // Third move: 5 spaces (should supersede pending move) - ProductionOne to ManeuverTwo
                    let thirdMove: MoveCommand =
                        { GameId = game; Nation = "France"; Space = Space.ManeuverTwo }

                    rondel.Execute <| Move thirdMove

                    // Assert: old charge voided
                    let voidCommands = getVoidCommands dispatchedCommands
                    Expect.hasLength voidCommands 1 "exactly one void command should be dispatched"
                    Expect.equal voidCommands.[0].BillingId firstBillingId "should void the first billing"
                    Expect.equal voidCommands.[0].GameId game "void command should have correct GameId"

                    // Assert: old move rejected
                    Expect.contains
                        publishedEvents
                        (MoveToActionSpaceRejected
                            { GameId = secondMove.GameId; Nation = "France"; Space = secondMove.Space })
                        "first pending move should be rejected"

                    // Assert: new charge created
                    let chargeCommands2 = getChargeCommands dispatchedCommands
                    Expect.hasLength chargeCommands2 1 "exactly one new charge should be dispatched"
                    let secondCharge = chargeCommands2.[0]

                    Expect.equal secondCharge.Amount (Amount.unsafe 4) "charge for 5 spaces should be 4M"

                    Expect.notEqual secondCharge.BillingId firstBillingId "new charge should have different billing id"
                    Expect.equal secondCharge.GameId game "new charge should have correct GameId"
                    Expect.equal secondCharge.Nation "France" "new charge should have correct Nation"

                    // Assert: no action determined (new move still pending payment)
                    let actionEvents2 =
                        publishedEvents
                        |> Seq.filter (function
                            | ActionDetermined _ -> true
                            | _ -> false)
                        |> Seq.toList

                    Expect.isEmpty actionEvents2 "no action should be determined for new pending move"

                testCase "superseding pending paid move with free move voids charge and completes immediately"
                <| fun _ ->
                    // Setup
                    let rondel, publishedEvents, dispatchedCommands = createRondel ()
                    let gameId = Guid.NewGuid() |> Id
                    let nations = [| "Germany" |]

                    // Initialize rondel
                    let initCommand = { GameId = gameId; Nations = Set.ofArray nations }

                    rondel.Execute <| SetToStartingPositions initCommand

                    publishedEvents.Clear()

                    // First move: Establish starting position (3 spaces, free)
                    let firstMoveCmd: MoveCommand =
                        { GameId = gameId; Nation = "Germany"; Space = Space.ManeuverOne }

                    rondel.Execute <| Move firstMoveCmd

                    Expect.contains
                        publishedEvents
                        (ActionDetermined { GameId = firstMoveCmd.GameId; Nation = "Germany"; Action = Action.Maneuver })
                        "first move should determine action"

                    publishedEvents.Clear()
                    dispatchedCommands.Clear()

                    // Second move: 5 spaces (pending payment) - ManeuverOne to Investor
                    let secondMove: MoveCommand =
                        { GameId = gameId; Nation = "Germany"; Space = Space.Investor }

                    rondel.Execute <| Move secondMove

                    let chargeCommands1 = getChargeCommands dispatchedCommands
                    Expect.hasLength chargeCommands1 1 "first pending move should dispatch charge"
                    let firstBillingId = chargeCommands1.[0].BillingId

                    Expect.equal chargeCommands1.[0].Amount (Amount.unsafe 4) "charge for 5 spaces should be 4M"

                    let actionEvents1 =
                        publishedEvents
                        |> Seq.filter (function
                            | ActionDetermined _ -> true
                            | _ -> false)
                        |> Seq.toList

                    Expect.isEmpty actionEvents1 "no action should be determined for pending move"

                    publishedEvents.Clear()
                    dispatchedCommands.Clear()

                    // Third move: 2 spaces (free, should supersede and complete immediately) - ManeuverOne to Factory
                    let thirdMove: MoveCommand =
                        { GameId = gameId; Nation = "Germany"; Space = Space.Factory }

                    rondel.Execute <| Move thirdMove

                    // Assert: old charge voided
                    let voidCommands = getVoidCommands dispatchedCommands
                    Expect.hasLength voidCommands 1 "exactly one void command should be dispatched"
                    Expect.equal voidCommands.[0].BillingId firstBillingId "should void the first billing"
                    Expect.equal voidCommands.[0].GameId gameId "void command should have correct GameId"

                    // Assert: old move rejected
                    Expect.contains
                        publishedEvents
                        (MoveToActionSpaceRejected
                            { GameId = secondMove.GameId; Nation = "Germany"; Space = secondMove.Space })
                        "first pending move should be rejected"

                    // Assert: no new charge (free move)
                    let chargeCommands2 = getChargeCommands dispatchedCommands
                    Expect.isEmpty chargeCommands2 "free move should not dispatch charge command"

                    // Assert: action determined for new move (free move completes immediately)
                    Expect.contains
                        publishedEvents
                        (ActionDetermined { GameId = thirdMove.GameId; Nation = "Germany"; Action = Action.Factory })
                        "free move should determine action immediately despite superseding" ]
          testList
              "onInvoicePaid"
              [ testCase "completes pending movement and publishes ActionDetermined event"
                <| fun _ ->
                    // Setup: create mocks
                    let rondel, publishedEvents, dispatchedCommands = createRondel ()

                    let gameId = Guid.NewGuid() |> Id
                    let nations = [| "Austria" |]

                    // Setup: initialize rondel
                    SetToStartingPositions { GameId = gameId; Nations = Set.ofArray nations }
                    |> rondel.Execute

                    // Setup: establish starting position
                    let moveOnRondel: MoveCommand =
                        { GameId = gameId; Nation = "Austria"; Space = Space.ManeuverOne }

                    Move moveOnRondel |> rondel.Execute

                    // Setup: make paid move (5 spaces - ManeuverOne to Investor)
                    Move { moveOnRondel with Space = Space.Investor } |> rondel.Execute

                    let billingId =
                        dispatchedCommands
                        |> Seq.choose (function
                            | ChargeMovement chargeCmd -> Some chargeCmd.BillingId
                            | _ -> None)
                        |> Seq.tryHead
                        |> Option.defaultWith (fun () -> failwith "charge command not dispatched")

                    // Execute: process payment confirmation
                    InvoicePaid { GameId = gameId; BillingId = billingId } |> rondel.Handle

                    // Assert: ActionDetermined event published for target space
                    Expect.contains
                        publishedEvents
                        (ActionDetermined { GameId = gameId; Nation = "Austria"; Action = Action.Investor })
                        "ActionDetermined event should be published after payment confirmation"

                    // Assert: subsequent move confirms position was updated correctly
                    Move { moveOnRondel with Space = Space.Import } |> rondel.Execute

                    Expect.contains
                        publishedEvents
                        (ActionDetermined { GameId = gameId; Nation = "Austria"; Action = Action.Import })
                        "subsequent move from Investor to Import (1 space) should succeed, confirming position was updated"

                testCase "paying twice for same movement only completes it once"
                <| fun _ ->
                    // Setup: create mocks
                    let rondel, publishedEvents, dispatchedCommands = createRondel ()

                    let gameId = Guid.NewGuid() |> Id
                    let nations = [| "Austria" |]

                    // Setup: initialize rondel
                    SetToStartingPositions { GameId = gameId; Nations = Set.ofArray nations }
                    |> rondel.Execute

                    // Setup: establish starting position
                    let moveOnRondel: MoveCommand =
                        { GameId = gameId; Nation = "Austria"; Space = Space.ManeuverOne }

                    Move moveOnRondel |> rondel.Execute

                    // Setup: make paid move (5 spaces - ManeuverOne to Investor)
                    Move { moveOnRondel with Space = Space.Investor } |> rondel.Execute

                    let billingId =
                        dispatchedCommands
                        |> Seq.choose (function
                            | ChargeMovement chargeCmd -> Some chargeCmd.BillingId
                            | _ -> None)
                        |> Seq.tryHead
                        |> Option.defaultWith (fun () -> failwith "charge command not dispatched")

                    // Execute: process payment confirmation (first time)
                    InvoicePaid { GameId = gameId; BillingId = billingId } |> rondel.Handle

                    // Execute: process same payment confirmation again (second time)
                    InvoicePaid { GameId = gameId; BillingId = billingId } |> rondel.Handle

                    // Assert: ActionDetermined for Investor appears only once (not duplicated)
                    let investorActionCount =
                        publishedEvents
                        |> Seq.filter (function
                            | ActionDetermined e when e.Action = Action.Investor -> true
                            | _ -> false)
                        |> Seq.length

                    Expect.equal investorActionCount 1 "paying twice should only complete movement once"

                    // Assert: subsequent move confirms position was updated correctly
                    Move { moveOnRondel with Space = Space.Import } |> rondel.Execute

                    Expect.contains
                        publishedEvents
                        (ActionDetermined { GameId = gameId; Nation = "Austria"; Action = Action.Import })
                        "subsequent move from Investor should succeed"

                testCase "payment for cancelled movement is ignored"
                <| fun _ ->
                    // Setup: create mocks
                    let rondel, publishedEvents, dispatchedCommands = createRondel ()

                    let gameId = Guid.NewGuid() |> Id
                    let nations = [| "France" |]

                    // Setup: initialize rondel
                    SetToStartingPositions { GameId = gameId; Nations = Set.ofArray nations }
                    |> rondel.Execute

                    // Setup: establish starting position
                    let moveOnRondel: MoveCommand =
                        { GameId = gameId; Nation = "France"; Space = Space.ProductionOne }

                    Move moveOnRondel |> rondel.Execute

                    // Setup: make paid move (4 spaces - ProductionOne to ProductionTwo)
                    Move { moveOnRondel with Space = Space.ProductionTwo } |> rondel.Execute

                    let voidedBillingId =
                        dispatchedCommands
                        |> Seq.choose (function
                            | ChargeMovement chargeCmd -> Some chargeCmd.BillingId
                            | _ -> None)
                        |> Seq.tryHead
                        |> Option.defaultWith (fun () -> failwith "charge command not dispatched")

                    // Setup: supersede with free move (2 spaces - ProductionOne to Taxation)
                    // This voids the previous charge and rejects the pending move
                    Move { moveOnRondel with Space = Space.Taxation } |> rondel.Execute

                    // Verify France is at Taxation after free move
                    Expect.contains
                        publishedEvents
                        (ActionDetermined { GameId = gameId; Nation = "France"; Action = Action.Taxation })
                        "free move should have completed to Taxation"

                    // Execute: payment arrives for the voided charge
                    InvoicePaid { GameId = gameId; BillingId = voidedBillingId } |> rondel.Handle

                    // Assert: current position is Taxation (from free move), not ProductionTwo
                    Move { moveOnRondel with Space = Space.Factory } |> rondel.Execute

                    Expect.contains
                        publishedEvents
                        (ActionDetermined { GameId = gameId; Nation = "France"; Action = Action.Factory })
                        "subsequent move from Taxation should succeed, confirming position was not changed by late payment" ]
          testList
              "onInvoicePaymentFailed"
              [ testCase "payment failure removes pending movement and publishes rejection"
                <| fun _ ->
                    // Setup: create mocks
                    let rondel, publishedEvents, dispatchedCommands = createRondel ()

                    let gameId = Guid.NewGuid() |> Id
                    let nations = [| "Austria" |]

                    // Setup: initialize rondel
                    SetToStartingPositions { GameId = gameId; Nations = Set.ofArray nations }
                    |> rondel.Execute

                    // Setup: establish starting position
                    let moveOnRondel: MoveCommand =
                        { GameId = gameId; Nation = "Austria"; Space = Space.ManeuverOne }

                    Move moveOnRondel |> rondel.Execute

                    // Setup: make paid move (5 spaces - ManeuverOne to Investor)
                    Move { moveOnRondel with Space = Space.Investor } |> rondel.Execute

                    let billingId =
                        dispatchedCommands
                        |> Seq.choose (function
                            | ChargeMovement chargeCmd -> Some chargeCmd.BillingId
                            | _ -> None)
                        |> Seq.tryHead
                        |> Option.defaultWith (fun () -> failwith "charge command not dispatched")

                    // Execute: process payment failure
                    InvoicePaymentFailed { GameId = gameId; BillingId = billingId } |> rondel.Handle

                    // Assert: MoveToActionSpaceRejected event published for target space
                    Expect.contains
                        publishedEvents
                        (MoveToActionSpaceRejected { GameId = gameId; Nation = "Austria"; Space = Space.Investor })
                        "MoveToActionSpaceRejected event should be published after payment failure"

                    // Assert: no action determined (failed payment does not complete movement)
                    Expect.isFalse
                        (publishedEvents
                         |> Seq.exists (function
                             | ActionDetermined action -> action.Action = Action.Investor
                             | _ -> false))
                        "ActionDetermined event should not be published after rejection"

                    // Assert: subsequent move from original position succeeds (confirms pending removed and position unchanged)
                    Move { moveOnRondel with Space = Space.Factory } |> rondel.Execute

                    Expect.contains
                        publishedEvents
                        (ActionDetermined { GameId = gameId; Nation = "Austria"; Action = Action.Factory })
                        "subsequent move from ManeuverOne to Factory (2 spaces) should succeed, confirming position was not changed by failed payment"

                testCase "processing payment failure twice only removes pending once"
                <| fun _ ->
                    // Setup: create mocks
                    let rondel, publishedEvents, dispatchedCommands = createRondel ()

                    let gameId = Guid.NewGuid() |> Id
                    let nations = [| "Austria" |]

                    // Setup: initialize rondel
                    SetToStartingPositions { GameId = gameId; Nations = Set.ofArray nations }
                    |> rondel.Execute

                    // Setup: establish starting position
                    let moveOnRondel: MoveCommand =
                        { GameId = gameId; Nation = "Austria"; Space = Space.ManeuverOne }

                    Move moveOnRondel |> rondel.Execute

                    // Setup: make paid move (4 spaces - ManeuverOne to ManeuverTwo)
                    Move { moveOnRondel with Space = Space.ManeuverTwo } |> rondel.Execute

                    let billingId =
                        dispatchedCommands
                        |> Seq.choose (function
                            | ChargeMovement chargeCmd -> Some chargeCmd.BillingId
                            | _ -> None)
                        |> Seq.tryHead
                        |> Option.defaultWith (fun () -> failwith "charge command not dispatched")

                    // Execute: process payment failure (first time)
                    InvoicePaymentFailed { GameId = gameId; BillingId = billingId } |> rondel.Handle

                    // Execute: process same payment failure again (second time)
                    InvoicePaymentFailed { GameId = gameId; BillingId = billingId } |> rondel.Handle

                    // Assert: MoveToActionSpaceRejected for ManeuverTwo appears only once (not duplicated)
                    Expect.hasLength
                        (publishedEvents
                         |> Seq.filter (function
                             | MoveToActionSpaceRejected e when e.Space = Space.ManeuverTwo -> true
                             | _ -> false)
                         |> Seq.toList)
                        1
                        "processing payment failure twice should only reject movement once"

                    // Assert: subsequent move confirms position remained at ManeuverOne
                    Move { moveOnRondel with Space = Space.Factory } |> rondel.Execute

                    Expect.contains
                        publishedEvents
                        (ActionDetermined { GameId = gameId; Nation = "Austria"; Action = Action.Factory })
                        "subsequent move from ManeuverOne should succeed"

                testCase "payment failure for voided charge is ignored"
                <| fun _ ->
                    // Setup: create mocks
                    let rondel, publishedEvents, dispatchedCommands = createRondel ()

                    let gameId = Guid.NewGuid() |> Id
                    let nations = [| "France" |]

                    // Setup: initialize rondel
                    SetToStartingPositions { GameId = gameId; Nations = Set.ofArray nations }
                    |> rondel.Execute

                    // Setup: establish starting position
                    let moveOnRondel: MoveCommand =
                        { GameId = gameId; Nation = "France"; Space = Space.ProductionOne }

                    Move moveOnRondel |> rondel.Execute

                    // Setup: make paid move (4 spaces - ProductionOne to ProductionTwo)
                    Move { moveOnRondel with Space = Space.ProductionTwo } |> rondel.Execute

                    let voidedBillingId =
                        dispatchedCommands
                        |> Seq.choose (function
                            | ChargeMovement chargeCmd -> Some chargeCmd.BillingId
                            | _ -> None)
                        |> Seq.tryHead
                        |> Option.defaultWith (fun () -> failwith "charge command not dispatched")

                    // Setup: supersede with free move (2 spaces - ProductionOne to Taxation)
                    // This voids the previous charge and rejects the pending move
                    Move { moveOnRondel with Space = Space.Taxation } |> rondel.Execute

                    // Verify France is at Taxation after free move
                    Expect.contains
                        publishedEvents
                        (ActionDetermined { GameId = gameId; Nation = "France"; Action = Action.Taxation })
                        "free move should have completed to Taxation"

                    // Execute: payment failure arrives for the voided charge
                    InvoicePaymentFailed { GameId = gameId; BillingId = voidedBillingId }
                    |> rondel.Handle

                    // Assert: no additional rejection event (already rejected during voiding)
                    Expect.hasLength
                        (publishedEvents
                         |> Seq.filter (function
                             | MoveToActionSpaceRejected e when e.Space = Space.ProductionTwo -> true
                             | _ -> false)
                         |> Seq.toList)
                        1
                        "ProductionTwo should only be rejected once (during voiding, not again on payment failure)"

                    // Assert: current position is Taxation (from free move), not ProductionTwo
                    Move { moveOnRondel with Space = Space.Factory } |> rondel.Execute

                    Expect.contains
                        publishedEvents
                        (ActionDetermined { GameId = gameId; Nation = "France"; Action = Action.Factory })
                        "subsequent move from Taxation should succeed, confirming position was not changed by late payment failure"

                testCase "payment failure after successful payment is ignored"
                <| fun _ ->
                    // Setup: create mocks
                    let rondel, publishedEvents, dispatchedCommands = createRondel ()

                    let gameId = Guid.NewGuid() |> Id
                    let nations = [| "Britain" |]

                    // Setup: initialize rondel
                    SetToStartingPositions { GameId = gameId; Nations = Set.ofArray nations }
                    |> rondel.Execute

                    // Setup: establish starting position
                    let moveOnRondel: MoveCommand =
                        { GameId = gameId; Nation = "Britain"; Space = Space.Import }

                    Move moveOnRondel |> rondel.Execute

                    // Setup: make paid move (5 spaces - Import to ProductionTwo)
                    Move { moveOnRondel with Space = Space.ProductionTwo } |> rondel.Execute

                    let billingId =
                        dispatchedCommands
                        |> Seq.choose (function
                            | ChargeMovement chargeCmd -> Some chargeCmd.BillingId
                            | _ -> None)
                        |> Seq.tryHead
                        |> Option.defaultWith (fun () -> failwith "charge command not dispatched")

                    // Setup: complete payment successfully
                    InvoicePaid { GameId = gameId; BillingId = billingId } |> rondel.Handle

                    // Verify Britain is at ProductionTwo after successful payment
                    Expect.contains
                        publishedEvents
                        (ActionDetermined { GameId = gameId; Nation = "Britain"; Action = Action.Production })
                        "paid move should have completed to ProductionTwo"

                    // Execute: payment failure arrives after successful payment
                    InvoicePaymentFailed { GameId = gameId; BillingId = billingId } |> rondel.Handle

                    // Assert: no rejection event (payment already succeeded)
                    Expect.isEmpty
                        (publishedEvents
                         |> Seq.filter (function
                             | MoveToActionSpaceRejected e when e.Space = Space.ProductionTwo -> true
                             | _ -> false))
                        "ProductionTwo should not be rejected (payment already succeeded)"

                    // Assert: position remains at ProductionTwo
                    Move { moveOnRondel with Space = Space.ManeuverTwo } |> rondel.Execute

                    Expect.contains
                        publishedEvents
                        (ActionDetermined { GameId = gameId; Nation = "Britain"; Action = Action.Maneuver })
                        "subsequent move from ProductionTwo should succeed, confirming position was not changed by late payment failure" ]
          testList
              "getNationPositions"
              [ testCase "returns None for unknown game"
                <| fun _ ->
                    let rondel, _, _ = createRondel ()
                    let gameId = Guid.NewGuid() |> Id

                    let result = rondel.GetNationPositions { GameId = gameId }

                    Expect.isNone result "Should return None for unknown game"

                testCase "returns positions for initialized game with no moves"
                <| fun _ ->
                    let rondel, _, _ = createRondel ()
                    let gameId = Guid.NewGuid() |> Id
                    let nations = Set.ofList [ "France"; "Germany" ]

                    rondel.Execute <| SetToStartingPositions { GameId = gameId; Nations = nations }

                    let result = rondel.GetNationPositions { GameId = gameId }

                    Expect.isSome result "Should return Some for initialized game"
                    let r = result.Value
                    Expect.equal r.GameId gameId "GameId should match"
                    Expect.equal r.Positions.Length 2 "Should have 2 nations"

                    let france = r.Positions |> List.find (fun p -> p.Nation = "France")
                    Expect.isNone france.CurrentSpace "France should have no current space"
                    Expect.isNone france.PendingSpace "France should have no pending space"

                testCase "returns current position after free move"
                <| fun _ ->
                    let rondel, _, _ = createRondel ()
                    let gameId = Guid.NewGuid() |> Id

                    rondel.Execute
                    <| SetToStartingPositions { GameId = gameId; Nations = Set.ofList [ "France" ] }

                    rondel.Execute
                    <| Move { GameId = gameId; Nation = "France"; Space = Space.Factory }

                    let result = rondel.GetNationPositions { GameId = gameId }

                    Expect.isSome result "Should return Some"
                    let france = result.Value.Positions |> List.find (fun p -> p.Nation = "France")
                    Expect.equal france.CurrentSpace (Some Space.Factory) "France should be at Factory"
                    Expect.isNone france.PendingSpace "France should have no pending space"

                testCase "returns pending space for paid move awaiting payment"
                <| fun _ ->
                    let rondel, _, _ = createRondel ()
                    let gameId = Guid.NewGuid() |> Id

                    rondel.Execute
                    <| SetToStartingPositions { GameId = gameId; Nations = Set.ofList [ "Austria" ] }

                    // First move to establish position
                    rondel.Execute
                    <| Move { GameId = gameId; Nation = "Austria"; Space = Space.Investor }
                    // Second move: 5 spaces (paid) - Investor to Factory
                    rondel.Execute
                    <| Move { GameId = gameId; Nation = "Austria"; Space = Space.Factory }

                    let result = rondel.GetNationPositions { GameId = gameId }

                    Expect.isSome result "Should return Some"
                    let austria = result.Value.Positions |> List.find (fun p -> p.Nation = "Austria")
                    Expect.equal austria.CurrentSpace (Some Space.Investor) "Austria should still be at Investor"
                    Expect.equal austria.PendingSpace (Some Space.Factory) "Austria should have pending move to Factory" ]
          testList
              "getRondelOverview"
              [ testCase "returns None for unknown game"
                <| fun _ ->
                    let rondel, _, _ = createRondel ()
                    let gameId = Guid.NewGuid() |> Id

                    let result = rondel.GetRondelOverview { GameId = gameId }

                    Expect.isNone result "Should return None for unknown game"

                testCase "returns overview for initialized game"
                <| fun _ ->
                    let rondel, _, _ = createRondel ()
                    let gameId = Guid.NewGuid() |> Id
                    let nations = Set.ofList [ "France"; "Germany"; "Austria" ]

                    rondel.Execute <| SetToStartingPositions { GameId = gameId; Nations = nations }

                    let result = rondel.GetRondelOverview { GameId = gameId }

                    Expect.isSome result "Should return Some for initialized game"
                    let r = result.Value
                    Expect.equal r.GameId gameId "GameId should match"
                    Expect.equal (r.NationNames |> List.sort) [ "Austria"; "France"; "Germany" ] "Nations should match" ] ]
