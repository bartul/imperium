namespace Imperium.Gameplay

open Imperium
// ──────────────────────────────────────────────────────────────────────────
// Integration Events
// ──────────────────────────────────────────────────────────────────────────

/// Integration events published by Gameplay to notify other bounded contexts.
type GameplayEvent = SetupCompleted of SetupCompletedEvent

/// Published when all currently required setup work is complete and play can begin.
and SetupCompletedEvent = { GameId: GameId }

/// Transforms Domain GameplayEvent to Contract type for publication.
module GameplayEvent =
    /// Transform Domain event to Contract event for cross-boundary communication.
    val toContract: GameplayEvent -> Contract.Gameplay.GameplayEvent
