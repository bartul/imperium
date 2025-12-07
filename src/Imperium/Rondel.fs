namespace Imperium

open System
open Imperium.Contract.Accounting
open Imperium.Contract.Rondel

module Rondel =
    open Imperium.Primitives

    // Internal types

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
    type Space =
        | Investor
        | Import
        | ProductionOne
        | ManeuverOne
        | Taxation
        | Factory
        | ProductionTwo
        | ManeuverTwo

    [<RequireQualifiedAccess>]
    type Action =
        | Investor
        | Import
        | Production
        | Maneuver
        | Taxation
        | Factory

    // State DTOs for persistence
    module Dto =
        type PendingMovement = { Nation: string; TargetSpace: string; BillingId: Guid }

        type RondelState = {
            GameId: Guid
            NationPositions: Map<string, string option>
            PendingMovements: Map<Guid, PendingMovement>
        }

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
        match command.Nations with
        | n when Set.isEmpty n -> Error "Cannot initialize rondel with zero nations."
        | _ ->
            let state = load command.GameId
            match state with 
            | Some _ -> Ok () // Already initialized, no-op
            | None -> 
                save { GameId = command.GameId; NationPositions = Map.empty; PendingMovements = Map.empty } |>  ignore 
                publish (PositionedAtStart { GameId = command.GameId })
                Ok ()

    // Command: Initiate nation movement to a space
    let move
        (load: LoadRondelState)
        (save: SaveRondelState)
        (publish: PublishRondelEvent)
        (chargeForMovement: ChargeNationForRondelMovement)
        (command: MoveCommand)
        : Result<unit, string> =
        invalidOp "Not implemented: move"

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
