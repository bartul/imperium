namespace Imperium.Rondel

open Imperium.Primitives

// ──────────────────────────────────────────────────────────────────────────
// Billing Identity
// ──────────────────────────────────────────────────────────────────────────

[<Struct>]
type RondelBillingId = private RondelBillingId of Id

module RondelBillingId =
    let create = Id.createMap RondelBillingId
    let newId () = Id.newId () |> RondelBillingId
    let value (RondelBillingId g) = g |> Id.value
    let ofId (id: Id) = RondelBillingId id
