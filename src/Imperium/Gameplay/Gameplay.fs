namespace Imperium.Gameplay

// ──────────────────────────────────────────────────────────────────────────
// Facade
// ──────────────────────────────────────────────────────────────────────────

[<RequireQualifiedAccess>]
module Gameplay =
    module internal Handlers =
        let startGame (state: GameplayState option) (command: StartGameCommand) : GameplayEffects =
            match state with
            | Some s -> { State = None; IntegrationEvents = []; OutboundCommands = [] }
            | None ->
                { State =
                    Some
                        { GameId = command.GameId
                          Status = InSetup
                          Players = command.Players
                          CompletedInitializations = Set.empty }
                  IntegrationEvents = []
                  OutboundCommands =
                    [ SetRondelToStartingPositions { GameId = command.GameId; Nations = NationId.all } ] }

        let rondelPositionedAtStart
            (state: GameplayState option)
            (event: RondelPositionedAtStartInboundEvent)
            : GameplayEffects =
            match state with
            | Some s when s.CompletedInitializations |> Set.contains GameInitialization.Rondel |> not ->
                { State = Some { s with CompletedInitializations = Set.singleton GameInitialization.Rondel }
                  IntegrationEvents = [ SetupCompleted { GameId = event.GameId } ]
                  OutboundCommands = [] }
            | _ -> { State = None; IntegrationEvents = []; OutboundCommands = [] }

    let execute (deps: GameplayDependencies) (command: GameplayCommand) : Async<unit> =
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

    let handle (deps: GameplayDependencies) (event: GameplayInboundEvent) : Async<unit> =
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
