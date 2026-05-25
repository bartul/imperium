namespace Imperium.UnitTests.Accounting

open Imperium.Accounting

type Context = { Deps: AccountingDependencies; Events: ResizeArray<AccountingEvent> }

module Context =
    let create () =
        let events = ResizeArray<AccountingEvent>()
        let publish (event: AccountingEvent) : Async<unit> = async { events.Add event }
        { Deps = { Publish = publish }; Events = events }
