namespace Imperium

open System

module Gameplay =
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
