namespace Imperium.Gameplay

open Imperium.Primitives

// ──────────────────────────────────────────────────────────────────────────
// State
// ──────────────────────────────────────────────────────────────────────────

/// Current lifecycle status for a game.
type GameStatus =
    | InSetup
    | InPlay

/// Setup work completed by participating bounded contexts.
type GameInitialization = | RondelStartingPositions

/// Persistent Gameplay state for a game lifecycle.
type GameplayState =
    { GameId: GameId
      Players: PlayerRoster
      Status: GameStatus
      CompletedInitializations: Set<GameInitialization> }
