namespace Imperium

open System
open Imperium.Primitives

module Gameplay =

    [<Struct>]
    type GameId = private GameId of Id
    module GameId =
        val create : Guid -> Result<GameId, string>
        val newId : unit -> GameId
        val value : GameId -> Guid
        val toString : GameId -> string
        val tryParse : string -> Result<GameId, string>

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
