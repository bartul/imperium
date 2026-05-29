module Imperium.UnitTests.Gameplay.PlayerId

open System
open Expecto
open Imperium.Gameplay

[<Tests>]
let tests =
    testList
        "Gameplay.PlayerId"
        [ testCase "create rejects an empty guid" (fun () ->
              Expect.isError (PlayerId.create Guid.Empty) "expected empty guid to be rejected") ]
