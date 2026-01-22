module Imperium.UnitTests.AccountingTests

open System
open Expecto
open Imperium.Accounting
open Imperium.Primitives

type private Accounting = { Execute: AccountingCommand -> unit }

let private createAccounting () =
    let publishedEvents = ResizeArray<AccountingEvent>()

    let publish (event: AccountingEvent) : Async<unit> = async { publishedEvents.Add event }

    let deps: AccountingDependencies = { Publish = publish }

    { Execute = fun cmd -> execute deps cmd |> Async.RunSynchronously }, publishedEvents

[<Tests>]
let tests =
    testList
        "Accounting"
        [ testList
              "chargeNationForRondelMovement"
              [ testCase "auto-approves and publishes RondelInvoicePaid"
                <| fun _ ->
                    let accounting, publishedEvents = createAccounting ()
                    let gameId = Guid.NewGuid() |> Id
                    let billingId = Guid.NewGuid() |> Id

                    let command: ChargeNationForRondelMovementCommand =
                        { GameId = gameId
                          Nation = "France"
                          Amount = Amount.unsafe 4
                          BillingId = billingId }

                    accounting.Execute(ChargeNationForRondelMovement command)

                    Expect.hasLength publishedEvents 1 "should publish exactly one event"

                    match publishedEvents.[0] with
                    | RondelInvoicePaid evt ->
                        Expect.equal evt.GameId gameId "event should have correct GameId"
                        Expect.equal evt.BillingId billingId "event should have correct BillingId"
                    | _ -> failtest "expected RondelInvoicePaid event" ]

          testList
              "voidRondelCharge"
              [ testCase "does nothing (no event published)"
                <| fun _ ->
                    let accounting, publishedEvents = createAccounting ()
                    let gameId = Guid.NewGuid() |> Id
                    let billingId = Guid.NewGuid() |> Id

                    let command: VoidRondelChargeCommand = { GameId = gameId; BillingId = billingId }

                    accounting.Execute(VoidRondelCharge command)

                    Expect.isEmpty publishedEvents "void command should not publish any events" ] ]
