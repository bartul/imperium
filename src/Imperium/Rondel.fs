namespace Imperium

open System
open Imperium.Gameplay

module Rondel =
    open Imperium.Primitives

    type RondelError = string
    [<Struct>]
    type RondelInvoiceId = private RondelInvoiceId of Id

    module RondelInvoiceId =
        let create guid = guid |> Id.createMap RondelInvoiceId
        let newId () = Id.newId () |> RondelInvoiceId
        let value (RondelInvoiceId g) = g |> Id.value
        let toString (RondelInvoiceId g) = g |> Id.toString
        let tryParse raw = raw |> Id.tryParseMap RondelInvoiceId

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
        | PositionedAtStart of gameId:GameId
        | ActionDetermined of gameId:GameId * nationId:NationId * action:Action
        | MovementToActionRejected of gameId:GameId * nationId:NationId * space:Space

    // Command: Initialize rondel state for a game
    let setToStartPositions (gameId: GameId) (nations: Set<NationId>) : Result<unit, RondelError> =
       invalidOp "Not implemented: setToStartPositions"

    // Command: Initiate nation movement to a space
    let move (gameId: GameId) (nationId: NationId) (space: Space) : Result<unit, RondelError> =
        invalidOp "Not implemented: move"

    // Event handler: Process successful invoice payment from Economy domain
    let onInvoicedPaid (gameId: GameId) (invoiceId: RondelInvoiceId) : Result<unit, RondelError> =
        invalidOp "Not implemented: onInvoicedPaid"

    // Event handler: Process failed invoice payment from Economy domain
    let onInvoicePaymentFailed (gameId: GameId) (invoiceId: RondelInvoiceId) : Result<unit, RondelError> =
        invalidOp "Not implemented: onInvoicePaymentFailed"
