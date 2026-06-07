module Imperium.UnitTests.Gameplay.Assertions

open Expecto
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

let assertSetupCompleted gameId =
    assertExactEvent (SetupCompleted { GameId = gameId }) "the gameplay should announce that setup is complete"

let assertNoEvents = events.HasSize 0 "no game events should be published yet"

let assertNoOutboundCommands =
    commands.HasSize 0 "no outbound commands should be emitted"

// ────────────────────────────────────────────────────────────────────────────────
// Query assertions
// ────────────────────────────────────────────────────────────────────────────────

let getGameplayStatusResult ctx = ctx.GetGameplayStatus()

let assertNoGameplayStatus ctx =
    Expect.isNone (getGameplayStatusResult ctx) "no gameplay status should be returned"

let assertGameplayStatus ctx =
    Expect.isSome (getGameplayStatusResult ctx) "gameplay status should be returned"

let assertGameplayStatusForGameId gameId ctx =
    let result = getGameplayStatusResult ctx
    Expect.isSome result "gameplay status should be returned"
    Expect.equal result.Value.GameId gameId "gameplay status should belong to expected game"

let assertGameplayInPlay expected ctx =
    let result = getGameplayStatusResult ctx
    Expect.isSome result "gameplay status should be returned"
    Expect.equal result.Value.InPlay expected "in-play status should match"

let assertGameplayPlayerCount expected ctx =
    let result = getGameplayStatusResult ctx
    Expect.isSome result "gameplay status should be returned"
    Expect.equal result.Value.NumberOfPlayers expected "player count should match"
