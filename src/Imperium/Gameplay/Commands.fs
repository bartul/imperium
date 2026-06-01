namespace Imperium.Gameplay

open Imperium
open FsToolkit.ErrorHandling

// ──────────────────────────────────────────────────────────────────────────
// Commands
// ──────────────────────────────────────────────────────────────────────────

type GameplayCommand = StartGame of StartGameCommand

and StartGameCommand = { GameId: GameId; Players: PlayerRoster }

module StartGameCommand =
    let fromContract (command: Contract.Gameplay.StartGameCommand) : Result<StartGameCommand, string> =
        result {
            let! gameId = GameId.create command.GameId
            let! players = PlayerRoster.create (command.PlayerIds |> List.ofArray)
            return { GameId = gameId; Players = players }
        }

// ──────────────────────────────────────────────────────────────────────────
// Outbound Commands
// ──────────────────────────────────────────────────────────────────────────

type GameplayOutboundCommand = SetRondelToStartingPositions of SetRondelToStartingPositionsOutboundCommand

and SetRondelToStartingPositionsOutboundCommand = { GameId: GameId; Nations: Set<NationId> }

module SetRondelToStartingPositionsOutboundCommand =
    let toContract (command: SetRondelToStartingPositionsOutboundCommand) : Contract.Rondel.SetToStartingPositionsCommand =
        { GameId = command.GameId |> GameId.value
          Nations = command.Nations |> Set.toArray |> Array.map NationId.toString }
