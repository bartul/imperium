namespace Imperium.Gameplay

open Imperium
open Imperium.Primitives

// ──────────────────────────────────────────────────────────────────────────
// Commands
// ──────────────────────────────────────────────────────────────────────────

type GameplayCommand = StartGame of StartGameCommand

and StartGameCommand = { GameId: GameId; Players: PlayerRoster }

module StartGameCommand =
    let fromContract (command: Contract.Gameplay.StartGameCommand) : Result<StartGameCommand, string> =
        let gameId = GameId.create command.GameId
        let players = PlayerRoster.create (command.PlayerIds |> List.ofArray)

        match gameId, players with
        | Error e, _ -> Error e
        | _, Error e -> Error e
        | Ok _, Ok _ -> failwith "Not implemented."

// ──────────────────────────────────────────────────────────────────────────
// Outbound Commands
// ──────────────────────────────────────────────────────────────────────────

type GameplayOutboundCommand = SetRondelToStartingPositions of SetRondelToStartingPositionsOutboundCommand

and SetRondelToStartingPositionsOutboundCommand = { GameId: GameId; Nations: Set<NationId> }

module SetRondelToStartingPositionsOutboundCommand =
    let toContract
        (_command: SetRondelToStartingPositionsOutboundCommand)
        : Contract.Rondel.SetToStartingPositionsCommand =
        failwith "Not implemented."
