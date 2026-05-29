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

/// Canonical nation identity participating in the game.
[<RequireQualifiedAccess>]
type NationId =
    | Germany
    | GreatBritain
    | France
    | Russia
    | AustriaHungary
    | Italy

/// Nation identity construction and formatting helpers.
module NationId =
    /// All canonical nations supported by the game.
    val internal all: Set<NationId>

    /// Convert a canonical nation identity to its display name.
    val toString: NationId -> string

    /// Parse a canonical nation identity from a display name.
    val tryParse: string -> Result<NationId, string>

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

/// Validated roster of distinct players taking part in a game.
type PlayerRoster = private PlayerRoster of Set<PlayerId>

/// Player roster construction and access helpers.
module PlayerRoster =
    /// Build a player roster from player Guids, validating count and uniqueness.
    val create: System.Guid list -> Result<PlayerRoster, string>

    /// Return the distinct players in the roster.
    val value: PlayerRoster -> Set<PlayerId>
