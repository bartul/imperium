namespace Imperium

open System
open Imperium.Gameplay

module Rondel =

    open Imperium.Primitives
    
    // CQRS bounded context for rondel game mechanics.
    // Commands are identified by GameId; internal state management is hidden.
    type RondelError = string
    /// Opaque identifier for invoices scoped to the rondel domain.
    [<Struct>]
    type RondelInvoiceId = private RondelInvoiceId of Id
    module RondelInvoiceId =
        val create : Guid -> Result<RondelInvoiceId, string>
        val newId : unit -> RondelInvoiceId
        val value : RondelInvoiceId -> Guid
        val toString : RondelInvoiceId -> string
        val tryParse : string -> Result<RondelInvoiceId, string>
        val value : RondelInvoiceId -> Guid
        val toString : RondelInvoiceId -> string

    /// The eight fixed spaces on the rondel board, arranged clockwise.
    /// Each space is a unique board position. Production and Maneuver appear twice
    /// (One/Two) but map to the same action when landed upon.
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

    /// Actions that can occur when landing on a rondel space. The "One/Two" spaces
    /// map to the same action.
    [<RequireQualifiedAccess>]
    type Action =
        | Investor
        | Import
        | Production
        | Maneuver
        | Taxation
        | Factory
    
    /// Integration events published by the Rondel bounded context to inform other domains.
    /// PositionedAtStart: Nations positioned at starting positions, rondel ready for movement commands.
    /// ActionDetermined: Nation successfully moved to a space and the corresponding action was determined.
    /// MovementToActionRejected: Nation's movement rejected due to payment failure.
    type Event =
        | PositionedAtStart of gameId:GameId
        | ActionDetermined of gameId:GameId * nationId:NationId * action:Action
        | MovementToActionRejected of gameId:GameId * nationId:NationId * space:Space

    // Public API

    /// Command: Initialize rondel for the specified game with the given nations.
    /// All nations are positioned at their starting positions.
    /// Publishes PositionedAtStart event internally. Fails if nation set is empty.
    val setToStartPositions : gameId:GameId -> nations:Set<NationId> -> Result<unit, RondelError>

    /// Command: Move a nation to the specified space on the rondel.
    /// Determines movement cost and issues invoice to Economy domain.
    /// Events published internally upon completion.
    val move : gameId:GameId -> nationId:NationId -> space:Space -> Result<unit, RondelError>

    /// Event handler: Processes invoice payment confirmation from Economy domain.
    /// Completes the nation's movement and publishes ActionDetermined event internally.
    val onInvoicedPaid : gameId:GameId -> invoiceId:RondelInvoiceId -> Result<unit, RondelError>

    /// Event handler: Processes invoice payment failure from Economy domain.
    /// Rejects the movement and publishes MovementToActionRejected event internally.
    val onInvoicePaymentFailed : gameId:GameId -> invoiceId:RondelInvoiceId -> Result<unit, RondelError>
