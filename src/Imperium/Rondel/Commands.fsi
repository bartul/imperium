namespace Imperium.Rondel

open Imperium
open Imperium.Primitives

// ──────────────────────────────────────────────────────────────────────────
// Domain Commands
// ──────────────────────────────────────────────────────────────────────────

/// Union of all rondel commands for routing and dispatch.
type RondelCommand =
    | SetToStartingPositions of SetToStartingPositionsCommand
    | Move of MoveCommand

/// Initialize rondel for a game with the participating nations.
and SetToStartingPositionsCommand = { GameId: Id; Nations: Set<string> }

/// Request to move a nation to a specific space on the rondel.
and MoveCommand = { GameId: Id; Nation: string; Space: Space }

/// Transforms Contract SetToStartingPositionsCommand to Domain type.
module SetToStartingPositionsCommand =
    /// Validate and transform Contract command to Domain command.
    /// Returns Error if GameId is invalid (Guid.Empty).
    val fromContract: Contract.Rondel.SetToStartingPositionsCommand -> Result<SetToStartingPositionsCommand, string>

/// Transforms Contract MoveCommand to Domain type.
module MoveCommand =
    /// Validate and transform Contract command to Domain command.
    /// Returns Error if GameId is invalid or Space name is unknown.
    val fromContract: Contract.Rondel.MoveCommand -> Result<MoveCommand, string>

// ──────────────────────────────────────────────────────────────────────────
// Outbound Commands (to other bounded contexts)
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
