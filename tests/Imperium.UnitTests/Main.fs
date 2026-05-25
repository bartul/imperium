module Imperium.UnitTests.Main

open System
open Expecto
open Imperium.Testing.Spec
open Imperium.Testing.Spec.Specification

module Accounting = Imperium.UnitTests.Accounting.Specs
module Rondel = Imperium.UnitTests.Rondel.Specs

let private renderSpecMarkdown (args: string array) =
    let filter = SpecFilter.fromArgs args

    let opts: SpecMarkdown.MarkdownRenderOptions = { ParentHeader = SpecMarkdown.H2 }

    let rootPath = [ "Imperium" ]

    let sections =
        [ Accounting.renderSpecMarkdown opts filter rootPath
          Rondel.renderSpecMarkdown opts filter rootPath ]
        |> List.choose id

    let title = $"## 📘{rootPath.[0]} Specification Based Tests"

    if List.isEmpty sections then
        $"{title}{Environment.NewLine}{Environment.NewLine}_no specs match the filter_"
    else
        title :: "" :: sections |> String.concat Environment.NewLine

[<EntryPoint>]
let main args =
    if args |> Array.contains "--render-spec-markdown" then
        renderSpecMarkdown args |> printfn "%s"
        0
    else
        let allTests =
            testList
                "Imperium"
                [ SpecificationTests.tests
                  FilterTests.tests
                  MarkdownTests.tests
                  AccountingContractTests.tests
                  RondelContractTests.tests
                  Accounting.tests
                  Gameplay.tests
                  Rondel.tests
                  TerminalBusTests.tests
                  TerminalRondelStoreTests.tests
                  RondelDirectCommitTests.tests
                  RondelHostTests.tests
                  AccountingHostTests.tests ]

        runTestsWithCLIArgs [] args allTests
