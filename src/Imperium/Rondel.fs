namespace Imperium

open System
open Imperium.Gameplay
open Imperium.Economy

module Rondel =
    open Imperium.Primitives

    // Public aliases and types kept in sync with the signature file; implementation
    // is intentionally stubbed for now.
    type RondelError = string
    // Opaque identifier for invoices scoped to the rondel domain.
    [<Struct>]
    type RondelInvoiceId = private RondelInvoiceId of Id

    module RondelInvoiceId =
        let create guid = guid |> Id.createMap RondelInvoiceId
        let newId () = Id.newId () |> RondelInvoiceId
        let value (RondelInvoiceId g) = g |> Id.value
        let toString (RondelInvoiceId g) = g |> Id.toString
        let tryParse raw = raw |> Id.tryParseMap RondelInvoiceId

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
        | NationMovementInvoiced of nationId:NationId * invoiceId:RondelInvoiceId * amount:Amount
        | NationActionDetermined of nationId:NationId * action:Action

    // Implementation stubs
    let createRondel (nations: Set<NationId>) : Result<(Rondel * Event list), RondelError> =
       invalidOp "Not implemented: createRondel" 

    let move (rondel: Rondel) (nationId: NationId) (space: Space) : Result<Event list, RondelError> =
        invalidOp "Not implemented: move"

    let onInvoicedPaid (rondel: Rondel) (invoiceId: RondelInvoiceId) : Result<Event list, RondelError> =
        invalidOp "Not implemented: onInvoicedPaid"

    let onInvoicePaymentFailed (rondel: Rondel) (invoiceId: RondelInvoiceId) : Result<Event list, RondelError> =
        invalidOp "Not implemented: onInvoicePaymentFailed"
