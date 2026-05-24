module Imperium.UnitTests.FilterTests

open Expecto
open Imperium.Testing.Spec
open Imperium.Testing.Spec.Specification

[<Tests>]
let tests =
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

              Expect.isEmpty result "both specs should be pruned because no expectations matched")

          testCase "fromArgs --join-with last-wins when multiple --join-with flags are present" (fun _ ->
              let filter =
                  SpecFilter.fromArgs [| "--join-with"; "."; "--join-with"; "/"; "--filter"; "Imperium/Rondel" |]

              Expect.isTrue
                  (filter.MatchExpectation [ "Imperium"; "Rondel"; "spec"; "exp" ])
                  "later --join-with / wins; --filter sees slash-joined path"

              Expect.isFalse
                  (filter.MatchExpectation [ "Imperium"; "Accounting"; "spec"; "exp" ])
                  "non-Rondel path still rejected")

          testCase "fromArgs --run with an exact expectation path matches only that expectation" (fun _ ->
              let filter = SpecFilter.fromArgs [| "--run"; "Imperium.Rondel.spec.exp" |]

              Expect.isTrue
                  (filter.MatchExpectation [ "Imperium"; "Rondel"; "spec"; "exp" ])
                  "exact expectation path should match"

              Expect.isFalse
                  (filter.MatchExpectation [ "Imperium"; "Rondel"; "spec"; "other-exp" ])
                  "sibling expectation should not match"

              Expect.isFalse
                  (filter.MatchExpectation [ "Imperium"; "Accounting"; "spec"; "exp" ])
                  "different BC should not match")

          testCase "fromArgs --run with a spec-level path matches all expectations under that spec" (fun _ ->
              let filter = SpecFilter.fromArgs [| "--run"; "Imperium.Rondel.spec" |]

              Expect.isTrue
                  (filter.MatchExpectation [ "Imperium"; "Rondel"; "spec"; "exp1" ])
                  "expectation under named spec should match"

              Expect.isTrue
                  (filter.MatchExpectation [ "Imperium"; "Rondel"; "spec"; "exp2" ])
                  "another expectation under named spec should match"

              Expect.isFalse
                  (filter.MatchExpectation [ "Imperium"; "Rondel"; "other-spec"; "exp" ])
                  "expectation under different spec should not match"

              Expect.isFalse
                  (filter.MatchExpectation [ "Imperium"; "Rondel"; "spec-but-different"; "exp" ])
                  "spec name with shared prefix but different segment should not match"

              Expect.isFalse
                  (filter.MatchExpectation [ "Imperium"; "Accounting"; "spec"; "exp" ])
                  "spec named 'spec' under a different BC should not match")

          testCase "fromArgs --run accepts multiple values; expectation matching any value passes" (fun _ ->
              let filter =
                  SpecFilter.fromArgs [| "--run"; "Imperium.Rondel.specA"; "Imperium.Accounting.specB" |]

              Expect.isTrue
                  (filter.MatchExpectation [ "Imperium"; "Rondel"; "specA"; "exp" ])
                  "first --run value should match"

              Expect.isTrue
                  (filter.MatchExpectation [ "Imperium"; "Accounting"; "specB"; "exp" ])
                  "second --run value should match"

              Expect.isFalse
                  (filter.MatchExpectation [ "Imperium"; "Rondel"; "specC"; "exp" ])
                  "unrelated path should not match")

          testCase "fromArgs --run stops consuming values at the next --flag boundary" (fun _ ->
              let filter =
                  SpecFilter.fromArgs [| "--run"; "Imperium.Rondel.specA"; "--join-with"; "." |]

              Expect.isTrue
                  (filter.MatchExpectation [ "Imperium"; "Rondel"; "specA"; "exp" ])
                  "value before the boundary should still match"

              Expect.isFalse
                  (filter.MatchExpectation [ "--join-with"; "exp" ])
                  "the boundary --flag token should not be consumed as a --run value"

              Expect.isFalse
                  (filter.MatchExpectation [ "."; "exp" ])
                  "the value after --join-with should not be consumed as a --run value either")

          testCase "fromArgs uses last --run when multiple --run flags are present" (fun _ ->
              let filter =
                  SpecFilter.fromArgs [| "--run"; "Imperium.Rondel.specA"; "--run"; "Imperium.Accounting.specB" |]

              Expect.isFalse
                  (filter.MatchExpectation [ "Imperium"; "Rondel"; "specA"; "exp" ])
                  "the first --run is overwritten and its value should not match"

              Expect.isTrue
                  (filter.MatchExpectation [ "Imperium"; "Accounting"; "specB"; "exp" ])
                  "the last --run wins; its value should match")

          testCase "fromArgs lets a later filter flag override an earlier --run" (fun _ ->
              let filter =
                  SpecFilter.fromArgs [| "--run"; "Imperium.Rondel.specA"; "--filter"; "Imperium.Accounting" |]

              Expect.isFalse
                  (filter.MatchExpectation [ "Imperium"; "Rondel"; "specA"; "exp" ])
                  "the earlier --run is overwritten and its value should not match"

              Expect.isTrue
                  (filter.MatchExpectation [ "Imperium"; "Accounting"; "specB"; "exp" ])
                  "the later --filter wins; a path under its prefix should match"

              Expect.isFalse
                  (filter.MatchExpectation [ "Imperium"; "Rondel"; "specB"; "exp" ])
                  "a path outside the later --filter prefix should not match")

          testCase "fromArgs --run alone with no values matches nothing" (fun _ ->
              let filter = SpecFilter.fromArgs [| "--run" |]

              Expect.isFalse
                  (filter.MatchExpectation [ "Imperium"; "Rondel"; "spec"; "exp" ])
                  "any expectation under an empty --run should be rejected"

              Expect.isFalse
                  (filter.MatchExpectation [ "Imperium"; "Accounting"; "spec"; "exp" ])
                  "another path should also be rejected")

          testCase "fromArgs --run honors --join-with /" (fun _ ->
              let filter =
                  SpecFilter.fromArgs [| "--join-with"; "/"; "--run"; "Imperium/Rondel/spec" |]

              Expect.isTrue
                  (filter.MatchExpectation [ "Imperium"; "Rondel"; "spec"; "exp" ])
                  "expectation under spec-level path with slash separator should match (hierarchical)"

              Expect.isFalse
                  (filter.MatchExpectation [ "Imperium"; "Accounting"; "spec"; "exp" ])
                  "different BC should still be rejected with slash separator"

              Expect.isFalse
                  (filter.MatchExpectation [ "Imperium"; "Rondel"; "spec-but-different"; "exp" ])
                  "segment-boundary safety still holds: 'spec' must not match 'spec-but-different'") ]
