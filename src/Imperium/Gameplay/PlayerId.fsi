namespace Imperium.Gameplay

open Imperium.Primitives

// ──────────────────────────────────────────────────────────────────────────
// Domain Values
// ──────────────────────────────────────────────────────────────────────────

/// Player identity within Gameplay.
[<Struct>]
type PlayerId = private PlayerId of Id

/// Player identity helpers.
module PlayerId =
    /// Create a Gameplay player id from a non-empty Guid.
    val create: System.Guid -> Result<PlayerId, string>

    /// Create a new Gameplay player id.
    val newId: unit -> PlayerId

    /// Unwrap the underlying Guid value.
    val value: PlayerId -> System.Guid

    /// Convert the player id to its canonical string representation.
    val toString: PlayerId -> string

    /// Parse a Gameplay player id from string.
    val tryParse: string -> Result<PlayerId, string>
