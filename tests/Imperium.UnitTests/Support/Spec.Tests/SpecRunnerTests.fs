module Imperium.UnitTests.SpecRunnerTests

open Expecto
open Imperium.Testing.Spec
open Imperium.Testing.Spec.Specification

[<Tests>]
let tests =
    testList
        "SpecRunner"
        [ testCase "runExpectation captures Expecto assertion failure" (fun _ ->
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

          testCase "runExpectation captures action failure" (fun _ ->
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

          testCase "runExpectation captures AssertException" (fun _ ->
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
              | Passed -> failtest "expected failure") ]
