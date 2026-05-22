namespace Imperium.Rondel

open Imperium
open Imperium.Primitives

// ──────────────────────────────────────────────────────────────────────────
// Integration Events (published by Rondel)
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
