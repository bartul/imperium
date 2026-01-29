module Imperium.UnitTests.TerminalRondelStoreTests

open Expecto
open Imperium.Primitives
open Imperium.Rondel
open Imperium.Terminal.Rondel

// ──────────────────────────────────────────────────────────────────────────
// Tests
// ──────────────────────────────────────────────────────────────────────────

[<Tests>]
let tests =
    testList
        "Terminal.RondelStore"
        [ testCase "load returns None for unknown game"
          <| fun _ ->
              let store = InMemoryRondelStore.create ()
              let result = store.Load(Id.newId ()) |> Async.RunSynchronously

              Expect.isNone result "unknown game should return None"

          testCase "save then load returns saved state"
          <| fun _ ->
              let store = InMemoryRondelStore.create ()
              let gameId = Id.newId ()

              let state: RondelState =
                  { GameId = gameId
                    NationPositions = Map.ofList [ ("Austria", Some Space.Factory) ]
                    PendingMovements = Map.empty }

              store.Save state |> Async.RunSynchronously |> ignore
              let loaded = store.Load gameId |> Async.RunSynchronously

              Expect.isSome loaded "saved state should be loadable"
              Expect.equal loaded.Value.GameId gameId "loaded state should match"

          testCase "save overwrites existing state"
          <| fun _ ->
              let store = InMemoryRondelStore.create ()
              let gameId = Id.newId ()

              let state1: RondelState =
                  { GameId = gameId
                    NationPositions = Map.ofList [ ("Austria", Some Space.Factory) ]
                    PendingMovements = Map.empty }

              let state2: RondelState =
                  { GameId = gameId
                    NationPositions = Map.ofList [ ("Austria", Some Space.Taxation) ]
                    PendingMovements = Map.empty }

              store.Save state1 |> Async.RunSynchronously |> ignore
              store.Save state2 |> Async.RunSynchronously |> ignore
              let loaded = store.Load gameId |> Async.RunSynchronously

              Expect.isSome loaded "state should exist"

              Expect.equal loaded.Value.NationPositions.["Austria"] (Some Space.Taxation) "should have updated position" ]
