namespace Imperium.Rondel

open Imperium.Primitives

// ──────────────────────────────────────────────────────────────────────────
// Write-Side Dependencies
// ──────────────────────────────────────────────────────────────────────────

/// Load rondel state by GameId. Returns None if game not initialized.
/// CancellationToken flows implicitly through Async context.
type LoadRondelState = Id -> Async<RondelState option>

/// Unified dependencies for all Rondel handlers.
/// Load resolves current state; Commit durably applies the resulting effects
/// (state, integration events, outbound commands) as an atomic unit.
type RondelDependencies = { Load: LoadRondelState; Commit: CommitRondelEffects }

/// Named effect shape returned by Rondel handlers.
/// Represents the side effects of a single command/event:
/// optional new state, published integration events, and outbound commands.
and RondelEffects =
    { State: RondelState option; IntegrationEvents: RondelEvent list; OutboundCommands: RondelOutboundCommand list }

/// Commit boundary for Rondel effects.
/// Infrastructure-owned function that durably applies state, publishes events,
/// and dispatches outbound commands as an atomic unit.
/// Failures propagate as exceptions (Async&lt;unit&gt; semantics).
and CommitRondelEffects = RondelEffects -> Async<unit>

// ──────────────────────────────────────────────────────────────────────────
// Read-Side Dependencies
// ──────────────────────────────────────────────────────────────────────────

/// Load rondel state for queries. Same as write-side Load.
/// Infrastructure may optimize with dedicated read store.
type LoadRondelStateForQuery = Id -> Async<RondelState option>

/// Dependencies for query handlers.
type RondelQueryDependencies = { Load: LoadRondelStateForQuery }
