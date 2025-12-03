namespace Imperium

open Imperium.Contract.Accounting
open Imperium.Contract.Rondel

module Rondel =

    // Public API

    /// Command: Initialize rondel for the specified game with the given nations.
    /// All nations are positioned at their starting positions.
    /// Publishes PositionedAtStart integration event. Fails if nation set is empty.
    val setToStartingPositions: SetToStartingPositions

    /// Command: Move a nation to the specified space on the rondel.
    /// Determines movement cost and charges via injected Accounting dependency.
    /// Integration event ActionDetermined published based on payment requirement.
    val move: ChargeNationForRondelMovement -> Move

    /// Event handler: Processes invoice payment confirmation from Accounting domain.
    /// Completes the nation's movement and publishes ActionDetermined integration event.
    val onInvoicedPaid: RondelInvoicePaid -> Result<unit, string>

    /// Event handler: Processes invoice payment failure from Accounting domain.
    /// Rejects the movement and publishes MoveToActionSpaceRejected integration event.
    val onInvoicePaymentFailed: RondelInvoicePaymentFailed -> Result<unit, string>
