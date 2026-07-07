namespace Imperium.Rondel

open Imperium
open Imperium.Primitives

// ──────────────────────────────────────────────────────────────────────────
// Outbound Commands
// ──────────────────────────────────────────────────────────────────────────

/// Union of all outbound commands dispatched to other bounded contexts.
type RondelOutboundCommand =
    | ChargeMovement of ChargeMovementOutboundCommand
    | VoidCharge of VoidChargeOutboundCommand

/// Command to charge a nation for paid rondel movement (4-6 spaces).
and ChargeMovementOutboundCommand = { GameId: Id; Nation: string; Amount: Amount; BillingId: RondelBillingId }

/// Command to void a previously initiated charge before payment completion.
and VoidChargeOutboundCommand = { GameId: Id; BillingId: RondelBillingId }

/// Transforms Domain ChargeMovementOutboundCommand to Accounting contract type.
module ChargeMovementOutboundCommand =
    /// Convert domain charge command to Accounting contract for dispatch.
    val toContract: ChargeMovementOutboundCommand -> Contract.Accounting.ChargeNationForRondelMovementCommand

/// Transforms Domain VoidChargeOutboundCommand to Accounting contract type.
module VoidChargeOutboundCommand =
    /// Convert domain void command to Accounting contract for dispatch.
    val toContract: VoidChargeOutboundCommand -> Contract.Accounting.VoidRondelChargeCommand
