namespace Imperium

open System
open Imperium.Contract.Accounting
open Imperium.Contract.Rondel

module Rondel =

    // Public API

    /// Command: Initialize rondel for the specified game with the given nations.
    /// All nations are positioned at their starting positions.
    /// Publishes PositionedAtStart event internally. Fails if nation set is empty.
    val setToStartPositions : command:SetToStartPositionsCommand -> Result<unit, string>

    /// Command: Move a nation to the specified space on the rondel.
    /// Determines movement cost and issues invoice to Accounting domain.
    /// Events published internally upon completion.
    val move : chargeForMovement:ChargeNationForRondelMovement -> command:MoveCommand -> Result<unit, string>

    /// Event handler: Processes invoice payment confirmation from Accounting domain.
    /// Completes the nation's movement and publishes ActionDetermined event internally.
    val onInvoicedPaid : event:RondelInvoicePaid -> Result<unit, string>
    
    /// Event handler: Processes invoice payment failure from Accounting domain.
    /// Rejects the movement and publishes MovementToActionRejected event internally.
    val onInvoicePaymentFailed : event:RondelInvoicePaymentFailed -> Result<unit, string>