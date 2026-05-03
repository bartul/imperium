module Imperium.UnitTests.Main

open System
open Expecto
open Spec

let private renderSpecMarkdown (args: string array) =
    let filter = SpecFilter.fromArgs args

    let opts: SpecMarkdown.MarkdownRenderOptions = { ParentHeader = SpecMarkdown.H2 }

    let rootPath = [ "Imperium" ]

    let sections =
        [ Accounting.renderSpecMarkdown opts filter rootPath
          Rondel.renderSpecMarkdown opts filter rootPath ]
        |> List.choose id

    let title = "## 📘Imperium Specification Based Tests"

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
                [ SpecTests.tests
                  Gameplay.tests
                  Rondel.tests
                  RondelHostTests.tests
                  TerminalBusTests.tests
                  TerminalRondelStoreTests.tests
                  AccountingHostTests.tests
                  Accounting.tests ]

        runTestsWithCLIArgs [] args allTests
