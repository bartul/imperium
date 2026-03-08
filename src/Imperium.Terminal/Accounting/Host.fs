namespace Imperium.Terminal.Accounting

open Imperium.Accounting
open Imperium.Terminal
open Imperium.Terminal.Shell

type AccountingHost = { Execute: AccountingCommand -> Async<unit> }

module AccountingHost =
    let private describeCommand =
        function
        | ChargeNationForRondelMovement _ -> "ChargeNationForRondelMovement"
        | VoidRondelCharge _ -> "VoidRondelCharge"

    let create (bus: IBus) : AccountingHost =
        let onMailboxError (cmd: AccountingCommand) (ex: exn) : Async<unit> =
            let notification: SystemNotification =
                { Severity = NotificationSeverity.Error
                  Source = NotificationSource.AccountingHost
                  Message = $"Failed processing {describeCommand cmd}: {ex.Message}" }

            bus.Publish notification

        let publish (evt: AccountingEvent) = bus.Publish evt

        let deps: AccountingDependencies = { Publish = publish }

        let mailbox = SupervisedMailbox.start (execute deps) onMailboxError

        { Execute = fun cmd -> async { mailbox.Post cmd } }
