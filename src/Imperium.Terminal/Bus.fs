namespace Imperium.Terminal

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
    let create () : IBus = failwith "Not implemented"
