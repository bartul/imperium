module Imperium.UnitTests.Accounting.Context

open Imperium.Accounting
open Imperium.Testing.Spec.Specification
open Imperium.Testing.Spec.Runner

// ────────────────────────────────────────────────────────────────────────────────
// Context
// ────────────────────────────────────────────────────────────────────────────────

type AccountingContext = { Deps: AccountingDependencies; Events: ResizeArray<AccountingEvent> }

let createContext () =
    let events = ResizeArray<AccountingEvent>()
    let publish (event: AccountingEvent) : Async<unit> = async { events.Add event }
    { Deps = { Publish = publish }; Events = events }

// ────────────────────────────────────────────────────────────────────────────────
// Runner
// ────────────────────────────────────────────────────────────────────────────────

let runner: SpecRunner<AccountingContext, NoState, NoState, AccountingCommand, unit> =
    { SpecRunner.empty with
        Execute = fun ctx cmd -> Accounting.execute ctx.Deps cmd |> Async.RunSynchronously
        ClearEvents = fun ctx -> ctx.Events.Clear() }
