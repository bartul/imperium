namespace Imperium.Rondel

open Imperium
open Imperium.Primitives

// ──────────────────────────────────────────────────────────────────────────
// Outbound Commands
// ──────────────────────────────────────────────────────────────────────────

type RondelOutboundCommand =
    | ChargeMovement of ChargeMovementOutboundCommand
    | VoidCharge of VoidChargeOutboundCommand

and ChargeMovementOutboundCommand = { GameId: Id; Nation: string; Amount: Amount; BillingId: RondelBillingId }

and VoidChargeOutboundCommand = { GameId: Id; BillingId: RondelBillingId }

module ChargeMovementOutboundCommand =
    let toContract (cmd: ChargeMovementOutboundCommand) : Contract.Accounting.ChargeNationForRondelMovementCommand =
        { GameId = Id.value cmd.GameId
          Nation = cmd.Nation
          Amount = cmd.Amount
          BillingId = RondelBillingId.value cmd.BillingId }

module VoidChargeOutboundCommand =
    let toContract (cmd: VoidChargeOutboundCommand) : Contract.Accounting.VoidRondelChargeCommand =
        { GameId = Id.value cmd.GameId; BillingId = RondelBillingId.value cmd.BillingId }
