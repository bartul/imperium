namespace Imperium.Accounting

open Imperium
open Imperium.Primitives

type AccountingEvent =
    | RondelInvoicePaid of RondelInvoicePaidEvent
    | RondelInvoicePaymentFailed of RondelInvoicePaymentFailedEvent

and RondelInvoicePaidEvent = { GameId: Id; BillingId: Id }

and RondelInvoicePaymentFailedEvent = { GameId: Id; BillingId: Id }

module AccountingEvent =
    let toContract (event: AccountingEvent) : Contract.Accounting.AccountingEvent =
        match event with
        | RondelInvoicePaid evt ->
            Contract.Accounting.RondelInvoicePaid { GameId = Id.value evt.GameId; BillingId = Id.value evt.BillingId }
        | RondelInvoicePaymentFailed evt ->
            Contract.Accounting.RondelInvoicePaymentFailed
                { GameId = Id.value evt.GameId; BillingId = Id.value evt.BillingId }
