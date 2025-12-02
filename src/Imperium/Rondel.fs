namespace Imperium

open System

module Rondel =
    open Imperium.Primitives

    type RondelError = string

    [<Struct>]
    type RondelBillingId = private RondelBillingId of Id

    module RondelBillingId =
        let create guid = guid |> Id.createMap RondelBillingId
        let newId () = Id.newId () |> RondelBillingId
        let value (RondelBillingId g) = g |> Id.value
        let toString (RondelBillingId g) = g |> Id.toString
        let tryParse raw = raw |> Id.tryParseMap RondelBillingId

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

    type RondelEvent =
        | PositionedAtStart of gameId: Guid
        | ActionDetermined of gameId: Guid * nationId: string * action: string
        | MovementToActionRejected of gameId: Guid * nationId: string * space: string

    // Command: Initialize rondel state for a game
    let setToStartPositions (gameId: Guid) (nations: Set<string>) : Result<unit, string> =
        invalidOp "Not implemented: setToStartPositions"

    // Command: Initiate nation movement to a space
    let move (gameId: Guid) (nationId: string) (space: string) : Result<unit, string> =
        invalidOp "Not implemented: move"

    // Event handler: Process successful invoice payment from Economy domain
    let onInvoicedPaid (gameId: Guid) (billingId: Guid) : Result<unit, string> =
        invalidOp "Not implemented: onInvoicedPaid"

    // Event handler: Process failed invoice payment from Economy domain
    let onInvoicePaymentFailed (gameId: Guid) (billingId: Guid) : Result<unit, string> =
        invalidOp "Not implemented: onInvoicePaymentFailed"
