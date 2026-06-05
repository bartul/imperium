namespace Imperium.Gameplay

open Imperium
// ──────────────────────────────────────────────────────────────────────────
// Integration Events
// ──────────────────────────────────────────────────────────────────────────

type GameplayEvent = SetupCompleted of SetupCompletedEvent

and SetupCompletedEvent = { GameId: GameId }

module GameplayEvent =
    let toContract event =
        match event with
        | SetupCompleted event -> Contract.Gameplay.SetupCompleted { GameId = GameId.value event.GameId }
