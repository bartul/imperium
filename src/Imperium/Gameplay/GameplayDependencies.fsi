namespace Imperium.Gameplay

// ──────────────────────────────────────────────────────────────────────────
// Dependencies
// ──────────────────────────────────────────────────────────────────────────

/// Load Gameplay state by GameId. Returns None if game is unknown.
type LoadGameplayState = GameId -> Async<GameplayState option>

/// Commit boundary for Gameplay effects.
type CommitGameplayEffects = GameplayEffects -> Async<unit>

/// Unified dependencies for all Gameplay handlers.
type GameplayDependencies = { Load: LoadGameplayState; Commit: CommitGameplayEffects }
