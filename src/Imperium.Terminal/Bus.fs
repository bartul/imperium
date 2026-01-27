namespace Imperium.Terminal

open System
open System.Collections.Generic

// ──────────────────────────────────────────────────────────────────────────
// Types
// ──────────────────────────────────────────────────────────────────────────

/// Cross-cutting event bus for bounded context communication
type IBus =
    abstract Publish<'T> : 'T -> Async<unit>
    abstract Subscribe<'T> : ('T -> Async<unit>) -> unit

// ──────────────────────────────────────────────────────────────────────────
// Factory
// ──────────────────────────────────────────────────────────────────────────

module Bus =

    /// Creates a new IBus instance
    let create () : IBus =
        let handlers = Dictionary<Type, ResizeArray<obj -> Async<unit>>>()

        { new IBus with
            member _.Publish<'T>(event: 'T) =
                async {
                    match handlers.TryGetValue(typeof<'T>) with
                    | true, handlerList ->
                        for handler in handlerList do
                            do! handler (box event)
                    | false, _ -> ()
                }

            member _.Subscribe<'T>(handler: 'T -> Async<unit>) =
                let eventType = typeof<'T>

                if not (handlers.ContainsKey eventType) then
                    handlers.[eventType] <- ResizeArray<obj -> Async<unit>>()

                handlers.[eventType].Add(fun obj -> handler (unbox<'T> obj)) }
