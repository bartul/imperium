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
    /// Wrap an already validated shared Id as a Gameplay player id.
    val create: Id -> PlayerId

    /// Unwrap the shared Id value.
    val value: PlayerId -> Id

/// Validated roster of players taking part in a game.
type PlayerRoster = private PlayerRoster of PlayerId list

/// Player roster construction and access helpers.
module PlayerRoster =
    /// Build a player roster from validated shared Id values.
    val create: Id list -> Result<PlayerRoster, string>

    /// Return player ids in roster order.
    val value: PlayerRoster -> PlayerId list
