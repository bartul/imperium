namespace Imperium.Rondel

open Imperium
open Imperium.Primitives

// ──────────────────────────────────────────────────────────────────────────
// Integration Events
// ──────────────────────────────────────────────────────────────────────────

/// Integration events published by the Rondel domain to notify other bounded contexts.
type RondelEvent =
    | PositionedAtStart of PositionedAtStartEvent
    | ActionDetermined of ActionDeterminedEvent
    | MoveToActionSpaceRejected of MoveToActionSpaceRejectedEvent

/// Published when nations are positioned at their starting positions, rondel ready for play.
and PositionedAtStartEvent = { GameId: Id }

/// Published when a nation successfully completes a move and an action is determined.
and ActionDeterminedEvent = { GameId: Id; Nation: string; Action: Action }

/// Published when a nation's movement is rejected (invalid move or payment failure).
and MoveToActionSpaceRejectedEvent = { GameId: Id; Nation: string; Space: Space }

/// Transforms Domain RondelEvent to Contract type for publication.
module RondelEvent =
    /// Transform Domain event to Contract event for cross-boundary communication.
    val toContract: RondelEvent -> Contract.Rondel.RondelEvent
