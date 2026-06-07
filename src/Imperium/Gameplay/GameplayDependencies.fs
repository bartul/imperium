namespace Imperium.Gameplay

// ──────────────────────────────────────────────────────────────────────────
// Write-Side Dependencies
// ──────────────────────────────────────────────────────────────────────────

type LoadGameplayState = GameId -> Async<GameplayState option>

type CommitGameplayEffects = GameplayEffects -> Async<unit>

type GameplayDependencies = { Load: LoadGameplayState; Commit: CommitGameplayEffects }

// ──────────────────────────────────────────────────────────────────────────
// Read-Side Dependencies
// ──────────────────────────────────────────────────────────────────────────

type LoadGameplayStatusProjection = GameId -> Async<GameplayStatusView option>

type GameplayQueryDependencies = { LoadStatus: LoadGameplayStatusProjection }
