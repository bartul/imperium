namespace Imperium.Gameplay

open Imperium
// ──────────────────────────────────────────────────────────────────────────
// Commands
// ──────────────────────────────────────────────────────────────────────────

/// Union of all Gameplay commands for routing and dispatch.
type GameplayCommand = StartGame of StartGameCommand

/// Intent to start a new game lifecycle.
and StartGameCommand = { GameId: GameId; Players: PlayerRoster }

/// Transforms Contract StartGameCommand to Domain type.
module StartGameCommand =
    /// Validate and transform Contract command to Domain command.
    val fromContract: Contract.Gameplay.StartGameCommand -> Result<StartGameCommand, string>
