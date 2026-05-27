namespace Imperium.Gameplay

open Imperium.Primitives

// ──────────────────────────────────────────────────────────────────────────
// Dependencies
// ──────────────────────────────────────────────────────────────────────────

/// Load Gameplay state by GameId. Returns None if game is unknown.
type LoadGameplayState = GameId -> Async<GameplayState option>

/// Named effect shape returned by Gameplay handlers.
type GameplayEffects =
    { State: GameplayState option
      IntegrationEvents: GameplayEvent list
      OutboundCommands: GameplayOutboundCommand list }

/// Commit boundary for Gameplay effects.
type CommitGameplayEffects = GameplayEffects -> Async<unit>

/// Unified dependencies for all Gameplay handlers.
type GameplayDependencies = { Load: LoadGameplayState; Commit: CommitGameplayEffects }
