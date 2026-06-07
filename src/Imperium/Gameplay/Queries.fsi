namespace Imperium.Gameplay

// ----------------------------------------------------------------------------
// Queries
// ----------------------------------------------------------------------------

/// Query for the public Gameplay status projection.
type GetGameplayStatusQuery = { GameId: GameId }

/// Internal query handlers for the Gameplay bounded context.
/// Routers in the public Gameplay facade delegate to these.
module internal Queries =
    /// Get the public Gameplay status projection for a game. Returns None if game not found.
    val getGameplayStatus: GameplayQueryDependencies -> GetGameplayStatusQuery -> Async<GameplayStatusView option>
