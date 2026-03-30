module Imperium.UnitTests.SpecTests

open Expecto
open Spec

let private runner: SpecRunner<int, NoState, NoState, unit, unit> = SpecRunner.empty

[<Tests>]
let tests =
    testList
        "Spec"
        [ testCase "specOn uses the provided context factory by default" (fun _ ->
              let specification =
                  specOn<int, NoState, unit, unit> (fun () -> 42) "uses default context" {
                      expect "context is available" (fun _ -> true)
                  }

              let context = prepareContext runner specification
              Expect.equal context 42 "specOn should populate the On factory")

          testCase "explicit on overrides the specOn default context factory" (fun _ ->
              let specification =
                  specOn<int, NoState, unit, unit> (fun () -> 1) "uses override context" {
                      on (fun () -> 2)
                      expect "override context is available" (fun _ -> true)
                  }

              let context = prepareContext runner specification
              Expect.equal context 2 "on should override the specOn default")

          testCase "plain spec remains compatible with explicit on" (fun _ ->
              let specification =
                  spec<int, NoState, unit, unit> "legacy spec" {
                      on (fun () -> 7)
                      expect "legacy context is available" (fun _ -> true)
                  }

              let context = prepareContext runner specification
              Expect.equal context 7 "spec should still support explicit on") ]
