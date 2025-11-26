namespace Imperium

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
        val all : Set<NationId>
        val toString : NationId -> string
        val tryParse : string -> Result<NationId, string>
