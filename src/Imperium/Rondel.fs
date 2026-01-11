namespace Imperium

module Rondel =
    open Imperium.Primitives
    open FsToolkit.ErrorHandling

    // ──────────────────────────────────────────────────────────────────────────
    // Value Types & Enumerations
    // ──────────────────────────────────────────────────────────────────────────

    /// Opaque identifier linking a rondel movement to its accounting charge.
    [<Struct>]
    type RondelBillingId = private RondelBillingId of Id

    module RondelBillingId =
        let create = Id.createMap RondelBillingId
        let newId () = Id.newId () |> RondelBillingId
        let value (RondelBillingId g) = g |> Id.value
        let toString (RondelBillingId g) = g |> Id.toString
        let tryParse = Id.tryParseMap RondelBillingId

    /// The six distinct actions a nation can perform on the rondel.
    [<RequireQualifiedAccess>]
    type Action =
        | Investor
        | Import
        | Production
        | Maneuver
        | Taxation
        | Factory

    module Action =
        let toString action =
            match action with
            | Action.Investor -> "Investor"
            | Action.Import -> "Import"
            | Action.Production -> "Production"
            | Action.Maneuver -> "Maneuver"
            | Action.Taxation -> "Taxation"
            | Action.Factory -> "Factory"

    /// The eight spaces on the rondel wheel, arranged clockwise.
    [<RequireQualifiedAccess>]
    type Space =
        | Investor
        | Import
        | ProductionOne
        | ManeuverOne
        | Taxation
        | Factory
        | ProductionTwo
        | ManeuverTwo

    module Space =
        /// Spaces in clockwise board order for distance calculation.
        let private spacesInOrder =
            [| Space.Investor
               Space.Import
               Space.ProductionOne
               Space.ManeuverOne
               Space.Taxation
               Space.Factory
               Space.ProductionTwo
               Space.ManeuverTwo |]

        /// Calculate clockwise distance between two spaces.
        let distance fromSpace toSpace =
            let fromIndex = Array.findIndex ((=) fromSpace) spacesInOrder
            let toIndex = Array.findIndex ((=) toSpace) spacesInOrder

            if toIndex >= fromIndex then
                toIndex - fromIndex
            else
                (Array.length spacesInOrder - fromIndex) + toIndex

        let toString space =
            match space with
            | Space.Investor -> "Investor"
            | Space.Import -> "Import"
            | Space.ProductionOne -> "ProductionOne"
            | Space.ManeuverOne -> "ManeuverOne"
            | Space.Taxation -> "Taxation"
            | Space.Factory -> "Factory"
            | Space.ProductionTwo -> "ProductionTwo"
            | Space.ManeuverTwo -> "ManeuverTwo"

        let fromString s =
            match s with
            | "Investor" -> Ok Space.Investor
            | "Import" -> Ok Space.Import
            | "ProductionOne" -> Ok Space.ProductionOne
            | "ManeuverOne" -> Ok Space.ManeuverOne
            | "Taxation" -> Ok Space.Taxation
            | "Factory" -> Ok Space.Factory
            | "ProductionTwo" -> Ok Space.ProductionTwo
            | "ManeuverTwo" -> Ok Space.ManeuverTwo
            | _ -> Error $"Invalid rondel space: {s}"

        /// Maps a rondel space to its corresponding action.
        let toAction space =
            match space with
            | Space.Investor -> Action.Investor
            | Space.Import -> Action.Import
            | Space.ProductionOne
            | Space.ProductionTwo -> Action.Production
            | Space.ManeuverOne
            | Space.ManeuverTwo -> Action.Maneuver
            | Space.Taxation -> Action.Taxation
            | Space.Factory -> Action.Factory

    // ──────────────────────────────────────────────────────────────────────────
    // Domain State
    // ──────────────────────────────────────────────────────────────────────────

    /// Persistent state for a game's rondel.
    type RondelState =
        {
            GameId: Id
            /// Maps nation name to current position. None indicates starting position.
            NationPositions: Map<string, Space option>
            /// Maps nation name to pending paid movement awaiting payment.
            PendingMovements: Map<string, PendingMovement>
        }

    /// A movement awaiting payment confirmation from Accounting.
    and PendingMovement =
        { Nation: string
          TargetSpace: Space
          BillingId: RondelBillingId }

    // ──────────────────────────────────────────────────────────────────────────
    // Commands
    // ──────────────────────────────────────────────────────────────────────────

    /// Union of all rondel commands for routing and dispatch.
    type RondelCommand =
        | SetToStartingPositions of SetToStartingPositionsCommand
        | Move of MoveCommand

    /// Initialize rondel for a game with the participating nations.
    and SetToStartingPositionsCommand = { GameId: Id; Nations: Set<string> }

    /// Request to move a nation to a specific space on the rondel.
    and MoveCommand =
        { GameId: Id
          Nation: string
          Space: Space }

    // ──────────────────────────────────────────────────────────────────────────
    // Events
    // ──────────────────────────────────────────────────────────────────────────

    /// Integration events published by the Rondel domain.
    type RondelEvent =
        | PositionedAtStart of PositionedAtStartEvent
        | ActionDetermined of ActionDeterminedEvent
        | MoveToActionSpaceRejected of MoveToActionSpaceRejectedEvent

    /// Published when nations are positioned at starting positions.
    and PositionedAtStartEvent = { GameId: Id }

    /// Published when a nation successfully completes a move.
    and ActionDeterminedEvent =
        { GameId: Id
          Nation: string
          Action: Action }

    /// Published when a nation's movement is rejected.
    and MoveToActionSpaceRejectedEvent =
        { GameId: Id
          Nation: string
          Space: Space }

    // ──────────────────────────────────────────────────────────────────────────
    // Outbound Commands (to other bounded contexts)
    // ──────────────────────────────────────────────────────────────────────────

    /// Union of all outbound commands dispatched to other bounded contexts.
    type RondelOutboundCommand =
        | ChargeMovement of ChargeMovementOutboundCommand
        | VoidCharge of VoidChargeOutboundCommand

    /// Command to charge a nation for paid rondel movement (4-6 spaces).
    and ChargeMovementOutboundCommand =
        { GameId: Id
          Nation: string
          Amount: Amount
          BillingId: RondelBillingId }

    /// Command to void a previously initiated charge before payment completion.
    and VoidChargeOutboundCommand =
        { GameId: Id
          BillingId: RondelBillingId }

    // ──────────────────────────────────────────────────────────────────────────
    // Incoming Events (from other bounded contexts)
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Inbound events from Accounting domain that affect Rondel state.
    /// These events are received after the Rondel dispatches charge commands
    /// for paid movements (4-6 spaces) and represent payment outcomes.
    /// </summary>
    type RondelInboundEvent =
        | InvoicePaid of InvoicePaidInboundEvent
        | InvoicePaymentFailed of InvoicePaymentFailedInboundEvent

    /// <summary>
    /// Payment confirmation received from Accounting domain.
    /// Indicates the nation successfully paid for a rondel movement,
    /// allowing the pending movement to complete.
    /// </summary>
    and InvoicePaidInboundEvent =
        {
            /// The game in which the payment was made.
            GameId: Id
            /// Correlates this payment to the pending movement awaiting confirmation.
            BillingId: RondelBillingId
        }

    /// <summary>
    /// Payment failure notification from Accounting domain.
    /// Indicates the nation could not pay for a rondel movement,
    /// causing the pending movement to be rejected.
    /// </summary>
    and InvoicePaymentFailedInboundEvent =
        {
            /// The game in which the payment failed.
            GameId: Id
            /// Correlates this failure to the pending movement to be rejected.
            BillingId: RondelBillingId
        }

    // ──────────────────────────────────────────────────────────────────────────
    // Dependencies
    // ──────────────────────────────────────────────────────────────────────────

    /// Load rondel state by GameId. Returns None if game not initialized.
    type LoadRondelState = Id -> RondelState option

    /// Save rondel state. Returns Error if persistence fails.
    type SaveRondelState = RondelState -> Result<unit, string>

    /// Publish rondel domain events to the event bus.
    type PublishRondelEvent = RondelEvent -> unit

    /// Dispatch outbound commands to other bounded contexts (e.g., Accounting).
    /// Infrastructure handles conversion to contract types and actual dispatch.
    type DispatchOutboundCommand = RondelOutboundCommand -> Result<unit, string>

    /// Unified dependencies for all Rondel handlers.
    /// All handlers receive the same dependencies record for consistency,
    /// even if some handlers don't use all dependencies.
    type RondelDependencies =
        { Load: LoadRondelState
          Save: SaveRondelState
          Publish: PublishRondelEvent
          Dispatch: DispatchOutboundCommand }

    // ──────────────────────────────────────────────────────────────────────────
    // Transformations (Contract <-> Domain)
    // ──────────────────────────────────────────────────────────────────────────

    /// Transforms Contract SetToStartingPositionsCommand to Domain type.
    module SetToStartingPositionsCommand =
        /// Validate and transform Contract command to Domain command.
        let fromContract (command: Contract.Rondel.SetToStartingPositionsCommand) =
            result {
                let! id = Id.create command.GameId
                let nations = Set.ofArray command.Nations

                if Set.isEmpty nations then
                    return! Error "Starting positions require at least one nation."
                else
                    return { GameId = id; Nations = nations }
            }

    /// Transforms Contract MoveCommand to Domain type.
    module MoveCommand =
        /// Validate and transform Contract command to Domain command.
        let fromContract (command: Contract.Rondel.MoveCommand) : Result<MoveCommand, string> =
            result {
                let! id = Id.create command.GameId
                let! space = Space.fromString command.Space

                return
                    { GameId = id
                      Nation = command.Nation
                      Space = space }
            }

    /// Transforms Domain RondelEvent to Contract type for publication.
    module RondelEvent =
        /// Transform Domain event to Contract event.
        let toContract event =
            match event with
            | PositionedAtStart e -> Contract.Rondel.PositionedAtStart { GameId = Id.value e.GameId }
            | ActionDetermined e ->
                Contract.Rondel.ActionDetermined
                    { GameId = Id.value e.GameId
                      Nation = e.Nation
                      Action = Action.toString e.Action }
            | MoveToActionSpaceRejected e ->
                Contract.Rondel.MoveToActionSpaceRejected
                    { GameId = Id.value e.GameId
                      Nation = e.Nation
                      Space = Space.toString e.Space }

    /// Transforms Domain ChargeMovementOutboundCommand to Accounting contract type.
    module ChargeMovementOutboundCommand =
        /// Convert domain charge command to Accounting contract for dispatch.
        let toContract (cmd: ChargeMovementOutboundCommand) : Contract.Accounting.ChargeNationForRondelMovementCommand =
            { GameId = Id.value cmd.GameId
              Nation = cmd.Nation
              Amount = cmd.Amount
              BillingId = RondelBillingId.value cmd.BillingId }

    /// Transforms Domain VoidChargeOutboundCommand to Accounting contract type.
    module VoidChargeOutboundCommand =
        /// Convert domain void command to Accounting contract for dispatch.
        let toContract (cmd: VoidChargeOutboundCommand) : Contract.Accounting.VoidRondelChargeCommand =
            { GameId = Id.value cmd.GameId
              BillingId = RondelBillingId.value cmd.BillingId }

    /// Transforms Domain PendingMovement to/from Contract type for persistence.
    module PendingMovement =
        /// Convert domain pending movement to serializable contract representation.
        let toContract (pending: PendingMovement) : Contract.Rondel.PendingMovement =
            { Nation = pending.Nation
              TargetSpace = Space.toString pending.TargetSpace
              BillingId = pending.BillingId |> RondelBillingId.value }

        /// Reconstruct domain pending movement from contract representation.
        let fromContract (pending: Contract.Rondel.PendingMovement) : Result<PendingMovement, string> =
            result {
                let! space = Space.fromString pending.TargetSpace
                let! billingId = RondelBillingId.create pending.BillingId

                return
                    { Nation = pending.Nation
                      TargetSpace = space
                      BillingId = billingId }
            }

    /// Transforms Domain RondelState to/from Contract type for persistence.
    module RondelState =
        /// Convert domain state to serializable contract representation.
        let toContract (state: RondelState) : Contract.Rondel.RondelState =
            { GameId = state.GameId |> Id.value
              NationPositions =
                state.NationPositions
                |> Map.map (fun _ position -> position |> Option.map Space.toString)
              PendingMovements =
                state.PendingMovements
                |> Map.map (fun _ pending -> PendingMovement.toContract pending) }

        /// Reconstruct domain state from contract representation.
        let fromContract (state: Contract.Rondel.RondelState) : Result<RondelState, string> =
            let nationPositions =
                result {
                    let mutable positions = Map.empty

                    for nation, position in state.NationPositions |> Map.toSeq do
                        match position with
                        | None -> positions <- positions |> Map.add nation None
                        | Some value ->
                            let! space = Space.fromString value
                            positions <- positions |> Map.add nation (Some space)

                    return positions
                }

            let pendingMovements =
                result {
                    let mutable movements = Map.empty

                    for nation, pending in state.PendingMovements |> Map.toSeq do
                        if pending.Nation <> nation then
                            return! Error $"Pending movement nation mismatch for {nation}."
                        else
                            let! mapped = PendingMovement.fromContract pending
                            movements <- movements |> Map.add nation mapped

                    return movements
                }

            result {
                let! gameId = Id.create state.GameId
                let! positions = nationPositions
                let! pending = pendingMovements

                return
                    { GameId = gameId
                      NationPositions = positions
                      PendingMovements = pending }
            }

    /// Transforms Contract RondelInvoicePaid to Domain InvoicePaidInboundEvent.
    module InvoicePaidInboundEvent =
        /// Validate and transform Contract event to Domain event.
        let fromContract (event: Contract.Accounting.RondelInvoicePaid) : Result<InvoicePaidInboundEvent, string> =
            result {
                let! gameId = Id.create event.GameId
                let! billingId = RondelBillingId.create event.BillingId

                return
                    { GameId = gameId
                      BillingId = billingId }
            }

    /// Transforms Contract RondelInvoicePaymentFailed to Domain InvoicePaymentFailedInboundEvent.
    module InvoicePaymentFailedInboundEvent =
        /// Validate and transform Contract event to Domain event.
        let fromContract
            (event: Contract.Accounting.RondelInvoicePaymentFailed)
            : Result<InvoicePaymentFailedInboundEvent, string> =
            result {
                let! gameId = Id.create event.GameId
                let! billingId = RondelBillingId.create event.BillingId

                return
                    { GameId = gameId
                      BillingId = billingId }
            }

    // ──────────────────────────────────────────────────────────────────────────
    // Handlers (Internal Types)
    // ──────────────────────────────────────────────────────────────────────────

    /// Materialize side effects from pure command results.
    /// Sequences IO operations (save state → publish events → dispatch commands) with Result.bind for error propagation.
    /// Used by all command handlers to apply the results of pure business logic execution.
    let internal materialize deps state events commands =
        let save = deps.Save
        let publish = deps.Publish
        let dispatch = deps.Dispatch

        let saveState state =
            match state with
            | Some s -> save s
            | None -> Ok()

        let publishEvents events = events |> List.iter publish |> Ok

        let executeOutboundCommands commands =
            (Ok(), commands)
            ||> List.fold (fun state cmd -> state |> Result.bind (fun () -> dispatch cmd))

        saveState state
        |> Result.bind (fun () -> publishEvents events)
        |> Result.bind (fun () -> executeOutboundCommands commands)
        |> Result.defaultWith (fun e -> failwith $"Failed to materialize side effects: {e}")

    /// Internal module containing pure move logic and decision pipeline.
    module internal Move =
        /// Movement decision outcome (internal to move handler).
        type MoveOutcome =
            | Rejected of rejectedCommand: MoveCommand
            | Free of targetSpace: Space * nation: string
            | FreeWithSupersedingUnpaidMovement of targetSpace: Space * nation: string
            | Paid of targetSpace: Space * distance: int * nation: string
            | PaidWithSupersedingUnpaidMovement of targetSpace: Space * distance: int * nation: string

        // Decision chain functions
        let noMovesAllowedIfNotInitialized (state, validatedCommand: MoveCommand) =
            match state with
            | None -> Resolve(Rejected validatedCommand)
            | Some s -> Continue(s, validatedCommand)

        let noMovesAllowedForNationNotInGame (rondelState: RondelState, validatedCommand: MoveCommand) =
            match rondelState.NationPositions |> Map.tryFind validatedCommand.Nation with
            | None -> Resolve(Rejected validatedCommand)
            | Some possibleNationPosition -> Continue(rondelState, validatedCommand, possibleNationPosition)

        let firstMoveIsFreeToAnyPosition (rondelState, validatedCommand: MoveCommand, possibleNationPosition) =
            match possibleNationPosition with
            | None -> Resolve(Free(validatedCommand.Space, validatedCommand.Nation))
            | Some currentNationPosition -> Continue(rondelState, validatedCommand, currentNationPosition)

        let decideMovementOutcome (rondelState: RondelState, validatedCommand: MoveCommand, currentNationPosition) =
            let distance = Space.distance currentNationPosition validatedCommand.Space

            let hasPendingMovement =
                rondelState.PendingMovements |> Map.containsKey validatedCommand.Nation

            match distance with
            | 0 -> Rejected validatedCommand
            | 1
            | 2
            | 3 ->
                if hasPendingMovement then
                    FreeWithSupersedingUnpaidMovement(validatedCommand.Space, validatedCommand.Nation)
                else
                    Free(validatedCommand.Space, validatedCommand.Nation)
            | 4
            | 5
            | 6 ->
                if hasPendingMovement then
                    PaidWithSupersedingUnpaidMovement(validatedCommand.Space, distance, validatedCommand.Nation)
                else
                    Paid(validatedCommand.Space, distance, validatedCommand.Nation)
            | _ -> Rejected validatedCommand

        // Outcome handler: transforms MoveOutcome to (state, events, commands) tuple
        let handleMoveOutcome
            (state: RondelState option)
            (outcome: MoveOutcome)
            : RondelState option * RondelEvent list * RondelOutboundCommand list =
            match outcome, state with
            | Rejected rejectedCommand, _ ->
                None,
                [ MoveToActionSpaceRejected
                      { GameId = rejectedCommand.GameId
                        Nation = rejectedCommand.Nation
                        Space = rejectedCommand.Space } ],
                []
            | Free(targetSpace, nation), Some state ->
                let newState =
                    { state with
                        NationPositions = state.NationPositions |> Map.add nation (Some targetSpace) }

                let actionDeterminedEvent =
                    ActionDetermined
                        { GameId = state.GameId
                          Nation = nation
                          Action = targetSpace |> Space.toAction }

                Some newState, [ actionDeterminedEvent ], []
            | FreeWithSupersedingUnpaidMovement(targetSpace, nation), Some state ->
                let newState =
                    { state with
                        NationPositions = state.NationPositions |> Map.add nation (Some targetSpace)
                        PendingMovements = state.PendingMovements |> Map.remove nation }

                let actionDeterminedEvent =
                    ActionDetermined
                        { GameId = state.GameId
                          Nation = nation
                          Action = targetSpace |> Space.toAction }

                let existingUnpaidMove = state.PendingMovements |> Map.find nation

                let existingUnpaidMoveRejectedEvent =
                    MoveToActionSpaceRejected
                        { GameId = state.GameId
                          Nation = nation
                          Space = existingUnpaidMove.TargetSpace }

                let voidChargeCommand =
                    VoidCharge
                        { GameId = state.GameId
                          BillingId = existingUnpaidMove.BillingId }

                Some newState, [ actionDeterminedEvent; existingUnpaidMoveRejectedEvent ], [ voidChargeCommand ]
            | Paid(targetSpace, distance, nation), Some state ->
                let billingId = RondelBillingId.newId ()

                let newPendingMove =
                    { TargetSpace = targetSpace
                      Nation = nation
                      BillingId = billingId }

                let newState =
                    { state with
                        PendingMovements = state.PendingMovements |> Map.add nation newPendingMove }

                let amount = (distance - 3) * 2 |> Amount.unsafe

                let chargeCommand =
                    ChargeMovement
                        { GameId = state.GameId
                          Nation = nation
                          Amount = amount
                          BillingId = billingId }

                Some newState, [], [ chargeCommand ]
            | PaidWithSupersedingUnpaidMovement(targetSpace, distance, nation), Some state ->
                let billingId = RondelBillingId.newId ()

                let newPendingMove =
                    { TargetSpace = targetSpace
                      Nation = nation
                      BillingId = billingId }

                let newState =
                    { state with
                        PendingMovements = state.PendingMovements |> Map.add nation newPendingMove }

                let amount = (distance - 3) * 2 |> Amount.unsafe

                let chargeCommand =
                    ChargeMovement
                        { GameId = state.GameId
                          Nation = nation
                          Amount = amount
                          BillingId = billingId }

                let existingUnpaidMove = state.PendingMovements |> Map.find nation

                let existingUnpaidMoveRejectedEvent =
                    MoveToActionSpaceRejected
                        { GameId = state.GameId
                          Nation = nation
                          Space = existingUnpaidMove.TargetSpace }

                let voidChargeCommand =
                    VoidCharge
                        { GameId = state.GameId
                          BillingId = existingUnpaidMove.BillingId }

                Some newState, [ existingUnpaidMoveRejectedEvent ], [ voidChargeCommand; chargeCommand ]
            | _, _ -> failwith "Unhandled move outcome."

        /// Pure move processing function: takes state and command, returns (state, events, commands) tuple.
        /// Contains no IO - purely transforms input to output based on business rules.
        let execute
            (command: MoveCommand)
            (state: RondelState option)
            : RondelState option * RondelEvent list * RondelOutboundCommand list =
            noMovesAllowedIfNotInitialized (state, command)
            |> Decision.bind noMovesAllowedForNationNotInGame
            |> Decision.bind firstMoveIsFreeToAnyPosition
            |> Decision.resolve decideMovementOutcome
            |> handleMoveOutcome state

    /// Internal module containing pure setToStartingPositions logic.
    module internal SetToStartingPositions =
        let private canNotSetStartPositionsWithNoNations command =
            if Set.isEmpty command.Nations then
                failwith "Cannot initialize rondel with zero nations."
            else
                command

        /// Pure setToStartingPositions processing function: takes state and command, returns (state, events, commands) tuple.
        /// Contains no IO - purely transforms input to output based on business rules.
        let execute
            (command: SetToStartingPositionsCommand)
            (state: RondelState option)
            : RondelState option * RondelEvent list * RondelOutboundCommand list =
            let validatedCommand = canNotSetStartPositionsWithNoNations command

            match state with
            | Some _ -> None, [], [] // Already initialized, no-op
            | None ->
                let newState: RondelState =
                    { GameId = validatedCommand.GameId
                      NationPositions = validatedCommand.Nations |> Set.toSeq |> Seq.map (fun n -> n, None) |> Map.ofSeq
                      PendingMovements = Map.empty }

                let positionedAtStartEvent = PositionedAtStart { GameId = validatedCommand.GameId }

                Some newState, [ positionedAtStartEvent ], []

    // ──────────────────────────────────────────────────────────────────────────
    // Handlers
    // ──────────────────────────────────────────────────────────────────────────

    /// Initialize rondel for the specified game with the given nations.
    let internal setToStartingPositions (deps: RondelDependencies) (command: SetToStartingPositionsCommand) : unit =

        deps.Load command.GameId
        |> SetToStartingPositions.execute command
        |||> materialize deps

    /// Move a nation to the specified space on the rondel.
    let internal move (deps: RondelDependencies) (command: MoveCommand) : unit =

        deps.Load command.GameId |> Move.execute command |||> materialize deps

    /// Process invoice payment confirmation from Accounting domain.
    let internal onInvoicedPaid (deps: RondelDependencies) (event: InvoicePaidInboundEvent) : Result<unit, string> =
        let load = deps.Load
        let save = deps.Save
        let publish = deps.Publish

        let performIO state events =
            let saveState state =
                match state with
                | Some s -> save s
                | None -> Ok()

            let publishEvents events = events |> List.iter publish |> Ok

            saveState state
            |> Result.bind (fun () -> publishEvents events)
            |> Result.defaultWith (fun e -> failwith $"Failed to perform IO side effects: {e}")

        let failIfNotInitialized (state: RondelState option, event: InvoicePaidInboundEvent) =
            match state with
            | Some s -> s, event
            | None -> failwith "Rondel not initialized for game."

        let registerPaymentAndCompleteMovement (state: RondelState, event: InvoicePaidInboundEvent) =
            let pendingMovement =
                state.PendingMovements
                |> Map.toSeq
                |> Seq.tryFind (fun (_, pm) -> pm.BillingId = event.BillingId)
                |> Option.map snd

            match pendingMovement with
            | None ->
                // No pending movement found - idempotent handling for duplicates or already completed/voided.
                None, []
            | Some pending ->
                let action = Space.toAction pending.TargetSpace

                let newNationPosition = Some pending.TargetSpace

                let newState =
                    { state with
                        NationPositions = state.NationPositions |> Map.add pending.Nation newNationPosition
                        PendingMovements = state.PendingMovements |> Map.remove pending.Nation }

                let actionDeterminedEvent =
                    ActionDetermined
                        { GameId = state.GameId
                          Nation = pending.Nation
                          Action = action }

                Some newState, [ actionDeterminedEvent ]

        let execute state event =
            failIfNotInitialized (state, event)
            |> registerPaymentAndCompleteMovement
            ||> performIO

        let loadedState = load event.GameId
        Ok(execute loadedState event)

    /// Process invoice payment failure from Accounting domain.
    let internal onInvoicePaymentFailed
        (deps: RondelDependencies)
        (event: InvoicePaymentFailedInboundEvent)
        : Result<unit, string> =
        invalidOp "Not implemented: onInvoicePaymentFailed"

    // ──────────────────────────────────────────────────────────────────────────
    // Public Routers
    // ──────────────────────────────────────────────────────────────────────────

    /// Execute a rondel command. Routes to the appropriate command handler.
    let execute (deps: RondelDependencies) (command: RondelCommand) : unit =
        match command with
        | SetToStartingPositions cmd -> setToStartingPositions deps cmd
        | Move cmd -> move deps cmd

    /// Handle an inbound event from other bounded contexts. Routes to the appropriate event handler.
    let handle (deps: RondelDependencies) (event: RondelInboundEvent) : Result<unit, string> =
        match event with
        | InvoicePaid evt -> onInvoicedPaid deps evt
        | InvoicePaymentFailed evt -> onInvoicePaymentFailed deps evt
