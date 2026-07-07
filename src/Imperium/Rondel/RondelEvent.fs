namespace Imperium.Rondel

open Imperium
open Imperium.Primitives

// ──────────────────────────────────────────────────────────────────────────
// Integration Events
// ──────────────────────────────────────────────────────────────────────────

type RondelEvent =
    | PositionedAtStart of PositionedAtStartEvent
    | ActionDetermined of ActionDeterminedEvent
    | MoveToActionSpaceRejected of MoveToActionSpaceRejectedEvent

and PositionedAtStartEvent = { GameId: Id }

and ActionDeterminedEvent = { GameId: Id; Nation: string; Action: Action }

and MoveToActionSpaceRejectedEvent = { GameId: Id; Nation: string; Space: Space }

module RondelEvent =
    let toContract event =
        match event with
        | PositionedAtStart e -> Contract.Rondel.PositionedAtStart { GameId = Id.value e.GameId }
        | ActionDetermined e ->
            Contract.Rondel.ActionDetermined
                { GameId = Id.value e.GameId; Nation = e.Nation; Action = Action.toString e.Action }
        | MoveToActionSpaceRejected e ->
            Contract.Rondel.MoveToActionSpaceRejected
                { GameId = Id.value e.GameId; Nation = e.Nation; Space = Space.toString e.Space }
