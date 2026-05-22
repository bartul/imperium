namespace Imperium.Rondel

open Imperium.Primitives

/// Internal pure module containing the move decision pipeline and outcome handling.
module internal Movement =

    type MoveOutcome =
        | Rejected of rejectedCommand: MoveCommand
        | Free of targetSpace: Space * nation: string
        | FreeWithSupersedingUnpaidMovement of targetSpace: Space * nation: string
        | Paid of targetSpace: Space * distance: int * nation: string
        | PaidWithSupersedingUnpaidMovement of targetSpace: Space * distance: int * nation: string

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

    let handleMoveOutcome
        (state: RondelState option)
        (outcome: MoveOutcome)
        : RondelState option * RondelEvent list * RondelOutboundCommand list =
        match outcome, state with
        | Rejected rejectedCommand, _ ->
            None,
            [ MoveToActionSpaceRejected
                  { GameId = rejectedCommand.GameId; Nation = rejectedCommand.Nation; Space = rejectedCommand.Space } ],
            []
        | Free(targetSpace, nation), Some state ->
            let newState = state |> RondelState.withNationPosition nation targetSpace

            let actionDeterminedEvent =
                ActionDetermined { GameId = state.GameId; Nation = nation; Action = targetSpace |> Space.toAction }

            Some newState, [ actionDeterminedEvent ], []
        | FreeWithSupersedingUnpaidMovement(targetSpace, nation), Some state ->
            let newState =
                state
                |> RondelState.withNationPosition nation targetSpace
                |> RondelState.withoutPendingMove nation

            let actionDeterminedEvent =
                ActionDetermined { GameId = state.GameId; Nation = nation; Action = targetSpace |> Space.toAction }

            let existingUnpaidMove = state.PendingMovements |> Map.find nation

            let existingUnpaidMoveRejectedEvent =
                MoveToActionSpaceRejected
                    { GameId = state.GameId; Nation = nation; Space = existingUnpaidMove.TargetSpace }

            let voidChargeCommand =
                VoidCharge { GameId = state.GameId; BillingId = existingUnpaidMove.BillingId }

            Some newState, [ actionDeterminedEvent; existingUnpaidMoveRejectedEvent ], [ voidChargeCommand ]
        | Paid(targetSpace, distance, nation), Some state ->
            let billingId = RondelBillingId.newId ()

            let newState = state |> RondelState.withPendingMove nation targetSpace billingId

            let amount = (distance - 3) * 2 |> Amount.unsafe

            let chargeCommand =
                ChargeMovement { GameId = state.GameId; Nation = nation; Amount = amount; BillingId = billingId }

            Some newState, [], [ chargeCommand ]
        | PaidWithSupersedingUnpaidMovement(targetSpace, distance, nation), Some state ->
            let billingId = RondelBillingId.newId ()

            let newState = state |> RondelState.withPendingMove nation targetSpace billingId

            let amount = (distance - 3) * 2 |> Amount.unsafe

            let chargeCommand =
                ChargeMovement { GameId = state.GameId; Nation = nation; Amount = amount; BillingId = billingId }

            let existingUnpaidMove = state.PendingMovements |> Map.find nation

            let existingUnpaidMoveRejectedEvent =
                MoveToActionSpaceRejected
                    { GameId = state.GameId; Nation = nation; Space = existingUnpaidMove.TargetSpace }

            let voidChargeCommand =
                VoidCharge { GameId = state.GameId; BillingId = existingUnpaidMove.BillingId }

            Some newState, [ existingUnpaidMoveRejectedEvent ], [ voidChargeCommand; chargeCommand ]
        | _, _ -> failwith "Unhandled move outcome."

    /// Pure move processing function: takes state and command, returns (state, events, commands) tuple.
    let execute
        (command: MoveCommand)
        (state: RondelState option)
        : RondelState option * RondelEvent list * RondelOutboundCommand list =
        noMovesAllowedIfNotInitialized (state, command)
        |> Decision.bind noMovesAllowedForNationNotInGame
        |> Decision.bind firstMoveIsFreeToAnyPosition
        |> Decision.resolve decideMovementOutcome
        |> handleMoveOutcome state
