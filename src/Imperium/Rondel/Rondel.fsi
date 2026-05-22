namespace Imperium.Rondel

/// Public facade for the Rondel bounded context.
/// Routes commands, inbound events, and queries to internal handlers.
[<RequireQualifiedAccess>]
module Rondel =

    /// Execute a rondel command. Routes to the appropriate command handler and commits its effects.
    /// CancellationToken flows implicitly through Async context.
    /// Throws if command execution fails (e.g., invalid state, persistence failure).
    val execute: RondelDependencies -> RondelCommand -> Async<unit>

    /// Handle an inbound event from other bounded contexts. Routes to the appropriate event handler and commits its effects.
    /// CancellationToken flows implicitly through Async context.
    val handle: RondelDependencies -> RondelInboundEvent -> Async<unit>

    /// Get nation positions for a game. Returns None if game not found.
    val getNationPositions: RondelQueryDependencies -> GetNationPositionsQuery -> Async<RondelPositionsView option>

    /// Get rondel overview for a game. Returns None if game not found.
    val getRondelOverview: RondelQueryDependencies -> GetRondelOverviewQuery -> Async<RondelView option>
