module Imperium.UnitTests.Main

open Expecto

[<EntryPoint>]
let main args =
    runTestsWithCLIArgs [] args Rondel.tests
