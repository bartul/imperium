namespace Imperium.Rondel

open System
open Imperium.Primitives

// ──────────────────────────────────────────────────────────────────────────
// Billing Identity
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
