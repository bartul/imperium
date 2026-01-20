namespace Imperium

open System
open Imperium.Primitives

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

    /// Union of all outbound commands dispatched to other bounded contexts.
    type RondelOutboundCommand =
        | ChargeMovement of ChargeMovementOutboundCommand
        | VoidCharge of VoidChargeOutboundCommand

    /// Command to charge a nation for paid rondel movement (4-6 spaces).
    and ChargeMovementOutboundCommand =
        { GameId: Id
          Nation: string
          Amount: Amount
          BillingId: RondelBillingId }

    /// Command to void a previously initiated charge before payment completion.
    and VoidChargeOutboundCommand =
        { GameId: Id
          BillingId: RondelBillingId }

    // ──────────────────────────────────────────────────────────────────────────
    // Incoming Events (from other bounded contexts)
    // ──────────────────────────────────────────────────────────────────────────

    /// Inbound events from Accounting domain that affect Rondel state.
    type RondelInboundEvent =
        | InvoicePaid of InvoicePaidInboundEvent
        | InvoicePaymentFailed of InvoicePaymentFailedInboundEvent

    /// Payment confirmation received from Accounting domain.
    and InvoicePaidInboundEvent =
        { GameId: Id
          BillingId: RondelBillingId }

    /// Payment failure notification from Accounting domain.
    and InvoicePaymentFailedInboundEvent =
        { GameId: Id
          BillingId: RondelBillingId }

    // ──────────────────────────────────────────────────────────────────────────
    // Dependencies
    // ──────────────────────────────────────────────────────────────────────────

    /// Load rondel state by GameId. Returns None if game not initialized.
    /// CancellationToken flows implicitly through Async context.
    type LoadRondelState = Id -> Async<RondelState option>

    /// Save rondel state. Returns Error if persistence fails.
    /// CancellationToken flows implicitly through Async context.
    type SaveRondelState = RondelState -> Async<Result<unit, string>>

    /// Publish rondel domain events to the event bus.
    /// CancellationToken flows implicitly through Async context.
    type PublishRondelEvent = RondelEvent -> Async<unit>

    /// Dispatch outbound commands to other bounded contexts (e.g., Accounting).
    /// Infrastructure handles conversion to contract types and actual dispatch.
    /// CancellationToken flows implicitly through Async context.
    type DispatchOutboundCommand = RondelOutboundCommand -> Async<Result<unit, string>>

    /// Unified dependencies for all Rondel handlers.
    /// All handlers receive the same dependencies record for consistency,
    /// even if some handlers don't use all dependencies.
    type RondelDependencies =
        { Load: LoadRondelState
          Save: SaveRondelState
          Publish: PublishRondelEvent
          Dispatch: DispatchOutboundCommand }

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
    type RondelPositionsView =
        { GameId: Id
          Positions: NationPositionView list }

    /// A nation's position on the rondel.
    and NationPositionView =
        { Nation: string
          CurrentSpace: Space option
          PendingSpace: Space option }

    /// Result of GetRondelOverview query.
    type RondelView =
        { GameId: Id; NationNames: string list }

    // ──────────────────────────────────────────────────────────────────────────
    // Query Dependencies
    // ──────────────────────────────────────────────────────────────────────────

    /// Load rondel state for queries. Same as write-side Load.
    /// Infrastructure may optimize with dedicated read store.
    type LoadRondelStateForQuery = Id -> Async<RondelState option>

    /// Dependencies for query handlers.
    type RondelQueryDependencies = { Load: LoadRondelStateForQuery }

    // ──────────────────────────────────────────────────────────────────────────
    // Transformations (Contract <-> Domain)
    // ──────────────────────────────────────────────────────────────────────────

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

    /// Transforms Contract RondelInvoicePaid to Domain InvoicePaidInboundEvent.
    module InvoicePaidInboundEvent =
        /// Validate and transform Contract event to Domain event.
        /// Returns Error if GameId or BillingId is invalid.
        val fromContract: Contract.Accounting.RondelInvoicePaid -> Result<InvoicePaidInboundEvent, string>

    /// Transforms Contract RondelInvoicePaymentFailed to Domain InvoicePaymentFailedInboundEvent.
    module InvoicePaymentFailedInboundEvent =
        /// Validate and transform Contract event to Domain event.
        /// Returns Error if GameId or BillingId is invalid.
        val fromContract:
            Contract.Accounting.RondelInvoicePaymentFailed -> Result<InvoicePaymentFailedInboundEvent, string>

    // ──────────────────────────────────────────────────────────────────────────
    // Handlers
    // ──────────────────────────────────────────────────────────────────────────

    /// Execute a rondel command. Routes to the appropriate command handler.
    /// CancellationToken flows implicitly through Async context.
    /// Throws if command execution fails (e.g., invalid state, persistence failure).
    val execute: RondelDependencies -> RondelCommand -> Async<unit>

    /// Handle an inbound event from other bounded contexts. Routes to the appropriate event handler.
    /// CancellationToken flows implicitly through Async context.
    /// Returns unit on success; raises exceptions on failure.
    val handle: RondelDependencies -> RondelInboundEvent -> Async<unit>

    // ──────────────────────────────────────────────────────────────────────────
    // Query Handlers
    // ──────────────────────────────────────────────────────────────────────────

    /// Get nation positions for a game.
    /// Returns None if game not found.
    val getNationPositions: RondelQueryDependencies -> GetNationPositionsQuery -> Async<RondelPositionsView option>

    /// Get rondel overview for a game.
    /// Returns None if game not found.
    val getRondelOverview: RondelQueryDependencies -> GetRondelOverviewQuery -> Async<RondelView option>
