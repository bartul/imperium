namespace Imperium

open System

module Rondel =

    // Public aliases and types kept in sync with the signature file; implementation
    // is intentionally stubbed for now.
    type NationId = Guid
    type Amount = int
    type Error = string
    type InvoiceId = Guid

    // Opaque Rondel handle (private record for now).
    type Rondel = private { dummy: unit }

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

    [<RequireQualifiedAccess>]
    type Action =
        | Investor
        | Import
        | Production
        | Maneuver
        | Taxation
        | Factory

    type Event =
        | RondelCreated
        | NationActionDetermined of NationId * Action

    // Implementation stubs
    let createRondel (nations: Set<NationId>) : Rondel =
        let _ = nations
        { dummy = () }

    let move (rondel: Rondel) (nationId: NationId) (space: Space) : Result<Event list, Error> =
        invalidOp "Not implemented: move"

    let onInvoicedPaid (rondel: Rondel) (invoiceId: InvoiceId) : Result<Event list, Error> =
        invalidOp "Not implemented: onInvoicedPaid"
