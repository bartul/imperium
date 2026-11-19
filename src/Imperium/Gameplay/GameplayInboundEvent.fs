namespace Imperium.Gameplay

open Imperium
open FsToolkit.ErrorHandling
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
