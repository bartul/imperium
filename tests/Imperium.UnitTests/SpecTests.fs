module Imperium.UnitTests.SpecTests

open Expecto
open Spec

let private runner: SpecRunner<int, NoState, NoState, unit, unit> = SpecRunner.empty

let private specTests =
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

let private specFilterTests =
    testList
        "SpecFilter"
        [ testCase "apply with none preserves all expectations" (fun _ ->
              let specification =
                  specOn<int, NoState, unit, unit> (fun () -> 0) "untouched spec" {
                      expect "first" (fun _ -> ())
                      expect "second" (fun _ -> ())
                  }

              let result = SpecFilter.apply SpecFilter.none [] [ specification ]

              Expect.hasLength result 1 "one spec should survive"

              let returnedSpec = result[0]
              Expect.equal returnedSpec.Name "untouched spec" "spec name preserved"
              Expect.equal returnedSpec.GivenState None "GivenState unchanged"
              Expect.equal returnedSpec.GivenActions [] "GivenActions unchanged"
              Expect.equal returnedSpec.Preserve false "Preserve flag unchanged"
              Expect.equal returnedSpec.Actions [] "Actions unchanged"

              Expect.hasLength returnedSpec.Expectations 2 "both expectations should survive"
              Expect.equal returnedSpec.Expectations[0].Description "first" "first expectation preserved"
              Expect.equal returnedSpec.Expectations[1].Description "second" "second expectation preserved")

          testCase "fromArgs --filter prefix-matches the joined path with default dot separator" (fun _ ->
              let filter = SpecFilter.fromArgs [| "--filter"; "Imperium.Rondel" |]

              Expect.isTrue
                  (filter.MatchExpectation [ "Imperium"; "Rondel"; "spec"; "exp" ])
                  "path under Imperium.Rondel should match the prefix"

              Expect.isFalse
                  (filter.MatchExpectation [ "Imperium"; "Accounting"; "spec"; "exp" ])
                  "path under a different BC should not match")

          testCase "fromArgs --join-with / changes the separator used by --filter" (fun _ ->
              let filter =
                  SpecFilter.fromArgs [| "--join-with"; "/"; "--filter"; "Imperium/Rondel" |]

              Expect.isTrue
                  (filter.MatchExpectation [ "Imperium"; "Rondel"; "spec"; "exp" ])
                  "with --join-with /, slash-separated prefix should match"

              Expect.isFalse
                  (filter.MatchExpectation [ "Imperium"; "Accounting"; "spec"; "exp" ])
                  "non-matching path should still be rejected")

          testCase "fromArgs --filter-test-list matches non-leaf segments only" (fun _ ->
              let filter = SpecFilter.fromArgs [| "--filter-test-list"; "moving" |]

              Expect.isTrue
                  (filter.MatchExpectation [ "Imperium"; "Rondel"; "moving 4 spaces"; "payment is required" ])
                  "spec name (non-leaf) containing the substring should match"

              Expect.isFalse
                  (filter.MatchExpectation [ "Imperium"; "Rondel"; "stay put"; "moving leaf only" ])
                  "leaf-only match should not count for --filter-test-list")

          testCase "fromArgs --filter-test-case matches only the leaf description" (fun _ ->
              let filter = SpecFilter.fromArgs [| "--filter-test-case"; "payment" |]

              Expect.isTrue
                  (filter.MatchExpectation [ "Imperium"; "Rondel"; "moving 4 spaces"; "payment is required" ])
                  "leaf description containing the substring should match"

              Expect.isFalse
                  (filter.MatchExpectation [ "Imperium"; "Rondel"; "payment spec"; "irrelevant" ])
                  "non-leaf match should not count for --filter-test-case")

          testCase "fromArgs uses the last filter flag when multiple are present" (fun _ ->
              // --filter A then --filter-test-case B → only --filter-test-case is in effect.
              // The earlier --filter "Imperium.Rondel" is discarded.
              let filter =
                  SpecFilter.fromArgs [| "--filter"; "Imperium.Rondel"; "--filter-test-case"; "payment" |]

              Expect.isTrue
                  (filter.MatchExpectation [ "Imperium"; "Accounting"; "spec"; "payment is required" ])
                  "later --filter-test-case 'payment' wins; --filter 'Imperium.Rondel' is discarded so a path under Accounting still matches on the leaf"

              Expect.isFalse
                  (filter.MatchExpectation [ "Imperium"; "Rondel"; "spec"; "no leaf match" ])
                  "the earlier --filter prefix is discarded; only the leaf 'payment' check runs, and this leaf does not contain 'payment'")

          testCase "apply keeps expectations matching the predicate and drops the rest" (fun _ ->
              let filter: SpecFilter.T =
                  { MatchExpectation = fun path -> List.last path = "kept" }

              let specification =
                  specOn<int, NoState, unit, unit> (fun () -> 0) "mixed spec" {
                      expect "dropped" (fun _ -> ())
                      expect "kept" (fun _ -> ())
                      expect "also dropped" (fun _ -> ())
                  }

              let result = SpecFilter.apply filter [ "Imperium"; "BC" ] [ specification ]

              Expect.hasLength result 1 "spec should survive because at least one expectation matches"

              let returnedSpec = result[0]
              Expect.equal returnedSpec.Name "mixed spec" "spec name preserved"

              Expect.hasLength returnedSpec.Expectations 1 "only the matching expectation should remain"

              Expect.equal
                  returnedSpec.Expectations[0].Description
                  "kept"
                  "the kept expectation is the one matching the predicate")

          testCase "apply prunes a spec whose expectations all fail the predicate" (fun _ ->
              let filter: SpecFilter.T = { MatchExpectation = fun _ -> false }

              let specA =
                  specOn<int, NoState, unit, unit> (fun () -> 0) "all-fail spec" {
                      expect "first" (fun _ -> ())
                      expect "second" (fun _ -> ())
                  }

              let specB =
                  specOn<int, NoState, unit, unit> (fun () -> 0) "also all-fail spec" { expect "only" (fun _ -> ()) }

              let result = SpecFilter.apply filter [ "Imperium"; "BC" ] [ specA; specB ]

              Expect.isEmpty result "both specs should be pruned because no expectations matched") ]

let private specMarkdownTests =
    testList
        "SpecMarkdown"
        [ testCase "render returns None for an empty spec list" (fun _ ->
              let runner: SpecRunner<int, NoState, NoState, unit, unit> = SpecRunner.empty
              let options: SpecMarkdown.MarkdownRenderOptions = { ParentHeader = SpecMarkdown.H3 }

              let result = SpecMarkdown.render options "any-section" runner []

              Expect.isNone result "empty spec list should produce no markdown")

          testCase
              "render returns Some markdown with the section header at one level beneath ParentHeader, plus the spec body"
              (fun _ ->
                  let runner: SpecRunner<int, NoState, NoState, unit, unit> = SpecRunner.empty

                  let options: SpecMarkdown.MarkdownRenderOptions = { ParentHeader = SpecMarkdown.H3 }

                  let specs =
                      [ specOn<int, NoState, unit, unit> (fun () -> 0) "a spec" { expect "an exp" (fun _ -> ()) } ]

                  let result = SpecMarkdown.render options "Accounting" runner specs

                  Expect.isSome result "non-empty spec list should produce markdown"

                  Expect.stringContains
                      result.Value
                      "#### Accounting"
                      "section header rendered one level beneath ParentHeader (H3 → H4)"

                  Expect.stringContains result.Value "a spec" "spec body included") ]

[<Tests>]
let tests = TestList([ specTests; specFilterTests; specMarkdownTests ], Normal)
