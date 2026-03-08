namespace Imperium.Terminal.Accounting

open Imperium.Accounting
open Imperium.Terminal

type AccountingHost = { Execute: AccountingCommand -> Async<unit> }

module AccountingHost =
    let create (bus: IBus) : AccountingHost =
        let ignoreMailboxError (_: AccountingCommand) (_: exn) = ()
        let publish (evt: AccountingEvent) = bus.Publish evt

        let deps: AccountingDependencies = { Publish = publish }

        let mailbox = SupervisedMailbox.start (execute deps) ignoreMailboxError

        { Execute = fun cmd -> async { mailbox.Post cmd } }
