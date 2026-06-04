namespace Imperium.Gameplay

open Imperium.Primitives

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
