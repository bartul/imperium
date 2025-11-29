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