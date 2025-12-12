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
            PendingMovements: Map<Guid, PendingMovement>
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
                    NationPositions = Map.empty
                    PendingMovements = Map.empty 
                }
                |> Result.map (fun () ->
                     publish (PositionedAtStart { GameId = validatedCommand.GameId |> Id.value })) 

        let state = load command.GameId      
        command 
        |> SetToStartingPositions.toDomain 
        |> Result.bind validate 
        |> Result.bind (execute state)

    // Command: Initiate nation movement to a space
    let move
        (load: LoadRondelState)
        (save: SaveRondelState)
        (publish: PublishRondelEvent)
        (chargeForMovement: ChargeNationForRondelMovement)
        (command: MoveCommand)
        : Result<unit, string> =
            
            let execute state validatedCommand =
                match state with
                | None -> 
                    publish (MoveToActionSpaceRejected { GameId = validatedCommand.GameId |> Id.value; Nation = validatedCommand.Nation; Space = validatedCommand.Space |> Space.toString })
                | Some _ ->
                    publish (ActionDetermined { GameId = validatedCommand.GameId |> Id.value; Nation = validatedCommand.Nation; Action = validatedCommand.Space |> Space.toAction |> Action.toString})
                Ok ()

            let state = load command.GameId
            command
            |> Move.toDomain
            |> Result.bind (execute state)

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
