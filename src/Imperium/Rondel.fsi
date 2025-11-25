namespace Imperium

open Imperium.Gameplay
open Imperium.Economy

module Rondel =

    // Minimal public aliases used by the Rondel API surface
    type Error = string
    /// Opaque identifier for invoices scoped to the rondel domain.
    type RondelInvoiceId = Guid

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
        | NationMovementInvoiced of NationId * RondelInvoiceId * Amount
        | NationActionDetermined of NationId * Action

    // Public API (minimal surface)
    /// Create a new Rondel instance with the game's fixed space layout and the set
    /// of nations that will occupy it.
    val createRondel : nations:Set<NationId> -> Rondel

    /// Initiate a move for `nationId` to the named `space` on the provided `rondel`.
    /// Returns future movement events once implemented.
    val move : rondel:Rondel -> nationId:NationId -> space:Space -> Result<Event list, Error>

    /// Handle an invoice-paid event published by another domain.
    val onInvoicedPaid : rondel:Rondel -> invoiceId:RondelInvoiceId -> Result<Event list, Error>

    /// Handle an invoice-payment-failed event from another domain; future implementation
    /// may adjust state accordingly.
    val onInvoicePaymentFailed : rondel:Rondel -> invoiceId:RondelInvoiceId -> Result<Event list, Error>
