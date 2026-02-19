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
        [ testList
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
