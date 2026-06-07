namespace Imperium.Gameplay

// ──────────────────────────────────────────────────────────────────────────
// Facade
// ──────────────────────────────────────────────────────────────────────────

/// Public facade for the Gameplay bounded context.
/// Routes commands, inbound events, and queries to internal handlers.
[<RequireQualifiedAccess>]
module Gameplay =

    /// Execute a Gameplay command. Routes to the appropriate command handler and commits its effects.
    val execute: GameplayDependencies -> GameplayCommand -> Async<unit>

    /// Handle an inbound event from another bounded context. Routes to the appropriate event handler and commits its effects.
    val handle: GameplayDependencies -> GameplayInboundEvent -> Async<unit>

    /// Get the public Gameplay status projection for a game. Returns None if game not found.
    val getGameplayStatus: GameplayQueryDependencies -> GetGameplayStatusQuery -> Async<GameplayStatusView option>
