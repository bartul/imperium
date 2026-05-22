namespace Imperium.Rondel

open System
open Imperium.Primitives

// ──────────────────────────────────────────────────────────────────────────
// Value Types & Enumerations
// ──────────────────────────────────────────────────────────────────────────

/// Opaque identifier linking a rondel movement to its accounting charge.
/// Used to correlate payment confirmations with pending movements.
[<Struct>]
type RondelBillingId = private RondelBillingId of Id

module RondelBillingId =
    /// Extract the underlying Guid value for comparison and serialization.
    val value: RondelBillingId -> Guid

    /// Create a RondelBillingId from an Id.
    val ofId: Id -> RondelBillingId

    /// Assembly-internal: validate a raw Guid and build a billing id.
    val internal create: (Guid -> Result<RondelBillingId, string>)

    /// Assembly-internal: mint a new billing id.
    val internal newId: unit -> RondelBillingId

/// The six distinct actions a nation can perform on the rondel.
/// Each action corresponds to one or two spaces on the circular track.
[<RequireQualifiedAccess>]
type Action =
    | Investor
    | Import
    | Production
    | Maneuver
    | Taxation
    | Factory

module Action =
    /// Assembly-internal: serialise an action to its persisted string form.
    val internal toString: Action -> string

/// The eight spaces on the rondel wheel, arranged clockwise.
/// Production and Maneuver each appear twice on the track.
[<RequireQualifiedAccess>]
type Space =
    | Investor
    | Import
    | ProductionOne
    | ManeuverOne
    | Taxation
    | Factory
    | ProductionTwo
    | ManeuverTwo

module Space =
    /// Maps a rondel space to its corresponding action.
    val toAction: Space -> Action

    /// Assembly-internal: clockwise distance between two spaces (0..7).
    val internal distance: Space -> Space -> int

    /// Assembly-internal: serialise a space to its persisted string form.
    val internal toString: Space -> string

    /// Assembly-internal: parse a space from its persisted string form.
    val internal fromString: string -> Result<Space, string>

// ──────────────────────────────────────────────────────────────────────────
// Queries
// ──────────────────────────────────────────────────────────────────────────

/// Query for nation positions in a game.
type GetNationPositionsQuery = { GameId: Id }

/// Query for basic rondel overview.
type GetRondelOverviewQuery = { GameId: Id }

// ──────────────────────────────────────────────────────────────────────────
// Query Results
// ──────────────────────────────────────────────────────────────────────────

/// Result of GetNationPositions query.
type RondelPositionsView = { GameId: Id; Positions: NationPositionView list }

/// A nation's position on the rondel.
and NationPositionView = { Nation: string; CurrentSpace: Space option; PendingSpace: Space option }

/// Result of GetRondelOverview query.
type RondelView = { GameId: Id; NationNames: string list }
