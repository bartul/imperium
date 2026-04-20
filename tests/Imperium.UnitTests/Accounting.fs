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
    CollectionAssert.forAccessor (fun (ctx: AccountingContext) -> ctx.Events :> seq<_>)

let private hasEventCount expected =
    events.HasSize expected "event count should match"

let private hasPaymentConfirmed =
    events.HasAny
        (function
        | RondelInvoicePaid _ -> true
        | _ -> false)
        "payment confirmation should be published"

let private hasNoPaymentFailed =
    events.HasNone
        (function
        | RondelInvoicePaymentFailed _ -> true
        | _ -> false)
        "no payment failure should be published"

let private hasExactPaymentConfirmed gameId billingId =
    events.Has (RondelInvoicePaid { GameId = gameId; BillingId = billingId }) "exact payment confirmation should match"

// ────────────────────────────────────────────────────────────────────────────────
// Specs
// ────────────────────────────────────────────────────────────────────────────────

let private accountingSpecs =
    let gameId = Id.newId ()
    let billingId = Id.newId ()
    let spec = specOn createContext

    [ spec "charging a nation for paid movement confirms payment" {
          when_command (
              ChargeNationForRondelMovement
                  { GameId = gameId; Nation = "France"; Amount = Amount.unsafe 4; BillingId = billingId }
          )

          expect "payment is confirmed" hasPaymentConfirmed
          expect "payment confirmation matches requested invoice" (hasExactPaymentConfirmed gameId billingId)
          expect "payment is not marked as failed" hasNoPaymentFailed
      }

      spec "voiding a charge records no accounting outcome" {
          when_command (VoidRondelCharge { GameId = Id.newId (); BillingId = Id.newId () })

          expect "no accounting outcomes are published" (hasEventCount 0)
          expect "payment is not marked as failed" hasNoPaymentFailed
      } ]

let renderSpecMarkdown options =
    SpecMarkdown.toMarkdownDocument options runner accountingSpecs

// ────────────────────────────────────────────────────────────────────────────────
// Test Registration
// ────────────────────────────────────────────────────────────────────────────────

[<Tests>]
let tests = testList "Accounting" (accountingSpecs |> List.map (toExpecto runner))
