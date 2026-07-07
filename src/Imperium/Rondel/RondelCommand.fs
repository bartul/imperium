namespace Imperium.Rondel

open Imperium
open Imperium.Primitives
open FsToolkit.ErrorHandling

// ──────────────────────────────────────────────────────────────────────────
// Domain Commands
// ──────────────────────────────────────────────────────────────────────────

type RondelCommand =
    | SetToStartingPositions of SetToStartingPositionsCommand
    | Move of MoveCommand

and SetToStartingPositionsCommand = { GameId: Id; Nations: Set<string> }

and MoveCommand = { GameId: Id; Nation: string; Space: Space }

module SetToStartingPositionsCommand =
    let fromContract (command: Contract.Rondel.SetToStartingPositionsCommand) =
        result {
            let! id = Id.create command.GameId
            let nations = Set.ofArray command.Nations

            if Set.isEmpty nations then
                return! Error "Starting positions require at least one nation."
            else
                return { GameId = id; Nations = nations }
        }

module MoveCommand =
    let fromContract (command: Contract.Rondel.MoveCommand) : Result<MoveCommand, string> =
        result {
            let! id = Id.create command.GameId
            let! space = Space.fromString command.Space

            return { GameId = id; Nation = command.Nation; Space = space }
        }
