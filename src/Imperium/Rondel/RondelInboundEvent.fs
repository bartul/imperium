namespace Imperium.Rondel

open Imperium
open Imperium.Primitives
open FsToolkit.ErrorHandling

// ──────────────────────────────────────────────────────────────────────────
// Inbound Events
// ──────────────────────────────────────────────────────────────────────────

type RondelInboundEvent =
    | InvoicePaid of InvoicePaidInboundEvent
    | InvoicePaymentFailed of InvoicePaymentFailedInboundEvent

and InvoicePaidInboundEvent = { GameId: Id; BillingId: RondelBillingId }

and InvoicePaymentFailedInboundEvent = { GameId: Id; BillingId: RondelBillingId }

module InvoicePaidInboundEvent =
    let fromContract (event: Contract.Accounting.RondelInvoicePaid) : Result<InvoicePaidInboundEvent, string> =
        result {
            let! gameId = Id.create event.GameId
            let! billingId = RondelBillingId.create event.BillingId

            return { GameId = gameId; BillingId = billingId }
        }

module InvoicePaymentFailedInboundEvent =
    let fromContract
        (event: Contract.Accounting.RondelInvoicePaymentFailed)
        : Result<InvoicePaymentFailedInboundEvent, string> =
        result {
            let! gameId = Id.create event.GameId
            let! billingId = RondelBillingId.create event.BillingId

            return { GameId = gameId; BillingId = billingId }
        }
