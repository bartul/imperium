module Imperium.UnitTests.Gameplay.PlayerRoster

open System
open Expecto
open Imperium.Gameplay

[<Tests>]
let tests =
    testList
        "Gameplay.PlayerRoster"
        [ testCase "create rejects fewer than two players" (fun () ->
              Expect.isError (PlayerRoster.create [ Guid.NewGuid() ]) "expected a single-player roster to be rejected")

          testCase "create rejects more than six players" (fun () ->
              let players = List.init 7 (fun _ -> Guid.NewGuid())
              Expect.isError (PlayerRoster.create players) "expected a seven-player roster to be rejected")

          testCase "create rejects duplicate players" (fun () ->
              let duplicate = Guid.NewGuid()
              Expect.isError (PlayerRoster.create [ duplicate; duplicate ]) "expected duplicate players to be rejected") ]
