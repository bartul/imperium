namespace Imperium

open System

module Rondel =

    open Imperium.Primitives
    
    // CQRS bounded context for rondel game mechanics.
    // Commands are identified by GameId; internal state management is hidden.
    type RondelError = string
    /// Opaque identifier for billing scoped to the rondel domain.
    [<Struct>]
    type RondelBillingId = private RondelBillingId of Id
    module RondelBillingId =
        val create : Guid -> Result<RondelBillingId, string>
        val newId : unit -> RondelBillingId
        val value : RondelBillingId -> Guid
        val toString : RondelBillingId -> string
        val tryParse : string -> Result<RondelBillingId, string>
        val value : RondelBillingId -> Guid
        val toString : RondelBillingId -> string

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
    type RondelEvent =
        | PositionedAtStart of gameId:Guid
        | ActionDetermined of gameId:Guid * nationId:string * action:string
        | MovementToActionRejected of gameId:Guid * nationId:string * space:string

    // Public API

    /// Command: Initialize rondel for the specified game with the given nations.
    /// All nations are positioned at their starting positions.
    /// Publishes PositionedAtStart event internally. Fails if nation set is empty.
    val setToStartPositions : gameId:Guid -> nations:Set<string> -> Result<unit, string>

    /// Command: Move a nation to the specified space on the rondel.
    /// Determines movement cost and issues invoice to Economy domain.
    /// Events published internally upon completion.
    val move : gameId:Guid -> nationId:string -> space:string -> Result<unit, string>

    /// Event handler: Processes invoice payment confirmation from Economy domain.
    /// Completes the nation's movement and publishes ActionDetermined event internally.
    val onInvoicedPaid : gameId:Guid -> billingId:Guid -> Result<unit, string>
    /// Event handler: Processes invoice payment failure from Economy domain.
    /// Rejects the movement and publishes MovementToActionRejected event internally.
    val onInvoicePaymentFailed : gameId:Guid -> billingId:Guid -> Result<unit, string>