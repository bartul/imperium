namespace Imperium.Gameplay

// ──────────────────────────────────────────────────────────────────────────
// Write-Side Dependencies
// ──────────────────────────────────────────────────────────────────────────

/// Load Gameplay state by GameId. Returns None if game is unknown.
type LoadGameplayState = GameId -> Async<GameplayState option>

/// Commit boundary for Gameplay effects.
type CommitGameplayEffects = GameplayEffects -> Async<unit>

/// Unified dependencies for all Gameplay handlers.
type GameplayDependencies = { Load: LoadGameplayState; Commit: CommitGameplayEffects }

// ──────────────────────────────────────────────────────────────────────────
// Read-Side Dependencies
// ──────────────────────────────────────────────────────────────────────────

/// Load the Gameplay status projection by GameId. Returns None if game is unknown.
type LoadGameplayStatusProjection = GameId -> Async<GameplayStatusView option>

/// Dependencies for Gameplay query handlers.
type GameplayQueryDependencies = { LoadStatus: LoadGameplayStatusProjection }
