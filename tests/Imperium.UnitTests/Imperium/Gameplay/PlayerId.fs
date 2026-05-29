module Imperium.UnitTests.Gameplay.PlayerId

open System
open Expecto
open Imperium.Gameplay

[<Tests>]
let tests =
    testList
        "Gameplay.PlayerId"
        [ testCase "create rejects an empty guid" (fun () ->
              Expect.isError (PlayerId.create Guid.Empty) "expected empty guid to be rejected")

          testCase "create accepts a non-empty guid and round-trips its value" (fun () ->
              let guid = Guid.NewGuid()
              let playerId = Expect.wantOk (PlayerId.create guid) "expected guid to be accepted"
              Expect.equal (PlayerId.value playerId) guid "value should round-trip")

          testCase "tryParse round-trips the canonical string" (fun () ->
              let playerId = PlayerId.newId ()
              let parsed = Expect.wantOk (PlayerId.tryParse (PlayerId.toString playerId)) "expected string to parse"
              Expect.equal parsed playerId "parsed id should equal original")

          testCase "tryParse rejects an invalid string" (fun () ->
              Expect.isError (PlayerId.tryParse "not-a-guid") "expected invalid string to be rejected") ]
