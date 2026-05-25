module Imperium.UnitTests.MarkdownTests

open Expecto
open Imperium.Testing.Spec
open Imperium.Testing.Spec.Specification

[<Tests>]
let tests =
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

                  Expect.stringContains result.Value "a spec" "spec body included")

          testCase "render includes failed expectation rows and continues with later expectations" (fun _ ->
              let runner: SpecRunner<int, NoState, NoState, unit, unit> = SpecRunner.empty
              let options: SpecMarkdown.MarkdownRenderOptions = { ParentHeader = SpecMarkdown.H3 }

              let specs =
                  [ specOn<int, NoState, unit, unit> (fun () -> 0) "mixed outcomes" {
                        expect "first expectation fails" (fun _ -> failwith "broken | value")
                        expect "later expectation still renders" (fun _ -> ())
                    } ]

              let result = SpecMarkdown.render options "SpecMarkdown" runner specs

              Expect.isSome result "non-empty spec list should produce markdown"

              Expect.stringContains
                  result.Value
                  "| ❌ | first expectation fails — broken \\| value |"
                  "failure row should render and escape table pipes once"

              Expect.stringContains
                  result.Value
                  "| ✅ | later expectation still renders |"
                  "markdown rendering should continue after a failed expectation") ]
