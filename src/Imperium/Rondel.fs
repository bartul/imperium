namespace Imperium

open System
open Imperium.Gameplay

module Rondel =
    open Imperium.Primitives

    // CQRS bounded context for rondel game mechanics.
    // Commands are identified by GameId; internal state will be managed by module.
    // Implementation currently stubbed, following interface-first development.
    type RondelError = string
    // Opaque identifier for invoices scoped to the rondel domain.
    [<Struct>]
    type RondelInvoiceId = private RondelInvoiceId of Id

    module RondelInvoiceId =
        let create guid = guid |> Id.createMap RondelInvoiceId
        let newId () = Id.newId () |> RondelInvoiceId
        let value (RondelInvoiceId g) = g |> Id.value
        let toString (RondelInvoiceId g) = g |> Id.toString
        let tryParse raw = raw |> Id.tryParseMap RondelInvoiceId

    // Temporary opaque handle - will be removed as part of CQRS refactoring.
    // Future: internal state will be stored in a dictionary/map indexed by GameId.
    type Rondel = private { dummy: unit }

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

    type Event =
        | PositionedAtStart of gameId:GameId
        | ActionDetermined of gameId:GameId * nationId:NationId * action:Action
        | MovementToActionRejected of gameId:GameId * nationId:NationId * space:Space

    // CQRS Command Handlers and Event Handlers (stubbed implementations)

    // Command: Initialize rondel state for a game
    let setToStartPositions (gameId: GameId) (nations: Set<NationId>) : Result<Event list, RondelError> =
       invalidOp "Not implemented: setToStartPositions"

    // Command: Initiate nation movement to a space
    let move (gameId: GameId) (nationId: NationId) (space: Space) : Result<Event list, RondelError> =
        invalidOp "Not implemented: move"

    // Event handler: Process successful invoice payment from Economy domain
    let onInvoicedPaid (gameId: GameId) (invoiceId: RondelInvoiceId) : Result<Event list, RondelError> =
        invalidOp "Not implemented: onInvoicedPaid"

    // Event handler: Process failed invoice payment from Economy domain
    let onInvoicePaymentFailed (gameId: GameId) (invoiceId: RondelInvoiceId) : Result<Event list, RondelError> =
        invalidOp "Not implemented: onInvoicePaymentFailed"
