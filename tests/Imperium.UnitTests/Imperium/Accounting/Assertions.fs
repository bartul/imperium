module Imperium.UnitTests.Accounting.Assertions

open Imperium.Accounting
open Imperium.Testing.Spec.CollectionAssert

// ────────────────────────────────────────────────────────────────────────────────
// Helpers
// ────────────────────────────────────────────────────────────────────────────────

let events = forAccessor (fun (ctx: Context) -> ctx.Events :> seq<_>)

let assertEventCount expected =
    events.HasSize expected "event count should match"

let assertPaymentConfirmed =
    events.HasAny
        (function
        | RondelInvoicePaid _ -> true
        | _ -> false)
        "payment confirmation should be published"

let assertNoPaymentConfirmed =
    events.HasNone
        (function
        | RondelInvoicePaid _ -> true
        | _ -> false)
        "no payment confirmation should be published"

let assertNoPaymentFailed =
    events.HasNone
        (function
        | RondelInvoicePaymentFailed _ -> true
        | _ -> false)
        "no payment failure should be published"

let assertExactPaymentConfirmed gameId billingId =
    events.Has (RondelInvoicePaid { GameId = gameId; BillingId = billingId }) "exact payment confirmation should match"
