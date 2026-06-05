namespace Imperium.Gameplay

open Imperium
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
