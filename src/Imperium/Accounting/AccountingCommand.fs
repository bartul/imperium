namespace Imperium.Accounting

open Imperium
open Imperium.Primitives
open FsToolkit.ErrorHandling

// ──────────────────────────────────────────────────────────────────────────
// Commands
// ──────────────────────────────────────────────────────────────────────────

type AccountingCommand =
    | ChargeNationForRondelMovement of ChargeNationForRondelMovementCommand
    | VoidRondelCharge of VoidRondelChargeCommand

and ChargeNationForRondelMovementCommand = { GameId: Id; Nation: string; Amount: Amount; BillingId: Id }

and VoidRondelChargeCommand = { GameId: Id; BillingId: Id }

module ChargeNationForRondelMovementCommand =
    let fromContract
        (cmd: Contract.Accounting.ChargeNationForRondelMovementCommand)
        : Result<ChargeNationForRondelMovementCommand, string> =
        result {
            let! gameId = Id.create cmd.GameId
            let! billingId = Id.create cmd.BillingId

            return { GameId = gameId; Nation = cmd.Nation; Amount = cmd.Amount; BillingId = billingId }
        }

module VoidRondelChargeCommand =
    let fromContract (cmd: Contract.Accounting.VoidRondelChargeCommand) : Result<VoidRondelChargeCommand, string> =
        result {
            let! gameId = Id.create cmd.GameId
            let! billingId = Id.create cmd.BillingId

            return { GameId = gameId; BillingId = billingId }
        }
