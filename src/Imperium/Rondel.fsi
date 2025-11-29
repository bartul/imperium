namespace Imperium

open System
open Imperium.Gameplay
open Imperium.Economy

module Rondel =

    open Imperium.Primitives
    
    // Minimal public aliases used by the Rondel API surface
    type RondelError = string
    /// Opaque identifier for invoices scoped to the rondel domain.
    [<Struct>]
    type RondelInvoiceId = private RondelInvoiceId of Id
    module RondelInvoiceId =
        val create : Guid -> Result<RondelInvoiceId, string>
        val newId : unit -> RondelInvoiceId
        val value : RondelInvoiceId -> Guid
        val toString : RondelInvoiceId -> string
        val tryParse : string -> Result<RondelInvoiceId, string>
        val value : RondelInvoiceId -> Guid
        val toString : RondelInvoiceId -> string

    /// Abstract, opaque handle representing an instance of a Rondel that
    /// maintains its own internal state (nation positions, pending moves).
    type Rondel

    /// Discriminated union naming the fixed spaces of the Rondel. Callers use
    /// this to indicate desired action; the Rondel resolves it to the internal
    /// ordered space layout. Each case is a unique rondel position (two Maneuver,
    /// two Production slots on the board).
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

    /// Actions that can occur when landing on a rondel space. The "One/Two" spaces
    /// map to the same action.
    [<RequireQualifiedAccess>]
    type Action =
        | Investor
        | Import
        | Production
        | Maneuver
        | Taxation
        | Factory
    
    /// Events produced by the Rondel public API. Currently only signals initial creation;
    /// movement and invoicing flows are intentionally deferred.
    type Event =
        | RondelCreated
        | NationMovementInvoiced of nationId:NationId * invoiceId:RondelInvoiceId * amount:Amount
        | NationActionDetermined of nationId:NationId * action:Action

    // Public API (minimal surface)
    /// Create a new Rondel instance with the game's fixed space layout and the set
    /// of nations that will occupy it. Returns error if nation set is empty.
    val createRondel : nations:Set<NationId> -> Result<(Rondel * Event list), RondelError>

    /// Initiate a move for `nationId` to the named `space` on the provided `rondel`.
    /// Returns future movement events once implemented.
    val move : rondel:Rondel -> nationId:NationId -> space:Space -> Result<Event list, RondelError>

    /// Handle an invoice-paid event published by another domain.
    val onInvoicedPaid : rondel:Rondel -> invoiceId:RondelInvoiceId -> Result<Event list, RondelError>

    /// Handle an invoice-payment-failed event from another domain; future implementation
    /// may adjust state accordingly.
    val onInvoicePaymentFailed : rondel:Rondel -> invoiceId:RondelInvoiceId -> Result<Event list, RondelError>
