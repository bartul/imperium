namespace Imperium.Rondel

open Imperium
open Imperium.Primitives

// ──────────────────────────────────────────────────────────────────────────
// Domain State
// ──────────────────────────────────────────────────────────────────────────

/// Persistent state for a game's rondel, tracking nation positions and pending movements.
type RondelState =
    {
        GameId: Id
        /// Maps nation name to current position. None indicates starting position (not yet moved).
        NationPositions: Map<string, Space option>
        /// Maps nation name to pending paid movement awaiting payment confirmation.
        PendingMovements: Map<string, PendingMovement>
    }

/// A movement awaiting payment confirmation from the Accounting domain.
and PendingMovement = { Nation: string; TargetSpace: Space; BillingId: RondelBillingId }

/// Transforms Domain PendingMovement to/from a Contract type for persistence.
module PendingMovement =
    /// Convert domain pending movement to serializable contract representation.
    val toContract: PendingMovement -> Contract.Rondel.PendingMovement
    /// Reconstruct domain pending movement from contract representation.
    /// Returns Error if Space name or BillingId is invalid.
    val fromContract: Contract.Rondel.PendingMovement -> Result<PendingMovement, string>

/// Mechanical rondel state construction/update helpers plus contract transformations.
module RondelState =
    /// Create initial rondel state for a game with the participating nations at their starting positions.
    /// Raises if the nation set is empty.
    val create: Id -> Set<string> -> RondelState

    /// Update the current position for an existing nation.
    /// Raises if the nation is not present in the state.
    val withNationPosition: string -> Space -> RondelState -> RondelState

    /// Update current positions for existing nations.
    /// Raises if any nation is not present in the state.
    val withNationPositions: seq<string * Space> -> RondelState -> RondelState

    /// Add or replace a pending move for an existing nation.
    /// Raises if the nation is not present in the state.
    val withPendingMove: string -> Space -> RondelBillingId -> RondelState -> RondelState

    /// Remove a pending move for an existing nation.
    /// Raises if the nation is not present in the state.
    val withoutPendingMove: string -> RondelState -> RondelState

    /// Convert domain state to serializable contract representation.
    val toContract: RondelState -> Contract.Rondel.RondelState
    /// Reconstruct domain state from contract representation.
    /// Returns Error if Space names or BillingIds are invalid.
    val fromContract: Contract.Rondel.RondelState -> Result<RondelState, string>

// ──────────────────────────────────────────────────────────────────────────
// Dependencies
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

/// Load rondel state for queries. Same as write-side Load.
/// Infrastructure may optimize with dedicated read store.
type LoadRondelStateForQuery = Id -> Async<RondelState option>

/// Dependencies for query handlers.
type RondelQueryDependencies = { Load: LoadRondelStateForQuery }
