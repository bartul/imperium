namespace Imperium.Contract

// Contract types for Accounting bounded context communication.
// Intentionally public - no .fsi file needed for infrastructure layer.

module Accounting =
    open System
    open Imperium.Primitives

    // Commands

    /// Charge a nation for rondel movement.
    type ChargeNationForRondelMovement = ChargeNationForRondelMovementCommand -> Result<unit, string>

    and ChargeNationForRondelMovementCommand =
        { GameId: Guid
          Nation: string
          Amount: Amount
          BillingId: Guid }

    /// Void a previously initiated rondel charge before payment completion.
    type VoidRondelCharge = VoidRondelChargeCommand -> Result<unit, string>
    and VoidRondelChargeCommand = { GameId: Guid; BillingId: Guid }

    /// Union of all commands that can be dispatched to Accounting bounded context.
    /// Used for infrastructure routing and dispatch.
    type AccountingCommand =
        | ChargeNationForRondelMovement of ChargeNationForRondelMovementCommand
        | VoidRondelCharge of VoidRondelChargeCommand

    // Events
    /// Integration events published by Accounting domain to notify other bounded contexts of payment outcomes.
    type AccountingEvent =
        | RondelInvoicePaid of RondelInvoicePaid
        | RondelInvoicePaymentFailed of RondelInvoicePaymentFailed

    /// Invoice payment succeeded.
    and RondelInvoicePaid = { GameId: Guid; BillingId: Guid }
    /// Invoice payment failed due to insufficient funds or validation error.
    and RondelInvoicePaymentFailed = { GameId: Guid; BillingId: Guid }
