module Imperium.UnitTests.AccountingHostTests

open Expecto
open Imperium.Accounting
open Imperium.Primitives
open Imperium.Terminal
open Imperium.Terminal.Accounting
open Imperium.Terminal.Shell

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

let private createAccountingHost (publishAccountingEvent: AccountingEvent -> Async<unit>) =
    let publishedEvents = ResizeArray<obj>()
    let innerBus = Bus.create ()

    let bus =
        { new IBus with
            member _.Publish<'T>(event: 'T) =
                async {
                    if typeof<'T> = typeof<AccountingEvent> then
                        let accountingEvent = box event :?> AccountingEvent
                        do! publishAccountingEvent accountingEvent
                        publishedEvents.Add(box accountingEvent)
                        do! innerBus.Publish accountingEvent
                    else
                        publishedEvents.Add(box event)
                        do! innerBus.Publish event
                }

            member _.Subscribe<'T>(handler: 'T -> Async<unit>) = innerBus.Subscribe<'T> handler }

    let host = AccountingHost.create bus

    {| Execute = fun cmd -> host.Execute cmd |> Async.RunSynchronously |}, publishedEvents, bus

let private createAccountingHostWithDefaults () =
    let publishAccountingEvent (_: AccountingEvent) = async { return () }

    createAccountingHost publishAccountingEvent

// ──────────────────────────────────────────────────────────────────────────
// Tests - Plumbing verification only
// ──────────────────────────────────────────────────────────────────────────

[<Tests>]
let tests =
    testList
        "Terminal.AccountingHost"
        [ testCase "wires command execution to domain"
          <| fun _ ->
              let host, _, _ = createAccountingHostWithDefaults ()
              let gameId = Id.newId ()
              let billingId = Id.newId ()

              ChargeNationForRondelMovement
                  { GameId = gameId; Nation = "Austria"; Amount = Amount.unsafe 2; BillingId = billingId }
              |> host.Execute

              // Command should execute without throwing
              ()

          testCase "publishes events to bus"
          <| fun _ ->
              let host, publishedEvents, _ = createAccountingHostWithDefaults ()
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
                      | :? AccountingEvent as evt ->
                          match evt with
                          | RondelInvoicePaid _ -> true
                          | _ -> false
                      | _ -> false)

              Expect.isTrue hasPaidEvent "should publish RondelInvoicePaid AccountingEvent"

          testCase "keeps processing commands after a handler failure"
          <| fun _ ->
              let mutable shouldFail = true

              let publishAccountingEvent (event: AccountingEvent) =
                  async {
                      if shouldFail then
                          shouldFail <- false
                          failwith "publish failed"

                      return ()
                  }

              let host, publishedEvents, _ = createAccountingHost publishAccountingEvent

              let gameId = Id.newId ()

              ChargeNationForRondelMovement
                  { GameId = gameId; Nation = "Austria"; Amount = Amount.unsafe 2; BillingId = Id.newId () }
              |> host.Execute

              ChargeNationForRondelMovement
                  { GameId = gameId; Nation = "Austria"; Amount = Amount.unsafe 2; BillingId = Id.newId () }
              |> host.Execute

              let hasMailboxErrorNotification () =
                  publishedEvents
                  |> Seq.exists (function
                      | :? SystemNotification as notification ->
                          notification.Severity = NotificationSeverity.Error
                          && notification.Source = NotificationSource.AccountingHost
                          && notification.Message.Contains("ChargeNationForRondelMovement")
                      | _ -> false)

              let hasPublishedAccountingEvent () =
                  publishedEvents
                  |> Seq.exists (function
                      | :? AccountingEvent -> true
                      | _ -> false)

              waitFor (fun () -> hasMailboxErrorNotification () && hasPublishedAccountingEvent ())
              Expect.isTrue (hasMailboxErrorNotification ()) "mailbox failure should publish a system notification"
              Expect.isTrue (hasPublishedAccountingEvent ()) "mailbox should continue processing later commands" ]
