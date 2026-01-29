module Imperium.UnitTests.AccountingHostTests

open Expecto
open Imperium.Accounting
open Imperium.Primitives
open Imperium.Terminal
open Imperium.Terminal.Accounting

// ──────────────────────────────────────────────────────────────────────────
// Test Helpers
// ──────────────────────────────────────────────────────────────────────────

let private waitFor (check: unit -> bool) =
    let rec loop delay attempts =
        if check () then
            ()
        elif attempts <= 0 then
            failwith "waitFor timed out"
        else
            System.Threading.Thread.Sleep(delay: int)
            loop (delay * 2) (attempts - 1)

    loop 5 12

let private createAccountingHost () =
    let publishedEvents = ResizeArray<obj>()
    let innerBus = Bus.create ()

    let bus =
        { new IBus with
            member _.Publish<'T>(event: 'T) =
                publishedEvents.Add(box event)
                innerBus.Publish event

            member _.Subscribe<'T>(handler: 'T -> Async<unit>) = innerBus.Subscribe<'T> handler }

    let host = AccountingHost.create bus

    {| Execute = fun cmd -> host.Execute cmd |> Async.RunSynchronously |}, publishedEvents, bus

// ──────────────────────────────────────────────────────────────────────────
// Tests - Plumbing verification only
// ──────────────────────────────────────────────────────────────────────────

[<Tests>]
let tests =
    testList
        "Terminal.AccountingHost"
        [ testCase "wires command execution to domain"
          <| fun _ ->
              let host, _, _ = createAccountingHost ()
              let gameId = Id.newId ()
              let billingId = Id.newId ()

              ChargeNationForRondelMovement
                  { GameId = gameId; Nation = "Austria"; Amount = Amount.unsafe 2; BillingId = billingId }
              |> host.Execute

              // Command should execute without throwing
              ()

          testCase "publishes events to bus"
          <| fun _ ->
              let host, publishedEvents, _ = createAccountingHost ()
              let gameId = Id.newId ()
              let billingId = Id.newId ()

              ChargeNationForRondelMovement
                  { GameId = gameId; Nation = "Austria"; Amount = Amount.unsafe 2; BillingId = billingId }
              |> host.Execute

              waitFor (fun () -> publishedEvents.Count > 0)

              let hasPaidEvent =
                  publishedEvents
                  |> Seq.exists (fun e ->
                      match e with
                      | :? RondelInvoicePaidEvent -> true
                      | _ -> false)

              Expect.isTrue hasPaidEvent "should publish RondelInvoicePaidEvent" ]
