module Imperium.UnitTests.Gameplay.PlayerRoster

open System
open Expecto
open Imperium.Gameplay

[<Tests>]
let tests =
    testList
        "Gameplay.PlayerRoster"
        [ testCase "create rejects fewer than two players" (fun () ->
              Expect.isError (PlayerRoster.create [ Guid.NewGuid() ]) "expected a single-player roster to be rejected") ]
