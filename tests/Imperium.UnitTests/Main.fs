module Imperium.UnitTests.Main

open System
open Expecto

let private renderSpecMarkdown () =
    String.concat
        Environment.NewLine
        [ "# Imperium Spec-Based Tests"
          ""
          "## Accounting"
          ""
          Accounting.renderSpecMarkdown ()
          ""
          "## Rondel"
          ""
          Rondel.renderSpecMarkdown () ]

[<EntryPoint>]
let main args =
    if args |> Array.contains "--render-spec-markdown" then
        renderSpecMarkdown () |> printfn "%s"
        0
    else
        let allTests =
            testList
                "Imperium"
                [ Gameplay.tests
                  RondelTests.tests
                  Rondel.tests
                  RondelHostTests.tests
                  TerminalBusTests.tests
                  TerminalRondelStoreTests.tests
                  AccountingHostTests.tests
                  Accounting.tests ]

        runTestsWithCLIArgs [] args allTests
