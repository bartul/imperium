module Imperium.UnitTests.Accounting.Specs

open Expecto
open Imperium.Primitives
open Imperium.Accounting
open Imperium.Testing.Spec
open Imperium.Testing.Spec.Specification
open Imperium.UnitTests.Accounting.Context
open Imperium.UnitTests.Accounting.Assertions

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

          expect "payment is confirmed" assertPaymentConfirmed
          expect "payment confirmation matches requested invoice" (assertExactPaymentConfirmed gameId billingId)
          expect "payment is not marked as failed" assertNoPaymentFailed
      }

      spec "voiding a charge records no accounting outcome" {
          when_command (VoidRondelCharge { GameId = Id.newId (); BillingId = Id.newId () })

          expect "no accounting outcomes are published" (assertEventCount 0)
          expect "payment is not marked as failed" assertNoPaymentFailed
      } ]

let renderSpecMarkdown
    (options: SpecMarkdown.MarkdownRenderOptions)
    (filter: SpecFilter.Predicate)
    (rootPath: string list)
    : string option =
    accountingSpecs
    |> SpecFilter.apply filter (rootPath @ [ "Accounting" ])
    |> SpecMarkdown.render options "Accounting" runner

// ────────────────────────────────────────────────────────────────────────────────
// Test Registration
// ────────────────────────────────────────────────────────────────────────────────

[<Tests>]
let tests =
    testList "Accounting" (accountingSpecs |> List.map (SpecRunner.toExpectoTestList runner))
