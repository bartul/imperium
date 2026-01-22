namespace Imperium

open Imperium.Primitives
open FsToolkit.ErrorHandling

module Accounting =

    // ──────────────────────────────────────────────────────────────────────────
    // Commands
    // ──────────────────────────────────────────────────────────────────────────

    type AccountingCommand =
        | ChargeNationForRondelMovement of ChargeNationForRondelMovementCommand
        | VoidRondelCharge of VoidRondelChargeCommand

    and ChargeNationForRondelMovementCommand =
        { GameId: Id
          Nation: string
          Amount: Amount
          BillingId: Id }

    and VoidRondelChargeCommand = { GameId: Id; BillingId: Id }

    // ──────────────────────────────────────────────────────────────────────────
    // Events
    // ──────────────────────────────────────────────────────────────────────────

    type AccountingEvent =
        | RondelInvoicePaid of RondelInvoicePaidEvent
        | RondelInvoicePaymentFailed of RondelInvoicePaymentFailedEvent

    and RondelInvoicePaidEvent = { GameId: Id; BillingId: Id }

    and RondelInvoicePaymentFailedEvent = { GameId: Id; BillingId: Id }

    // ──────────────────────────────────────────────────────────────────────────
    // Dependencies
    // ──────────────────────────────────────────────────────────────────────────

    type PublishAccountingEvent = AccountingEvent -> Async<unit>

    type AccountingDependencies = { Publish: PublishAccountingEvent }

    // ──────────────────────────────────────────────────────────────────────────
    // Transformations (Contract <-> Domain)
    // ──────────────────────────────────────────────────────────────────────────

    module ChargeNationForRondelMovementCommand =
        let fromContract
            (cmd: Contract.Accounting.ChargeNationForRondelMovementCommand)
            : Result<ChargeNationForRondelMovementCommand, string> =
            result {
                let! gameId = Id.create cmd.GameId
                let! billingId = Id.create cmd.BillingId

                return
                    { GameId = gameId
                      Nation = cmd.Nation
                      Amount = cmd.Amount
                      BillingId = billingId }
            }

    module VoidRondelChargeCommand =
        let fromContract
            (cmd: Contract.Accounting.VoidRondelChargeCommand)
            : Result<VoidRondelChargeCommand, string> =
            result {
                let! gameId = Id.create cmd.GameId
                let! billingId = Id.create cmd.BillingId
                return { GameId = gameId; BillingId = billingId }
            }

    module AccountingEvent =
        let toContract (event: AccountingEvent) : Contract.Accounting.AccountingEvent =
            match event with
            | RondelInvoicePaid evt ->
                Contract.Accounting.RondelInvoicePaid
                    { GameId = Id.value evt.GameId
                      BillingId = Id.value evt.BillingId }
            | RondelInvoicePaymentFailed evt ->
                Contract.Accounting.RondelInvoicePaymentFailed
                    { GameId = Id.value evt.GameId
                      BillingId = Id.value evt.BillingId }

    // ──────────────────────────────────────────────────────────────────────────
    // Handlers
    // ──────────────────────────────────────────────────────────────────────────

    let execute (_: AccountingDependencies) (_: AccountingCommand) : Async<unit> =
        async { return () }
