module Imperium.UnitTests.Gameplay.NationId

open Expecto
open Imperium.Gameplay

[<Tests>]
let tests =
    testList
        "Gameplay.NationId"
        [ testCase "Germany round-trips through toString and tryParse" (fun () ->
              let parsed = Expect.wantOk (NationId.tryParse (NationId.toString NationId.Germany)) "expected Germany to parse"
              Expect.equal parsed NationId.Germany "Germany should round-trip") ]
