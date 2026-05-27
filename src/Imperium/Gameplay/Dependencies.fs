namespace Imperium.Gameplay

open Imperium.Primitives

// ──────────────────────────────────────────────────────────────────────────
// Dependencies
// ──────────────────────────────────────────────────────────────────────────

type LoadGameplayState = GameId -> Async<GameplayState option>

type GameplayEffects =
    { State: GameplayState option
      IntegrationEvents: GameplayEvent list
      OutboundCommands: GameplayOutboundCommand list }

type CommitGameplayEffects = GameplayEffects -> Async<unit>

type GameplayDependencies = { Load: LoadGameplayState; Commit: CommitGameplayEffects }
