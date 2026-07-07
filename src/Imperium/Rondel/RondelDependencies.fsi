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

// ──────────────────────────────────────────────────────────────────────────
// Read-Side Dependencies
// ──────────────────────────────────────────────────────────────────────────

/// Load rondel state for queries. Same as write-side Load.
/// Infrastructure may optimize with dedicated read store.
type LoadRondelStateForQuery = Id -> Async<RondelState option>

/// Dependencies for query handlers.
type RondelQueryDependencies = { Load: LoadRondelStateForQuery }
