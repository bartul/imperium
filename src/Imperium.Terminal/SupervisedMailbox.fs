namespace Imperium.Terminal

type ProcessMailboxMessage<'msg> = 'msg -> Async<unit>
type MailboxErrorHandler<'msg> = 'msg -> exn -> Async<unit>

[<RequireQualifiedAccess>]
module SupervisedMailbox =

    /// Starts a mailbox that keeps processing messages after individual failures.
    let start<'msg>
        (processMessage: ProcessMailboxMessage<'msg>)
        (onError: MailboxErrorHandler<'msg>)
        : MailboxProcessor<'msg> =
        MailboxProcessor.Start(fun inbox ->
            let rec loop () =
                async {
                    let! msg = inbox.Receive()

                    try
                        do! processMessage msg
                    with ex ->
                        try
                            do! onError msg ex
                        with _ ->
                            ()

                    return! loop ()
                }

            loop ())
