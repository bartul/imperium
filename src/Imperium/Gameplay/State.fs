namespace Imperium.Gameplay

open Imperium.Primitives

// ──────────────────────────────────────────────────────────────────────────
// State
// ──────────────────────────────────────────────────────────────────────────

type GameStatus =
    | InSetup
    | InPlay

type GameInitialization = | RondelStartingPositions

type GameplayState =
    { GameId: GameId
      Nations: Set<NationId>
      Players: PlayerRoster
      Status: GameStatus
      CompletedInitializations: Set<GameInitialization> }
