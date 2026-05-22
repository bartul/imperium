namespace Imperium.Accounting

open Imperium
open Imperium.Primitives

// ──────────────────────────────────────────────────────────────────────────
// Commands
// ──────────────────────────────────────────────────────────────────────────

/// Union of all accounting commands for routing and dispatch.
type AccountingCommand =
    | ChargeNationForRondelMovement of ChargeNationForRondelMovementCommand
    | VoidRondelCharge of VoidRondelChargeCommand

/// Command to charge a nation for rondel movement.
and ChargeNationForRondelMovementCommand = { GameId: Id; Nation: string; Amount: Amount; BillingId: Id }

/// Command to void a previously initiated rondel charge.
and VoidRondelChargeCommand = { GameId: Id; BillingId: Id }

/// Transforms Contract ChargeNationForRondelMovementCommand to a Domain type.
module ChargeNationForRondelMovementCommand =
    /// Validate and transform Contract command to Domain command.
    /// Returns Error if GameId or BillingId is invalid.
    val fromContract:
        Contract.Accounting.ChargeNationForRondelMovementCommand -> Result<ChargeNationForRondelMovementCommand, string>

/// Transforms Contract VoidRondelChargeCommand to a Domain type.
module VoidRondelChargeCommand =
    /// Validate and transform Contract command to Domain command.
    /// Returns Error if GameId or BillingId is invalid.
    val fromContract: Contract.Accounting.VoidRondelChargeCommand -> Result<VoidRondelChargeCommand, string>
