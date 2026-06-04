namespace Imperium.Gameplay

// ──────────────────────────────────────────────────────────────────────────
// Facade
// ──────────────────────────────────────────────────────────────────────────

[<RequireQualifiedAccess>]
module Gameplay =
    module internal Handlers =
        let startGame state (command: StartGameCommand) =
            match state with
            | Some _ -> GameplayEffects.none
            | None ->
                let newState =
                    { GameId = command.GameId
                      Status = InSetup
                      Players = command.Players
                      CompletedInitializations = Set.empty }

                let newCommand =
                    SetRondelToStartingPositions { GameId = command.GameId; Nations = NationId.all }

                GameplayEffects.create newState |> GameplayEffects.withCommand newCommand

        let rondelPositionedAtStart state (event: RondelPositionedAtStartInboundEvent) =
            match state with
            | Some s when s.CompletedInitializations |> Set.contains GameInitialization.Rondel |> not ->
                GameplayEffects.create { s with CompletedInitializations = Set.singleton GameInitialization.Rondel }
                |> GameplayEffects.withEvent (SetupCompleted { GameId = event.GameId })
            | _ -> GameplayEffects.none

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
