namespace Imperium.Terminal.Accounting

open Imperium.Accounting
open Imperium.Terminal

type AccountingHost = { Execute: AccountingCommand -> Async<unit> }

module AccountingHost =
    let create (bus: IBus) : AccountingHost =
        let publish (evt: AccountingEvent) =
            match evt with
            | RondelInvoicePaid inner -> bus.Publish inner
            | RondelInvoicePaymentFailed inner -> bus.Publish inner

        let deps: AccountingDependencies = { Publish = publish }

        let mailbox =
            MailboxProcessor.Start(fun inbox ->
                let rec loop () =
                    async {
                        let! cmd = inbox.Receive()
                        do! execute deps cmd
                        return! loop ()
                    }

                loop ())

        { Execute = fun cmd -> async { mailbox.Post cmd } }
