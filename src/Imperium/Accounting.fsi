namespace Imperium

open Imperium.Primitives

module Accounting =

    // ──────────────────────────────────────────────────────────────────────────
    // Commands
    // ──────────────────────────────────────────────────────────────────────────

    /// Union of all accounting commands for routing and dispatch.
    type AccountingCommand =
        | ChargeNationForRondelMovement of ChargeNationForRondelMovementCommand
        | VoidRondelCharge of VoidRondelChargeCommand

    /// Command to charge a nation for rondel movement.
    and ChargeNationForRondelMovementCommand =
        { GameId: Id
          Nation: string
          Amount: Amount
          BillingId: Id }

    /// Command to void a previously initiated rondel charge.
    and VoidRondelChargeCommand = { GameId: Id; BillingId: Id }

    // ──────────────────────────────────────────────────────────────────────────
    // Events
    // ──────────────────────────────────────────────────────────────────────────

    /// Integration events published by Accounting to notify other bounded contexts.
    type AccountingEvent =
        | RondelInvoicePaid of RondelInvoicePaidEvent
        | RondelInvoicePaymentFailed of RondelInvoicePaymentFailedEvent

    /// Invoice payment succeeded.
    and RondelInvoicePaidEvent = { GameId: Id; BillingId: Id }

    /// Invoice payment failed.
    and RondelInvoicePaymentFailedEvent = { GameId: Id; BillingId: Id }

    // ──────────────────────────────────────────────────────────────────────────
    // Dependencies
    // ──────────────────────────────────────────────────────────────────────────

    /// Publish accounting domain events to the event bus.
    /// CancellationToken flows implicitly through Async context.
    type PublishAccountingEvent = AccountingEvent -> Async<unit>

    /// Unified dependencies for all Accounting handlers.
    type AccountingDependencies = { Publish: PublishAccountingEvent }

    // ──────────────────────────────────────────────────────────────────────────
    // Transformations (Contract <-> Domain)
    // ──────────────────────────────────────────────────────────────────────────

    /// Transforms Contract ChargeNationForRondelMovementCommand to a Domain type.
    module ChargeNationForRondelMovementCommand =
        /// Validate and transform Contract command to Domain command.
        /// Returns Error if GameId or BillingId is invalid.
        val fromContract:
            Contract.Accounting.ChargeNationForRondelMovementCommand ->
                Result<ChargeNationForRondelMovementCommand, string>

    /// Transforms Contract VoidRondelChargeCommand to a Domain type.
    module VoidRondelChargeCommand =
        /// Validate and transform Contract command to Domain command.
        /// Returns Error if GameId or BillingId is invalid.
        val fromContract: Contract.Accounting.VoidRondelChargeCommand -> Result<VoidRondelChargeCommand, string>

    /// Transforms Domain AccountingEvent to a Contract type for publication.
    module AccountingEvent =
        /// Transform Domain event to Contract event for cross-boundary communication.
        val toContract: AccountingEvent -> Contract.Accounting.AccountingEvent

    // ──────────────────────────────────────────────────────────────────────────
    // Handlers
    // ──────────────────────────────────────────────────────────────────────────

    /// Execute an accounting command. Routes to the appropriate command handler.
    /// CancellationToken flows implicitly through Async context.
    val execute: AccountingDependencies -> AccountingCommand -> Async<unit>
