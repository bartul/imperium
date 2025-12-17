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
        let private spacesInOrder = [|
            Space.Investor
            Space.Import
            Space.ProductionOne
            Space.ManeuverOne
            Space.Taxation
            Space.Factory
            Space.ProductionTwo
            Space.ManeuverTwo
        |]
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
            | Space.ProductionOne | Space.ProductionTwo -> Action.Production
            | Space.ManeuverOne | Space.ManeuverTwo -> Action.Maneuver
            | Space.Taxation -> Action.Taxation
            | Space.Factory -> Action.Factory


    type RondelCommand =
        | StartingPositions of SetToStartingPositions
        | Move of Move
    and SetToStartingPositions = { GameId: Id; Nations: Set<string> }
    and Move = { GameId: Id; Nation: string; Space: Space }
    module SetToStartingPositions =   
        let toDomain (command : SetToStartingPositionsCommand) =
            Id.create command.GameId
            |> Result.map (fun id ->
                { GameId = id; Nations = Set.ofArray command.Nations })
    module Move =
        let toDomain (command : MoveCommand) =
            Id.create command.GameId
            |> Result.bind (fun id -> Space.fromString command.Space |> Result.map (fun space -> id, space))
            |> Result.map (fun (id, space) -> { GameId = id; Nation = command.Nation; Space = space })

    // State DTOs for persistence
    module Dto =

        type RondelState = {
            GameId: Guid
            NationPositions: Map<string, string option>
            PendingMovements: Map<string, PendingMovement>
        }
        and PendingMovement = { Nation: string; TargetSpace: string; BillingId: Guid }

    // Public API types
    type LoadRondelState = Guid -> Dto.RondelState option
    type SaveRondelState = Dto.RondelState -> Result<unit, string>
    type PublishRondelEvent = RondelEvent -> unit

    // Command: Initialize rondel state for a game
    let setToStartingPositions
        (load: LoadRondelState)
        (save: SaveRondelState)
        (publish: PublishRondelEvent)
        (command: SetToStartingPositionsCommand)
        : Result<unit, string> =

        let validate unvalidatedCommand =
            if Set.isEmpty unvalidatedCommand.Nations then Error "Cannot initialize rondel with zero nations." else Ok unvalidatedCommand

        let execute state (validatedCommand : SetToStartingPositions) =
            match state with
            | Some _ -> Ok () // Already initialized, no-op
            | None ->
                save { 
                    GameId = validatedCommand.GameId |> Id.value 
                    NationPositions = validatedCommand.Nations |> Set.toSeq |> Seq.map (fun n -> n, None) |> Map.ofSeq
                    PendingMovements = Map.empty 
                }
                |> Result.map (fun () ->
                     publish (PositionedAtStart { GameId = validatedCommand.GameId |> Id.value })) 

        let state = load command.GameId      
        command 
        |> SetToStartingPositions.toDomain 
        |> Result.bind validate 
        |> Result.bind (execute state)

    type MoveOutcome = 
        | Rejected
        | Free of Space 
        | FreeWithSupersedingUnpaidMovement of (Space * string)
        | Paid of (Space * int)
        | PaidWithSupersedingUnpaidMovement of (Space * int * string)
    // Command: Initiate nation movement to a space
    let move
        (load: LoadRondelState)
        (save: SaveRondelState)
        (publish: PublishRondelEvent)
        (chargeForMovement: ChargeNationForRondelMovement)
        (voidCharge: VoidRondelCharge)
        (command: MoveCommand)
        : Result<unit, string> =
            let noMovesAllowedIfNotInitialized (state : Dto.RondelState option, validatedCommand) =
                match state with
                | None -> Resolve Rejected
                | Some s -> Continue (s, validatedCommand)
            let noMovesAllowedForNationNotInGame (rondelState : Dto.RondelState, validatedCommand) =
                match rondelState.NationPositions |> Map.tryFind validatedCommand.Nation with
                | None -> Resolve Rejected
                | Some possibleNationPosition -> Continue (rondelState, validatedCommand, possibleNationPosition)
            let firstMoveIsFreeToAnyPosition (rondelState : Dto.RondelState, validatedCommand, possibleNationPosition) =
                match possibleNationPosition with
                | None -> Resolve (Free validatedCommand.Space)
                | Some currentNationPosition -> Continue (rondelState, validatedCommand, currentNationPosition)
            let failIfPositionIsInvalid (rondelState, validatedCommand, currentNationPosition) =
                match Space.fromString currentNationPosition with
                | Ok space -> Continue (rondelState, validatedCommand, space)
                | Error e -> failwith $"Invalid nation position in state. {e}"
            let decideMovementOutcome (rondelState : Dto.RondelState, validatedCommand, currentNationPosition) =
                let distance = Space.distance currentNationPosition validatedCommand.Space
                let hasPendingMovement = rondelState.PendingMovements |> Map.containsKey validatedCommand.Nation
                match distance, hasPendingMovement with
                | 0, _ -> Rejected
                | 1, true | 2, true | 3, true -> FreeWithSupersedingUnpaidMovement (validatedCommand.Space, validatedCommand.Nation)
                | 1, false | 2, false | 3, false -> Free validatedCommand.Space
                | 4, true | 5, true | 6, true -> PaidWithSupersedingUnpaidMovement (validatedCommand.Space, distance, validatedCommand.Nation)
                | 4, false | 5, false | 6, false -> Paid (validatedCommand.Space, distance)
                | _, _ -> Rejected

            let handleMoveOutcome (state: Dto.RondelState option) outcome =
                match outcome with
                | Rejected ->
                    publish (MoveToActionSpaceRejected { GameId = command.GameId; Nation = command.Nation; Space = command.Space })
                | Free space ->
                    let newState =
                        match state with
                        | Some (s: Dto.RondelState) -> { s with NationPositions = s.NationPositions |> Map.add command.Nation (Some (Space.toString space)) }
                        | None -> failwith "Rondel state not initialized."
                    save newState |> ignore
                    publish (ActionDetermined { GameId = command.GameId; Nation = command.Nation; Action = space |> Space.toAction |> Action.toString})
                | FreeWithSupersedingUnpaidMovement (space, nation) ->
                    let supersedingPendingMovement = 
                        match state with
                        | Some s -> s.PendingMovements |> Map.find nation
                        | None -> failwith "Rondel state not initialized."
                    let newState = 
                        match state with
                        | Some s -> { s with NationPositions = s.NationPositions |> Map.add nation (Some (Space.toString space)); PendingMovements = s.PendingMovements |> Map.remove nation }
                        | None -> failwith "Rondel state not initialized."
                    save newState |> ignore
                    publish (ActionDetermined { GameId = command.GameId; Nation = nation; Action = space |> Space.toAction |> Action.toString})
                    let voidCommand = { GameId = command.GameId; BillingId = supersedingPendingMovement.BillingId } : VoidRondelChargeCommand
                    voidCommand |> voidCharge |> ignore
                    publish (MoveToActionSpaceRejected { GameId = command.GameId; Nation = nation; Space = supersedingPendingMovement.TargetSpace })
                | Paid (space, distance) ->
                    let billingId = Guid.NewGuid()
                    let pendingMovement = { TargetSpace = Space.toString space; Nation = command.Nation; BillingId = billingId } : Dto.PendingMovement
                    let newState = 
                        match state with
                        | Some s -> { s with PendingMovements = s.PendingMovements |> Map.add command.Nation pendingMovement }
                        | None -> failwith "Rondel state not initialized."
                    save newState |> ignore
                    let amount = (distance - 3) * 2 |> Amount.create
                    let chargeCommand amount = { GameId = command.GameId; Nation = command.Nation; Amount = amount; BillingId = billingId }
                    amount 
                    |> Result.map chargeCommand
                    |> Result.bind chargeForMovement
                    |> ignore
                | PaidWithSupersedingUnpaidMovement (space, distance, nation) ->
                    let supersedingPendingMovement = 
                        match state with
                        | Some s -> s.PendingMovements |> Map.find nation
                        | None -> failwith "Rondel state not initialized."
                    let newState = 
                        match state with
                        | Some s -> { s with NationPositions = s.NationPositions |> Map.add nation (Some (Space.toString space)); PendingMovements = s.PendingMovements |> Map.remove nation }
                        | None -> failwith "Rondel state not initialized."
                    save newState |> ignore
                    let voidCommand = { GameId = command.GameId; BillingId = supersedingPendingMovement.BillingId } : VoidRondelChargeCommand
                    voidCommand |> voidCharge |> ignore
                    let billingId = Guid.NewGuid()
                    let pendingMovement = { TargetSpace = Space.toString space; Nation = command.Nation; BillingId = billingId } : Dto.PendingMovement
                    let newStateAfterCharge = 
                        match state with
                        | Some s -> { s with PendingMovements = s.PendingMovements |> Map.add command.Nation pendingMovement }
                        | None -> failwith "Rondel state not initialized."
                    save newStateAfterCharge |> ignore
                    let amount = (distance - 3) * 2 |> Amount.create
                    let chargeCommand amount = { GameId = command.GameId; Nation = command.Nation; Amount = amount; BillingId = billingId }
                    amount 
                    |> Result.map chargeCommand
                    |> Result.bind chargeForMovement
                    |> ignore   
                    publish (MoveToActionSpaceRejected { GameId = command.GameId; Nation = nation; Space = supersedingPendingMovement.TargetSpace })    

            let state = load command.GameId
            let execute (state : Dto.RondelState option) (command : Move)  =
                noMovesAllowedIfNotInitialized (state, command)
                |> Decision.bind noMovesAllowedForNationNotInGame
                |> Decision.bind firstMoveIsFreeToAnyPosition
                |> Decision.bind failIfPositionIsInvalid
                |> Decision.resolve decideMovementOutcome
                |> handleMoveOutcome state
            command
            |> Move.toDomain
            |> Result.bind (fun cmd -> Ok (execute state cmd))


    // Event handler: Process successful invoice payment from Accounting domain
    let onInvoicedPaid
        (load: LoadRondelState)
        (save: SaveRondelState)
        (publish: PublishRondelEvent)
        (event: RondelInvoicePaid)
        : Result<unit, string> =
        invalidOp "Not implemented: onInvoicedPaid"

    // Event handler: Process failed invoice payment from Accounting domain
    let onInvoicePaymentFailed
        (load: LoadRondelState)
        (save: SaveRondelState)
        (publish: PublishRondelEvent)
        (event: RondelInvoicePaymentFailed)
        : Result<unit, string> =
        invalidOp "Not implemented: onInvoicePaymentFailed"
