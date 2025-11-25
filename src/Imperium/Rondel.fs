namespace Imperium

open Imperium.Gameplay
open Imperium.Economy

module Rondel =

    // Public aliases and types kept in sync with the signature file; implementation
    // is intentionally stubbed for now.
    type Error = string
    // Opaque identifier for invoices scoped to the rondel domain.
    type RondelInvoiceId = Guid

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
        | NationMovementInvoiced of NationId * RondelInvoiceId * Amount
        | NationActionDetermined of NationId * Action

    // Implementation stubs
    let createRondel (nations: Set<NationId>) : Rondel =
        let _ = nations
        { dummy = () }

    let move (rondel: Rondel) (nationId: NationId) (space: Space) : Result<Event list, Error> =
        invalidOp "Not implemented: move"

    let onInvoicedPaid (rondel: Rondel) (invoiceId: RondelInvoiceId) : Result<Event list, Error> =
        invalidOp "Not implemented: onInvoicedPaid"

    let onInvoicePaymentFailed (rondel: Rondel) (invoiceId: RondelInvoiceId) : Result<Event list, Error> =
        invalidOp "Not implemented: onInvoicePaymentFailed"
