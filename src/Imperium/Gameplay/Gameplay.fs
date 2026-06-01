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
                    { State = None
                      IntegrationEvents = []
                      OutboundCommands =
                        [ SetRondelToStartingPositions { GameId = command.GameId; Nations = NationId.all } ] }
            }

    let execute (deps: GameplayDependencies) (command: GameplayCommand) : Async<unit> =
        async {
            let! effects =
                match command with
                | StartGame cmd -> Handlers.startGame deps.Load cmd

            do! deps.Commit effects
        }

    let handle (_deps: GameplayDependencies) (_event: GameplayInboundEvent) : Async<unit> = failwith "Not implemented."
