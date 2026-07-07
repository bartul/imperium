namespace Imperium.Gameplay

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
            GameplayEffects.create
                { s with
                    Status = InPlay
                    CompletedInitializations = s.CompletedInitializations |> Set.add GameInitialization.Rondel }
            |> GameplayEffects.withEvent (SetupCompleted { GameId = event.GameId })
        | _ -> GameplayEffects.none
