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
        "Spec"
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

              Expect.equal result.Outcome Passed "assertion should pass")

          testCase "SpecRunner.runExpectation captures Expecto assertion failure" (fun _ ->
              let runner: SpecRunner<int, NoState, NoState, unit, unit> = SpecRunner.empty

              let specification =
                  specOn<int, NoState, unit, unit> (fun () -> 1) "assertion failure" {
                      expect "context equals 2" (fun ctx -> Expect.equal ctx 2 "should be 2")
                  }

              let result =
                  SpecRunner.runExpectation runner specification specification.Expectations.Head

              match result.Outcome with
              | Failed _ -> ()
              | Passed -> failtest "expected assertion failure")

          testCase "SpecRunner.runExpectation captures action failure" (fun _ ->
              let runner: SpecRunner<int, NoState, NoState, unit, unit> = SpecRunner.empty
              let failingRunner = { runner with Execute = fun _ _ -> failwith "action exploded" }

              let specification =
                  specOn<int, NoState, unit, unit> (fun () -> 1) "action failure" {
                      when_command ()
                      expect "never reached" (fun _ -> ())
                  }

              let result =
                  SpecRunner.runExpectation failingRunner specification specification.Expectations.Head

              match result.Outcome with
              | Failed ex -> Expect.stringContains ex.Message "action exploded" "exception message should match"
              | Passed -> failtest "expected action failure to be captured")

          testCase "SpecRunner.runExpectation captures state snapshots" (fun _ ->
              let mutable counter = 0

              let countingRunner: SpecRunner<int, NoState, int, unit, unit> =
                  { SpecRunner.empty with
                      Execute =
                          fun _ _ ->
                              counter <- counter + 1
                              ()
                      CaptureState = Some(fun _ -> counter) }

              let specification =
                  specOn<int, NoState, unit, unit> (fun () -> 0) "state capture" {
                      when_command ()
                      expect "state is captured" (fun _ -> ())
                  }

              counter <- 0

              let result =
                  SpecRunner.runExpectation countingRunner specification specification.Expectations.Head

              Expect.equal result.InitialState (Some 0) "initial state should be captured before actions"
              Expect.equal result.FinalState (Some 1) "final state should be captured after actions")

          testCase "preserve keeps setup side effects" (fun _ ->
              let mutable callCount = 0

              let trackingRunner: SpecRunner<int, NoState, NoState, unit, unit> =
                  { SpecRunner.empty with
                      Execute =
                          fun _ _ ->
                              callCount <- callCount + 1
                              ()
                      ClearEvents = fun _ -> callCount <- 0
                      ClearCommands = fun _ -> () }

              let specification =
                  specOn<int, NoState, unit, unit> (fun () -> 0) "preserve test" {
                      given_command ()
                      preserve
                      expect "setup effects are kept" (fun _ -> Expect.equal callCount 1 "should see setup effect")
                  }

              let result =
                  SpecRunner.runExpectation trackingRunner specification specification.Expectations.Head

              Expect.equal result.Outcome Passed "preserve should keep setup side effects")

          testCase "SpecRunner.runExpectation captures AssertException" (fun _ ->
              let runner: SpecRunner<int, NoState, NoState, unit, unit> = SpecRunner.empty

              let specification =
                  specOn<int, NoState, unit, unit> (fun () -> 1) "rethrow test" {
                      expect "fails" (fun ctx -> Expect.equal ctx 99 "should be 99")
                  }

              let result =
                  SpecRunner.runExpectation runner specification specification.Expectations.Head

              match result.Outcome with
              | Failed(:? AssertException) -> ()
              | Failed ex -> failtestf "expected AssertException, got %s" (ex.GetType().Name)
              | Passed -> failtest "expected failure")

          testCase "collection HasAny failure includes actual items" (fun _ ->
              let collection =
                  CollectionAssert.forAccessor (fun (items: int list) -> items :> seq<int>)

              try
                  collection.HasAny ((=) 4) "number should exist" [ 1; 2; 3 ]
                  failtest "expected assertion failure"
              with :? AssertException as ex ->
                  Expect.stringContains ex.Message "number should exist" "message should include assertion context"
                  Expect.stringContains ex.Message "Actual items: 1; 2; 3" "message should include actual items")

          testCase "collection HasNone failure includes matching items" (fun _ ->
              let collection =
                  CollectionAssert.forAccessor (fun (items: int list) -> items :> seq<int>)

              try
                  collection.HasNone (fun item -> item % 2 = 0) "number should not be even" [ 1; 2; 3; 4 ]
                  failtest "expected assertion failure"
              with :? AssertException as ex ->
                  Expect.stringContains
                      ex.Message
                      "number should not be even"
                      "message should include assertion context"

                  Expect.stringContains ex.Message "found: 2; 4" "message should include matching items"
                  Expect.stringContains ex.Message "Actual items: 1; 2; 3; 4" "message should include actual items") ]
