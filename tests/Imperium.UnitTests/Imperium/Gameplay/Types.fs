module Imperium.UnitTests.Gameplay.Types

open System
open Expecto
open Imperium.Gameplay

[<Tests>]
let tests =
    testList
        "Gameplay.Types"
        [ testList
              "GameId"
              [ testCase "create rejects an empty guid" (fun () ->
                    Expect.isError (GameId.create Guid.Empty) "expected empty guid to be rejected") ] ]