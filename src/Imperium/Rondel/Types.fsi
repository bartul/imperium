namespace Imperium.Rondel

open System
open Imperium
open Imperium.Primitives

// ──────────────────────────────────────────────────────────────────────────
// Value Types & Enumerations
// ──────────────────────────────────────────────────────────────────────────

/// Opaque identifier linking a rondel movement to its accounting charge.
/// Used to correlate payment confirmations with pending movements.
[<Struct>]
type RondelBillingId = private RondelBillingId of Id

module RondelBillingId =
    /// Extract the underlying Guid value for comparison and serialization.
    val value: RondelBillingId -> Guid

    /// Create a RondelBillingId from an Id.
    val ofId: Id -> RondelBillingId

    /// Assembly-internal: validate a raw Guid and build a billing id.
    val internal create: (Guid -> Result<RondelBillingId, string>)

    /// Assembly-internal: mint a new billing id.
    val internal newId: unit -> RondelBillingId

/// The six distinct actions a nation can perform on the rondel.
/// Each action corresponds to one or two spaces on the circular track.
[<RequireQualifiedAccess>]
type Action =
    | Investor
    | Import
    | Production
    | Maneuver
    | Taxation
    | Factory

/// The eight spaces on the rondel wheel, arranged clockwise.
/// Production and Maneuver each appear twice on the track.
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
    /// Maps a rondel space to its corresponding action.
    val toAction: Space -> Action

    /// Assembly-internal: clockwise distance between two spaces (0..7).
    val internal distance: Space -> Space -> int

    /// Assembly-internal: serialise a space to its persisted string form.
    val internal toString: Space -> string

    /// Assembly-internal: parse a space from its persisted string form.
    val internal fromString: string -> Result<Space, string>

// ──────────────────────────────────────────────────────────────────────────
// Commands
// ──────────────────────────────────────────────────────────────────────────

/// Union of all rondel commands for routing and dispatch.
type RondelCommand =
    | SetToStartingPositions of SetToStartingPositionsCommand
    | Move of MoveCommand

/// Initialize rondel for a game with the participating nations.
and SetToStartingPositionsCommand = { GameId: Id; Nations: Set<string> }

/// Request to move a nation to a specific space on the rondel.
and MoveCommand = { GameId: Id; Nation: string; Space: Space }

/// Transforms Contract SetToStartingPositionsCommand to Domain type.
module SetToStartingPositionsCommand =
    /// Validate and transform Contract command to Domain command.
    /// Returns Error if GameId is invalid (Guid.Empty).
    val fromContract: Contract.Rondel.SetToStartingPositionsCommand -> Result<SetToStartingPositionsCommand, string>

/// Transforms Contract MoveCommand to Domain type.
module MoveCommand =
    /// Validate and transform Contract command to Domain command.
    /// Returns Error if GameId is invalid or Space name is unknown.
    val fromContract: Contract.Rondel.MoveCommand -> Result<MoveCommand, string>

// ──────────────────────────────────────────────────────────────────────────
// Events
// ──────────────────────────────────────────────────────────────────────────

/// Integration events published by the Rondel domain to notify other bounded contexts.
type RondelEvent =
    | PositionedAtStart of PositionedAtStartEvent
    | ActionDetermined of ActionDeterminedEvent
    | MoveToActionSpaceRejected of MoveToActionSpaceRejectedEvent

/// Published when nations are positioned at their starting positions, rondel ready for play.
and PositionedAtStartEvent = { GameId: Id }

/// Published when a nation successfully completes a move and an action is determined.
and ActionDeterminedEvent = { GameId: Id; Nation: string; Action: Action }

/// Published when a nation's movement is rejected (invalid move or payment failure).
and MoveToActionSpaceRejectedEvent = { GameId: Id; Nation: string; Space: Space }

/// Transforms Domain RondelEvent to Contract type for publication.
module RondelEvent =
    /// Transform Domain event to Contract event for cross-boundary communication.
    val toContract: RondelEvent -> Contract.Rondel.RondelEvent

// ──────────────────────────────────────────────────────────────────────────
// Outbound Commands (to other bounded contexts)
// ──────────────────────────────────────────────────────────────────────────

/// Union of all outbound commands dispatched to other bounded contexts.
type RondelOutboundCommand =
    | ChargeMovement of ChargeMovementOutboundCommand
    | VoidCharge of VoidChargeOutboundCommand

/// Command to charge a nation for paid rondel movement (4-6 spaces).
and ChargeMovementOutboundCommand = { GameId: Id; Nation: string; Amount: Amount; BillingId: RondelBillingId }

/// Command to void a previously initiated charge before payment completion.
and VoidChargeOutboundCommand = { GameId: Id; BillingId: RondelBillingId }

/// Transforms Domain ChargeMovementOutboundCommand to Accounting contract type.
module ChargeMovementOutboundCommand =
    /// Convert domain charge command to Accounting contract for dispatch.
    val toContract: ChargeMovementOutboundCommand -> Contract.Accounting.ChargeNationForRondelMovementCommand

/// Transforms Domain VoidChargeOutboundCommand to Accounting contract type.
module VoidChargeOutboundCommand =
    /// Convert domain void command to Accounting contract for dispatch.
    val toContract: VoidChargeOutboundCommand -> Contract.Accounting.VoidRondelChargeCommand

// ──────────────────────────────────────────────────────────────────────────
// Incoming Events (from other bounded contexts)
// ──────────────────────────────────────────────────────────────────────────

/// Inbound events from Accounting domain that affect Rondel state.
type RondelInboundEvent =
    | InvoicePaid of InvoicePaidInboundEvent
    | InvoicePaymentFailed of InvoicePaymentFailedInboundEvent

/// Payment confirmation received from Accounting domain.
and InvoicePaidInboundEvent = { GameId: Id; BillingId: RondelBillingId }

/// Payment failure notification from Accounting domain.
and InvoicePaymentFailedInboundEvent = { GameId: Id; BillingId: RondelBillingId }

/// Transforms Contract RondelInvoicePaid to Domain InvoicePaidInboundEvent.
module InvoicePaidInboundEvent =
    /// Validate and transform Contract event to Domain event.
    /// Returns Error if GameId or BillingId is invalid.
    val fromContract: Contract.Accounting.RondelInvoicePaid -> Result<InvoicePaidInboundEvent, string>

/// Transforms Contract RondelInvoicePaymentFailed to Domain InvoicePaymentFailedInboundEvent.
module InvoicePaymentFailedInboundEvent =
    /// Validate and transform Contract event to Domain event.
    /// Returns Error if GameId or BillingId is invalid.
    val fromContract: Contract.Accounting.RondelInvoicePaymentFailed -> Result<InvoicePaymentFailedInboundEvent, string>

// ──────────────────────────────────────────────────────────────────────────
// Queries
// ──────────────────────────────────────────────────────────────────────────

/// Query for nation positions in a game.
type GetNationPositionsQuery = { GameId: Id }

/// Query for basic rondel overview.
type GetRondelOverviewQuery = { GameId: Id }

// ──────────────────────────────────────────────────────────────────────────
// Query Results
// ──────────────────────────────────────────────────────────────────────────

/// Result of GetNationPositions query.
type RondelPositionsView = { GameId: Id; Positions: NationPositionView list }

/// A nation's position on the rondel.
and NationPositionView = { Nation: string; CurrentSpace: Space option; PendingSpace: Space option }

/// Result of GetRondelOverview query.
type RondelView = { GameId: Id; NationNames: string list }
