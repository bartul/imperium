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
                    Expect.isError (GameId.create Guid.Empty) "expected empty guid to be rejected")

                testCase "create accepts a non-empty guid and round-trips its value" (fun () ->
                    let guid = Guid.NewGuid()
                    let gameId = Expect.wantOk (GameId.create guid) "expected guid to be accepted"
                    Expect.equal (GameId.value gameId) guid "value should round-trip")

                testCase "tryParse round-trips the canonical string" (fun () ->
                    let gameId = GameId.newId ()
                    let parsed = Expect.wantOk (GameId.tryParse (GameId.toString gameId)) "expected string to parse"
                    Expect.equal parsed gameId "parsed id should equal original")

                testCase "tryParse rejects an invalid string" (fun () ->
                    Expect.isError (GameId.tryParse "not-a-guid") "expected invalid string to be rejected") ] ]