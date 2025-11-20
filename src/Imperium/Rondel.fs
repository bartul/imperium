namespace Imperium

open System

module Rondel =

    // Public aliases and types (moved here so the project compiles without a
    // separate signature file). These mirror the API previously declared in
    // `Rondel.fsi` and are intentionally minimal; the implementation will be
    // completed later.
    type PlayerId = Guid
    type SlotIndex = int
    type Amount = int
    type Error = string
    type InvoiceId = Guid

    // Opaque Rondel handle (private record for now).
    type Rondel = private { dummy: unit }

    type Space =
        | Income
        | Build
        | MoveArmy
        | Upgrade
        | Market
        | Diplomacy
        | Invest
        | Pass

    type Event =
        | MoveAnnounced of PlayerId * Space * InvoiceId option
        | InvoiceIssued of InvoiceId * PlayerId * Amount
        | InvoicePaid of InvoiceId * PlayerId * Amount
        | MoveCompleted of PlayerId * Space
        | MoveCancelled of PlayerId * Space * string
        | MoveFailed of PlayerId * Space * string

    // Implementation stubs
    let createRondel () : Rondel = { dummy = () }

    let move (rondel:Rondel) (playerId:PlayerId) (space:Space) : Result<Event list, Error> =
        invalidOp "Not implemented: move"

    let onInvoicedPaid (rondel:Rondel) (invoiceId:InvoiceId) : Result<Event list, Error> =
        invalidOp "Not implemented: onInvoicedPaid"
