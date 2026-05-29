namespace Imperium.Gameplay

open System
open Imperium.Primitives
open FsToolkit.ErrorHandling

// ──────────────────────────────────────────────────────────────────────────
// Domain Values
// ──────────────────────────────────────────────────────────────────────────

[<Struct>]
type GameId = private GameId of Id

module GameId =
    let create guid = guid |> Id.createMap GameId

    let newId () = Id.newId () |> GameId

    let value (GameId id) = id |> Id.value

    let toString (GameId id) = id |> Id.toString

    let tryParse raw = raw |> Id.tryParseMap GameId

[<RequireQualifiedAccess>]
type NationId =
    | Germany
    | GreatBritain
    | France
    | Russia
    | AustriaHungary
    | Italy

module NationId =
    let all =
        [ NationId.Germany
          NationId.GreatBritain
          NationId.France
          NationId.Russia
          NationId.AustriaHungary
          NationId.Italy ]
        |> Set.ofList

    let toString =
        function
        | NationId.Germany -> "Germany"
        | NationId.GreatBritain -> "Great Britain"
        | NationId.France -> "France"
        | NationId.Russia -> "Russia"
        | NationId.AustriaHungary -> "Austria-Hungary"
        | NationId.Italy -> "Italy"

    let tryParse (raw: string) =
        if String.IsNullOrWhiteSpace raw then
            Error "Nation cannot be empty."
        else
            let normalized = raw.Trim().ToLowerInvariant()

            let isMatch nation =
                let name = toString nation
                name.ToLowerInvariant() = normalized

            match all |> Seq.tryFind isMatch with
            | Some nation -> Ok nation
            | None ->
                let expected = all |> Seq.map toString |> String.concat ", "
                Error $"Unknown nation '{raw}'. Expected one of: {expected}."

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
        | count when count < minPlayers ->
            Error $"A game requires at least {minPlayers} players, but got {count}."
        | _ -> failwith "PlayerRoster.create is not fully implemented yet."
        // let count = List.length players

        // if count < minPlayers then
        //     Error $"A game requires at least {minPlayers} players, but got {count}."
        // elif count > maxPlayers then
        //     Error $"A game supports at most {maxPlayers} players, but got {count}."
        // elif (players |> List.distinct |> List.length) <> count then
        //     Error "Players must be unique."
        // else
            // players |> List.traverseResultM PlayerId.create |> Result.map PlayerRoster

    let value (PlayerRoster ids) = ids
