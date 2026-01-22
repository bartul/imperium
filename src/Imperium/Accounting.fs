namespace Imperium

open Imperium.Primitives

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
            (_: Contract.Accounting.ChargeNationForRondelMovementCommand)
            : Result<ChargeNationForRondelMovementCommand, string> =
            Error "Not implemented"

    module VoidRondelChargeCommand =
        let fromContract
            (_: Contract.Accounting.VoidRondelChargeCommand)
            : Result<VoidRondelChargeCommand, string> =
            Error "Not implemented"

    module AccountingEvent =
        let toContract (_: AccountingEvent) : Contract.Accounting.AccountingEvent =
            failwith "Not implemented"

    // ──────────────────────────────────────────────────────────────────────────
    // Handlers
    // ──────────────────────────────────────────────────────────────────────────

    let execute (_: AccountingDependencies) (_: AccountingCommand) : Async<unit> =
        async { return () }
