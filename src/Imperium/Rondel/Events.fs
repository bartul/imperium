namespace Imperium.Rondel

open Imperium
open Imperium.Primitives
open FsToolkit.ErrorHandling

// ──────────────────────────────────────────────────────────────────────────
// Integration Events (published by Rondel)
// ──────────────────────────────────────────────────────────────────────────

type RondelEvent =
    | PositionedAtStart of PositionedAtStartEvent
    | ActionDetermined of ActionDeterminedEvent
    | MoveToActionSpaceRejected of MoveToActionSpaceRejectedEvent

and PositionedAtStartEvent = { GameId: Id }

and ActionDeterminedEvent = { GameId: Id; Nation: string; Action: Action }

and MoveToActionSpaceRejectedEvent = { GameId: Id; Nation: string; Space: Space }

module RondelEvent =
    let toContract event =
        match event with
        | PositionedAtStart e -> Contract.Rondel.PositionedAtStart { GameId = Id.value e.GameId }
        | ActionDetermined e ->
            Contract.Rondel.ActionDetermined
                { GameId = Id.value e.GameId; Nation = e.Nation; Action = Action.toString e.Action }
        | MoveToActionSpaceRejected e ->
            Contract.Rondel.MoveToActionSpaceRejected
                { GameId = Id.value e.GameId; Nation = e.Nation; Space = Space.toString e.Space }

// ──────────────────────────────────────────────────────────────────────────
// Incoming Events (from other bounded contexts)
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
