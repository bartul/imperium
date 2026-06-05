namespace Imperium.Gameplay

// ──────────────────────────────────────────────────────────────────────────
// Dependencies
// ──────────────────────────────────────────────────────────────────────────

type LoadGameplayState = GameId -> Async<GameplayState option>

type CommitGameplayEffects = GameplayEffects -> Async<unit>

type GameplayDependencies = { Load: LoadGameplayState; Commit: CommitGameplayEffects }
