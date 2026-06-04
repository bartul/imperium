namespace Imperium.Gameplay

// ──────────────────────────────────────────────────────────────────────────
// State
// ──────────────────────────────────────────────────────────────────────────

/// Current lifecycle status for a game.
type GameStatus =
    | InSetup
    | InPlay

/// Setup work completed by participating bounded contexts.
[<RequireQualifiedAccess>]
type GameInitialization = | Rondel

/// Persistent Gameplay state for a game lifecycle.
type GameplayState =
    { GameId: GameId; Players: PlayerRoster; Status: GameStatus; CompletedInitializations: Set<GameInitialization> }
