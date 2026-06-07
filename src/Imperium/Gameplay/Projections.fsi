namespace Imperium.Gameplay

// ----------------------------------------------------------------------------
// Projections
// ----------------------------------------------------------------------------

/// Current public status projection for a game.
type GameplayStatusView = { GameId: GameId; InPlay: bool; NumberOfPlayers: int }

/// Projection builders for Gameplay read models.
module GameplayStatusProjection =
    /// Project the current Gameplay state into the public status view.
    val fromState: GameplayState -> GameplayStatusView
