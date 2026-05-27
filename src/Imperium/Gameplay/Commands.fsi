namespace Imperium.Gameplay

open Imperium
open Imperium.Primitives

// ──────────────────────────────────────────────────────────────────────────
// Commands
// ──────────────────────────────────────────────────────────────────────────

/// Union of all Gameplay commands for routing and dispatch.
type GameplayCommand = StartGame of StartGameCommand

/// Intent to start a new game lifecycle.
and StartGameCommand = { GameId: GameId; Nations: Set<NationId>; Players: PlayerRoster }

/// Transforms Contract StartGameCommand to Domain type.
module StartGameCommand =
    /// Validate and transform Contract command to Domain command.
    val fromContract: Contract.Gameplay.StartGameCommand -> Result<StartGameCommand, string>

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
