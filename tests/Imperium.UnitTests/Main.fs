module Imperium.UnitTests.Main

open Expecto

[<EntryPoint>]
let main args =
    let allTests =
        testList
            "AllTests"
            [ Gameplay.tests
              Rondel.tests
              RondelHostTests.tests
              TerminalBusTests.tests
              TerminalRondelStoreTests.tests
              AccountingHostTests.tests ]

    runTestsWithCLIArgs [] args allTests
