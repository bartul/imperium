module Imperium.UnitTests.SpecificationTests

open Expecto
open Imperium.Testing.Spec
open Imperium.Testing.Spec.Specification

let private runAndExpectPass runner (specification: Specification<_, _, _, _>) =
    let result =
        SpecRunner.runExpectation runner specification specification.Expectations.Head

    Expect.equal result.Outcome Passed "expectation should pass"

[<Tests>]
let tests =
    testList
        "Specification"
        [ testCase "specOn uses the provided context factory by default" (fun _ ->
              let specification =
                  specOn<int, NoState, unit, unit> (fun () -> 42) "uses default context" {
                      expect "context equals 42" (fun ctx ->
                          Expect.equal ctx 42 "specOn should populate the On factory")
                  }

              let runner: SpecRunner<int, NoState, NoState, unit, unit> = SpecRunner.empty
              runAndExpectPass runner specification)

          testCase "explicit on overrides the specOn default context factory" (fun _ ->
              let specification =
                  specOn<int, NoState, unit, unit> (fun () -> 1) "uses override context" {
                      on (fun () -> 2)
                      expect "context equals 2" (fun ctx -> Expect.equal ctx 2 "on should override the specOn default")
                  }

              let runner: SpecRunner<int, NoState, NoState, unit, unit> = SpecRunner.empty
              runAndExpectPass runner specification)

          testCase "last on wins when multiple on operations are present" (fun _ ->
              let specification =
                  specOn<int, NoState, unit, unit> (fun () -> 1) "uses last on context" {
                      on (fun () -> 2)
                      on (fun () -> 3)
                      expect "context equals 3" (fun ctx -> Expect.equal ctx 3 "the last on should win")
                  }

              let runner: SpecRunner<int, NoState, NoState, unit, unit> = SpecRunner.empty
              runAndExpectPass runner specification)

          testCase "plain spec remains compatible with explicit on" (fun _ ->
              let specification =
                  spec<int, NoState, unit, unit> "legacy spec" {
                      on (fun () -> 7)
                      expect "context equals 7" (fun ctx -> Expect.equal ctx 7 "spec should still support explicit on")
                  }

              let runner: SpecRunner<int, NoState, NoState, unit, unit> = SpecRunner.empty
              runAndExpectPass runner specification)

          testCase "expect accepts Expecto assertions that pass" (fun _ ->
              let runner: SpecRunner<int, NoState, NoState, unit, unit> = SpecRunner.empty

              let specification =
                  specOn<int, NoState, unit, unit> (fun () -> 42) "assertion pass" {
                      expect "context equals 42" (fun ctx -> Expect.equal ctx 42 "should be 42")
                  }

              let result =
                  SpecRunner.runExpectation runner specification specification.Expectations.Head

              Expect.equal result.Outcome Passed "assertion should pass") ]
