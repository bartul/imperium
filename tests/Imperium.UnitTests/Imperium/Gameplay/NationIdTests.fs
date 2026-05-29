module Imperium.UnitTests.Gameplay.NationId

open Expecto
open Imperium.Gameplay

[<Tests>]
let tests =
    testList
        "Gameplay.NationId"
        [ testCase "tryParse round-trips the canonical string" (fun () ->
              let parsed =
                  Expect.wantOk (NationId.tryParse (NationId.toString NationId.Germany)) "expected Germany to parse"

              Expect.equal parsed NationId.Germany "Germany should round-trip")

          testCase "tryParse rejects an unknown nation" (fun () ->
              Expect.isError (NationId.tryParse "Spain") "expected unknown nation to be rejected")

          testCase "tryParse rejects a blank value" (fun () ->
              Expect.isError (NationId.tryParse "  ") "expected blank nation to be rejected")

          testCase "tryParse is case insensitive" (fun () ->
              let parsed =
                  Expect.wantOk (NationId.tryParse "GERMANY") "expected case-insensitive parse to succeed"

              Expect.equal parsed NationId.Germany "should parse Germany regardless of case") ]
