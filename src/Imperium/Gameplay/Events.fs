namespace Imperium.Gameplay

open Imperium
open FsToolkit.ErrorHandling

// ──────────────────────────────────────────────────────────────────────────
// Integration Events
// ──────────────────────────────────────────────────────────────────────────

type GameplayEvent = SetupCompleted of SetupCompletedEvent

and SetupCompletedEvent = { GameId: GameId }

module GameplayEvent =
    let toContract event =
        match event with
        | SetupCompleted event -> Contract.Gameplay.SetupCompleted { GameId = GameId.value event.GameId }

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
