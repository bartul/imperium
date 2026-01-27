namespace Imperium.Terminal

open System
open System.Collections.Concurrent

// ──────────────────────────────────────────────────────────────────────────
// Types
// ──────────────────────────────────────────────────────────────────────────

// Cross-cutting event bus for bounded context communication
type IBus =
    abstract Publish<'T> : 'T -> Async<unit>
    abstract Subscribe<'T> : ('T -> Async<unit>) -> unit

// ──────────────────────────────────────────────────────────────────────────
// Factory
// ──────────────────────────────────────────────────────────────────────────

module Bus =

    /// Creates a new IBus instance
    /// Uses typed handler lists to avoid boxing events on publish
    let create () : IBus =
        let handlers = ConcurrentDictionary<Type, obj>()

        { new IBus with
            member _.Publish<'T>(event: 'T) =
                async {
                    match handlers.TryGetValue(typeof<'T>) with
                    | true, list ->
                        let typedList = list :?> ResizeArray<'T -> Async<unit>>

                        for handler in typedList do
                            do! handler event
                    | false, _ -> ()
                }

            member _.Subscribe<'T>(handler: 'T -> Async<unit>) =
                let list =
                    handlers.GetOrAdd(typeof<'T>, fun _ -> ResizeArray<'T -> Async<unit>>() :> obj)
                    :?> ResizeArray<'T -> Async<unit>>

                list.Add(handler) }
