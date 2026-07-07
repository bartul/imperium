namespace Imperium.Accounting

open Imperium
open Imperium.Primitives

// ──────────────────────────────────────────────────────────────────────────
// Events
// ──────────────────────────────────────────────────────────────────────────

/// Integration events published by Accounting to notify other bounded contexts.
type AccountingEvent =
    | RondelInvoicePaid of RondelInvoicePaidEvent
    | RondelInvoicePaymentFailed of RondelInvoicePaymentFailedEvent

/// Invoice payment succeeded.
and RondelInvoicePaidEvent = { GameId: Id; BillingId: Id }

/// Invoice payment failed.
and RondelInvoicePaymentFailedEvent = { GameId: Id; BillingId: Id }

/// Transforms Domain AccountingEvent to a Contract type for publication.
module AccountingEvent =
    /// Transform Domain event to Contract event for cross-boundary communication.
    val toContract: AccountingEvent -> Contract.Accounting.AccountingEvent
