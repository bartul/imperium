namespace Imperium.Gameplay

open Imperium.Primitives

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
