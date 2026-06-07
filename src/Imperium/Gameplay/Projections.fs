namespace Imperium.Gameplay

// ----------------------------------------------------------------------------
// Projections
// ----------------------------------------------------------------------------

type GameplayStatusView = { GameId: GameId; InPlay: bool; NumberOfPlayers: int }

module GameplayStatusProjection =
    let fromState (state: GameplayState) : GameplayStatusView =
        { GameId = state.GameId
          InPlay =
            match state.Status with
            | InPlay -> true
            | InSetup -> false
          NumberOfPlayers = state.Players |> PlayerRoster.value |> Set.count }
