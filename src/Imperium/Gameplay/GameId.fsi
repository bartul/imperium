namespace Imperium.Gameplay

open Imperium.Primitives

// ──────────────────────────────────────────────────────────────────────────
// Domain Values
// ──────────────────────────────────────────────────────────────────────────

/// Gameplay-owned game identity.
[<Struct>]
type GameId = private GameId of Id

/// Game identity helpers.
module GameId =
    /// Create a Gameplay game id from a non-empty Guid.
    val create: System.Guid -> Result<GameId, string>

    /// Create a new Gameplay game id.
    val newId: unit -> GameId

    /// Unwrap the underlying Guid value.
    val value: GameId -> System.Guid

    /// Convert the game id to its canonical string representation.
    val toString: GameId -> string

    /// Parse a Gameplay game id from string.
    val tryParse: string -> Result<GameId, string>
