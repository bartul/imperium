module Imperium.UnitTests.Accounting.Specs

open Expecto
open Imperium.Primitives
open Imperium.Accounting
open Imperium.Testing.Spec
open Imperium.Testing.Spec.Specification
open Imperium.UnitTests.Accounting.Assertions

// ────────────────────────────────────────────────────────────────────────────────
// Runner
// ────────────────────────────────────────────────────────────────────────────────

let private runner: SpecRunner<Context, NoState, NoState, AccountingCommand, unit> =
    { SpecRunner.empty with
        Execute = fun ctx cmd -> Accounting.execute ctx.Deps cmd |> Async.RunSynchronously
        ClearEvents = fun ctx -> ctx.Events.Clear() }

// ────────────────────────────────────────────────────────────────────────────────
// Specs
// ────────────────────────────────────────────────────────────────────────────────

let private specifications =
    let gameId = Id.newId ()
    let billingId = Id.newId ()
    let spec = specOn Context.create

    [ spec "charging a nation for paid movement confirms payment" {
          when_command (
              ChargeNationForRondelMovement
                  { GameId = gameId; Nation = "France"; Amount = Amount.unsafe 4; BillingId = billingId }
          )

          expect "payment is confirmed" assertPaymentConfirmed
          expect "payment confirmation matches requested invoice" (assertExactPaymentConfirmed gameId billingId)
          expect "payment is not marked as failed" assertNoPaymentFailed
      }

      spec "voiding a charge records no accounting outcome" {
          when_command (VoidRondelCharge { GameId = Id.newId (); BillingId = Id.newId () })

          expect "no accounting outcomes are published" (assertEventCount 0)
          expect "payment is not marked as failed" assertNoPaymentFailed
      } ]

let renderMarkdown
    (options: Markdown.RenderOptions)
    (filter: SpecFilter.Predicate)
    (rootPath: string list)
    : string option =
    specifications
    |> SpecFilter.apply filter (rootPath @ [ "Accounting" ])
    |> Markdown.render options "Accounting" runner

// ────────────────────────────────────────────────────────────────────────────────
// Test Registration
// ────────────────────────────────────────────────────────────────────────────────

[<Tests>]
let tests =
    testList "Accounting" (specifications |> List.map (SpecRunner.toExpectoTestList runner))
