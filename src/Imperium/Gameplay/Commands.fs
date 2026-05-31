namespace Imperium.Gameplay

open Imperium
open Imperium.Primitives

// ──────────────────────────────────────────────────────────────────────────
// Commands
// ──────────────────────────────────────────────────────────────────────────

type GameplayCommand = StartGame of StartGameCommand

and StartGameCommand = { GameId: GameId; Nations: Set<NationId>; Players: PlayerRoster }

module StartGameCommand =
    let fromContract (command: Contract.Gameplay.StartGameCommand) : Result<StartGameCommand, string> =
        let gameId = GameId.create command.GameId
        match gameId with
        | Error e -> Error e
        | Ok _ -> failwith "Not implemented."

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
