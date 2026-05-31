module Imperium.UnitTests.Main

open System
open Expecto
open Imperium.Testing.Spec

module Accounting = Imperium.UnitTests.Accounting.Specs
module Rondel = Imperium.UnitTests.Rondel.Specs

let private renderMarkdown (args: string array) =
    let filter = SpecFilter.fromArgs args

    let opts: Markdown.RenderOptions = { ParentHeader = Markdown.H2 }

    let rootPath = [ "Imperium" ]

    let sections =
        [ Accounting.renderMarkdown opts filter rootPath
          Rondel.renderMarkdown opts filter rootPath ]
        |> List.choose id

    let title = $"## 📘{rootPath.[0]} Specification Based Tests"

    if List.isEmpty sections then
        $"{title}{Environment.NewLine}{Environment.NewLine}_no specs match the filter_"
    else
        title :: "" :: sections |> String.concat Environment.NewLine

[<EntryPoint>]
let main args =
    if args |> Array.contains "--render-spec-markdown" then
        renderMarkdown args |> printfn "%s"
        0
    else
        let allTests =
            testList
                "Imperium"
                [ SpecificationTests.tests
                  SpecRunnerTests.tests
                  CollectionAssertTests.tests
                  FilterTests.tests
                  MarkdownTests.tests
                  AccountingContractTests.tests
                  GameplayContractTests.tests
                  RondelContractTests.tests
                  Accounting.tests
                  Gameplay.GameId.tests
                  Gameplay.NationId.tests
                  Gameplay.PlayerId.tests
                  Gameplay.PlayerRoster.tests
                  Rondel.tests
                  TerminalBusTests.tests
                  TerminalRondelStoreTests.tests
                  RondelDirectCommitTests.tests
                  RondelHostTests.tests
                  AccountingHostTests.tests ]

        runTestsWithCLIArgs [] args allTests
