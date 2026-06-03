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

/// Builders for constructing and accumulating GameplayEffects in a pipeline style.
module GameplayEffects =
    /// The initial empty effects: no state change, no events, no commands.
    val empty: GameplayEffects

    /// Returns effects with the given state applied.
    val withState: GameplayState -> GameplayEffects -> GameplayEffects

    /// Creates effects from a state, starting from empty.
    val create: GameplayState -> GameplayEffects

    /// Appends an integration event to effects.
    val withEvent: GameplayEvent -> GameplayEffects -> GameplayEffects

    /// Appends an outbound command to effects.
    val withCommand: GameplayOutboundCommand -> GameplayEffects -> GameplayEffects

/// Commit boundary for Gameplay effects.
type CommitGameplayEffects = GameplayEffects -> Async<unit>

/// Unified dependencies for all Gameplay handlers.
type GameplayDependencies = { Load: LoadGameplayState; Commit: CommitGameplayEffects }
