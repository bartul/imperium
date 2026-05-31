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
        let nations = 
            command.Nations 
            |> Array.map NationId.tryParse
            |> Array.fold (fun acc result ->
                match acc, result with
                | Error accError, Error resultError -> Error (sprintf "%s; %s" accError resultError)   
                | Error e, _ -> Error e
                | _, Error e -> Error e
                | Ok set, Ok nation -> Ok (Set.add nation set)
            ) (Ok Set.empty)

        match gameId, nations with
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
