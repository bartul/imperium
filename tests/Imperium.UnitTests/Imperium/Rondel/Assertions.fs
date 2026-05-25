module Imperium.UnitTests.Rondel.Assertions

open Expecto
open Imperium.Rondel
open Imperium.Primitives
open Imperium.Testing.Spec.CollectionAssert

// ────────────────────────────────────────────────────────────────────────────────
// Helpers
// ────────────────────────────────────────────────────────────────────────────────

let events = forAccessor (fun (ctx: Context) -> ctx.Events :> seq<_>)

let commands = forAccessor (fun (ctx: Context) -> ctx.Commands :> seq<_>)

let assertExactEvent event_ message = events.Has event_ message

let assertStartingPositionsSet gameId =
    assertExactEvent (PositionedAtStart { GameId = gameId }) "starting positions should be set"

let assertNoStartingPositionsSet gameId =
    events.HasNone (fun e -> e = PositionedAtStart { GameId = gameId }) "starting positions should not be set"

let assertActionDetermined =
    events.HasAny
        (function
        | ActionDetermined _ -> true
        | _ -> false)
        "action should be determined"

let assertNoActionDetermined =
    events.HasNone
        (function
        | ActionDetermined _ -> true
        | _ -> false)
        "no action should be determined"

let assertChargeCommand =
    commands.HasAny
        (function
        | ChargeMovement _ -> true
        | _ -> false)
        "charge command should be dispatched"

let assertNoChargeCommand =
    commands.HasNone
        (function
        | ChargeMovement _ -> true
        | _ -> false)
        "no charge command should be dispatched"

let assertChargeCommandOfM millions =
    let amount = Amount.unsafe millions

    commands.HasAny
        (function
        | ChargeMovement cmd when cmd.Amount = amount -> true
        | _ -> false)
        $"charge command of %d{millions}M should be dispatched"

let assertExactCommand command message = commands.Has command message

let assertExactEventCount event_ expectedCount message =
    events.Count event_ expectedCount message

let getNationPositionsResult ctx = ctx.GetNationPositions()

let newBillingId () = Id.newId () |> RondelBillingId.ofId

let assertNoNationPositions ctx =
    Expect.isNone (getNationPositionsResult ctx) "no positions should be returned"

let assertNationPositions ctx =
    Expect.isSome (getNationPositionsResult ctx) "positions should be returned"

let assertNationPositionsForGameId gameId ctx =
    let result = getNationPositionsResult ctx
    Expect.isSome result "positions should be returned"
    Expect.equal result.Value.GameId gameId "positions should belong to expected game"

let assertNationPositionsCount expectedCount ctx =
    let result = getNationPositionsResult ctx
    Expect.isSome result "positions should be returned"
    Expect.equal (List.length result.Value.Positions) expectedCount "nation position count should match"

let assertNationPosition nation currentSpace pendingSpace ctx =
    let result = getNationPositionsResult ctx
    Expect.isSome result "positions should be returned"

    let position = result.Value.Positions |> List.tryFind (fun p -> p.Nation = nation)

    Expect.isSome position $"position for %s{nation} should exist"
    Expect.equal position.Value.CurrentSpace currentSpace $"%s{nation} current space should match"
    Expect.equal position.Value.PendingSpace pendingSpace $"%s{nation} pending space should match"

let getRondelOverviewResult ctx = ctx.GetRondelOverview()

let assertNoRondelOverview ctx =
    Expect.isNone (getRondelOverviewResult ctx) "no overview should be returned"

let assertRondelOverview ctx =
    Expect.isSome (getRondelOverviewResult ctx) "overview should be returned"

let assertRondelOverviewForGameId gameId ctx =
    let result = getRondelOverviewResult ctx
    Expect.isSome result "overview should be returned"
    Expect.equal result.Value.GameId gameId "overview should belong to expected game"

let assertRondelOverviewNationNames expectedNames ctx =
    let result = getRondelOverviewResult ctx
    Expect.isSome result "overview should be returned"
    Expect.equal (result.Value.NationNames |> List.sort) (expectedNames |> List.sort) "nation names should match"
