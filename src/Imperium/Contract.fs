namespace Imperium.Contract

// Contract types for cross-bounded-context communication.
// Intentionally public - no .fsi file needed for infrastructure layer.

module Accounting = 
    open System
    open Imperium.Primitives

    type ChargeNationForRondelMovementCommand = {
        GameId: Guid
        Nation: string
        Amount: Amount
        BillingId: Guid
    }
    type ChargeNationForRondelMovement = ChargeNationForRondelMovementCommand -> unit
    type AccountingCommand =
        | ChargeNationForRondelMovement of ChargeNationForRondelMovementCommand


    type RondelInvoicePaid = {
        GameId: Guid
        BillingId: Guid
    }
    type RondelInvoicePaymentFailed = {
        GameId: Guid
        BillingId: Guid
    }

    type AccountingEvent =
        | RondelInvoicePaid of gameId:Guid * billingId:Guid
        | RondelInvoicePaymentFailed of gameId:Guid * billingId:Guid
        
module Rondel =
    open System

    type SetToStartPositionsCommand = {
        GameId: Guid
        Nations: Set<string>
    }
    type SetToStartPositions = SetToStartPositionsCommand -> unit
    type MoveCommand = {
        GameId: Guid
        Nation: string
        Space: string
    }
    type Move = MoveCommand -> unit
    type RondelCommand =
        | SetToStartPositions of SetToStartPositionsCommand
        | Move of MoveCommand

    /// Integration events published by the Rondel bounded context to inform other domains.
    /// PositionedAtStart: Nations positioned at starting positions, rondel ready for movement commands.
    /// ActionDetermined: Nation successfully moved to a space and the corresponding action was determined.
    /// MovementToActionRejected: Nation's movement rejected due to payment failure.
    type RondelEvent =
        | PositionedAtStart of gameId:Guid
        | ActionDetermined of gameId:Guid * nation:string * action:string
        | MovementToActionRejected of gameId:Guid * nation:string * space:string

module Gameplay = 

    let Foo = "Bar"