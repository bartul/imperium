namespace Imperium

open System
open Imperium.Contract.Accounting
open Imperium.Contract.Rondel

module Rondel =
    open Imperium.Primitives

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

    // RondelEvent is defined in Imperium.Contract.Rondel module

    // Public API types
    type PublishRondelEvent = RondelEvent -> unit

    // Command: Initialize rondel state for a game
    let setToStartingPositions (publish: PublishRondelEvent) (command: SetToStartingPositionsCommand) : Result<unit, string> =
        invalidOp "Not implemented: setToStartingPositions"

    // Command: Initiate nation movement to a space
    let move (publish: PublishRondelEvent) (chargeForMovement: ChargeNationForRondelMovement) (command: MoveCommand) : Result<unit, string> =
        invalidOp "Not implemented: move"

    // Event handler: Process successful invoice payment from Accounting domain
    let onInvoicedPaid (publish: PublishRondelEvent) (event: RondelInvoicePaid) : Result<unit, string> =
        invalidOp "Not implemented: onInvoicedPaid"

    // Event handler: Process failed invoice payment from Accounting domain
    let onInvoicePaymentFailed (publish: PublishRondelEvent) (event: RondelInvoicePaymentFailed) : Result<unit, string> =
        invalidOp "Not implemented: onInvoicePaymentFailed"
