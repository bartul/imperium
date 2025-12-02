namespace Imperium.Primitives

open System

type Id = Id of Guid

module Id =
    let create guid =
        if guid = Guid.Empty then
            Error "Id cannot be Guid.Empty."
        else
            Ok (Id guid)

    let createMap mapper guid = create guid |> Result.map mapper

    let newId () = Guid.NewGuid() |> Id

    let value (Id g) = g

    let toString (Id g) = g.ToString()

    let tryParse (raw: string) =
        match Guid.TryParse raw with
        | true, guid -> create guid
        | false, _ -> Error "Invalid GUID format."

    let tryParseMap mapper raw = tryParse raw |> Result.map mapper

[<Measure>]
type M // million

/// Shared amount representation across economic flows (millions).
[<Struct>]
type Amount = private Amount of int<M>

module Amount =
    [<RequireQualifiedAccess>]
    type Error =
        | NegativeAmount of string

    let create (millions: int) =
        if millions < 0 then
            Error "Amount cannot be negative (millions)."
        else
            Ok (Amount(millions * 1<M>))

    let unsafe (millions: int) = Amount(millions * 1<M>)
    let value (Amount v) = int v

    let zero = Amount 0<M>
    let (+) (Amount a) (Amount b) = Amount(a + b)
    let (-) (Amount a) (Amount b) = Amount(a - b)

    let tryParse (raw: string) =
        match Int32.TryParse raw with
        | true, v -> create v
        | false, _ -> Error $"Invalid amount format: '%s{raw}'."