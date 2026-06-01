module Imperium.UnitTests.Gameplay.Assertions

open Imperium.Gameplay
open Imperium.Testing.Spec.CollectionAssert

// ────────────────────────────────────────────────────────────────────────────────
// Collection accessors
// ────────────────────────────────────────────────────────────────────────────────

let events = forAccessor (fun (ctx: Context) -> ctx.Events :> seq<_>)
let commands = forAccessor (fun (ctx: Context) -> ctx.Commands :> seq<_>)

// ────────────────────────────────────────────────────────────────────────────────
// Event and command primitives
// ────────────────────────────────────────────────────────────────────────────────

let assertExactEvent expected message = events.Has expected message
let assertExactCommand expected message = commands.Has expected message

// ────────────────────────────────────────────────────────────────────────────────
// Domain assertions
// ────────────────────────────────────────────────────────────────────────────────

let assertRondelAskedToSetStartingPositions gameId nations =
    assertExactCommand
        (SetRondelToStartingPositions { GameId = gameId; Nations = nations })
        "rondel should be asked to set its starting positions"

let assertNoEvents = events.HasSize 0 "no game events should be published yet"

let assertNoOutboundCommands =
    commands.HasSize 0 "no outbound commands should be emitted"
