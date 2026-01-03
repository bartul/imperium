namespace Imperium

open System
open Imperium.Contract.Accounting
open Imperium.Contract.Rondel

module Rondel =
    open Imperium.Primitives

    // Domain types

    type RondelError = string

    [<Struct>]
    type RondelBillingId = private RondelBillingId of Id

    module RondelBillingId =
        let create = Id.createMap RondelBillingId
        let newId () = Id.newId () |> RondelBillingId
        let value (RondelBillingId g) = g |> Id.value
        let toString (RondelBillingId g) = g |> Id.toString
        let tryParse = Id.tryParseMap RondelBillingId

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
        let private spacesInOrder =
            [| Space.Investor
               Space.Import
               Space.ProductionOne
               Space.ManeuverOne
               Space.Taxation
               Space.Factory
               Space.ProductionTwo
               Space.ManeuverTwo |]

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

        let fromString (s: string) : Result<Space, string> =
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


    type RondelInboundEvent =
        | RondelInvoicePaid of RondelInvoicePaid
        | RondelInvoicePaymentFailed of RondelInvoicePaymentFailed

    type RondelOutboundCommand =
        | ChargeNationForRondelMovement of ChargeNationForRondelMovementCommand
        | VoidRondelCharge of VoidRondelChargeCommand

    // State DTOs for persistence
    module Dto =

        type RondelState =
            { GameId: Guid
              NationPositions: Map<string, string option>
              PendingMovements: Map<string, PendingMovement> }

        and PendingMovement =
            { Nation: string
              TargetSpace: string
              BillingId: Guid }

    // Public API types
    type LoadRondelState = Guid -> Dto.RondelState option
    type SaveRondelState = Dto.RondelState -> Result<unit, string>
    type PublishRondelEvent = RondelEvent -> unit

    type RondelCommand =
        | SetToStartingPositions of SetToStartingPositionsCommand
        | Move of MoveCommand

    and SetToStartingPositionsCommand = { GameId: Id; Nations: Set<string> }

    and MoveCommand =
        { GameId: Id
          Nation: string
          Space: Space }

    module SetToStartingPositionsCommand =
        let toDomain (command: Contract.Rondel.SetToStartingPositionsCommand) =
            Id.create command.GameId
            |> Result.map (fun id ->
                { GameId = id
                  Nations = Set.ofArray command.Nations })

    module MoveCommand =
        let toDomain (command: Contract.Rondel.MoveCommand) =
            Id.create command.GameId
            |> Result.bind (fun id -> Space.fromString command.Space |> Result.map (fun space -> id, space))
            |> Result.map (fun (id, space) ->
                { GameId = id
                  Nation = command.Nation
                  Space = space })

    // Command: Initialize rondel state for a game
    let setToStartingPositions
        (load: LoadRondelState)
        (save: SaveRondelState)
        (publish: PublishRondelEvent)
        (command: SetToStartingPositionsCommand)
        : unit =

        let canNotSetStartPositionsWithNoNations command =
            if Set.isEmpty command.Nations then
                failwith "Cannot initialize rondel with zero nations."
            else
                command

        let execute state (command: SetToStartingPositionsCommand) =
            match state with
            | Some _ -> None, [], [] // Already initialized, no-op
            | None ->
                let newState: Dto.RondelState =
                    { GameId = command.GameId |> Id.value
                      NationPositions = command.Nations |> Set.toSeq |> Seq.map (fun n -> n, None) |> Map.ofSeq
                      PendingMovements = Map.empty }

                let positionedAtStartEvent =
                    PositionedAtStart { GameId = command.GameId |> Id.value }

                Some newState, [ positionedAtStartEvent ], []

        let performIO state events commands =
            let saveState state =
                match state with
                | Some s -> save s
                | None -> Ok()

            let publishEvents events = events |> List.iter publish |> Ok

            let executeOutboundCommands commands =
                let executeCommand =
                    function
                    | ChargeNationForRondelMovement c -> Ok()
                    | VoidRondelCharge c -> Ok()

                (Ok(), commands)
                ||> List.fold (fun state cmd -> state |> Result.bind (fun () -> executeCommand cmd))

            saveState state
            |> Result.bind (fun () -> publishEvents events)
            |> Result.bind (fun () -> executeOutboundCommands commands)
            |> Result.defaultWith (fun e -> failwith $"Failed to perform IO side effects: {e}")

        command
        |> canNotSetStartPositionsWithNoNations
        |> (fun cmd -> load (cmd.GameId |> Id.value), cmd)
        ||> execute
        |||> performIO

    type MoveOutcome =
        | Rejected of rejectedCommand: MoveCommand
        | Free of targetSpace: Space * nation: string
        | FreeWithSupersedingUnpaidMovement of targetSpace: Space * nation: string
        | Paid of targetSpace: Space * distance: int * nation: string
        | PaidWithSupersedingUnpaidMovement of targetSpace: Space * distance: int * nation: string
    // Command: Initiate nation movement to a space
    let move
        (load: LoadRondelState)
        (save: SaveRondelState)
        (publish: PublishRondelEvent)
        (chargeForMovement: ChargeNationForRondelMovement)
        (voidCharge: VoidRondelCharge)
        (command: MoveCommand)
        : unit =
        let noMovesAllowedIfNotInitialized (state, validatedCommand) =
            match state with
            | None -> Resolve(Rejected validatedCommand)
            | Some s -> Continue(s, validatedCommand)

        let noMovesAllowedForNationNotInGame (rondelState: Dto.RondelState, validatedCommand) =
            match rondelState.NationPositions |> Map.tryFind validatedCommand.Nation with
            | None -> Resolve(Rejected validatedCommand)
            | Some possibleNationPosition -> Continue(rondelState, validatedCommand, possibleNationPosition)

        let firstMoveIsFreeToAnyPosition (rondelState, validatedCommand, possibleNationPosition) =
            match possibleNationPosition with
            | None -> Resolve(Free(validatedCommand.Space, validatedCommand.Nation))
            | Some currentNationPosition -> Continue(rondelState, validatedCommand, currentNationPosition)

        let failIfPositionIsInvalid (rondelState, validatedCommand, currentNationPosition) =
            match Space.fromString currentNationPosition with
            | Ok space -> Continue(rondelState, validatedCommand, space)
            | Error e -> failwith $"Invalid nation position in state. {e}"

        let decideMovementOutcome (rondelState: Dto.RondelState, validatedCommand, currentNationPosition) =
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
            (state: Dto.RondelState option)
            (outcome: MoveOutcome)
            : Dto.RondelState option * RondelEvent list * RondelOutboundCommand list =
            match outcome, state with
            | Rejected rejectedCommand, _ ->
                None,
                [ MoveToActionSpaceRejected
                      { GameId = rejectedCommand.GameId |> Id.value
                        Nation = rejectedCommand.Nation
                        Space = rejectedCommand.Space |> Space.toString } ],
                []
            | Free(targetSpace, nation), Some state ->
                let newState =
                    { state with
                        NationPositions = state.NationPositions |> Map.add nation (Some(Space.toString targetSpace)) }

                let actionDeterminedEvent =
                    ActionDetermined
                        { GameId = state.GameId
                          Nation = nation
                          Action = targetSpace |> Space.toAction |> Action.toString }

                Some newState, [ actionDeterminedEvent ], []
            | FreeWithSupersedingUnpaidMovement(targetSpace, nation), Some state ->
                let newState =
                    { state with
                        NationPositions = state.NationPositions |> Map.add nation (Some(Space.toString targetSpace))
                        PendingMovements = state.PendingMovements |> Map.remove nation }

                let actionDeterminedEvent =
                    ActionDetermined
                        { GameId = state.GameId
                          Nation = nation
                          Action = targetSpace |> Space.toAction |> Action.toString }

                let existingUnpaidMove = state.PendingMovements |> Map.find nation

                let existingUnpaidMoveRejectedEvent =
                    MoveToActionSpaceRejected
                        { GameId = state.GameId
                          Nation = nation
                          Space = existingUnpaidMove.TargetSpace }

                let voidChargeCommand =
                    { GameId = state.GameId
                      BillingId = existingUnpaidMove.BillingId }
                    : VoidRondelChargeCommand

                Some newState,
                [ actionDeterminedEvent; existingUnpaidMoveRejectedEvent ],
                [ VoidRondelCharge voidChargeCommand ]
            | Paid(targetSpace, distance, nation), Some state ->
                let billingId = Guid.NewGuid()

                let newPendingMove =
                    { TargetSpace = Space.toString targetSpace
                      Nation = nation
                      BillingId = billingId }
                    : Dto.PendingMovement

                let newState =
                    { state with
                        PendingMovements = state.PendingMovements |> Map.add nation newPendingMove }

                let amount = (distance - 3) * 2 |> Amount.unsafe

                let chargeCommand =
                    { GameId = state.GameId
                      Nation = nation
                      Amount = amount
                      BillingId = billingId }

                Some newState, [], [ ChargeNationForRondelMovement chargeCommand ]
            | PaidWithSupersedingUnpaidMovement(targetSpace, distance, nation), Some state ->
                let billingId = Guid.NewGuid()

                let newPendingMove =
                    { TargetSpace = Space.toString targetSpace
                      Nation = nation
                      BillingId = billingId }
                    : Dto.PendingMovement

                let newState =
                    { state with
                        PendingMovements = state.PendingMovements |> Map.add nation newPendingMove }

                let amount = (distance - 3) * 2 |> Amount.unsafe

                let chargeCommand =
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
                    { GameId = state.GameId
                      BillingId = existingUnpaidMove.BillingId }
                    : VoidRondelChargeCommand

                Some newState,
                [ existingUnpaidMoveRejectedEvent ],
                [ VoidRondelCharge voidChargeCommand
                  ChargeNationForRondelMovement chargeCommand ]
            | _, _ -> failwith "Unhandled move outcome."

        let performIO state events commands =
            let saveState state =
                match state with
                | Some s -> save s
                | None -> Ok()

            let publishEvents events = events |> List.iter publish |> Ok

            let executeOutboundCommands commands =
                let executeCommand =
                    function
                    | ChargeNationForRondelMovement c -> chargeForMovement c
                    | VoidRondelCharge c -> voidCharge c

                (Ok(), commands)
                ||> List.fold (fun state cmd -> state |> Result.bind (fun () -> executeCommand cmd))

            saveState state
            |> Result.bind (fun () -> publishEvents events)
            |> Result.bind (fun () -> executeOutboundCommands commands)
            |> Result.defaultWith (fun e -> failwith $"Failed to perform IO side effects: {e}")

        let execute state command =
            noMovesAllowedIfNotInitialized (state, command)
            |> Decision.bind noMovesAllowedForNationNotInGame
            |> Decision.bind firstMoveIsFreeToAnyPosition
            |> Decision.bind failIfPositionIsInvalid
            |> Decision.resolve decideMovementOutcome
            |> handleMoveOutcome state
            |||> performIO

        command |> (fun cmd -> load (cmd.GameId |> Id.value), cmd) ||> execute


    // Event handler: Process successful invoice payment from Accounting domain
    let onInvoicedPaid
        (load: LoadRondelState)
        (save: SaveRondelState)
        (publish: PublishRondelEvent)
        (event: RondelInvoicePaid)
        : Result<unit, string> =

        let performIO state events =
            let saveState state =
                match state with
                | Some s -> save s
                | None -> Ok()

            let publishEvents events = events |> List.iter publish |> Ok

            saveState state
            |> Result.bind (fun () -> publishEvents events)
            |> Result.defaultWith (fun e -> failwith $"Failed to perform IO side effects: {e}")

        let failIfNotInitialized (state: Dto.RondelState option, event: RondelInvoicePaid) =
            match state with
            | Some s -> (s, event)
            | None -> failwith "Rondel not initialized for game."

        let registerPaymentAndCompleteMovement (state: Dto.RondelState, event: RondelInvoicePaid) =
            let billingId = event.BillingId

            let pendingMovement =
                state.PendingMovements
                |> Map.toSeq
                |> Seq.tryFind (fun (_, pm) -> pm.BillingId = billingId)
                |> Option.map snd

            match pendingMovement with
            | None ->
                // No pending movement found for this BillingId - event is ignored for idempotency.
                // This handles duplicate payment events or payments received after movement was already completed/voided.
                None, []
            | Some pending ->
                let action =
                    Space.fromString pending.TargetSpace
                    |> Result.map (Space.toAction >> Action.toString)
                    |> Result.defaultWith (fun parseError ->
                        failwith
                            $"State corruption: invalid TargetSpace '{pending.TargetSpace}' for pending movement. GameId: {state.GameId}, Nation: {pending.Nation}, Error: {parseError}")

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

        let loadedState = load (event.GameId)
        Ok(execute loadedState event)

    // Event handler: Process failed invoice payment from Accounting domain
    let onInvoicePaymentFailed
        (load: LoadRondelState)
        (save: SaveRondelState)
        (publish: PublishRondelEvent)
        (event: RondelInvoicePaymentFailed)
        : Result<unit, string> =
        invalidOp "Not implemented: onInvoicePaymentFailed"
