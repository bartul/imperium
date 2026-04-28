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
                      expect "context is available" (fun _ -> ())
                  }

              let context = prepareContext runner specification
              Expect.equal context 42 "specOn should populate the On factory")

          testCase "explicit on overrides the specOn default context factory" (fun _ ->
              let specification =
                  specOn<int, NoState, unit, unit> (fun () -> 1) "uses override context" {
                      on (fun () -> 2)
                      expect "override context is available" (fun _ -> ())
                  }

              let context = prepareContext runner specification
              Expect.equal context 2 "on should override the specOn default")

          testCase "last on wins when multiple on operations are present" (fun _ ->
              let specification =
                  specOn<int, NoState, unit, unit> (fun () -> 1) "uses last on context" {
                      on (fun () -> 2)
                      on (fun () -> 3)
                      expect "last override context is available" (fun _ -> ())
                  }

              let context = prepareContext runner specification
              Expect.equal context 3 "the last on should win")

          testCase "plain spec remains compatible with explicit on" (fun _ ->
              let specification =
                  spec<int, NoState, unit, unit> "legacy spec" {
                      on (fun () -> 7)
                      expect "legacy context is available" (fun _ -> ())
                  }

              let context = prepareContext runner specification
              Expect.equal context 7 "spec should still support explicit on")

          testCase "expect accepts Expecto assertions that pass" (fun _ ->
              let specification =
                  specOn<int, NoState, unit, unit> (fun () -> 42) "assertion pass" {
                      expect "context equals 42" (fun ctx -> Expect.equal ctx 42 "should be 42")
                  }

              let result = runExpectation runner specification specification.Expectations.Head
              Expect.equal result.Outcome Passed "assertion should pass")

          testCase "runExpectation captures Expecto assertion failure" (fun _ ->
              let specification =
                  specOn<int, NoState, unit, unit> (fun () -> 1) "assertion failure" {
                      expect "context equals 2" (fun ctx -> Expect.equal ctx 2 "should be 2")
                  }

              let result = runExpectation runner specification specification.Expectations.Head

              match result.Outcome with
              | Failed _ -> ()
              | Passed -> failtest "expected assertion failure")

          testCase "runExpectation captures action failure" (fun _ ->
              let failingRunner = { runner with Execute = fun _ _ -> failwith "action exploded" }

              let specification =
                  specOn<int, NoState, unit, unit> (fun () -> 1) "action failure" {
                      when_command ()
                      expect "never reached" (fun _ -> ())
                  }

              let result =
                  runExpectation failingRunner specification specification.Expectations.Head

              match result.Outcome with
              | Failed ex -> Expect.stringContains ex.Message "action exploded" "exception message should match"
              | Passed -> failtest "expected action failure to be captured")

          testCase "runExpectation captures state snapshots" (fun _ ->
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
                  runExpectation countingRunner specification specification.Expectations.Head

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
                  runExpectation trackingRunner specification specification.Expectations.Head

              Expect.equal result.Outcome Passed "preserve should keep setup side effects")

          testCase "runExpectation preserves exception type for rethrow" (fun _ ->
              let specification =
                  specOn<int, NoState, unit, unit> (fun () -> 1) "rethrow test" {
                      expect "fails" (fun ctx -> Expect.equal ctx 99 "should be 99")
                  }

              let result = runExpectation runner specification specification.Expectations.Head

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
