namespace Imperium.Gameplay

open System
open Imperium.Primitives
open FsToolkit.ErrorHandling

// ──────────────────────────────────────────────────────────────────────────
// Domain Values
// ──────────────────────────────────────────────────────────────────────────

[<Struct>]
type PlayerId = private PlayerId of Id

module PlayerId =
    let create guid = guid |> Id.createMap PlayerId

    let newId () = Id.newId () |> PlayerId

    let value (PlayerId id) = id |> Id.value

    let toString (PlayerId id) = id |> Id.toString

    let tryParse raw = raw |> Id.tryParseMap PlayerId

type PlayerRoster = private PlayerRoster of Set<PlayerId>

module PlayerRoster =
    [<Literal>]
    let private minPlayers = 2

    [<Literal>]
    let private maxPlayers = 6

    let create (players: Guid list) : Result<PlayerRoster, string> =
        match List.length players with
        | count when count < minPlayers -> Error $"A game requires at least {minPlayers} players, but got {count}."
        | count when count > maxPlayers -> Error $"A game supports at most {maxPlayers} players, but got {count}."
        | count when count <> (players |> List.distinct |> List.length) -> Error "Players must be unique."
        | _ ->
            players
            |> List.traverseResultM PlayerId.create
            |> Result.map Set.ofList
            |> Result.map PlayerRoster

    let value (PlayerRoster ids) = ids
