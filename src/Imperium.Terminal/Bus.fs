namespace Imperium.Terminal

open System
open System.Collections.Concurrent
open System.Threading

// ──────────────────────────────────────────────────────────────────────────
// Types
// ──────────────────────────────────────────────────────────────────────────

// Cross-cutting event bus for bounded context communication
type IBus =
    abstract Publish<'T> : 'T -> Async<unit>
    abstract Subscribe<'T> : ('T -> Async<unit>) -> unit

type private HandlerStore<'T>() =
    let gate = obj ()
    let mutable handlers: ('T -> Async<unit>) list = []

    member _.Snapshot() : ('T -> Async<unit>) list = Volatile.Read(&handlers)

    member _.Subscribe(handler: 'T -> Async<unit>) : unit =
        lock gate (fun () -> Volatile.Write(&handlers, handler :: handlers))

// ──────────────────────────────────────────────────────────────────────────
// Factory
// ──────────────────────────────────────────────────────────────────────────

module Bus =

    /// Creates a new IBus instance
    /// Uses per-type immutable handler snapshots to make publish safe under concurrent subscription.
    let create () : IBus =
        let handlers = ConcurrentDictionary<Type, obj>()

        let invokeSafely handler event =
            async {
                try
                    do! handler event
                with _ ->
                    ()
            }

        { new IBus with
            member _.Publish<'T>(event: 'T) =
                async {
                    match handlers.TryGetValue(typeof<'T>) with
                    | true, store ->
                        let typedStore = store :?> HandlerStore<'T>

                        for handler in typedStore.Snapshot() do
                            do! invokeSafely handler event
                    | false, _ -> ()
                }

            member _.Subscribe<'T>(handler: 'T -> Async<unit>) =
                let typedStore =
                    handlers.GetOrAdd(typeof<'T>, fun _ -> HandlerStore<'T>() :> obj) :?> HandlerStore<'T>

                typedStore.Subscribe(handler) }
