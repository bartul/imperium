namespace Imperium

open System

module Rondel =

    // Minimal public aliases used by the Rondel API surface
    type PlayerId = Guid
    type SlotIndex = int
    type Amount = int
    type Error = string

    /// Identifier for invoices created by the rondel (payment handled externally).
    type InvoiceId = Guid

    /// Abstract, opaque handle representing an instance of a Rondel that
    /// maintains its own internal state (player positions, pending moves, invoices).
    type Rondel

    /// Discriminated union naming the fixed spaces of the Rondel. Callers use
    /// this to indicate desired action; the Rondel resolves it to the internal
    /// ordered space layout.
    type Space =
        | Income
        | Build
        | MoveArmy
        | Upgrade
        | Market
        | Diplomacy
        | Invest
        | Pass

    /// Events produced by the Rondel public API. These are the only types
    /// consumers need to handle to integrate with the payment and financial systems.
    type Event =
        | MoveAnnounced of PlayerId * Space * InvoiceId option
        | InvoiceIssued of InvoiceId * PlayerId * Amount
        | InvoicePaid of InvoiceId * PlayerId * Amount
        | MoveCompleted of PlayerId * Space
        | MoveCancelled of PlayerId * Space * string
        | MoveFailed of PlayerId * Space * string

    // Public API (minimal surface)
    /// Create a new Rondel instance with the game's fixed space layout.
    val createRondel : unit -> Rondel

    /// Initiate a move for `playerId` to the named `space` on the provided `rondel`.
    /// - If the move is free the rondel will apply it immediately and return events.
    /// - If the move requires invoicing the rondel will create a pending move and
    ///   emit an `InvoiceIssued` event; payment is handled externally.
    val move : rondel:Rondel -> playerId:PlayerId -> space:Space -> Result<Event list, Error>

    /// Called by the external payment subsystem when an invoice is settled. The
    /// rondel will complete any pending move associated with `invoiceId` and
    /// return the events produced by completion (including `MoveCompleted`).
    val onInvoicedPaid : rondel:Rondel -> invoiceId:InvoiceId -> Result<Event list, Error>
