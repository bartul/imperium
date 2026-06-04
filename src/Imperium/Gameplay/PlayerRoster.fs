namespace Imperium.Gameplay

open FsToolkit.ErrorHandling

// ──────────────────────────────────────────────────────────────────────────
// Domain Values
// ──────────────────────────────────────────────────────────────────────────

type PlayerRoster = private PlayerRoster of Set<PlayerId>

module PlayerRoster =
    [<Literal>]
    let private minPlayers = 2

    [<Literal>]
    let private maxPlayers = 6

    let create players =
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
