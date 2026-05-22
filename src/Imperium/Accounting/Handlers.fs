namespace Imperium.Accounting

module internal Handlers =

    /// Process charge command by auto-approving and publishing paid event.
    let chargeNationForRondelMovement
        (deps: AccountingDependencies)
        (cmd: ChargeNationForRondelMovementCommand)
        : Async<unit> =
        async {
            let event: AccountingEvent =
                RondelInvoicePaid { GameId = cmd.GameId; BillingId = cmd.BillingId }

            do! deps.Publish event
        }

    /// Process void command (skeleton does nothing).
    let voidRondelCharge (_: AccountingDependencies) (_: VoidRondelChargeCommand) : Async<unit> = async { return () }
