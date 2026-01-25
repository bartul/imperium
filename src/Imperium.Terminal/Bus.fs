module Imperium.Terminal.Bus

open System

// ──────────────────────────────────────────────────────────────────────────
// Types
// ──────────────────────────────────────────────────────────────────────────

/// Cross-cutting event bus for bounded context communication
type Bus =
    { Publish: obj -> Async<unit>
      Register: Type -> (obj -> Async<unit>) -> unit }

// ──────────────────────────────────────────────────────────────────────────
// Factory
// ──────────────────────────────────────────────────────────────────────────

/// Creates a new Bus instance
let create () : Bus =
    failwith "Not implemented"

/// Helper: combine transform + handle, skip on transform error
let subscription
    (transform: 'TContract -> Result<'TDomain, string>)
    (handle: 'TDomain -> Async<unit>)
    : obj -> Async<unit> =
    failwith "Not implemented"

/// Type-safe publish helper
let publish<'T> (bus: Bus) (event: 'T) : Async<unit> =
    failwith "Not implemented"
