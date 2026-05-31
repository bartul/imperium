namespace Imperium.Gameplay

open Imperium
open FsToolkit.ErrorHandling

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
    let fromContract (event: Contract.Rondel.PositionedAtStart) : Result<RondelPositionedAtStartInboundEvent, string> =
        result {
            let! gameId = GameId.create event.GameId
            return { GameId = gameId }
        }
