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

let private runner: ISpecRunner<AccountingContext, NoState, AccountingCommand, unit> =
    { new ISpecRunner<AccountingContext, NoState, AccountingCommand, unit> with
        member _.Execute ctx cmd =
            execute ctx.Deps cmd |> Async.RunSynchronously

        member _.Handle _ _ = ()
        member _.ClearEvents ctx = ctx.Events.Clear()
        member _.ClearCommands _ = ()
        member _.CaptureState _ = NoState }

// ────────────────────────────────────────────────────────────────────────────────
// Specs
// ────────────────────────────────────────────────────────────────────────────────

let private specs =
    [ spec "chargeNationForRondelMovement auto-approves and publishes RondelInvoicePaid" {
          on createContext

          when_
              [ Execute(
                    ChargeNationForRondelMovement
                        { GameId = Guid.NewGuid() |> Id
                          Nation = "France"
                          Amount = Amount.unsafe 4
                          BillingId = Guid.NewGuid() |> Id }
                ) ]

          expect "publishes exactly one event" (fun ctx -> ctx.Events.Count = 1)

          expect "event is RondelInvoicePaid" (fun ctx ->
              match ctx.Events.[0] with
              | RondelInvoicePaid _ -> true
              | _ -> false)
      }

      spec "voidRondelCharge does nothing" {
          on createContext

          when_
              [ VoidRondelCharge { GameId = Guid.NewGuid() |> Id; BillingId = Guid.NewGuid() |> Id }
                |> Execute ]

          expect "no events published" (fun ctx -> ctx.Events.Count = 0)
      } ]

let renderSpecMarkdown () =
    SpecMarkdown.toMarkdownDocument runner specs

// ────────────────────────────────────────────────────────────────────────────────
// Test Registration
// ────────────────────────────────────────────────────────────────────────────────

[<Tests>]
let tests = testList "Accounting" (specs |> List.map (toExpecto runner))
