namespace Imperium.Gameplay

// ----------------------------------------------------------------------------
// Queries
// ----------------------------------------------------------------------------

type GetGameplayStatusQuery = { GameId: GameId }

module internal Queries =
    let getGameplayStatus
        (deps: GameplayQueryDependencies)
        (query: GetGameplayStatusQuery)
        : Async<GameplayStatusView option> =
        deps.LoadStatus query.GameId
