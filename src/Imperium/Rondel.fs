namespace Imperium

open System
open Imperium.Gameplay
open Imperium.Economy

module Rondel =

    // Public aliases and types kept in sync with the signature file; implementation
    // is intentionally stubbed for now.
    type RondelError = string
    // Opaque identifier for invoices scoped to the rondel domain.
    [<Struct>]
    type RondelInvoiceId = private RondelInvoiceId of Guid

    module RondelInvoiceId =
        let create guid =
            if guid = Guid.Empty then
                RondelError "RondelInvoiceId cannot be Guid.Empty." |> Error
            else
               RondelInvoiceId guid |> Ok

        let newId () = Guid.NewGuid() |> RondelInvoiceId
        let value (RondelInvoiceId g) = g
        let toString (RondelInvoiceId g) = g.ToString()

        let tryParse (raw: string) =
            match Guid.TryParse raw with
            | true, guid -> create guid
            | false, _ -> RondelError "Invalid GUID format." |> Error

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
