namespace Imperium.Rondel

open Imperium
open Imperium.Primitives

// ──────────────────────────────────────────────────────────────────────────
// Inbound Events
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
