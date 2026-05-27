namespace Imperium.Gameplay

open Imperium
open Imperium.Primitives

// ──────────────────────────────────────────────────────────────────────────
// Commands
// ──────────────────────────────────────────────────────────────────────────

type GameplayCommand = StartGame of StartGameCommand

and StartGameCommand = { GameId: GameId; Nations: Set<NationId>; Players: PlayerRoster }

module StartGameCommand =
    let fromContract (_command: Contract.Gameplay.StartGameCommand) : Result<StartGameCommand, string> =
        failwith "Not implemented."

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
