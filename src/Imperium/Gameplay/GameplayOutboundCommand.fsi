namespace Imperium.Gameplay

open Imperium
// ──────────────────────────────────────────────────────────────────────────
// Outbound Commands
// ──────────────────────────────────────────────────────────────────────────

/// Union of all outbound commands dispatched to other bounded contexts.
type GameplayOutboundCommand = SetRondelToStartingPositions of SetRondelToStartingPositionsOutboundCommand

/// Command to initialize Rondel with the participating nations.
and SetRondelToStartingPositionsOutboundCommand = { GameId: GameId; Nations: Set<NationId> }

/// Transforms Domain SetRondelToStartingPositionsOutboundCommand to Rondel contract type.
module SetRondelToStartingPositionsOutboundCommand =
    /// Convert domain setup command to Rondel contract for dispatch.
    val toContract: SetRondelToStartingPositionsOutboundCommand -> Contract.Rondel.SetToStartingPositionsCommand
