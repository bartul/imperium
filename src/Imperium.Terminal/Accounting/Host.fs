namespace Imperium.Terminal.Accounting

open Imperium.Accounting
open Imperium.Terminal

type AccountingHost =
    { Execute: AccountingCommand -> Async<unit> }

module AccountingHost =
    let create (bus: IBus) : AccountingHost = failwith "Not implemented"
