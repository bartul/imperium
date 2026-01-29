module Imperium.UnitTests.AccountingContractTests

open System
open Expecto
open Imperium.Accounting
open Imperium.Primitives

module ContractAccounting = Imperium.Contract.Accounting

[<Tests>]
let tests =
    testList
        "Accounting.Contract"
        [ testList
              "ChargeNationForRondelMovementCommand.fromContract"
              [ testCase "requires a valid game id"
                <| fun _ ->
                    let contractCommand: ContractAccounting.ChargeNationForRondelMovementCommand =
                        { GameId = Guid.Empty; Nation = "France"; Amount = Amount.unsafe 2; BillingId = Guid.NewGuid() }

                    let result = ChargeNationForRondelMovementCommand.fromContract contractCommand
                    Expect.isError result "charge command cannot have empty GameId"

                testCase "requires a valid billing id"
                <| fun _ ->
                    let contractCommand: ContractAccounting.ChargeNationForRondelMovementCommand =
                        { GameId = Guid.NewGuid(); Nation = "France"; Amount = Amount.unsafe 2; BillingId = Guid.Empty }

                    let result = ChargeNationForRondelMovementCommand.fromContract contractCommand
                    Expect.isError result "charge command cannot have empty BillingId"

                testCase "accepts valid command"
                <| fun _ ->
                    let contractCommand: ContractAccounting.ChargeNationForRondelMovementCommand =
                        { GameId = Guid.NewGuid()
                          Nation = "France"
                          Amount = Amount.unsafe 4
                          BillingId = Guid.NewGuid() }

                    let result = ChargeNationForRondelMovementCommand.fromContract contractCommand
                    Expect.isOk result "valid command should transform successfully" ]

          testList
              "VoidRondelChargeCommand.fromContract"
              [ testCase "requires a valid game id"
                <| fun _ ->
                    let contractCommand: ContractAccounting.VoidRondelChargeCommand =
                        { GameId = Guid.Empty; BillingId = Guid.NewGuid() }

                    let result = VoidRondelChargeCommand.fromContract contractCommand
                    Expect.isError result "void command cannot have empty GameId"

                testCase "requires a valid billing id"
                <| fun _ ->
                    let contractCommand: ContractAccounting.VoidRondelChargeCommand =
                        { GameId = Guid.NewGuid(); BillingId = Guid.Empty }

                    let result = VoidRondelChargeCommand.fromContract contractCommand
                    Expect.isError result "void command cannot have empty BillingId"

                testCase "accepts valid command"
                <| fun _ ->
                    let contractCommand: ContractAccounting.VoidRondelChargeCommand =
                        { GameId = Guid.NewGuid(); BillingId = Guid.NewGuid() }

                    let result = VoidRondelChargeCommand.fromContract contractCommand
                    Expect.isOk result "valid command should transform successfully" ] ]
