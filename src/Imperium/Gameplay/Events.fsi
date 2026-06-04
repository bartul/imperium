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

// ──────────────────────────────────────────────────────────────────────────
// Incoming Events
// ──────────────────────────────────────────────────────────────────────────

/// Inbound events from other bounded contexts that affect Gameplay state.
type GameplayInboundEvent = RondelPositionedAtStart of RondelPositionedAtStartInboundEvent

/// Rondel confirmed participating nations have been positioned at start.
and RondelPositionedAtStartInboundEvent = { GameId: GameId }

/// Transforms Contract PositionedAtStart to Domain RondelPositionedAtStartInboundEvent.
module RondelPositionedAtStartInboundEvent =
    /// Validate and transform Contract event to Domain event.
    val fromContract: Contract.Rondel.PositionedAtStart -> Result<RondelPositionedAtStartInboundEvent, string>
