namespace Imperium

open System
open Imperium.Primitives
open Imperium.Contract.Accounting
open Imperium.Contract.Rondel

module Rondel =

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

    // ──────────────────────────────────────────────────────────────────────────
    // Domain State
    // ──────────────────────────────────────────────────────────────────────────

    /// Persistent state for a game's rondel, tracking nation positions and pending movements.
    type RondelState =
        {
            GameId: Id
            /// Maps nation name to current position. None indicates starting position (not yet moved).
            NationPositions: Map<string, Space option>
            /// Maps nation name to pending paid movement awaiting payment confirmation.
            PendingMovements: Map<string, PendingMovement>
        }

    /// A movement awaiting payment confirmation from the Accounting domain.
    and PendingMovement =
        { Nation: string
          TargetSpace: Space
          BillingId: RondelBillingId }

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
    and MoveCommand =
        { GameId: Id
          Nation: string
          Space: Space }

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
    and ActionDeterminedEvent =
        { GameId: Id
          Nation: string
          Action: Action }

    /// Published when a nation's movement is rejected (invalid move or payment failure).
    and MoveToActionSpaceRejectedEvent =
        { GameId: Id
          Nation: string
          Space: Space }

    // ──────────────────────────────────────────────────────────────────────────
    // Outbound Commands (to other bounded contexts)
    // ──────────────────────────────────────────────────────────────────────────

    /// Command to charge a nation for paid rondel movement (4-6 spaces).
    type ChargeMovementOutboundCommand =
        { GameId: Id
          Nation: string
          Amount: Amount
          BillingId: RondelBillingId }

    /// Command to void a previously initiated charge before payment completion.
    type VoidChargeOutboundCommand =
        { GameId: Id
          BillingId: RondelBillingId }

    /// Union of all outbound commands dispatched to other bounded contexts.
    type RondelOutboundCommand =
        | ChargeMovement of ChargeMovementOutboundCommand
        | VoidCharge of VoidChargeOutboundCommand

    // ──────────────────────────────────────────────────────────────────────────
    // Incoming Events (from other bounded contexts)
    // ──────────────────────────────────────────────────────────────────────────

    /// Incoming events from Accounting domain that affect Rondel state.
    type RondelIncomingEvent =
        | InvoicePaid of InvoicePaidEvent
        | InvoicePaymentFailed of InvoicePaymentFailedEvent

    /// Payment confirmation received from Accounting domain.
    and InvoicePaidEvent =
        { GameId: Id
          BillingId: RondelBillingId }

    /// Payment failure notification from Accounting domain.
    and InvoicePaymentFailedEvent =
        { GameId: Id
          BillingId: RondelBillingId }

    // ──────────────────────────────────────────────────────────────────────────
    // Dependencies
    // ──────────────────────────────────────────────────────────────────────────

    /// Load rondel state by GameId. Returns None if game not initialized.
    type LoadRondelState = Id -> RondelState option

    /// Save rondel state. Returns Error if persistence fails.
    type SaveRondelState = RondelState -> Result<unit, string>

    /// Publish rondel domain events to the event bus.
    type PublishRondelEvent = RondelEvent -> unit

    /// Dispatch outbound commands to other bounded contexts (e.g., Accounting).
    /// Infrastructure handles conversion to contract types and actual dispatch.
    type DispatchOutboundCommand = RondelOutboundCommand -> Result<unit, string>

    // ──────────────────────────────────────────────────────────────────────────
    // Transformations (Contract <-> Domain)
    // ──────────────────────────────────────────────────────────────────────────

    /// Transforms Contract SetToStartingPositionsCommand to Domain type.
    module SetToStartingPositionsCommand =
        /// Validate and transform Contract command to Domain command.
        /// Returns Error if GameId is invalid (Guid.Empty).
        val toDomain: Contract.Rondel.SetToStartingPositionsCommand -> Result<SetToStartingPositionsCommand, string>

    /// Transforms Contract MoveCommand to Domain type.
    module MoveCommand =
        /// Validate and transform Contract command to Domain command.
        /// Returns Error if GameId is invalid or Space name is unknown.
        val toDomain: Contract.Rondel.MoveCommand -> Result<MoveCommand, string>

    /// Transforms Domain RondelEvent to Contract type for publication.
    module RondelEvent =
        /// Transform Domain event to Contract event for cross-boundary communication.
        val toContract: RondelEvent -> Contract.Rondel.RondelEvent

    /// Transforms Domain ChargeMovementOutboundCommand to Accounting contract type.
    module ChargeMovementOutboundCommand =
        /// Convert domain charge command to Accounting contract for dispatch.
        val toContract: ChargeMovementOutboundCommand -> Contract.Accounting.ChargeNationForRondelMovementCommand

    /// Transforms Domain VoidChargeOutboundCommand to Accounting contract type.
    module VoidChargeOutboundCommand =
        /// Convert domain void command to Accounting contract for dispatch.
        val toContract: VoidChargeOutboundCommand -> Contract.Accounting.VoidRondelChargeCommand

    /// Transforms Domain RondelState to/from Contract type for persistence.
    module RondelState =
        /// Convert domain state to serializable contract representation.
        val toContract: RondelState -> Contract.Rondel.RondelState
        /// Reconstruct domain state from contract representation.
        /// Returns Error if Space names or BillingIds are invalid.
        val fromContract: Contract.Rondel.RondelState -> Result<RondelState, string>

    /// Transforms Domain PendingMovement to/from Contract type for persistence.
    module PendingMovement =
        /// Convert domain pending movement to serializable contract representation.
        val toContract: PendingMovement -> Contract.Rondel.PendingMovement
        /// Reconstruct domain pending movement from contract representation.
        /// Returns Error if Space name or BillingId is invalid.
        val fromContract: Contract.Rondel.PendingMovement -> Result<PendingMovement, string>

    /// Transforms Contract RondelInvoicePaid to Domain InvoicePaidEvent.
    module InvoicePaidEvent =
        /// Validate and transform Contract event to Domain event.
        /// Returns Error if GameId or BillingId is invalid.
        val toDomain: Contract.Accounting.RondelInvoicePaid -> Result<InvoicePaidEvent, string>

    /// Transforms Contract RondelInvoicePaymentFailed to Domain InvoicePaymentFailedEvent.
    module InvoicePaymentFailedEvent =
        /// Validate and transform Contract event to Domain event.
        /// Returns Error if GameId or BillingId is invalid.
        val toDomain: Contract.Accounting.RondelInvoicePaymentFailed -> Result<InvoicePaymentFailedEvent, string>

    // ──────────────────────────────────────────────────────────────────────────
    // Handlers
    // ──────────────────────────────────────────────────────────────────────────

    /// Initialize rondel for the specified game with the given nations.
    /// All nations are positioned at their starting positions.
    /// Publishes PositionedAtStart event. Throws if nation set is empty.
    val setToStartingPositions:
        LoadRondelState -> SaveRondelState -> PublishRondelEvent -> SetToStartingPositionsCommand -> unit

    /// Move a nation to the specified space on the rondel.
    /// Free moves (1-3 spaces) complete immediately with ActionDetermined event.
    /// Paid moves (4-6 spaces) dispatch charge and await payment confirmation.
    /// Throws for invalid moves (0 spaces, 7+ spaces, uninitialized game).
    val move: LoadRondelState -> SaveRondelState -> PublishRondelEvent -> DispatchOutboundCommand -> MoveCommand -> unit

    /// Process invoice payment confirmation from Accounting domain.
    /// Completes pending movement and publishes ActionDetermined event.
    /// Idempotent: ignores events for non-existent pending movements.
    val onInvoicedPaid:
        LoadRondelState -> SaveRondelState -> PublishRondelEvent -> InvoicePaidEvent -> Result<unit, string>

    /// Process invoice payment failure from Accounting domain.
    /// Rejects movement and publishes MoveToActionSpaceRejected event.
    val onInvoicePaymentFailed:
        LoadRondelState -> SaveRondelState -> PublishRondelEvent -> InvoicePaymentFailedEvent -> Result<unit, string>
