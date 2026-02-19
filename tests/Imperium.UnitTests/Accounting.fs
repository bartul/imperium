module Imperium.UnitTests.Accounting

open System
open Expecto
open Spec
open Imperium.Accounting
open Imperium.Primitives

// ────────────────────────────────────────────────────────────────────────────────
// Context
// ────────────────────────────────────────────────────────────────────────────────

type AccountingContext = { Deps: AccountingDependencies; Events: ResizeArray<AccountingEvent> }

let private createContext () =
    let events = ResizeArray<AccountingEvent>()
    let publish (event: AccountingEvent) : Async<unit> = async { events.Add event }
    { Deps = { Publish = publish }; Events = events }

// ────────────────────────────────────────────────────────────────────────────────
// Runner
// ────────────────────────────────────────────────────────────────────────────────

let private runner: ISpecRunner<AccountingContext, NoState, NoState, AccountingCommand, unit> =
    { new ISpecRunner<AccountingContext, NoState, NoState, AccountingCommand, unit> with
        member _.Execute ctx cmd =
            execute ctx.Deps cmd |> Async.RunSynchronously

        member _.Handle _ _ = ()
        member _.ClearEvents ctx = ctx.Events.Clear()
        member _.ClearCommands _ = ()
        member _.SeedState _ _ = ()
        member _.SeedFor _ = None
        member _.CaptureState _ = NoState }

// ────────────────────────────────────────────────────────────────────────────────
// Helpers
// ────────────────────────────────────────────────────────────────────────────────

let private hasEventCount expected ctx =
    ctx.Events.Count = expected

let private hasPaymentConfirmed ctx =
    ctx.Events
    |> Seq.exists (function
        | RondelInvoicePaid _ -> true
        | _ -> false)

let private hasPaymentFailed ctx =
    ctx.Events
    |> Seq.exists (function
        | RondelInvoicePaymentFailed _ -> true
        | _ -> false)

// ────────────────────────────────────────────────────────────────────────────────
// Specs
// ────────────────────────────────────────────────────────────────────────────────

let private accountingSpecs =
    [ spec "charging a nation for paid movement confirms payment" {
          on createContext

          when_
              [ ChargeNationForRondelMovement
                    { GameId = Guid.NewGuid() |> Id
                      Nation = "France"
                      Amount = Amount.unsafe 4
                      BillingId = Guid.NewGuid() |> Id }
                |> Execute ]

          expect "payment is confirmed" hasPaymentConfirmed
          expect "payment is not marked as failed" (hasPaymentFailed >> not)
      }

      spec "voiding a charge records no accounting outcome" {
          on createContext

          when_
              [ VoidRondelCharge { GameId = Guid.NewGuid() |> Id; BillingId = Guid.NewGuid() |> Id }
                |> Execute ]

          expect "payment is not marked as failed" (hasPaymentFailed >> not)
      } ]

let renderSpecMarkdown options =
    SpecMarkdown.toMarkdownDocument options runner accountingSpecs

// ────────────────────────────────────────────────────────────────────────────────
// Test Registration
// ────────────────────────────────────────────────────────────────────────────────

[<Tests>]
let tests = testList "Accounting" (accountingSpecs |> List.map (toExpecto runner))
