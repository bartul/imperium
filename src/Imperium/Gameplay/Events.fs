namespace Imperium.Gameplay

open Imperium
open Imperium.Primitives

// ──────────────────────────────────────────────────────────────────────────
// Integration Events
// ──────────────────────────────────────────────────────────────────────────

type GameplayEvent = SetupCompleted of SetupCompletedEvent

and SetupCompletedEvent = { GameId: GameId }

module GameplayEvent =
    let toContract (_event: GameplayEvent) : Contract.Gameplay.GameplayEvent = failwith "Not implemented."

// ──────────────────────────────────────────────────────────────────────────
// Incoming Events
// ──────────────────────────────────────────────────────────────────────────

type GameplayInboundEvent = RondelPositionedAtStart of RondelPositionedAtStartInboundEvent

and RondelPositionedAtStartInboundEvent = { GameId: GameId }

module RondelPositionedAtStartInboundEvent =
    let fromContract (_event: Contract.Rondel.PositionedAtStart) : Result<RondelPositionedAtStartInboundEvent, string> =
        failwith "Not implemented."
