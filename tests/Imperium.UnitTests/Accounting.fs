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

let private runner: SpecRunner<AccountingContext, NoState, NoState, AccountingCommand, unit> =
    { SpecRunner.empty with
        Execute = fun ctx cmd -> execute ctx.Deps cmd |> Async.RunSynchronously
        ClearEvents = fun ctx -> ctx.Events.Clear() }

// ────────────────────────────────────────────────────────────────────────────────
// Helpers
// ────────────────────────────────────────────────────────────────────────────────

let private events =
    CollectionExpect.forAccessor (fun (ctx: AccountingContext) -> ctx.Events :> seq<_>)

let private hasEventCount expected = events.HasSize expected

let private hasPaymentConfirmed =
    events.HasAny (function
        | RondelInvoicePaid _ -> true
        | _ -> false)

let private hasPaymentFailed =
    events.HasAny (function
        | RondelInvoicePaymentFailed _ -> true
        | _ -> false)

let private hasExactPaymentConfirmed gameId billingId =
    events.Has(RondelInvoicePaid { GameId = gameId; BillingId = billingId })

// ────────────────────────────────────────────────────────────────────────────────
// Specs
// ────────────────────────────────────────────────────────────────────────────────

let private accountingSpecs =
    let gameId = Guid.NewGuid() |> Id
    let billingId = Guid.NewGuid() |> Id

    [ spec "charging a nation for paid movement confirms payment" {
          on createContext

          when_
              [ ChargeNationForRondelMovement
                    { GameId = gameId; Nation = "France"; Amount = Amount.unsafe 4; BillingId = billingId }
                |> Execute ]

          expect "payment is confirmed" hasPaymentConfirmed
          expect "payment confirmation matches requested invoice" (hasExactPaymentConfirmed gameId billingId)
          expect "payment is not marked as failed" (hasPaymentFailed >> not)
      }

      spec "voiding a charge records no accounting outcome" {
          on createContext

          when_
              [ VoidRondelCharge { GameId = Guid.NewGuid() |> Id; BillingId = Guid.NewGuid() |> Id }
                |> Execute ]

          expect "no accounting outcomes are published" (hasEventCount 0)
          expect "payment is not marked as failed" (hasPaymentFailed >> not)
      } ]

let renderSpecMarkdown options =
    SpecMarkdown.toMarkdownDocument options runner accountingSpecs

// ────────────────────────────────────────────────────────────────────────────────
// Test Registration
// ────────────────────────────────────────────────────────────────────────────────

[<Tests>]
let tests = testList "Accounting" (accountingSpecs |> List.map (toExpecto runner))
