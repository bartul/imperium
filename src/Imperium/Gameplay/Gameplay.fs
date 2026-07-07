namespace Imperium.Gameplay

// ──────────────────────────────────────────────────────────────────────────
// Facade
// ──────────────────────────────────────────────────────────────────────────

[<RequireQualifiedAccess>]
module Gameplay =
    let execute deps command =
        async {
            let gameId =
                match command with
                | StartGame cmd -> cmd.GameId

            let! state = deps.Load gameId

            let effects =
                match command with
                | StartGame cmd -> Handlers.startGame state cmd

            do! deps.Commit effects
        }

    let getGameplayStatus
        (deps: GameplayQueryDependencies)
        (query: GetGameplayStatusQuery)
        : Async<GameplayStatusView option> =
        Queries.getGameplayStatus deps query

    let handle deps event =
        async {
            let gameId =
                match event with
                | RondelPositionedAtStart evt -> evt.GameId

            let! state = deps.Load gameId

            let effects =
                match event with
                | RondelPositionedAtStart evt -> Handlers.rondelPositionedAtStart state evt

            do! deps.Commit effects
        }
