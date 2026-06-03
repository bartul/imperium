namespace Imperium.Gameplay

open Imperium.Primitives

// ──────────────────────────────────────────────────────────────────────────
// State
// ──────────────────────────────────────────────────────────────────────────

type GameStatus =
    | InSetup
    | InPlay

type GameInitialization = | Rondel

type GameplayState =
    { GameId: GameId; Players: PlayerRoster; Status: GameStatus; CompletedInitializations: Set<GameInitialization> }
