namespace Imperium.Rondel

open Imperium.Primitives

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

// ──────────────────────────────────────────────────────────────────────────
// Query Handlers
// ──────────────────────────────────────────────────────────────────────────

/// Internal query handlers for the Rondel bounded context.
/// Routers in the public Rondel facade delegate to these.
module internal Queries =
    /// Get nation positions for a game. Returns None if game not found.
    val getNationPositions: RondelQueryDependencies -> GetNationPositionsQuery -> Async<RondelPositionsView option>

    /// Get rondel overview for a game. Returns None if game not found.
    val getRondelOverview: RondelQueryDependencies -> GetRondelOverviewQuery -> Async<RondelView option>
