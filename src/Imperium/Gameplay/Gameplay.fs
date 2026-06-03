namespace Imperium.Gameplay

// ──────────────────────────────────────────────────────────────────────────
// Facade
// ──────────────────────────────────────────────────────────────────────────

[<RequireQualifiedAccess>]
module Gameplay =
    module internal Handlers =
        let startGame (load: LoadGameplayState) (command: StartGameCommand) : Async<GameplayEffects> =
            async {
                let! state = load command.GameId

                return
                    if state.IsSome then
                        { State = None; IntegrationEvents = []; OutboundCommands = [] }
                    else
                        { State =
                            Some
                                { GameId = command.GameId
                                  Status = InPlay
                                  Players = command.Players
                                  CompletedInitializations = Set.empty }
                          IntegrationEvents = []
                          OutboundCommands =
                            [ SetRondelToStartingPositions { GameId = command.GameId; Nations = NationId.all } ] }
            }

        let rondelPositionedAtStart
            (load: LoadGameplayState)
            (event: RondelPositionedAtStartInboundEvent)
            : Async<GameplayEffects> =
            async {
                let! state = load event.GameId

                return
                    match state with
                    | Some s ->
                        { State =
                            Some { s with CompletedInitializations = Set.singleton GameInitialization.Rondel }
                          IntegrationEvents = [ SetupCompleted { GameId = event.GameId } ]
                          OutboundCommands = [] }
                    | None -> { State = None; IntegrationEvents = []; OutboundCommands = [] }
            }

    let execute (deps: GameplayDependencies) (command: GameplayCommand) : Async<unit> =
        async {
            let! effects =
                match command with
                | StartGame cmd -> Handlers.startGame deps.Load cmd

            do! deps.Commit effects
        }

    let handle (deps: GameplayDependencies) (event: GameplayInboundEvent) : Async<unit> =
        async {
            let! effects =
                match event with
                | RondelPositionedAtStart rondelAtStartEvent ->
                    Handlers.rondelPositionedAtStart deps.Load rondelAtStartEvent

            do! deps.Commit effects
        }
